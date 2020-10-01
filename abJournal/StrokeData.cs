using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Ink;
using System.Windows.Media;
using ProtoBuf;

namespace abJournal {
    [ProtoContract]
    public class DrawingAttributesPlus : System.ComponentModel.INotifyPropertyChanged {
        public static List<double> NormalDashArray = new List<double>();
        public DrawingAttributesPlus() {
            DashArray = new List<double>();
        }
        List<double> dashArray;
        [ProtoMember(1)]
        public List<double> DashArray {
            get { return dashArray; }
            set { dashArray = value; OnPropertyChanged("DashArray"); }
        }
        public bool IsNormalDashArray {
            get { return (dashArray.Count == 0); }
        }
        public DrawingAttributesPlus Clone() {
            DrawingAttributesPlus rv = new DrawingAttributesPlus();
            for(int i = 0; i < DashArray.Count; ++i) {
                rv.dashArray.Add(dashArray[i]);
            }
            return rv;
        }
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) {
            if(PropertyChanged != null) {
                PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(name));
            }
        }
    }
    [ProtoContract]
    public partial class StrokeData : Stroke {
        [ProtoMember(1)]
        public DrawingAttributesPlus DrawingAttributesPlus { get; set; }
        // protobuf用．空っぽにしたりSkipConstructor=trueにしたりすると駄目みたい．
        // よくわからない……．
        StrokeData() : base(new StylusPointCollection(new Point[] { new Point(0, 0) })) { }
        public StrokeData(StylusPointCollection spc, DrawingAttributes att, DrawingAttributesPlus attplus)
            : base(spc, att.Clone()) { }
    }

    [ProtoContract]
    public class StrokeDataCollection : List<StrokeData> {
        public StrokeDataCollection() { }
        public StrokeDataCollection(int capacity) : base(capacity) { }
        public StrokeDataCollection(IEnumerable<StrokeData> collection) : base(collection) { }
        public Rect GetBounds() {
            if(Count == 0) return new Rect();
            var rv = this[0].GetBounds();
            for(int i = 1; i < Count; ++i) {
                rv.Union(this[i].GetBounds());
            }
            return rv;
        }
    }


    [ProtoContract]
    public class TextData {
        [ProtoMember(1)]
        public string Text { get; set; }
        [ProtoMember(2)]
        public Rect Rect { get; set; }
        [ProtoMember(3)]
        public FontFamily FontFamily { get; set; }
        [ProtoMember(4)]
        public double FontSize { get; set; }
        [ProtoMember(5)]
        public FontStyle FontStyle { get; set; }
        [ProtoMember(6)]
        public FontWeight FontWeight { get; set; }
        [ProtoMember(7)]
        public Color Color { get; set; }
        public bool Selected { get; set; }
        public TextData(string text) {
            Rect = new Rect();
            FontFamily = new FontFamily("ＭＳ ゴシック");
            Text = text;
        }
        public TextData(string text, Rect rect, FontFamily family, double size, FontStyle style, FontWeight weight, Color color) {
            Text = text; Rect = rect; FontFamily = family;
            FontStyle = style; FontWeight = weight; Color = color;
        }
    }
    [ProtoContract]
    public class TextDataCollection : List<TextData> { }

    #region 単純にしたデータ（違うスレッドに持ち出せる）
    [ProtoContract(SkipConstructor = true)]
    public class StrokeDataStruct {
        [ProtoMember(1)]
        public System.Windows.Input.StylusPointCollection StylusPoints { get; set; }
        [ProtoMember(2)]
        public System.Windows.Ink.DrawingAttributes DrawingAttributes { get; set; }
        [ProtoMember(3)]
        public DrawingAttributesPlus DrawingAttributesPlus { get; set; }
        public StrokeDataStruct(abStroke stroke) {
            StylusPoints = stroke.StylusPoints;
            DrawingAttributes = stroke.DrawingAttributes;
            DrawingAttributesPlus = stroke.DrawingAttributesPlus;
        }
        public abJournal.StrokeData ToStrokeData() {
            return new abJournal.StrokeData(
                StylusPoints,
                DrawingAttributes,
                DrawingAttributesPlus
            ); 
        }
    }
    [ProtoContract(SkipConstructor = true)]
    public class TextDataStruct {
        [ProtoMember(1)]
        public string Text { get; set; }
        [ProtoMember(2)]
        public Rect Rect { get; set; }
        [ProtoMember(3)]
        public string FontFamily { get; set; }
        [ProtoContract(SkipConstructor = true)]
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
        public TextDataStruct(abJournal.TextData td) {
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
    #endregion

}
