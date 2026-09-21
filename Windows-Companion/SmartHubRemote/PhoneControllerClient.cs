using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace SmartHubRemote {
    internal sealed class PhoneControllerClient:IDisposable {
        readonly JavaScriptSerializer json=new JavaScriptSerializer();readonly object writeLock=new object();TcpClient tcp;StreamWriter writer;volatile bool connected;
        public event Action<string> Status;public bool Connected{get{return connected;}}
        public void Connect(string host,string pin){DisposeSocket();tcp=new TcpClient();tcp.Connect(host,18790);tcp.NoDelay=true;var stream=tcp.GetStream();writer=new StreamWriter(stream,new UTF8Encoding(false)){AutoFlush=true};var reader=new StreamReader(stream,Encoding.UTF8);Write(new{type="pair",pin=pin,name=Environment.MachineName+" Windows Controller"});string first=reader.ReadLine();if(first==null)throw new IOException("Phone closed the connection");var reply=json.DeserializeObject(first) as System.Collections.Generic.Dictionary<string,object>;if(reply==null||!reply.ContainsKey("type")||Convert.ToString(reply["type"])!="paired")throw new InvalidOperationException(reply!=null&&reply.ContainsKey("message")?Convert.ToString(reply["message"]):"Pairing failed");connected=true;Status?.Invoke("Phone paired");Task.Run(()=>ReadLoop(reader));}
        void ReadLoop(StreamReader reader){try{string line;while(connected&&(line=reader.ReadLine())!=null){var value=json.DeserializeObject(line) as System.Collections.Generic.Dictionary<string,object>;if(value!=null&&value.ContainsKey("type")&&Convert.ToString(value["type"])=="error")Status?.Invoke("Phone error: "+(value.ContainsKey("message")?Convert.ToString(value["message"]):"Unknown"));}}catch(Exception e){if(connected)Status?.Invoke(e.Message);}finally{connected=false;}}
        public void Tap(double x,double y){Write(new{type="phone_tap",x=x,y=y});}
        public void Swipe(double x1,double y1,double x2,double y2,long duration){Write(new{type="phone_swipe",x1=x1,y1=y1,x2=x2,y2=y2,duration=duration});}
        public void Global(string action){Write(new{type="phone_global",action=action});}
        public void Text(string text){Write(new{type="phone_text",text=text});}
        void Write(object value){lock(writeLock){if(writer==null)throw new InvalidOperationException("Connect to the phone first");writer.WriteLine(json.Serialize(value));}}
        public static string Discover(){using(var udp=new UdpClient()){udp.EnableBroadcast=true;udp.Client.ReceiveTimeout=3500;byte[] q=Encoding.UTF8.GetBytes("DISCOVER_SMARTHUB_RECEIVER");udp.Send(q,q.Length,new IPEndPoint(IPAddress.Broadcast,18792));var remote=new IPEndPoint(IPAddress.Any,0);byte[] b=udp.Receive(ref remote);string answer=Encoding.UTF8.GetString(b);if(!answer.StartsWith("SMARTHUB_RECEIVER|"))throw new IOException("Unknown receiver response");return remote.Address.ToString();}}
        void DisposeSocket(){connected=false;try{if(tcp!=null)tcp.Close();}catch{}tcp=null;writer=null;}
        public void Dispose(){DisposeSocket();}
    }
}
