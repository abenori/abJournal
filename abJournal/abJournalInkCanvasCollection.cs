using System;
using System.Collections.Generic;
using System.Linq;
using ProtoBuf;
using System.Windows;
using System.Windows.Media;
using System.IO;
using System.IO.Compression;
using System.ComponentModel;
using System.Windows.Ink;

namespace abJournal {
    public partial class abJournalInkCanvasCollection : abInkCanvasCollection<abJournalInkCanvas>, INotifyPropertyChanged, IDisposable {
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
                if (Title != null) rv.Title = (string)Title.Clone();
                return rv;
            }
        }

        public string FileName { get; set; }
        public CanvasCollectionInfo Info;
        public abJournalInkCanvasCollection() {
            FileName = null;
            Info = new CanvasCollectionInfo() { ShowDate = true, ShowTitle = true };
            PropertyChanged += BackgroundPDF.ScaleChanged;
        }

        #region Canvas追加
        public void AddCanvas() {
            AddCanvas(new List<abStroke>(), new DrawingAttributes(), new DrawingAttributesPlus());
        }
        public void AddCanvas(List<abStroke> strokes, DrawingAttributes datt, DrawingAttributesPlus dattp) {
            AddCanvas(strokes, datt, dattp, Info, Info.InkCanvasInfo);
        }
        public void AddCanvas(List<abStroke> strokes, DrawingAttributes datt, DrawingAttributesPlus dattp, CanvasCollectionInfo info, abJournalInkCanvas.InkCanvasInfo inkcanvasinfo) {
            InsertCanvas(strokes, datt, dattp, info, inkcanvasinfo, Count);
        }
        public void InsertCanvas(int index) {
            InsertCanvas(new List<abStroke>(), new DrawingAttributes(), new DrawingAttributesPlus(), index);
        }
        public void InsertCanvas(List<abStroke> strokes, DrawingAttributes datt, DrawingAttributesPlus dattp, int index) {
            InsertCanvas(strokes, datt, dattp, Info, Info.InkCanvasInfo, index);
        }
        public void InsertCanvas(List<abStroke> strokes, DrawingAttributes datt, DrawingAttributesPlus dattp, CanvasCollectionInfo info, abJournalInkCanvas.InkCanvasInfo inkcanvasinfo, int index) {
            var canvas = new abJournalInkCanvas(strokes, datt, dattp, inkcanvasinfo);
            base.InsertCanvas(canvas, index);
            if (index == 0) DrawNoteContents(canvas, info);
            DrawRules(canvas, (index == 0) && Info.ShowTitle);
        }

        public void ReDraw() {
            for (int i = 0; i < Count; ++i) {
                var c = this[i];
                c.CleanUpBrush();
                c.SetBackgroundFromStr();
                c.Info = this.Info.InkCanvasInfo;
                if (i == 0) DrawNoteContents(c, Info);
                DrawRules(c, (i == 0) && Info.ShowTitle);
                c.ReDraw();
            }
        }
        #endregion

        #region background関連
        public void SetBackgroundFromStr() {
            foreach (var c in this) c.SetBackgroundFromStr();
        }
        #endregion

        #region Import
        public async void Import(string path) {
            // 全く更新がない時はインポートしたページのみにする
            bool newImport = false;
            if (!Updated && FileName == null) {
                DeleteCanvas(0);
                Info.ShowTitle = false;
                newImport = true;
            }
            using (var file = new AttachedFile(path)) {
                var ext = System.IO.Path.GetExtension(path).ToLower();
                switch (ext) {
                case ".pdf": {
                        int oldCount = Count;
                        await BackgroundPDF.LoadFile(file, this);
                        for (int i = 0; i < Count - oldCount; ++i) {
                            this[i + oldCount].Info.BackgroundStr = "image:pdf:" + file.Identifier + ":page=" + i.ToString();
                        }
                        break;
                    }
                case ".xps": {
                        int oldCount = Count;
                        BackgroundXPS.LoadFile(file, this);
                        for (int i = 0; i < Count - oldCount; ++i) {
                            this[i + oldCount].Info.BackgroundStr = "image:xps:" + file.Identifier + ":page=" + i.ToString();
                        }
                        break;
                    }
                default:
                    throw new NotImplementedException();
                }
            }
            if (newImport) {
                ClearUndoChain();
                ClearUpdated();
            }
        }
        #endregion

        #region 保存用データ
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
        [ProtoContract]
        public class ablibInkCanvasCollectionSavingProtobufData2 {
            [ProtoContract(SkipConstructor = true)]
            public class CanvasData {
                public CanvasData(List<abStroke> strokes, abJournalInkCanvas.InkCanvasInfo i) {
                    Info = i.DeepCopy();
                    Data = new InkData(strokes);
                }
                [ProtoMember(1)]
                public abJournalInkCanvas.InkCanvasInfo Info;
                [ProtoContract(SkipConstructor = true)]
                public class InkData {
                    public InkData(List<abStroke> d) {
                        Strokes = new List<StrokeDataStruct>();
                        foreach (var c in d) { Strokes.Add(new StrokeDataStruct(c)); }
                        Texts = new List<TextDataStruct>();
                    }
                    [ProtoMember(1)]
                    public List<StrokeDataStruct> Strokes { get; set; }
                    [ProtoMember(2)]
                    public List<TextDataStruct> Texts { get; set; }
                }
                [ProtoMember(2)]
                public InkData Data;
            }
            [ProtoMember(1)]
            public List<CanvasData> Data { get; set; }
            [ProtoMember(2)]
            public List<AttachedFile.SavingAttachedFile> AttachedFiles { get; set; }
            [ProtoMember(3)]
            public CanvasCollectionInfo Info { get; set; }
            public ablibInkCanvasCollectionSavingProtobufData2() {
                Data = new List<CanvasData>();
                AttachedFiles = new List<AttachedFile.SavingAttachedFile>();
                Info = new CanvasCollectionInfo();
            }
        }

        #endregion

        #region 保存
        public static string GetSchema() {
            //return abInkData.SetProtoBufTypeModel(ProtoBuf.Meta.TypeModel.Create()).GetSchema(typeof(ablibInkCanvasCollectionSavingProtobufData));
            //return abInkData.SetProtoBufTypeModel(ProtoBuf.Meta.RuntimeTypeModel.Create()).GetSchema(typeof(ablibInkCanvasCollectionSavingProtobufData));
            return abInkData.SetProtoBufTypeModel(ProtoBuf.Meta.RuntimeTypeModel.Create()).GetSchema(typeof(ablibInkCanvasCollectionSavingProtobufData2));
        }
        private ablibInkCanvasCollectionSavingProtobufData2 MakeSavingData() {
            var data = new ablibInkCanvasCollectionSavingProtobufData2();
            data.Data.Capacity = this.Count;
            foreach (var c in this) {
                data.Data.Add(new ablibInkCanvasCollectionSavingProtobufData2.CanvasData(c.Strokes.Select(s => s as abStroke).ToList(), c.Info));
            }
            data.Info = Info;
            return data;
        }
        public async System.Threading.Tasks.Task SaveDataAndPDFAsync() {
            await SaveDataAndPDFAsync(FileName);
        }
        public async System.Threading.Tasks.Task SaveDataAndPDFAsync(string file) {
            var data = MakeSavingData();
            try {
                await SaveAsync(file, data);
                await SavePDFAsync(Path.ChangeExtension(file, ".pdf"), data);
            }
            catch (Exception e) { throw e; }
            FileName = file;
        }
        public async System.Threading.Tasks.Task SaveAsync() {
            await SaveAsync(FileName);
        }
        public async System.Threading.Tasks.Task SaveAsync(string file) {
            try { await SaveAsync(file, MakeSavingData()); }
            catch (Exception e) { throw e; }
            FileName = file;
        }
        public void SaveDataAndPDF() {
            SaveDataAndPDF(FileName);
        }
        public void SaveDataAndPDF(string file) {
            var data = MakeSavingData();
            try {
                Save(file, data);
                SavePDF(Path.ChangeExtension(file, ".pdf"), data);
            }
            catch (Exception e) { throw e; }
            FileName = file;
        }

        public void Save() {
            Save(FileName);
        }
        public void Save(string file) {
            try { Save(file, MakeSavingData()); }
            catch (Exception e) { throw e; }
            FileName = file;
        }
        private static void Save(string file, ablibInkCanvasCollectionSavingProtobufData2 data) {
            string tmpFile = null;
            if (File.Exists(file)) {
                tmpFile = Path.GetTempFileName();
                File.Delete(tmpFile);
                File.Move(file, tmpFile);
            }
            try {
                var model = abInkData.SetProtoBufTypeModel2(ProtoBuf.Meta.RuntimeTypeModel.Create());
                using (var zip = ZipFile.Open(file, ZipArchiveMode.Create)) {
                    data.AttachedFiles = AttachedFile.Save(zip);
                    var mainEntry = zip.CreateEntry("_data.abjnt");
                    using (var ws = mainEntry.Open()) {
                        model.Serialize(ws, data);
                    }
                }
            }
            catch (Exception e) {
                System.Diagnostics.Debug.WriteLine(e.Message);
                File.Delete(file);
                if (tmpFile != null) File.Move(tmpFile, file);
                throw e;
            }

            /*
            using(var wfs = new System.IO.FileStream(file, System.IO.FileMode.Create)) {
                //using(var zs = new System.IO.Compression.GZipStream(wfs, System.IO.Compression.CompressionLevel.Optimal)) {
                model.Serialize(wfs, data);
            }*/
            if (tmpFile != null) File.Delete(tmpFile);
        }

        private static async System.Threading.Tasks.Task SaveAsync(string file, ablibInkCanvasCollectionSavingProtobufData2 data) {
            await System.Threading.Tasks.Task.Run(() => {
                Save(file, data);
            });
        }

        public async System.Threading.Tasks.Task SavePDFAsync(string file) {
            await SavePDFAsync(file, MakeSavingData());
        }
        public void SavePDF(string file) {
            SavePDF(file, MakeSavingData());
        }
        private static void SavePDF(string file, ablibInkCanvasCollectionSavingProtobufData2 data) {
            double scale = (double)720 / (double)254 / Paper.mmToSize;
            var documents = new Dictionary<string, iTextSharp.text.pdf.PdfReader>();
            FileStream fw = null;
            iTextSharp.text.Document doc = null;
            iTextSharp.text.pdf.PdfWriter writer = null;
            try {
                fw = new FileStream(file, FileMode.Create, FileAccess.Write);
                for (int i = 0; i < data.Data.Count; ++i) {
                    var ps = Paper.GetPaperSize(data.Data[i].Info.Size);
                    iTextSharp.text.Rectangle pagesize;
                    // 1 = 1/72インチ = 25.4/72 mm
                    switch (ps) {
                    case Paper.PaperSize.A0: pagesize = iTextSharp.text.PageSize.A0; break;
                    case Paper.PaperSize.A1: pagesize = iTextSharp.text.PageSize.A1; break;
                    case Paper.PaperSize.A2: pagesize = iTextSharp.text.PageSize.A2; break;
                    case Paper.PaperSize.A3: pagesize = iTextSharp.text.PageSize.A3; break;
                    case Paper.PaperSize.A4: pagesize = iTextSharp.text.PageSize.A4; break;
                    case Paper.PaperSize.A5: pagesize = iTextSharp.text.PageSize.A5; break;
                    case Paper.PaperSize.B0: pagesize = iTextSharp.text.PageSize.B0; break;
                    case Paper.PaperSize.B1: pagesize = iTextSharp.text.PageSize.B1; break;
                    case Paper.PaperSize.B2: pagesize = iTextSharp.text.PageSize.B2; break;
                    case Paper.PaperSize.B3: pagesize = iTextSharp.text.PageSize.B3; break;
                    case Paper.PaperSize.B4: pagesize = iTextSharp.text.PageSize.B4; break;
                    case Paper.PaperSize.B5: pagesize = iTextSharp.text.PageSize.B5; break;
                    case Paper.PaperSize.Letter: pagesize = iTextSharp.text.PageSize.LETTER; break;
                    case Paper.PaperSize.Tabloid: pagesize = iTextSharp.text.PageSize.TABLOID; break;
                    case Paper.PaperSize.Ledger: pagesize = iTextSharp.text.PageSize.LEDGER; break;
                    case Paper.PaperSize.Legal: pagesize = iTextSharp.text.PageSize.LEGAL; break;
                    //case Paper.PaperSize.Folio: pagesize = iTextSharp.text.PageSize.; break;
                    //case Paper.PaperSize.Quarto: pagesize = iTextSharp.text.PageSize.QUARTO; break;
                    case Paper.PaperSize.Executive: pagesize = iTextSharp.text.PageSize.EXECUTIVE; break;
                    //case Paper.PaperSize.Statement: pagesize = iTextSharp.text.PageSize.STATEMENT; break;
                    case Paper.PaperSize.Other:
                        pagesize = new iTextSharp.text.Rectangle((float)(data.Data[i].Info.Size.Width * scale), (float)(data.Data[i].Info.Size.Height * scale));
                        break;
                    default:
                        var s = Paper.GetmmSize(ps);
                        pagesize = new iTextSharp.text.Rectangle((float)s.Width * 720 / 254, (float)s.Height * 720 / 254);
                        break;
                    }
                    if (doc == null) {
                        doc = new iTextSharp.text.Document(pagesize);
                        writer = iTextSharp.text.pdf.PdfWriter.GetInstance(doc, fw);
                        doc.Open();
                    } else doc.SetPageSize(pagesize);
                    if (Properties.Settings.Default.Landscape) {
                        writer.AddPageDictEntry(iTextSharp.text.pdf.PdfName.ROTATE, new iTextSharp.text.pdf.PdfNumber(270));
                    }
                    if (data.Data[i].Info.BackgroundStr != null && data.Data[i].Info.BackgroundStr.StartsWith("image:pdf:")) {
                        var str = data.Data[i].Info.BackgroundStr.Substring("image:pdf:".Length);
                        var r = str.IndexOf(":");
                        if (r != -1) {
                            var id = str.Substring(0, r);
                            int pagenum;
                            if (Int32.TryParse(str.Substring(r + "page=".Length + 1), out pagenum)) {
                                iTextSharp.text.pdf.PdfReader pdfdoc;
                                if (documents.ContainsKey(id)) pdfdoc = documents[id];
                                else {
                                    using (var f = AttachedFile.GetFileFromIdentifier(id)) {
                                        pdfdoc = new iTextSharp.text.pdf.PdfReader(f.FileName);
                                        documents[id] = pdfdoc;
                                    }
                                }
                                var page = writer.GetImportedPage(pdfdoc, pagenum + 1);
                                writer.DirectContent.AddTemplate(page, 0, 0);
                            }
                        }
                    }
                    if (i == 0) DrawNoteContents(writer, data.Data[i].Info.Size.Width, data.Data[i].Info.Size.Height, data.Info, scale);
                    DrawRules(writer, data.Data[i].Info.Size.Width, data.Data[i].Info.Size.Height, data.Info.InkCanvasInfo.HorizontalRule, data.Info.InkCanvasInfo.VerticalRule, (i == 0 && data.Info.ShowTitle), scale);
                    DrawingStrokes.DrawPath(writer, scale, data.Data[i].Data.Strokes);
                    if (Properties.Settings.Default.SaveTextToPDF) {
                        var gstate = new iTextSharp.text.pdf.PdfGState();
                        gstate.FillOpacity = 0;
                        gstate.StrokeOpacity = 0;
                        var font = iTextSharp.text.FontFactory.GetFont("游ゴシック", iTextSharp.text.pdf.BaseFont.IDENTITY_H, iTextSharp.text.pdf.BaseFont.NOT_EMBEDDED, 16);
                        using (var analyzer = new InkAnalyzer()) {
                            var strokes = new StrokeCollection(data.Data[i].Data.Strokes.Select(s => new Stroke(s.StylusPoints)));
                            analyzer.AddStrokes(strokes);
                            analyzer.Analyze();
                            var nodes = analyzer.FindNodesOfType(ContextNodeType.Line);
                            foreach (var node in nodes) {
                                var rect = node.Location.GetBounds();
                                var str = ((LineNode)node).GetRecognizedString();
                                writer.DirectContent.SaveState();
                                writer.DirectContent.SetGState(gstate);
                                writer.DirectContent.BeginText();
                                writer.DirectContent.SetFontAndSize(font.BaseFont, (float)(scale * rect.Height));
                                writer.DirectContent.ShowTextAligned(iTextSharp.text.pdf.PdfContentByte.ALIGN_LEFT, str, (float)(scale * rect.Left), (float)(writer.PageSize.Height - scale * rect.Bottom), 0);
                                writer.DirectContent.EndText();
                                writer.DirectContent.RestoreState();
                            }
                        }
                    }
                    if (i != data.Data.Count - 1) doc.NewPage();
                }
                doc.AddCreator("abJournal");
                doc.AddTitle(data.Info.Title);
                writer.Info.Put(new iTextSharp.text.pdf.PdfName("CreationDate"), new iTextSharp.text.pdf.PdfDate(data.Info.Date));
                writer.Info.Put(new iTextSharp.text.pdf.PdfName("ModificationDate"), new iTextSharp.text.pdf.PdfDate(DateTime.Now));
            }
            finally {
                doc?.Close();
                writer?.Close();
                fw?.Close();
                foreach (var d in documents) {
                    d.Value.Close();
                }
            }
        }
        private static async System.Threading.Tasks.Task SavePDFAsync(string file, ablibInkCanvasCollectionSavingProtobufData2 data) {
            await System.Threading.Tasks.Task.Run(() => { SavePDF(file, data); });
        }

        #endregion

        #region 読み込み
        private bool LoadProtoBuf(System.IO.FileStream fs) {
            fs.Seek(0, SeekOrigin.Begin);
            var model = abInkData.SetProtoBufTypeModel(ProtoBuf.Meta.RuntimeTypeModel.Create());
            ablibInkCanvasCollectionSavingProtobufData protodata = null;
            try {
                var zip = new ZipArchive(fs);
                var data = zip.GetEntry("_data.abjnt");
                using (var reader = data.Open()) {
                    protodata = (ablibInkCanvasCollectionSavingProtobufData)model.Deserialize(reader, new ablibInkCanvasCollectionSavingProtobufData(), typeof(ablibInkCanvasCollectionSavingProtobufData));
                }
                AttachedFile.Open(zip, protodata.AttachedFiles);
            }
            catch (Exception e) { System.Diagnostics.Debug.WriteLine(e.Message); }
            // protobufデシリアライズ
            if (protodata == null) {
                try { protodata = (ablibInkCanvasCollectionSavingProtobufData)model.Deserialize(fs, new ablibInkCanvasCollectionSavingProtobufData(), typeof(ablibInkCanvasCollectionSavingProtobufData)); }
                catch (Exception e) { System.Diagnostics.Debug.WriteLine(e.Message); }
            }
            if (protodata != null) {
                Clear();
                Info = protodata.Info;
                foreach (var d in protodata.Data) {
                    AddCanvas(d.Data.Strokes.Select(s => new abStroke(s.StylusPoints,s.DrawingAttributes,s.DrawingAttributesPlus)).ToList(), new DrawingAttributes(), new DrawingAttributesPlus(), protodata.Info, d.Info);
                }
                return true;
            } else {
                return false;
            }
        }
        private bool LoadProtoBuf2(System.IO.FileStream fs) {
            var model = abInkData.SetProtoBufTypeModel2(ProtoBuf.Meta.RuntimeTypeModel.Create());
            ablibInkCanvasCollectionSavingProtobufData2 protodata = null;
            try {
                var zip = new ZipArchive(fs);
                var data = zip.GetEntry("_data.abjnt");
                using (var reader = data.Open()) {
                    protodata = (ablibInkCanvasCollectionSavingProtobufData2)model.Deserialize(reader, new ablibInkCanvasCollectionSavingProtobufData2(), typeof(ablibInkCanvasCollectionSavingProtobufData2));
                }
                AttachedFile.Open(zip, protodata.AttachedFiles);
            }
            catch (Exception e) { System.Diagnostics.Debug.WriteLine(e.Message); }
            // protobufデシリアライズ
            if (protodata == null) {
                try { protodata = (ablibInkCanvasCollectionSavingProtobufData2)model.Deserialize(fs, new ablibInkCanvasCollectionSavingProtobufData(), typeof(ablibInkCanvasCollectionSavingProtobufData2)); }
                catch (Exception e) { System.Diagnostics.Debug.WriteLine(e.Message); }
            }
            if (protodata != null) {
                Clear();
                Info = protodata.Info;
                foreach (var d in protodata.Data) {
                    var abstrokes = new List<abStroke>();
                    if (d.Data.Strokes != null) {
                        abstrokes.Capacity = d.Data.Strokes.Count;
                        foreach (var s in d.Data.Strokes) {
                            abstrokes.Add(new abStroke(s.StylusPoints, s.DrawingAttributes, s.DrawingAttributesPlus));
                        }
                    }
                    /*
                    if (d.Data.Texts != null) {
                        abinkdata.Texts.Capacity = d.Data.Texts.Count;
                        foreach (var t in d.Data.Texts) {
                            abinkdata.Texts.Add(t.ToTextData());
                        }
                    }*/
                    AddCanvas(abstrokes, new DrawingAttributes(), new DrawingAttributesPlus(), protodata.Info, d.Info);
                }
                return true;
            } else {
                return false;
            }
        }

        public void Open(string file) {
            var watch = new Stopwatch();
            using (var fs = new System.IO.FileStream(file, System.IO.FileMode.Open)) {
                if (LoadProtoBuf2(fs) || LoadProtoBuf(fs)) { }
            }
            if (Count == 0) AddCanvas();
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
        static readonly string pdfFontName = "游ゴシック";
        // 周りの円弧を除いた部分がtitleheightになる．
        static void GetYohakuHankei(double Width, double Height, out double xyohaku, out double yyohaku, out double titleheight, out double hankei) {
            xyohaku = Width * 0.03;
            yyohaku = Width * 0.03;
            titleheight = Height * 0.06;
            hankei = Width * 0.02;
        }
        static Dictionary<ABInkCanvas, Visual> noteContents = new Dictionary<ABInkCanvas, Visual>();
        public void DrawNoteContents(ABInkCanvas c) {
            DrawNoteContents(c, Info);
        }
        public static void DrawNoteContents(ABInkCanvas c, CanvasCollectionInfo info) {
            if (noteContents.ContainsKey(c)) {
                c.VisualChildren.Remove(noteContents[c]);
            }
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) {
                double xyohaku, yyohaku, height, hankei;
                GetYohakuHankei(c.Width, c.Height, out xyohaku, out yyohaku, out height, out hankei);
                if (info.ShowTitle) {
                    dc.DrawRoundedRectangle(null, new Pen(Brushes.LightGray, 1), new Rect(xyohaku, yyohaku, c.Width - 2 * xyohaku, height + 2 * hankei), hankei, hankei);
                    if (info.Title != null && info.Title != "") {
                        double textheight = height + 2 * hankei;
                        if (info.ShowDate) textheight -= 20;
                        double width = c.Width - 2 * xyohaku - 2 * hankei;
                        var pt = new Point(xyohaku + hankei, yyohaku);
                        double fontsize = GuessFontSize(info.Title, pdfFontName, width, textheight);
                        var text = new FormattedText(info.Title, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(pdfFontName), fontsize, Brushes.Black);
                        var textSize = GetStringSize(info.Title, pdfFontName, fontsize);
                        text.MaxTextWidth = width;
                        text.MaxTextHeight = textSize.Height;
                        int n = (int)(textSize.Width / width) + 1;
                        pt.Y += (textheight - n * text.MaxTextHeight) / 2;
                        text.MaxTextHeight *= n;
                        dc.DrawText(text, pt);
                    }

                    if (info.ShowDate) {
                        var text = new FormattedText(info.Date.ToLongDateString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(pdfFontName), 12, Brushes.Gray);
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
            c.VisualChildren.Add(visual);
            noteContents[c] = visual;
        }

        public void DrawRules(abJournalInkCanvas c, bool showTitle) {
            if (showTitle) {
                double xyohaku, yyohaku, height, hankei;
                GetYohakuHankei(c.Width, c.Height, out xyohaku, out yyohaku, out height, out hankei);
                var skip = new List<Rect>() { new Rect(xyohaku, yyohaku, c.Width - 2 * xyohaku, height + 2 * hankei) };
                var drawrect = new Rect(0, yyohaku + height + 2 * hankei, c.Width, c.Height - (yyohaku + height + 2 * hankei));
                c.DrawRule(skip, drawrect);
            } else {
                c.DrawRule();
            }
        }

        public static void DrawNoteContents(iText.Kernel.Pdf.PdfPage page, double pwidth, double pheight, CanvasCollectionInfo info, double scale) {
            double xyohaku, yyohaku, height, hankei;
            GetYohakuHankei(pwidth, pheight, out xyohaku, out yyohaku, out height, out hankei);
            var canvas = new iText.Kernel.Pdf.Canvas.PdfCanvas(page);
            if (info.ShowTitle) {
                canvas.SetStrokeColor(iText.Kernel.Colors.ColorConstants.LIGHT_GRAY);
                canvas.SetLineDash(0);
                canvas.RoundRectangle(scale * xyohaku, page.GetPageSize().GetHeight() - scale * (height + 2 * hankei + yyohaku), scale * (pwidth - 2 * xyohaku), scale * (height + 2 * hankei), scale * hankei);
                canvas.Stroke();
                double dateTextHeight = 0;
                Size datetextSize = new Size();
                if (info.ShowDate) {
                    datetextSize = GetStringSize(info.Date.ToLongDateString(), pdfFontName, 12);
                    dateTextHeight = datetextSize.Height + 8;
                }
                if (info.Title != null && info.Title != "") {
                    var rect = new iText.Kernel.Geom.Rectangle(
                        (float)(scale * (xyohaku + hankei)),
                        (float)(page.GetPageSize().GetHeight() - scale * (yyohaku + height + 2 * hankei - dateTextHeight)),
                        (float)(scale * (pwidth - xyohaku - hankei)),
                        (float)(page.GetPageSize().GetHeight() - scale * yyohaku));
                    //writer.DirectContent.SetColorStroke(iTextSharp.text.BaseColor.BLACK);
                    //writer.DirectContent.Rectangle(rect.Left,rect.Bottom,rect.Width,rect.Height);
                    //writer.DirectContent.Stroke();
                    double fontsize = GuessFontSize(info.Title, pdfFontName, rect.GetWidth() / scale, rect.GetHeight() / scale);
                    var font = iText.Kernel.Font.PdfFontFactory.CreateFont(pdfFontName, iText.IO.Font.PdfEncodings.IDENTITY_H, true);
                    canvas.SetFillColor(iText.Kernel.Colors.ColorConstants.BLACK);
                    /*
                    var column = new iTextSharp.text.pdf.ColumnText(writer.DirectContent);
                    column.Alignment = iTextSharp.text.pdf.PdfContentByte.ALIGN_LEFT;
                    column.AddText(new iTextSharp.text.Phrase(info.Title, font));
                    column.SetSimpleColumn(rect);
                    column.Go(true);
                    var y = column.YLine;*/
                }
            }
        }
        public static void DrawNoteContents(iTextSharp.text.pdf.PdfWriter writer, double pwidth, double pheight, CanvasCollectionInfo info, double scale) {
            double xyohaku, yyohaku, height, hankei;
            GetYohakuHankei(pwidth, pheight, out xyohaku, out yyohaku, out height, out hankei);
            if (info.ShowTitle) {
                writer.DirectContent.SetColorStroke(iTextSharp.text.BaseColor.LIGHT_GRAY);
                writer.DirectContent.SetLineDash(0);
                writer.DirectContent.RoundRectangle(scale * xyohaku, writer.PageSize.Height - scale * (height + 2 * hankei + yyohaku), scale * (pwidth - 2 * xyohaku), scale * (height + 2 * hankei), scale * hankei);
                writer.DirectContent.Stroke();
                double dateTextHeight = 0;
                Size datetextSize = new Size();
                if (info.ShowDate) {
                    datetextSize = GetStringSize(info.Date.ToLongDateString(), pdfFontName, 12);
                    dateTextHeight = datetextSize.Height + 8;
                }
                if (info.Title != null && info.Title != "") {
                    var rect = new iTextSharp.text.Rectangle(
                        (float)(scale * (xyohaku + hankei)),
                        (float)(writer.PageSize.Height - scale * (yyohaku + height + 2 * hankei - dateTextHeight)),
                        (float)(scale * (pwidth - xyohaku - hankei)),
                        (float)(writer.PageSize.Height - scale * yyohaku));
                    //writer.DirectContent.SetColorStroke(iTextSharp.text.BaseColor.BLACK);
                    //writer.DirectContent.Rectangle(rect.Left,rect.Bottom,rect.Width,rect.Height);
                    //writer.DirectContent.Stroke();
                    double fontsize = GuessFontSize(info.Title, pdfFontName, rect.Width / scale, rect.Height / scale);
                    var font = iTextSharp.text.FontFactory.GetFont(pdfFontName, iTextSharp.text.pdf.BaseFont.IDENTITY_H, iTextSharp.text.pdf.BaseFont.EMBEDDED, (float)(scale * fontsize));
                    writer.DirectContent.SetColorFill(iTextSharp.text.BaseColor.BLACK);
                    // 調整
                    var column = new iTextSharp.text.pdf.ColumnText(writer.DirectContent);
                    column.Alignment = iTextSharp.text.pdf.PdfContentByte.ALIGN_LEFT;
                    column.AddText(new iTextSharp.text.Phrase(info.Title, font));
                    column.SetSimpleColumn(rect);
                    column.Go(true);
                    var y = column.YLine;

                    rect = new iTextSharp.text.Rectangle(rect.Left, rect.Bottom, rect.Right, rect.Top - (y - rect.Bottom) / 2);
                    column = new iTextSharp.text.pdf.ColumnText(writer.DirectContent);
                    column.Alignment = iTextSharp.text.pdf.PdfContentByte.ALIGN_LEFT;
                    column.AddText(new iTextSharp.text.Phrase(info.Title, font));
                    column.SetSimpleColumn(rect);
                    column.Go(false);
                }
                if (info.ShowTitle) {
                    var font = iTextSharp.text.FontFactory.GetFont(pdfFontName, iTextSharp.text.pdf.BaseFont.IDENTITY_H, iTextSharp.text.pdf.BaseFont.EMBEDDED, (float)(scale * 12)).BaseFont;
                    if (font == null) throw new Exception("Font " + pdfFontName + " is not found");
                    writer.DirectContent.SetColorStroke(iTextSharp.text.BaseColor.LIGHT_GRAY);
                    writer.DirectContent.SetLineDash(new double[] { 3, 3 }, 0);
                    writer.DirectContent.MoveTo(scale * (xyohaku + hankei),
                        writer.PageSize.Height - scale * (yyohaku + 2 * hankei + height - datetextSize.Height - 8));
                    writer.DirectContent.LineTo(
                        scale * (pwidth - xyohaku - hankei),
                        writer.PageSize.Height - scale * (yyohaku + 2 * hankei + height - datetextSize.Height - 8));
                    writer.DirectContent.Stroke();
                    writer.DirectContent.SetLineDash(0);
                    writer.DirectContent.BeginText();
                    writer.DirectContent.SetFontAndSize(font, (float)(scale * 12));
                    writer.DirectContent.SetColorFill(iTextSharp.text.BaseColor.GRAY);
                    writer.DirectContent.ShowTextAligned(
                        iTextSharp.text.pdf.PdfContentByte.ALIGN_LEFT,
                        info.Date.ToLongDateString(),
                        (float)(scale * (pwidth - xyohaku - datetextSize.Width - hankei)),
                        writer.PageSize.Height - (float)(scale * (yyohaku + 2 * hankei + height - 8)), 0);
                    writer.DirectContent.EndText();
                }
            }
        }

        public static void DrawRules(iTextSharp.text.pdf.PdfWriter writer, double pwidth, double pheight, abJournalInkCanvas.Rule HorizontalRule, abJournalInkCanvas.Rule VerticalRule, bool showTitle, double scale) {
            double xyohaku, yyohaku, height, hankei;
            GetYohakuHankei(pwidth, pheight, out xyohaku, out yyohaku, out height, out hankei);
            if (HorizontalRule.Show) {
                writer.DirectContent.SetLineDash(HorizontalRule.DashArray.ToArray(), 0);
                writer.DirectContent.SetColorStroke(new iTextSharp.text.BaseColor(
                    HorizontalRule.Color.R,
                    HorizontalRule.Color.G,
                    HorizontalRule.Color.B,
                    HorizontalRule.Color.A));
                double d = HorizontalRule.Interval;
                if (showTitle && !HorizontalRule.Show) d += yyohaku + height + 2 * hankei;
                for (; d < pheight; d += HorizontalRule.Interval) {
                    if (showTitle && yyohaku < d && d < yyohaku + height) {
                        writer.DirectContent.MoveTo(0, writer.PageSize.Height - scale * d);
                        writer.DirectContent.LineTo(scale * xyohaku, writer.PageSize.Height - scale * d);
                        writer.DirectContent.Stroke();
                        writer.DirectContent.MoveTo(scale * (pwidth - xyohaku), writer.PageSize.Height - scale * d); ;
                        writer.DirectContent.LineTo(scale * pwidth, writer.PageSize.Height - scale * d);
                        writer.DirectContent.Stroke();
                    } else {
                        writer.DirectContent.MoveTo(0, writer.PageSize.Height - scale * d);
                        writer.DirectContent.LineTo(scale * pwidth, writer.PageSize.Height - scale * d);
                        writer.DirectContent.Stroke();
                    }
                }
                writer.DirectContent.SetLineDash(0);
            }
            if (VerticalRule.Show) {
                writer.DirectContent.SetColorStroke(new iTextSharp.text.BaseColor(
                    VerticalRule.Color.R,
                    VerticalRule.Color.G,
                    VerticalRule.Color.B,
                    VerticalRule.Color.A));
                writer.DirectContent.SetLineDash(VerticalRule.DashArray.ToArray(), 0);
                for (double d = VerticalRule.Interval; d < pwidth; d += VerticalRule.Interval) {
                    if (showTitle && xyohaku < d && d < pwidth - xyohaku) {
                        writer.DirectContent.MoveTo(scale * d, writer.PageSize.Height);
                        writer.DirectContent.LineTo(scale * d, writer.PageSize.Height - scale * yyohaku);
                        writer.DirectContent.Stroke();
                        writer.DirectContent.MoveTo(scale * d, writer.PageSize.Height - scale * (yyohaku + height + 2 * hankei));
                        writer.DirectContent.LineTo(scale * d, writer.PageSize.Height - scale * pheight);
                        writer.DirectContent.Stroke();
                    } else {
                        writer.DirectContent.MoveTo(scale * d, 0);
                        writer.DirectContent.LineTo(scale * d, writer.PageSize.Height - scale * pheight);
                        writer.DirectContent.Stroke();
                    }
                }
                writer.DirectContent.SetLineDash(0);
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

        // 推測が怪しい気がしてきたので，後で考え直す．
        public static double GuessFontSize(string str, string fontname, double width, double height) {
            var size = GetStringSize(str, fontname, 100);
            int n = (int)Math.Sqrt(height * size.Width / (width * size.Height));
            var f = 100 * Math.Max(n * width / size.Width, height / ((n + 1) * size.Height));
            while (f > 0) {
                size = GetStringSize(str, fontname, f);
                int k = (int)(size.Width / width) + 1;
                if (k * size.Height > height) --f;
                else return f;
            }
            return f + 1;
        }
        #endregion

        #region 印刷用
        public IEnumerable<abJournalInkCanvas> GetPrintingCanvases() {
            for (int i = 0; i < Count; ++i) {
                var r = this[i].GetPrintingCanvas();
                if (i == 0) DrawNoteContents(r);
                DrawRules(r, (i == 0 && Info.ShowTitle));
                yield return r;
            }
        }
        #endregion

        public void Dispose() {
            foreach (var c in this) {
                c.BackgroundData?.Dispose(c);
            }
        }
    }
}
