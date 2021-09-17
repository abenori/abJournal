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
    /// SystemSetting.xaml の相互作用ロジック
    /// </summary>
    public partial class SystemSetting : Window {
        public SystemSetting() {
            InitializeComponent();
            DataContext = Properties.Settings.Default;
        }

        private void OK_Click(object sender, RoutedEventArgs e) {
            int time;
            if (int.TryParse(AfterEraseWaitTime.Text, out time)) {
                Properties.Settings.Default.WaitAfterErase = time;
            } else {
                MessageBox.Show("不正な値：" + AfterEraseWaitTime.Text);
                e.Handled = true;
                return;
            }
            Properties.Settings.Default.Save();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.Reload();
            DialogResult = false;
        }
    }
}
