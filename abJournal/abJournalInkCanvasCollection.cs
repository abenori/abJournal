using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using ProtoBuf;
using System.Windows;
using System.Windows.Media;
using System.IO;
using System.IO.Compression;
using System.ComponentModel;

namespace abJournal {
    public class abJournalInkCanvasCollection : abInkCanvasCollection<abJournalInkCanvas>,INotifyPropertyChanged {
        [ProtoContract(SkipConstructor = true)]
        public class CanvasCollectionInfo {
            public CanvasCollectionInfo() {
                InkCanvasInfo = new abJournalInkCanvas.InkCanvasInfo();
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
            public abJournalInkCanvas.InkCanvasInfo InkCanvasInfo { get; set; }

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

        public string FileName { get; set; }
        public CanvasCollectionInfo Info;
        public abJournalInkCanvasCollection() {
            FileName = null;
            Info = new CanvasCollectionInfo() { ShowDate = true, ShowTitle = true };
            PropertyChanged += BackgroundImageManager.PDFBackground.ScaleChanged;
        }

        #region Canvas追加
        public void AddCanvas() {
            AddCanvas(new abInkData());
        }
        public void AddCanvas(abInkData d) {
            AddCanvas(d, Info, Info.InkCanvasInfo);
        }
        public void AddCanvas(abInkData d, CanvasCollectionInfo info, abJournalInkCanvas.InkCanvasInfo inkcanvasinfo) {
            InsertCanvas(d, info, inkcanvasinfo, Count);
        }
        public void InsertCanvas(int index) {
            InsertCanvas(new abInkData(), index);
        }
        public void InsertCanvas(abInkData d, int index) {
            InsertCanvas(d, Info, Info.InkCanvasInfo, index);
        }
        public void InsertCanvas(abInkData d, CanvasCollectionInfo info, abJournalInkCanvas.InkCanvasInfo inkcanvasinfo, int index) {
            var canvas = new abJournalInkCanvas(d, inkcanvasinfo);
            base.InsertCanvas(canvas, index);
            if(index == 0) DrawNoteContents(canvas, info);
            DrawRules(canvas, inkcanvasinfo.HorizontalRule, inkcanvasinfo.VerticalRule, (index == 0) && Info.ShowTitle);
        }

        public void ReDraw() {
            for(int i = 0 ; i < Count ; ++i) {
                var c = this[i];
                c.CleanUpBrush();
                c.SetBackgroundFromStr();
                if(i == 0) DrawNoteContents(c, Info);
                DrawRules(c, c.Info.HorizontalRule, c.Info.VerticalRule, (i == 0) && Info.ShowTitle);
                c.ReDraw();
            }
        }
        #endregion

        #region background関連
        public void SetBackgroundFromStr() {
            foreach(var c in this) c.SetBackgroundFromStr();
        }
        #endregion

        #region Import
        public void Import(string path) {
            // 全く更新がない時はインポートしたページのみにする
            bool newImport = false;
            if(!Updated && FileName == null) {
                DeleteCanvas(0);
                Info.ShowTitle = false;
                newImport = true;
            }
            using(var file = new AttachedFile(path)) {
                var ext = System.IO.Path.GetExtension(path);
                switch(ext) {
                case ".pdf": {
                        int oldCount = Count;
                        BackgroundImageManager.LoadPDFFile(file, this);
                        for(int i = 0 ; i < Count - oldCount ; ++i) {
                            this[i + oldCount].Info.BackgroundStr = "image:pdf:" + file.Identifier + ":page=" + i.ToString();
                        }
                        break;
                    }
                case ".xps": {
                        int oldCount = Count;
                        BackgroundImageManager.LoadXPSFile(file, this);
                        for(int i = 0 ; i < Count - oldCount ; ++i) {
                            this[i + oldCount].Info.BackgroundStr = "image:xps:" + file.Identifier + ":page=" + i.ToString();
                        }
                        break;
                    }
                default:
                    throw new NotImplementedException();
                }
            }
            if(newImport) {
                ClearUndoChain();
                ClearUpdated();
            }
        }
        #endregion

        #region 保存など
        [ProtoContract]
        public class ablibInkCanvasCollectionSavingProtobufData {
            [ProtoContract(SkipConstructor = true)]
            public class CanvasData {
                public CanvasData(abInkData d, abJournalInkCanvas.InkCanvasInfo i) {
                    Data = d;
                    Info = i.DeepCopy();
                }
                [ProtoMember(1)]
                public abInkData Data;
                [ProtoMember(2)]
                public abJournalInkCanvas.InkCanvasInfo Info;
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
                    Info = new abJournalInkCanvas.InkCanvasInfo();
                }
                public CanvasData(abJournal.Saving.InkData d, abJournalInkCanvas.InkCanvasInfo i) {
                    Data = d;
                    Info = i.DeepCopy();
                }
                public abJournal.Saving.InkData Data;
                public abJournalInkCanvas.InkCanvasInfo Info;
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
                data.Data.Add(new ablibInkCanvasCollectionSavingProtobufData.CanvasData(c.InkData, c.Info));
            }
            data.Info = Info;
            var model = abInkData.SetProtoBufTypeModel(ProtoBuf.Meta.TypeModel.Create());
            string tmpFile = null;
            if(File.Exists(file)) {
                tmpFile = Path.GetTempFileName();
                File.Delete(tmpFile);
                File.Move(file, tmpFile);
            }
            try {
                using(var zip = ZipFile.Open(file, ZipArchiveMode.Create)) {
                    data.AttachedFiles = AttachedFile.Save(zip);
                    var mainEntry = zip.CreateEntry("_data.abjnt");
                    using(var ws = mainEntry.Open()) {
                        model.Serialize(ws, data);
                    }
                }
            }
            catch(Exception e) {
                File.Move(tmpFile, file);
                throw e;
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
            var documents = new Dictionary<string, PdfSharp.Pdf.PdfDocument>();
            try {
                using(var doc = new PdfSharp.Pdf.PdfDocument()) {
                    for(int i = 0 ; i < Count ; ++i) {
                        PdfSharp.Pdf.PdfPage page;
                        if(this[i].Info.BackgroundStr.StartsWith("image:pdf:")) {
                            var str = this[i].Info.BackgroundStr.Substring("image:pdf:".Length);
                            var r = str.IndexOf(":");
                            if(r == -1) page = doc.AddPage();
                            else {
                                var id = str.Substring(0, r);
                                try {
                                    var pagenum = Int32.Parse(str.Substring(r + "page=".Length + 1));
                                    PdfSharp.Pdf.PdfDocument pdfdoc;
                                    if(documents.ContainsKey(id)) pdfdoc = documents[id];
                                    else {
                                        using(var f = AttachedFile.GetFileFromIdentifier(id)) {
                                            pdfdoc = PdfSharp.Pdf.IO.PdfReader.Open(f.FileName, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
                                            documents[id] = pdfdoc;
                                        }
                                    }
                                    doc.AddPage(pdfdoc.Pages[pagenum]);
                                    page = doc.Pages[i];
                                }
                                catch(FormatException) {
                                    page = doc.AddPage();
                                }
                            }
                        }else page = doc.AddPage();
                        var ps = Paper.GetPaperSize(new Size(this[i].Width, this[i].Height));
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
                            page.Width = this[i].Width * scale;
                            page.Height = this[i].Height * scale;
                            break;
                        default:
                            var s = Paper.GetmmSize(ps);
                            page.Width = s.Width * 720 / 254;
                            page.Height = s.Height * 720 / 254;
                            break;
                        }
                        var g = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                        g.ScaleTransform(scale);
                        if(i == 0) DrawNoteContents(g, this[i], Info);
                        DrawRules(g, this[i], this[i].Info.HorizontalRule, this[i].Info.VerticalRule, (i == 0 && Info.ShowTitle));
                        this[i].InkData.AddPdfGraphic(g);
                    }
                    doc.Info.Creator = "abJournal";
                    doc.Info.Title = Info.Title;
                    doc.Info.CreationDate = Info.Date;
                    doc.Info.ModificationDate = DateTime.Now;
                    doc.Save(new System.IO.FileStream(file, System.IO.FileMode.Create));
                }
            }
            finally {
                foreach(var d in documents) {
                    d.Value.Dispose();
                }
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

                    Clear();
                    foreach(var d in data.Data) {
                        abInkData id = new abInkData();
                        id.LoadSavingData(d.Data);
                        AddCanvas(id, data.Info, d.Info);
                    }
                    Info = data.Info;
                }
            }
            if(Count == 0) AddCanvas();
            FileName = file;
            SetBackgroundFromStr();
            InvalidateVisual();
            ClearUpdated();
            ClearUndoChain();
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
            if(noteContents.ContainsKey(c)) {
                c.Children.Remove(noteContents[c]);
            }
            var visual = new DrawingVisual();
            using(var dc = visual.RenderOpen()) {
                double xyohaku, yyohaku, height, hankei;
                GetYohakuHankei(c, out xyohaku, out yyohaku, out height, out hankei);
                if(info.ShowTitle) {
                    dc.DrawRoundedRectangle(null, new Pen(Brushes.LightGray, 1), new Rect(xyohaku, yyohaku, c.Width - 2 * xyohaku, height + 2 * hankei), hankei, hankei);

                    if(info.Title != null && info.Title != "") {
                        double width = c.Width - 2 * xyohaku - 2 * hankei;
                        var pt = new Point(xyohaku + hankei, yyohaku + hankei / 2);
                        double fontsize = GuessFontSize(info.Title, "游ゴシック", width, height);
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
                        dc.DrawText(text, new Point(c.Width - xyohaku - text.Width - hankei, yyohaku + 2 * hankei + height - text.Height - 4));

                        var pen = new Pen(Brushes.LightGray, 1);
                        pen.DashStyle = new DashStyle(new double[] { 3, 3 }, 0);
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

        public void DrawRules(abJournalInkCanvas c, bool showTitle) {
            DrawRules(c, c.Info.HorizontalRule, c.Info.VerticalRule, showTitle);
        }
        public static void DrawRules(abJournalInkCanvas c, abJournalInkCanvas.Rule Horizontal, abJournalInkCanvas.Rule Vertical, bool showTitle) {
            if(rules.ContainsKey(c)) {
                c.Children.Remove(rules[c]);
            }
            var visual = new DrawingVisual();
            using(var dc = visual.RenderOpen()) {
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

        public static void DrawNoteContents(PdfSharp.Drawing.XGraphics g, abJournalInkCanvas c, CanvasCollectionInfo info) {
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

        public static void DrawRules(PdfSharp.Drawing.XGraphics g, abJournalInkCanvas c, abJournalInkCanvas.Rule HorizontalRule, abJournalInkCanvas.Rule VerticalRule, bool showTitle) {
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

        #region 印刷用
        public IEnumerable<abJournalInkCanvas> GetPrintingCanvases(DrawingAlgorithm algo) {
            for(int i = 0 ; i < Count ; ++i){
                var r = this[i].GetPrintingCanvas(algo);
                if(i == 0) DrawNoteContents(r);
                DrawRules(r, r.Info.HorizontalRule, r.Info.VerticalRule, (i == 0 && Info.ShowTitle));
                yield return r;
            }
        }
        #endregion
    }
}
