using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace abJournal {
    namespace pdfium {
        public class PDFDocument : IDisposable {
            IntPtr documentPtr;
            public string FileName { get; private set; }
            //static int documentloadednum = 0;
            public PDFDocument(string path) {
                documentPtr = PInvoke.FPDF_LoadDocument(path, null);
                if (documentPtr == IntPtr.Zero) throw new System.IO.FileNotFoundException();
                //{ ++documentloadednum; System.Diagnostics.Debug.WriteLine("PDFDocumentLoaded: " + documentloadednum.ToString()); }
                FileName = path;
            }
            //static int pageloadednum = 0;
            public PDFPage GetPage(int pageNum) {
                return new PDFPage(documentPtr, pageNum);
            }
            public int GetPageCount() {
                return PInvoke.FPDF_GetPageCount(documentPtr);
            }
            //static int documentunloadednum = 0;
            public void Dispose() {
                if (documentPtr != IntPtr.Zero) {
                    PInvoke.FPDF_CloseDocument(documentPtr);
                    //{ ++documentunloadednum; System.Diagnostics.Debug.WriteLine("PDFDocumentUnLoaded: " + documentunloadednum.ToString()); }
                    documentPtr = IntPtr.Zero;
                }
            }
            static PDFDocument() {
                if (pdfiumInitializerHolder.initializer == null) pdfiumInitializerHolder.initializer = new pdfiumInitializer();
            }
            ~PDFDocument() { Dispose(); }
        }
        public class PDFPage : IDisposable {
            public PDFPage(IntPtr doc, int pageNum) {
                pagePtr = IntPtr.Zero;
                IntPtr p = PInvoke.FPDF_LoadPage(doc, pageNum);
                //{++pageloadednum;System.Diagnostics.Debug.WriteLine("PDFPageLoadPage: " + pageloadednum.ToString());}
                if (p == IntPtr.Zero) throw new Exception();//後で直す
                pagePtr = p;
            }
            IntPtr pagePtr;
            Size? sizeImpl = null;
            public Size Size {
                get {
                    if (sizeImpl == null) {
                        sizeImpl = new Size(PInvoke.FPDF_GetPageWidth(pagePtr), PInvoke.FPDF_GetPageHeight(pagePtr));
                    }
                    return sizeImpl.Value;
                }
            }
            public BitmapSource GetBitmapSource(Rect rect, double scale, Color background) {
                const double scale_multiple = 1.5;
                var hdc = PInvoke.GetDC(IntPtr.Zero);
                try {
                    double multx = PInvoke.GetDeviceCaps(hdc, PInvoke.DeviceCap.LOGPIXELSX) * scale * scale_multiple / 96;
                    double multy = PInvoke.GetDeviceCaps(hdc, PInvoke.DeviceCap.LOGPIXELSY) * scale * scale_multiple / 96;
                    int width = (int)(rect.Width * multx);
                    int height = (int)(rect.Height * multy);
                    int x = (int)(rect.Left * multx);
                    int y = (int)(rect.Top * multy);
                    var pdfbitmap = PInvoke.FPDFBitmap_Create(width, height, 0);
                    int col = (background.A << 24) | (background.R << 16) | (background.G << 8) | (background.B);
                    try {
                        PInvoke.FPDFBitmap_FillRect(pdfbitmap, 0, 0, width, height, (uint)col);
                        PInvoke.FPDF_RenderPageBitmap(pdfbitmap, pagePtr, -x, -y, x + width, y + height, 0, 0);
                        var stride = PInvoke.FPDFBitmap_GetStride(pdfbitmap);
                        var buf = PInvoke.FPDFBitmap_GetBuffer(pdfbitmap);
                        BitmapSource bitmap = null;
                        for (int i = 0; i < 2; ++i) {
                            try {
                                bitmap = BitmapSource.Create(width, height, 96 * scale_multiple, 96 * scale_multiple, PixelFormats.Bgr32, null, buf, height * stride, stride);
                                break;
                            }
                            catch (OutOfMemoryException) {
                                System.Diagnostics.Debug.WriteLine("OutOfMemory");
                                GC.Collect();
                            }
                        }
                        return bitmap;
                    }
                    finally {
                        PInvoke.FPDFBitmap_Destroy(pdfbitmap);
                    }
                }
                finally {
                    PInvoke.ReleaseDC(IntPtr.Zero, hdc);
                }
            }

            public System.Windows.Media.Visual GetVisual(Rect rect, double scale, Color background) {
                var bitmap = GetBitmapSource(rect, scale, background);
                if (bitmap == null) return null;
                bitmap.Freeze();
                var rv = new System.Windows.Media.DrawingVisual();
                using (var dc = rv.RenderOpen()) {
                    //dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, rect.Left + rect.Width, rect.Top + rect.Height));
                    dc.DrawImage(bitmap, rect);
                }
                return rv;
            }
            //static int pageunloadednum = 0;
            public void Dispose() {
                if (pagePtr != IntPtr.Zero) {
                    //{ ++pageunloadednum; System.Diagnostics.Debug.WriteLine("PdfPageUnloaded: " + pageunloadednum.ToString());}
                    PInvoke.FPDF_ClosePage(pagePtr);
                    pagePtr = IntPtr.Zero;
                }
            }
            ~PDFPage() { Dispose(); }
            static PDFPage() {
                if (pdfiumInitializerHolder.initializer == null) pdfiumInitializerHolder.initializer = new pdfiumInitializer();
            }
        }
        class PInvoke {
            [DllImport("pdfium.dll")]
            public static extern void FPDF_InitLibrary();
            [DllImport("pdfium.dll")]
            public static extern void FPDF_DestroyLibrary();
            [DllImport("pdfium.dll")]
            public static extern IntPtr FPDF_LoadDocument(string file_path, string password);
            [DllImport("pdfium.dll")]
            public static extern IntPtr FPDF_LoadPage(IntPtr document, int page);
            [DllImport("pdfium.dll")]
            public static extern double FPDF_GetPageWidth(IntPtr page);
            [DllImport("pdfium.dll")]
            public static extern double FPDF_GetPageHeight(IntPtr page);
            [DllImport("pdfium.dll")]
            public static extern void FPDF_ClosePage(IntPtr page);
            [DllImport("pdfium.dll")]
            public static extern void FPDF_CloseDocument(IntPtr document);
            [DllImport("pdfium.dll")]
            public static extern int FPDF_GetPageCount(IntPtr document);
            [DllImport("pdfium.dll")]
            public static extern void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page, int start_x, int start_y, int size_x, int size_y, int rotate, int flags);
            [DllImport("pdfium.dll")]
            public static extern IntPtr FPDFBitmap_Create(int width, int height, int alpha);
            [DllImport("pdfium.dll")]
            public static extern void FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);
            [DllImport("pdfium.dll")]
            public static extern IntPtr FPDFBitmap_GetBuffer(IntPtr bitmap);
            [DllImport("pdfium.dll")]
            public static extern void FPDFBitmap_Destroy(IntPtr bitmap);
            [DllImport("pdfium.dll")]
            public static extern int FPDFBitmap_GetStride(IntPtr bitmap);

            [DllImport("user32.dll")]
            public static extern IntPtr GetDC(IntPtr hWnd);
            [DllImport("user32.dll")]
            public static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);
            [DllImport("gdi32.dll")]
            public static extern int GetDeviceCaps(IntPtr hdc, DeviceCap nIndex);
            public enum DeviceCap {
                DRIVERVERSION = 0,
                TECHNOLOGY = 2,
                HORZSIZE = 4,
                VERTSIZE = 6,
                HORZRES = 8,
                VERTRES = 10,
                BITSPIXEL = 12,
                PLANES = 14,
                NUMBRUSHES = 16,
                NUMPENS = 18,
                NUMMARKERS = 20,
                NUMFONTS = 22,
                NUMCOLORS = 24,
                PDEVICESIZE = 26,
                CURVECAPS = 28,
                LINECAPS = 30,
                POLYGONALCAPS = 32,
                TEXTCAPS = 34,
                CLIPCAPS = 36,
                RASTERCAPS = 38,
                ASPECTX = 40,
                ASPECTY = 42,
                ASPECTXY = 44,
                SHADEBLENDCAPS = 45,
                LOGPIXELSX = 88,
                LOGPIXELSY = 90,
                SIZEPALETTE = 104,
                NUMRESERVED = 106,
                COLORRES = 108,
                PHYSICALWIDTH = 110,
                PHYSICALHEIGHT = 111,
                PHYSICALOFFSETX = 112,
                PHYSICALOFFSETY = 113,
                SCALINGFACTORX = 114,
                SCALINGFACTORY = 115,
                VREFRESH = 116,
                DESKTOPVERTRES = 117,
                DESKTOPHORZRES = 118,
                BLTALIGNMENT = 119
            }

        }
        class pdfiumInitializer {
            public pdfiumInitializer() {
                PInvoke.FPDF_InitLibrary();
            }
            ~pdfiumInitializer() {
                PInvoke.FPDF_DestroyLibrary();
            }
        }
        static class pdfiumInitializerHolder {
            public static pdfiumInitializer initializer = null;
        }
    }
}
