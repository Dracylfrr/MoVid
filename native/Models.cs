using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web.Script.Serialization;
namespace MoVid {
 public class Preferences {
  public string Region="US",Timing="before",Sort="recent",KeyCipher="",TopicCipher="";
  public string EmailAddress="",EmailSessionCipher="",EmailUnsubscribeCipher="";
  public bool EmailEnabled=true,EmailHighPriority=false;
  public int Hour=9;
  public bool Notifications=true,Trailers=false,ConfirmedDates=false,Theater=true,Digital=true,TV=true,Mobile=false,Onboarded=false,FastSearch=false;
 }
 public class Person { public int Id; public string Name,Photo,Department,Since; public long Added; public override string ToString(){return Name??"";} }
 public class Release {
  public string Id,Media,Title,Date,Kind,Region,Poster,Note; public int TitleId; public bool Stale;
  public List<int> People=new List<int>();
 }
 public class Notice { public string Key,Title,Message,Url,PhoneStatus,EmailStatus,Time; }
 public class AppState {
  public Preferences Settings=new Preferences(); public List<Person> Follows=new List<Person>();
  public List<Release> Releases=new List<Release>();public List<string> Saved=new List<string>(),Dismissed=new List<string>();
  public Dictionary<string,string> Sent=new Dictionary<string,string>();public Dictionary<string,List<string>> TrailerSeen=new Dictionary<string,List<string>>();
  public List<Notice> Inbox=new List<Notice>();public string LastCheck="",LastError="",Warnings="";
 }
 public class Reminder { public Release Release; public string Key,When; }
 public static class Rules {
  public static bool DeliveryEnabled(Preferences s){return s.Notifications||(s.EmailEnabled&&!String.IsNullOrEmpty(s.EmailSessionCipher));}
  public static string Day(DateTime date){return date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);}
  public static bool ValidDay(string value){DateTime d;return DateTime.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out d);}
  public static bool Enabled(Release r,Preferences s){return r.Kind=="theater"?s.Theater:r.Kind=="digital"?s.Digital:r.Kind=="tv"?s.TV:false;}
  public static string Kind(string kind){return kind=="theater"?"In theaters":kind=="digital"?"Digital release (rent, buy or stream)":kind=="tv"?"TV season premiere":"Date not announced";}
  public static IEnumerable<Person> Eligible(Release r,IEnumerable<Person> people){return people.Where(p=>r.People.Contains(p.Id)&&ValidDay(r.Date)&&String.CompareOrdinal(r.Date,p.Since)>0);}
  public static string Message(Release r,IEnumerable<Person> people,string when){string names=String.Join(" and ",Eligible(r,people).Take(2).Select(p=>p.Name));return(names.Length>0?names+"’s ":"")+r.Title+" is releasing "+when+".";}
  public static List<Reminder> Due(AppState s,DateTime now){
   var result=new List<Reminder>();if(!DeliveryEnabled(s.Settings)||s.Settings.Timing=="none"||now.Hour<s.Settings.Hour)return result;
   foreach(var r in s.Releases){
    if(r.Stale||s.Dismissed.Contains(r.Id)||!Enabled(r,s.Settings)||!Eligible(r,s.Follows).Any())continue;
    string when=r.Date==Day(now)?"TODAY":r.Date==Day(now.AddDays(1))?"TOMORROW":null;
    if(when==null||(s.Settings.Timing=="before"&&when!="TOMORROW")||(s.Settings.Timing=="day"&&when!="TODAY"))continue;
    string key=r.Id+"|"+r.Date+"|"+when;if(!s.Sent.ContainsKey(key))result.Add(new Reminder{Release=r,Key=key,When=when});
   }return result;
  }
  public static List<Release> MovieDates(Dictionary<string,object> movie,Dictionary<string,object> releaseData,string region){
   var rows=J.Arr(J.Get(releaseData,"results")).Select(J.Obj).FirstOrDefault(x=>J.Str(x,"iso_3166_1")==region);var result=new List<Release>();
   foreach(string kind in new[]{"theater","digital"}){
    var dates=J.Arr(J.Get(rows,"release_dates")).Select(J.Obj).Where(x=>kind=="theater"?(J.Num(x,"type")==2||J.Num(x,"type")==3):J.Num(x,"type")==4).Select(x=>J.Str(x,"release_date")).Where(x=>x.Length>=10).Select(x=>x.Substring(0,10)).Where(ValidDay).OrderBy(x=>x).ToList();
    if(dates.Count==0)continue;int id=J.Num(movie,"id");result.Add(new Release{Id="movie:"+id+":"+region+":"+kind,TitleId=id,Media="movie",Title=J.Str(movie,"title"),Poster=J.Str(movie,"poster_path"),Date=dates[0],Kind=kind,Region=region});
   }return result;
  }
 }
 public static class J {
  public static JavaScriptSerializer Serializer(){return new JavaScriptSerializer{MaxJsonLength=16*1024*1024,RecursionLimit=100};}
  public static Dictionary<string,object> Obj(object v){return v as Dictionary<string,object>??new Dictionary<string,object>();}
  public static object Get(Dictionary<string,object> v,string key){object r;return v!=null&&v.TryGetValue(key,out r)?r:null;}
  public static string Str(Dictionary<string,object> v,string k){return Convert.ToString(Get(v,k),CultureInfo.InvariantCulture)??"";}
  public static int Num(Dictionary<string,object> v,string k){int n;return Int32.TryParse(Str(v,k),out n)?n:0;}
  public static bool Bool(Dictionary<string,object> v,string k){return Object.Equals(Get(v,k),true);}
  public static IEnumerable<object> Arr(object v){return v as IEnumerable<object>??Enumerable.Empty<object>();}
  public static Dictionary<string,object> Parse(string json){return Obj(Serializer().DeserializeObject(json));}
 }
}
