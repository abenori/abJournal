using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ablib {
    public class Paper {
        public enum PaperSize {
            A0, A1, A2, A3, A4, A5, A6, A7, A8, A9, A10,
            B0, B1, B2, B3, B4, B5, B6, B7, B8, B9, B10,
            C0, C1, C2, C3, C4, C5, C6, C7, C8, C9, C10,
            JISB0, JISB1, JISB2, JISB3, JISB4, JISB5, JISB6, JISB7, JISB8, JISB9, JISB10, JISB11, JISB12,
            Letter, Tabloid, Ledger, Legal, Folio, Quarto, Executive, Statement,
            Other
        };

        public static readonly Dictionary<PaperSize, Size> mmSizes = new Dictionary<PaperSize, Size>(){
			{PaperSize.A0,new Size(841,1189)},{PaperSize.A1,new Size(594, 841)},
			{PaperSize.A2,new Size(420, 594)},{PaperSize.A3,new Size(297, 420)},
			{PaperSize.A4,new Size(210,297)},{PaperSize.A5,new Size(148, 210)},
			{PaperSize.A6,new Size(105, 148)},{PaperSize.A7,new Size(74, 105)},
			{PaperSize.A8,new Size(52, 74)},{PaperSize.A9,new Size(37, 52)},
			{PaperSize.A10,new Size(26, 37)},
			{PaperSize.B0,new Size(1000, 1414)},{PaperSize.B1,new Size(707, 1000)},
			{PaperSize.B2,new Size(500, 707)},{PaperSize.B3,new Size( 353, 500)},
			{PaperSize.B4,new Size(250, 353)},{PaperSize.B5,new Size(176, 250)},
			{PaperSize.B6,new Size(125, 176)},{PaperSize.B7,new Size(88, 125)},
			{PaperSize.B8,new Size(62, 88)},{PaperSize.B9,new Size(44, 62)},
			{PaperSize.B10,new Size(31, 44)},
			{PaperSize.JISB0,new Size(1030, 1456)},{PaperSize.JISB1,new Size(728, 1030)},
			{PaperSize.JISB2,new Size(515, 728)},{PaperSize.JISB3,new Size(364, 515)},
			{PaperSize.JISB4,new Size(257, 364)},{PaperSize.JISB5,new Size(182, 257)},
			{PaperSize.JISB6,new Size(128, 182)},{PaperSize.JISB7,new Size(91, 128)},
			{PaperSize.JISB8,new Size(64, 91)},{PaperSize.JISB9,new Size(45, 64)},
			{PaperSize.JISB10,new Size(32, 45)},{PaperSize.JISB11,new Size(22,32)},
			{PaperSize.JISB12,new Size(16,22)},
			{PaperSize.C0,new Size(917,1297)},{PaperSize.C1,new Size(648,917)},
			{PaperSize.C2,new Size(458, 648)},{PaperSize.C3,new Size(324, 458)},
			{PaperSize.C4,new Size(229, 354)},{PaperSize.C5,new Size(162, 229)},
			{PaperSize.C6,new Size(114, 162)},{PaperSize.C7,new Size(81, 114)},
			{PaperSize.C8,new Size(57, 81)},{PaperSize.C9,new Size(40,57)},
			{PaperSize.C10,new Size(28,40)},
			{PaperSize.Letter,new Size(216,279)},
			{PaperSize.Tabloid,new Size(279,432)},
			{PaperSize.Ledger,new Size(432,279)},
			{PaperSize.Legal,new Size(216,356)},
			{PaperSize.Folio,new Size(210,330)},
			{PaperSize.Quarto,new Size(229,279)},
			{PaperSize.Executive,new Size(184,267)},
			{PaperSize.Statement,new Size(140,216)},
        };

        // これをmmなサイズにかけると内部長さになる（ということにする）
        public const double mmToSize = (double) 800 / (double) 210;

        public static PaperSize GetPaperSizeFrommm(Size size) {
            const double abs = 1;
            foreach(var ms in mmSizes) {
                if(Math.Abs(ms.Value.Width - size.Width) < abs && Math.Abs(ms.Value.Height - size.Height) < abs) return ms.Key;
            }
            return PaperSize.Other;
        }
        public static PaperSize GetPaperSize(Size size) {
            return GetPaperSizeFrommm(new Size(size.Width / mmToSize, size.Height / mmToSize));
        }
        public static Size GetSize(PaperSize ps) {
            Size s = GetmmSize(ps);
            return new Size(s.Width * mmToSize, s.Height * mmToSize);
        }
        public static Size GetmmSize(PaperSize ps) {
            try { return mmSizes[ps]; }
            catch(KeyNotFoundException) { throw new NotImplementedException(); }
        }
    }
}
