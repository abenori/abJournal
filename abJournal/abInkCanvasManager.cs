﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Media;
using System.Windows;
using System.IO;
using System.IO.Compression;
using ProtoBuf;

namespace abJournal {
    public class abInkCanvasManager : IEnumerable<abInkCanvasManager.ManagedInkCanvas> {
        #region 付加情報クラス
        [ProtoContract(SkipConstructor = true)]
        public class CanvasCollectionInfo {
            public CanvasCollectionInfo() {
                InkCanvasInfo = new InkCanvasInfo();
                InkCanvasInfo.Size = Paper.GetSize(Paper.PaperSize.A4);
                Date = DateTime.Now;
                //ShowDate = true;
                //ShowTitle = true;
            }
            [ProtoMember(1)]
            public DateTime Date { get; set; }
            [ProtoMember(2)]
            public bool ShowDate { get; set; }
            [ProtoMember(3)]
            public bool ShowTitle { get; set; }
            [ProtoMember(4)]
            public string Title { get; set; }
            [ProtoMember(5)]
            public InkCanvasInfo InkCanvasInfo { get; set; }

            public CanvasCollectionInfo DeepCopy() {
                CanvasCollectionInfo rv = new CanvasCollectionInfo();
                rv.Date = Date;
                rv.ShowDate = ShowDate;
                rv.ShowTitle = ShowTitle;
                rv.InkCanvasInfo = InkCanvasInfo.DeepCopy();
                if(Title != null) rv.Title = (string) Title.Clone();
                return rv;
            }
        }

        [ProtoContract]
        public class Rule {
            public Rule() {
                DashArray = new DoubleCollection(new double[] { 1, 1 });
                Color = Colors.LightBlue;
                Interval = 80;
                Thickness = 2;
                Show = false;
            }
            [ProtoMember(1)]
            public Color Color { get; set; }
            [ProtoMember(2)]
            public DoubleCollection DashArray { get; set; }
            [ProtoMember(3)]
            public double Interval { get; set; }
            [ProtoMember(4)]
            public bool Show { get; set; }
            [ProtoMember(5)]
            public double Thickness { get; set; }
            public Rule DeepCopy() {
                Rule rv = new Rule();
                rv.Color = Color;
                rv.DashArray.Clear();
                for(int i = 0 ; i < DashArray.Count ; ++i) rv.DashArray.Add(DashArray[i]);
                rv.Interval = Interval;
                rv.Show = Show;
                rv.Thickness = Thickness;
                return rv;
            }
        }
        [ProtoContract(SkipConstructor = true)]
        public class InkCanvasInfo {
            [ProtoMember(1)]
            public Rule HorizontalRule = new Rule();
            [ProtoMember(2)]
            public Rule VerticalRule = new Rule();
            [ProtoMember(3)]
            public Size Size = new Size();
            [ProtoMember(4)]
            public Color BackgroundColor { get; set; }

            // Backgroundの種別を記述する．この操作はabInkCanvasManager内で完結させる．
            // "color": BackgroundColor（から作られたSolidBrush）
            // "image:xps:(識別子):page=(ページ)": xpsファイルから作られたVisualBrush，ファイル名は(識別子)で管理される
            [ProtoMember(5)]
            public string BackgroundStr { get; set; }
            
            public InkCanvasInfo() {
                BackgroundColor = Colors.White;
            }
            public InkCanvasInfo DeepCopy() {
                InkCanvasInfo rv = new InkCanvasInfo();
                rv.HorizontalRule = HorizontalRule.DeepCopy();
                rv.VerticalRule = VerticalRule.DeepCopy();
                rv.Size = Size;
                rv.BackgroundColor = BackgroundColor;
                rv.BackgroundStr = BackgroundStr;
                return rv;
            }
        }
        #endregion

        public CanvasCollectionInfo Info;
        public string FileName { get; set; }
        List<InkCanvasInfo> inkCanvasInfo = new List<InkCanvasInfo>();

        public class ManagedInkCanvas {
            public abInkCanvas InkCanvas;
            public InkCanvasInfo Info;
            public ManagedInkCanvas(abInkCanvas c, InkCanvasInfo i) {
                InkCanvas = c; Info = i;
            }
        }

