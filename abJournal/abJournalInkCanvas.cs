using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using System.Windows;
using System.Windows.Media;

namespace abJournal {
    public class abJournalInkCanvas : abInkCanvas,IabInkCanvas{
        // 必須
        public abJournalInkCanvas(abInkData d, double width, double height)
            : base(d, width, height) {
            Info = new InkCanvasInfo();
            Info.Size.Width = width;
            Info.Size.Height = height;
        }
        public abJournalInkCanvas(abInkData d, InkCanvasInfo info)
            : base(d, info.Size.Width, info.Size.Height) {
            Info = info.DeepCopy();
        }

        #region 付加情報クラス
        [ProtoContract]
        public class Rule {
            public Rule() {
                DashArray = new DoubleCollection();
                Color = Colors.LightBlue;
                Interval = 80;
                Thickness = 2;
                Show = false;
            }
            [ProtoMember(1)]
            public Color Color { get; set; }
            [ProtoMember(2)]
            public DoubleCollection DashArray { get; set; }
            [ProtoMember(3)]
            public double Interval { get; set; }
            [ProtoMember(4)]
            public bool Show { get; set; }
            [ProtoMember(5)]
            public double Thickness { get; set; }
            public Rule DeepCopy() {
                Rule rv = new Rule();
                rv.Color = Color;
                rv.DashArray.Clear();
                for(int i = 0 ; i < DashArray.Count ; ++i) rv.DashArray.Add(DashArray[i]);
                rv.Interval = Interval;
                rv.Show = Show;
                rv.Thickness = Thickness;
                return rv;
            }
        }
        [ProtoContract(SkipConstructor = true)]
        public class InkCanvasInfo {
            [ProtoMember(1)]
            public Rule HorizontalRule = new Rule();
            [ProtoMember(2)]
            public Rule VerticalRule = new Rule();
            [ProtoMember(3)]
            public Size Size = new Size();
            [ProtoMember(4)]
            public Color BackgroundColor { get; set; }

            // Backgroundの種別を記述する．この操作はabInkCanvasManager内で完結させる．
            // "color": BackgroundColor（から作られたSolidBrush）
            // "image:xps:(識別子):page=(ページ)": xpsファイルから作られたVisualBrush，ファイル名は(識別子)で管理される
            [ProtoMember(5)]
            public string BackgroundStr { get; set; }

            public InkCanvasInfo() {
                BackgroundColor = Colors.White;
            }
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
            set { base.Width = value; Info.Size.Width = value; }
        }
        public new double Height {
            get { return base.Height; }
            set { base.Height = value; Info.Size.Height = value; }
        }

        public void SetBackground(Color color) {
            CleanUpBrush();
            Background = new SolidColorBrush(color);
            Background.Freeze();
            Info.BackgroundColor = color;
            Info.BackgroundStr = "color";
        }
        public void CleanUpBrush() {
            var str = Info.BackgroundStr;
            if(str == null) return;
            if(str.StartsWith("image")) {
                str = str.Substring("image:".Length);
                if(str.StartsWith("xps:")) {
                    BackgroundImageManager.XPSBackground.DisposeBackground(this);
                } else if(str.StartsWith("pdf:")) {
                    BackgroundImageManager.PDFBackground.DisposeBackground(this);
                }
            }
        }
        public void SetBackgroundFromStr() {
            string str = Info.BackgroundStr;
            if(str == null || str == "" ){
                var brush = Background as SolidColorBrush;
                if(brush != null) Info.BackgroundColor = brush.Color;
                else Background = new SolidColorBrush(Info.BackgroundColor);
                Info.BackgroundStr = "color";
            }else if(str == "color") SetBackground(Info.BackgroundColor);
            else if(str.StartsWith("image:")) {
                str = str.Substring("image:".Length);
                if(str.StartsWith("xps:")) {
                    str = str.Substring("xps:".Length);
                    int r = str.IndexOf(":");
                    try {
                        using(var file = AttachedFile.GetFileFromIdentifier(str.Substring(0, r))) {
                            if(file != null) {
                                int pageNumber = Int32.Parse(str.Substring(r + "page=".Length + 1));
                                BackgroundImageManager.XPSBackground.SetBackground(this, file, pageNumber);
                            }
                        }
                    }
                    catch { }// file = nullなら無視する
                } else if(str.StartsWith("pdf:")) {
                    str = str.Substring("pdf:".Length);
                    int r = str.IndexOf(":");
                    try {
                        using(var file = AttachedFile.GetFileFromIdentifier(str.Substring(0, r))) {
                            int pageNumber = Int32.Parse(str.Substring(r + "page=".Length + 1));
                            BackgroundImageManager.PDFBackground.SetBackground(this, file, pageNumber);
                        }
                    }
                    catch { }// file = nullなら無視する
                } else throw new NotImplementedException();
            } else throw new NotImplementedException();
        }
        public new void RemovedFromView() {
            CleanUpBrush();
        }
        public new void AddedToView() {
            SetBackgroundFromStr();
        }

        public abJournalInkCanvas GetPrintingCanvas(DrawingAlgorithm algo) {
            var r = new abJournalInkCanvas(InkData.Clone(), Info);
            r.InkData.DrawingAlgorithm = algo;
            var str = Info.BackgroundStr;
            if(str.StartsWith("image:")) {
                str = str.Substring("image:".Length);
                if(str.StartsWith("xps:")) {
                    str = str.Substring("xps:".Length);
                    int ri = str.IndexOf(":");
                    try {
                        using(var file = AttachedFile.GetFileFromIdentifier(str.Substring(0, ri))) {
                            if(file != null) {
                                int pageNumber = Int32.Parse(str.Substring(ri + "page=".Length + 1));
                                BackgroundImageManager.XPSBackground.SetBackground_IgnoreViewport(r, file, pageNumber);
                            }
                        }
                    }
                    catch { }// file = nullなら無視する
                } else if(str.StartsWith("pdf:")) {
                    str = str.Substring("pdf:".Length);
                    int ri = str.IndexOf(":");
                    try {
                        using(var file = AttachedFile.GetFileFromIdentifier(str.Substring(0, ri))) {
                            int pageNumber = Int32.Parse(str.Substring(ri + "page=".Length + 1));
                            BackgroundImageManager.PDFBackground.SetBackground_IgnoreViewport(r, file, pageNumber);
                        }
                    }
                    catch { }// file = nullなら無視する
                }
            }
            r.ReDraw();
            return r;
        }


    }
}
