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
        #endregion

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

        #region カーソル
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
        #endregion

        public InkCanvas(InkData d, double width, double height) {
            Children = new VisualCollection(this);
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

        #region InkDataからの通知を受け取る
        public event InkData.UndoChainChangedEventhandelr UndoChainChanged = ((sender, e) => { });

        protected virtual void OnUndoChainChanged(InkData.UndoChainChangedEventArgs e) {
            UndoChainChanged(this, e);
        }
        
        void InkData_StrokeSelectedChanged(object sender, InkData.StrokeChangedEventArgs e) {
            foreach(var s in e.Strokes) {
                s.UpdateVisual();
            }
        }

        void InkData_StrokeChanged(object sender, InkData.StrokeChangedEventArgs e) {
            foreach(var s in e.Strokes){
                s.UpdateVisual();
            }
        }

        void InkData_StrokeDeleted(object sender, InkData.StrokeChangedEventArgs e) {
            foreach(var s in e.Strokes) {
                Children.Remove(s.Visual);
            }
        }

        void InkData_StrokeAdded(object sender, InkData.StrokeChangedEventArgs e) {
            foreach(var s in e.Strokes) {
                Children.Add(s.Visual);
            }
        }

        void InkData_UndoChainChanged(object sender, InkData.UndoChainChangedEventArgs e) {
            OnUndoChainChanged(e);
        }
        #endregion

        #region 描画
        // 内部状態保持用変数
        DrawingVisualLine StrokeDrawingVisual = null;

        void DrawingStart(StylusPoint pt) {
            InkData.ProcessPointerDown(Mode, StrokeDrawingAttributes, StrokeDrawingAttributesPlus, pt);
            if(Mode == InkManipulationMode.Selecting) {
                StrokeDrawingVisual = new DrawingVisualLine(2, Brushes.Orange, DottedDoubleCollection, true);
            } else if(Mode == InkManipulationMode.Inking){
                StrokeDrawingVisual = new DrawingVisualLine(PenThickness, StrokeBrush, StrokeDrawingAttributesPlus.DashArray, StrokeDrawingAttributes.IgnorePressure);
            }
            if(Mode != InkManipulationMode.Erasing) {
                StrokeDrawingVisual.StartPoint(pt);
                Children.Add(StrokeDrawingVisual);
            }
        }
        
        void Drawing(StylusPoint pt) {
            InkData.ProcessPointerUpdate(pt);
            if(Mode != InkManipulationMode.Erasing) {
                StrokeDrawingVisual.AddPoint(pt);
            }
        }

        void DrawingEnd(StylusPoint pt) {
            if(Mode != InkManipulationMode.Erasing) {
                for(int i = Children.Count - 1 ; i >= 0 ; --i) {
                    if(Children[i] == StrokeDrawingVisual) {
                        Children.RemoveAt(i);
                        break;
                    }
                }
            }
            StrokeDrawingVisual = null;
            InkData.ProcessPointerUp();// Mode = Selectingの場合はここで選択位置用四角形が造られる
        }

        class DrawingVisualLine : DrawingVisual {
            bool ignorePressure;
            StylusPoint prevPoint;

            double dashOffset = 0;
            DrawingGroup drawingGroup = null;
            Pen pen = null;

            PathFigure pathFigure = null;
            public DrawingVisualLine(double thickness, Brush brush, DoubleCollection dash, bool ignpres) {
                pen = new Pen(brush, thickness);
                pen.DashStyle = new DashStyle(dash, 0);
                pen.DashCap = PenLineCap.Flat;
                pen.Freeze();
                ignorePressure = ignpres;
                if(ignorePressure) {
                    var geom = new PathGeometry();
                    pathFigure = new PathFigure();
                    geom.Figures.Add(pathFigure);
                    using(var dc = RenderOpen()) {
                        dc.DrawGeometry(null, pen, geom);
                    }
                } else {
                    drawingGroup = new DrawingGroup();
                    using(var dc = RenderOpen()) {
                        dc.DrawDrawing(drawingGroup);
                    }
                }
            }
            public void StartPoint(StylusPoint pt) {
                if(ignorePressure) pathFigure.StartPoint = pt.ToPoint();
                prevPoint = pt;
            }
            public void AddPoint(StylusPoint pt) {
                if(ignorePressure) {
                    pathFigure.Segments.Add(new LineSegment() { Point = pt.ToPoint(), IsSmoothJoin = true });
                } else {
                    var p = pen.Clone();
                    p.Thickness *= pt.PressureFactor * 2;
                    if(p.DashStyle.Dashes.Count > 0) {
                        p.DashStyle.Offset = dashOffset;
                        dashOffset += (Math.Sqrt((prevPoint.X - pt.X) * (prevPoint.X - pt.X) + (prevPoint.Y - pt.Y) * (prevPoint.Y - pt.Y))) / pen.Thickness;
                    }
                    p.Freeze();
                    using(var dc = drawingGroup.Append()) {
                        dc.DrawLine(p, prevPoint.ToPoint(), pt.ToPoint());
                    }
                }
                prevPoint = pt;
            }
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
            var pt = e.GetPosition(this);
            SetCursor();
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
            SetCursor(null);
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
            Children.Clear();
            foreach(var s in InkData.Strokes) {
                s.ReDraw();
                Children.Add(s.Visual);
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
            InvalidateVisual();
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

        public InkCanvas Clone(){
            var rv = new InkCanvas(InkData.Clone(), Width, Height);
            rv.FixedDrawingGroup = FixedDrawingGroup.Clone();
            rv.mode = mode;
            rv.ignorePressure = ignorePressure;
            rv.PenColor = PenColor;
            rv.PenThickness = PenThickness;
            rv.PenDashArray = PenDashArray.Clone();
            rv.BackGroundColor = BackGroundColor;
            return rv;
        }


        #region FrameworkElementでの描画のため
        public VisualCollection Children;
        Brush background;
        public Brush Background {
            get { return background; }
            set { background = value; InvalidateVisual(); }
        }
        protected override int VisualChildrenCount {
            get {
                return Children.Count;
            }
        }
        protected override Visual GetVisualChild(int index) {
            return Children[index];
        }
        protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters) {
            return new PointHitTestResult(this, hitTestParameters.HitPoint);
        }
        protected override void OnRender(DrawingContext drawingContext) {
            if(Background != null) {
                drawingContext.DrawRectangle(Background, null, new Rect(0, 0, RenderSize.Width, RenderSize.Height));
            }
            drawingContext.DrawDrawing(FixedDrawingGroup);
            base.OnRender(drawingContext);
        }
        DrawingGroup FixedDrawingGroup = new DrawingGroup();
        // 追加で何か書き込みたいときに使う．Childrenに入っているものよりも前に描画される（はず）
        public void FixedRenderClear() { FixedDrawingGroup.Children.Clear(); }
        public DrawingContext FixedRenderAppend() { return FixedDrawingGroup.Append(); }
        #endregion
    }
}