        public abInkCanvasManager(abInkCanvasCollection main) {
            FileName = null;
            MainCanvas = main;
            Info = new CanvasCollectionInfo() { ShowDate = true, ShowTitle = true };
            MainCanvas.PropertyChanged += BackgroundImageManager.PDFBackground.ScaleChanged;
        }

        #region Canvas追加
        public void AddCanvas() {
            AddCanvas(new abInkData());
        }
        public void AddCanvas(abInkData d) {
            AddCanvas(d, Info, Info.InkCanvasInfo);
        }
        public void AddCanvas(abInkData d, CanvasCollectionInfo info, InkCanvasInfo inkcanvasinfo) {
            InsertCanvas(d, info, inkcanvasinfo, MainCanvas.Count);
        }
        public void InsertCanvas(int index) {
            InsertCanvas(new abInkData(), index);
        }
        public void InsertCanvas(abInkData d, int index) {
            InsertCanvas(d, Info, Info.InkCanvasInfo, index);
        }
        public void InsertCanvas(abInkData d, CanvasCollectionInfo info,InkCanvasInfo inkcanvasinfo, int index) {
            var icinfo = inkcanvasinfo.DeepCopy();
            MainCanvas.InsertCanvas(d, inkcanvasinfo.Size, icinfo.BackgroundColor, index);
            var c = MainCanvas[index];
            inkCanvasInfo.Insert(index, icinfo);
            if(index == 0) DrawNoteContents(c, info);
            DrawRules(c, inkcanvasinfo.HorizontalRule, inkcanvasinfo.VerticalRule, (index == 0) && Info.ShowTitle);
            SetBackgroundFromStr(index);
        }
        public void DeleteCanvas(int index) {
            CleanUpBrush(this[index]);
            MainCanvas.DeleteCanvas(index);
            inkCanvasInfo.RemoveAt(index);
        }
        public void Clear() {
            MainCanvas.Clear();
            inkCanvasInfo.Clear();
        }
        #endregion

        public void ReDraw() {
            for(int i = 0 ; i < MainCanvas.Count ; ++i){
                var c = this[i];
                if(i == 0) DrawNoteContents(c.InkCanvas, Info);
                DrawRules(c.InkCanvas, c.Info.HorizontalRule, c.Info.VerticalRule, (i == 0) && Info.ShowTitle);
                c.InkCanvas.ReDraw();
            }
        }

        #region background関連
        public void SetBackground(int index,Color color) {
            var c = this[index];
            CleanUpBrush(c);
            c.InkCanvas.Background = new SolidColorBrush(color);
            c.InkCanvas.Background.Freeze();
            c.Info.BackgroundColor = color;
            c.Info.BackgroundStr = "color";
        }
        void CleanUpBrush(ManagedInkCanvas c) {
            var str = c.Info.BackgroundStr;
            if(str == null) return;
            if(str.StartsWith("image")) {
                str = str.Substring("image:".Length);
                if(str.StartsWith("xps:")) {
                    BackgroundImageManager.XPSBackground.DisposeBackground(c);
                }else if(str.StartsWith("pdf:")){
                    BackgroundImageManager.PDFBackground.DisposeBackground(c);
                }
            }
        }
        public void SetBackgroundFromStr(int index) {
            var c = this[index];
            string str = c.Info.BackgroundStr;
            if(str == null || str == "" || str == "color") SetBackground(index, c.Info.BackgroundColor);
            else if(str.StartsWith("image:")) {
                str = str.Substring("image:".Length);
                if(str.StartsWith("xps:")) {
                    str = str.Substring("xps:".Length);
                    int r = str.IndexOf(":");
                    var file = AttachedFile.GetFileFromIdentifier(str.Substring(0, r));
                    if(file != null) {
                        int pageNumber = Int32.Parse(str.Substring(r + "page=".Length + 1));
                        BackgroundImageManager.XPSBackground.SetBackground(c, file, pageNumber);
                    }
                } else if(str.StartsWith("pdf:")){
                    str = str.Substring("pdf:".Length);
                    int r = str.IndexOf(":");
                    try {
                        using(var file = AttachedFile.GetFileFromIdentifier(str.Substring(0, r))) {
                            int pageNumber = Int32.Parse(str.Substring(r + "page=".Length + 1));
                            BackgroundImageManager.PDFBackground.SetBackground(c, file, pageNumber);
                        }
                    }
                    catch { }// file = nullなら無視する
                } else throw new NotImplementedException();
            } else throw new NotImplementedException();
        }
        public void SetBackgroundFromStr() {
            for(int i = 0 ; i < Count ; ++i) SetBackgroundFromStr(i);
        }
        #endregion

