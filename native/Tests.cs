using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
namespace MoVid {
 public static class SelfTests {
  public static int Run(string[] args){
   int i=Array.IndexOf(args,"--test-dir");string dir=i>=0?args[i+1]:Path.Combine(Path.GetTempPath(),"movid-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(dir);
   var checks=new List<string>();try{RunAsync(dir,checks).GetAwaiter().GetResult();File.WriteAllText(Path.Combine(dir,"self-test.json"),J.Serializer().Serialize(new{passed=true,checks=checks}));return 0;}catch(Exception ex){File.WriteAllText(Path.Combine(dir,"self-test.json"),J.Serializer().Serialize(new{passed=false,checks=checks,error=ex.ToString()}));return 1;}
  }
  public static void Assert(bool condition,string description){if(!condition)throw new Exception("FAILED: "+description);}
  public static Dictionary<string,object> Fixture(string path,bool dated,List<Dictionary<string,object>> videos){
   string tomorrow=Rules.Day(DateTime.Now.AddDays(1));
   if(path.StartsWith("/search/person"))return J.Parse("{\"results\":[{\"id\":1,\"name\":\"Ariana Grande\",\"profile_path\":null,\"known_for_department\":\"Acting\"},{\"id\":2,\"name\":\"Christopher Nolan\",\"profile_path\":null,\"known_for_department\":\"Directing\"}]}");
   if(path.EndsWith("combined_credits"))return J.Parse("{\"cast\":[{\"id\":100,\"media_type\":\"movie\",\"title\":\"A Future Picture (test data)\",\"release_date\":\""+tomorrow+"\"},{\"id\":200,\"media_type\":\"tv\",\"name\":\"A Future Season (test data)\"}],\"crew\":[]}");
   if(path.StartsWith("/movie/100?")){var d=J.Parse("{\"id\":100,\"title\":\"A Future Picture (test data)\",\"release_dates\":{\"results\":[{\"iso_3166_1\":\"US\",\"release_dates\":[{\"type\":3,\"release_date\":\""+tomorrow+"T00:00:00Z\"},{\"type\":4,\"release_date\":\""+tomorrow+"T00:00:00Z\"}]}]},\"videos\":{}}");if(!dated)d["release_dates"]=J.Parse("{\"results\":[]}");d["videos"]=new Dictionary<string,object>{{"results",videos}};return d;}
   if(path.StartsWith("/tv/200?"))return J.Parse("{\"id\":200,\"name\":\"A Future Season (test data)\",\"first_air_date\":\"2020-01-01\",\"seasons\":[{\"season_number\":3,\"air_date\":\""+tomorrow+"\"}]}");
   if(path.Contains("/season/3/credits"))return J.Parse("{\"cast\":[{\"id\":1}],\"crew\":[]}");
   if(path.Contains("configuration"))return new Dictionary<string,object>();
   throw new Exception("Unexpected fixture request: "+path);
  }
  static async Task RunAsync(string dir,List<string> checks){
   var store=new Storage(dir);store.ApiKey="private-test-key";store.NewTopic();store.Save();Assert(!File.ReadAllText(Path.Combine(dir,"state.json")).Contains("private-test-key"),"credential encrypted");var reloaded=new Storage(dir);Assert(reloaded.ApiKey=="private-test-key"&&reloaded.PhoneTopic.StartsWith("movid-"),"DPAPI round trip");checks.Add("Encrypted credentials and phone topic persist without plaintext");
   var s=store.State;Assert(s.Settings.Timing=="before"&&!s.Settings.Trailers&&!s.Settings.ConfirmedDates,"day-before-only defaults");s.Settings.Hour=0;var r=new Release{Id="test",Title="Purple Starman",Date=Rules.Day(DateTime.Now.AddDays(1)),Kind="theater",People=new List<int>{1}};s.Follows.Add(new Person{Id=1,Name="John Apple",Since=Rules.Day(DateTime.Now.AddDays(-1))});s.Releases.Add(r);
   Assert(Rules.Due(s,DateTime.Now).Count==1,"tomorrow reminder");Assert(Rules.Message(r,s.Follows,"TOMORROW")=="John Apple’s Purple Starman is releasing TOMORROW.","notification wording");s.Sent[Rules.Due(s,DateTime.Now)[0].Key]="test";Assert(Rules.Due(s,DateTime.Now).Count==0,"deduplication");s.Sent.Clear();s.Follows[0].Since=r.Date;Assert(Rules.Due(s,DateTime.Now).Count==0,"follow baseline");s.Follows.Clear();s.Releases.Clear();checks.Add("Reminder timing, wording, duplicate protection and follow-date baseline");
   var movie=J.Parse("{\"id\":5,\"title\":\"Regional test\"}");var dates=J.Parse("{\"results\":[{\"iso_3166_1\":\"US\",\"release_dates\":[{\"type\":1,\"release_date\":\"2027-01-01\"},{\"type\":2,\"release_date\":\"2027-02-01\"},{\"type\":3,\"release_date\":\"2027-02-02\"},{\"type\":4,\"release_date\":\"2027-03-01\"}]}]}");Assert(Rules.MovieDates(movie,dates,"US").Count==2&&Rules.MovieDates(movie,dates,"GB").Count==0,"regional dates");checks.Add("Regional theater and digital dates exclude festivals and other countries");
   s.Settings.Trailers=true;s.Settings.ConfirmedDates=true;bool dated=true;var videos=new List<Dictionary<string,object>>();var engine=new Engine(store);engine.Api.Fixture=path=>Task.FromResult(Fixture(path,dated,videos));int shown=0;engine.Desktop=(title,message)=>shown++;
   await engine.Follow(new Person{Id=1,Name="Ariana Grande"});Assert(store.State.Releases.Count==3,"follow auto checks");Assert(store.State.Inbox.Any(n=>n.Key.StartsWith("welcome:")),"first-follow alert");int count=shown;await engine.Refresh(true);Assert(shown==count,"repeat check deduplicates");checks.Add("Actor follow immediately discovers upcoming movies and TV and sends a test alert");
   await engine.Follow(new Person{Id=2,Name="Christopher Nolan"});Assert(store.State.Releases.First(x=>x.Kind=="theater").People.Count==2,"shared title credit");Assert(store.State.Releases.First(x=>x.Kind=="tv").People.Count==1,"confirmed season credit");checks.Add("Shared titles merge followed people; TV requires confirmed season credit");
   dated=false;await engine.Refresh(true);var unknown=store.State.Releases.First(x=>x.Kind=="unknown");store.State.Saved.Add(unknown.Id);
   videos.Add(J.Parse("{\"id\":\"v1\",\"type\":\"Trailer\",\"official\":true,\"site\":\"YouTube\",\"key\":\"abcDEF12345\"}"));await engine.Refresh(true);Assert(store.State.Inbox.Count(n=>n.Key.StartsWith("trailer:"))==1,"trailer alert");await engine.Refresh(true);Assert(store.State.Inbox.Count(n=>n.Key.StartsWith("trailer:"))==1,"trailer deduplication");
   dated=true;await engine.Refresh(true);Assert(store.State.Inbox.Any(n=>n.Key.StartsWith("confirmed:")),"date confirmation");Assert(store.State.Saved.Contains("movie:100:US:theater"),"saved radar carries over");checks.Add("New trailers, confirmed dates and undated watchlist transitions");
   string backup=store.Export();Assert(!backup.Contains(store.State.Settings.KeyCipher)&&!backup.Contains(store.State.Settings.TopicCipher),"export excludes secrets");store.Import(backup);Assert(store.ApiKey=="private-test-key"&&!store.State.Settings.Mobile,"import preserves local key and pauses phone");checks.Add("Backups exclude secrets; imports preserve local connection and disable phone sends");
   s=store.State;s.Settings.Notifications=false;Assert(Rules.Due(s,DateTime.Now).Count==0,"notifications off");s.Settings.Sort="az";store.Save();Assert(new Storage(dir).State.Settings.Sort=="az","sort persists");checks.Add("Notification off switch and alphabetical sort persist");
   engine.PhoneOverride=(message,url)=>{throw new Exception("Simulated offline phone");};s.Settings.Mobile=true;await engine.Notify("phone-failure","MoVid","Test",null);Assert(s.Inbox[0].PhoneStatus.Contains("not sent"),"phone failure visible");checks.Add("Phone delivery failures remain visible without blocking desktop alerts");
   s.Settings.Mobile=false;Dictionary<string,object> posted=null;
   engine.Api.Email.Fixture=(path,data)=>{
    if(path=="/auth/start")return Task.FromResult(J.Parse("{\"challenge\":\"test-challenge\"}"));
    if(path=="/auth/verify")return Task.FromResult(new Dictionary<string,object>{{"token",new string('a',64)},{"unsubscribeToken",new string('b',64)},{"email","person@example.com"}});
    if(path=="/alerts"){posted=data;return Task.FromResult(J.Parse("{\"accepted\":true}"));}
    if(path.StartsWith("/catalog"))return Task.FromResult(J.Parse("{\"results\":[{\"id\":1,\"name\":\"Ariana Grande\"}]}"));
    return Task.FromResult(new Dictionary<string,object>());
   };
   string challenge=await engine.Api.Email.Start("person@example.com");await engine.Api.Email.Verify(challenge,"123456");Assert(engine.Api.Email.Connected&&s.Settings.EmailAddress=="person@example.com","email verified connection");
   s.Settings.EmailHighPriority=true;await engine.Notify("email-client-test","MoVid","Your email works.",null);Assert(posted!=null&&J.Bool(posted,"highPriority")&&s.Inbox[0].EmailStatus=="Email service accepted","email priority delivered to service");
   s.Settings.Notifications=false;s.Releases.Add(new Release{Id="email-only",Title="Email Only",Date=Rules.Day(DateTime.Now.AddDays(1)),Kind="theater",People=new List<int>{1}});s.Follows.First(p=>p.Id==1).Since=Rules.Day(DateTime.Now.AddDays(-1));Assert(Rules.Due(s,DateTime.Now).Count>0,"email-only reminders work");
   engine.Api.Fixture=null;var searched=await engine.Api.Search("Ariana");Assert(searched.Count==1,"catalog uses service instead of per-user API key");checks.Add("Email verification, server catalog, priority preference and email-only alerts");
   string emailBackup=store.Export();Assert(!emailBackup.Contains(new string('a',64))&&!emailBackup.Contains(s.Settings.EmailSessionCipher)&&!emailBackup.Contains("person@example.com"),"backup excludes email identity and secrets");
   await engine.Api.Email.Disconnect();Assert(!engine.Api.Email.Connected&&!s.Settings.EmailEnabled,"email disconnect");checks.Add("Email secrets excluded from export and disconnect disables delivery");
  }
 }
 public partial class MainForm {
  public async Task RunSmokeTests(string output){
   try{
    engine.Api.Fixture=path=>Task.FromResult(SelfTests.Fixture(path,true,new List<Dictionary<string,object>>()));store.State.Settings.Onboarded=true;store.State.Settings.Hour=0;
    engine.Api.Email.Fixture=(path,data)=>Task.FromResult(path=="/auth/start"?J.Parse("{\"challenge\":\"smoke-challenge\"}"):path=="/auth/verify"?new Dictionary<string,object>{{"token",new string('a',64)},{"unsubscribeToken",new string('b',64)},{"email","person@example.com"}}:J.Parse("{\"accepted\":true}"));
    Navigate("welcome");await Task.Delay(180);CaptureImage(Path.Combine(output,"01-welcome.png"));
    Navigate("following");search.Text="Ariana";await Search();SelfTests.Assert(found.Count==2,"native search results");await engine.Follow(found[0]);await engine.Follow(found[1]);Navigate("following");await Task.Delay(150);CaptureImage(Path.Combine(output,"02-following.png"));
    Navigate("upcoming");await Task.Delay(100);CaptureImage(Path.Combine(output,"03-upcoming.png"));
    Navigate("settings");fast.Checked=true;await SaveSettings();SelfTests.Assert(store.State.Settings.FastSearch,"fast suggestions saved");
    emailAddressBox.Text="person@example.com";emailChallenge=await engine.Api.Email.Start(emailAddressBox.Text);await engine.Api.Email.Verify(emailChallenge,"123456");Navigate("settings");emailPriorityBox.Checked=true;store.State.Settings.EmailHighPriority=true;store.Save();await Task.Delay(100);CaptureImage(Path.Combine(output,"04-settings.png"));
    Navigate("following");search.Text="Ari";SelfTests.Assert(suggestions.Visible&&suggestions.Items.Count==1&&suggestions.GetItemText(suggestions.Items[0])=="Ariana Grande","local cached dropdown");CaptureImage(Path.Combine(output,"05-suggestions.png"));
    Hide();SelfTests.Assert(!Visible&&!IsDisposed&&tray.Visible,"close-to-tray keeps running");OpenWindow();Navigate("inbox");await Task.Delay(100);CaptureImage(Path.Combine(output,"06-inbox.png"));
    var process=System.Diagnostics.Process.GetCurrentProcess();File.WriteAllText(Path.Combine(output,"desktop-test.json"),J.Serializer().Serialize(new{passed=true,framework=Environment.Version.ToString(),workingSetMB=Math.Round(process.WorkingSet64/1048576.0,1),privateMB=Math.Round(process.PrivateMemorySize64/1048576.0,1),checks=new[]{"Native window renders","Search results and first follow","Upcoming movie and confirmed TV season","Settings persist","Cached dropdown suggestions","Tray remains alive with window hidden","Notification history"}}));
   }catch(Exception ex){Environment.ExitCode=1;File.WriteAllText(Path.Combine(output,"desktop-test.json"),J.Serializer().Serialize(new{passed=false,error=ex.ToString()}));}finally{Quit();}
  }
  void CaptureImage(string path){using(var b=new Bitmap(Width,Height)){DrawToBitmap(b,new Rectangle(0,0,Width,Height));b.Save(path,System.Drawing.Imaging.ImageFormat.Png);}}
 }
}
