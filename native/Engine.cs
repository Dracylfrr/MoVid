using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
namespace MoVid {
 public class Engine {
  public readonly Storage Store;public readonly Catalog Api;
  public bool Busy;public string Progress="";
  public Action Changed=delegate{};public Action<string,string> Desktop=delegate{};
  public Func<string,string,Task> PhoneOverride;
  public Engine(Storage s){Store=s;Api=new Catalog(s);}
  public async Task Notify(string key,string title,string message,string url){
   var s=Store.State;if(s.Sent.ContainsKey(key))return;
   Desktop(title,message);s.Sent[key]=DateTime.UtcNow.ToString("o");Store.Save();
   string phone="Desktop only";
   if(s.Settings.Mobile){try{if(PhoneOverride!=null)await PhoneOverride(message,url);else await Api.Phone(message,url);phone="Phone service accepted";}catch(Exception ex){phone="Phone not sent: "+ex.Message;}}
   s.Inbox.Insert(0,new Notice{Key=key,Title=title,Message=message,Url=url,PhoneStatus=phone,Time=DateTime.Now.ToString("o")});s.Inbox=s.Inbox.Take(100).ToList();Store.Save();Changed();
  }
  public async Task Remind(){foreach(var due in Rules.Due(Store.State,DateTime.Now))await Notify(due.Key,"MoVid",Rules.Message(due.Release,Store.State.Follows,due.When)+"\n"+Rules.Kind(due.Release.Kind)+(due.Release.Kind=="tv"?" · regional availability may differ":" · "+due.Release.Region),null);}
  public async Task Follow(Person found){
   if(Busy)throw new Exception("Wait for the current release check to finish.");if(Store.State.Follows.Any(p=>p.Id==found.Id))return;
   found.Since=Rules.Day(DateTime.Now);found.Added=DateTime.UtcNow.Ticks;Store.State.Follows.Add(found);Store.Save();Changed();
   await Refresh(true);
   var events=Store.State.Releases.Where(r=>r.People.Contains(found.Id)).OrderBy(r=>r.Date??"9999").ToList();var first=events.FirstOrDefault(r=>Rules.ValidDay(r.Date));
   string message="You’re following "+found.Name+". "+(first!=null?first.Title+" is scheduled for "+DateTime.ParseExact(first.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture).ToString("MMMM d, yyyy")+".":events.Count>0?"An upcoming project has no announced release date yet.":"No upcoming projects were found in the database yet.")+"\nThis is your first-follow test alert.";
   if(Store.State.Settings.Notifications)await Notify("welcome:"+found.Id+":"+found.Added,"MoVid",message,null);
  }
  public void Unfollow(int id){if(Busy)throw new Exception("Wait for the current check to finish.");var s=Store.State;s.Follows.RemoveAll(p=>p.Id==id);foreach(var r in s.Releases)r.People.Remove(id);s.Releases.RemoveAll(r=>r.People.Count==0);Store.Save();Changed();}
  public async Task Refresh(bool force){
   if(Busy)return;var s=Store.State;
   if(s.Follows.Count==0||Store.ApiKey.Length==0&&Api.Fixture==null)return;
   DateTime last;if(!force&&DateTime.TryParse(s.LastCheck,out last)&&DateTime.UtcNow-last.ToUniversalTime()<TimeSpan.FromHours(20)){await Remind();return;}
   Busy=true;Progress="Checking your people…";Changed();
   try{
    string today=Rules.Day(DateTime.Now),yearAgo=Rules.Day(DateTime.Now.AddYears(-1));
    var credits=new Dictionary<string,Dictionary<string,object>>();var links=new Dictionary<string,HashSet<int>>();
    foreach(var p in s.Follows){
     var all=await Api.Get("/person/"+p.Id+"/combined_credits");
     foreach(var c in J.Arr(J.Get(all,"cast")).Concat(J.Arr(J.Get(all,"crew"))).Select(J.Obj)){
      string media=J.Str(c,"media_type");if(media!="movie"&&media!="tv")continue;
      if(media=="movie"&&J.Str(c,"release_date").Length>0&&String.CompareOrdinal(J.Str(c,"release_date"),yearAgo)<0)continue;
      string k=media+":"+J.Num(c,"id");credits[k]=c;if(!links.ContainsKey(k))links[k]=new HashSet<int>();links[k].Add(p.Id);
     }
    }
    var releases=new List<Release>();var warnings=new List<string>();var updates=new List<Tuple<string,string,string>>();var old=s.Releases.ToList();int n=0;
    foreach(var pair in credits){
     var c=pair.Value;string media=J.Str(c,"media_type");int id=J.Num(c,"id");var people=links[pair.Key].ToList();
     Progress="Checking titles · "+(++n)+" of "+credits.Count;Changed();
     try{
      var detail=await Api.Get("/"+media+"/"+id+"?append_to_response="+(media=="movie"?"release_dates,videos":"videos"));
      var events=new List<Release>();string title=J.Str(detail,media=="movie"?"title":"name");if(title.Length==0)title=J.Str(c,media=="movie"?"title":"name");
      if(media=="movie"){
       events=Rules.MovieDates(detail,J.Obj(J.Get(detail,"release_dates")),s.Settings.Region);
       if(events.Count==0&&(J.Str(c,"release_date").Length==0||String.CompareOrdinal(J.Str(c,"release_date"),today)>0))events.Add(new Release{Id=pair.Key+":"+s.Settings.Region+":unknown",TitleId=id,Media=media,Title=title,Kind="unknown",Region=s.Settings.Region});
      }else{
       foreach(var season in J.Arr(J.Get(detail,"seasons")).Select(J.Obj)){
        int number=J.Num(season,"season_number");string date=J.Str(season,"air_date");if(number<1||!Rules.ValidDay(date)||String.CompareOrdinal(date,today)<0)continue;
        var cast=await Api.Get("/tv/"+id+"/season/"+number+"/credits");var confirmed=J.Arr(J.Get(cast,"cast")).Concat(J.Arr(J.Get(cast,"crew"))).Select(J.Obj).Select(x=>J.Num(x,"id")).Where(x=>people.Contains(x)).Distinct().ToList();
        if(confirmed.Count==0){warnings.Add(title+": future season credits not confirmed.");continue;}
        events.Add(new Release{Id=pair.Key+":season:"+number,Media=media,TitleId=id,Title=title+" · Season "+number,Date=date,Kind="tv",Region=s.Settings.Region,People=confirmed,Note="TV premiere date; your region may differ."});
       }
       if(J.Str(detail,"first_air_date").Length==0&&events.Count==0)events.Add(new Release{Id=pair.Key+":unknown",Media=media,TitleId=id,Title=title,Kind="unknown",Region=s.Settings.Region});
      }
      foreach(var r in events){if(r.People.Count==0)r.People=people.ToList();r.Poster=J.Str(detail,"poster_path");if(r.Date!=null&&(String.CompareOrdinal(r.Date,today)<0||!Rules.Eligible(r,s.Follows).Any()))continue;releases.Add(r);}
      if(events.Any(r=>r.Date==null||String.CompareOrdinal(r.Date,today)>0)){
       var videos=J.Arr(J.Get(J.Obj(J.Get(detail,"videos")),"results")).Select(J.Obj).Where(v=>J.Str(v,"type")=="Trailer"&&J.Bool(v,"official")&&J.Str(v,"site")=="YouTube"&&System.Text.RegularExpressions.Regex.IsMatch(J.Str(v,"key"),"^[A-Za-z0-9_-]{6,30}$")).ToList();
       List<string> seen;bool baseline=s.TrailerSeen.TryGetValue(pair.Key,out seen);if(!baseline)seen=new List<string>();
       foreach(var v in videos){string videoId=J.Str(v,"id");if(videoId.Length==0)videoId=J.Str(v,"key");if(baseline&&!seen.Contains(videoId)&&s.Settings.Trailers&&s.Settings.Notifications&&!events.All(r=>s.Dismissed.Contains(r.Id)))updates.Add(Tuple.Create("trailer:"+pair.Key+":"+videoId,"A new trailer for "+title+" is available.","https://www.youtube.com/watch?v="+J.Str(v,"key")));if(!seen.Contains(videoId))seen.Add(videoId);}
       s.TrailerSeen[pair.Key]=seen;
      }
     }catch(Exception ex){if(ex.Message.Contains("connection key")||ex.Message.Contains("database is busy"))throw;warnings.Add(J.Str(c,media=="movie"?"title":"name")+": "+ex.Message);foreach(var r in old.Where(r=>r.TitleId==id&&r.Media==media&&(r.Date==null||String.CompareOrdinal(r.Date,today)>=0))){r.Stale=true;releases.Add(r);}}
    }
    foreach(var prior in old.Where(r=>r.Date==null))foreach(var fresh in releases.Where(r=>r.Date!=null&&r.TitleId==prior.TitleId&&r.Media==prior.Media)){if(s.Saved.Contains(prior.Id)&&!s.Saved.Contains(fresh.Id))s.Saved.Add(fresh.Id);if(s.Dismissed.Contains(prior.Id)&&!s.Dismissed.Contains(fresh.Id))s.Dismissed.Add(fresh.Id);}
    s.Releases=releases.OrderBy(r=>r.Date??"9999").ToList();s.LastCheck=DateTime.UtcNow.ToString("o");s.LastError="";s.Warnings=String.Join("\n",warnings.Take(20));Store.Save();
    foreach(var u in updates)await Notify(u.Item1,"MoVid",u.Item2,u.Item3);
    if(s.Settings.Notifications&&s.Settings.ConfirmedDates)foreach(var r in releases.Where(r=>r.Date!=null&&!r.Stale&&String.CompareOrdinal(r.Date,today)>0&&Rules.Enabled(r,s.Settings)&&!s.Dismissed.Contains(r.Id)))if(old.Any(o=>o.Media==r.Media&&o.TitleId==r.TitleId&&o.Date==null))await Notify("confirmed:"+r.Id+":"+r.Date,"MoVid",Rules.Message(r,s.Follows,"on "+DateTime.ParseExact(r.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture).ToString("MMMM d, yyyy"))+"\nRelease date confirmed · "+Rules.Kind(r.Kind),null);
    await Remind();
   }catch(Exception ex){s.LastError=ex.Message;Store.Save();throw;}finally{Busy=false;Progress="";Changed();}
  }
 }
}
