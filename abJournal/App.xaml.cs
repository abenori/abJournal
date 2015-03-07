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
            using(var PDFPageFinalizer = new BackgroundImageManager.PDFPage.Finalizer())
            using(var PDFBackgroundFinalizer = new BackgroundImageManager.PDFBackground.Finalizer())
            using(var XPSPageFinalizer = new BackgroundImageManager.XPSPage.Finalizer())
            using(var XPSBackgroundFinalizer = new BackgroundImageManager.XPSBackground.Finalizer())
            {
                abJournal.App app = new abJournal.App();
                app.InitializeComponent();
                app.Run();
            }
        }
    }
}
