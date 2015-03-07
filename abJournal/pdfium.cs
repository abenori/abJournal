﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace abJournal {
    namespace pdfium{
        public class PDFDocument : IDisposable {
            IntPtr documentPtr;
            public string FileName { get; private set; }
            public PDFDocument(string path) {
                documentPtr = PInvoke.FPDF_LoadDocument(path, null);
                if(documentPtr == IntPtr.Zero) throw new System.IO.FileNotFoundException();
                FileName = path;
            }
            public PDFPage GetPage(int pageNum) {
                IntPtr p = PInvoke.FPDF_LoadPage(documentPtr, pageNum);
                if(p == IntPtr.Zero) throw new Exception();//後で直す
                return new PDFPage(p);
            }
            public int GetPageCount() {
                return PInvoke.FPDF_GetPageCount(documentPtr);
            }
            public void Dispose() {
                if(documentPtr != IntPtr.Zero) {
                    PInvoke.FPDF_CloseDocument(documentPtr);
                    documentPtr = IntPtr.Zero;
                }
            }
            static PDFDocument() {
                if(pdfiumInitializerHolder.initializer == null) pdfiumInitializerHolder.initializer = new pdfiumInitializer();
            }
            ~PDFDocument() { Dispose(); }
        }
        public class PDFPage : IDisposable {
            public PDFPage(IntPtr p) {
                pagePtr = p;
            }
            IntPtr pagePtr;
            Size? sizeInner = null;
            public Size Size {
                get {
                    if(sizeInner == null) {
                        sizeInner = new Size(PInvoke.FPDF_GetPageWidth(pagePtr), PInvoke.FPDF_GetPageHeight(pagePtr));
                    }
                    return sizeInner.Value;
                }
            }
            public BitmapSource GetBitmapSource(Rect rect, double scale) {
                const double scale_multiple = 2;
                var desktophdc = PInvoke.GetDC(IntPtr.Zero);
                double multx = PInvoke.GetDeviceCaps(desktophdc, PInvoke.DeviceCap.LOGPIXELSX) / 96 * scale * scale_multiple;
                double multy = PInvoke.GetDeviceCaps(desktophdc, PInvoke.DeviceCap.LOGPIXELSY) / 96 * scale * scale_multiple;
                int width = (int) (rect.Width * multx);
                int height = (int) (rect.Height * multy);
                int x = (int) (rect.Left * multx);
                int y = (int) (rect.Top * multy);
                var pdfbitmap = PInvoke.FPDFBitmap_Create(width, height, 0);
                PInvoke.FPDFBitmap_FillRect(pdfbitmap, 0, 0, width, height, 0xFFFFFFFF);
                PInvoke.FPDF_RenderPageBitmap(pdfbitmap, pagePtr, -x, -y, x + width, y + height, 0, 0);
                var stride = PInvoke.FPDFBitmap_GetStride(pdfbitmap);
                var buf = PInvoke.FPDFBitmap_GetBuffer(pdfbitmap);
                var bitmap = BitmapSource.Create(width, height, 96 * scale_multiple, 96 * scale_multiple, PixelFormats.Bgr32, null, buf, height * stride, stride);
                PInvoke.FPDFBitmap_Destroy(pdfbitmap);
                return bitmap;
            }

            public System.Windows.Media.Visual GetVisual(Rect rect, double scale) {
                var bitmap = GetBitmapSource(rect, scale);
                bitmap.Freeze();
                var rv = new System.Windows.Media.DrawingVisual();
                using(var dc = rv.RenderOpen()) {
                    //dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, rect.Left + rect.Width, rect.Top + rect.Height));
                    dc.DrawImage(bitmap, rect);
                }
                return rv;
            }
            public void Dispose() {
                if(pagePtr != IntPtr.Zero) {
                    PInvoke.FPDF_ClosePage(pagePtr);
                    pagePtr = IntPtr.Zero;
                }
            }
            ~PDFPage() { Dispose(); }
            static PDFPage() {
                if(pdfiumInitializerHolder.initializer == null) pdfiumInitializerHolder.initializer = new pdfiumInitializer();
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
