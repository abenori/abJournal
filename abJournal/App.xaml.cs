﻿using System;
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
            try {
                using(new BackgroundXPS.Finalizer()) {
                    abJournal.App app = new abJournal.App();
                    app.InitializeComponent();
                    app.Run();
                }
            }
            catch(Exception e) {
#if DEBUG
                string me = System.IO.Path.GetFullPath(System.Reflection.Assembly.GetExecutingAssembly().Location).ToLower();
                string outlog = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(me),System.IO.Path.GetFileNameWithoutExtension(me) + "_log.txt");
                using(var fs = new System.IO.StreamWriter(outlog,true)) {
                    fs.WriteLine("時刻：" + DateTime.Now.ToString());
                    fs.WriteLine("Message: " + e.Message);
                    fs.WriteLine(e.StackTrace);
                    fs.WriteLine("");
                }
                MessageBox.Show("例外発生，" + outlog + "にログを出力しました．");
#endif
                throw;
            }
        }
    }
}
