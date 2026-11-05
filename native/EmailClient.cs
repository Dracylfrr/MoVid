using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
namespace MoVid {
 public class EmailClient {
  readonly Storage store;static readonly HttpClient Client=new HttpClient{Timeout=TimeSpan.FromSeconds(22)};
  public string BaseUrl{get;private set;}
  public Func<string,Dictionary<string,object>,Task<Dictionary<string,object>>> Fixture;
  public bool Configured{get{return BaseUrl.Length>0||Fixture!=null;}}
  public bool Connected{get{return Configured&&Storage.Unprotect(store.State.Settings.EmailSessionCipher).Length>0;}}
  public EmailClient(Storage storage){
   store=storage;BaseUrl="";
   try{using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("service-config.json"))if(stream!=null)using(var reader=new StreamReader(stream)){var config=J.Parse(reader.ReadToEnd());Uri uri;string text=J.Str(config,"serviceUrl");if(Uri.TryCreate(text,UriKind.Absolute,out uri)&&uri.Scheme=="https"&&uri.UserInfo.Length==0&&uri.Query.Length==0&&uri.Fragment.Length==0&&uri.AbsolutePath=="/")BaseUrl=uri.GetLeftPart(UriPartial.Authority);}}catch{}
  }
  public async Task<Dictionary<string,object>> Request(string path,Dictionary<string,object> data=null,bool authenticated=true){
   if(Fixture!=null)return await Fixture(path,data);
   if(!Configured)throw new Exception("MoVid’s email service has not been activated by its owner yet. No email was sent.");
   using(var request=new HttpRequestMessage(data==null?HttpMethod.Get:HttpMethod.Post,BaseUrl+path)){
    if(authenticated){string token=Storage.Unprotect(store.State.Settings.EmailSessionCipher);if(token.Length==0)throw new Exception("Verify your email in Settings first.");request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);}
    if(data!=null)request.Content=new StringContent(J.Serializer().Serialize(data),Encoding.UTF8,"application/json");
    using(var response=await Client.SendAsync(request)){
     var result=J.Parse(await response.Content.ReadAsStringAsync());
     if(!response.IsSuccessStatusCode){if((int)response.StatusCode==401&&authenticated){store.State.Settings.EmailSessionCipher="";store.Save();}throw new Exception(J.Str(result,"error").Length>0?J.Str(result,"error"):"MoVid’s email service is unavailable. Please try again later.");}
     return result;
    }
   }
  }
  public async Task<string> Start(string email){var r=await Request("/auth/start",new Dictionary<string,object>{{"email",email},{"highPriority",store.State.Settings.EmailHighPriority}},false);return J.Str(r,"challenge");}
  public async Task Verify(string challenge,string code){
   var r=await Request("/auth/verify",new Dictionary<string,object>{{"challenge",challenge},{"code",code}},false);string token=J.Str(r,"token"),unsubscribe=J.Str(r,"unsubscribeToken");
   if(!System.Text.RegularExpressions.Regex.IsMatch(token,"^[a-f0-9]{64}$")||!System.Text.RegularExpressions.Regex.IsMatch(unsubscribe,"^[a-f0-9]{64}$"))throw new Exception("Email verification could not be completed. Request a new code.");
   store.State.Settings.EmailAddress=J.Str(r,"email");store.State.Settings.EmailSessionCipher=Storage.Protect(token);store.State.Settings.EmailUnsubscribeCipher=Storage.Protect(unsubscribe);store.State.Settings.EmailEnabled=true;store.State.Settings.Onboarded=true;store.Save();
  }
  public async Task Send(string key,string message,string url){
   var s=store.State.Settings;var r=await Request("/alerts",new Dictionary<string,object>{{"key",key},{"message",message},{"url",url??""},{"unsubscribeToken",Storage.Unprotect(s.EmailUnsubscribeCipher)},{"highPriority",s.EmailHighPriority}});if(!J.Bool(r,"accepted"))throw new Exception("Email delivery was not confirmed by the service.");
  }
  public async Task Disconnect(){if(Connected)await Request("/disconnect",new Dictionary<string,object>());store.State.Settings.EmailSessionCipher="";store.State.Settings.EmailUnsubscribeCipher="";store.State.Settings.EmailAddress="";store.State.Settings.EmailEnabled=false;store.Save();}
 }
}
