using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Media;
using System.Windows.Xps;
using System.Windows.Documents;
using System.Windows.Xps.Packaging;

namespace abJournal {
    public class BackgroundImageManager {
        #region PDF
        public class PDFPage : IDisposable{
            public AttachedFile File { get; private set; }
            public int PageNum { get; private set; }
            public PDFPage(AttachedFile file, int pageNum) {
                var doc = GetDocument(file);
                File = new AttachedFile(file);
                PageNum = pageNum;
            }

            public static int GetPageCount(AttachedFile file){
                var doc = GetDocument(file);
                return doc.GetPageCount();
            }
            static pdfium.PDFDocument GetDocument(AttachedFile file){
                if(PDFDOcuments.ContainsKey(file.FileName)) return PDFDOcuments[file.FileName];
                else {
                    var doc = new pdfium.PDFDocument(file.FileName);
                    PDFDOcuments[file.FileName] = doc;
                    return doc;
                }
            }
            public void Dispose() {
                File.Dispose();
            }
            public pdfium.PDFPage GetPage() {
                return GetDocument(File).GetPage(PageNum);
            }

            public class Finalizer : IDisposable{
                public void Dispose() {
                    foreach(var d in PDFDOcuments) {
                        d.Value.Dispose();
                    }
                }
            }
            static Dictionary<string, pdfium.PDFDocument> PDFDOcuments = new Dictionary<string, pdfium.PDFDocument>();
        }

        public static class PDFBackground {
            static double scale = 1;
            public static void SetBackground_IgnoreViewport(abJournalInkCanvas c, AttachedFile file, int pageNum) {
                using(var page = new PDFPage(file, pageNum))
                using(var pdfpage = page.GetPage()) {
                    var brush = new VisualBrush(pdfpage.GetVisual(new Rect(0, 0, c.Width, c.Height), scale,c.Info.BackgroundColor));
                    c.Background = brush;
                }
            }

            public static void SetBackground(abJournalInkCanvas c, AttachedFile file, int pageNum) {
                var page = new PDFPage(file, pageNum);
                SetBackground(c, page);
            }
            public static void SetBackground(abJournalInkCanvas c, PDFPage page) {
                BackgroundPDFPage[c] = page;
                c.ViewportChanged += Setviewport;
                if(c.Viewport.Height != 0) {
                    using(var pdfpage = page.GetPage()) {
                        var brush = new VisualBrush(pdfpage.GetVisual(new Rect(0, 0, c.Width, c.Height), scale,c.Info.BackgroundColor));
                        c.Background = brush;
                    }
                } else c.Background = null;
            }
            public static void DisposeBackground(abJournalInkCanvas c){
                c.Background = null;
                var page = BackgroundPDFPage[c];
                page.Dispose();
                BackgroundPDFPage.Remove(c);
                c.ViewportChanged -= Setviewport;
            }

            static void Setviewport(object sender, abInkCanvas.ViewportChangedEventArgs e) {                
                //System.Diagnostics.Debug.WriteLine("page = " + BackgroundPDFPage[(abJournalInkCanvas) sender].PageNum.ToString() + ", old = " + e.OldViewport.ToString() + ", new = " + e.NewViewport.ToString());
                if(e.OldViewport.Height == 0) {
                    if(e.NewViewport.Height != 0) {
                        var canvas = (abJournalInkCanvas) sender;
                        using(var pdfpage = BackgroundPDFPage[canvas].GetPage()){
                            var brush = new VisualBrush(pdfpage.GetVisual(new Rect(0, 0, canvas.Width, canvas.Height), scale, canvas.Info.BackgroundColor));
                            canvas.Background = brush;
                        }
                    }
                }else{
                    if(e.NewViewport.Height == 0){
                        var canvas = (abJournalInkCanvas) sender;
                        canvas.Background = null;
                    }
                }
            }
            public static void ScaleChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
                if(e.PropertyName == "Scale") {
                    //System.Diagnostics.Debug.WriteLine("ScaleChanged");
                    var newScale = ((abJournalInkCanvasCollection) sender).Scale;
                    if(scale != newScale) {
                        scale = newScale;
                        foreach(var c in BackgroundPDFPage) {
                            if(c.Key.Background != null) {
                                using(var pdfpage = c.Value.GetPage()) {
                                    var brush = new VisualBrush(pdfpage.GetVisual(new Rect(0, 0, c.Key.Width, c.Key.Height), scale, c.Key.Info.BackgroundColor));
                                    c.Key.Background = brush;
                                }
                                //System.Diagnostics.Debug.WriteLine("Background changed because scale is changed");
                            }
                        }
                    }
                }
            }

            public class Finalizer : IDisposable {
                public void Dispose() {
                    foreach(var d in BackgroundPDFPage) {
                        d.Value.Dispose();
                    }
                }
            }

            static Dictionary<abJournalInkCanvas, PDFPage> BackgroundPDFPage = new Dictionary<abJournalInkCanvas, PDFPage>();
        }

