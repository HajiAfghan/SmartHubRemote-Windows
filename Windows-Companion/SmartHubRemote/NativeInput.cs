using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
namespace SmartHubRemote {
    internal static class NativeInput {
        [DllImport("user32.dll", SetLastError=true)] static extern bool SetCursorPos(int x,int y);
        [DllImport("user32.dll", SetLastError=true)] static extern bool GetCursorPos(out POINT point);
        [DllImport("user32.dll", CharSet=CharSet.Unicode, SetLastError=true)] static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")] public static extern bool LockWorkStation();
        [DllImport("powrprof.dll", SetLastError=true)] public static extern bool SetSuspendState(bool hibernate,bool force,bool disableWake);
        const uint LEFTDOWN=0x0002,LEFTUP=0x0004,RIGHTDOWN=0x0008,RIGHTUP=0x0010,MIDDLEDOWN=0x0020,MIDDLEUP=0x0040,WHEEL=0x0800;
        const uint INPUT_MOUSE=0,INPUT_KEYBOARD=1,KEYUP=0x0002,UNICODE=0x0004;
        [StructLayout(LayoutKind.Sequential)] struct POINT { public int X,Y; }
        [StructLayout(LayoutKind.Sequential)] struct INPUT { public uint type; public InputUnion U; }
        // INPUT is a native union. Including all members is required so Marshal.SizeOf
        // is 28 bytes on x86 and 40 bytes on x64; the old keyboard-only union was too
        // small on 64-bit Windows and made SendInput silently fail.
        [StructLayout(LayoutKind.Explicit)] struct InputUnion { [FieldOffset(0)] public MOUSEINPUT mi; [FieldOffset(0)] public KEYBDINPUT ki; [FieldOffset(0)] public HARDWAREINPUT hi; }
        [StructLayout(LayoutKind.Sequential)] struct MOUSEINPUT { public int dx,dy; public uint mouseData,flags,time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] struct KEYBDINPUT { public ushort vk,scan; public uint flags,time; public UIntPtr extra; }
        [StructLayout(LayoutKind.Sequential)] struct HARDWAREINPUT { public uint message; public ushort paramL,paramH; }
        public static void SetPosition(int x,int y){if(!SetCursorPos(x,y))throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows refused cursor movement");}
        public static void Move(double dx,double dy){POINT p;if(!GetCursorPos(out p))throw new Win32Exception(Marshal.GetLastWin32Error(),"Could not read cursor position");SetPosition(p.X+(int)Math.Round(dx),p.Y+(int)Math.Round(dy));}
        static INPUT Mouse(uint flags,uint data=0){var i=new INPUT();i.type=INPUT_MOUSE;i.U.mi.flags=flags;i.U.mi.mouseData=data;return i;}
        static INPUT KeyInput(ushort vk,ushort scan,uint flags){var i=new INPUT();i.type=INPUT_KEYBOARD;i.U.ki.vk=vk;i.U.ki.scan=scan;i.U.ki.flags=flags;return i;}
        static void Inject(params INPUT[] input){uint sent=SendInput((uint)input.Length,input,Marshal.SizeOf(typeof(INPUT)));if(sent!=(uint)input.Length)throw new Win32Exception(Marshal.GetLastWin32Error(),"Windows blocked remote input");}
        public static void Click(string b){uint d=LEFTDOWN,u=LEFTUP;if(b=="right"){d=RIGHTDOWN;u=RIGHTUP;}else if(b=="middle"){d=MIDDLEDOWN;u=MIDDLEUP;}Inject(Mouse(d),Mouse(u));}
        public static void Button(string action){uint f=action=="left_down"?LEFTDOWN:LEFTUP;Inject(Mouse(f));}
        public static void Scroll(int delta){Inject(Mouse(WHEEL,unchecked((uint)delta)));}
        public static void Key(int vk){Inject(KeyInput((ushort)vk,0,0),KeyInput((ushort)vk,0,KEYUP));}
        public static void Text(string text){foreach(char ch in text)Inject(KeyInput(0,ch,UNICODE),KeyInput(0,ch,UNICODE|KEYUP));}
    }
}
