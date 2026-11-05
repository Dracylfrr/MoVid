using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace MoVid {
 public partial class MainForm:Form {
  readonly Storage store;readonly Engine engine;readonly bool testing;bool quitting,initialHidden,previousBusy;int searchVersion;string view="upcoming";
  readonly Color bg=Color.FromArgb(16,20,17),panelColor=Color.FromArgb(27,33,28),accent=Color.FromArgb(211,244,122),muted=Color.FromArgb(166,179,161);
  Panel side,body;FlowLayoutPanel content;Label status,title,subtitle;Button refresh;NotifyIcon tray;
  TextBox search,keyBox,topicBox;ListBox suggestions;List<Person> found=new List<Person>(),cache=new List<Person>();
  ComboBox region,timing,hour;CheckBox notifications,trailers,confirmed,theater,digital,tv,mobile,startup,fast;
  readonly Dictionary<string,Image> images=new Dictionary<string,Image>();readonly HashSet<string> downloading=new HashSet<string>();
  readonly System.Windows.Forms.Timer timer=new System.Windows.Forms.Timer();
  static readonly HttpClient ImageClient=new HttpClient{Timeout=TimeSpan.FromSeconds(12)};
  public MainForm(Storage storage,bool hidden,bool test){
   store=storage;testing=test;initialHidden=hidden&&!test;engine=new Engine(store);engine.Changed=UpdateState;engine.Desktop=ShowNotice;
   Text="MoVid";MinimumSize=new Size(840,620);Size=new Size(1180,800);StartPosition=FormStartPosition.CenterScreen;BackColor=bg;ForeColor=Color.WhiteSmoke;Font=new Font("Segoe UI",10);Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);DoubleBuffered=true;
   BuildShell();LoadCache();BuildTray();Navigate(store.State.Settings.Onboarded?"upcoming":"welcome");IntPtr windowHandle=Handle;
   FormClosing+=delegate(object sender,FormClosingEventArgs e){if(!quitting&&e.CloseReason==CloseReason.UserClosing){e.Cancel=true;Hide();}else quitting=true;};
   FormClosed+=delegate{timer.Stop();tray.Visible=false;tray.Dispose();foreach(var image in images.Values)image.Dispose();};
   timer.Interval=5000;timer.Tick+=async delegate{timer.Interval=300000;if(!engine.Busy)await Safe(async()=>await engine.Refresh(false));};if(!test)timer.Start();
   Microsoft.Win32.SystemEvents.PowerModeChanged+=PowerChanged;
   FormClosed+=delegate{Microsoft.Win32.SystemEvents.PowerModeChanged-=PowerChanged;};
  }
  protected override void SetVisibleCore(bool value){if(initialHidden){initialHidden=false;base.SetVisibleCore(false);return;}base.SetVisibleCore(value);}
  protected override void WndProc(ref Message m){if(m.Msg==Program.ShowMessage){OpenWindow();return;}if(m.Msg==Program.QuitMessage){Quit();return;}base.WndProc(ref m);}
  void PowerChanged(object sender,Microsoft.Win32.PowerModeChangedEventArgs e){if(e.Mode==Microsoft.Win32.PowerModes.Resume&&!IsDisposed)BeginInvoke(new Action(async()=>await Safe(async()=>await engine.Refresh(false))));}
  void OpenWindow(){Show();if(WindowState==FormWindowState.Minimized)WindowState=FormWindowState.Normal;Activate();}
  void Quit(){quitting=true;Application.Exit();}
  Button Btn(string text,Action action,bool primary=false){var b=new Button{Text=text,AutoSize=true,MinimumSize=new Size(90,36),FlatStyle=FlatStyle.Flat,BackColor=primary?accent:panelColor,ForeColor=primary?bg:Color.WhiteSmoke,Padding=new Padding(10,4,10,4),Margin=new Padding(0,0,10,8),Cursor=Cursors.Hand};b.FlatAppearance.BorderColor=Color.FromArgb(58,72,54);b.Click+=delegate{action();};return b;}
  Label LabelText(string text,float size=10,Color? color=null){return new Label{Text=text,AutoSize=true,MaximumSize=new Size(850,0),Font=new Font("Segoe UI",size),ForeColor=color??Color.WhiteSmoke,Margin=new Padding(0,0,0,10)};}
  async Task Safe(Func<Task> action){try{await action();}catch(Exception ex){SetStatus(ex.Message,true);}}
  void SetStatus(string text,bool error=false){status.Text=text;status.ForeColor=error?Color.Salmon:muted;}
  void BuildShell(){
   side=new Panel{Dock=DockStyle.Left,Width=205,Padding=new Padding(22,28,14,20),BackColor=Color.FromArgb(21,27,22)};Controls.Add(side);
   var nav=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false};side.Controls.Add(nav);
   nav.Controls.Add(LabelText("MoVid ●",26,accent));nav.Controls.Add(LabelText("YOUR NEXT PICTURE",8,muted));
   foreach(var entry in new[]{new[]{"Upcoming","upcoming"},new[]{"Following","following"},new[]{"Watchlist","watchlist"},new[]{"Notifications","inbox"},new[]{"Settings","settings"}}){string id=entry[1];var button=Btn(entry[0],()=>Navigate(id));button.Width=165;button.Height=43;button.Margin=new Padding(0,12,0,0);nav.Controls.Add(button);}
   var footer=LabelText("Personal. Local. No AI.\nClose the window to keep\nMoVid in the system tray.",9,muted);footer.Dock=DockStyle.Bottom;footer.Height=80;footer.AutoSize=false;side.Controls.Add(footer);
   body=new Panel{Dock=DockStyle.Fill,Padding=new Padding(32,25,25,20)};Controls.Add(body);body.BringToFront();
   var top=new Panel{Dock=DockStyle.Top,Height=130};body.Controls.Add(top);title=LabelText("",27);title.Location=new Point(0,30);top.Controls.Add(title);subtitle=LabelText("",10,muted);subtitle.Location=new Point(1,86);top.Controls.Add(subtitle);
   var eyebrow=LabelText("YOUR PERSONAL RELEASE RADAR",8,accent);eyebrow.Location=new Point(2,0);top.Controls.Add(eyebrow);
   refresh=Btn("Check releases",async()=>await Safe(async()=>{await engine.Refresh(true);SetStatus("Upcoming releases checked.");}));refresh.Anchor=AnchorStyles.Top|AnchorStyles.Right;refresh.Location=new Point(top.Width-145,0);top.Controls.Add(refresh);top.Resize+=delegate{refresh.Left=top.ClientSize.Width-refresh.Width;};
   status=LabelText("Ready",9,muted);status.Dock=DockStyle.Bottom;status.Height=38;status.AutoSize=false;body.Controls.Add(status);
   content=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(2,8,15,12)};body.Controls.Add(content);content.BringToFront();content.Resize+=delegate{foreach(Control c in content.Controls)if(c is FlowLayoutPanel||c is Panel)c.Width=Math.Max(400,content.ClientSize.Width-28);};
  }
  void BuildTray(){
   tray=new NotifyIcon{Icon=Icon,Text="MoVid · release reminders",Visible=true};var menu=new ContextMenuStrip();menu.Items.Add("Open MoVid",null,delegate{OpenWindow();});menu.Items.Add("Check releases",null,async delegate{await Safe(async()=>await engine.Refresh(true));});
   var startItem=new ToolStripMenuItem("Start with Windows"){CheckOnClick=true,Checked=!testing&&Storage.StartupEnabled(Application.ExecutablePath)};startItem.CheckedChanged+=delegate{if(!testing)Storage.SetStartup(startItem.Checked,Application.ExecutablePath);};menu.Items.Add(startItem);menu.Items.Add(new ToolStripSeparator());menu.Items.Add("Quit MoVid",null,delegate{Quit();});tray.ContextMenuStrip=menu;tray.DoubleClick+=delegate{OpenWindow();};tray.BalloonTipClicked+=delegate{OpenWindow();Navigate("inbox");};
  }
  void ShowNotice(string heading,string message){if(testing)return;tray.ShowBalloonTip(8000,"MoVid",message.Length>250?message.Substring(0,247)+"…":message,ToolTipIcon.Info);}
  public void Navigate(string next){
   view=next;foreach(Control c in content.Controls.Cast<Control>().ToArray())c.Dispose();content.Controls.Clear();
   string heading=next=="following"?"Follow their next move.":next=="settings"?"Make it yours.":next=="inbox"?"Your notifications.":next=="watchlist"?"Your watchlist.":next=="welcome"?"Welcome to MoVid.":"Something to look forward to.";
   title.Text=heading;subtitle.Text=next=="following"?"Search a name, press Enter, and select their photo to follow.":next=="settings"?"Personal preferences, startup, and optional phone alerts.":next=="welcome"?"A lightweight release tracker that runs on your computer.":"Future releases from the people you follow.";
   if(next=="welcome")Welcome();else if(next=="settings")SettingsView();else if(next=="following")Following();else if(next=="inbox")Inbox();else Releases(next=="watchlist");UpdateState();
  }
  FlowLayoutPanel Stack(){int width=Math.Max(500,content.ClientSize.Width-30);return new FlowLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,MinimumSize=new Size(width,0),MaximumSize=new Size(width,0),FlowDirection=FlowDirection.TopDown,WrapContents=false,Width=width,BackColor=panelColor,Padding=new Padding(20),Margin=new Padding(0,0,0,18)};}
  FlowLayoutPanel Row(){return new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.LeftToRight,WrapContents=true,Width=Math.Max(500,content.ClientSize.Width-30),Margin=new Padding(0,0,0,14)};}
  void Welcome(){var box=Stack();box.Controls.Add(LabelText("Follow people. Catch what’s next.",18,accent));box.Controls.Add(LabelText("1. Connect the movie database once.\n2. Search for an actor or filmmaker and select Follow.\n3. Choose trailers, confirmed dates, and release-day reminders.",11));box.Controls.Add(LabelText("Closing this window keeps MoVid in your tray. It starts quietly with Windows after installation.\nUse the tray menu to quit completely. No checks or new sends happen while the app is quit or uninstalled.",10,muted));box.Controls.Add(LabelText("Windows notifications are built in. Phone lock-screen alerts use the optional free ntfy app, not SMS or Apple Messages.",10,muted));box.Controls.Add(Btn("Get started",()=>{store.State.Settings.Onboarded=true;store.Save();Navigate("settings");},true));content.Controls.Add(box);}
  void UpdateState(){
   if(IsDisposed)return;if(InvokeRequired){BeginInvoke(new Action(UpdateState));return;}
   bool finished=previousBusy&&!engine.Busy;previousBusy=engine.Busy;
   if(finished&&(view=="upcoming"||view=="watchlist"||view=="inbox")){Navigate(view);return;}
   refresh.Enabled=!engine.Busy;
   SetStatus(engine.Busy?engine.Progress:store.State.LastError.Length>0?store.State.LastError:store.LoadWarning.Length>0?store.LoadWarning:store.State.LastCheck.Length>0?"Last checked "+DateTime.Parse(store.State.LastCheck).ToLocalTime().ToString("g"):"Ready · add a movie database connection in Settings to begin.",store.State.LastError.Length>0);
  }
  void LoadCache(){try{string path=Path.Combine(store.DirectoryPath,"people-cache.json");if(File.Exists(path))cache=J.Serializer().Deserialize<List<Person>>(File.ReadAllText(path))??new List<Person>();}catch{cache=new List<Person>();}}
  void SaveCache(){cache=cache.GroupBy(p=>p.Id).Select(g=>g.Last()).Take(5000).ToList();File.WriteAllText(Path.Combine(store.DirectoryPath,"people-cache.json"),J.Serializer().Serialize(cache));}
  void Following(){
   var row=Row();search=new TextBox{Width=360,Font=new Font("Segoe UI",12),BackColor=panelColor,ForeColor=Color.WhiteSmoke,BorderStyle=BorderStyle.FixedSingle,Margin=new Padding(0,4,12,8)};row.Controls.Add(search);row.Controls.Add(Btn("Search",async()=>await Safe(Search),true));content.Controls.Add(row);
   suggestions=new ListBox{Width=500,Height=120,Visible=false,BackColor=panelColor,ForeColor=Color.WhiteSmoke,BorderStyle=BorderStyle.FixedSingle,DisplayMember="Name"};content.Controls.Add(suggestions);
   search.KeyDown+=async delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;await Safe(Search);}if(e.KeyCode==Keys.Down&&suggestions.Visible)suggestions.Focus();};
   search.TextChanged+=delegate{if(!store.State.Settings.FastSearch)return;string q=search.Text.Trim();var matches=q.Length<2?new List<Person>():cache.Concat(store.State.Follows).GroupBy(p=>p.Id).Select(g=>g.First()).Where(p=>p.Name.IndexOf(q,StringComparison.CurrentCultureIgnoreCase)>=0).OrderBy(p=>p.Name).Take(8).ToList();suggestions.DataSource=matches;suggestions.Visible=matches.Count>0;};
   suggestions.DoubleClick+=delegate{PickSuggestion();};suggestions.KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Enter){e.SuppressKeyPress=true;PickSuggestion();}};
   var sort=Row();sort.Controls.Add(LabelText("Your people",14));sort.Controls.Add(Btn("Recently added",()=>{store.State.Settings.Sort="recent";store.Save();Navigate("following");}));sort.Controls.Add(Btn("A–Z",()=>{store.State.Settings.Sort="az";store.Save();Navigate("following");}));content.Controls.Add(sort);
   var followed=store.State.Settings.Sort=="az"?store.State.Follows.OrderBy(p=>p.Name).ToList():store.State.Follows.OrderByDescending(p=>p.Added).ToList();
   foreach(var p in followed)content.Controls.Add(PersonCard(p,true));
   if(followed.Count==0)content.Controls.Add(LabelText("Start with someone you love watching. Search a name above.",11,muted));
  }
  void PickSuggestion(){var p=suggestions.SelectedItem as Person;if(p==null)return;found=new List<Person>{p};suggestions.Visible=false;ShowResults();}
  async Task Search(){if(engine.Busy)throw new Exception("Wait for the current release check to finish.");string q=search.Text.Trim();if(q.Length<2){SetStatus("Enter at least two letters.");return;}int version=++searchVersion;SetStatus("Searching people…");var matches=await engine.Api.Search(q);cache.AddRange(matches);SaveCache();if(version!=searchVersion||view!="following")return;found=matches;suggestions.Visible=false;ShowResults();SetStatus(found.Count+" matching people. Choose the right photo and follow.");}
  void ShowResults(){
   var previous=content.Controls.OfType<FlowLayoutPanel>().FirstOrDefault(c=>Convert.ToString(c.Tag)=="results");if(previous!=null){content.Controls.Remove(previous);previous.Dispose();}
   var results=Stack();results.Tag="results";results.Controls.Add(LabelText("Search results",14,accent));foreach(var p in found)results.Controls.Add(PersonCard(p,false));if(found.Count==0)results.Controls.Add(LabelText("No matching people found. Try another spelling.",10,muted));content.Controls.Add(results);content.Controls.SetChildIndex(results,2);
  }
  Panel PersonCard(Person p,bool followed){
   var card=new Panel{Width=Math.Max(500,content.ClientSize.Width-70),Height=98,BackColor=panelColor,Margin=new Padding(0,0,0,10)};
   var image=new PictureBox{Left=10,Top=10,Width=56,Height=76,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.FromArgb(48,62,39)};card.Controls.Add(image);LoadPhoto(image,p.Photo);
   var name=LabelText(p.Name,12);name.Location=new Point(80,18);name.MaximumSize=new Size(card.Width-250,40);card.Controls.Add(name);var detail=LabelText(followed?"Added "+p.Since:p.Department,9,muted);detail.Location=new Point(81,52);card.Controls.Add(detail);
   bool already=store.State.Follows.Any(x=>x.Id==p.Id);var action=Btn(followed?"Unfollow":already?"Following":"Follow",async()=>await Safe(async()=>{if(followed)engine.Unfollow(p.Id);else await engine.Follow(p);Navigate("following");}),!followed&&!already);action.Enabled=followed||!already;action.Left=card.Width-action.Width-15;action.Top=28;card.Controls.Add(action);if(!followed&&!already){image.Cursor=Cursors.Hand;image.Click+=async delegate{await Safe(async()=>{await engine.Follow(p);Navigate("following");});};}return card;
  }
  async void LoadPhoto(PictureBox picture,string path){
   if(String.IsNullOrEmpty(path)||!System.Text.RegularExpressions.Regex.IsMatch(path,"^/[A-Za-z0-9_.-]+$"))return;
   if(images.ContainsKey(path)){picture.Image=images[path];return;}if(downloading.Contains(path))return;
   downloading.Add(path);try{var bytes=await ImageClient.GetByteArrayAsync("https://image.tmdb.org/t/p/w185"+path);using(var stream=new MemoryStream(bytes))using(var raw=Image.FromStream(stream)){var image=new Bitmap(raw);if(images.Count<160){images[path]=image;if(!picture.IsDisposed)picture.Image=image;}else image.Dispose();}}catch{}finally{downloading.Remove(path);}
  }
  void Releases(bool saved){
   var s=store.State;var events=s.Releases.Where(r=>saved?s.Saved.Contains(r.Id):!s.Dismissed.Contains(r.Id)&&(r.Kind=="unknown"||Rules.Enabled(r,s.Settings))).ToList();
   if(events.Count==0){var box=Stack();box.Controls.Add(LabelText(saved?"Save something to look forward to.":"Your next favorite is still ahead.",18,accent));box.Controls.Add(LabelText("Follow someone and check for upcoming releases. Past work is not shown.",11,muted));box.Controls.Add(Btn("Find your people",()=>Navigate("following"),true));content.Controls.Add(box);}
   foreach(var r in events){var box=Stack();box.Controls.Add(LabelText(Rules.ValidDay(r.Date)?DateTime.ParseExact(r.Date,"yyyy-MM-dd",CultureInfo.InvariantCulture).ToString("ddd, MMM d, yyyy"):"DATE TO BE ANNOUNCED",10,accent));box.Controls.Add(LabelText(r.Title,17));box.Controls.Add(LabelText(Rules.Kind(r.Kind)+(r.Kind=="tv"?" · availability in your country may differ":" · "+r.Region),10,muted));box.Controls.Add(LabelText("Following "+String.Join(" · ",s.Follows.Where(p=>r.People.Contains(p.Id)).Select(p=>p.Name)),10));if(r.Stale)box.Controls.Add(LabelText("Date needs rechecking. Reminders paused for this title.",10,Color.Salmon));
    var row=Row();row.Controls.Add(Btn(s.Saved.Contains(r.Id)?"✓ Saved":"Save to watchlist",()=>{if(s.Saved.Contains(r.Id))s.Saved.Remove(r.Id);else s.Saved.Add(r.Id);store.Save();Navigate(view);}));row.Controls.Add(Btn(s.Dismissed.Contains(r.Id)?"Restore":"Dismiss",()=>{if(s.Dismissed.Contains(r.Id))s.Dismissed.Remove(r.Id);else s.Dismissed.Add(r.Id);store.Save();Navigate(view);}));row.Controls.Add(Btn("Details ↗",()=>OpenUrl("https://www.themoviedb.org/"+r.Media+"/"+r.TitleId)));box.Controls.Add(row);content.Controls.Add(box);}
   content.Controls.Add(LabelText("Dates can change. Digital includes rent, buy, or stream; it does not guarantee a subscription premiere.",9,muted));
  }
  void Inbox(){foreach(var n in store.State.Inbox){var box=Stack();box.Controls.Add(LabelText(n.Message,12));box.Controls.Add(LabelText(DateTime.Parse(n.Time).ToString("g")+" · "+n.PhoneStatus,9,muted));if(!String.IsNullOrEmpty(n.Url))box.Controls.Add(Btn("Watch trailer ↗",()=>OpenUrl(n.Url)));content.Controls.Add(box);}if(store.State.Inbox.Count==0)content.Controls.Add(LabelText("Quiet for now. Your release reminders and test alerts will appear here.",12,muted));}
  static void OpenUrl(string url){Uri uri;if(!Uri.TryCreate(url,UriKind.Absolute,out uri)||uri.Scheme!="https"||!new[]{"github.com","www.themoviedb.org","developer.themoviedb.org","docs.ntfy.sh","ntfy.sh","www.youtube.com","apps.apple.com","play.google.com"}.Contains(uri.Host))return;Process.Start(new ProcessStartInfo(url){UseShellExecute=true});}
 }
}
