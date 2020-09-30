using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using System.Windows;
using System.Windows.Media;
using System.Text.RegularExpressions;
using System.Windows.Ink;

namespace abJournal {
    public partial class abJournalInkCanvas : abInkCanvas {
        public abJournalInkCanvas(List<abStroke> strokes, DrawingAttributes dattr, DrawingAttributesPlus dattrp, double width, double height)
            : base(strokes, dattr, dattrp, width, height) {
            Info = new InkCanvasInfo();
            Info.Size = new Size(Width, Height);
        }
        public abJournalInkCanvas(List<abStroke> strokes, DrawingAttributes dattr, DrawingAttributesPlus dattrp, InkCanvasInfo info)
            : base(strokes, dattr, dattrp, info.Size.Width, info.Size.Height) {
            Info = info.DeepCopy();
        }

        public abJournalInkCanvas(double width,double height) : base(width, height) {
            Info = new InkCanvasInfo();
            Info.Size = new Size(Width, Height);
        }
        #region 付加情報クラス
        [ProtoContract]
        public class Rule {
            [ProtoMember(1)]
            public Color Color { get; set; } = Colors.LightBlue;
            [ProtoMember(2)]
            public List<double> DashArray { get; set; } = new List<double>();
            [ProtoMember(3)]
            public double Interval { get; set; } = 80;
            [ProtoMember(4)]
            public bool Show { get; set; } = false;
            [ProtoMember(5)]
            public double Thickness { get; set; } = 2;
            [ProtoMember(6)]
            public double StartMargin { get; set; } = 0;
            [ProtoMember(7)]
            public double EndMargin { get; set; } = 0;
            public Rule DeepCopy() {
                Rule rv = new Rule();
                rv.Color = Color;
                rv.DashArray.Clear();
                for (int i = 0; i < DashArray.Count; ++i) rv.DashArray.Add(DashArray[i]);
                rv.Interval = Interval;
                rv.Show = Show;
                rv.Thickness = Thickness;
                return rv;
            }
        }
        [ProtoContract(SkipConstructor = true)]
        public class InkCanvasInfo {
            [ProtoMember(1)]
            public Rule HorizontalRule { get; set; } = new Rule();
            [ProtoMember(2)]
            public Rule VerticalRule { get; set; } = new Rule();
            [ProtoMember(3)]
            public Size Size { get; set; } = new Size();
            [ProtoMember(4)]
            public Color BackgroundColor { get; set; } = Colors.White;

            // Backgroundの種別を記述する．この操作はabJournalInkCanvasCollection内で完結させる．
            // "color": BackgroundColor（から作られたSolidBrush）
            // "image:pdf:(識別子):page=(ページ)": pdfファイルから作られたVisualBrush，ファイル名は(識別子)で管理される
            // "image:xps:(識別子):page=(ページ)": xpsファイルから作られたVisualBrush，ファイル名は(識別子)で管理される
            [ProtoMember(5)]
            public string BackgroundStr { get; set; }

            public InkCanvasInfo DeepCopy() {
                InkCanvasInfo rv = new InkCanvasInfo();
                rv.HorizontalRule = HorizontalRule.DeepCopy();
                rv.VerticalRule = VerticalRule.DeepCopy();
                rv.Size = Size;
                rv.BackgroundColor = BackgroundColor;
                rv.BackgroundStr = BackgroundStr;
                return rv;
            }
        }
        #endregion

        public InkCanvasInfo Info { get; set; }

        public new double Width {
            get { return base.Width; }
            set { base.Width = value; Info.Size = new Size(value, Info.Size.Height); base.OnPropertyChanged("Width"); }
        }
        public new double Height {
            get { return base.Height; }
            set { base.Height = value; Info.Size = new Size(Info.Size.Width, value); }
        }
        private DrawingVisual RuleVisual = null;

        public void CleanUpBrush() {
            if (BackgroundData != null) BackgroundData.Dispose(this);
            else Background = null;
        }
        public void SetBackgroundFromStr() {
            string str = Info.BackgroundStr;
            if (str == null || str == "" || str == "color") {
                BackgroundColor.SetBackground(this, Info.BackgroundColor);
                Info.BackgroundStr = "color";
                return;
            }
            var SetBacks = new List<Tuple<Regex, Action<Match>>>(){
                new Tuple<Regex,Action<Match>>(new Regex("^image:pdf:([^:]*):page=([0-9]+)$"),(m) =>{
                    using(var file = AttachedFile.GetFileFromIdentifier(m.Groups[1].Value)){
                        int pageNumber;
                        if(Int32.TryParse(m.Groups[2].Value,out pageNumber)) BackgroundPDF.SetBackground(this, file, pageNumber);
                    }
                }),
                new Tuple<Regex,Action<Match>>(new Regex("^image:xps:([^:]*):page=([0-9]+)$"),(m) =>{
                    using(var file = AttachedFile.GetFileFromIdentifier(m.Groups[1].Value)){
                        int pageNumber;
                        if(Int32.TryParse(m.Groups[2].Value, out pageNumber)) BackgroundXPS.SetBackground(this, file, pageNumber);
                    }
                })
            };
            foreach (var act in SetBacks) {
                var m = act.Item1.Match(str);
                if (m.Success) {
                    act.Item2(m);
                    return;
                }
            }
            throw new NotImplementedException();
        }
        public new void RemovedFromView() {
            CleanUpBrush();
        }
        public new void AddedToView() {
            SetBackgroundFromStr();
        }

