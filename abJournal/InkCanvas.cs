using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Ink;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Diagnostics;
using System.ComponentModel;
using ProtoBuf;

namespace ablib {
    public class InkCanvas : Canvas {
        // データ
        public InkData InkData {get; set;}

        // Strokeに対応する描画されているもの
        Dictionary<StrokeData, Shape> Paths = new Dictionary<StrokeData, Shape>();

        // 設定用
        InkManipulationMode mode;
        public InkManipulationMode Mode {
            get { return mode; }
            set {
                if(PenID == 0) {
                    mode = value;
                    SetCursor();
                }
            }
        }

        // 筆圧情報を使うかどうか……なんだけど現状まともに実装されていないので，基本falseと同様です．
        bool usePressure;
        public bool UsePressure {
            get { return usePressure; }
            set {
                if(PenID == 0) {
                    StrokeDrawingAttributes.IgnorePressure = !value;
                    usePressure = value;
                }
            }
        }
        DrawingAttributes StrokeDrawingAttributes = new DrawingAttributes();
        SolidColorBrush StrokeBrush = Brushes.Black;
        public Color PenColor {
            get { return StrokeBrush.Color; }
            set {
                StrokeBrush = new SolidColorBrush(value);
                StrokeDrawingAttributes.Color = value;
                SetCursor();
            }
        }   
        public double PenThickness {
            get { return StrokeDrawingAttributes.Width; }
            set {
                StrokeDrawingAttributes.Width = StrokeDrawingAttributes.Height = value;
                SetCursor();
            }
        }

        public DrawingAttributesPlus StrokeDrawingAttributesPlus = new DrawingAttributesPlus();
        public DoubleCollection PenDashArray {
            get { return StrokeDrawingAttributesPlus.DashArray; }
            set { StrokeDrawingAttributesPlus.DashArray = value; }
        }

        // 一時退避
        InkManipulationMode SavedMode;
        void SaveMode() {
            SavedMode = Mode;
        }

        void RestoreMode() {
            Mode = SavedMode;
        }

        Color backGroundColor = Colors.White;
        public Color BackGroundColor {
            get { return backGroundColor; }
            set { backGroundColor = value; Background = new SolidColorBrush(backGroundColor); }
        }
        [ProtoContract]
        public class Rule {
            public Rule() {
                DashArray = new DoubleCollection(new double[] { 1, 1 });
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
        [ProtoContract(SkipConstructor=true)]
        public class InkCanvasInfo{
            [ProtoMember(1)]
            public Rule HorizontalRule = new Rule();
            [ProtoMember(2)]
            public Rule VerticalRule = new Rule();
            [ProtoMember(3)]
            public Size Size = new Size();
            [ProtoMember(4)]
            public Color BackGround = Colors.White;
            public InkCanvasInfo DeepCopy() {
                InkCanvasInfo rv = new InkCanvasInfo();
                rv.HorizontalRule = HorizontalRule.DeepCopy();
                rv.VerticalRule = VerticalRule.DeepCopy();
                rv.Size = Size;
                rv.BackGround = BackGround;
                return rv;
            }
        }
        public InkCanvasInfo Info {
            get {
                return new InkCanvasInfo() {
                    HorizontalRule = HorizontalRule.DeepCopy(),
                    VerticalRule = VerticalRule.DeepCopy(),
                    Size = new Size(Width,Height),
                    BackGround = BackGroundColor
                };
            }
        }
        public Rule HorizontalRule = new Rule();
        public Rule VerticalRule = new Rule();

        // newしまくらないためだけ
        static DoubleCollection DottedDoubleCollection = new DoubleCollection(new double[] { 1, 1 });

        // Cursors.Noneを指定してCanvasに書いて動かそうと思ったけど，
        // Cursors.Noneを指定しても変わらないことが多々あるので，
        // 直接造ることにした……Webからのコピペ（Img2Cursor.MakeCursor）に丸投げだけど．
        static Dictionary<Tuple<Color, double>, Cursor> InkingCursors = new Dictionary<Tuple<Color, double>, Cursor>();
        Cursor MakeInkingCursor(double thickness, Color color) {
            var key = new Tuple<Color,double>(color, thickness);
            if(InkingCursors.ContainsKey(key)) return InkingCursors[key];
            thickness *= 2;
            const int cursorsize = 254;
            using(var img = new System.Drawing.Bitmap(cursorsize, cursorsize))
            using(var g = System.Drawing.Graphics.FromImage(img)){
	            g.FillRectangle(System.Drawing.Brushes.White, new System.Drawing.Rectangle(0, 0, cursorsize, cursorsize));
	            g.FillEllipse(
	                new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B)),
	                new System.Drawing.Rectangle((int) (cursorsize / 2 - thickness / 2), (int) (cursorsize / 2 - thickness / 2), (int) thickness, (int) thickness));
	            var c =  abJournal.Img2Cursor.MakeCursor(img, new Point(cursorsize/2, cursorsize/2), new Point(0, 0));
	            InkingCursors[key] = c;
	            return c;
            }
        }
        // 消しゴムカーソル
        static Cursor ErasingCursor =  abJournal.Img2Cursor.MakeCursor(abJournal.Properties.Resources.eraser_cursor, new Point(2, 31), new Point(0, 0));