        #region Import
        public void Import(string path) {
            // 全く更新がない時はインポートしたページのみにする
            bool newImport = false;
            if(!MainCanvas.Updated && FileName == null) {
                DeleteCanvas(0);
                Info.ShowTitle = false;
                newImport = true;
            }
            using(var file = new AttachedFile(path)) {
                var ext = System.IO.Path.GetExtension(path);
                switch(ext) {
                case ".xps":
                    BackgroundImageManager.LoadXPSFile(file, this);
                    if(newImport) {
                        MainCanvas.ClearUndoChain();
                        MainCanvas.ClearUpdated();
                    }
                    break;
                case ".pdf":
                    int oldCount = Count;
                    BackgroundImageManager.LoadPDFFile(file, this);
                    for(int i = 0 ; i < Count - oldCount ; ++i) {
                        this[i + oldCount].Info.BackgroundStr = "image:pdf:" + file.Identifier + ":page=" + i.ToString();
                    }
                    if(newImport) {
                        MainCanvas.ClearUndoChain();
                        MainCanvas.ClearUpdated();
                    }
                    break;
                default:
                    throw new NotImplementedException();
                }
            }
        }
        #endregion

        public IEnumerable<abInkCanvas> GetInkCanvases(DrawingAlgorithm algo) {
            for(int i = 0 ; i < Count ; ++i) {
                var c = this[i];
                var r = c.InkCanvas.Clone();
                r.InkData.DrawingAlgorithm = algo;
                r.ReDraw();
                if(i == 0) DrawNoteContents(r);
                DrawRules(r, c.Info.HorizontalRule, c.Info.VerticalRule, (i == 0 && Info.ShowTitle));
                yield return r;
            }
        }

        #region 保存など
        [ProtoContract]
        public class ablibInkCanvasCollectionSavingProtobufData {
            [ProtoContract(SkipConstructor = true)]
            public class CanvasData {
                public CanvasData(abInkData d, InkCanvasInfo i) {
                    Data = d;
                    Info = i.DeepCopy();
                }
                [ProtoMember(1)]
                public abInkData Data;
                [ProtoMember(2)]
                public InkCanvasInfo Info;
            }
            [ProtoMember(1)]
            public List<CanvasData> Data { get; set; }
            [ProtoMember(2)]
            public CanvasCollectionInfo Info { get; set; }
            [ProtoMember(3)]
            public List<AttachedFile.SavingAttachedFile> AttachedFiles { get; set; }
            public ablibInkCanvasCollectionSavingProtobufData() {
                Data = new List<CanvasData>();
                Info = new CanvasCollectionInfo();
                AttachedFiles = new List<AttachedFile.SavingAttachedFile>();
            }
        }
        public class ablibInkCanvasCollectionSavingData {
            public class CanvasData {
                public CanvasData() {
                    Data = new abJournal.Saving.InkData();
                    Info = new InkCanvasInfo();
                }
                public CanvasData(abJournal.Saving.InkData d, InkCanvasInfo i) {
                    Data = d;
                    Info = i.DeepCopy();
                }
                public abJournal.Saving.InkData Data;
                public InkCanvasInfo Info;
            }
            public List<CanvasData> Data { get; set; }
            public CanvasCollectionInfo Info { get; set; }
            public ablibInkCanvasCollectionSavingData() {
                Data = new List<CanvasData>();
                Info = new CanvasCollectionInfo();
            }
        }
        public static string GetSchema() {
            return abInkData.SetProtoBufTypeModel(ProtoBuf.Meta.TypeModel.Create()).GetSchema(typeof(ablibInkCanvasCollectionSavingProtobufData));
        }
        public void Save() {
            Save(FileName);
        }
        public void Save(string file) {
            ablibInkCanvasCollectionSavingProtobufData data = new ablibInkCanvasCollectionSavingProtobufData();
            foreach(var c in this) {
                data.Data.Add(new ablibInkCanvasCollectionSavingProtobufData.CanvasData(c.InkCanvas.InkData,c.Info));
            }
            data.Info = Info;
            var model = abInkData.SetProtoBufTypeModel(ProtoBuf.Meta.TypeModel.Create());
            string tmpFile = null;
            if(File.Exists(file)) {
                tmpFile = Path.GetTempFileName();
                File.Delete(tmpFile);
                File.Move(file, tmpFile);
            }
            using(var zip = ZipFile.Open(file, ZipArchiveMode.Create)) {
                data.AttachedFiles = AttachedFile.Save(zip);
                var mainEntry = zip.CreateEntry("_data.abjnt");
                using(var ws = mainEntry.Open()) {
                    model.Serialize(ws, data);
                }
            }

            /*
            using(var wfs = new System.IO.FileStream(file, System.IO.FileMode.Create)) {
                //using(var zs = new System.IO.Compression.GZipStream(wfs, System.IO.Compression.CompressionLevel.Optimal)) {
                model.Serialize(wfs, data);
            }*/
            if(tmpFile != null) File.Delete(tmpFile);
            FileName = file;
        }

