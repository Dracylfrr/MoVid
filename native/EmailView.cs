using System;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
namespace MoVid {
 public partial class MainForm {
  bool advancedVisible,emailChanging,emailBusy;string emailChallenge="";TextBox emailAddressBox,emailCodeBox;CheckBox emailEnabledBox,emailPriorityBox;
  void EmailCard(){
   var box=Stack();box.Controls.Add(LabelText("Email alerts",18,accent));
   box.Controls.Add(LabelText("Enter your email, type the six-digit code we send you, and follow your favorite people.",10,muted));
   box.Controls.Add(LabelText("Default: one reminder the day before release, at 9 AM on this computer. Other reminders can be enabled in Settings.",10,accent));
   if(!engine.Api.Email.Configured)box.Controls.Add(LabelText("The MoVid owner has not activated email alerts yet. You can still use desktop reminders with a personal connection under Advanced setup.",10,Color.Salmon));
   else box.Controls.Add(LabelText(engine.Api.Email.Connected?"Connected · "+store.State.Settings.EmailAddress:"Not connected yet. We will only send to the address you verify.",10,accent));
   emailPriorityBox=Check("Request high-priority emails",store.State.Settings.EmailHighPriority);box.Controls.Add(emailPriorityBox);
   box.Controls.Add(LabelText("Gmail chooses which emails are Important. For High priority only notifications, use the Gmail setup guide below and send a test email.",9,muted));
   if(!engine.Api.Email.Connected||emailChanging){
   box.Controls.Add(LabelText("Your email address",10));emailAddressBox=new TextBox{Width=440,Text=store.State.Settings.EmailAddress,BackColor=bg,ForeColor=Color.WhiteSmoke,Margin=new Padding(0,0,0,12)};box.Controls.Add(emailAddressBox);
   var actions=Row();var start=Btn("Send me a code",async()=>await Safe(async()=>{
    if(emailBusy)return;emailBusy=true;try{
    if(engine.Busy)throw new Exception("Wait for the current check to finish.");
    store.State.Settings.EmailHighPriority=emailPriorityBox.Checked;string address=emailAddressBox.Text.Trim();
    if(address.Length>254||!System.Text.RegularExpressions.Regex.IsMatch(address,"^[^\\s@<>]+@[^\\s@<>]+\\.[^\\s@<>]+$"))throw new Exception("Enter a valid email address.");
    SetStatus("Sending your verification code…");emailChallenge=await engine.Api.Email.Start(address);SetStatus("Check your inbox or spam folder for the six-digit MoVid code.");
    }finally{emailBusy=false;}
   }),true);start.Enabled=engine.Api.Email.Configured;actions.Controls.Add(start);box.Controls.Add(actions);
   box.Controls.Add(LabelText("Verification code",10));emailCodeBox=new TextBox{Width=180,MaxLength=6,BackColor=bg,ForeColor=Color.WhiteSmoke,Margin=new Padding(0,0,0,12)};box.Controls.Add(emailCodeBox);
   actions=Row();var verify=Btn("Verify and continue",async()=>await Safe(async()=>{
    if(emailBusy)return;emailBusy=true;try{
    if(emailChallenge.Length==0)throw new Exception("Choose Send me a code first.");
    await engine.Api.Email.Verify(emailChallenge,emailCodeBox.Text.Trim());emailChallenge="";emailChanging=false;Navigate("following");SetStatus("Email connected. Search for someone and select Follow.");
    }finally{emailBusy=false;}
   }),true);verify.Enabled=engine.Api.Email.Configured;actions.Controls.Add(verify);box.Controls.Add(actions);
   }else{var connectedActions=Row();connectedActions.Controls.Add(Btn("Find people to follow",()=>Navigate("following"),true));connectedActions.Controls.Add(Btn("Change email",()=>{emailChanging=true;Navigate(view);}));box.Controls.Add(connectedActions);}
   emailEnabledBox=Check("Email me my selected release reminders",store.State.Settings.EmailEnabled);box.Controls.Add(emailEnabledBox);
   var savedActions=Row();savedActions.Controls.Add(Btn("Save email preferences",()=>{store.State.Settings.EmailEnabled=emailEnabledBox.Checked;store.State.Settings.EmailHighPriority=emailPriorityBox.Checked;store.Save();SetStatus("Email preferences saved.");}));
   var test=Btn("Send test email",async()=>await Safe(async()=>{
    if(!engine.Api.Email.Connected)throw new Exception("Verify your email first.");
    store.State.Settings.EmailEnabled=emailEnabledBox.Checked;store.State.Settings.EmailHighPriority=emailPriorityBox.Checked;store.Save();
    await engine.Api.Email.Send("test:"+DateTime.UtcNow.Ticks,"Your MoVid email alerts are connected.\nThis is a test. Future release reminders will use the movie or show title as the subject.",null);SetStatus("The email service accepted your test. Check your inbox; then test Gmail’s phone notification setting.");
   }));test.Enabled=engine.Api.Email.Configured;savedActions.Controls.Add(test);box.Controls.Add(savedActions);
   var helpActions=Row();helpActions.Controls.Add(Btn("Gmail high-priority setup",GmailHelp));helpActions.Controls.Add(Btn("Disconnect email",async()=>await Safe(async()=>{await engine.Api.Email.Disconnect();Navigate(view);SetStatus("This device is disconnected from email delivery.");})));box.Controls.Add(helpActions);
   if(!engine.Api.Email.Configured)box.Controls.Add(Btn("Owner activation guide ↗",()=>OpenUrl(Program.ApplicationUrl+"/blob/main/server/OWNER-SETUP.md")));
   box.Controls.Add(LabelText("Turn email off here or use the unsubscribe link in a release email. Your computer must be on and MoVid running. Quitting or uninstalling stops new reminders from this computer.",9,muted));
   content.Controls.Add(box);
  }
  void GmailHelp(){
   MessageBox.Show("Gmail controls which emails it considers important; MoVid cannot change that label from the sender side.\n\nFor your High priority only setting:\n1. Send a test email from MoVid.\n2. Open Gmail on your computer and select that email.\n3. Choose More → Filter messages like these.\n4. Create a filter and choose Always mark it as important.\n5. Send another test email and check your iPhone.\n\nKeep Gmail and iPhone notifications enabled. Focus modes, previews, inbox categories, and Gmail decisions can still affect phone alerts.\n\nThe app’s Request high-priority emails option only adds standard priority headers; it does not guarantee a Gmail notification.","MoVid · Gmail phone notifications",MessageBoxButtons.OK,MessageBoxIcon.Information);
  }
 }
}
