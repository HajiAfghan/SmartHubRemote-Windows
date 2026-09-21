using System;
using System.Net;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace SmartHubRemote {
    static class Program {
        [DllImport("shell32.dll",SetLastError=true)]static extern int SetCurrentProcessExplicitAppUserModelID([MarshalAs(UnmanagedType.LPWStr)]string appId);
        [STAThread] static void Main() { try{SetCurrentProcessExplicitAppUserModelID("SmartHubTechnology.SmartHubRemote");}catch{}ServicePointManager.SecurityProtocol=SecurityProtocolType.Tls12; Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm()); }
    }
}
