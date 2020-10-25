using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Diagnostics;
using Microsoft.Win32;
using System.ComponentModel;
using System.IO.Compression;
using System.IO;

/* 
 * TODO（やりたい）：
 * テキストボックスのサポート（なくてもいい気がしてきた）
 */ 

namespace abJournal {
    /// <summary>
    /// MainWindow.xaml の相互作用ロジック
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged,IDisposable {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
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
                    if(mainCanvas.Landscape) {
                        mainCanvas.Scale = mainCanvas.ActualHeight / maxWidth;
                    } else {
                        mainCanvas.Scale = mainCanvas.ActualWidth / maxWidth;
                    }
                }
            }
        }

        public enum InkMode{
            Pen0 = 0,Pen1 = 1,Pen2 = 2,Pen3 = 3,Pen4 = 4,Pen5 = 5,Pen6 = 6,Pen7 = 7,Erasing,Selecting
        }
        InkMode penMode = InkMode.Pen0;
        public InkMode PenMode {
            get { return penMode; }
            set {
                penMode = value;
                switch (penMode) {
                case InkMode.Erasing:
                    mainCanvas.Mode = InkManipulationMode.Erasing;
                    break;
                case InkMode.Selecting:
                    mainCanvas.Mode = InkManipulationMode.Selecting;
                    break;
                default:
                    mainCanvas.Mode = InkManipulationMode.Inking;
                    int index = (int)penMode;
                    mainCanvas.PenColor = PenColor[index];
                    mainCanvas.PenThickness = PenThickness[index];
                    mainCanvas.PenDashed = PenDashed[index];
                    mainCanvas.PenIsHilighter = PenHilight[index];
                    break;
                }
                OnPropertyChanged("PenMode");
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
#if DEBUG
                rv += " (Debug)";
#endif
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
        public System.Windows.Media.Color[] PenColor {
            get { return Properties.Settings.Default.PenColor; }
        }
        public bool[] PenShowInToolbar {
            get { return Properties.Settings.Default.PenShowInToolbar; }
        }
        public bool[] PenHilight {
            get { return Properties.Settings.Default.PenHilight; }
        }

        public System.Collections.Specialized.StringCollection History {
            get { return Properties.Settings.Default.History; }
        }

        BlockWndowsKey blockWindows = null;
        public MainWindow() {
            iTextSharp.text.FontFactory.RegisterDirectory(Environment.SystemDirectory.Replace("system32", "fonts"));
            bool topdf = false;
            bool help = false;
            var opt = new NDesk.Options.OptionSet() {
                {"getprotoschema","保存用.protoを作成．",var => {
                    using(var fs = new System.IO.StreamWriter(System.IO.Path.Combine(Environment.CurrentDirectory,"abJournal.proto"))){
                        fs.WriteLine(abJournalInkCanvasCollection.GetSchema());
                    }
                    Environment.Exit(0);
                }},
                {"topdf","PDFファイルに変換",val => {topdf = (val != null);}},
                {"help","ヘルプを表示",val =>{help = (val != null);}}
            };
            List<string> files = opt.Parse(Environment.GetCommandLineArgs());
            files.RemoveAt(0);
            if(topdf) {
                foreach(var f in files) {
                    var pdf = Path.Combine(Path.GetDirectoryName(f), Path.GetFileNameWithoutExtension(f) + ".pdf");
                    pdf = Path.GetFullPath(pdf);
                    var c = new abJournalInkCanvasCollection();
                    try {
                        c.Open(f);
                        c.SavePDF(pdf);
                    }
                    catch(Exception e) {
                        MessageBox.Show(f + " のPDFへの変換に失敗．\n" + e.Message + "\n" + e.StackTrace);
                    }
                }
                Environment.Exit(0);
            }

            InitializeComponent();
            abInkCanvas.ErasingCursor = Img2Cursor.MakeCursor(abJournal.Properties.Resources.eraser_cursor, new Point(2, 31), new Point(0, 0));
            DataContext = this;
            SetLowLevelKeyboardHook();

            //mainCanvas.ManipulationDelta += ((s, e) => { OnPropertyChanged("abmainCanvas"); });
            mainCanvas.UndoChainChanged += ((s, e) => { OnPropertyChanged("WindowTitle"); });
            mainCanvas.MouseDown += ((s, e) => { mainCanvas.Focus(); });
            mainCanvas.StylusDown += ((s, e) => { mainCanvas.Focus(); });
            mainCanvas.PropertyChanged += ((s, e) => { if(e.PropertyName == "Updated")OnPropertyChanged("WindowTitle"); });

            Panel.SetZIndex(mainCanvas, -4);

            ScaleComboBoxIndex = 0;// デフォルトは横幅に合わせる．
            PenMode = InkMode.Pen0;
            mainCanvas.DrawingAlgorithm = Properties.Settings.Default.DrawingAlgorithm;
            mainCanvas.IgnorePressure = Properties.Settings.Default.IgnorePressure;
            mainCanvas.Landscape = Properties.Settings.Default.Landscape;

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
        private void Window_Closed(object sender, EventArgs e) {
            Dispose();
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
        private async void SaveAsCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            var fd = new SaveFileDialog();
            fd.InitialDirectory = System.IO.Path.GetDirectoryName(mainCanvas.FileName);
            fd.FileName = System.IO.Path.GetFileName(mainCanvas.FileName);
            fd.Filter = "abjnt ファイル (*.abjnt)|*.abjnt|PDF ファイル (*.pdf)|*.pdf|全てのファイル|*.*";
            if(fd.ShowDialog() == true) {
                try {
                    var ext = System.IO.Path.GetExtension(fd.FileName).ToLower();
                    WindowTitle = "保存中……";
                    SaveButton.IsEnabled = false;
                    if(ext == ".pdf") {
                        try {
                            await mainCanvas.SavePDFAsync(fd.FileName);
                        }
                        catch(Exception ex) {
                            MessageBox.Show("PDFファイルの作成に失敗しました．\n" + ex.Message);
                        }
                        //abmainCanvas.SavePDFWithiText(fd.FileName);
                    } else {
                        try {
                            if(Properties.Settings.Default.SaveWithPDF) {
                                await mainCanvas.SaveDataAndPDFAsync(fd.FileName);
                            } else {
                                await mainCanvas.SaveAsync(fd.FileName);
                            }
                        }
                        catch(Exception ex) {
                            MessageBox.Show("ファイルの保存に失敗しました．\n" + ex.Message);
                        }
                        mainCanvas.ClearUpdated();
                        AddHistory(fd.FileName);
                    }
                }
                catch(System.IO.IOException) {
                    MessageBox.Show("他のアプリケーションが\n" + fd.FileName + "\nを開いているようです．","abJournal");
                }
                finally {
                    WindowTitle = null;
                    SaveButton.IsEnabled = true;
                }
                OnPropertyChanged("abmainCanvas");
            }
        }
        private async void SaveCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            if(mainCanvas.FileName == null) SaveAsCommandExecuted(sender, e);
            else {
                SaveButton.IsEnabled = false;
                try {
                    if (Properties.Settings.Default.SaveWithPDF) {
                        await mainCanvas.SaveDataAndPDFAsync();
                    } else {
                        await mainCanvas.SaveAsync();
                    }
                    SaveButton.IsEnabled = true;
                    mainCanvas.ClearUpdated();
                    AddHistory(mainCanvas.FileName);
                    OnPropertyChanged("abmainCanvas");
                }
                catch(Exception ex) {
                    MessageBox.Show(ex.Message);
                }
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
        public static readonly RoutedCommand ReOpen = new RoutedCommand("ReOpen", typeof(MainWindow));
        private void ReOpenCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            var file = mainCanvas.FileName;
            var page = mainCanvas.CurrentPage;
            if(file == null) {
                SaveAsCommandExecuted(sender, e);
                return;
            }
            if(!BeforeClose()) return;
            mainCanvas.Clear();
            mainCanvas.Open(file);
            mainCanvas.MovePage(page);

        }
        public static readonly RoutedCommand Import = new RoutedCommand("Import", typeof(MainWindow));
        private void ImportCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            var ofd = new OpenFileDialog();
            ofd.Title = "インポートするファイルを選んでください";
            ofd.Filter = "pdfファイル (*.pdf)|*.pdf|xpsファイル (*.xps)|*.xps";
            if(ofd.ShowDialog() == true) {
                try {
					WindowTitle = "インポート中……";
                    if(Path.GetExtension(ofd.FileName).ToLower() == ".pdf") {
                        try {
                            using (var pdfdoc = new iTextSharp.text.pdf.PdfReader(ofd.FileName)) { }
                        }
                        catch(System.Exception) {
                            if(MessageBox.Show("このPDFファイルは使用しているiTextSharpが対応していない可能性があるため，このPDFファイルを含む文書をPDFへと変換できない可能性があります．続行しますか？", "abJournal", MessageBoxButton.YesNo) != MessageBoxResult.Yes) {
                                WindowTitle = null;
                                return;
                            }
                        }
                    }
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
        public static readonly RoutedCommand SelectAll = new RoutedCommand("SelectAll", typeof(MainWindow));
        private void SelectAllCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            int page;
            if(menuPosition != null) {
                page = mainCanvas.GetPageFromScreenPoint(menuPosition.Value);
                menuPosition = null;
            } else page = mainCanvas.CurrentPage;
            mainCanvas.SelectAll(page);
        }
        private void PasteCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            Point pt;
            if(menuPosition != null)pt = menuPosition.Value;
            else pt = new Point(System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y);
            menuPosition = null;
            mainCanvas.Paste(mainCanvas.GetPageFromScreenPoint(pt), pt);
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
                if(mainCanvas.Landscape)mainCanvas.Scale = mainCanvas.ActualHeight / maxWidth;
                else mainCanvas.Scale = mainCanvas.ActualWidth / maxWidth;
            }
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
                mainCanvas.Landscape = Properties.Settings.Default.Landscape;
                if(ScaleComboBoxIndex == 0) ScaleComboBoxIndex = 0;// 倍率計算し直し
            }
        }

        public static readonly RoutedCommand PenSetting = new RoutedCommand("PenSetting", typeof(MainWindow));
        private void PenSettingCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            PenSettingDialog dialog = new PenSettingDialog();
            if(dialog.ShowDialog() == true) {
                PenMode = PenMode;
                OnPropertyChanged("PenColor");
                OnPropertyChanged("PenThickness");
                OnPropertyChanged("PenDashed");
                OnPropertyChanged("PenHilight");
                OnPropertyChanged("PenShowInToolbar");
            }
        }
        public static readonly RoutedCommand ModeChangeToPen = new RoutedCommand("ModeChangeToPen", typeof(MainWindow));
        private void ModeChangeToPenCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            PenMode = (InkMode)int.Parse(e.Parameter.ToString());
        }
        public static readonly RoutedCommand ModeChangeToEraser = new RoutedCommand("ModeChangeToEraser", typeof(MainWindow));
        private void ModeChangeToEraserCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            PenMode = InkMode.Erasing;
        }
        public static readonly RoutedCommand ModeChangeToSelection = new RoutedCommand("ModeChangeToSelection", typeof(MainWindow));
        private void ModeChangeToSelectionCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            PenMode = InkMode.Selecting;
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
            var wsize = mainCanvas.Landscape ? new Size(mainCanvas.ActualHeight, mainCanvas.ActualWidth) : new Size(mainCanvas.ActualWidth, mainCanvas.ActualHeight);
            var dialog = new PageSetting(mainCanvas.Info, wsize);
            if (dialog.ShowDialog() == true) {
                mainCanvas.Info = dialog.Info;
                mainCanvas.Info.InkCanvasInfo.Size = new Size(dialog.PaperWidth, dialog.PaperHeight);
                foreach (var c in mainCanvas) {
                    c.Info.BackgroundColor = dialog.info.InkCanvasInfo.BackgroundColor;
                    c.Width = dialog.PaperWidth;
                    c.Height = dialog.PaperHeight;
                }
                mainCanvas.ReDraw();
                OnPropertyChanged("mainCanvas");
            }
        }
        public static readonly RoutedCommand OpenHistory = new RoutedCommand("OpenHistory", typeof(MainWindow));
        private void OpenHistoryCommandExecuted(object sender, ExecutedRoutedEventArgs e) {
            string f = (string) e.Parameter;
            if(f != null) {
                f = f.Substring(f.IndexOf("(") + 1);
                f = f.Substring(0, f.Length - 1);
                if(f != null) FileOpen(new List<string>() { f });
            }
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
            var loadfiles = new List<string>();
            foreach(var f in files) {
                var ext = Path.GetExtension(f).ToLower();
                if(ext == ".pdf" || ext == ".xps") mainCanvas.Import(f);
                else loadfiles.Add(f);
            }
            FileOpen(loadfiles);
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
                        catch(InvalidOperationException e) {
                            MessageBox.Show(files[0] + " は正しいフォーマットではありません．" + e.Message);
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
        public void Dispose() {
            mainCanvas.Dispose();
        }

        Point? menuPosition = null;
        private void ContextMenu_Opened(object sender, RoutedEventArgs e) {
            menuPosition = new Point(System.Windows.Forms.Cursor.Position.X, System.Windows.Forms.Cursor.Position.Y);
            mainCanvas.IsEnabled = false;
        }

        private void ContextMenu_Closed(object sender, RoutedEventArgs e) {
            menuPosition = null;
            mainCanvas.IsEnabled = true;
        }

        private void CommandBinding_Executed(object sender, ExecutedRoutedEventArgs e)
        {

        }
    }
}

