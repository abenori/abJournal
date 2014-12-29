using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace abJournal {
    /// <summary>
    /// AboutDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class AboutDialog : Window {
        public AboutDialog() {
            InitializeComponent();
            DataContext = this;
            string me = System.Reflection.Assembly.GetExecutingAssembly().Location;
            fileVersion = System.Diagnostics.FileVersionInfo.GetVersionInfo(me);
            fileInfo = new System.IO.FileInfo(me);
        }
        public System.Diagnostics.FileVersionInfo fileVersion { get; set; }
        public System.IO.FileInfo fileInfo { get; set; }
        public string createTime { get { return fileInfo.LastWriteTime.ToString(); } }


        private void OK_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }
    }
}
