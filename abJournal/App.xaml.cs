using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace abJournal {
    /// <summary>
    /// App.xaml の相互作用ロジック
    /// </summary>
    public partial class App : Application {
        [System.STAThreadAttribute()]
        [System.Diagnostics.DebuggerNonUserCodeAttribute()]
        public static void Main() {
            using(new BackgroundXPS.Finalizer()) {
                abJournal.App app = new abJournal.App();
                app.InitializeComponent();
                app.Run();
            }
        }
    }
}
