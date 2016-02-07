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

namespace abJournal {
    public class abInkCanvas : FrameworkElement, IabInkCanvas {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) {
            if(PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        #region 公開用プロパティ
        // データ
        public abInkData InkData { get; set; }

        // 設定用
        InkManipulationMode mode;
        public InkManipulationMode Mode {
            get { return mode; }
            set {
                if(PenID == 0) {
                    mode = value;
                    SetCursor();
                    OnPropertyChanged("Mode");
                }
            }
        }

        DrawingAttributes StrokeDrawingAttributes = new DrawingAttributes();
        SolidColorBrush StrokeBrush = Brushes.Black;
        public Color PenColor {
            get { return StrokeBrush.Color; }
            set {
                StrokeBrush = new SolidColorBrush(value);
                StrokeBrush.Freeze();
                StrokeDrawingAttributes.Color = value;
                SetCursor();
                OnPropertyChanged("PenColor");
            }
        }
        public double PenThickness {
            get { return StrokeDrawingAttributes.Width; }
            set {
                StrokeDrawingAttributes.Width = StrokeDrawingAttributes.Height = value;
                SetCursor();
                OnPropertyChanged("PenThickness");
            }
        }

        public DrawingAttributesPlus StrokeDrawingAttributesPlus = new DrawingAttributesPlus();
        public DoubleCollection PenDashArray {
            get { return StrokeDrawingAttributesPlus.DashArray; }
            set { StrokeDrawingAttributesPlus.DashArray = value; }
        }

        public new double Height {
            get { return base.Height; }
            set { base.Height = value; OnPropertyChanged("Height"); }
        }
        public new double Width {
            get { return base.Width; }
            set { base.Width = value; OnPropertyChanged("Width"); }
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
            var key = new Tuple<Color, double>(color, thickness);
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
        public static Cursor ErasingCursor = null;

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
        // Curosr = とすればいいんだけど，気分的にSetCursor経由でカーソルの設定はすることにしたい．
        void SetCursor(Cursor c) {
            if(c != Cursor) Cursor = c;
        }
        #endregion

        public abInkCanvas(abInkData d, double width, double height) {
            StrokeChildren = new VisualCollection(this);
            Children = new VisualCollection(this);

            InkData = d;
            Width = width; Height = height;
            PenThickness = 2;
            InkData.IgnorePressure = true;
            PenColor = Colors.Black;
            Mode = InkManipulationMode.Inking;
            Background = Brushes.White;
            StrokeDrawingAttributes.FitToCurve = true;
            StrokeDrawingAttributes.IgnorePressure = true;
            SetCursor();

            InkData.StrokeAdded += InkData_StrokeAdded;
            InkData.StrokeDeleted += InkData_StrokeDeleted;
            InkData.StrokeChanged += InkData_StrokeChanged;
            InkData.StrokeSelectedChanged += InkData_StrokeSelectedChanged;
            InkData.UndoChainChanged += InkData_UndoChainChanged;

            MouseLeftButtonDown += ((s, e) => { e.Handled = true; });
            TouchDown += ((s, e) => {
                var pt = e.GetTouchPoint(this);
                if(pt.Size.Width > 5 || pt.Size.Height > 5) e.Handled = true;
            });
        }

        #region InkDataからの通知を受け取る
        public event abInkData.UndoChainChangedEventhandelr UndoChainChanged = ((sender, e) => { });

        protected virtual void OnUndoChainChanged(abInkData.UndoChainChangedEventArgs e) {
            UndoChainChanged(this, e);
        }

        void InkData_StrokeSelectedChanged(object sender, abInkData.StrokeChangedEventArgs e) {
            foreach(var s in e.Strokes) {
                s.UpdateVisual();
            }
        }

        void InkData_StrokeChanged(object sender, abInkData.StrokeChangedEventArgs e) {
            foreach(var s in e.Strokes) {
                s.UpdateVisual();
            }
        }

        void InkData_StrokeDeleted(object sender, abInkData.StrokeChangedEventArgs e) {
            foreach(var s in e.Strokes) {
                StrokeChildren.Remove(s.Visual);
            }
        }

        void InkData_StrokeAdded(object sender, abInkData.StrokeChangedEventArgs e) {
            foreach(var s in e.Strokes) {
                StrokeChildren.Add(s.Visual);
            }
        }

        void InkData_UndoChainChanged(object sender, abInkData.UndoChainChangedEventArgs e) {
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
            } else if(Mode == InkManipulationMode.Inking) {
                StrokeDrawingVisual = new DrawingVisualLine(PenThickness, StrokeBrush, StrokeDrawingAttributesPlus.DashArray, StrokeDrawingAttributes.IgnorePressure);
            }
            if(Mode != InkManipulationMode.Erasing) {
                StrokeDrawingVisual.StartPoint(pt);
                StrokeChildren.Add(StrokeDrawingVisual);
            }
        }

        void Drawing(StylusPoint pt) {
            if(Mode == InkManipulationMode.Selecting) {
                var prev = StrokeDrawingVisual.PrevPoint;
	            // さぼることでHitTestを速くさせる．
                if((prev.X - pt.X) * (prev.X - pt.X) + (prev.Y - pt.Y) * (prev.Y - pt.Y) < 64) return;
            }
            InkData.ProcessPointerUpdate(pt);
            if(Mode != InkManipulationMode.Erasing) {
                StrokeDrawingVisual.AddPoint(pt);
            }
        }

        void DrawingEnd(StylusPoint pt) {
            if(Mode != InkManipulationMode.Erasing) {
                for(int i = StrokeChildren.Count - 1 ; i >= 0 ; --i) {
                    if(StrokeChildren[i] == StrokeDrawingVisual) {
                        StrokeChildren.RemoveAt(i);
                        break;
                    }
                }
            }
            StrokeDrawingVisual = null;
            InkData.ProcessPointerUp();// Mode = Selectingの場合はここで選択位置用四角形が造られる
        }

		// ペンを走らせている時の描画を担当するクラス．
        class DrawingVisualLine : DrawingVisual {
            public StylusPoint PrevPoint;
            bool ignorePressure;

            double dashOffset = 0;
            DrawingGroup drawingGroup = null;
            Pen pen = null;
            public DrawingVisualLine(double thickness, Brush brush, DoubleCollection dash, bool ignpres) {
                pen = new Pen(brush, thickness);
                pen.DashStyle = new DashStyle(dash, 0);
                pen.DashCap = PenLineCap.Flat;
                pen.Freeze();
                ignorePressure = ignpres;
                drawingGroup = new DrawingGroup();
                using(var dc = RenderOpen()) {
                    dc.DrawDrawing(drawingGroup);
                }
            }
            public void StartPoint(StylusPoint pt) {
                PrevPoint = pt;
            }
            public void AddPoint(StylusPoint pt) {
                var p = pen.Clone();
                if(!ignorePressure) p.Thickness *= pt.PressureFactor * 2;
                if(p.DashStyle.Dashes.Count > 0) {
                    p.DashStyle.Offset = dashOffset;
                    dashOffset += (Math.Sqrt((PrevPoint.X - pt.X) * (PrevPoint.X - pt.X) + (PrevPoint.Y - pt.Y) * (PrevPoint.Y - pt.Y))) / pen.Thickness;
                }
                p.Freeze();
                using(var dc = drawingGroup.Append()) {
                    dc.DrawLine(p, PrevPoint.ToPoint(), pt.ToPoint());
                }
                PrevPoint = pt;
            }
        }
        #endregion

        #region イベントハンドラ
        int PenID = 0;
        int TouchType = 0;
        const int STYLUS = 1;
        const int TOUCH = 2;
        const int MOUSE = 3;
        public new event MouseButtonEventHandler MouseLeftButtonDown = ((s, e) => { });
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e) {
			base.OnMouseLeftButtonDown(e);
            if(!e.Handled) MouseLeftButtonDown(this,e);
            if(e.Handled) return;
            if(PenID != 0) return;
            if(TouchType != 0) return;
            var pt = e.GetPosition(this);
            SetCursor();
            DrawingStart(new StylusPoint(pt.X, pt.Y));
            TouchType = MOUSE;
            e.Handled = true;
        }
        public new event MouseEventHandler MouseMove = ((s, e) => { });
        protected override void OnMouseMove(MouseEventArgs e) {
			base.OnMouseMove(e);
            if(!e.Handled) MouseMove(this, e);
            if(e.Handled) return;
            if(TouchType == MOUSE && e.LeftButton == MouseButtonState.Pressed) {
                var pt = e.GetPosition(this);
                Drawing(new StylusPoint(pt.X, pt.Y));
                e.Handled = true;
            } else {
                if(e.StylusDevice == null) SetCursor(null);
            }
        }
        public new event MouseButtonEventHandler MouseLeftButtonUp = ((s, e) => { });
        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e) {
            base.OnMouseLeftButtonUp(e);
            if(!e.Handled) MouseLeftButtonUp(this, e);
            if(e.Handled) return;
            if(TouchType != MOUSE) return;
            var pt = e.GetPosition(this);
            DrawingEnd(new StylusPoint(pt.X, pt.Y));
            e.Handled = true;
            TouchType = 0;
            SetCursor(null);
        }
        public new event MouseEventHandler MouseLeave = ((s, e) => { });
        protected override void OnMouseLeave(MouseEventArgs e) {
            base.OnMouseLeave(e);
            if(!e.Handled) MouseLeave(this, e);
            if(e.Handled) return;
            if(e.LeftButton == MouseButtonState.Pressed) {
                if(TouchType != MOUSE) return;
                var pt = e.GetPosition(this);
                DrawingEnd(new StylusPoint(pt.X, pt.Y));
                e.Handled = true;
                TouchType = 0;
            }
        }
        public new event EventHandler<TouchEventArgs> TouchDown = ((s, e) => { });
        protected override void OnTouchDown(TouchEventArgs e) {
            base.OnTouchDown(e);
            if(!e.Handled) TouchDown(this, e);
            if(e.Handled) { e.Handled = false; return; }
            SetCursor();
            if(PenID != 0) return;
            if(TouchType != 0) return;
            var pt = e.GetTouchPoint(this);
            //System.Diagnostics.Debug.WriteLine(pt.Size);
            DrawingStart(new StylusPoint(pt.Position.X, pt.Position.Y));
            PenID = e.TouchDevice.Id;
            TouchType = TOUCH;
            e.Handled = true;
        }
        public new event EventHandler<TouchEventArgs> TouchMove = ((s, e) => { });
        protected override void OnTouchMove(TouchEventArgs e) {
            base.OnTouchMove(e);
            if(!e.Handled) TouchMove(this, e);
            if(e.Handled) return;
            if(TouchType == TOUCH && PenID == e.TouchDevice.Id) {
                var pt = e.GetTouchPoint(this);
                Drawing(new StylusPoint(pt.Position.X, pt.Position.Y));
                e.Handled = true;
            }
        }
        void OnTouchUpLeave(TouchEventArgs e) {
            if(TouchType == TOUCH && PenID == e.TouchDevice.Id) {
                var pt = e.GetTouchPoint(this);
                DrawingEnd(new StylusPoint(pt.Position.X, pt.Position.Y));
                PenID = 0;
                TouchType = 0;
                e.Handled = true;
            }
        }
        public new event EventHandler<TouchEventArgs> TouchUp = ((s, e) => { });
        protected override void OnTouchUp(TouchEventArgs e) {
            base.OnTouchUp(e);
            if(!e.Handled) TouchUp(this, e);
            if(e.Handled) return;
            OnTouchUpLeave(e);
        }
        public new event EventHandler<TouchEventArgs> TouchLeave = ((s, e) => { });
        protected override void OnTouchLeave(TouchEventArgs e) {
            base.OnTouchLeave(e);
            if(!e.Handled) TouchLeave(this, e);
            if(e.Handled) return;
            OnTouchUpLeave(e);
        }
        public new event StylusEventHandler StylusInAirMove = ((s, e) => { });
        protected override void OnStylusInAirMove(StylusEventArgs e) {
            base.OnStylusInAirMove(e);
            if(!e.Handled) StylusInAirMove(this, e);
            if(e.Handled) return;
            SetCursor();
        }
        public new event StylusEventHandler StylusOutOfRange = ((s, e) => { });
        protected override void OnStylusOutOfRange(StylusEventArgs e) {
            base.OnStylusOutOfRange(e);
            if(!e.Handled) StylusOutOfRange(this, e);
            if(e.Handled) return;
            SetCursor(null);
        }
        public new event StylusEventHandler StylusInRange = ((s, e) => { });
        protected override void OnStylusInRange(StylusEventArgs e) {
            base.OnStylusInRange(e);
            if(!e.Handled) StylusInRange(this, e);
            if(e.Handled) return;
            SetCursor();
        }
        public new event StylusDownEventHandler StylusDown = ((s, e) => { });
        protected override void OnStylusDown(System.Windows.Input.StylusDownEventArgs e) {
            base.OnStylusDown(e);
            if(!e.Handled) StylusDown(this, e);
            if(e.Handled) return;
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

        public new event StylusEventHandler StylusMove = ((s, e) => { });
        protected override void OnStylusMove(System.Windows.Input.StylusEventArgs e) {
            base.OnStylusMove(e);
            if(!e.Handled) StylusMove(this, e);
            if(e.Handled) return;
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

        void OnStylusUpLeave(System.Windows.Input.StylusEventArgs e) {
            if(e.StylusDevice.TabletDevice.Type != TabletDeviceType.Stylus) return;
            if(PenID != e.StylusDevice.Id) return;
            if(TouchType != STYLUS) return;
            PenID = 0;// 描画終了
            TouchType = 0;
            DrawingEnd(e.GetStylusPoints(this)[0]);
            RestoreMode();
        }
        public new event StylusEventHandler StylusUp = ((s, e) => { });
        protected override void OnStylusUp(System.Windows.Input.StylusEventArgs e) {
            base.OnStylusUp(e);
            if(!e.Handled) StylusUp(this, e);
            if(e.Handled) return;
            OnStylusUpLeave(e);
        }
        public new event StylusEventHandler StylusLeave = ((s, e) => { });
        protected override void OnStylusLeave(System.Windows.Input.StylusEventArgs e) {
            base.OnStylusLeave(e);
            if(!e.Handled) StylusLeave(this, e);
            if(e.Handled) return;
            OnStylusUpLeave(e);
        }
        #endregion

        public void ReDraw() {
            StrokeChildren.Clear();
            foreach(var s in InkData.Strokes) {
                s.ReDraw();
                StrokeChildren.Add(s.Visual);
                /*
                foreach(var spt in s.StylusPoints) {
                    var pt = spt.ToPoint();
                    var ell = new Ellipse() {
                        Stroke = Brushes.Red,
                        Width = 1,
                        Height = 1
                    };
                    StrokeChildren.Add(ell);
                    SetLeft(ell, pt.X - 0.5);
                    SetTop(ell, pt.Y - 0.5);
                    SetZIndex(ell, 10);
                }*/
            }
            InvalidateVisual();
            return;
        }

        public void RemovedFromView() { }
        public void AddedToView() { }

        public void Copy() {
            InkData.Copy();
        }

        public void Cut() {
            InkData.Cut();
        }

        public void Paste() {
            InkData.Paste();
        }
        public void Paste(Point pt) {
            InkData.Paste(pt);
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

        #region FrameworkElementでの描画のため
        // StrokeChldrenがChildrenより前に描画される．
        VisualCollection StrokeChildren; // Stroke描画オブジェトをと入れる
        public VisualCollection Children; // それ以外の描画オブジェクトを入れる．外部公開．
        Brush background;
        public Brush Background {
            get { return background; }
            set { background = value; InvalidateVisual(); }
        }
        protected override int VisualChildrenCount {
            get {
                return StrokeChildren.Count + Children.Count;
            }
        }
        protected override Visual GetVisualChild(int index) {
            if(index < Children.Count) return Children[index];
            else return StrokeChildren[index - Children.Count];
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

        public Rect Viewport { get; protected set; }
        #region Viewport制御
        public void SetViewport(Rect rc) {
            OnViewportChanged(new ViewportChangedEventArgs(Viewport, rc));
            Viewport = rc;
        }
        public class ViewportChangedEventArgs : EventArgs {
            public Rect OldViewport { get; private set; }
            public Rect NewViewport { get; private set; }
            public ViewportChangedEventArgs(Rect o, Rect n) {
                OldViewport = o;
                NewViewport = n;
            }
        }
        protected virtual void OnViewportChanged(ViewportChangedEventArgs e) {
            ViewportChanged(this, e);
            //System.Diagnostics.Debug.WriteLine("OnViewportChanged: old = " + e.OldViewport.ToString() + ", nwe = " + e.NewViewport.ToString());
        }
        public delegate void ViewportChangedEventHandler(object sender, ViewportChangedEventArgs e);
        public event ViewportChangedEventHandler ViewportChanged = ((s, e) => { });
        #endregion
    }

    public interface IabInkCanvas : INotifyPropertyChanged{
        double Height { get; set; }
        double Width { get; set; }
        abInkData InkData { get; set; }
        Brush Background { get; set; }
        double PenThickness { get; set; }
        DoubleCollection PenDashArray { get; set; }
        Color PenColor { get; set; }
        InkManipulationMode Mode { get; set; }
        void Copy();
        void Cut();
        void Paste();
        void Paste(Point pt);
        bool Undo();
        bool Redo();
        event abInkData.UndoChainChangedEventhandelr UndoChainChanged;
        void SetViewport(Rect rc);
        // Canvasに入る/から出る時にabInkCanvasCollectionから呼び出される．
        void RemovedFromView();
        void AddedToView();
        void ReDraw();
        void ClearSelected();
    }
}