        public static void LoadPDFFile(AttachedFile file, abJournalInkCanvasCollection collection){
            int pageCount = PDFPage.GetPageCount(file);
            double scale = (double) 254 * Paper.mmToSize / (double) 720;
            //pageCount = 2;
            for(int i = 0 ; i < pageCount ; ++i) {
                collection.AddCanvas();
                var c = collection[collection.Count - 1];
                var page = new PDFPage(file, i);
                using(var pdfpage = page.GetPage()) {
                    var size = new Size(pdfpage.Size.Width * scale, pdfpage.Size.Height * scale);
                    var ps = Paper.GetPaperSize(size);
                    if(ps != Paper.PaperSize.Other) size = Paper.GetSize(ps);
                    c.Width = size.Width;
                    c.Height = size.Height;
                }
                PDFBackground.SetBackground(c, page);
            }
        }
        #endregion

        #region XPS
        public class XPSPage : IDisposable {
            public AttachedFile File { get; private set; }
            public int PageNum { get; private set; }
            public XPSPage(AttachedFile file, int pageNum) {
                var pages = GetPaginator(file);
                File = new AttachedFile(file);
                PageNum = pageNum;
            }
            public static int GetPageCount(AttachedFile file) {
                return GetPaginator(file).PageCount;
            }
            static DocumentPaginator GetPaginator(AttachedFile file){
                if(XPSDocument.ContainsKey(file.FileName))return XPSDocument[file.FileName].GetFixedDocumentSequence().DocumentPaginator;
                else{
                    var doc = new XpsDocument(file.FileName,System.IO.FileAccess.Read);
                    XPSDocument[file.FileName] = doc;
                    return doc.GetFixedDocumentSequence().DocumentPaginator;
                }
            }
            public void Dispose() {
                File.Dispose();
            }
            public DocumentPage GetPage() {
                return GetPaginator(File).GetPage(PageNum);
            }
            public class Finalizer : IDisposable {
                public void Dispose() {
                    foreach(var d in XPSDocument) {
                        d.Value.Close();
                    }
                }
            }

            static Dictionary<string, XpsDocument> XPSDocument = new Dictionary<string, XpsDocument>();
        }

        public static class XPSBackground {
            public static void SetBackground_IgnoreViewport(abJournalInkCanvas c, AttachedFile file, int pageNum) {
                using(var page = new XPSPage(file, pageNum))
                using(var pagedoc = page.GetPage()) {
                    c.Background = new VisualBrush(pagedoc.Visual);
                }
            }

            public static void SetBackground(abJournalInkCanvas c, AttachedFile file, int pageNum) {
                var page = new XPSPage(file, pageNum);
                SetBackground(c, page);
            }
            public static void SetBackground(abJournalInkCanvas c, XPSPage page) {
                BackgroundXPSPage[c] = page;
                c.ViewportChanged += Setviewport;
                if(c.Viewport.Height != 0) {
                    using(var pagedoc = page.GetPage()) {
                        c.Background = new VisualBrush(pagedoc.Visual);
                    }
                }
            }
            public static void DisposeBackground(abJournalInkCanvas c) {
                c.Background = null;
                var page = BackgroundXPSPage[c];
                page.Dispose();
                BackgroundXPSPage.Remove(c);
                c.ViewportChanged -= Setviewport;
            }

            static void Setviewport(object sender, abInkCanvas.ViewportChangedEventArgs e) {
                if(e.OldViewport.Height == 0) {
                    if(e.NewViewport.Height != 0) {
                        var canvas = (abJournalInkCanvas) sender;
                        using(var pagedoc = BackgroundXPSPage[canvas].GetPage()) {
                            var brush = new VisualBrush(pagedoc.Visual);
                            canvas.Background = brush;
                        }
                    }
                } else {
                    if(e.NewViewport.Height == 0) {
                        var canvas = (abJournalInkCanvas) sender;
                        canvas.Background = null;
                    }
                }
            }
            public class Finalizer : IDisposable {
                public void Dispose() {
                    foreach(var d in BackgroundXPSPage) {
                        d.Value.Dispose();
                    }
                }
            }
            static Dictionary<abJournalInkCanvas, XPSPage> BackgroundXPSPage = new Dictionary<abJournalInkCanvas, XPSPage>();
        }

        public static void LoadXPSFile(AttachedFile file, abJournalInkCanvasCollection collection){
            int pageCount = XPSPage.GetPageCount(file);
            double scale = (double) 25.4 / (double) 96 * Paper.mmToSize;
            //pageCount = 2;
            for(int i = 0 ; i < pageCount ; ++i) {
                collection.AddCanvas();
                var c = collection[collection.Count - 1];
                var page = new XPSPage(file, i);
                using(var pagedoc = page.GetPage()) {
                    var size = new Size(pagedoc.Size.Width * scale, pagedoc.Size.Height * scale);
                    var ps = Paper.GetPaperSize(size);
                    if(ps != Paper.PaperSize.Other) size = Paper.GetSize(ps);
                    c.Width = size.Width;
                    c.Height = size.Height;
                    XPSBackground.SetBackground(c, page);
                }
            }
        }
        #endregion
    }
}
