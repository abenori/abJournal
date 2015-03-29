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
    public partial class abJournalInkCanvas {
        public BackgroundImage.BackgroundData BackgroundData = null;
        protected override void OnViewportChanged(abInkCanvas.ViewportChangedEventArgs e) {
            if(BackgroundData != null) BackgroundData.SetViewport(this, e);
            base.OnViewportChanged(e);
        }
    }

    namespace BackgroundImage {
        public interface BackgroundData {
            void Dispose(abJournalInkCanvas c);
            void SetViewport(abJournalInkCanvas c,abInkCanvas.ViewportChangedEventArgs e);
        }
    }

    public class BackgroundColor : BackgroundImage.BackgroundData {
        public void Dispose(abJournalInkCanvas c) { c.Background = null; }
        public void SetViewport(abJournalInkCanvas c, abJournalInkCanvas.ViewportChangedEventArgs e) { }
        public static void SetBackground(abJournalInkCanvas c, Color color) {
            if(c.BackgroundData != null) c.BackgroundData.Dispose(c);
            c.BackgroundData = new BackgroundColor();
            c.Background = new SolidColorBrush(color);
            c.Background.Freeze();
        }
    }

    #region PDF
    public class BackgroundPDF : BackgroundImage.BackgroundData {
        AttachedFile File = null;
        int PageNum = 0;
        BackgroundPDF(AttachedFile file, int pageNum) {
            File = new AttachedFile(file);
            PageNum = pageNum;
        }

        static int GetPageCount(AttachedFile file) {
            return GetDocument(file).GetPageCount();
        }
        static pdfium.PDFDocument GetDocument(AttachedFile file) {
            if(PDFDOcuments.ContainsKey(file.FileName)) return PDFDOcuments[file.FileName];
            else {
                var doc = new pdfium.PDFDocument(file.FileName);
                PDFDOcuments[file.FileName] = doc;
                return doc;
            }
        }

        pdfium.PDFPage GetPage() {
            return GetDocument(File).GetPage(PageNum);
        }

        static double scale = 1;

        public static void ScaleChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e) {
            if(e.PropertyName == "Scale") {
                //System.Diagnostics.Debug.WriteLine("ScaleChanged");
                var collection = (abJournalInkCanvasCollection) sender;
                var newScale = collection.Scale;
                if(scale != newScale) {
                    scale = newScale;
                    foreach(var c in collection) {
                        if(c.Background != null) {
                            if(c.BackgroundData is BackgroundPDF) {
                                ((BackgroundPDF) c.BackgroundData).SetBackgroundImage(c);
                            }
                        }
                    }
                }
            }
        }

        static void SetBackground(abJournalInkCanvas c, BackgroundPDF page) {
            if(c.BackgroundData != null) c.BackgroundData.Dispose(c);
            if(c.Viewport.Height != 0) {
                page.SetBackgroundImage(c);
            } else c.Background = null;
            c.BackgroundData = page;
        }

        async void SetBackgroundImage(abJournalInkCanvas c) {
            using(var pdfpage = GetPage()) {
                double width = c.Width, height = c.Height;
                var backcolor = c.Info.BackgroundColor;
                var bitmap = await System.Threading.Tasks.Task.Run(() => {
                    var b = pdfpage.GetBitmapSource(new Rect(0, 0, width, height), scale, backcolor);
                    b.Freeze();
                    return b;
                });
                var visual = new DrawingVisual();
                using(var dc = visual.RenderOpen()) { dc.DrawImage(bitmap, new Rect(0, 0, width, height)); }
                c.Background = new VisualBrush(visual);
            }
        }

        public void Dispose(abJournalInkCanvas c) {
            File.Dispose();
            c.Background = null;
            c.BackgroundData = null;
        }

        public void SetViewport(abJournalInkCanvas canvas, abInkCanvas.ViewportChangedEventArgs e) {
            if(e.OldViewport.Height == 0) {
                if(e.NewViewport.Height != 0) {
                    canvas.Background = new SolidColorBrush(canvas.Info.BackgroundColor);
                    SetBackgroundImage(canvas);
                }
            } else {
                if(e.NewViewport.Height == 0) {
                    canvas.Background = null;
                }
            }
        }

        public static void SetBackground(abJournalInkCanvas c, AttachedFile file, int pageNum) {
            var page = new BackgroundPDF(file, pageNum);
            SetBackground(c, page);
        }

        public static void SetBackground_IgnoreViewport(abJournalInkCanvas c, AttachedFile file, int pageNum) {
            BackgroundPDF page = null;
            try {
                page = new BackgroundPDF(file, pageNum);
                using(var pdfpage = page.GetPage()) {
                    var brush = new VisualBrush(pdfpage.GetVisual(new Rect(0, 0, c.Width, c.Height), scale, c.Info.BackgroundColor));
                    c.Background = brush;
                }
            }
            finally {
                if(page != null) page.File.Dispose();
            }
        }
        public static void LoadFile(AttachedFile file, abJournalInkCanvasCollection collection) {
            int pageCount = BackgroundPDF.GetPageCount(file);
            double scale = (double) 254 * Paper.mmToSize / (double) 720;
            //pageCount = 2;
            for(int i = 0 ; i < pageCount ; ++i) {
                collection.AddCanvas();
                var c = collection[collection.Count - 1];
                var page = new BackgroundPDF(file, i);
                using(var pdfpage = page.GetPage()) {
                    var size = new Size(pdfpage.Size.Width * scale, pdfpage.Size.Height * scale);
                    var ps = Paper.GetPaperSize(size);
                    if(ps != Paper.PaperSize.Other) size = Paper.GetSize(ps);
                    c.Width = size.Width;
                    c.Height = size.Height;
                }
                SetBackground(c, page);
            }
        }
        public class Finalizer : IDisposable{
            public void Dispose() {
                foreach(var doc in PDFDOcuments) {
                    doc.Value.Dispose();
                }
            }
        }
        static Dictionary<string, pdfium.PDFDocument> PDFDOcuments = new Dictionary<string, pdfium.PDFDocument>();
    }
    #endregion

    #region XPS
    public class BackgroundXPS : BackgroundImage.BackgroundData {
        AttachedFile File = null;
        int PageNum = 0;
        BackgroundXPS(AttachedFile file, int pageNum) {
            var pages = GetPaginator(file);
            File = new AttachedFile(file);
            PageNum = pageNum;
        }

        static int GetPageCount(AttachedFile file) {
            return GetPaginator(file).PageCount;
        }

        static DocumentPaginator GetPaginator(AttachedFile file) {
            if(XPSDocuments.ContainsKey(file.FileName)) return XPSDocuments[file.FileName].GetFixedDocumentSequence().DocumentPaginator;
            else {
                var doc = new XpsDocument(file.FileName, System.IO.FileAccess.Read);
                XPSDocuments[file.FileName] = doc;
                return doc.GetFixedDocumentSequence().DocumentPaginator;
            }
        }
        
        DocumentPage GetPage() {
            return GetPaginator(File).GetPage(PageNum);
        }

        static void SetBackground(abJournalInkCanvas c, BackgroundXPS page) {
            if(c.BackgroundData != null) c.BackgroundData.Dispose(c);
            if(c.Viewport.Height != 0) {
                using(var pagedoc = page.GetPage()) {
                    c.Background = new VisualBrush(pagedoc.Visual);
                }
            } else c.Background = null;
            c.BackgroundData = page;
        }

        public void SetViewport(abJournalInkCanvas canvas, abInkCanvas.ViewportChangedEventArgs e) {
            if(e.OldViewport.Height == 0) {
                if(e.NewViewport.Height != 0) {
                    using(var pagedoc = GetPage()) {
                        var brush = new VisualBrush(pagedoc.Visual);
                        canvas.Background = brush;
                    }
                }
            } else {
                if(e.NewViewport.Height == 0) {
                    canvas.Background = null;
                }
            }
        }

        public void Dispose(abJournalInkCanvas c) {
            File.Dispose();
            c.BackgroundData = null;
            c.Background = null;
        }
        
        public static void SetBackground(abJournalInkCanvas c, AttachedFile file, int pageNum) {
            var page = new BackgroundXPS(file, pageNum);
            SetBackground(c, page);
        }

        public static void SetBackground_IgnoreViewport(abJournalInkCanvas c, AttachedFile file, int pageNum) {
            BackgroundXPS page = null;
            try {
                page = new BackgroundXPS(file, pageNum);
                using(var pagedoc = page.GetPage()) {
                    c.Background = new VisualBrush(pagedoc.Visual);
                }
            }
            finally {
                if(page != null) page.File.Dispose();
            }
        }

        public static void LoadFile(AttachedFile file, abJournalInkCanvasCollection collection) {
            int pageCount = BackgroundXPS.GetPageCount(file);
            double scale = (double) 25.4 / (double) 96 * Paper.mmToSize;
            //pageCount = 2;
            for(int i = 0 ; i < pageCount ; ++i) {
                collection.AddCanvas();
                var c = collection[collection.Count - 1];
                var page = new BackgroundXPS(file, i);
                using(var pagedoc = page.GetPage()) {
                    var size = new Size(pagedoc.Size.Width * scale, pagedoc.Size.Height * scale);
                    var ps = Paper.GetPaperSize(size);
                    if(ps != Paper.PaperSize.Other) size = Paper.GetSize(ps);
                    c.Width = size.Width;
                    c.Height = size.Height;
                    SetBackground(c, page);
                }
            }
        }
 
        public class Finalizer : IDisposable {
            public void Dispose() {
                foreach(var doc in XPSDocuments) {
                    doc.Value.Close();
                }
            }
        }
        static Dictionary<string, XpsDocument> XPSDocuments = new Dictionary<string, XpsDocument>();
    }
    #endregion
}


