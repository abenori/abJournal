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
using System.IO.Compression;

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
            }
        }

        bool FitScaleToWidth = true;
        public int ScaleComboBoxIndex {
            get {
                if(FitScaleToWidth) return 0;
                if(mainCanvas.Scale == 0.5) return 1;
                else if(mainCanvas.Scale == 0.7) return 2;
                else if(mainCanvas.Scale == 1) return 3;
                else if(mainCanvas.Scale == 1.5) return 4;
                else if(mainCanvas.Scale == 2) return 5;
                else return 0;
            }
            set {
                FitScaleToWidth = false;
                var scales = new double[] { 0.5, 0.7, 1, 1.1, 1.25, 1.5, 2 };
                if(value >= 1 && value <= scales.Length) {
                    mainCanvas.Scale = scales[value - 1];
                } else {
                    FitScaleToWidth = true;
                    if(mainCanvas.Count == 0) return;
                    double maxWidth = mainCanvas.Select(c => c.Width).Max();
                    mainCanvas.Scale = MainPanel.ActualWidth / maxWidth;
                }
            }
        }

        int currentPen = 0;
        public int CurrentPen {
            get { return currentPen; }
            set {
                currentPen = value;
                mainCanvas.PenColor = Properties.Settings.Default.PenColor[currentPen];
                mainCanvas.PenThickness = Properties.Settings.Default.PenThickness[currentPen];
                mainCanvas.PenDashed = Properties.Settings.Default.PenDashed[currentPen];
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
                if(mainCanvas.FileName == null) rv = "無題ノート";
                else rv = System.IO.Path.GetFileName(mainCanvas.FileName);
                if(mainCanvas.Updated) rv += " （更新）";
                rv += "  abJournal";
                return rv;
            }
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

        BlockWndowsKey blockWindows = null;
        public MainWindow() {            
            var opt = new NDesk.Options.OptionSet() {
                {"getprotoschema","保存用.protoを作成．",var => {
                    using(var fs = new System.IO.StreamWriter(System.IO.Path.Combine(Environment.CurrentDirectory,"abJournal.proto"))){
                        fs.WriteLine(abJournalInkCanvasCollection.GetSchema());
                    }
                    Environment.Exit(0);
                }}
            };
            List<string> files = opt.Parse(Environment.GetCommandLineArgs());
            files.RemoveAt(0);

            InitializeComponent();
            DataContext = this;
            SetLowLevelKeyboardHook();

            //mainCanvas.ManipulationDelta += ((s, e) => { OnPropertyChanged("abmainCanvas"); });
            mainCanvas.UndoChainChanged += ((s, e) => { OnPropertyChanged("WindowTitle"); });
            mainCanvas.MouseDown += ((s, e) => { mainCanvas.Focus(); });
            mainCanvas.StylusDown += ((s, e) => { mainCanvas.Focus(); });
            mainCanvas.PropertyChanged += ((s, e) => { if(e.PropertyName == "Updated")OnPropertyChanged("WindowTitle"); });

            Panel.SetZIndex(mainCanvas, -4);

            ScaleComboBoxIndex = 0;// デフォルトは横幅に合わせる．
            //ScaleComboBoxIndex = 3;// Scale = 1
            //ScaleComboBoxIndex = 5;// Scale = 1
            CurrentPen = 0;
            mainCanvas.DrawingAlgorithm = Properties.Settings.Default.DrawingAlgorithm;
            mainCanvas.IgnorePressure = Properties.Settings.Default.IgnorePressure;

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
            if(blockWindows != null) {
                blockWindows.Dispose();
                blockWindows = null;
            }
        }
        void SetLowLevelKeyboardHook() {
            if(Properties.Settings.Default.IsBlockWindowsKey && blockWindows == null) {
                blockWindows = new BlockWndowsKey();
            } else if(!Properties.Settings.Default.IsBlockWindowsKey && blockWindows != null) {
                blockWindows.Dispose();
                blockWindows = null;
            }
        }
		class BlockWndowsKey : LowLevelKeyboardHook {
	        protected override void OnKeyDown(object sender, LowLevelKeyEventArgs e) {
	            if(e.Key == Key.LWin) e.Handled = true;
	        }
	        protected override void OnKeyUp(object sender, LowLevelKeyEventArgs e) {
	            if(e.Key == Key.LWin) e.Handled = true;
	        }
	    }

        private void Window_Closing(object sender, CancelEventArgs e) {
            if(!BeforeClose()) e.Cancel = true;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            if(mainCanvas.Count == 0)AddPage.Execute(null, this);
            mainCanvas.ClearUpdated();
            mainCanvas.ClearUndoChain();
            Window_SizeChanged(sender, null);
        }

        private void UndoCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.Undo();
        }
        private void UndoCommandCanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = mainCanvas != null ? mainCanvas.CanUndo() : false;
        }
        private void RedoCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.Redo();
        }
        private void RedoCommandCanExecute(object sender, CanExecuteRoutedEventArgs e) {
            e.CanExecute = mainCanvas != null ? mainCanvas.CanRedo() : false;
        }
        private void DeleteCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.Delete();
        }
        private void SaveAsCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            var fd = new SaveFileDialog();
            fd.FileName = System.IO.Path.GetFileName(mainCanvas.FileName);
            fd.Filter = "abjnt ファイル (*.abjnt)|*.abjnt|PDF ファイル (*.pdf)|*.pdf|全てのファイル|*.*";
            if(fd.ShowDialog() == true) {
                try {
                    var ext = System.IO.Path.GetExtension(fd.FileName).ToLower();
                    if(ext == ".pdf") {
                        mainCanvas.SavePDF(fd.FileName);
                        //abmainCanvas.SavePDFWithiText(fd.FileName);
                    } else {
                        mainCanvas.Save(fd.FileName);
                        mainCanvas.ClearUpdated();
                        AddHistory(fd.FileName);
                    }
                }
                catch(System.IO.IOException) {
                    MessageBox.Show("他のアプリケーションが\n" + fd.FileName + "\nを開いているようです．","abJournal");
                } 
                OnPropertyChanged("abmainCanvas");
            }
        }
        private void SaveCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            if(mainCanvas.FileName == null) SaveAsCommandExecuted(sender, e);
            else {
                mainCanvas.Save();
                mainCanvas.ClearUpdated();
                AddHistory(mainCanvas.FileName);
                OnPropertyChanged("abmainCanvas");
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
                FileOpen(new List<string>() { fd.FileName });
                OnPropertyChanged("abmainCanvas");
            }
        }
        public static readonly RoutedCommand Import = new RoutedCommand("Import", typeof(MainWindow));
        private void ImportCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            var ofd = new OpenFileDialog();
            ofd.Title = "インポートするファイルを選んでください";
            ofd.Filter = "pdfファイル (*.pdf)|*.pdf|xpsファイル (*.xps)|*.xps";
            if(ofd.ShowDialog() == true) {
                try {
					WindowTitle = "インポート中……";
                    mainCanvas.Import(ofd.FileName);
					WindowTitle = null;
                    // Importで横幅が変わる可能性があるので，「横幅にあわせる」の場合は計算し直し．
                    if(ScaleComboBoxIndex == 0) {
                        ScaleComboBoxIndex = 0;
                    }
                }
                catch(NotImplementedException) {
                    MessageBox.Show("サポートされていない形式です．", "abJournal");
                }
                catch(System.IO.FileNotFoundException) {
                    MessageBox.Show("ファイルが見付かりません．", "abJournal");
                }
            }
        }

        private bool BeforeClose() {
            if(mainCanvas.Updated) {
                MessageBoxResult res = MessageBoxResult.No;
                if(mainCanvas.FileName != null) {
                    res = MessageBox.Show("\"" + mainCanvas.FileName + "\" への変更を保存しますか？", "abJournal", MessageBoxButton.YesNoCancel);
                    if(res == MessageBoxResult.Yes) {
                        mainCanvas.Save();
                        AddHistory(mainCanvas.FileName);
                        return true;
                    } else return (res != MessageBoxResult.Cancel);
                } else {
                    res = MessageBox.Show("ノートは更新されています．保存しますか？", "abJournal", MessageBoxButton.YesNoCancel);
                    if(res == MessageBoxResult.Yes) {
                        var fd = new SaveFileDialog();
                        fd.Filter = "abjnt ファイル (*.abjnt)|*.abjnt|全てのファイル|*.*";
                        if(fd.ShowDialog() == true) {
                            mainCanvas.Save(fd.FileName);
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
            mainCanvas.SelectAll();
        }
        private void PasteCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.Paste();
        }
        private void CopyCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.Copy();
        }
        private void CutCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.Cut();
        }
        private void PrintCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            if(mainCanvas.Count == 0) {
                MessageBox.Show("ページがありません．","abJournal");
                return;
            }
            PrintDialog pd = new PrintDialog();
            if(pd.ShowDialog() == true) {
                WindowTitle = "印刷準備中……";
                FixedDocument doc = new FixedDocument();
                var canvases = mainCanvas.GetPrintingCanvases(Properties.Settings.Default.PrintDrawingAlgorithm);
                foreach(var c in canvases){
                    FixedPage page = new FixedPage();
                    page.Width = c.Width;
                    page.Height = c.Height;
                    page.Children.Add(c);
                    PageContent content = new PageContent();
                    content.Child = page;
                    doc.Pages.Add(content);
                }
                WindowTitle = "印刷中……";
                pd.PrintDocument(doc.DocumentPaginator,mainCanvas.FileName == null ?
                    "無題ノート" : System.IO.Path.GetFileNameWithoutExtension(mainCanvas.FileName));
                WindowTitle = null;
            }
            return;
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e) {
            if(FitScaleToWidth && mainCanvas.Count != 0) {
                double maxWidth = mainCanvas.Select(c => c.Width).Max();
                mainCanvas.Scale = MainPanel.ActualWidth / maxWidth;
            }
            /*
            abmainCanvas.Width = ActualWidth;
            abmainCanvas.Height = ActualHeight;
             */
            mainCanvas.Scroll();
        }
        public static readonly RoutedCommand AddPage = new RoutedCommand("AddPage", typeof(MainWindow));
        private void AddPageCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.AddCanvas();
        }
        public static readonly RoutedCommand InsertPage = new RoutedCommand("InsertPage", typeof(MainWindow));
        private void InsertPageCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.InsertCanvas(mainCanvas.CurrentPage + 1);
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
            if(mainCanvas.Count > 0) {
                mainCanvas.DeleteCanvas(mainCanvas.CurrentPage);
            }
        }

        public static readonly RoutedCommand SystemSetting = new RoutedCommand("SystemSetting", typeof(MainWindow));
        private void SystemSettingCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            SystemSetting dialog = new SystemSetting();
            if(dialog.ShowDialog() == true) {
                mainCanvas.DrawingAlgorithm = Properties.Settings.Default.DrawingAlgorithm;
                SetLowLevelKeyboardHook();
                mainCanvas.IgnorePressure = Properties.Settings.Default.IgnorePressure;
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
            mainCanvas.Mode = InkManipulationMode.Inking;
        }
        public static readonly RoutedCommand ModeChangeToEraser = new RoutedCommand("ModeChangeToEraser", typeof(MainWindow));
        private void ModeChangeToEraserCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.Mode = InkManipulationMode.Erasing;
        }
        public static readonly RoutedCommand ModeChangeToSelection = new RoutedCommand("ModeChangeToSelection", typeof(MainWindow));
        private void ModeChangeToSelectionCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.Mode = InkManipulationMode.Selecting;
        }
        public static readonly RoutedCommand ClearSelection = new RoutedCommand("ClearSelection", typeof(MainWindow));
        private void ClearSelectionCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.ClearSelected();
        }

        private void FirstPageExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.CurrentPage = 0;
        }
        private void LastPageExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.CurrentPage = mainCanvas.Count - 1;
        }
        private void PreviousPageExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.CurrentPage++;
        }
        private void NextPageExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.CurrentPage++;
        }
        private void MoveDownExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.Scroll(new Vector(0, -20));
        }
        private void MoveUpExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.Scroll(new Vector(0, 20));
        }
        private void ScrollPageDownExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.Scroll(new Vector(0, -400));
        }
        private void ScrollPageUpExecuted(object sender, ExecutedRoutedEventArgs e) {
            mainCanvas.Scroll(new Vector(0, 400));
        }
        public static readonly RoutedCommand PageSetting = new RoutedCommand("PageSetting", typeof(MainWindow));
        private void PageSettingCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            var dialog = new PageSetting(mainCanvas.Info);
            if(dialog.ShowDialog() == true) {
                mainCanvas.Info = dialog.Info;
                foreach(var c in mainCanvas) c.Info.BackgroundColor = dialog.info.InkCanvasInfo.BackgroundColor;
                mainCanvas.ReDraw();
                OnPropertyChanged("mainCanvas");
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
                if(mainCanvas.FileName == null && !mainCanvas.Updated) {
                    WindowTitle = "ファイルを開いています……";
                    while(files.Count > 0) {
                        try { 
                            mainCanvas.Open(files[0]);
                            AddHistory(files[0]);
                            files.RemoveAt(0);
                        }
                        catch(InvalidOperationException) {
                            MessageBox.Show(files[0] + " は正しいフォーマットではありません．");
                            files.RemoveAt(0);
                            continue;
                        }
                        catch(System.IO.FileNotFoundException) {
                            MessageBox.Show(files[0] + "は存在しません．");
                            files.RemoveAt(0);
                            continue;
                        }
                        break;
                    }
                    WindowTitle = null;
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

