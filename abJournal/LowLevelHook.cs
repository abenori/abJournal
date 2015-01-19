using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows;

namespace abJournal {
    class LowLevelKeyboardHook : IDisposable{
        delegate int HookProc(int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("user32.dll")]
        static extern int SetWindowsHookEx(int idHook, HookProc lpfn, IntPtr hInstance, int threadId);
        [DllImport("user32.dll")]
        static extern bool UnhookWindowsHookEx(int idHook);
        [DllImport("user32.dll")]
        static extern int CallNextHookEx(int idHook, int nCode, IntPtr wParam, IntPtr lParam);
        [DllImport("kernel32.dll")]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [StructLayout(LayoutKind.Sequential)]
        struct KeyboardHookStruct{
          public int vkCode;
          public int scanCode;
          public int flags;
          public int time;
          public int dwExtraInfo;
        };
        const int WM_KEYDOWN = 0x0100;
        const int WM_KEYUP = 0x0101;
        const int WH_KEYBOARD_LL = 13;
		int hookID = 0;

        public bool Hooked {
            get { return hookID != 0; }
        }
        HookProc hookProc;
        public LowLevelKeyboardHook() {
            using(System.Diagnostics.Process proc = System.Diagnostics.Process.GetCurrentProcess())
            using(System.Diagnostics.ProcessModule module = proc.MainModule) {
                hookProc = new HookProc(LowLevelKeyboardProc);

                hookID = SetWindowsHookEx(
                    WH_KEYBOARD_LL,
                    hookProc,
                    GetModuleHandle(module.ModuleName),
                    0);
                System.Diagnostics.Debug.WriteLine("hookId = " + hookID.ToString());
            }
        }
        ~LowLevelKeyboardHook() { Dispose(); }
        public void Dispose(){
            UnhookWindowsHookEx(hookID);
        }

        int LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam) {
            KeyboardHookStruct kbd = (KeyboardHookStruct) Marshal.PtrToStructure(lParam, typeof(KeyboardHookStruct));
            var e = new LowLevelKeyEventArgs(KeyInterop.KeyFromVirtualKey(kbd.vkCode));
            if(wParam.ToInt32() == WM_KEYDOWN) OnKeyDown(this, e);
            else if(wParam.ToInt32() == WM_KEYUP) OnKeyUp(this, e);
            if(e.Handled) return 1;
            else return CallNextHookEx(hookID, nCode, wParam, lParam);
        }
        protected virtual void OnKeyDown(object sender, LowLevelKeyEventArgs e){
            KeyDown(sender, e);
        }
        protected virtual void OnKeyUp(object sender, LowLevelKeyEventArgs e) {
            Keyup(sender, e);
        }
        public class LowLevelKeyEventArgs : EventArgs{
            public LowLevelKeyEventArgs(Key key) {
                Key = key;
                Handled = false;
            }
            public Key Key { get; set; }
            public bool Handled { get; set; }
        }
        public delegate void LowLevelKeyEventHandler(object sender, LowLevelKeyEventArgs e);
        public event LowLevelKeyEventHandler KeyDown = (s, e) => { };
        public event LowLevelKeyEventHandler Keyup = (s, e) => { };

    }
}
