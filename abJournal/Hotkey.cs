using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace abJournal {
    // ホットキーでWindowsキーを無効化しようと思って加えたコード
    // がダメで低レベルフックに切り替えたので使われていません．
    // 
    // var hot = new Hotkey(Window);
    // hot.Add(mod,key,event)
    // とするとmod + keyを押されるとeventが発動．
    // やめるときはClearかDispose
    // Window.Closedで自動でClearが呼ばれる．
    public class Hotkey : IDisposable{
        const int WM_HOTKEY = 0x0312;
        [DllImport("user32.dll")]
        static extern int RegisterHotKey(IntPtr hWnd, int id, int MOD_KEY, int VK);
        [DllImport("user32.dll")]
        static extern int UnregisterHotKey(IntPtr hWnd, int id);

        Dictionary<int, Tuple<ModifierKeys,Key,EventHandler>> hotkeyEvents;
        List<Tuple<ModifierKeys, Key, EventHandler>> RegisterWaitingList = new List<Tuple<ModifierKeys, Key, EventHandler>>();
        System.Windows.Window Window;
        IntPtr hwnd = IntPtr.Zero;

        public Hotkey(System.Windows.Window w) {
            Window = w;
            Window.Closed += Window_Closed;
            if(Window.IsLoaded)SetWndProc();
            else Window.Loaded += Window_Loaded;
            hotkeyEvents = new Dictionary<int,Tuple<ModifierKeys,Key,EventHandler>>();
        }
        ~Hotkey() {
            Clear();
        }
        void Window_Closed(object sender, EventArgs e) {
            Clear();
        }

        void Window_Loaded(object sender, RoutedEventArgs e) {
            SetWndProc();
            foreach(var h in RegisterWaitingList) {
                Add(h.Item1, h.Item2, h.Item3);
            }
            RegisterWaitingList.Clear();
        }
        void SetWndProc() {
            WindowInteropHelper helper = new WindowInteropHelper(Window);
            HwndSource source = HwndSource.FromHwnd(helper.Handle);
            source.AddHook(new HwndSourceHook(WndProc));
            hwnd = new WindowInteropHelper(Window).Handle;
        }
        IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled) {
            if(msg == WM_HOTKEY) {
                try {
                    hotkeyEvents[wParam.ToInt32()].Item3(this, new EventArgs());
                    handled = true;
                }
                catch(KeyNotFoundException) { return IntPtr.Zero; }
                return new IntPtr(1);
            } else return IntPtr.Zero;
        }
        int id = 0x0000;
        public void Add(ModifierKeys mod, Key key, EventHandler ev) {
            int m = (int) mod;
            int vk = KeyInterop.VirtualKeyFromKey(key);
            if(hwnd == IntPtr.Zero) {
                RegisterWaitingList.Add(new Tuple<ModifierKeys, Key, EventHandler>(mod, key, ev));
            } else {
                for( ; id < 0xc000 ; ++id) {
                    if(RegisterHotKey(hwnd, id, m, vk) != 0)break;
                }
                if(id < 0xc000){
					hotkeyEvents.Add(id, new Tuple<ModifierKeys, Key, EventHandler>(mod, key, ev));
	                ++id;
	            }
            }
        }
        public void Clear() {
            foreach(var h in hotkeyEvents) UnregisterHotKey(hwnd, h.Key);
        }
        public void Dispose() {
            Clear();
        }
    }

}
