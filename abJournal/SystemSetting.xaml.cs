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

        KeyValuePair<string, ablib.DrawingAlgorithm>[] ComboList = new KeyValuePair<string, ablib.DrawingAlgorithm>[]{
                new KeyValuePair<string,ablib.DrawingAlgorithm>("Stroke.GetGeometry",ablib.DrawingAlgorithm.dotNet),
                new KeyValuePair<string,ablib.DrawingAlgorithm>("独自型その1",ablib.DrawingAlgorithm.Type1),
                new KeyValuePair<string,ablib.DrawingAlgorithm>("独自型その1 + 点補正",ablib.DrawingAlgorithm.Type1WithHosei)
        };
        public SystemSetting() {
            InitializeComponent();

            DrawingAlgorithmComboBox.ItemsSource = ComboList;
            DrawingAlgorithmComboBox.SelectedValuePath = "Value";
            DrawingAlgorithmComboBox.DisplayMemberPath = "Key";

            PrintDrawingAlgorithmComboBox.ItemsSource = ComboList;
            PrintDrawingAlgorithmComboBox.SelectedValuePath = "Value";
            PrintDrawingAlgorithmComboBox.DisplayMemberPath = "Key";

            DataContext = Properties.Settings.Default;
        }

        private void OK_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.Save();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.Reload();
            DialogResult = false;
        }
    }
}
