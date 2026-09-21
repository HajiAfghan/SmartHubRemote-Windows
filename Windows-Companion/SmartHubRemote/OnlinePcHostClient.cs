using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace SmartHubRemote {
    // Makes the Windows companion a relay host. Control commands use the same
    // protocol as LAN; JPEG screen frames are sent as binary to avoid Base64 delay.
    internal sealed class OnlinePcHostClient:IDisposable {
        readonly RemoteServer server;readonly JavaScriptSerializer json=new JavaScriptSerializer{MaxJsonLength=64*1024*1024};
        readonly SemaphoreSlim sendLock=new SemaphoreSlim(1,1);ClientWebSocket socket;CancellationTokenSource cancel;SessionState state;
        string room,pin,secret;public event Action<string> Status;
        public bool Connected{get{return socket!=null&&socket.State==WebSocketState.Open;}}
        public OnlinePcHostClient(RemoteServer remote){server=remote;}
        public void Start(string deviceId,string currentPin,string deviceSecret){room=deviceId;pin=currentPin;secret=deviceSecret;try{if(cancel!=null)cancel.Cancel();}catch{}if(state!=null)state.Streaming=false;DisposeSocket();cancel=new CancellationTokenSource();Task.Run(()=>ConnectLoop(cancel.Token));}
        public void ChangePin(string value){pin=value;Start(room,pin,secret);}
        async Task ConnectLoop(CancellationToken token){while(!token.IsCancellationRequested){try{string baseUrl=ConfigurationManager.AppSettings["RelayWebSocketUrl"]??"";if(baseUrl.Contains("YOUR-WORKER")||!baseUrl.StartsWith("wss://"))throw new InvalidOperationException("Relay URL is not configured");socket=new ClientWebSocket();socket.Options.SetRequestHeader("X-Device-Secret",secret);await socket.ConnectAsync(new Uri(baseUrl+"/"+room+"?role=host&pin="+pin),token);state=new SessionState{Paired=true,Name="Online controller"};Status?.Invoke("Online ready • ID "+room);await ReadLoop(token);}catch(OperationCanceledException){return;}catch(Exception e){Status?.Invoke("Online relay: "+e.Message);}if(state!=null)state.Streaming=false;DisposeSocket();if(!token.IsCancellationRequested)try{await Task.Delay(5000,token);}catch{return;}}}
        async Task ReadLoop(CancellationToken token){byte[] buffer=new byte[128*1024];while(Connected&&!token.IsCancellationRequested){using(var ms=new MemoryStream()){WebSocketReceiveResult result;do{result=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),token);if(result.MessageType==WebSocketMessageType.Close)return;ms.Write(buffer,0,result.Count);}while(!result.EndOfMessage);if(result.MessageType!=WebSocketMessageType.Text)continue;string line=Encoding.UTF8.GetString(ms.ToArray());Dictionary<string,object> m=null;try{m=json.Deserialize<Dictionary<string,object>>(line);}catch{}string type=m!=null&&m.ContainsKey("type")?Convert.ToString(m["type"]):"";if(type=="relay_ready")continue;if(type=="peer_status"){bool online=m.ContainsKey("online")&&Convert.ToBoolean(m["online"]);Status?.Invoke(online?"Online controller connected • ID "+room:"Online ready • ID "+room);if(online)server.SendCurrentClipboard(SendFromServer);continue;}server.ProcessLine(line,SendFromServer,state);}}}
        void SendFromServer(string line){try{var m=json.Deserialize<Dictionary<string,object>>(line);string type=m!=null&&m.ContainsKey("type")?Convert.ToString(m["type"]):"";if((type=="screen"||type=="screenshot")&&m.ContainsKey("data")){Send(Convert.FromBase64String(Convert.ToString(m["data"])),WebSocketMessageType.Binary);return;}}catch{}Send(Encoding.UTF8.GetBytes(line),WebSocketMessageType.Text);}
        void Send(byte[] data,WebSocketMessageType type){Task.Run(async()=>{if(!Connected)return;await sendLock.WaitAsync();try{if(Connected)await socket.SendAsync(new ArraySegment<byte>(data),type,true,cancel.Token);}catch{}finally{sendLock.Release();}});}
        void DisposeSocket(){try{if(socket!=null)socket.Dispose();}catch{}socket=null;}
        public void Dispose(){try{if(cancel!=null)cancel.Cancel();}catch{}DisposeSocket();cancel=null;}
    }
}
