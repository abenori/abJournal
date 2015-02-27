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

        KeyValuePair<string, DrawingAlgorithm>[] ComboList = new KeyValuePair<string, DrawingAlgorithm>[]{
                new KeyValuePair<string,DrawingAlgorithm>("Stroke.GetGeometry",DrawingAlgorithm.dotNet),
                new KeyValuePair<string,DrawingAlgorithm>("独自型その1",DrawingAlgorithm.Type1),
                new KeyValuePair<string,DrawingAlgorithm>("独自型その1 + 点補正",DrawingAlgorithm.Type1WithHosei),
                new KeyValuePair<string,DrawingAlgorithm>("直線で結ぶだけ",DrawingAlgorithm.Line),
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
