using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using System.Windows;
using System.Windows.Media;
using System.Text.RegularExpressions;

namespace abJournal {
    public partial class abJournalInkCanvas : abInkCanvas,IabInkCanvas{
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

            // Backgroundの種別を記述する．この操作はabJournalInkCanvasCollection内で完結させる．
            // "color": BackgroundColor（から作られたSolidBrush）
            // "image:pdf:(識別子):page=(ページ)": pdfファイルから作られたVisualBrush，ファイル名は(識別子)で管理される
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

        public void CleanUpBrush() {
            if(BackgroundData != null)BackgroundData.Dispose(this);
            else Background = null;
        }
        public void SetBackgroundFromStr() {
            string str = Info.BackgroundStr;
            if(str == null || str == "" || str == "color") {
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
            foreach(var act in SetBacks) {
                var m = act.Item1.Match(str);
                if(m.Success) {
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

        public abJournalInkCanvas GetPrintingCanvas(DrawingAlgorithm algo) {
            var r = new abJournalInkCanvas(InkData.Clone(), Info);
            r.InkData.DrawingAlgorithm = algo;
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
            foreach(var act in SetBacks) {
                var m = act.Item1.Match(str);
                if(m.Success) {
                    act.Item2(m);
                }
            }
            r.ReDraw();
            return r;
        }


    }
}