        public void SavePDF(string file) {
            double scale = (double) 720 / (double) 254 / Paper.mmToSize;

            using(var doc = new PdfSharp.Pdf.PdfDocument()) {
                for(int i = 0 ; i < MainCanvas.Count ; ++i) {
                    var page = doc.AddPage();
                    var ps = Paper.GetPaperSize(new Size(MainCanvas[i].Width, MainCanvas[i].Height));
                    // 1 = 1/72インチ = 25.4/72 mm
                    switch(ps) {
                    case Paper.PaperSize.A0: page.Size = PdfSharp.PageSize.A0; break;
                    case Paper.PaperSize.A1: page.Size = PdfSharp.PageSize.A1; break;
                    case Paper.PaperSize.A2: page.Size = PdfSharp.PageSize.A2; break;
                    case Paper.PaperSize.A3: page.Size = PdfSharp.PageSize.A3; break;
                    case Paper.PaperSize.A4: page.Size = PdfSharp.PageSize.A4; break;
                    case Paper.PaperSize.A5: page.Size = PdfSharp.PageSize.A5; break;
                    case Paper.PaperSize.B0: page.Size = PdfSharp.PageSize.B0; break;
                    case Paper.PaperSize.B1: page.Size = PdfSharp.PageSize.B1; break;
                    case Paper.PaperSize.B2: page.Size = PdfSharp.PageSize.B2; break;
                    case Paper.PaperSize.B3: page.Size = PdfSharp.PageSize.B3; break;
                    case Paper.PaperSize.B4: page.Size = PdfSharp.PageSize.B4; break;
                    case Paper.PaperSize.B5: page.Size = PdfSharp.PageSize.B5; break;
                    case Paper.PaperSize.Letter: page.Size = PdfSharp.PageSize.Letter; break;
                    case Paper.PaperSize.Tabloid: page.Size = PdfSharp.PageSize.Tabloid; break;
                    case Paper.PaperSize.Ledger: page.Size = PdfSharp.PageSize.Ledger; break;
                    case Paper.PaperSize.Legal: page.Size = PdfSharp.PageSize.Legal; break;
                    case Paper.PaperSize.Folio: page.Size = PdfSharp.PageSize.Folio; break;
                    case Paper.PaperSize.Quarto: page.Size = PdfSharp.PageSize.Quarto; break;
                    case Paper.PaperSize.Executive: page.Size = PdfSharp.PageSize.Executive; break;
                    case Paper.PaperSize.Statement: page.Size = PdfSharp.PageSize.Statement; break;
                    case Paper.PaperSize.Other:
                        page.Width = MainCanvas[i].Width * scale;
                        page.Height = MainCanvas[i].Height * scale;
                        break;
                    default:
                        var s = Paper.GetmmSize(ps);
                        page.Width = s.Width * 720 / 254;
                        page.Height = s.Height * 720 / 254;
                        break;
                    }
                    var g = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                    g.ScaleTransform(scale);
                    if(i == 0) DrawNoteContents(g, MainCanvas[i], Info);
                    DrawRules(g, MainCanvas[i], inkCanvasInfo[i].HorizontalRule,inkCanvasInfo[i].VerticalRule,(i == 0 && Info.ShowTitle));
                    MainCanvas[i].InkData.AddPdfGraphic(g);
                }
                doc.Info.Creator = "abJournal";
                doc.Info.Title = Info.Title;
                doc.Info.CreationDate = Info.Date;
                doc.Info.ModificationDate = DateTime.Now;
                doc.Save(new System.IO.FileStream(file, System.IO.FileMode.Create));
            }
        }

