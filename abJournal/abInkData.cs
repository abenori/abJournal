using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Runtime.Serialization;
using System.Xml;
using ProtoBuf;

namespace abJournal {
    /**
     * 管理したいもの
     * Stroke
     * テキスト
     * 選択状態
     */
    [ProtoContract]
    public class abInkData{
        [ProtoMember(1)]
        public StrokeDataCollection Strokes { get; set; }
        [ProtoMember(2)]
        public TextDataCollection Texts { get; set; }

        public abInkData() {
            Strokes = new StrokeDataCollection();
            Texts = new TextDataCollection();
        }

        #region Proto-bufferのためのクラス
        [ProtoContract]
        class ProtoStylusPointCollection {
            [ProtoMember(1,OverwriteList=true)]
            List<StylusPoint> Points = new List<StylusPoint>();
            public ProtoStylusPointCollection(StylusPointCollection spc){
                Points = spc == null ? null : new List<StylusPoint>(spc.ToList());
                //Points = spc.ToList();
            }
            public static implicit operator StylusPointCollection(ProtoStylusPointCollection pspc) {
                return new StylusPointCollection(pspc.Points);
            }
            public static implicit operator ProtoStylusPointCollection(StylusPointCollection spc) {
                return new ProtoStylusPointCollection(spc);
            }
        }
        [ProtoContract]
        class ProtoFontFamily {
            [ProtoMember(1)]
            string name;
            public ProtoFontFamily(FontFamily ff){
                name = ff.FamilyNames[System.Windows.Markup.XmlLanguage.GetLanguage(System.Globalization.CultureInfo.CurrentCulture.ToString())];
            }
            public static implicit operator ProtoFontFamily(FontFamily ff) {
                return new ProtoFontFamily(ff);
            }
            public static implicit operator FontFamily(ProtoFontFamily pff) {
                return new FontFamily(pff.name);
            }
        }
        [ProtoContract(SkipConstructor=true)]
        class ProtoFontStyle {
            enum Style { Normal = 1, Italic = 2, Oblique = 3 };
            [ProtoMember(1)]
            Style style;
            ProtoFontStyle(Style s) { style = s; }
            public static implicit operator FontStyle(ProtoFontStyle pfs){
                switch(pfs.style) {
                case Style.Italic: return FontStyles.Italic;
                case Style.Oblique: return FontStyles.Oblique;
                default: return FontStyles.Normal;
                }
            }
            public static implicit operator ProtoFontStyle(FontStyle fs) {
                if(fs == FontStyles.Italic) return new ProtoFontStyle(Style.Italic);
                else if(fs == FontStyles.Oblique) return new ProtoFontStyle(Style.Oblique);
                else return new ProtoFontStyle(Style.Normal);
            }
        }
        [ProtoContract(SkipConstructor=true)]
        class ProtoFontWeight {
            enum Weight { Thin = 1, ExtraLight = 2, UltraLight = 3, Light = 4, Normal = 5, Regular = 6, Medium = 7, DemiBold = 8, SemiBold = 9, Bold = 10, ExtraBold = 11, UltraBold = 12, Black = 13, Heavy = 14, ExtraBlack = 15, UltraBlack = 16};
            [ProtoMember(1)]
            Weight weight;
            ProtoFontWeight(Weight w){weight = w;}
            public static implicit operator FontWeight(ProtoFontWeight pfw) {
                switch(pfw.weight) {
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
            public static implicit operator ProtoFontWeight(FontWeight fw) {
                if(fw == FontWeights.Thin) return new ProtoFontWeight(Weight.Thin);
                else if(fw == FontWeights.ExtraLight) return new ProtoFontWeight(Weight.ExtraLight);
                else if(fw == FontWeights.UltraLight) return new ProtoFontWeight(Weight.UltraLight);
                else if(fw == FontWeights.Light) return new ProtoFontWeight(Weight.Light);
                else if(fw == FontWeights.Regular) return new ProtoFontWeight(Weight.Regular);
                else if(fw == FontWeights.Medium) return new ProtoFontWeight(Weight.Medium);
                else if(fw == FontWeights.DemiBold) return new ProtoFontWeight(Weight.DemiBold);
                else if(fw == FontWeights.SemiBold) return new ProtoFontWeight(Weight.SemiBold);
                else if(fw == FontWeights.Bold) return new ProtoFontWeight(Weight.Bold);
                else if(fw == FontWeights.ExtraBold) return new ProtoFontWeight(Weight.ExtraBold);
                else if(fw == FontWeights.UltraBold) return new ProtoFontWeight(Weight.UltraBold);
                else if(fw == FontWeights.Black) return new ProtoFontWeight(Weight.Black);
                else if(fw == FontWeights.Heavy) return new ProtoFontWeight(Weight.Heavy);
                else if(fw == FontWeights.ExtraBlack) return new ProtoFontWeight(Weight.ExtraBlack);
                else if(fw == FontWeights.UltraBlack) return new ProtoFontWeight(Weight.UltraBlack);
                else return new ProtoFontWeight(Weight.Normal);
            }
        }
        public static ProtoBuf.Meta.RuntimeTypeModel SetProtoBufTypeModel(ProtoBuf.Meta.RuntimeTypeModel model) {
            model.Add(typeof(Stroke), true);
            model[typeof(Stroke)].Add("StylusPoints","DrawingAttributes");
            model[typeof(Stroke)].AddSubType(100, typeof(StrokeData));
            model.Add(typeof(StylusPoint), true);
            model[typeof(StylusPoint)].Add("X","Y","PressureFactor");
            model.Add(typeof(DrawingAttributes), true);
            model[typeof(DrawingAttributes)].Add("Color","FitToCurve","Height","IgnorePressure","IsHighlighter","StylusTip","Width");
            model.Add(typeof(Color), true);
            model[typeof(Color)].Add("A", "R", "G", "B");
            model.Add(typeof(StylusPointCollection), false).SetSurrogate(typeof(ProtoStylusPointCollection));
            model.Add(typeof(Size), true);
            model[typeof(Size)].Add("Height", "Width");
            model.Add(typeof(Rect), true);
            model[typeof(Rect)].Add("X", "Y", "Width", "Height");
            model.Add(typeof(FontFamily), false).SetSurrogate(typeof(ProtoFontFamily));
            model.Add(typeof(FontStyle), false).SetSurrogate(typeof(ProtoFontStyle));
            model.Add(typeof(FontWeight), false).SetSurrogate(typeof(ProtoFontWeight));
            return model;
        }
        public static ProtoBuf.Meta.RuntimeTypeModel SetProtoBufTypeModel2(ProtoBuf.Meta.RuntimeTypeModel model) {
            model.Add(typeof(Color), true);
            model[typeof(Color)].Add("A", "R", "G", "B");
            model.Add(typeof(Size), true);
            model[typeof(Size)].Add("Height", "Width");
            model.Add(typeof(StylusPoint), true);
            model[typeof(StylusPoint)].Add("X", "Y", "PressureFactor");
            model.Add(typeof(StylusPointCollection), false).SetSurrogate(typeof(ProtoStylusPointCollection));
            model.Add(typeof(DrawingAttributes), true);
            model[typeof(DrawingAttributes)].Add("Color", "FitToCurve", "Height", "IgnorePressure", "IsHighlighter", "StylusTip", "Width");
            model.Add(typeof(Rect), true);
            model[typeof(Rect)].Add("X", "Y", "Width", "Height");
            model.Add(typeof(FontFamily), false).SetSurrogate(typeof(ProtoFontFamily));
            model.Add(typeof(FontStyle), false).SetSurrogate(typeof(ProtoFontStyle));
            model.Add(typeof(FontWeight), false).SetSurrogate(typeof(ProtoFontWeight));
            return model;
        }

        #endregion
    }


}

