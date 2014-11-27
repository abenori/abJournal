using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Forms;
using System.ComponentModel;

namespace abJournal {
    /// <summary>
    /// PenSettingDialog.xaml の相互作用ロジック
    /// </summary>
    public partial class PenSettingDialog : Window,INotifyPropertyChanged {
        public static double[] thicks = new double[] { 1, 1.5, 2, 3, 4, 6, 8, 10 };

        // バインディング用
        public bool[] PenDashed {
            get { return Properties.Settings.Default.PenDashed; }
        }
        public double[] PenThickness {
            get { return Properties.Settings.Default.PenThickness; }
        }
        public Color[] PenColor {
            get { return Properties.Settings.Default.PenColor; }
        }

        public PenSettingDialog() {
            InitializeComponent();
            DataContext = this;
            AddThicksToComboBox(Pen0Thickness_ComboBox);
            AddThicksToComboBox(Pen1Thickness_ComboBox);
            AddThicksToComboBox(Pen2Thickness_ComboBox);
            AddThicksToComboBox(Pen3Thickness_ComboBox);
            AddThicksToComboBox(Pen4Thickness_ComboBox);
            AddThicksToComboBox(Pen5Thickness_ComboBox);
            AddThicksToComboBox(Pen6Thickness_ComboBox);
            AddThicksToComboBox(Pen7Thickness_ComboBox);
        }
        private void AddThicksToComboBox(System.Windows.Controls.ComboBox combo) {
            for(int i = 0 ; i < thicks.Count() ; ++i) combo.Items.Add(thicks[i]);
        }

        private void ColorSetting(int i) {
            ColorDialog diag = new ColorDialog();
            Color pen = Properties.Settings.Default.PenColor[i];
            diag.Color = System.Drawing.Color.FromArgb(pen.A, pen.R, pen.G, pen.B);
            if(diag.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                Properties.Settings.Default.PenColor[i] = Color.FromArgb(diag.Color.A, diag.Color.R, diag.Color.G, diag.Color.B);
                OnPropertyChanged("PenColor");
            }
        }

        private void OK_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.Save();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) {
            Properties.Settings.Default.Reload();
            DialogResult = false;            
        }

        private void Pen0Color_Click(object sender, RoutedEventArgs e) {
            ColorSetting(0);
        }
        private void Pen1Color_Click(object sender, RoutedEventArgs e) {
            ColorSetting(1);
        }
        private void Pen2Color_Click(object sender, RoutedEventArgs e) {
            ColorSetting(2);
        }
        private void Pen3Color_Click(object sender, RoutedEventArgs e) {
            ColorSetting(3);
        }
        private void Pen4Color_Click(object sender, RoutedEventArgs e) {
            ColorSetting(4);
        }
        private void Pen5Color_Click(object sender, RoutedEventArgs e) {
            ColorSetting(5);
        }
        private void Pen6Color_Click(object sender, RoutedEventArgs e) {
            ColorSetting(6);
        }
        private void Pen7Color_Click(object sender, RoutedEventArgs e) {
            ColorSetting(7);
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) {
            if(PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
    }

    public class ThicknessComboBoxSelConvereter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            double t = (double) value;
            int index = PenSettingDialog.thicks.Count() - 1;
            for(int j = 0 ; j < PenSettingDialog.thicks.Count() - 1 ; ++j) {
                if(t < (PenSettingDialog.thicks[j] + PenSettingDialog.thicks[j + 1]) / 2) {
                    index = j;
                    break;
                }
            }
            return index;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return PenSettingDialog.thicks[(int) value];
        }
    }
}
