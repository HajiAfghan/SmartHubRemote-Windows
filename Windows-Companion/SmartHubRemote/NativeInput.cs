using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace SmartHubRemote {
    internal static class NativeInput {
        [DllImport("user32.dll")] static extern void mouse_event(uint flags,uint dx,uint dy,uint data,UIntPtr extra);
        [DllImport("user32.dll")] static extern void keybd_event(byte vk,byte scan,uint flags,UIntPtr extra);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")] public static extern bool LockWorkStation();
        [DllImport("powrprof.dll", SetLastError=true)] public static extern bool SetSuspendState(bool hibernate,bool force,bool disableWake);
        const uint LEFTDOWN=0x0002,LEFTUP=0x0004,RIGHTDOWN=0x0008,RIGHTUP=0x0010,MIDDLEDOWN=0x0020,MIDDLEUP=0x0040,WHEEL=0x0800;
        [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion U; }
        [StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public KEYBDINPUT ki; }
        [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort vk,scan; public uint flags,time; public UIntPtr extra; }
        public static void Move(double dx,double dy){var p=Cursor.Position;Cursor.Position=new System.Drawing.Point(p.X+(int)dx,p.Y+(int)dy);}
        public static void Click(string b){uint d=LEFTDOWN,u=LEFTUP;if(b=="right"){d=RIGHTDOWN;u=RIGHTUP;}else if(b=="middle"){d=MIDDLEDOWN;u=MIDDLEUP;}mouse_event(d,0,0,0,UIntPtr.Zero);mouse_event(u,0,0,0,UIntPtr.Zero);}
        public static void Button(string action){uint f=action=="left_down"?LEFTDOWN:LEFTUP;mouse_event(f,0,0,0,UIntPtr.Zero);}
        public static void Scroll(int delta){mouse_event(WHEEL,0,0,unchecked((uint)delta),UIntPtr.Zero);}
        public static void Key(int vk){keybd_event((byte)vk,0,0,UIntPtr.Zero);keybd_event((byte)vk,0,2,UIntPtr.Zero);}
        public static void Text(string text){foreach(char ch in text){var a=new INPUT[2];a[0].type=a[1].type=1;a[0].U.ki.scan=a[1].U.ki.scan=ch;a[0].U.ki.flags=4;a[1].U.ki.flags=6;SendInput(2,a,Marshal.SizeOf(typeof(INPUT)));}}
    }
}