        public void Open(string file) {
            var watch = new Stopwatch();
            using(var fs = new System.IO.FileStream(file, System.IO.FileMode.Open)) {
                var model = abInkData.SetProtoBufTypeModel(ProtoBuf.Meta.TypeModel.Create());
                ablibInkCanvasCollectionSavingProtobufData protodata = null;
                try {
                    using(var zip = new ZipArchive(fs)) {
                        var data = zip.GetEntry("_data.abjnt");
                        using(var reader = data.Open()) {
                            protodata = (ablibInkCanvasCollectionSavingProtobufData) model.Deserialize(reader, new ablibInkCanvasCollectionSavingProtobufData(), typeof(ablibInkCanvasCollectionSavingProtobufData));
                        }
                        AttachedFile.Open(zip, protodata.AttachedFiles);
                    }
                }
                catch(Exception e) { System.Diagnostics.Debug.WriteLine(e.Message); }
                // protobufデシリアライズ
                if(protodata == null) {
                    try { protodata = (ablibInkCanvasCollectionSavingProtobufData) model.Deserialize(fs, new ablibInkCanvasCollectionSavingProtobufData(), typeof(ablibInkCanvasCollectionSavingProtobufData)); }
                    catch(Exception e) { System.Diagnostics.Debug.WriteLine(e.Message); }
                }
                if(protodata != null) {
                    Clear();
                    foreach(var d in protodata.Data) {
                        AddCanvas(d.Data, protodata.Info, d.Info);
                    }
                    Info = protodata.Info;
                } else {
                    System.Diagnostics.Debug.WriteLine("Protobufデシリアライズでエラー");
                    var xml = new System.Xml.Serialization.XmlSerializer(typeof(ablibInkCanvasCollectionSavingData));
                    ablibInkCanvasCollectionSavingData data = null;
                    // gzip解凍+XMLデシリアライズ
                    using(var zs = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress)) {
                        try { data = (ablibInkCanvasCollectionSavingData) xml.Deserialize(zs); }
                        catch(System.IO.InvalidDataException) { }

                    }
                    // 単なるXMLデシリアライズ
                    if(data == null) {
                        data = (ablibInkCanvasCollectionSavingData) xml.Deserialize(fs);
                    }

                    MainCanvas.Clear();
                    foreach(var d in data.Data) {
                        abInkData id = new abInkData();
                        id.LoadSavingData(d.Data);
                        AddCanvas(id, data.Info, d.Info);
                    }
                    Info = data.Info;
                }
            }
            if(MainCanvas.Count == 0) AddCanvas();
            FileName = file;
            SetBackgroundFromStr();
            MainCanvas.InvalidateVisual();
            MainCanvas.ClearUpdated();
            MainCanvas.ClearUndoChain();
            watch.CheckTime("Openにかかった時間");
            //CanvasCollection[0].ReDraw();
            //foreach(var str in CanvasCollection[0].abInkData.Strokes) {
            //ForDebugPtsDrwaing(new PointCollection(str.StylusPoints.Where(s => true).Select(p => p.ToPoint())), Brushes.Red);
            //}
            //ForDebugPtsDrwaing(new PointCollection(StrokeData.HoseiPts.Select(p => p.ToPoint())), Brushes.Blue);
            //ForDebugPtsDrwaing(StrokeData.CuspPts, Brushes.Green);
            //Scale = 13;
        }
        #endregion

