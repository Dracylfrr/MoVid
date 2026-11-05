import test from 'node:test';
import assert from 'node:assert/strict';
import {DatabaseSync} from 'node:sqlite';
import {readFileSync} from 'node:fs';
import {createService} from './worker.mjs';
class D1 {
 constructor(){this.db=new DatabaseSync(':memory:');this.db.exec(readFileSync(new URL('./schema.sql',import.meta.url),'utf8'));}
 prepare(sql){const db=this.db;let values=[];return{bind(...args){values=args;return this;},async first(){return db.prepare(sql).get(...values)||null;},async run(){return db.prepare(sql).run(...values);}};}
 async batch(commands){return Promise.all(commands.map(c=>c.run()));}
}
function fixture(){
 const emails=[],calls=[],env={DB:new D1(),TMDB_TOKEN:'owner-token',BREVO_KEY:'owner-mail-key',MAIL_FROM:'owner@example.com',PUBLIC_URL:'https://movid-email.example.workers.dev'};
 const service=createService({fetcher:async(url,opts)=>{
   calls.push({url,opts});if(url.includes('brevo.com')){emails.push(JSON.parse(opts.body));return Response.json({messageId:'accepted-'+emails.length});}
   return Response.json({results:[{id:1,name:'Ariana Grande'}]});
 }});
 const request=async(path,data,token)=>{
  const headers={'CF-Connecting-IP':'192.0.2.1'};if(token)headers.Authorization='Bearer '+token;
  if(data)headers['Content-Type']='application/json';
  return service.fetch(new Request(env.PUBLIC_URL+path,{method:data?'POST':'GET',headers,...data?{body:JSON.stringify(data)}:{}}),env);
 };
 const signIn=async(email='person@example.com')=>{
  const started=await request('/auth/start',{email});assert.equal(started.status,200);const {challenge}=await started.json();
  const code=emails.at(-1).subject.match(/\d{6}/)[0];const verified=await request('/auth/verify',{challenge,code});assert.equal(verified.status,200);return verified.json();
 };
 return{env,emails,calls,request,signIn,service};
}
test('Unconfigured service fails honestly and never sends mail',async()=>{
 const f=fixture();delete f.env.BREVO_KEY;assert.equal((await (await f.request('/health')).json()).ready,false);assert.equal((await f.request('/auth/start',{email:'person@example.com'})).status,503);assert.equal(f.emails.length,0);
});
test('Email verification consumes a code, hashes secrets, and creates an authenticated device',async()=>{
 const f=fixture(),auth=await f.signIn();assert.equal(auth.email,'person@example.com');assert.match(auth.token,/^[a-f0-9]{64}$/);
 const stored=f.env.DB.db.prepare('SELECT token_hash FROM sessions').get();assert.notEqual(stored.token_hash,auth.token);
 const me=await f.request('/account',null,auth.token);assert.equal(me.status,200);assert.equal((await me.json()).email,auth.email);
 assert.equal(f.env.DB.db.prepare('SELECT count(*) n FROM challenges').get().n,0);
});
test('Wrong codes stop after five tries and cannot be replayed',async()=>{
 const f=fixture(),res=await f.request('/auth/start',{email:'p@example.com'}),{challenge}=await res.json(),real=f.emails[0].subject.match(/\d{6}/)[0];
 const wrong=real==='000000'?'000001':'000000';for(let i=0;i<5;i++)assert.equal((await f.request('/auth/verify',{challenge,code:wrong})).status,401);
 assert.equal((await f.request('/auth/verify',{challenge,code:real})).status,401);
});
test('Catalog proxy requires sign-in, restricts paths, and keeps provider keys server-side',async()=>{
 const f=fixture();assert.equal((await f.request('/catalog?path='+encodeURIComponent('/search/person?query=Ariana'))).status,401);
 const auth=await f.signIn(),res=await f.request('/catalog?path='+encodeURIComponent('/search/person?query=Ariana'),null,auth.token);assert.equal(res.status,200);
 assert.equal((await res.text()).includes('owner-token'),false);assert.equal((await f.request('/catalog?path='+encodeURIComponent('/account'),null,auth.token)).status,400);
 assert.equal((await f.request('/catalog?path='+encodeURIComponent('/movie/1?api_key=stolen'),null,auth.token)).status,400);
});
test('Alerts can only go to the verified address, use the message as subject, and deduplicate',async()=>{
 const f=fixture(),auth=await f.signIn(),data={key:'movie:1:tomorrow',message:'Ariana Grande’s Next Picture is releasing TOMORROW.\nIn theaters.',unsubscribeToken:auth.unsubscribeToken,email:'attacker@example.com'};
 let res=await f.request('/alerts',data,auth.token);assert.equal(res.status,200);assert.equal(f.emails.at(-1).to[0].email,'person@example.com');assert.equal(f.emails.at(-1).subject,'[MoVid] Ariana Grande’s Next Picture is releasing TOMORROW.');
 const count=f.emails.length;res=await f.request('/alerts',data,auth.token);assert.equal((await res.json()).duplicate,true);assert.equal(f.emails.length,count);assert.ok(f.emails.at(-1).textContent.includes('/unsubscribe?token='));
});
test('High-priority is opt-in sender metadata, while unsubscribe remains present',async()=>{
 const f=fixture(),auth=await f.signIn();
 const data={key:'priority-test',message:'Priority test',unsubscribeToken:auth.unsubscribeToken,highPriority:true};
 assert.equal((await f.request('/alerts',data,auth.token)).status,200);const headers=f.emails.at(-1).headers;
 assert.equal(headers.Importance,'high');assert.equal(headers['X-Priority'],'1');assert.ok(headers['List-Unsubscribe']);
 assert.equal((await f.request('/alerts',{...data,key:'normal-test',highPriority:false},auth.token)).status,200);
 assert.equal(f.emails.at(-1).headers.Importance,undefined);
});
test('Unsubscribe requires confirmation; disconnect revokes this device; no remote mail schedules exist',async()=>{
 const f=fixture(),auth=await f.signIn();const path='/unsubscribe?token='+auth.unsubscribeToken;assert.equal((await f.request(path)).status,200);assert.equal((await f.request('/account',null,auth.token)).status,200);
 const stop=await f.service.fetch(new Request(f.env.PUBLIC_URL+path,{method:'POST'}),f.env);assert.equal(stop.status,200);assert.equal((await f.request('/account',null,auth.token)).status,401);
 const count=f.emails.length;await f.service.scheduled({},f.env);assert.equal(f.emails.length,count);
 const second=await f.signIn();assert.equal((await f.request('/disconnect',{},second.token)).status,200);assert.equal((await f.request('/account',null,second.token)).status,401);
});
test('Verification throttles and a fixed free-tier mail budget stop sends',async()=>{
 const f=fixture();for(let i=0;i<3;i++)assert.equal((await f.request('/auth/start',{email:'person@example.com'})).status,200);assert.equal((await f.request('/auth/start',{email:'person@example.com'})).status,429);
 const g=fixture(),bucket=Math.floor(Date.now()/86400000);g.env.DB.db.prepare('INSERT INTO limits(id,count,expires) VALUES(?,?,?)').run('mail-total:'+bucket,200,Date.now()+86400000);
 assert.equal((await g.request('/auth/start',{email:'person@example.com'})).status,429);assert.equal(g.emails.length,0);
});
