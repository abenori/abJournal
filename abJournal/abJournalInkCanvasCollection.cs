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
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

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
            if (index == 0) DrawNoteContents(canvas, info);
            DrawRules(canvas, inkcanvasinfo.HorizontalRule, inkcanvasinfo.VerticalRule, (index == 0) && Info.ShowTitle);
        }

        public void ReDraw() {
            for (int i = 0; i < Count; ++i) {
                var c = this[i];
                c.CleanUpBrush();
                c.SetBackgroundFromStr();
                if (i == 0) DrawNoteContents(c, Info);
                DrawRules(c, c.Info.HorizontalRule, c.Info.VerticalRule, (i == 0) && Info.ShowTitle);
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
        public void Import(string path) {
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
                case ".pdf":
                    {
                        int oldCount = Count;
                        BackgroundPDF.LoadFile(file, this);
                        for (int i = 0; i < Count - oldCount; ++i) {
                            this[i + oldCount].Info.BackgroundStr = "image:pdf:" + file.Identifier + ":page=" + i.ToString();
                        }
                        break;
                    }
                case ".xps":
                    {
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
                public CanvasData(abInkData d, abJournalInkCanvas.InkCanvasInfo i) {
                    Info = i.DeepCopy();
                    Data = new InkData(d);
                }
                [ProtoMember(1)]
                public abJournalInkCanvas.InkCanvasInfo Info;
                [ProtoContract(SkipConstructor = true)]
                public class InkData {
                    [ProtoContract(SkipConstructor = true)]
                    public class StrokeData {
                        [ProtoMember(1)]
                        public System.Windows.Input.StylusPointCollection StylusPoints { get; set; }
                        [ProtoMember(2)]
                        public System.Windows.Ink.DrawingAttributes DrawingAttributes { get; set; }
                        [ProtoMember(3)]
                        public DrawingAttributesPlus DrawingAttributesPlus { get; set; }
                        public StrokeData(abJournal.StrokeData stroke) {
                            StylusPoints = stroke.StylusPoints;
                            DrawingAttributes = stroke.DrawingAttributes;
                            DrawingAttributesPlus = stroke.DrawingAttributesPlus;
                        }
                        public abJournal.StrokeData ToStrokeData() {
                            return new abJournal.StrokeData(
                                //new System.Windows.Input.StylusPointCollection(),
                                StylusPoints,
                                //new System.Windows.Ink.DrawingAttributes(),
                                DrawingAttributes,
                                //new DrawingAttributesPlus(),
                                DrawingAttributesPlus,
                                abJournal.Properties.Settings.Default.DrawingAlgorithm
                            );
                        }
                    }
                    public InkData(abInkData d) {
                        Strokes = new List<StrokeData>();
                        foreach(var c in d.Strokes) { Strokes.Add(new StrokeData(c)); }
                        Texts = new List<TextData>();
                        Texts.Add(new TextData(new abJournal.TextData("あ")));
                    }
                    [ProtoMember(1)]
                    public List<StrokeData> Strokes { get; set; }
                    [ProtoContract(SkipConstructor=true)]
                    public class TextData {
                        [ProtoMember(1)]
                        public string Text { get; set; }
                        [ProtoMember(2)]
                        public Rect Rect { get; set; }
                        [ProtoMember(3)]
                        public string FontFamily { get; set; }
                        [ProtoContract(SkipConstructor=true)]
                        public class FontStyleData {
                            public enum Style { Normal = 1, Italic = 2, Oblique = 3 }
                            [ProtoMember(1)]
                            public Style style { get; set; }
                            public FontStyleData(FontStyle fs) {
                                if(fs == FontStyles.Italic) style = Style.Italic;
                                else if(fs == FontStyles.Oblique) style = Style.Oblique;
                                else style = Style.Normal;
                            }
                            public FontStyle ToFontStyle() {
                                switch(style) {
                                    case Style.Italic: return FontStyles.Italic;
                                    case Style.Oblique: return FontStyles.Oblique;
                                    default: return FontStyles.Normal;
                                }
                            }
                        }
                        [ProtoMember(4)]
                        FontStyleData FontStyle { get; set; }
                        [ProtoContract(SkipConstructor = true)]
                        public class FontWeightData {
                            public enum Weight { Thin = 1, ExtraLight = 2, UltraLight = 3, Light = 4, Normal = 5, Regular = 6, Medium = 7, DemiBold = 8, SemiBold = 9, Bold = 10, ExtraBold = 11, UltraBold = 12, Black = 13, Heavy = 14, ExtraBlack = 15, UltraBlack = 16 };
                            public FontWeightData(FontWeight fw) {
                                if(fw == FontWeights.Thin) weight = Weight.Thin;
                                else if(fw == FontWeights.ExtraLight) weight = Weight.ExtraLight;
                                else if(fw == FontWeights.UltraLight) weight = Weight.UltraLight;
                                else if(fw == FontWeights.Light) weight = Weight.Light;
                                else if(fw == FontWeights.Regular) weight = Weight.Regular;
                                else if(fw == FontWeights.Medium) weight = Weight.Medium;
                                else if(fw == FontWeights.DemiBold) weight = Weight.DemiBold;
                                else if(fw == FontWeights.SemiBold) weight = Weight.SemiBold;
                                else if(fw == FontWeights.Bold) weight = Weight.Bold;
                                else if(fw == FontWeights.ExtraBold) weight = Weight.ExtraBold;
                                else if(fw == FontWeights.UltraBold) weight = Weight.UltraBold;
                                else if(fw == FontWeights.Black) weight = Weight.Black;
                                else if(fw == FontWeights.Heavy) weight = Weight.Heavy;
                                else if(fw == FontWeights.ExtraBlack) weight = Weight.ExtraBlack;
                                else if(fw == FontWeights.UltraBlack) weight = Weight.UltraBlack;
                                else weight = Weight.Normal;
                            }
                            public FontWeight ToFontWeight() {
                                switch(weight) {
                                    case Weight.Thin: return FontWeights.Thin;
                                    case Weight.ExtraLight: return FontWeights.ExtraLight;
                                    case Weight.UltraLight: return FontWeights.UltraLight;
                                    case Weight.Light: return FontWeights.Light;
                                    case Weight.Regular: return FontWeights.Regular;
                                    case Weight.Medium: return FontWeights.Medium;
                                    case Weight.DemiBold: return FontWeights.DemiBold;
                                    case Weight.SemiBold: return FontWeights.SemiBold;
                                    case Weight.Bold: return FontWeights.Bold;
                                    case Weight.ExtraBold: return FontWeights.ExtraBold;
                                    case Weight.UltraBold: return FontWeights.UltraBold;
                                    case Weight.Black: return FontWeights.Black;
                                    case Weight.Heavy: return FontWeights.Heavy;
                                    case Weight.ExtraBlack: return FontWeights.ExtraBlack;
                                    case Weight.UltraBlack: return FontWeights.UltraBlack;
                                    default: return FontWeights.Normal;
                                }
                            }
                            public Weight weight;
                        }
                        [ProtoMember(5)]
                        FontWeightData FontWeight { get; set; }
                        [ProtoMember(6)]
                        double FontSize { get; set; }
                        [ProtoMember(7)]
                        public Color Color { get; set; }
                        public TextData(abJournal.TextData td) {
                            Text = td.Text; Rect = td.Rect;
                            FontFamily = td.FontFamily.FamilyNames[System.Windows.Markup.XmlLanguage.GetLanguage(System.Globalization.CultureInfo.CurrentCulture.ToString())];
                            FontStyle = new FontStyleData(td.FontStyle); FontWeight = new FontWeightData(td.FontWeight);
                            FontSize = td.FontSize; Color = td.Color;
                        }
                        public abJournal.TextData ToTextData() {
                            return new abJournal.TextData(
                                Text, Rect, new System.Windows.Media.FontFamily(FontFamily), FontSize,
                                FontStyle.ToFontStyle(), FontWeight.ToFontWeight(), Color
                                );
                        }
                    }
                    [ProtoMember(2)]
                    public List<TextData> Texts { get; set; }
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
        #endregion

        #region 保存
        public static string GetSchema() {
            //return abInkData.SetProtoBufTypeModel(ProtoBuf.Meta.TypeModel.Create()).GetSchema(typeof(ablibInkCanvasCollectionSavingProtobufData));
            //return abInkData.SetProtoBufTypeModel(ProtoBuf.Meta.RuntimeTypeModel.Create()).GetSchema(typeof(ablibInkCanvasCollectionSavingProtobufData));
            return abInkData.SetProtoBufTypeModel(ProtoBuf.Meta.RuntimeTypeModel.Create()).GetSchema(typeof(ablibInkCanvasCollectionSavingProtobufData2));
        }
        public void Save() {
            Save(FileName);
        }
        public void Save(string file) {
            /*
            ablibInkCanvasCollectionSavingProtobufData data = new ablibInkCanvasCollectionSavingProtobufData();
            foreach (var c in this) {
                data.Data.Add(new ablibInkCanvasCollectionSavingProtobufData.CanvasData(c.InkData, c.Info));
            }
            */
            var data = new ablibInkCanvasCollectionSavingProtobufData2();
            data.Data.Capacity = this.Count;
            foreach(var c in this) {
                data.Data.Add(new ablibInkCanvasCollectionSavingProtobufData2.CanvasData(c.InkData, c.Info));
            }
            data.Info = Info;
            var task = SaveProcAsync(data, file);
        }
        private async System.Threading.Tasks.Task SaveProcAsync(ablibInkCanvasCollectionSavingProtobufData2 data, string file) {

            string tmpFile = null;
            if(File.Exists(file)) {
                tmpFile = Path.GetTempFileName();
                File.Delete(tmpFile);
                File.Move(file, tmpFile);
            }
            try {
                var model = abInkData.SetProtoBufTypeModel2(ProtoBuf.Meta.RuntimeTypeModel.Create());
                using(var zip = ZipFile.Open(file, ZipArchiveMode.Create)) {
                    data.AttachedFiles = AttachedFile.Save(zip);
                    var mainEntry = zip.CreateEntry("_data.abjnt");
                    using(var ws = mainEntry.Open()) {
                        await System.Threading.Tasks.Task.Run(() => model.Serialize(ws, data));
                    }
                }
            }
            catch(Exception e) {
                System.Diagnostics.Debug.WriteLine(e.Message);
                File.Delete(file);
                if(tmpFile != null) File.Move(tmpFile, file);
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
            double scale = (double)720 / (double)254 / Paper.mmToSize;
            var documents = new Dictionary<string, iTextSharp.text.pdf.PdfReader>();
            FileStream fw = null;
            iTextSharp.text.Document doc = null;
            iTextSharp.text.pdf.PdfWriter writer = null;
            try {
                fw = new FileStream(file, FileMode.Create, FileAccess.Write);
                for (int i = 0; i < Count; ++i) {
                    var ps = Paper.GetPaperSize(new Size(this[i].Width, this[i].Height));
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
                        pagesize = new iTextSharp.text.Rectangle((float)(this[i].Width * scale), (float)(this[i].Height * scale));
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
                    if (this[i].Info.BackgroundStr.StartsWith("image:pdf:")) {
                        var str = this[i].Info.BackgroundStr.Substring("image:pdf:".Length);
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
                    if (i == 0) DrawNoteContents(writer, this[i], Info, scale);
                    DrawRules(writer, this[i], this[i].Info.HorizontalRule, this[i].Info.VerticalRule, (i == 0 && Info.ShowTitle),scale);
                    this[i].InkData.AddPdfGarphic(writer, scale);
                    if (Properties.Settings.Default.SaveTextToPDF) this[i].InkData.AddTextToPDFGraphic(writer, scale);
                    if (i != Count - 1) doc.NewPage();
                }
                doc.AddCreator("abJournal");
                doc.AddTitle(Info.Title);
                writer.Info.Put(new iTextSharp.text.pdf.PdfName("CreationDate"), new iTextSharp.text.pdf.PdfDate(Info.Date));
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
        #endregion

        #region 読み込み
        private bool LoadProtoBuf(System.IO.FileStream fs) {
            fs.Seek(0,SeekOrigin.Begin);
            var model = abInkData.SetProtoBufTypeModel(ProtoBuf.Meta.RuntimeTypeModel.Create());
            ablibInkCanvasCollectionSavingProtobufData protodata = null;
            try {
                var zip = new ZipArchive(fs);
                var data = zip.GetEntry("_data.abjnt");
                using(var reader = data.Open()) {
                    protodata = (ablibInkCanvasCollectionSavingProtobufData)model.Deserialize(reader, new ablibInkCanvasCollectionSavingProtobufData(), typeof(ablibInkCanvasCollectionSavingProtobufData));
                }
                AttachedFile.Open(zip, protodata.AttachedFiles);
            }
            catch(Exception e) { System.Diagnostics.Debug.WriteLine(e.Message); }
            // protobufデシリアライズ
            if(protodata == null) {
                try { protodata = (ablibInkCanvasCollectionSavingProtobufData)model.Deserialize(fs, new ablibInkCanvasCollectionSavingProtobufData(), typeof(ablibInkCanvasCollectionSavingProtobufData)); }
                catch(Exception e) { System.Diagnostics.Debug.WriteLine(e.Message); }
            }
            if(protodata != null) {
                Clear();
                foreach(var d in protodata.Data) {
                    AddCanvas(d.Data, protodata.Info, d.Info);
                }
                Info = protodata.Info;
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
                using(var reader = data.Open()) {
                    protodata = (ablibInkCanvasCollectionSavingProtobufData2)model.Deserialize(reader, new ablibInkCanvasCollectionSavingProtobufData2(), typeof(ablibInkCanvasCollectionSavingProtobufData2));
                }
                AttachedFile.Open(zip, protodata.AttachedFiles);
            }
            catch(Exception e) { System.Diagnostics.Debug.WriteLine(e.Message); }
            // protobufデシリアライズ
            if(protodata == null) {
                try { protodata = (ablibInkCanvasCollectionSavingProtobufData2)model.Deserialize(fs, new ablibInkCanvasCollectionSavingProtobufData(), typeof(ablibInkCanvasCollectionSavingProtobufData2)); }
                catch(Exception e) { System.Diagnostics.Debug.WriteLine(e.Message); }
            }
            if(protodata != null) {
                Clear();
                foreach(var d in protodata.Data) {
                    var abinkdata = new abInkData();
                    if(d.Data.Strokes != null) {
                        abinkdata.Strokes.Capacity = d.Data.Strokes.Count;
                        foreach(var s in d.Data.Strokes) {
                            abinkdata.Strokes.Add(s.ToStrokeData());
                        }
                    }
                    if(d.Data.Texts != null) {
                        abinkdata.Texts.Capacity = d.Data.Texts.Count;
                        foreach(var t in d.Data.Texts) {
                            abinkdata.Texts.Add(t.ToTextData());
                        }
                    }
                    AddCanvas(abinkdata, protodata.Info, d.Info);
                }
                Info = protodata.Info;
                return true;
            } else {
                return false;
            }
        }


        private bool LoadXML(System.IO.FileStream fs) {
            var xml = new System.Xml.Serialization.XmlSerializer(typeof(ablibInkCanvasCollectionSavingData));
            ablibInkCanvasCollectionSavingData data = null;
            // gzip解凍+XMLデシリアライズ
            using(var zs = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress)) {
                try { data = (ablibInkCanvasCollectionSavingData)xml.Deserialize(zs); }
                catch(System.IO.InvalidDataException) { }

            }
            // 単なるXMLデシリアライズ
            if(data == null) {
                data = (ablibInkCanvasCollectionSavingData)xml.Deserialize(fs);
            }

            if(data != null) {
                Clear();
                foreach(var d in data.Data) {
                    abInkData id = new abInkData();
                    id.LoadSavingData(d.Data);
                    AddCanvas(id, data.Info, d.Info);
                }
                Info = data.Info;
                return true;
            } else return false;
        }


        public void Open(string file) {
            var watch = new Stopwatch();
            using(var fs = new System.IO.FileStream(file, System.IO.FileMode.Open)) {
                if(LoadProtoBuf2(fs) || LoadProtoBuf(fs) || LoadXML(fs)) { }
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
        static readonly string pdfFontName = "游ゴシック";
        // 周りの円弧を除いた部分がtitleheightになる．
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
            if (noteContents.ContainsKey(c)) {
                c.Children.Remove(noteContents[c]);
            }
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) {
                double xyohaku, yyohaku, height, hankei;
                GetYohakuHankei(c, out xyohaku, out yyohaku, out height, out hankei);
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
            c.Children.Add(visual);
            noteContents[c] = visual;
        }
        static Dictionary<abInkCanvas, Visual> rules = new Dictionary<abInkCanvas, Visual>();

        public void DrawRules(abJournalInkCanvas c, bool showTitle) {
            DrawRules(c, c.Info.HorizontalRule, c.Info.VerticalRule, showTitle);
        }
        public static void DrawRules(abJournalInkCanvas c, abJournalInkCanvas.Rule Horizontal, abJournalInkCanvas.Rule Vertical, bool showTitle) {
            if (rules.ContainsKey(c)) {
                c.Children.Remove(rules[c]);
            }
            var visual = new DrawingVisual();
            using (var dc = visual.RenderOpen()) {
                double xyohaku, yyohaku, height, hankei;
                GetYohakuHankei(c, out xyohaku, out yyohaku, out height, out hankei);
                if (Horizontal.Show) {
                    double d = Horizontal.Interval;
                    if (showTitle && !Horizontal.Show) d += yyohaku + height + 2 * hankei;
                    var brush = new SolidColorBrush(Horizontal.Color);
                    brush.Freeze();
                    var pen = new Pen(brush, Horizontal.Thickness);
                    pen.DashStyle = new DashStyle(Horizontal.DashArray, 0);
                    pen.DashCap = PenLineCap.Flat;
                    pen.Freeze();
                    for (; d < c.Height; d += Horizontal.Interval) {
                        if (showTitle && yyohaku < d && d < yyohaku + height) {
                            dc.DrawLine(pen, new Point(0, d), new Point(xyohaku, d));
                            dc.DrawLine(pen, new Point(c.Width - xyohaku, d), new Point(c.Width, d));
                        } else {
                            dc.DrawLine(pen, new Point(0, d), new Point(c.Width, d));
                        }
                    }
                }
                if (Vertical.Show) {
                    var brush = new SolidColorBrush(Vertical.Color);
                    brush.Freeze();
                    var pen = new Pen(brush, Vertical.Thickness);
                    pen.DashStyle = new DashStyle(Vertical.DashArray, 0);
                    pen.DashCap = PenLineCap.Flat;
                    pen.Freeze();
                    for (double d = Vertical.Interval; d < c.Width; d += Vertical.Interval) {
                        if (showTitle && xyohaku < d && d < c.Width - xyohaku) {
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

        public static void DrawNoteContents(iTextSharp.text.pdf.PdfWriter writer, abJournalInkCanvas c, CanvasCollectionInfo info, double scale) {
            double xyohaku, yyohaku, height, hankei;
            GetYohakuHankei(c, out xyohaku, out yyohaku, out height, out hankei);
            if (info.ShowTitle) {
                writer.DirectContent.SetColorStroke(iTextSharp.text.BaseColor.LIGHT_GRAY);
                writer.DirectContent.SetLineDash(0);
                writer.DirectContent.RoundRectangle(scale * xyohaku, writer.PageSize.Height - scale * (height + 2 * hankei + yyohaku), scale * (c.Width - 2 * xyohaku), scale * (height + 2 * hankei), scale * hankei);
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
                        (float)(writer.PageSize.Height - scale * (yyohaku + height + 2*hankei - dateTextHeight)),
                        (float)(scale * (c.Width - xyohaku - hankei)),
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
                        scale * (c.Width - xyohaku - hankei),
                        writer.PageSize.Height - scale * (yyohaku + 2 * hankei + height - datetextSize.Height - 8));
                    writer.DirectContent.Stroke();
                    writer.DirectContent.SetLineDash(0);
                    writer.DirectContent.BeginText();
                    writer.DirectContent.SetFontAndSize(font, (float)(scale * 12));
                    writer.DirectContent.SetColorFill(iTextSharp.text.BaseColor.GRAY);
                    writer.DirectContent.ShowTextAligned(
                        iTextSharp.text.pdf.PdfContentByte.ALIGN_LEFT,
                        info.Date.ToLongDateString(),
                        (float)(scale * (c.Width - xyohaku - datetextSize.Width - hankei)),
                        writer.PageSize.Height - (float)(scale * (yyohaku + 2 * hankei + height - 8)), 0);
                    writer.DirectContent.EndText();
                }
            }
       }

        public static void DrawRules(iTextSharp.text.pdf.PdfWriter writer, abJournalInkCanvas c, abJournalInkCanvas.Rule HorizontalRule, abJournalInkCanvas.Rule VerticalRule, bool showTitle, double scale) {
            double xyohaku, yyohaku, height, hankei;
            GetYohakuHankei(c, out xyohaku, out yyohaku, out height, out hankei);
            if (HorizontalRule.Show) {
                writer.DirectContent.SetLineDash(HorizontalRule.DashArray.ToArray(), 0);
                writer.DirectContent.SetColorStroke(new iTextSharp.text.BaseColor(
                    HorizontalRule.Color.R,
                    HorizontalRule.Color.G,
                    HorizontalRule.Color.B,
                    HorizontalRule.Color.A));
                double d = HorizontalRule.Interval;
                if (showTitle && !HorizontalRule.Show) d += yyohaku + height + 2 * hankei;
                for (; d < c.Height; d += HorizontalRule.Interval) {
                    if (showTitle && yyohaku < d && d < yyohaku + height) {
                        writer.DirectContent.MoveTo(0, writer.PageSize.Height - scale * d);
                        writer.DirectContent.LineTo(scale * xyohaku, writer.PageSize.Height - scale * d);
                        writer.DirectContent.Stroke();
                        writer.DirectContent.MoveTo(scale * (c.Width - xyohaku), writer.PageSize.Height - scale * d); ;
                        writer.DirectContent.LineTo(scale * c.Width, writer.PageSize.Height - scale * d);
                        writer.DirectContent.Stroke();
                    } else {
                        writer.DirectContent.MoveTo(0, writer.PageSize.Height - scale * d);
                        writer.DirectContent.LineTo(scale * c.Width, writer.PageSize.Height - scale * d);
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
                for (double d = VerticalRule.Interval; d < c.Width; d += VerticalRule.Interval) {
                    if (showTitle && xyohaku < d && d < c.Width - xyohaku) {
                        writer.DirectContent.MoveTo(scale * d, writer.PageSize.Height);
                        writer.DirectContent.LineTo(scale * d, writer.PageSize.Height - scale * yyohaku);
                        writer.DirectContent.Stroke();
                        writer.DirectContent.MoveTo(scale * d, writer.PageSize.Height - scale * (yyohaku + height + 2 * hankei));
                        writer.DirectContent.LineTo(scale * d, writer.PageSize.Height - scale * c.Height);
                        writer.DirectContent.Stroke();
                    } else {
                        writer.DirectContent.MoveTo(scale * d, 0);
                        writer.DirectContent.LineTo(scale * d, writer.PageSize.Height - scale * c.Height);
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
            int n = (int) Math.Sqrt(height * size.Width / (width * size.Height));
            var f = 100 * Math.Max(n * width / size.Width, height / ((n + 1) * size.Height));
            while(f > 0) {
                size = GetStringSize(str, fontname, f);
                int k = (int) (size.Width/width)+1;
                if(k * size.Height > height) --f;
                else return f;
            }
            return f + 1;
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

        public void Dispose() {
            foreach(var c in this) {
                c.BackgroundData?.Dispose(c);
            }
        }
    }
}