        #region タイトルとか描くやつ（PDF含）
        static void GetYohakuHankei(abInkCanvas c, out double xyohaku, out double yyohaku, out double titleheight, out double hankei) {
            xyohaku = c.Width * 0.03;
            yyohaku = c.Width * 0.03;
            titleheight = c.Height * 0.06;
            hankei = c.Width * 0.02;
        }
        static Dictionary<abInkCanvas, Visual> noteContents = new Dictionary<abInkCanvas, Visual>();
        public void DrawNoteContents(abInkCanvas c) {
            DrawNoteContents(c, Info);
        }
        public static void DrawNoteContents(abInkCanvas c, CanvasCollectionInfo info) {
			if(noteContents.ContainsKey(c)){
				 c.Children.Remove(noteContents[c]); 
			}
            var visual = new DrawingVisual();
            using(var dc = visual.RenderOpen()){
                double xyohaku, yyohaku, height, hankei;
                GetYohakuHankei(c, out xyohaku, out yyohaku, out height, out hankei);
                if(info.ShowTitle) {
                    dc.DrawRoundedRectangle(null, new Pen(Brushes.LightGray, 1), new Rect(xyohaku, yyohaku, c.Width - 2 * xyohaku, height + 2*hankei), hankei, hankei);

                    if(info.Title != null && info.Title != "") {
                        double width = c.Width - 2 * xyohaku - 2 * hankei;
                        var pt = new Point(xyohaku + hankei, yyohaku + hankei / 2);
                        double fontsize = GuessFontSize(info.Title, "游ゴシック", width,height);
                        var text = new FormattedText(info.Title, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("游ゴシック"), fontsize, Brushes.Black);
                        text.MaxTextWidth = width;
                        text.MaxTextHeight = GetStringSize(info.Title, "游ゴシック", fontsize).Height;
                        int n = (int) (height / text.MaxTextHeight);
                        pt.Y += (height - n * text.MaxTextHeight) / 2;
                        text.MaxTextHeight *= n;
                        dc.DrawText(text, pt);
                    }

                    if(info.ShowDate) {
                        var text = new FormattedText(info.Date.ToLongDateString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("游ゴシック"), 12, Brushes.Gray);
                        dc.DrawText(text, new Point(c.Width - xyohaku - text.Width - hankei,yyohaku + 2 * hankei + height - text.Height - 4));

                        var pen = new Pen(Brushes.LightGray,1);
                        pen.DashStyle = new DashStyle(new double[]{3,3},0);
                        dc.DrawLine(pen,
                            new Point(xyohaku + hankei, yyohaku + 2 * hankei + height - text.Height - 8),
                            new Point(c.Width - xyohaku - hankei, yyohaku + 2 * hankei + height - text.Height - 8)
                            );
                    }
                }
            }
            c.Children.Add(visual);
            noteContents[c] = visual;
        }

        static Dictionary<abInkCanvas, Visual> rules = new Dictionary<abInkCanvas, Visual>();

        public void DrawRules(ManagedInkCanvas c,bool showTitle) {
            DrawRules(c.InkCanvas, c.Info.HorizontalRule, c.Info.VerticalRule, showTitle);
        }
        public static void DrawRules(abInkCanvas c, Rule Horizontal, Rule Vertical, bool showTitle) {
			if(rules.ContainsKey(c)){
				c.Children.Remove(rules[c]); 
			}
            var visual = new DrawingVisual();
            using(var dc = visual.RenderOpen()){
                double xyohaku, yyohaku, height, hankei;
                GetYohakuHankei(c, out xyohaku, out yyohaku, out height, out hankei);
                if(Horizontal.Show) {
                    double d = Horizontal.Interval;
                    if(showTitle && !Horizontal.Show) d += yyohaku + height + 2 * hankei;
                    var brush = new SolidColorBrush(Horizontal.Color);
                    brush.Freeze();
                    var pen = new Pen(brush, Horizontal.Thickness);
                    pen.DashStyle = new DashStyle(Horizontal.DashArray, 0);
                    pen.DashCap = PenLineCap.Flat;
                    pen.Freeze();
                    for( ; d < c.Height ; d += Horizontal.Interval) {
                        if(showTitle && yyohaku < d && d < yyohaku + height) {
                            dc.DrawLine(pen, new Point(0, d), new Point(xyohaku, d));
                            dc.DrawLine(pen, new Point(c.Width - xyohaku, d), new Point(c.Width, d));
                        } else {
                            dc.DrawLine(pen, new Point(0, d), new Point(c.Width, d));
                        }
                    }
                }
                if(Vertical.Show) {
                    var brush = new SolidColorBrush(Vertical.Color);
                    brush.Freeze();
                    var pen = new Pen(brush, Vertical.Thickness);
                    pen.DashStyle = new DashStyle(Vertical.DashArray, 0);
                    pen.DashCap = PenLineCap.Flat;
                    pen.Freeze();
                    for(double d = Vertical.Interval ; d < c.Width ; d += Vertical.Interval) {
                        if(showTitle && xyohaku < d && d < c.Width - xyohaku) {
                            dc.DrawLine(pen, new Point(d, 0), new Point(d, yyohaku));
                            dc.DrawLine(pen, new Point(d, yyohaku + height + 2 * hankei), new Point(d, c.Height));
                        } else {
                            dc.DrawLine(pen, new Point(d, 0), new Point(d, c.Height));
                        }
                    }
                }
            }
            rules[c] = visual;
            c.Children.Add(visual);
        }

