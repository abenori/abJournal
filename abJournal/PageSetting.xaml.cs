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
using System.ComponentModel;
using System.Windows.Forms;

namespace abJournal {
    /// <summary>
    /// PageSetting.xaml の相互作用ロジック
    /// </summary>
    public partial class PageSetting : Window, INotifyPropertyChanged {
        public abJournalInkCanvasCollection.CanvasCollectionInfo info;
        public abJournalInkCanvasCollection.CanvasCollectionInfo Info {
            get { return info; }
            set { info = value; OnPropertyChanged("Info"); }
        }
        public double PaperWidth { get; set; }
        public double PaperHeight { get; set; }
        Size WindowSize;

        public PageSetting(abJournalInkCanvasCollection.CanvasCollectionInfo i, Size windowsize) {
            WindowSize = windowsize;
            Info = i.DeepCopy();
            PaperWidth = Info.InkCanvasInfo.Size.Width;
            PaperHeight = Info.InkCanvasInfo.Size.Height;
            DataContext = this;
            InitializeComponent();
        }

        private void OK_Click(object sender, RoutedEventArgs e) {
            DialogResult = true;
        }
        private void Cancel_Click(object sender, RoutedEventArgs e) {
            DialogResult = false;
        }
        private void BackGroundButton_Click(object sender, RoutedEventArgs e) {
            ColorDialog diag = new ColorDialog();
            Color col = Info.InkCanvasInfo.BackgroundColor;
            diag.Color = System.Drawing.Color.FromArgb(col.A, col.R, col.G, col.B);
            if (diag.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                Info.InkCanvasInfo.BackgroundColor = Color.FromArgb(diag.Color.A, diag.Color.R, diag.Color.G, diag.Color.B);
                OnPropertyChanged("Info");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) {
            if (PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        private void HorizontalColorButton_Click(object sender, RoutedEventArgs e) {
            ColorDialog diag = new ColorDialog();
            Color col = Info.InkCanvasInfo.HorizontalRule.Color;
            diag.Color = System.Drawing.Color.FromArgb(col.A, col.R, col.G, col.B);
            if (diag.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                Info.InkCanvasInfo.HorizontalRule.Color = Color.FromArgb(diag.Color.A, diag.Color.R, diag.Color.G, diag.Color.B);
                OnPropertyChanged("Info");
            }
        }

        private void VerticalColorButton_Click(object sender, RoutedEventArgs e) {
            ColorDialog diag = new ColorDialog();
            Color col = Info.InkCanvasInfo.VerticalRule.Color;
            diag.Color = System.Drawing.Color.FromArgb(col.A, col.R, col.G, col.B);
            if (diag.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                Info.InkCanvasInfo.VerticalRule.Color = Color.FromArgb(diag.Color.A, diag.Color.R, diag.Color.G, diag.Color.B);
                OnPropertyChanged("Info");
            }
        }

        private void FixWindowRatioButton_Click(object sender, RoutedEventArgs e) {
            PaperHeight = Math.Round(PaperWidth * WindowSize.Height / WindowSize.Width, 2);
            OnPropertyChanged("PaperHeight");
        }

        private void TextBox_PreviewTextInput_CheckDouble(object sender, TextCompositionEventArgs e) {
            double r;
            var textbox = (System.Windows.Controls.TextBox)sender;
            var text = textbox.Text.Insert(textbox.CaretIndex, e.Text);
            if (!Double.TryParse(text, out r)) e.Handled = true;
        }

        private void TextBox_PreviewExecuted_CheckDouble(object sender, ExecutedRoutedEventArgs e) {
            if (e.Command == ApplicationCommands.Paste) {
                double r;
                var text = System.Windows.Clipboard.GetText();
                if (!Double.TryParse(text, out r)) e.Handled = true;
            }
        }
        private void TextBox_PreviewTextInput_CheckDoubleArray(object sender, TextCompositionEventArgs e) {
            double r;
            var textbox = (System.Windows.Controls.TextBox)sender;
            var text = textbox.Text.Insert(textbox.CaretIndex, e.Text);
            if (text.Split(new char[] { ',' }).Any(s => !double.TryParse(s, out r))) e.Handled = true;
        }

        private void TextBox_PreviewExecuted_CheckDoubleArray(object sender, ExecutedRoutedEventArgs e) {
            if (e.Command == ApplicationCommands.Paste) {
                double r;
                var text = System.Windows.Clipboard.GetText();
                if (text.Split(new char[] { ',' }).Any(s => !double.TryParse(s, out r))) e.Handled = true;
                else ((System.Windows.Controls.TextBox)sender).Paste();
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e) {
            Info.InkCanvasInfo.Size = new Size(PaperWidth, PaperHeight);
       }
    }
    class abJournalPointTommConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            return ((double)value) / Paper.mmToSize;
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            try { return (Double.Parse((string)value)) * Paper.mmToSize; }
            catch (Exception) { return 0; }
        }
    }

    class DashArrayConverter : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            var v = ((List<double>)value).Select(d => d / Paper.mmToSize);
            return string.Join(",", v);
        }
        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            double d;
            return new List<double>(((string)value).Split(new char[] { ',' }).Where(s => double.TryParse(s, out d)).Select(s => double.Parse(s) * Paper.mmToSize));
        }
    }
}