        void SetCursor() {
            switch(Mode) {
            case InkManipulationMode.Inking:
                Cursor = MakeInkingCursor(PenThickness, PenColor); break;
            case InkManipulationMode.Erasing:
                Cursor = ErasingCursor; break;
            default:
                Cursor = Cursors.Cross; break;
            }
        }


        public InkCanvas(InkData d, double width, double height) {
            InkData = d;
            Width = width; Height = height;
            PenThickness = 2;
            usePressure = false;
            PenColor = Colors.Black;
            Mode = InkManipulationMode.Inking;
            BackGroundColor = Colors.White;
            StrokeDrawingAttributes.FitToCurve = true;
            StrokeDrawingAttributes.IgnorePressure = true;
            SetCursor();

            InkData.StrokeAdded += InkData_StrokeAdded;
            InkData.StrokeDeleted += InkData_StrokeDeleted;
            InkData.StrokeChanged += InkData_StrokeChanged;
            InkData.StrokeSelectedChanged += InkData_StrokeSelectedChanged;
            InkData.UndoChainChanged += InkData_UndoChainChanged;
        }

        // http://stackoverflow.com/questions/10362911/rendering-drawingvisuals-fast-in-wpf
        // にあった「おまじない」
        // Childrenに大量にAddしていても，描画が遅くならない
        // いや，実際には遅くなるけど，だいぶ軽減される……ような気がする．
        List<Visual> visuals = new List<Visual>();
        public void AddVisual(Visual visual){
            if(visual != null) {
                visuals.Add(visual);
                base.AddVisualChild(visual);
                base.AddLogicalChild(visual);
            }
        }
        public void RemoveVisual(Visual visual) {
            if(visual != null) {
                visuals.Remove(visual);
                base.RemoveVisualChild(visual);
                base.RemoveLogicalChild(visual);
            }
        }

        public event InkData.UndoChainChangedEventhandelr UndoChainChanged = ((sender, e) => { });

        protected virtual void OnUndoChainChanged(InkData.UndoChainChangedEventArgs e) {
            UndoChainChanged(this, e);
        }
        
        void InkData_StrokeSelectedChanged(object sender, InkData.StrokeChangedEventArgs e) {
            foreach(var s in e.Strokes) {
                Path p = (Path) Paths[s];
                s.GetPath(ref p);
            }
        }

        // Strokeの描画は，InkDataからの通知に応じて行う
        void InkData_StrokeChanged(object sender, InkData.StrokeChangedEventArgs e) {
            foreach(var s in e.Strokes){
                Path p = (Path) Paths[s];
                s.GetPath(ref p);
            }
        }

        void InkData_StrokeDeleted(object sender, InkData.StrokeChangedEventArgs e) {
            foreach(var s in e.Strokes) {
                Children.Remove(Paths[s]);
                Paths.Remove(s);
            }
        }