        public static void DrawNoteContents(PdfSharp.Drawing.XGraphics g, abInkCanvas c, CanvasCollectionInfo info) {
            var pdf_ja_font_options = new PdfSharp.Drawing.XPdfFontOptions(PdfSharp.Pdf.PdfFontEncoding.Unicode, PdfSharp.Pdf.PdfFontEmbedding.Always);

            double xyohaku, yyohaku, height, hankei;
            GetYohakuHankei(c, out xyohaku, out yyohaku, out height, out hankei);
            var titlePath = new PdfSharp.Drawing.XGraphicsPath();
            if(info.ShowTitle) {
                titlePath.StartFigure();
                //titlePath.AddLine(xyohaku + hankei,yyohaku,c.Width - xyohaku - hankei, yyohaku);
                titlePath.AddArc(c.Width - xyohaku - hankei, yyohaku, hankei, hankei, 270, 90);
                titlePath.AddLine(c.Width - xyohaku, yyohaku + hankei, c.Width - xyohaku, yyohaku + hankei + height);
                titlePath.AddArc(c.Width - xyohaku - hankei, yyohaku + hankei + height, hankei, hankei, 0, 90);
                titlePath.AddLine(c.Width - xyohaku - hankei, yyohaku + 2 * hankei + height, xyohaku + hankei, yyohaku + 2 * hankei + height);
                titlePath.AddArc(xyohaku, yyohaku + height + hankei, hankei, hankei, 90, 90);
                titlePath.AddLine(xyohaku, yyohaku + hankei + height, xyohaku, yyohaku + hankei);
                titlePath.AddArc(xyohaku, yyohaku, hankei, hankei, 180, 90);
                titlePath.CloseFigure();
                g.DrawPath(PdfSharp.Drawing.XPens.LightGray, titlePath);

                if(info.Title != null && info.Title != "") {
                    var rect = new PdfSharp.Drawing.XRect(xyohaku + hankei, yyohaku + hankei / 2, c.Width - 2 * xyohaku - 2 * hankei, height);
                    double fontsize = GuessFontSize(info.Title, "游ゴシック", rect.Width, rect.Height);
                    // 真ん中に配置するための座標計算
                    double textHeight = GetStringSize(info.Title, "游ゴシック", fontsize).Height;
                    int n = (int) (rect.Height / textHeight);
                    rect.Y += (rect.Height - n * textHeight) / 2;
                    rect.Height = n * textHeight;
                    var pdf_ja_font = new PdfSharp.Drawing.XFont("游ゴシック", fontsize, PdfSharp.Drawing.XFontStyle.Regular, pdf_ja_font_options);
                    var tf = new PdfSharp.Drawing.Layout.XTextFormatter(g);
                    tf.DrawString(info.Title, pdf_ja_font, PdfSharp.Drawing.XBrushes.Black, rect, PdfSharp.Drawing.XStringFormats.TopLeft);
                }

                if(info.ShowDate) {
                    var textSize = GetStringSize(info.Date.ToLongDateString(), "游ゴシック", 12);
                    var pdf_ja_font = new PdfSharp.Drawing.XFont("游ゴシック", 12, PdfSharp.Drawing.XFontStyle.Regular, pdf_ja_font_options);
                    g.DrawString(
                        info.Date.ToLongDateString(),
                        pdf_ja_font,
                        PdfSharp.Drawing.XBrushes.Gray,
                        c.Width - xyohaku - textSize.Width - hankei,
                        yyohaku + 2 * hankei + height - textSize.Height - 4,
                        PdfSharp.Drawing.XStringFormats.TopLeft);

                    var pen = new PdfSharp.Drawing.XPen(PdfSharp.Drawing.XColors.LightGray, 1);
                    pen.DashPattern = new double[] { 3, 3 };
                    g.DrawLine(pen,
                        xyohaku + hankei,
                        yyohaku + 2 * hankei + height - textSize.Height - 8,
                        c.Width - xyohaku - hankei - 1,// 破線がかっこ悪いので調整
                        yyohaku + 2 * hankei + height - textSize.Height - 8);
                }
            }
        }

