using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Win32;
namespace MoVid {
 public class Storage {
  public readonly string DirectoryPath;public AppState State;public string LoadWarning="";
  public Storage(string path){DirectoryPath=path;Directory.CreateDirectory(path);Load();}
  public void Load(){string f=Path.Combine(DirectoryPath,"state.json");if(!File.Exists(f)){State=new AppState();return;}try{State=J.Serializer().Deserialize<AppState>(File.ReadAllText(f,Encoding.UTF8));if(State==null||State.Settings==null||State.Follows==null||State.Releases==null||State.Sent==null||State.TrailerSeen==null||State.Inbox==null||State.Saved==null||State.Dismissed==null)throw new InvalidDataException();}catch{State=new AppState();LoadWarning="Saved profile could not be read. Original preserved as state.unreadable.json.";File.Copy(f,Path.Combine(DirectoryPath,"state.unreadable.json"),true);}}
  public void Save(){string f=Path.Combine(DirectoryPath,"state.json"),t=f+".tmp";File.WriteAllText(t,J.Serializer().Serialize(State),new UTF8Encoding(false));if(File.Exists(f))File.Replace(t,f,f+".bak");else File.Move(t,f);}
  public static string Protect(string v){return String.IsNullOrEmpty(v)?"":Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(v),null,DataProtectionScope.CurrentUser));}
  public static string Unprotect(string v){try{return String.IsNullOrEmpty(v)?"":Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(v),null,DataProtectionScope.CurrentUser));}catch{return "";}}
  public string ApiKey{get{return Unprotect(State.Settings.KeyCipher);}set{State.Settings.KeyCipher=Protect(value);}}
  public string PhoneTopic{get{return Unprotect(State.Settings.TopicCipher);}set{State.Settings.TopicCipher=Protect(value);}}
  public string NewTopic(){var b=new byte[24];using(var r=RandomNumberGenerator.Create())r.GetBytes(b);PhoneTopic="movid-"+BitConverter.ToString(b).Replace("-","").ToLowerInvariant();Save();return PhoneTopic;}
  public static void SetStartup(bool enabled,string exe){using(var k=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(enabled)k.SetValue("MoVid","\""+exe+"\" --background",RegistryValueKind.String);else k.DeleteValue("MoVid",false);}}
  public static bool StartupEnabled(string exe){using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))return k!=null&&Convert.ToString(k.GetValue("MoVid","")).IndexOf("\""+exe+"\"",StringComparison.OrdinalIgnoreCase)>=0;}
  public string Export(){var s=J.Serializer().Deserialize<AppState>(J.Serializer().Serialize(State));s.Settings.KeyCipher="";s.Settings.TopicCipher="";s.Settings.Mobile=false;s.Sent.Clear();s.TrailerSeen.Clear();s.Inbox.Clear();return J.Serializer().Serialize(s);}
  public void Import(string json){
   if(json.Length>2*1024*1024)throw new Exception("Backup must be smaller than 2 MB.");var s=J.Serializer().Deserialize<AppState>(json);
   if(s==null||s.Settings==null||s.Follows==null||s.Follows.Count>500||s.Saved==null||s.Dismissed==null)throw new Exception("Invalid MoVid backup.");
   var ids=new System.Collections.Generic.HashSet<int>();foreach(var p in s.Follows)if(p.Id<=0||!ids.Add(p.Id)||String.IsNullOrWhiteSpace(p.Name)||p.Name.Length>200||!Rules.ValidDay(p.Since))throw new Exception("Invalid person in backup.");
   if(s.Settings.Region==null||!System.Text.RegularExpressions.Regex.IsMatch(s.Settings.Region,"^[A-Z]{2}$")||s.Settings.Hour<0||s.Settings.Hour>23||Array.IndexOf(new[]{"both","before","day","none"},s.Settings.Timing)<0)throw new Exception("Invalid preferences in backup.");
   s.Settings.KeyCipher=State.Settings.KeyCipher;s.Settings.TopicCipher=State.Settings.TopicCipher;s.Settings.Mobile=false;s.Settings.Onboarded=true;
   s.Releases=new System.Collections.Generic.List<Release>();s.Inbox=new System.Collections.Generic.List<Notice>();s.Sent=new System.Collections.Generic.Dictionary<string,string>();s.TrailerSeen=new System.Collections.Generic.Dictionary<string,System.Collections.Generic.List<string>>();s.LastCheck="";s.LastError="";State=s;Save();
  }
 }
}