        void InkData_StrokeAdded(object sender, InkData.StrokeChangedEventArgs e) {
            foreach(var s in e.Strokes) {
                Path p = new Path();
                s.GetPath(ref p);
                Paths[s] = p;
                Children.Add(p);
            }
        }

        void InkData_UndoChainChanged(object sender, InkData.UndoChainChangedEventArgs e) {
            OnUndoChainChanged(e);
        }

        #region イベントハンドラ
        // 内部状態保持用変数
        int PenID = 0;
        int StartDrawingIndexOfChildren = -1;
        StylusPoint PrevPoint;
        PathFigure StrokePathFigure;
        Path StrokePathWhenDrawing;
        protected override void OnStylusDown(System.Windows.Input.StylusDownEventArgs e) {
            //base.OnStylusDown(e);
            if(e.StylusDevice.TabletDevice.Type != TabletDeviceType.Stylus) return;
            PrevPoint = e.GetStylusPoints(this)[0];
            //PrevPoint = new StylusPoint(e.GetPosition(this).X, e.GetPosition(this).Y, 0.5f);
            SaveMode();

            // どっちも押していない：Name = "Stylus", Button[0] = Down, Button[1] = Up
            // 上のボタンを押している：Name = "Eraser"，Button[0] = Down, Button[1] = Up
            // 下のボタンを押している：Name = "Stylus"，Button[0] = Down, Button[1] = Down
            if(e.StylusDevice.Name == "Eraser") {
                Mode = InkManipulationMode.Erasing;
            } else if(
                 e.StylusDevice.StylusButtons.Count > 1 &&
                 e.StylusDevice.StylusButtons[1].StylusButtonState == StylusButtonState.Down
             ) {
                Mode = InkManipulationMode.Selecting;
            }
            InkData.ProcessPointerDown(Mode, StrokeDrawingAttributes, StrokeDrawingAttributesPlus, PrevPoint);
            PenID = e.StylusDevice.Id;
            StartDrawingIndexOfChildren = Children.Count;
            if(Mode != InkManipulationMode.Erasing){
                StrokePathFigure = new PathFigure() {
                    StartPoint = PrevPoint.ToPoint()
                };
                var geometry = new PathGeometry();
                geometry.Figures.Add(StrokePathFigure);
                if(Mode == InkManipulationMode.Inking) {
                    StrokePathWhenDrawing = new Path() {
                        Data = geometry,
                        StrokeThickness = PenThickness,
                        Stroke = StrokeBrush,
                        StrokeDashArray = StrokeDrawingAttributesPlus.DashArray
                    };
                } else {
                    StrokePathWhenDrawing = new Path() {
                        Data = geometry,
                        StrokeThickness = 2,
                        Stroke = Brushes.Orange,
                        StrokeDashArray = DottedDoubleCollection
                    };
                }
                Children.Add(StrokePathWhenDrawing);
            }
        }

        protected override void OnStylusMove(System.Windows.Input.StylusEventArgs e) {
            if(e.StylusDevice.TabletDevice.Type != TabletDeviceType.Stylus) {
                /*
                System.Diagnostics.Debug.WriteLine("TouchMove");
                System.Diagnostics.Debug.WriteLine(e.GetStylusPoints(this)[0].ToPoint());
                System.Diagnostics.Debug.WriteLine(e.GetPosition(this));
                 */ 
                return;
            }
            if(PenID != e.StylusDevice.Id) return;
            var pt = e.GetStylusPoints(this)[0];
            //var pt = new StylusPoint(e.GetPosition(this).X,e.GetPosition(this).Y);
            if(Mode == InkManipulationMode.Inking) {
                StrokePathFigure.Segments.Add(new LineSegment() {
                    Point = pt.ToPoint(),
                    IsSmoothJoin = true
                });
                PrevPoint = pt;
                InkData.ProcessPointerUpdate(pt);
            } else if(Mode == InkManipulationMode.Selecting) {
                if((PrevPoint.X - pt.X) * (PrevPoint.X - pt.X) + (PrevPoint.Y - pt.Y) * (PrevPoint.Y - pt.Y) > 16) {
                    StrokePathFigure.Segments.Add(new LineSegment() {
                        Point = pt.ToPoint(),
                        IsSmoothJoin = true
                    });
                    PrevPoint = pt;
                    InkData.ProcessPointerUpdate(pt);
                }
            } else InkData.ProcessPointerUpdate(pt);
        }