        public static void DrawRules(PdfSharp.Drawing.XGraphics g, abInkCanvas c, Rule HorizontalRule,Rule VerticalRule, bool showTitle) {
            double xyohaku, yyohaku, height, hankei;
            GetYohakuHankei(c, out xyohaku, out yyohaku, out height, out hankei);
            if(HorizontalRule.Show) {
                double d = HorizontalRule.Interval;
                if(showTitle && !HorizontalRule.Show) d += yyohaku + height + 2 * hankei;
                var pen = new PdfSharp.Drawing.XPen(PdfSharp.Drawing.XColor.FromArgb(
                    HorizontalRule.Color.A,
                    HorizontalRule.Color.R,
                    HorizontalRule.Color.G,
                    HorizontalRule.Color.B), HorizontalRule.Thickness);
                pen.DashPattern = HorizontalRule.DashArray.ToArray();
                for( ; d < c.Height ; d += HorizontalRule.Interval) {
                    if(showTitle && yyohaku < d && d < yyohaku + height) {
                        g.DrawLine(pen, 0, d, xyohaku, d);
                        g.DrawLine(pen, c.Width - xyohaku, d, c.Width, d);
                    } else {
                        g.DrawLine(pen, 0, d, c.Width, d);
                    }
                }
            }
            if(VerticalRule.Show) {
                var pen = new PdfSharp.Drawing.XPen(PdfSharp.Drawing.XColor.FromArgb(
                    VerticalRule.Color.A,
                    VerticalRule.Color.R,
                    VerticalRule.Color.G,
                    VerticalRule.Color.B), VerticalRule.Thickness);
                pen.DashPattern = VerticalRule.DashArray.ToArray();
                for(double d = VerticalRule.Interval ; d < c.Width ; d += VerticalRule.Interval) {
                    if(showTitle && xyohaku < d && d < c.Width - xyohaku) {
                        g.DrawLine(pen, d, 0, d, yyohaku);
                        g.DrawLine(pen, d, yyohaku + height + 2 * hankei, d, c.Height);
                    } else {
                        g.DrawLine(pen, d, 0, d, c.Height);
                    }
                }
            }
        }

        public static Size GetStringSize(string str, string fontname, double fontsize) {
            var ft = new FormattedText(str, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(fontname),
                fontsize,
                Brushes.White);
            return new Size(ft.Width, ft.Height);
        }

        public static double GuessFontSize(string str, string fontname, double width, double height) {
            var size = GetStringSize(str, fontname, 10);
            int n = (int) Math.Sqrt(height * size.Width / (width * size.Height));
            // はみ出ることがあったので1ひいておく．
            return 10 * Math.Max(n * width / size.Width, height / ((n + 1) * size.Height)) - 1;
        }
        #endregion

        #region IEnumerator実装
        public ManagedInkCanvas this[int i] {
            get { return new ManagedInkCanvas(MainCanvas[i], inkCanvasInfo[i]); }
        }

        public IEnumerator<ManagedInkCanvas> GetEnumerator() {
            for(int i = 0 ; i < MainCanvas.Count ; ++i) {
                yield return this[i];
            }
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            for(int i = 0 ; i < MainCanvas.Count ; ++i) {
                yield return this[i];
            }
        }
        public int Count { get { return MainCanvas.Count; } }
        #endregion
        abInkCanvasCollection MainCanvas;
        BackgroundImageManager BackgroundImageManager = new BackgroundImageManager();
    }
}