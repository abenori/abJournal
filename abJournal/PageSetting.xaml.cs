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
        public ablib.InkCanvasCollection.CanvasCollectionInfo info;
        public ablib.InkCanvasCollection.CanvasCollectionInfo Info {
            get { return info; }
            set { info = value; OnPropertyChanged("Info"); }
        }

        public PageSetting(ablib.InkCanvasCollection.CanvasCollectionInfo i) {
            Info = i.DeepCopy();
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
            Color col = Info.InkCanvasInfo.BackGround;
            diag.Color = System.Drawing.Color.FromArgb(col.A, col.R, col.G, col.B);
            if(diag.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
                Info.InkCanvasInfo.BackGround = Color.FromArgb(diag.Color.A, diag.Color.R, diag.Color.G, diag.Color.B);
                OnPropertyChanged("Info");
            }
        }
 
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) {
            if(PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }
    }
}
