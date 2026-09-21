using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SmartHubRemote {
    internal sealed class FastFileServer:IDisposable {
        readonly Func<string> currentPin;readonly Action<string> log;TcpListener listener;CancellationTokenSource cancel;
        public FastFileServer(Func<string> pin,Action<string> logger){currentPin=pin;log=logger;}
        public void Start(){cancel=new CancellationTokenSource();listener=new TcpListener(IPAddress.Any,18793);listener.Start();Task.Run(()=>Loop(cancel.Token));}
        async Task Loop(CancellationToken token){while(!token.IsCancellationRequested){try{var c=await listener.AcceptTcpClientAsync();Task.Run(()=>Receive(c));}catch{if(!token.IsCancellationRequested)log("Fast file receiver stopped unexpectedly");}}}
        void Receive(TcpClient tcp){try{tcp.NoDelay=true;tcp.ReceiveBufferSize=1024*1024;tcp.SendBufferSize=256*1024;tcp.ReceiveTimeout=120000;using(tcp)using(var s=tcp.GetStream())using(var input=new BinaryReader(new BufferedStream(s,1024*1024),Encoding.UTF8,true))using(var output=new BinaryWriter(s,Encoding.UTF8,true)){if(ReadJavaUtf(input)!="SMARTHUB_FILE_V1")throw new IOException("Invalid file protocol");string pin=ReadJavaUtf(input);if(pin!=currentPin()){output.Write((byte)0);WriteJavaUtf(output,"Invalid receiver PIN");output.Flush();return;}string name=Safe(ReadJavaUtf(input));long size=ReadInt64Big(input);if(size<0||size>50L*1024*1024*1024)throw new IOException("Invalid file size");string dir=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads","SmartHub Remote");Directory.CreateDirectory(dir);string final=Unique(Path.Combine(dir,name)),part=final+"."+size+".part";long offset=File.Exists(part)?Math.Min(new FileInfo(part).Length,size):0;output.Write((byte)1);WriteInt64Big(output,offset);output.Flush();using(var file=new FileStream(part,FileMode.OpenOrCreate,FileAccess.Write,FileShare.None,1024*1024,FileOptions.SequentialScan)){file.Position=offset;byte[] b=new byte[1024*1024];long total=offset;while(total<size){int n=input.Read(b,0,(int)Math.Min(b.Length,size-total));if(n<=0)throw new EndOfStreamException("Transfer interrupted");file.Write(b,0,n);total+=n;}}File.Move(part,final);WriteJavaUtf(output,"SAVED");output.Flush();log("Fast file saved: "+final);}}catch(Exception e){log("Fast file: "+e.Message);}}
        static string ReadJavaUtf(BinaryReader r){int n=(r.ReadByte()<<8)|r.ReadByte();byte[] b=r.ReadBytes(n);if(b.Length!=n)throw new EndOfStreamException();return Encoding.UTF8.GetString(b);}
        static void WriteJavaUtf(BinaryWriter w,string value){byte[] b=Encoding.UTF8.GetBytes(value??"");if(b.Length>65535)throw new IOException("Message too long");w.Write((byte)(b.Length>>8));w.Write((byte)b.Length);w.Write(b);}
        static long ReadInt64Big(BinaryReader r){byte[] b=r.ReadBytes(8);if(b.Length!=8)throw new EndOfStreamException();if(BitConverter.IsLittleEndian)Array.Reverse(b);return BitConverter.ToInt64(b,0);}
        static void WriteInt64Big(BinaryWriter w,long value){byte[] b=BitConverter.GetBytes(value);if(BitConverter.IsLittleEndian)Array.Reverse(b);w.Write(b);}
        static string Safe(string name){var invalid=Path.GetInvalidFileNameChars();string n=new string((name??"shared_file").Select(c=>invalid.Contains(c)?'_':c).ToArray());return n.Length>120?n.Substring(n.Length-120):n;}
        static string Unique(string p){if(!File.Exists(p)&&!File.Exists(p+".part"))return p;string d=Path.GetDirectoryName(p),n=Path.GetFileNameWithoutExtension(p),e=Path.GetExtension(p);for(int i=1;;i++){string x=Path.Combine(d,n+" ("+i+")"+e);if(!File.Exists(x)&&!File.Exists(x+".part"))return x;}}
        public void Dispose(){if(cancel!=null)cancel.Cancel();try{listener?.Stop();}catch{}listener=null;}
    }
}
