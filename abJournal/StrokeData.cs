using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Ink;
using System.Windows.Media;
using System.Runtime.Serialization;
using System.Xml;
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
        public DrawingAlgorithm algorithm = DrawingAlgorithm.dotNet;
        public DrawingAlgorithm Algorithm {
            set { if(value != algorithm) redraw = true; algorithm = value; }
        }
        bool selected;
        public bool Selected {
            get { return selected; }
            set { if(selected != value) redraw = true; selected = value; }
        }
        Pen pen = null;
        Pen Pen {
            get {
                if (pen == null) {
                    var brush = new SolidColorBrush(DrawingAttributes.Color);
                    brush.Opacity = DrawingAttributes.IsHighlighter ? 0.5 : 1;
                    pen = new Pen(brush, DrawingAttributes.Width);
                    pen.EndLineCap = pen.StartLineCap = PenLineCap.Round;
                    if (!DrawingAttributesPlus.IsNormalDashArray) {
                        pen.DashStyle = new DashStyle(DrawingAttributesPlus.DashArray, 0);
                        pen.DashCap = PenLineCap.Flat;
                    }
                    pen.Freeze();
                }
                return pen;
            }
        }
        public new DrawingAttributes DrawingAttributes {
            get { return base.DrawingAttributes; }
            set { base.DrawingAttributes = value; redraw = true; pen = null; }
        }
        DrawingAttributesPlus drawingAttributesPlus = new DrawingAttributesPlus();
        [ProtoMember(1)]
        public DrawingAttributesPlus DrawingAttributesPlus {
            get { return drawingAttributesPlus; }
            set { drawingAttributesPlus = value; redraw = true; pen = null; }
        }

        // protobuf用．空っぽにしたりSkipConstructor=trueにしたりすると駄目みたい．
        // よくわからない……．
        StrokeData()
            : base(new StylusPointCollection(new Point[] { new Point(0, 0) })) {
            DrawingAttributes.AttributeChanged += ((s, e) => { redraw = true; pen = null; });
            DrawingAttributesPlus.PropertyChanged += ((s, e) => { redraw = true; pen = null; });
            visual = new DrawingVisual();
            visual.Transform = new MatrixTransform();
        }
        public StrokeData(StylusPointCollection spc, DrawingAttributes att, DrawingAttributesPlus attplus, DrawingAlgorithm algo, bool selecting = false)
            : base(spc, att.Clone()) {
            DrawingAttributes.AttributeChanged += ((s, e) => { redraw = true; pen = null; });
            DrawingAttributesPlus.PropertyChanged += ((s, e) => { redraw = true; pen = null; });
            visual = new DrawingVisual();
            visual.Transform = new MatrixTransform();
            Selected = selecting;
            drawingAttributesPlus = attplus;
            Algorithm = algo;
        }

        public override void Transform(Matrix transformMatrix, bool applyToStylusTip) {
            ((MatrixTransform)visual.Transform).Matrix *= transformMatrix;
            base.Transform(transformMatrix, applyToStylusTip);
        }
        // VisualはこのStrokeDataが生きている間有効
        // 各種データの変更により描画の結果は変わりうる．
        DrawingVisual visual;
        public DrawingVisual Visual {
            get { UpdateVisual(); return visual; }
        }
        bool redraw = true;

        public void ReDraw() { redraw = true; UpdateVisual(); }

        // 内部状態の変更などにより見た目が変化すべきでも，
        // Visualを実際に取得するか，UpdateVisualを実行するまでは
        // Visualは更新されない．
        public void UpdateVisual() {
            UpdateVisual(DrawingAttributes, DrawingAttributesPlus, Selected, algorithm, Pen);
        }
        // Strokg.GetGeometry = 厚みがある：「二重線」ができる
        // GetOriginalGeometryType1：線でひく：破線が引ける
        // これらの特徴のため，Algoithmが無視されることがある
        void UpdateVisual(DrawingAttributes dattr, DrawingAttributesPlus dattrPlus, bool selecting, DrawingAlgorithm algo, Pen pen) {
            if(!redraw) return;
            redraw = false;
            visual.Transform = new MatrixTransform();

            using(var dc = visual.RenderOpen()) {
                if(selecting) {
                    var geom = base.GetGeometry(dattr);
                    var p = pen.Clone();
                    p.Thickness /= 5;
                    p.Freeze();
                    dc.DrawGeometry(null, p, geom);
                } else {
                    switch(algo) {
                        case DrawingAlgorithm.Type1WithHosei:
                            DrawOriginalType1(dc, MabikiPointsType1(GetHoseiPoints(StylusPoints), dattr), dattr, dattrPlus, pen);
                            break;
                        case DrawingAlgorithm.Type1:
                            DrawOriginalType1(dc, MabikiPointsType1(StylusPoints, dattr), dattr, dattrPlus, pen);
                            break;
                        case DrawingAlgorithm.Line:
                            DrawOriginalLine(dc, StylusPoints, dattr, dattrPlus, pen);
                            break;
                        default:
                            if(dattrPlus.IsNormalDashArray) {
                                base.Draw(dc, dattr);
                            } else {
                                DrawOriginalType1(dc, MabikiPointsType1(StylusPoints, dattr), dattr, dattrPlus, pen);
                            }
                            break;
                    }
                }
            }
        }


        public new StrokeData Clone() {
            return new StrokeData(StylusPoints, DrawingAttributes, DrawingAttributesPlus, algorithm);
        }

        #region Stroke独自描画の実装
        //public static StylusPointCollection HoseiPts = new StylusPointCollection();
        static DoubleCollection Weight = new DoubleCollection();
        static StrokeData() {
            // 手ぶれ？補正のWeightの初期化
            double s = 1;
            for(int i = 0; i < 20; ++i) {
                Weight.Add(Math.Exp(-i * i / (2 * s * s)));
            }
        }

        static StylusPointCollection GetHoseiPoints(StylusPointCollection Points) {
            //HoseiPts.Clear();
            StylusPointCollection rv = new StylusPointCollection(Points.Count);
            // 手ぶれ？補正
            // 参考：http://www24.atwiki.jp/sigetch_2007/pages/18.html
            // 単に周辺の点（最大2N+1個）を重み付きで足しているだけです．
            // 重みはe^{-x^2}がよさげ（上のURLから）なのでそうしている．
            const int N = 3;
            for(int i = 0; i < Points.Count; ++i) {
                var pt = new StylusPoint();
                double wsum = 0;
                int first = Math.Max(0, i - N), last = Math.Min(Points.Count - 1, i + N);
                for(int j = first; j <= last; ++j) {
                    double w = Weight[Math.Abs(j - i)];
                    wsum += w;
                    pt.X += Points[j].X * w;
                    pt.Y += Points[j].Y * w;
                }
                pt.X /= wsum;
                pt.Y /= wsum;
                pt.PressureFactor = Points[i].PressureFactor;
                rv.Add(pt);
                //HoseiPts.Add(pt);
            }
            return rv;
        }

        void DrawOriginalType1(DrawingContext dc, StylusPointCollection Points, DrawingAttributes dattr, DrawingAttributesPlus dattrplus, Pen pen) {
            if(Points.Count <= 1) {
                return;
            }
            if(dattr.IgnorePressure) {
                StreamGeometry geom = new StreamGeometry();
                using(var ctx = geom.Open()) {
                    ctx.BeginFigure(Points[0].ToPoint(), false, false);
                    PointCollection ctrl1 = new PointCollection(), ctrl2 = new PointCollection();
                    GenerateBezierControlPointsType1(Points, ref ctrl1, ref ctrl2);
                    for(int i = 1; i < Points.Count; ++i) {
                        ctx.BezierTo(ctrl1[i - 1], ctrl2[i - 1], Points[i].ToPoint(), true, false);
                    }
                }
                geom.Freeze();
                dc.DrawGeometry(null, pen, geom);
            } else {
                PointCollection ctrl1 = new PointCollection(), ctrl2 = new PointCollection();
                GenerateBezierControlPointsType1(Points, ref ctrl1, ref ctrl2);
                var group = new DrawingGroup();
                double dashOffset = 0;
                for(int i = 1; i < Points.Count; ++i) {
                    StreamGeometry geom = new StreamGeometry();
                    using(var ctx = geom.Open()) {
                        ctx.BeginFigure(Points[i - 1].ToPoint(), false, false);
                        ctx.BezierTo(ctrl1[i - 1], ctrl2[i - 1], Points[i].ToPoint(), true, false);
                    }
                    geom.Freeze();
                    var p = pen.Clone();
                    p.Thickness *= Points[i - 1].PressureFactor * 2;
                    if(p.DashStyle.Dashes.Count > 0) {
                        p.DashStyle.Offset = dashOffset;
                        double dx = Points[i].X - Points[i - 1].X, dy = Points[i - 1].Y - Points[i].Y;
                        dashOffset += Math.Sqrt(dx * dx + dy * dy) / pen.Thickness;
                    }
                    p.Freeze();
                    group.Children.Add(new GeometryDrawing(null, p, geom));
                }
                group.Freeze();
                dc.DrawDrawing(group);
            }
        }

        void DrawOriginalLine(DrawingContext dc, StylusPointCollection Points, DrawingAttributes dattr, DrawingAttributesPlus dattrplus, Pen pen) {
            if(Points.Count <= 1) {
                return;
            }
            if(dattr.IgnorePressure) {
                for(int i = 1; i < Points.Count; ++i) {
                    dc.DrawLine(pen, Points[i - 1].ToPoint(), Points[i].ToPoint());
                }
            } else {
                double dashOffset = 0;
                for(int i = 1; i < Points.Count; ++i) {
                    var p = pen.Clone();
                    p.Thickness *= Points[i - 1].PressureFactor * 2;
                    if(p.DashStyle.Dashes.Count > 0) {
                        p.DashStyle.Offset = dashOffset;
                        double dx = Points[i].X - Points[i - 1].X, dy = Points[i].Y - Points[i - 1].Y;
                        dashOffset += Math.Sqrt(dx * dx + dy * dy) / pen.Thickness;
                    }
                    p.Freeze();
                    dc.DrawLine(p, Points[i - 1].ToPoint(), Points[i].ToPoint());
                }
            }
        }

         
        // 「不要そう」な点を消す．
        // p -- q -- rとあって，これが大体直線上に並んでいる場合にはqを間引く．
        // i : 手元の点，j：先の点
        static StylusPointCollection MabikiPointsType1(StylusPointCollection Points, DrawingAttributes dattr) {
            var nokoriPoints = new StylusPointCollection(Points.Description, Points.Count / 2);
            nokoriPoints.Add(Points[0]);
            for (int i = 0; i < Points.Count - 1; ++i) {
                double pressuresum = 0;
                for (int j = i + 2; j < Points.Count; ++j) {
                    pressuresum += Points[j].PressureFactor;
                    double pressuremean = pressuresum / (j - i - 1);
                    bool mabiku = true;
                    if ((new Vector(Points[j].X - Points[i].X, Points[j].Y - Points[i].Y)).Length > 10) {
                        mabiku = false;
                    } else {
                        for (int k = i + 1; k < j; ++k) {
                            Vector vec1 = new Vector(Points[k].X - Points[i].X, Points[k].Y - Points[i].Y);
                            Vector vec2 = new Vector(Points[j].X - Points[k].X, Points[j].Y - Points[k].Y);
                            if (Math.Abs(Vector.AngleBetween(vec1, vec2)) > 20) {
                                mabiku = false;
                                break;
                            } else if (!dattr.IgnorePressure && Math.Abs(Points[k].PressureFactor - pressuremean) > 0.1) {
                                mabiku = false;
                                break;
                            }
                        }
                    }
                    if (!mabiku) {
                        //System.Diagnostics.Debug.WriteLine("added points at : " + (j - 1).ToString());
                        nokoriPoints.Add(Points[j - 1]);
                        i = j - 2;
                        break;
                    }
                }
            }
            nokoriPoints.Add(Points.Last());
            //System.Diagnostics.Debug.WriteLine("nokori / zentai = " + nokoriPoints.Count.ToString() + " / " + Points.Count.ToString() + " = "  + ((double)nokoriPoints.Count/(double)Points.Count).ToString());
            return nokoriPoints;
        }
        /*
        // とんがり点を探す．これは間引かない．
        hoseiPoints[0] = new PointWithBool(hoseiPoints[0].Point, true);
        hoseiPoints[hoseiPoints.Count - 1] = new PointWithBool(hoseiPoints[hoseiPoints.Count - 1].Point, true);
        if(hoseiPoints.Count > 2) {
            Vector prevSa = hoseiPoints[1].Point - hoseiPoints[0].Point;
            prevSa.Normalize();
            for(int i = 1 ; i < hoseiPoints.Count - 1 ; ++i) {
                Vector Sa = hoseiPoints[i + 1].Point - hoseiPoints[i].Point;
                Sa.Normalize();
                if(prevSa * Sa < 0.9) {
                    hoseiPoints[i] = new PointWithBool(hoseiPoints[i].Point, true);
                    CuspPts.Add(hoseiPoints[i].Point);
                }
                prevSa = Sa;
            }
        }
        */

        // Bezier曲線の制御点を計算する
        // http://www.antigrain.com/research/bezier_interpolation/
        // から．
        // 端のところのcontrol pointは最初の点にする．
        static void GenerateBezierControlPointsType1(StylusPointCollection Points, ref PointCollection ctrlpt1, ref PointCollection ctrlpt2) {
            System.Diagnostics.Debug.Assert(Points.Count >= 2);
            ctrlpt1.Clear(); ctrlpt2.Clear();
            Point firstCtrlPoint = Points[0].ToPoint();
            double prevLength = (Points[1].ToPoint() - Points[0].ToPoint()).Length;
            for(int i = 1; i < Points.Count - 1; ++i) {
                double length = (Points[i + 1].ToPoint() - Points[i].ToPoint()).Length;
                if (prevLength + length == 0) {
                    ctrlpt1.Add(Points[i].ToPoint());
                    ctrlpt2.Add(Points[i].ToPoint());
                    firstCtrlPoint = Points[i].ToPoint();
                } else {
                    Vector vec = (Points[i + 1].ToPoint() - Points[i - 1].ToPoint()) / 2;
                    ctrlpt1.Add(firstCtrlPoint);
                    ctrlpt2.Add(Points[i].ToPoint() - (vec * prevLength / (length + prevLength)));
                    firstCtrlPoint = Points[i].ToPoint() + (vec * length) / (length + prevLength);
                    prevLength = length;
                }
            }
            ctrlpt1.Add(firstCtrlPoint);
            ctrlpt2.Add(Points.Last().ToPoint());
        }

        public static void DrawPath(iTextSharp.text.pdf.PdfWriter writer, double scale, StylusPointCollection stylusPoints, DrawingAttributes drawingAttributes) {
            StylusPointCollection pts = MabikiPointsType1(stylusPoints, drawingAttributes);
            PointCollection cpt1 = new PointCollection(), cpt2 = new PointCollection();
            GenerateBezierControlPointsType1(pts, ref cpt1, ref cpt2);
            writer.DirectContent.MoveTo(scale * pts[0].X, writer.PageSize.Height - scale * pts[0].Y);
            for(int i = 0; i < pts.Count - 1; ++i) {
                if (!drawingAttributes.IgnorePressure) {
                    writer.DirectContent.SetLineWidth(drawingAttributes.Width * pts[i].PressureFactor * scale * 2);
                }
                writer.DirectContent.CurveTo(scale * cpt1[i].X, writer.PageSize.Height - scale * cpt1[i].Y,
                    scale * cpt2[i].X, writer.PageSize.Height - scale * cpt2[i].Y,
                    scale * pts[i + 1].X, writer.PageSize.Height - scale * pts[i + 1].Y);
            }
            writer.DirectContent.Stroke();
        }
    }

    #endregion

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
        public StrokeDataStruct(abJournal.StrokeData stroke) {
            StylusPoints = stroke.StylusPoints;
            DrawingAttributes = stroke.DrawingAttributes;
            DrawingAttributesPlus = stroke.DrawingAttributesPlus;
        }
        public abJournal.StrokeData ToStrokeData() {
            return new abJournal.StrokeData(
                StylusPoints,
                DrawingAttributes,
                DrawingAttributesPlus,
                abJournal.Properties.Settings.Default.DrawingAlgorithm
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
