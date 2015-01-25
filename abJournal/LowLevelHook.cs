using System;
using System.Runtime.InteropServices;
using System.Windows.Input;

/* var llkh = new LowLevelKeyboardHoook();
でフック開始．OnKeyDown/Upをオーバーラードするか，
イベントKeyDown/Upで割り込める．
e.Handled = trueとすると，他のキーは無効化される．
最後はllkh.Dispose();でフックおしまい．
*/
namespace ablib {
    public class LowLevelKeyboardHook : IDisposable{
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
        struct KeyboardHookStruct {
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
            }
        }
        ~LowLevelKeyboardHook() { Dispose(); }
        public void Dispose(){
            if(hookID != 0) UnhookWindowsHookEx(hookID);
            hookID = 0;
        }

        int LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam) {
            KeyboardHookStruct kbd = (KeyboardHookStruct) Marshal.PtrToStructure(lParam, typeof(KeyboardHookStruct));
            var e = new LowLevelKeyEventArgs(KeyInterop.KeyFromVirtualKey(kbd.vkCode));
            switch(wParam.ToInt32()) {
            case WM_KEYDOWN: OnKeyDown(this, e); break;
            case WM_KEYUP: OnKeyUp(this, e); break;
            default: break;
            }
            if(e.Handled) return 1;
            else return CallNextHookEx(hookID, nCode, wParam, lParam);
        }
        protected virtual void OnKeyDown(object sender, LowLevelKeyEventArgs e){
            KeyDown(sender, e);
        }
        protected virtual void OnKeyUp(object sender, LowLevelKeyEventArgs e) {
            KeyUp(sender, e);
        }
        public class LowLevelKeyEventArgs : EventArgs{
            public LowLevelKeyEventArgs(Key k) {
                Key = k;
                Handled = false;
            }
            public Key Key { get; set; }
            public bool Handled { get; set; }
        }
        public delegate void LowLevelKeyEventHandler(object sender, LowLevelKeyEventArgs e);
        public event LowLevelKeyEventHandler KeyDown = (s, e) => { };
        public event LowLevelKeyEventHandler KeyUp = (s, e) => { };
    }
}
