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
    internal sealed class OnlinePhoneControllerClient:IDisposable {
        readonly JavaScriptSerializer json=new JavaScriptSerializer();ClientWebSocket socket;CancellationTokenSource cancel;
        public event Action<string> Status;public event Action<byte[]> Frame;public bool Connected{get{return socket!=null&&socket.State==WebSocketState.Open;}}
        public async Task Connect(string room,string pin){Dispose();string baseUrl=ConfigurationManager.AppSettings["RelayWebSocketUrl"]??"";if(baseUrl.Contains("YOUR-WORKER")||!baseUrl.StartsWith("wss://"))throw new InvalidOperationException("Set RelayWebSocketUrl in SmartHubRemote.exe.config first");cancel=new CancellationTokenSource();socket=new ClientWebSocket();await socket.ConnectAsync(new Uri(baseUrl+"/"+room+"?role=controller&pin="+pin),cancel.Token);Status?.Invoke("Online relay connected");Task.Run(()=>ReadLoop());}
        async Task ReadLoop(){byte[] buffer=new byte[700000];try{while(Connected){using(var ms=new MemoryStream()){WebSocketReceiveResult result;do{result=await socket.ReceiveAsync(new ArraySegment<byte>(buffer),cancel.Token);if(result.MessageType==WebSocketMessageType.Close){Status?.Invoke("Online relay closed");return;}ms.Write(buffer,0,result.Count);}while(!result.EndOfMessage);byte[] data=ms.ToArray();if(result.MessageType==WebSocketMessageType.Binary)Frame?.Invoke(data);else{var m=json.Deserialize<Dictionary<string,object>>(Encoding.UTF8.GetString(data));if(m!=null&&m.ContainsKey("type")&&Convert.ToString(m["type"])=="error")Status?.Invoke("Phone error: "+Convert.ToString(m["message"]));}}}}catch(Exception e){Status?.Invoke("Online disconnected: "+e.Message);}}
        public void Send(object value){if(!Connected)return;byte[] b=Encoding.UTF8.GetBytes(json.Serialize(value));Task.Run(async()=>{try{await socket.SendAsync(new ArraySegment<byte>(b),WebSocketMessageType.Text,true,cancel.Token);}catch{}});}
        public void Tap(double x,double y){Send(new{type="phone_tap",x=x,y=y});}public void Swipe(double x1,double y1,double x2,double y2,long duration){Send(new{type="phone_swipe",x1=x1,y1=y1,x2=x2,y2=y2,duration=duration});}public void Global(string action){Send(new{type="phone_global",action=action});}public void Text(string text){Send(new{type="phone_text",text=text});}
        public void Dispose(){try{if(cancel!=null)cancel.Cancel();}catch{}try{if(socket!=null)socket.Dispose();}catch{}socket=null;cancel=null;}
    }
}
