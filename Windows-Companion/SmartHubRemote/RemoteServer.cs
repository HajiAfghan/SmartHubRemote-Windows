using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows.Forms;

namespace SmartHubRemote {
    internal sealed class SessionState { public bool Paired; public bool Streaming; public string Name="Unknown"; }
    internal sealed class RemoteServer : IDisposable {
        readonly Control ui; readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=64*1024*1024};
        readonly ConcurrentDictionary<Guid,ClientSession> clients=new ConcurrentDictionary<Guid,ClientSession>();
        TcpListener listener; UdpClient discovery; CancellationTokenSource cancel; public string Pin{get;set;} public int Port{get;set;}=18788;
        public event Action<string> Log; public event Action<int> ClientCountChanged;
        public RemoteServer(Control ui,string pin){this.ui=ui;Pin=pin;}
        public void Start(){if(listener!=null)return;cancel=new CancellationTokenSource();listener=new TcpListener(IPAddress.Any,Port);listener.Start();discovery=new UdpClient(18789);Task.Run(()=>AcceptLoop(cancel.Token));Task.Run(()=>DiscoveryLoop(cancel.Token));Log?.Invoke("Server started on port "+Port);}
        async Task DiscoveryLoop(CancellationToken token){while(!token.IsCancellationRequested){try{UdpReceiveResult r=await discovery.ReceiveAsync();string q=Encoding.UTF8.GetString(r.Buffer);if(q=="DISCOVER_SMARTHUB_REMOTE"){byte[] data=Encoding.UTF8.GetBytes("SMARTHUB_REMOTE|"+Environment.MachineName+"|"+Port);await discovery.SendAsync(data,data.Length,r.RemoteEndPoint);}}catch{if(!token.IsCancellationRequested)await Task.Delay(300);}}}
        async Task AcceptLoop(CancellationToken token){while(!token.IsCancellationRequested){try{var tcp=await listener.AcceptTcpClientAsync();var c=new ClientSession(tcp);clients[c.Id]=c;ClientCountChanged?.Invoke(clients.Count);Task.Run(()=>ClientLoop(c,token));}catch{if(!token.IsCancellationRequested)Log?.Invoke("Connection accept failed");}}}
        async Task ClientLoop(ClientSession c,CancellationToken token){try{using(c.Tcp){var stream=c.Tcp.GetStream();using(var reader=new StreamReader(stream,Encoding.UTF8,false,8192,true))using(var writer=new StreamWriter(stream,new UTF8Encoding(false),8192,true){AutoFlush=true}){c.Send=line=>{lock(writer)writer.WriteLine(line);};string line;while(!token.IsCancellationRequested&&(line=await reader.ReadLineAsync())!=null)ProcessLine(line,c.Send,c.State);}}}catch(Exception e){Log?.Invoke("Client ended: "+e.Message);}finally{ClientSession old;clients.TryRemove(c.Id,out old);ClientCountChanged?.Invoke(clients.Count);}}
        public void ProcessLine(string line,Action<string> send,SessionState state){try{var m=json.Deserialize<Dictionary<string,object>>(line);string type=Get(m,"type");if(!state.Paired){if(type!="pair"||Get(m,"pin")!=Pin){Send(send,new{type="error",message="Invalid pairing PIN"});return;}state.Paired=true;state.Name=Get(m,"name");Send(send,new{type="paired",server=Environment.MachineName,protocol=3});Log?.Invoke(state.Name+" paired");return;}Log?.Invoke("Received: "+type);switch(type){
            // Native input does not need the WinForms UI thread. Running it directly also
            // avoids dropped BeginInvoke calls on older Windows 8 machines.
            case "move":NativeInput.Move(Num(m,"dx"),Num(m,"dy"));Ack(send,type);break;case "click":NativeInput.Click(Get(m,"button"));Ack(send,type);break;case "button":NativeInput.Button(Get(m,"action"));Ack(send,type);break;case "scroll":NativeInput.Scroll((int)Num(m,"dy"));Ack(send,type);break;
            case "key":NativeInput.Key((int)Num(m,"code"));Ack(send,type);break;case "text":NativeInput.Text(Get(m,"text"));Ack(send,type);break;
            case "clipboard":UI(()=>{try{Clipboard.SetText(Get(m,"text"));}catch{}});break;
            case "screenshot":SendImage(send,"screenshot");break;case "screen_start":if(!state.Streaming){state.Streaming=true;Log?.Invoke(state.Name+" started live screen");Send(send,new{type="screen_status",active=true});Task.Run(()=>StreamLoop(send,state));}break;case "screen_stop":state.Streaming=false;Send(send,new{type="screen_status",active=false});break;
            case "pointer":var b=Screen.PrimaryScreen.Bounds;NativeInput.SetPosition(b.Left+(int)(Num(m,"x")*b.Width),b.Top+(int)(Num(m,"y")*b.Height));if(Bool(m,"click"))NativeInput.Click("left");Ack(send,type);break;
            case "launch":Launch(Get(m,"app"));Ack(send,type);break;case "system":SystemAction(Get(m,"action"));Ack(send,type);break;case "file":SaveFile(m,send);break;
            default:throw new InvalidOperationException("Unknown command: "+type);
        }}catch(Exception e){Log?.Invoke("Command error: "+e.Message);Send(send,new{type="error",message=e.Message});}}
        async Task StreamLoop(Action<string> send,SessionState state){while(state.Streaming){SendImage(send,"screen");await Task.Delay(220);}}
        void SendImage(Action<string> send,string type){try{var b=Screen.PrimaryScreen.Bounds;using(var full=new Bitmap(b.Width,b.Height))using(var g=Graphics.FromImage(full)){g.CopyFromScreen(b.Left,b.Top,0,0,b.Size);int w=Math.Min(1280,full.Width),h=(int)Math.Round(full.Height*(w/(double)full.Width));using(var bmp=new Bitmap(full,w,h))using(var ms=new MemoryStream()){var codec=ImageCodecInfo.GetImageEncoders().First(x=>x.FormatID==ImageFormat.Jpeg.Guid);var ep=new EncoderParameters(1);ep.Param[0]=new EncoderParameter(System.Drawing.Imaging.Encoder.Quality,type=="screenshot"?72L:48L);bmp.Save(ms,codec,ep);Send(send,new{type=type,data=Convert.ToBase64String(ms.ToArray()),width=bmp.Width,height=bmp.Height});}}}catch(Exception e){Log?.Invoke("Screen capture: "+e.Message);}}
        void SaveFile(Dictionary<string,object> m,Action<string> send){byte[] data=Convert.FromBase64String(Get(m,"data"));if(data.Length>20*1024*1024)throw new InvalidOperationException("File exceeds 20 MB");string name=string.Concat(Get(m,"name").Select(ch=>Path.GetInvalidFileNameChars().Contains(ch)?'_':ch));string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads","SmartHub Remote");Directory.CreateDirectory(dir);string path=Unique(Path.Combine(dir,name));File.WriteAllBytes(path,data);Send(send,new{type="file_saved",path=path});Log?.Invoke("Received file: "+path);}
        void Launch(string app){var map=new Dictionary<string,string>{{"explorer","explorer.exe"},{"browser","https://www.google.com"},{"notepad","notepad.exe"},{"calculator","calc.exe"},{"taskmgr","taskmgr.exe"},{"settings","ms-settings:"}};string target;if(!map.TryGetValue(app,out target))throw new InvalidOperationException("Application is not allowed");Process.Start(new ProcessStartInfo(target){UseShellExecute=true});}
        void SystemAction(string action){if(action=="lock")UI(()=>NativeInput.LockWorkStation());else if(action=="sleep")UI(()=>NativeInput.SetSuspendState(false,false,false));else if(action=="restart")Process.Start("shutdown","/r /t 10 /c \"SmartHub Remote restart\"");else if(action=="shutdown")Process.Start("shutdown","/s /t 10 /c \"SmartHub Remote shutdown\"");}
        public void BroadcastClipboard(string text){foreach(var c in clients.Values)if(c.State.Paired)Send(c.Send,new{type="clipboard",text=text});}
        void Send(Action<string> send,object value){try{send(json.Serialize(value));}catch{}}
        void Ack(Action<string> send,string command){Send(send,new{type="ack",command=command});}
        void UI(Action a){if(ui.IsDisposed)return;if(ui.InvokeRequired)ui.BeginInvoke(a);else a();}
        static string Get(Dictionary<string,object> m,string k){object v;return m.TryGetValue(k,out v)&&v!=null?Convert.ToString(v,CultureInfo.InvariantCulture):"";} static double Num(Dictionary<string,object>m,string k){object v;if(!m.TryGetValue(k,out v)||v==null)return 0;try{return Convert.ToDouble(v,CultureInfo.InvariantCulture);}catch{double x;double.TryParse(Convert.ToString(v,CultureInfo.InvariantCulture),NumberStyles.Any,CultureInfo.InvariantCulture,out x);return x;}} static bool Bool(Dictionary<string,object>m,string k){bool x;bool.TryParse(Get(m,k),out x);return x;}
        static string Unique(string p){if(!File.Exists(p))return p;string d=Path.GetDirectoryName(p),n=Path.GetFileNameWithoutExtension(p),e=Path.GetExtension(p);for(int i=1;;i++){string x=Path.Combine(d,n+" ("+i+")"+e);if(!File.Exists(x))return x;}}
        public void Dispose(){if(cancel!=null)cancel.Cancel();try{listener?.Stop();}catch{}try{discovery?.Close();}catch{}listener=null;discovery=null;foreach(var c in clients.Values)try{c.Tcp.Close();}catch{}clients.Clear();}
        sealed class ClientSession{public Guid Id=Guid.NewGuid();public TcpClient Tcp;public SessionState State=new SessionState();public Action<string> Send;public ClientSession(TcpClient t){Tcp=t;}}
    }
}
