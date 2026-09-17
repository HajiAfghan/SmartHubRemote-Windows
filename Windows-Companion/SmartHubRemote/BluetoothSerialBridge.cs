using System;
using System.IO.Ports;
using System.Text;
namespace SmartHubRemote {
    internal sealed class BluetoothSerialBridge:IDisposable {
        SerialPort port; readonly RemoteServer server; readonly SessionState state=new SessionState(); public event Action<string> Log;
        public BluetoothSerialBridge(RemoteServer server){this.server=server;}
        public static string[] Ports(){return SerialPort.GetPortNames();}
        public void Start(string name){Dispose();port=new SerialPort(name,115200,Parity.None,8,StopBits.One){Encoding=Encoding.UTF8,NewLine="\n",ReadTimeout=1000};port.DataReceived+=(s,e)=>{try{while(port!=null&&port.IsOpen&&port.BytesToRead>0){string line=port.ReadLine();server.ProcessLine(line,x=>{if(port!=null&&port.IsOpen)port.WriteLine(x);},state);}}catch(Exception ex){Log?.Invoke(ex.Message);}};port.Open();Log?.Invoke("Bluetooth serial listening on "+name);}
        public void Dispose(){try{if(port!=null){port.Close();port.Dispose();}}catch{}port=null;}
    }
}
