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
    public class InkCanvas : FrameworkElement {
        #region 公開用プロパティ
        // データ
        public InkData InkData {get; set;}

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

        bool ignorePressure;
        public bool IgnorePressure {
            get { return ignorePressure; }
            set {
                if(PenID == 0) {
                    if(ignorePressure != value) {
                        ignorePressure = value;
                        StrokeDrawingAttributes.IgnorePressure = value;
                        foreach(var s in InkData.Strokes) s.DrawingAttributes.IgnorePressure = value;
                    }
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
        [ProtoContract(SkipConstructor = true)]
        public class InkCanvasInfo {
            [ProtoMember(1)]
            public Rule HorizontalRule = new Rule();
            [ProtoMember(2)]
            public Rule VerticalRule = new Rule();
            [ProtoMember(3)]
            public Size Size = new Size();
            [ProtoMember(4)]
            public Color BackGround { get; set; }
            public InkCanvasInfo() {
                BackGround = Colors.White;
            }
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
                    Size = new Size(Width, Height),
                    BackGround = BackGroundColor
                };
            }
        }
        public Rule HorizontalRule = new Rule();
        public Rule VerticalRule = new Rule();
        #endregion

        // Strokeに対応する描画されているもの
        Dictionary<StrokeData, Visual> Paths = new Dictionary<StrokeData, Visual>();

        // 一時退避
        InkManipulationMode SavedMode;
        void SaveMode() {
            SavedMode = Mode;
        }

        void RestoreMode() {
            Mode = SavedMode;
        }
        // newしまくらないためだけ
        static DoubleCollection DottedDoubleCollection = new DoubleCollection(new double[] { 1, 1 });

        // Cursors.Noneを指定してCanvasに書いて動かそうと思ったけど，
        // Cursors.Noneを指定しても変わらないことが多々あるので，
        // 直接造ることにした……Webからのコピペ（Img2Cursor.MakeCursor）に丸投げだけど．
        static Dictionary<Tuple<Color, double>, Cursor> InkingCursors = new Dictionary<Tuple<Color, double>, Cursor>();
        static Cursor MakeInkingCursor(double thickness, Color color) {
            var key = new Tuple<Color,double>(color, thickness);
            try { return InkingCursors[key]; }
            catch(KeyNotFoundException) {
                thickness *= 2;
                const int cursorsize = 254;
                using(var img = new System.Drawing.Bitmap(cursorsize, cursorsize))
                using(var g = System.Drawing.Graphics.FromImage(img)) {
                    g.FillRectangle(System.Drawing.Brushes.White, new System.Drawing.Rectangle(0, 0, cursorsize, cursorsize));
                    g.FillEllipse(
                        new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B)),
                        new System.Drawing.Rectangle((int) (cursorsize / 2 - thickness / 2), (int) (cursorsize / 2 - thickness / 2), (int) thickness, (int) thickness));
                    var c = Img2Cursor.MakeCursor(img, new Point(cursorsize / 2, cursorsize / 2), new Point(0, 0));
                    InkingCursors[key] = c;
                    return c;
                }
            }
        }
        // 消しゴムカーソル
        static Cursor ErasingCursor =  Img2Cursor.MakeCursor(abJournal.Properties.Resources.eraser_cursor, new Point(2, 31), new Point(0, 0));

        void SetCursor() {
            switch(Mode) {
            case InkManipulationMode.Inking:
                SetCursor(MakeInkingCursor(PenThickness, PenColor)); break;
            case InkManipulationMode.Erasing:
                SetCursor(ErasingCursor); break;
            default:
                SetCursor(Cursors.Cross); break;
            }
        }
        void SetCursor(Cursor c) {
            if(c != Cursor) Cursor = c;
        }


        public InkCanvas(InkData d, double width, double height) {
            InkData = d;
            Width = width; Height = height;
            PenThickness = 2;
            ignorePressure = true;
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

        /*
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
        }*/

        #region InkDataからの通知を受け取る
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
        #endregion

        #region 描画
        // 内部状態保持用変数
        int StartDrawingIndexOfChildren = -1;
        StylusPoint PrevPoint;
        PathFigure StrokePathFigure;
        Path StrokePathWhenDrawing;

        void DrawingStart(StylusPoint pt) {
            PrevPoint = pt;
            InkData.ProcessPointerDown(Mode, StrokeDrawingAttributes, StrokeDrawingAttributesPlus, PrevPoint);
            StartDrawingIndexOfChildren = Children.Count;
            if(Mode != InkManipulationMode.Erasing) {
                if(Mode != InkManipulationMode.Inking || !StrokeDrawingAttributesPlus.IsNormalDashArray) {
                    StrokePathFigure = new PathFigure() {
                        StartPoint = pt.ToPoint()
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
        }
        void Drawing(StylusPoint pt) {
            if(Mode == InkManipulationMode.Inking) {
                if(!StrokeDrawingAttributesPlus.IsNormalDashArray) {
                    StrokePathFigure.Segments.Add(new LineSegment() {
                        Point = pt.ToPoint(),
                        IsSmoothJoin = true
                    });
                } else {
                    double thickness = PenThickness;
                    if(!StrokeDrawingAttributes.IgnorePressure) {
                        thickness *= (pt.PressureFactor * 2);
                    }
                    Children.Add(new Line() {
                        X1 = PrevPoint.X,
                        Y1 = PrevPoint.Y,
                        X2 = pt.X,
                        Y2 = pt.Y,
                        Stroke = StrokeBrush,
                        StrokeDashArray = StrokeDrawingAttributesPlus.DashArray,
                        StrokeThickness = thickness
                    });
                }
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

        void DrawingEnd(StylusPoint pt) {
            if(Mode == InkManipulationMode.Inking) {
                if(!StrokeDrawingAttributesPlus.IsNormalDashArray) {
                    for(int i = Children.Count - 1 ; i >= 0 ; --i) {
                        if(Children[i] == StrokePathWhenDrawing) Children.RemoveAt(i);
                    }
                } else {
                    Children.RemoveAt(Children.Count - 1);
                    Children.RemoveRange(StartDrawingIndexOfChildren, Children.Count - StartDrawingIndexOfChildren);
                }
                StartDrawingIndexOfChildren = -1;
            } else if(Mode == InkManipulationMode.Selecting) {
                StartDrawingIndexOfChildren = -1;
                for(int i = Children.Count - 1 ; i >= 0 ; --i) {
                    if(Children[i] == StrokePathWhenDrawing) Children.RemoveAt(i);
                }
            }
            InkData.ProcessPointerUp();// Mode = Selectingの場合はここで選択位置用四角形が造られる
        }
        #endregion

        #region イベントハンドラ
        int PenID = 0;
        int TouchType = 0;
        const int STYLUS = 1;
        const int TOUCH = 2;
        const int MOUSE = 3;
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
            if(PenID != 0) return;
            if(TouchType != 0) return;
            if(e.ClickCount != 1) return;
            var pt = e.GetPosition(this);
            DrawingStart(new StylusPoint(pt.X, pt.Y));
            TouchType = MOUSE;
            e.Handled = true;
        }
        protected override void OnMouseMove(MouseEventArgs e) {
            if(TouchType == MOUSE && e.LeftButton == MouseButtonState.Pressed) {
                var pt = e.GetPosition(this);
                Drawing(new StylusPoint(pt.X, pt.Y));
                e.Handled = true;
            } else {
                if(e.StylusDevice == null) SetCursor(null);
            }
        }
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
            if(TouchType != MOUSE) return;
            var pt = e.GetPosition(this);
            DrawingEnd(new StylusPoint(pt.X, pt.Y));
            e.Handled = true;
            TouchType = 0;
        }
        protected override void OnMouseLeave(MouseEventArgs e) {
            if(e.LeftButton == MouseButtonState.Pressed) {
                if(TouchType != MOUSE) return;
                var pt = e.GetPosition(this);
                DrawingEnd(new StylusPoint(pt.X, pt.Y));
                e.Handled = true;
                TouchType = 0;
            }
        }
        protected override void OnTouchDown(TouchEventArgs e) {
            SetCursor();
            if(PenID != 0) return;
            if(TouchType != 0) return;
            var pt = e.GetTouchPoint(this);
            //System.Diagnostics.Debug.WriteLine(pt.Size);
            if(pt.Size.Width < 5 && pt.Size.Height < 5) {
                DrawingStart(new StylusPoint(pt.Position.X, pt.Position.Y));
                PenID = e.TouchDevice.Id;
                TouchType = TOUCH;
                e.Handled = true;
            }
        }
        protected override void OnTouchMove(TouchEventArgs e) {
            if(TouchType == TOUCH && PenID == e.TouchDevice.Id) {
                var pt = e.GetTouchPoint(this);
                Drawing(new StylusPoint(pt.Position.X, pt.Position.Y));
                e.Handled = true;
            }
        }
        protected override void OnTouchUp(TouchEventArgs e) {
            if(TouchType == TOUCH && PenID == e.TouchDevice.Id) {
                var pt = e.GetTouchPoint(this);
                DrawingEnd(new StylusPoint(pt.Position.X, pt.Position.Y));
                PenID = 0;
                TouchType = 0;
                e.Handled = true;
            }
        }
        protected override void OnTouchLeave(TouchEventArgs e) {
            OnTouchUp(e);
        }
        protected override void OnStylusInAirMove(StylusEventArgs e) {
            SetCursor();
        }
        protected override void OnStylusDown(System.Windows.Input.StylusDownEventArgs e) {
            if(PenID != 0) return;
            if(TouchType != 0) return;
            //base.OnStylusDown(e);
            if(e.StylusDevice.TabletDevice.Type != TabletDeviceType.Stylus) return;
            SaveMode();

            // VAIO Duo 13の場合
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
            PenID = e.StylusDevice.Id;
            TouchType = STYLUS;
            DrawingStart(e.GetStylusPoints(this)[0]);
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
            if(TouchType != STYLUS) return;
            if(PenID != e.StylusDevice.Id) return;
            var pt = e.GetStylusPoints(this)[0];
            //var pt = new StylusPoint(e.GetPosition(this).X,e.GetPosition(this).Y);
            Drawing(pt);
        }

        protected override void OnStylusUp(System.Windows.Input.StylusEventArgs e) {
            if(e.StylusDevice.TabletDevice.Type != TabletDeviceType.Stylus) return;
            if(PenID != e.StylusDevice.Id) return;
            if(TouchType != STYLUS) return;
            PenID = 0;// 描画終了
            TouchType = 0;
            DrawingEnd(e.GetStylusPoints(this)[0]);
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
                s.GetPath(ref p, s.DrawingAttributes, s.DrawingAttributesPlus, s.Selected, InkData.DrawingAlgorithm);
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

        public Canvas GetCanvas(DrawingAlgorithm algorithm,bool ignorepressure) {
            Canvas canvas = new Canvas();
            canvas.Height = Height; canvas.Width = Width;
            canvas.Background = Background;
            foreach(var s in InkData.Strokes) {
                var p = new Path();
                bool ignpres = s.DrawingAttributes.IgnorePressure;
                s.DrawingAttributes.IgnorePressure = ignorepressure;
                s.GetPath(ref p, s.DrawingAttributes, s.DrawingAttributesPlus, false, algorithm);
                s.DrawingAttributes.IgnorePressure = ignpres;
                canvas.Children.Add(p);
            }
            return canvas;
        }
        public Canvas GetCanvas(DrawingAlgorithm algorithm = DrawingAlgorithm.dotNet){
            return GetCanvas(algorithm, ignorePressure);
        }


        #region FrameworkElementでの描画のため
        public VisualCollection Children;
        public Brush Background;
        protected override int VisualChildrenCount {
            get {
                return Children.Count;
            }
        }
        protected override Visual GetVisualChild(int index) {
            if(index < 0 || index >= Children.Count) {
                throw new ArgumentOutOfRangeException();
            }
            return Children[index];
        }
        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters) {
            return new PointHitTestResult(this, hitTestParameters.HitPoint);
        }
        protected override void OnRender(DrawingContext drawingContext) {
            if(Background != null) {
                drawingContext.DrawRectangle(Background, null, new Rect(0, 0, RenderSize.Width, RenderSize.Height));
            }
            base.OnRender(drawingContext);
        }


        #endregion
    }


}

