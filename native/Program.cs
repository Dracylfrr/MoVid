using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
[assembly: AssemblyTitle("MoVid")]
[assembly: AssemblyDescription("Personal movie and TV release reminders")]
[assembly: AssemblyCompany("MoVid contributors")]
[assembly: AssemblyProduct("MoVid")]
[assembly: AssemblyVersion("0.2.1.0")]
[assembly: AssemblyFileVersion("0.2.1.0")]
namespace MoVid {
 static class Program {
  public const string ApplicationUrl="https://github.com/Dracylfrr/MoVid";
  static Mutex mutex;
  [DllImport("shell32.dll",CharSet=CharSet.Unicode)]static extern int SetCurrentProcessExplicitAppUserModelID(string id);
  [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
  [DllImport("user32.dll")]static extern IntPtr FindWindow(string cls,string title);
  [DllImport("user32.dll")]static extern bool PostMessage(IntPtr handle,int msg,IntPtr w,IntPtr l);
  public const int ShowMessage=0x8001,QuitMessage=0x8002;
  [STAThread]static void Main(string[] args){
   System.Net.ServicePointManager.SecurityProtocol=System.Net.SecurityProtocolType.Tls12;
   SetProcessDPIAware();SetCurrentProcessExplicitAppUserModelID("MoVid.Personal.Desktop");
   if(args.Contains("--quit")){var w=FindWindow(null,"MoVid");if(w!=IntPtr.Zero)PostMessage(w,QuitMessage,IntPtr.Zero,IntPtr.Zero);return;}
   bool testing=args.Contains("--self-test")||args.Contains("--smoke-test");
   if(args.Contains("--self-test")){Environment.ExitCode=SelfTests.Run(args);return;}
   if(!testing){bool created;mutex=new Mutex(true,"Local\\MoVid.Personal.Desktop",out created);if(!created){var w=FindWindow(null,"MoVid");if(w!=IntPtr.Zero)PostMessage(w,ShowMessage,IntPtr.Zero,IntPtr.Zero);return;}}
   Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
   string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"MoVid","Data");
   if(testing){int i=Array.IndexOf(args,"--test-dir");if(i<0||i+1>=args.Length)throw new ArgumentException("Tests require --test-dir.");data=Path.GetFullPath(args[i+1]);}
   try{var form=new MainForm(new Storage(data),args.Contains("--background"),testing);if(testing)form.Shown+=async delegate{await form.RunSmokeTests(data);};Application.Run(form);}
   catch(Exception ex){if(testing)File.WriteAllText(Path.Combine(data,"fatal.txt"),ex.ToString());else MessageBox.Show("MoVid could not start.\n"+ex.Message,"MoVid",MessageBoxButtons.OK,MessageBoxIcon.Error);Environment.ExitCode=1;}
   finally{if(mutex!=null)mutex.Dispose();}
  }
 }
}
