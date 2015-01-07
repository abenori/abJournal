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
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using Microsoft.Win32;
using System.ComponentModel;
using ablib;

/* 
 * TODO（やりたい）：
 * テキストボックスのサポート（なくてもいい気がしてきた）
 */ 

namespace abJournal {
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) {
            if(PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
                if(name == "MainCanvas") {
                    PropertyChanged(this, new PropertyChangedEventArgs("WindowTitle"));
                }
            }
        }

        bool FitScaleToWidth = true;
        public int ScaleComboBoxIndex {
            get {
                if(FitScaleToWidth) return 0;
                if(MainCanvas.Scale == 0.5) return 1;
                else if(MainCanvas.Scale == 0.7) return 2;
                else if(MainCanvas.Scale == 1) return 3;
                else if(MainCanvas.Scale == 1.5) return 4;
                else if(MainCanvas.Scale == 2) return 5;
                else return 0;
            }
            set {
                FitScaleToWidth = false;
                var scales = new double[] { 0.5, 0.7, 1, 1.1, 1.25, 1.5, 2 };
                if(value >= 1 && value <= scales.Length) {
                    MainCanvas.Scale = scales[value - 1];
                } else {
                    FitScaleToWidth = true;
                    if(MainCanvas.Count == 0) return;
                    MainCanvas.Scale = MainPanel.ActualWidth / MainCanvas[0].Width;
                }
                OnPropertyChanged("MainCanvas");
            }
        }

        int currentPen = 0;
        public int CurrentPen {
            get { return currentPen; }
            set {
                currentPen = value;
                MainCanvas.PenColor = Properties.Settings.Default.PenColor[currentPen];
                MainCanvas.PenThickness = Properties.Settings.Default.PenThickness[currentPen];
                MainCanvas.PenDashed = Properties.Settings.Default.PenDashed[currentPen];
                OnPropertyChanged("MainCanvas");
            }
        }

        string windowTitle = null;
        public string WindowTitle {
            set {
                windowTitle = value;
                OnPropertyChanged("WindowTitle");
            }
            get {
                if(windowTitle != null)return windowTitle; 
                string rv;
                if(MainCanvas.FileName == "") rv = "無題ノート";
                else rv = System.IO.Path.GetFileName(MainCanvas.FileName);
                if(MainCanvas.Updated) rv += " （更新）";
                rv += "  abJournal";
                return rv;
            }
        }

        public InkCanvasCollection MainCanvas {
            get { return mainCanvas; }
        }

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
        public System.Collections.Specialized.StringCollection History {
            get { return Properties.Settings.Default.History; }
        }

        public MainWindow() {
            InitializeComponent();

            DataContext = this;
            MainCanvas.ManipulationDelta += ((s, e) => { OnPropertyChanged("MainCanvas"); });
            MainCanvas.UndoChainChanged += ((s, e) => { OnPropertyChanged("MainCanvas"); });
            MainCanvas.MouseDown += ((s, e) => { MainCanvas.Focus(); });
            MainCanvas.StylusDown += ((s, e) => { MainCanvas.Focus(); });

            Panel.SetZIndex(MainCanvas, -4);

            ScaleComboBoxIndex = 0;// デフォルトは横幅に合わせる．
            CurrentPen = 0;
            MainCanvas.DrawingAlgorithm = Properties.Settings.Default.DrawingAlgorithm;

            var opt = new NDesk.Options.OptionSet() { };
            List<string> files = opt.Parse(Environment.GetCommandLineArgs());
            files.RemoveAt(0);// .exe本体
            files.RemoveAll(f => {
                if(!System.IO.File.Exists(f)) {
                    MessageBox.Show("ファイル " + f + " は存在しません．");
                    return true;
                } else return false;
            });
            FileOpen(files);
        }

        ~MainWindow() {
            Properties.Settings.Default.Save();
        }

        private void Window_Closing(object sender, CancelEventArgs e) {
            if(!BeforeClose()) e.Cancel = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            if(MainCanvas.Count == 0)AddPage.Execute(null, this);
            MainCanvas.ClearUpdated();
            MainCanvas.ClearUndoChain();
            Window_SizeChanged(sender, null);
        }

        private void UndoCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.Undo();
            OnPropertyChanged("MainCanvas");
        }
        private void UndoCommandCanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = MainCanvas != null ? MainCanvas.CanUndo() : false;
        }
        private void RedoCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.Redo();
            OnPropertyChanged("MainCanvas");
        }
        private void RedoCommandCanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = MainCanvas != null ? MainCanvas.CanRedo() : false;
        }
        private void DeleteCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.Delete();
            OnPropertyChanged("MainCanvas");
        }
        private void SaveAsCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            var fd = new SaveFileDialog();
            fd.FileName = System.IO.Path.GetFileName(MainCanvas.FileName);
            fd.Filter = "abjnt ファイル (*.abjnt)|*.abjnt|PDF ファイル (*.pdf)|*.pdf|全てのファイル|*.*";
            if(fd.ShowDialog() == true) {
                try {
                    if(System.IO.Path.GetExtension(fd.FileName) == ".pdf") {
                        MainCanvas.SavePDF(fd.FileName);
                        //MainCanvas.SavePDFWithiText(fd.FileName);
                    } else {
                        MainCanvas.Save(fd.FileName);
                        MainCanvas.ClearUpdated();
                        AddHistory(fd.FileName);
                    }
                }
                catch(System.IO.IOException) {
                    MessageBox.Show("他のアプリケーションが\n" + fd.FileName + "\nを開いているようです．","abJournal");
                } 
                OnPropertyChanged("MainCanvas");
            }
        }
        private void SaveCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            if(MainCanvas.FileName == "") SaveAsCommandExecuted(sender, e);
            else {
                MainCanvas.Save();
                MainCanvas.ClearUpdated();
                AddHistory(MainCanvas.FileName);
                OnPropertyChanged("MainCanvas");
            }
        }
        private void NewCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            string me = System.Reflection.Assembly.GetExecutingAssembly().Location;
            using(var proc = new Process()) {
                proc.StartInfo.FileName = me;
                try { proc.Start(); }
                catch { MessageBox.Show("新しいノートの作成に失敗しました．", "abJournal"); }
            }
        }
        private void OpenCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            var fd = new OpenFileDialog();
            fd.Filter = "abjnt ファイル (*.abjnt)|*.abjnt|全てのファイル|*.*";
            if(fd.ShowDialog() == true) {
                if(System.IO.File.Exists(fd.FileName)) {
                    FileOpen(new List<string>() { fd.FileName });
                    OnPropertyChanged("MainCanvas");
                } else {
                    MessageBox.Show("\"" + fd.FileName + "\" は存在しません．", "abJournal");
                }
            }
        }
        private bool BeforeClose() {
            if(MainCanvas.Updated) {
                MessageBoxResult res = MessageBoxResult.No;
                if(MainCanvas.FileName != "") {
                    res = MessageBox.Show("\"" + MainCanvas.FileName + "\" への変更を保存しますか？", "abJournal", MessageBoxButton.YesNoCancel);
                    if(res == MessageBoxResult.Yes) {
                        MainCanvas.Save();
                        AddHistory(MainCanvas.FileName);
                        return true;
                    } else return (res != MessageBoxResult.Cancel);
                } else {
                    res = MessageBox.Show("ノートは更新されています．保存しますか？", "abJournal", MessageBoxButton.YesNoCancel);
                    if(res == MessageBoxResult.Yes) {
                        var fd = new SaveFileDialog();
                        fd.Filter = "abjnt ファイル (*.abjnt)|*.abjnt|全てのファイル|*.*";
                        if(fd.ShowDialog() == true) {
                            MainCanvas.Save(fd.FileName);
                            AddHistory(fd.FileName);
                            return true;
                        } else return false;
                    } else return (res != MessageBoxResult.Cancel);
                }
            } else return true;
        }

        private void CloseCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            if(BeforeClose()) Close();
        }
        private void SelectAllCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.SelectAll();
        }
        private void PasteCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.Paste();
            OnPropertyChanged("MainCanvas");
        }
        private void CopyCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.Copy();
            OnPropertyChanged("MainCanvas");
        }
        private void CutCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.Cut();
            OnPropertyChanged("MainCanvas");
        }
        private void PrintCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            //MainCanvas[0].InkData.GeneratePDF(@"C:\Users\Abe_Noriyuki\Desktop\abjnt_s.pdf");
            if(MainCanvas.Count == 0) {
                MessageBox.Show("ページがありません．","abJournal");
                return;
            }
            PrintDialog pd = new PrintDialog();
            if(pd.ShowDialog() == true) {
                FixedDocument doc = new FixedDocument();
                for(int i = 0 ; i < MainCanvas.Count ; ++i) {
                    FixedPage page = new FixedPage();
                    page.Width = MainCanvas[i].Width;
                    page.Height = MainCanvas[i].Height;
                    Canvas c = MainCanvas[i].GetCanvas(Properties.Settings.Default.PrintDrawingAlgorithm);
                    if(i == 0) {
                        InkCanvasCollection.DrawNoteContents(c,MainCanvas.Info);
                    }
                    page.Children.Add(c);
                    PageContent content = new PageContent();
                    content.Child = page;
                    doc.Pages.Add(content);
                }
                pd.PrintDocument(doc.DocumentPaginator,MainCanvas.FileName == "" ?
                    "無題ノート" : System.IO.Path.GetFileNameWithoutExtension(MainCanvas.FileName));
            }
            return;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) {
            if(FitScaleToWidth && MainCanvas.Count != 0) {
                MainCanvas.Scale = MainPanel.ActualWidth / MainCanvas[0].Width;
            }
            /*
            MainCanvas.Width = ActualWidth;
            MainCanvas.Height = ActualHeight;
             */
            MainCanvas.Scroll();
        }
        public static readonly RoutedCommand AddPage = new RoutedCommand("AddPage", typeof(MainWindow));
        private void AddPageCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.AddCanvas();
            OnPropertyChanged("MainCanvas");
        }
        public static readonly RoutedCommand InsertPage = new RoutedCommand("InsertPage", typeof(MainWindow));
        private void InsertPageCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.InsertCanvas(MainCanvas.CurrentPage + 1);
            OnPropertyChanged("MainCanvas");
        }

        WindowState SaveWindowState;
        public static readonly RoutedCommand FullScreen = new RoutedCommand("FullScreen", typeof(MainWindow));
        private void FullScreenCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            if(WindowStyle == WindowStyle.None) {
                WindowStyle = WindowStyle.SingleBorderWindow;
                ResizeMode = ResizeMode.CanResize;
                WindowState = SaveWindowState;
                FullScreenButton.IsChecked = false;
            } else {
                SaveWindowState = WindowState;
                WindowState = WindowState.Normal;
                ResizeMode = ResizeMode.NoResize;
                WindowStyle = WindowStyle.None;
                WindowState = WindowState.Maximized;
                FullScreenButton.IsChecked = true;
            }
        }
        public static readonly RoutedCommand DeletePage = new RoutedCommand("DeletePage", typeof(MainWindow));
        private void DeletePageCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.DeleteCanvas(MainCanvas.CurrentPage);
            OnPropertyChanged("MainCanvas");
        }

        public static readonly RoutedCommand SystemSetting = new RoutedCommand("SystemSetting", typeof(MainWindow));
        private void SystemSettingCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            SystemSetting dialog = new SystemSetting();
            if(dialog.ShowDialog() == true) {
                MainCanvas.DrawingAlgorithm = Properties.Settings.Default.DrawingAlgorithm;
            }
        }

        public static readonly RoutedCommand PenSetting = new RoutedCommand("PenSetting", typeof(MainWindow));
        private void PenSettingCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            PenSettingDialog dialog = new PenSettingDialog();
            if(dialog.ShowDialog() == true) {
                CurrentPen = CurrentPen;
                OnPropertyChanged("PenColor");
                OnPropertyChanged("PenThickness");
                OnPropertyChanged("PenDashed");
            }
        }
        public static readonly RoutedCommand ModeChangeToPen = new RoutedCommand("ModeChangeToPen", typeof(MainWindow));
        private void ModeChangeToPenCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.Mode = ablib.InkManipulationMode.Inking;
            OnPropertyChanged("MainCanvas");
        }
        public static readonly RoutedCommand ModeChangeToEraser = new RoutedCommand("ModeChangeToEraser", typeof(MainWindow));
        private void ModeChangeToEraserCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.Mode = ablib.InkManipulationMode.Erasing;
            OnPropertyChanged("MainCanvas");
        }
        public static readonly RoutedCommand ModeChangeToSelection = new RoutedCommand("ModeChangeToSelection", typeof(MainWindow));
        private void ModeChangeToSelectionCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.Mode = ablib.InkManipulationMode.Selecting;
            OnPropertyChanged("MainCanvas");
        }
        public static readonly RoutedCommand ClearSelection = new RoutedCommand("ClearSelection", typeof(MainWindow));
        private void ClearSelectionCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.ClearSelected();
        }

        private void FirstPageExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.CurrentPage = 0;
            OnPropertyChanged("MainCanvas");
        }
        private void LastPageExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.CurrentPage = MainCanvas.Count - 1;
            OnPropertyChanged("MainCanvas");
        }
        private void PreviousPageExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.CurrentPage++;
            OnPropertyChanged("MainCanvas");
        }
        private void NextPageExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.CurrentPage++;
            OnPropertyChanged("MainCanvas");
        }
        private void MoveDownExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.Scroll(new Vector(0, -20));
            OnPropertyChanged("MainCanvas");
        }
        private void MoveUpExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.Scroll(new Vector(0, 20));
            OnPropertyChanged("MainCanvas");
        }
        private void ScrollPageDownExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.Scroll(new Vector(0, -400));
            OnPropertyChanged("MainCanvas");
        }
        private void ScrollPageUpExecuted(object sender, ExecutedRoutedEventArgs e) {
            MainCanvas.Scroll(new Vector(0, 400));
            OnPropertyChanged("MainCanvas");
        }
        public static readonly RoutedCommand PageSetting = new RoutedCommand("PageSetting", typeof(MainWindow));
        private void PageSettingCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            var dialog = new PageSetting(MainCanvas.Info);
            if(dialog.ShowDialog() == true) {
                MainCanvas.Info = dialog.Info;
                OnPropertyChanged("MainCanvas");
            }
        }
        public static readonly RoutedCommand OpenHistory = new RoutedCommand("OpenHistory", typeof(MainWindow));
        private void OpenHistoryCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            string f = (string) e.Parameter;
            if(f != null) FileOpen(new List<string>() { f });
        }
        public static readonly RoutedCommand ShowAboutDialog = new RoutedCommand("ShowAboutDialog", typeof(MainWindow));
        private void ShowAboutDialogCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            (new AboutDialog()).ShowDialog();
        }


        private void mainCanvas_PreviewDragOver(object sender, DragEventArgs e) {
            if(e.Data.GetDataPresent(DataFormats.FileDrop, true)) e.Effects = DragDropEffects.Copy;
            else e.Effects = DragDropEffects.None;
            e.Handled = true;
        }

        private void mainCanvas_Drop(object sender, DragEventArgs e) {
            var files = (string[]) e.Data.GetData(DataFormats.FileDrop);
            FileOpen(new List<string>(files));
        }

        private void FileOpen(List<string> files) {
            if(files.Count > 0) {
                if(MainCanvas.FileName == "" && !MainCanvas.Updated) {
                    WindowTitle = "ファイルを開いています……";
                    while(files.Count > 0) {
                        try { MainCanvas.Open(files[0]); }
                        catch(InvalidOperationException) {
                            MessageBox.Show(files[0] + " は正しいフォーマットではありません．");
                            files.RemoveAt(0);
                            continue;
                        }
                        break;
                    }
                    WindowTitle = null;
                    AddHistory(files[0]);
                    files.RemoveAt(0);
                }
            }
            if(files.Count > 0) {
                foreach(var f in files) AddHistory(f);
                string me = System.Reflection.Assembly.GetExecutingAssembly().Location;
                using(var proc = new Process()) {
                    proc.StartInfo.FileName = me;
                    foreach(var f in files) {
                        proc.StartInfo.Arguments = "\"" + f + "\"";
                        WindowTitle = "f " + "を開いています．";
                        try { proc.Start(); }
                        catch(Win32Exception) { MessageBox.Show(f + " が開けませんでした．"); }
                        proc.WaitForInputIdle(10000);
                        WindowTitle = null;
                    }
                }
            }
        }

        void AddHistory(string file) {
            Properties.Settings.Default.History.Remove(file);
            Properties.Settings.Default.History.Insert(0, file);
            if(Properties.Settings.Default.History.Count > 10) {
                Properties.Settings.Default.History.RemoveAt(Properties.Settings.Default.History.Count - 1);
            }
            Properties.Settings.Default.Save();
            OnPropertyChanged("History");
        }
    }
}
