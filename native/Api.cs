using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
namespace MoVid {
 public class Catalog {
  public Func<string,Task<Dictionary<string,object>>> Fixture;
  private static readonly HttpClient Client=new HttpClient{Timeout=TimeSpan.FromSeconds(20)};
  private readonly Storage store;
  public Catalog(Storage storage){store=storage;}
  public async Task<Dictionary<string,object>> Get(string path){
   if(Fixture!=null)return await Fixture(path);
   string key=store.ApiKey;if(key.Length==0)throw new Exception("Connect the movie database once in Settings to search for real people and releases.");
   var request=new HttpRequestMessage(HttpMethod.Get,"https://api.themoviedb.org/3"+path+(path.Contains("?")?"&":"?")+"language=en-US");
   if(Regex.IsMatch(key,"^[a-fA-F0-9]{32}$"))request.RequestUri=new Uri(request.RequestUri+"&api_key="+Uri.EscapeDataString(key));else request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",key);
   using(request)using(var response=await Client.SendAsync(request)){
    if((int)response.StatusCode==401||(int)response.StatusCode==403)throw new Exception("TMDB did not accept the connection key. Check it in Settings.");
    if((int)response.StatusCode==429)throw new Exception("The movie database is busy. Please wait and try again.");
    if(!response.IsSuccessStatusCode)throw new Exception("Movie database request failed ("+(int)response.StatusCode+").");
    return J.Parse(await response.Content.ReadAsStringAsync());
   }
  }
  public async Task<List<Person>> Search(string query){
   var json=await Get("/search/person?include_adult=false&query="+Uri.EscapeDataString(query));
   return J.Arr(J.Get(json,"results")).Select(J.Obj).Where(x=>!J.Bool(x,"adult")).Select(x=>new Person{Id=J.Num(x,"id"),Name=J.Str(x,"name"),Photo=J.Str(x,"profile_path"),Department=J.Str(x,"known_for_department")}).Where(p=>p.Id>0).ToList();
  }
  public async Task Phone(string message,string url){
   string topic=store.PhoneTopic;if(!Regex.IsMatch(topic,"^movid-[a-f0-9]{48}$"))throw new Exception("Set up your phone connection first.");
   var data=new Dictionary<string,object>{{"topic",topic},{"title","MoVid"},{"message",message},{"priority",3},{"tags",new[]{"movie_camera"}}};
   Uri safe;if(Uri.TryCreate(url,UriKind.Absolute,out safe)&&safe.Scheme=="https"&&(safe.Host=="www.youtube.com"||safe.Host=="www.themoviedb.org"))data["click"]=url;
   using(var request=new HttpRequestMessage(HttpMethod.Post,"https://ntfy.sh")){
    request.Content=new StringContent(J.Serializer().Serialize(data),Encoding.UTF8,"application/json");
    // Immediate delivery only: no remote schedules remain after MoVid quits/uninstalls.
    using(var response=await Client.SendAsync(request)){if(!response.IsSuccessStatusCode)throw new Exception((int)response.StatusCode==429?"Free phone-service limit reached; try again later.":"Phone service could not accept the alert.");}
   }
  }
 }
}
