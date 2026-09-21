using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SmartHubRemote {
    internal static class FastFileClient {
        public static Task Send(string path,string host,string pin,Action<int> progress){return Task.Run(()=>SendNow(path,host,pin,progress));}
        static void SendNow(string path,string host,string pin,Action<int> progress){
            var info=new FileInfo(path);if(!info.Exists)throw new FileNotFoundException("File not found",path);
            using(var tcp=new TcpClient()){var connect=tcp.ConnectAsync(host,18793);if(!connect.Wait(7000))throw new IOException("Phone did not answer on fast-file port 18793");tcp.SendTimeout=30000;tcp.ReceiveTimeout=30000;
                using(var network=tcp.GetStream())using(var input=new BinaryReader(network,Encoding.UTF8,true))using(var output=new BinaryWriter(network,Encoding.UTF8,true))using(var file=File.OpenRead(path)){
                    WriteJavaUtf(output,"SMARTHUB_FILE_V1");WriteJavaUtf(output,pin);WriteJavaUtf(output,info.Name);WriteInt64Big(output,info.Length);output.Flush();
                    if(input.ReadByte()==0)throw new IOException(ReadJavaUtf(input));long offset=ReadInt64Big(input);if(offset<0||offset>info.Length)throw new IOException("Receiver returned an invalid resume offset");file.Position=offset;
                    byte[] buffer=new byte[128*1024];long sent=offset;int last=-1,n;while((n=file.Read(buffer,0,buffer.Length))>0){output.Write(buffer,0,n);sent+=n;int percent=info.Length==0?100:(int)(sent*100/info.Length);if(percent!=last){last=percent;progress?.Invoke(percent);}}output.Flush();
                    if(ReadJavaUtf(input)!="SAVED")throw new IOException("Phone did not confirm the saved file");
                }
            }
        }
        static string ReadJavaUtf(BinaryReader r){int n=(r.ReadByte()<<8)|r.ReadByte();byte[] b=r.ReadBytes(n);if(b.Length!=n)throw new EndOfStreamException();return Encoding.UTF8.GetString(b);}
        static void WriteJavaUtf(BinaryWriter w,string value){byte[] b=Encoding.UTF8.GetBytes(value??"");if(b.Length>65535)throw new IOException("File name is too long");w.Write((byte)(b.Length>>8));w.Write((byte)b.Length);w.Write(b);}
        static long ReadInt64Big(BinaryReader r){byte[] b=r.ReadBytes(8);if(b.Length!=8)throw new EndOfStreamException();if(BitConverter.IsLittleEndian)Array.Reverse(b);return BitConverter.ToInt64(b,0);}
        static void WriteInt64Big(BinaryWriter w,long value){byte[] b=BitConverter.GetBytes(value);if(BitConverter.IsLittleEndian)Array.Reverse(b);w.Write(b);}
    }
}