        public abJournalInkCanvas GetPrintingCanvas() {
            var strokes = new List<abStroke>();
            foreach (var s in Strokes) { strokes.Add(s.Clone() as abStroke); }
            var r = new abJournalInkCanvas(strokes, DefaultDrawingAttributes, DefaultDrawingAttributesPlus, Info);
            var SetBacks = new List<Tuple<Regex, Action<Match>>>(){
                new Tuple<Regex,Action<Match>>(new Regex("^image:pdf:([^:]*):page=([0-9]+)$"),(m) =>{
                    using(var file = AttachedFile.GetFileFromIdentifier(m.Groups[1].Value)){
                        int pageNumber;
                        if(Int32.TryParse(m.Groups[2].Value, out pageNumber)) BackgroundPDF.SetBackground_IgnoreViewport(r, file, pageNumber);
                    }
                }),
                new Tuple<Regex,Action<Match>>(new Regex("^image:xps:([^:]*):page=([0-9]+)$"),(m) =>{
                    using(var file = AttachedFile.GetFileFromIdentifier(m.Groups[1].Value)){
                        int pageNumber;
                        if(Int32.TryParse(m.Groups[2].Value, out pageNumber)) BackgroundXPS.SetBackground_IgnoreViewport(r, file, pageNumber);
                    }
                })
            };
            var str = Info.BackgroundStr;
            foreach (var act in SetBacks) {
                var m = act.Item1.Match(str);
                if (m.Success) {
                    act.Item2(m);
                }
            }
            r.ReDraw();
            return r;
        }
        public void DrawRule() { DrawRule(new List<Rect>()); }
        public void DrawRule(List<Rect> skiparea) {
            DrawRule(skiparea, new Rect(0, 0, Width, Height));
        }

        public void DrawRule(List<Rect> skiparea, Rect area) {
            if (RuleVisual != null) {
                VisualChildren.Remove(RuleVisual);
            }
            RuleVisual = new DrawingVisual();
            using (var dc = RuleVisual.RenderOpen()) {
                if (Info.HorizontalRule.Show && Info.HorizontalRule.Interval > 0) {
                    var brush = new SolidColorBrush(Info.HorizontalRule.Color);
                    brush.Freeze();
                    var pen = new Pen(brush, Info.HorizontalRule.Thickness);
                    pen.DashStyle = new DashStyle(Info.HorizontalRule.DashArray, 0);
                    pen.DashCap = PenLineCap.Flat;
                    pen.Freeze();
                    for (double d = Info.HorizontalRule.Interval + Info.HorizontalRule.StartMargin + area.Top;
                        d < area.Bottom - Info.HorizontalRule.EndMargin; d += Info.HorizontalRule.Interval) {
                        double skipleft = Width, skipright = 0;
                        foreach (var a in skiparea) {
                            if (a.Top <= d && d <= a.Bottom) {
                                skipleft = (a.Left < skipleft) ? a.Left : skipleft;
                                skipright = (a.Right > skipright) ? a.Right : skipright;
                            }
                        }
                        if (skipleft < skipright) {
                            if (area.Left < skipleft) dc.DrawLine(pen, new Point(area.Left, d), new Point(skipleft, d));
                            if (area.Right > skipright) dc.DrawLine(pen, new Point(skipright, d), new Point(area.Right, d));
                        } else {
                            dc.DrawLine(pen, new Point(area.Left, d), new Point(area.Right, d));
                        }
                    }
                }
                if (Info.VerticalRule.Show && Info.VerticalRule.Interval > 0) {
                    var brush = new SolidColorBrush(Info.VerticalRule.Color);
                    brush.Freeze();
                    var pen = new Pen(brush, Info.VerticalRule.Thickness);
                    pen.DashStyle = new DashStyle(Info.VerticalRule.DashArray, 0);
                    pen.DashCap = PenLineCap.Flat;
                    pen.Freeze();
                    for (double d = Info.VerticalRule.Interval + Info.VerticalRule.StartMargin + area.Left;
                        d < area.Right - Info.VerticalRule.EndMargin; d += Info.VerticalRule.Interval) {
                        double skiptop = Height, skipbottom = 0;
                        foreach (var a in skiparea) {
                            if (a.Left <= d && d <= a.Right) {
                                skiptop = (a.Top < skiptop) ? a.Top : skiptop;
                                skipbottom = (a.Bottom > skiptop) ? a.Bottom : skipbottom;
                            }
                        }
                        if (skiptop < skipbottom) {
                            if (area.Top < skiptop) dc.DrawLine(pen, new Point(d, area.Top), new Point(d, skiptop));
                            if (area.Bottom > skipbottom) dc.DrawLine(pen, new Point(d, skipbottom), new Point(d, area.Bottom));
                        } else {
                            dc.DrawLine(pen, new Point(d, area.Top), new Point(d, area.Bottom));
                        }
                    }
                }
            }
            VisualChildren.Add(RuleVisual);
        }
    }
}