        protected override void OnStylusUp(System.Windows.Input.StylusEventArgs e) {
            if(e.StylusDevice.TabletDevice.Type != TabletDeviceType.Stylus) return;
            if(PenID != e.StylusDevice.Id) return;
            if(Mode == InkManipulationMode.Inking) {
                //Children.RemoveAt(Children.Count - 1);
                //Children.RemoveRange(StartDrawingIndexOfChildren, Children.Count - StartDrawingIndexOfChildren);
                //StartDrawingIndexOfChildren = -1;
                for(int i = Children.Count - 1 ; i >= 0 ; --i) {
                    if(Children[i] == StrokePathWhenDrawing) Children.RemoveAt(i);
                }
            } else if(Mode == InkManipulationMode.Selecting) {
                //Children.RemoveAt(Children.Count - 1);
                //Children.RemoveRange(StartDrawingIndexOfChildren, Children.Count - StartDrawingIndexOfChildren);
                //StartDrawingIndexOfChildren = -1;
                for(int i = Children.Count - 1 ; i >= 0 ; --i) {
                    if(Children[i] == StrokePathWhenDrawing) Children.RemoveAt(i);
                }
            }
            PenID = 0;// 描画終了
            InkData.ProcessPointerUp();// Mode = Selectingの場合はここで選択位置用四角形が造られる
            RestoreMode();
        }
        protected override void OnStylusLeave(System.Windows.Input.StylusEventArgs e) {
            OnStylusUp(e);
        }
        #endregion

        public void ReDraw() {
            Paths.Clear();
            Children.Clear();
            if(InkData.Strokes.Count == 0) return;
            foreach(var s in InkData.Strokes) {
                var p = new Path();
                s.GetPath(ref p);
                Children.Add(p);
                Paths[s] = p;
                /*
                foreach(var spt in s.StylusPoints) {
                    var pt = spt.ToPoint();
                    var ell = new Ellipse() {
                        Stroke = Brushes.Red,
                        Width = 1,
                        Height = 1
                    };
                    Children.Add(ell);
                    SetLeft(ell, pt.X - 0.5);
                    SetTop(ell, pt.Y - 0.5);
                    SetZIndex(ell, 10);
                }*/
            }

            return;
        }

        public void Copy() {
            InkData.Copy();
        }
        public void Cut() {
            InkData.Cut();
        }
        public void Paste() {
            InkData.BeginUndoGroup();
            InkData.Paste();
            // 大きすぎる場合は縮小する
            /*
            double x = Canvas.GetLeft(SelectedRectTracker);
            double y = Canvas.GetTop(SelectedRectTracker);
            double width = SelectedRectTracker.Width;
            double height = SelectedRectTracker.Height;
            if(x + width > Width) {
                height = height * (Width - x) / width;
                width = Width - x;
            }
            if(height > Height - y) {
                width = width * (Height - y) / height;
                height = Height - y;
            }
            if(width == Width - x || height == Height - y) {
                InkData.MoveSelected(
                    new Rect(x, y, SelectedRectTracker.Width, SelectedRectTracker.Height),
                    new Rect(x, y, width, height));
            }*/
            InkData.EndUndoGroup();
        }

        public bool Undo() {
            return InkData.Undo();
        }

        public bool Redo() {
            return InkData.Redo();
        }
        public void ClearSelected() {
            InkData.ClearSelected();
        }

        public Canvas GetCanvas(DrawingAlgorithm algorithm = DrawingAlgorithm.dotNet) {
            Canvas canvas = new Canvas();
            canvas.Height = Height; canvas.Width = Width;
            canvas.Background = Background;
            foreach(var s in InkData.Strokes) {
                var p = new Path();
                s.GetPath(ref p, s.DrawingAttributes,s.DrawingAttributesPlus, false,algorithm);
                canvas.Children.Add(p);
            }
            return canvas;
        }
    }
}

