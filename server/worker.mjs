const json = (data,status=200) => new Response(JSON.stringify(data),{status,headers:{'Content-Type':'application/json','Cache-Control':'no-store','X-Content-Type-Options':'nosniff'}});
const html = body => new Response(`<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width"><title>MoVid email preferences</title><body style="font:18px system-ui;max-width:600px;margin:60px auto;padding:24px;background:#101411;color:#f0f2e9">${body}</body></html>`,{headers:{'Content-Type':'text/html;charset=utf-8','Content-Security-Policy':"default-src 'none'; style-src 'unsafe-inline'; form-action 'self'; frame-ancestors 'none'",'Referrer-Policy':'no-referrer','Cache-Control':'no-store','X-Content-Type-Options':'nosniff'}});
const hex = bytes => Array.from(bytes,b=>b.toString(16).padStart(2,'0')).join('');
const random = (length=32) => hex(crypto.getRandomValues(new Uint8Array(length)));
const hash = async text => hex(new Uint8Array(await crypto.subtle.digest('SHA-256',new TextEncoder().encode(text))));
const DAY=86400000,SESSION=30*DAY;
class ServiceError extends Error {constructor(message,status=400){super(message);this.status=status;}}
const fail=(message,status)=>{throw new ServiceError(message,status);};

export function createService({fetcher=fetch,clock=Date.now}={}) {
  async function limited(db,key,max,windowMs) {
    const now=clock(),bucket=Math.floor(now/windowMs),id=`${key}:${bucket}`;
    const row=await db.prepare('INSERT INTO limits(id,count,expires) VALUES(?,1,?) ON CONFLICT(id) DO UPDATE SET count=count+1 RETURNING count').bind(id,(bucket+1)*windowMs).first();
    if(row.count>max)fail('Please wait before trying again. MoVid has reached a free sending or request limit.',429);
  }
  function ready(env){return Boolean(env.DB&&env.TMDB_TOKEN&&env.BREVO_KEY&&env.MAIL_FROM&&env.PUBLIC_URL);}
  async function body(request){
    if(Number(request.headers.get('content-length')||0)>4096)fail('Request is too large.',413);
    const reader=request.body?.getReader();let text='',size=0;const decoder=new TextDecoder();
    if(reader)while(true){const {value,done}=await reader.read();if(done)break;size+=value.byteLength;if(size>4096){await reader.cancel();fail('Request is too large.',413);}text+=decoder.decode(value,{stream:true});}text+=decoder.decode();
    try{const parsed=JSON.parse(text);if(!parsed||typeof parsed!=='object'||Array.isArray(parsed))throw Error();return parsed;}catch{fail('The request could not be read.');}
  }
  async function send(env,email,subject,text,unsubscribeToken,highPriority=false) {
    // This ceiling is fixed below Brevo's published free allowance. No paid credits or upgrades are requested.
    await limited(env.DB,'mail-total',200,DAY);
    const payload={sender:{name:'MoVid',email:env.MAIL_FROM},to:[{email}],subject:('[MoVid] '+subject).replace(/[\r\n]/g,' ').slice(0,200),textContent:text,headers:{'Auto-Submitted':'auto-generated'}};
    // A request to clients that honor these headers; this cannot set Gmail's recipient-side Importance label.
    if(highPriority)Object.assign(payload.headers,{'Importance':'high','X-Priority':'1','X-MSMail-Priority':'High'});
    if(unsubscribeToken){
      const link=new URL('/unsubscribe',env.PUBLIC_URL);link.searchParams.set('token',unsubscribeToken);
      payload.textContent+=`\n\nStop MoVid emails: ${link.href}\nYou can also disconnect email in MoVid. Quitting or uninstalling MoVid stops new alert requests from this computer.`;
      payload.headers['List-Unsubscribe']=`<${link.href}>`;
    }
    let response;
    try{response=await fetcher('https://api.brevo.com/v3/smtp/email',{method:'POST',headers:{'Content-Type':'application/json','api-key':env.BREVO_KEY},body:JSON.stringify(payload),signal:AbortSignal.timeout(15000)});}catch{fail('The email service could not be reached. Please try again later.',503);}
    if(!response.ok)fail(response.status===429?'The free email allowance has been reached. Please try again later.':'The MoVid email sender is not ready. Please contact the app owner.',503);
    const accepted=await response.json();if(!accepted.messageId)fail('The email service did not confirm acceptance. Please try again later.',503);
    return accepted.messageId;
  }
  async function account(request,env) {
    const bearer=request.headers.get('Authorization')||'';
    if(!/^Bearer [a-f0-9]{64}$/.test(bearer))fail('Please verify your email in MoVid.',401);
    const tokenHash=await hash(bearer.slice(7));
    const user=await env.DB.prepare('SELECT a.id,a.email,a.enabled FROM sessions s JOIN accounts a ON a.id=s.account_id WHERE s.token_hash=? AND s.expires>?').bind(tokenHash,clock()).first();
    if(!user)fail('Your connection expired. Please verify your email again.',401);
    await env.DB.prepare('UPDATE sessions SET expires=? WHERE token_hash=?').bind(clock()+SESSION,tokenHash).run();
    await limited(env.DB,`api:${user.id}`,4000,DAY);
    return {...user,tokenHash};
  }
  async function handle(request,env,ctx={waitUntil:()=>{}}) {
    try {
      const url=new URL(request.url),route=url.pathname;
      if(route==='/health'&&request.method==='GET')return json({service:'MoVid',ready:ready(env),emailLimitPerDay:200});
      if(!ready(env))fail('Email setup has not been completed by the MoVid owner yet. No email has been sent.',503);
      if(route==='/auth/start'&&request.method==='POST'){
        const data=await body(request),email=String(data.email||'').trim().toLowerCase();
        if(email.length>254||! /^[^\s@<>]+@[^\s@<>]+\.[^\s@<>]+$/.test(email))fail('Enter a valid email address.');
        const ip=await hash(`ip:${request.headers.get('CF-Connecting-IP')||'unknown'}`);
        await limited(env.DB,ip,5,3600000);await limited(env.DB,`address:${await hash(email)}`,3,3600000);
        const id=random(),number=crypto.getRandomValues(new Uint32Array(1))[0];
        const code=String(number%1000000).padStart(6,'0');
        await env.DB.prepare('DELETE FROM challenges WHERE email=? OR expires<?').bind(email,clock()).run();
        await env.DB.prepare('INSERT INTO challenges(id,email,code_hash,expires) VALUES(?,?,?,?)').bind(id,email,await hash(id+':'+code),clock()+600000).run();
        try{await send(env,email,`Your MoVid code is ${code}`,`Your MoVid verification code is ${code}.\n\nEnter it in the MoVid app within 10 minutes.\nIf you did not request this code, ignore this email. Do not share the code.`,null,data.highPriority===true);}catch(e){await env.DB.prepare('DELETE FROM challenges WHERE id=?').bind(id).run();throw e;}
        return json({challenge:id,message:'Check your email for a six-digit code. It may take a minute.'});
      }
      if(route==='/auth/verify'&&request.method==='POST'){
        const data=await body(request),id=String(data.challenge||''),code=String(data.code||'');
        if(!/^[a-f0-9]{64}$/.test(id)||!/^\d{6}$/.test(code))fail('Enter the six-digit code from your email.');
        const challenge=await env.DB.prepare('UPDATE challenges SET attempts=attempts+1 WHERE id=? AND expires>? AND attempts<5 RETURNING email,code_hash,attempts').bind(id,clock()).first();
        if(!challenge||challenge.code_hash!==await hash(id+':'+code))fail('That code is incorrect or expired. Request a new code if needed.',401);
        // Consume atomically: simultaneous verification attempts cannot mint multiple sessions.
        const consumed=await env.DB.prepare('DELETE FROM challenges WHERE id=? RETURNING email').bind(id).first();
        if(!consumed)fail('That code has already been used. Request a new one.',401);
        const existing=await env.DB.prepare('SELECT id,unsubscribe_token FROM accounts WHERE email=?').bind(challenge.email).first();
        const accountId=existing?.id||random(16),unsubscribe=existing?.unsubscribe_token||random();
        await env.DB.prepare('INSERT INTO accounts(id,email,enabled,unsubscribe_hash,unsubscribe_token) VALUES(?,?,1,?,?) ON CONFLICT(email) DO UPDATE SET enabled=1').bind(accountId,challenge.email,await hash(unsubscribe),unsubscribe).run();
        const token=random();await env.DB.prepare('INSERT INTO sessions(token_hash,account_id,expires) VALUES(?,?,?)').bind(await hash(token),accountId,clock()+SESSION).run();
        return json({token,email:challenge.email,unsubscribeToken:unsubscribe});
      }
      if(route==='/unsubscribe'){
        const token=String(url.searchParams.get('token')||'');
        if(!/^[a-f0-9]{64}$/.test(token))fail('This unsubscribe link is invalid.');
        const tokenHash=await hash(token),user=await env.DB.prepare('SELECT id FROM accounts WHERE unsubscribe_hash=?').bind(tokenHash).first();
        if(!user)return html('<h1>This link has expired.</h1><p>You can disconnect email in MoVid Settings.</p>');
        if(request.method==='GET')return html('<h1>Stop MoVid emails?</h1><p>This stops release emails for this address on every connected MoVid device.</p><form method="post"><button style="font:inherit;padding:14px">Stop emails</button></form>');
        if(request.method==='POST'){await env.DB.batch([env.DB.prepare('UPDATE accounts SET enabled=0 WHERE id=?').bind(user.id),env.DB.prepare('DELETE FROM sessions WHERE account_id=?').bind(user.id)]);return html('<h1>MoVid emails are stopped.</h1><p>You can close this page. Verify your email again in MoVid only if you want to reconnect.</p>');}
      }
      const user=await account(request,env);
      if(route==='/account'&&request.method==='GET')return json({email:user.email,enabled:Boolean(user.enabled)});
      if(route==='/disconnect'&&request.method==='POST'){
        await env.DB.prepare('DELETE FROM sessions WHERE token_hash=?').bind(user.tokenHash).run();return json({disconnected:true});
      }
      if(route==='/catalog'&&request.method==='GET'){
        const path=url.searchParams.get('path')||'';
        if(! /^\/(search\/person|person\/\d+\/(combined_credits)|movie\/\d+|tv\/\d+(\/season\/\d+\/credits)?)(\?.*)?$/.test(path)||path.length>500)fail('Unsupported movie database request.');
        const upstream=new URL('https://api.themoviedb.org/3'+path);
        for(const key of [...upstream.searchParams.keys()])if(!['query','include_adult','append_to_response','page'].includes(key))fail('Unsupported search option.');
        upstream.searchParams.set('language','en-US');upstream.searchParams.set('include_adult','false');
        if(upstream.searchParams.has('append_to_response')&&!['release_dates,videos','videos'].includes(upstream.searchParams.get('append_to_response')))fail('Unsupported movie database details.');
        await limited(env.DB,'catalog-global',20000,DAY);
        const headers={Accept:'application/json'};
        if(/^[a-f0-9]{32}$/i.test(env.TMDB_TOKEN))upstream.searchParams.set('api_key',env.TMDB_TOKEN);else headers.Authorization=`Bearer ${env.TMDB_TOKEN}`;
        const response=await fetcher(upstream.href,{headers,signal:AbortSignal.timeout(15000),cf:{cacheTtl:3600,cacheEverything:true}});
        if(!response.ok)fail('The movie database is temporarily unavailable. Please try again later.',503);
        return json(await response.json());
      }
      if(route==='/alerts'&&request.method==='POST'){
        if(!user.enabled)fail('Email alerts are disabled. Verify your email again to reconnect.',403);
        const data=await body(request),key=String(data.key||''),message=String(data.message||''),link=String(data.url||'');
        if(key.length<3||key.length>200||message.length<3||message.length>1800)fail('Invalid release alert.');
        if(link&&! /^https:\/\/www\.(youtube\.com\/watch\?v=[A-Za-z0-9_-]+|themoviedb\.org\/(movie|tv)\/\d+)$/.test(link))fail('Unsupported alert link.');
        await limited(env.DB,`alerts:${user.id}`,30,3600000);
        const id=await hash(user.id+':'+key);
        const prior=await env.DB.prepare('INSERT OR IGNORE INTO deliveries(id,account_id,status,created) VALUES(?,?,\'pending\',?) RETURNING id').bind(id,user.id,clock()).first();
        if(!prior){const existing=await env.DB.prepare('SELECT status FROM deliveries WHERE id=?').bind(id).first();if(existing?.status==='accepted')return json({accepted:true,duplicate:true});fail('This alert is already being processed or its delivery could not be confirmed. Use Send test email to try a new test.',409);}
        try{
          const unsubscribe=String(data.unsubscribeToken||'');
          const matches=/^[a-f0-9]{64}$/.test(unsubscribe)&&await env.DB.prepare('SELECT id FROM accounts WHERE id=? AND unsubscribe_hash=?').bind(user.id,await hash(unsubscribe)).first();
          if(!matches)fail('Please verify your email again to refresh its preferences link.',401);
          await send(env,user.email,message.split('\n')[0],message+(link?'\n\n'+link:''),unsubscribe,data.highPriority===true);
          await env.DB.prepare('UPDATE deliveries SET status=\'accepted\' WHERE id=?').bind(id).run();
          return json({accepted:true});
        }catch(e){await env.DB.prepare('UPDATE deliveries SET status=\'failed\' WHERE id=?').bind(id).run();throw e;}
      }
      return json({error:'Not found.'},404);
    }catch(e){return json({error:e instanceof ServiceError?e.message:'MoVid could not complete that request. Please try again later.'},e instanceof ServiceError?e.status:500);}
  }
  return {fetch:handle,async scheduled(event,env){
    if(!env.DB)return;
    await env.DB.batch([
      env.DB.prepare('DELETE FROM challenges WHERE expires<?').bind(clock()),
      env.DB.prepare('DELETE FROM sessions WHERE expires<?').bind(clock()),
      env.DB.prepare('DELETE FROM limits WHERE expires<?').bind(clock()-DAY),
      env.DB.prepare('DELETE FROM deliveries WHERE created<?').bind(clock()-90*DAY)
    ]);
  }};
}
export default createService();
