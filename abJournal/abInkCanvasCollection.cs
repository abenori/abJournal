﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Input;
using System.Runtime.Serialization;
using System.Xml;
using ProtoBuf;
using System.ComponentModel;

namespace abJournal {
    // InkCanvasたちからなる「文書用」
    // InkCanvasの適切な配置とかを担当．
    // 継承しているCanvasの中にもう一つCanvas（innerCanvas）を置き，その中にablib.InkCanvasを並べる．
    public class abInkCanvasCollection : Canvas, IEnumerable<abInkCanvas>, INotifyPropertyChanged {
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) {
            if(PropertyChanged != null) {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        List<abInkCanvas> CanvasCollection = new List<abInkCanvas>();
        Canvas innerCanvas = new Canvas();
        const double sukima = 100;
        const double LengthBetweenCanvas = 10;
        #region 公開用のプロパティ

        DrawingAlgorithm drawingAlgorithm = DrawingAlgorithm.dotNet;
        public DrawingAlgorithm DrawingAlgorithm {
            get { return drawingAlgorithm; }
            set {
                if(drawingAlgorithm != value) {
                    drawingAlgorithm = value;
                    foreach(var c in CanvasCollection) {
                        c.InkData.DrawingAlgorithm = value;
                        c.ReDraw();
                    }
                    OnPropertyChanged("DrawingAlgorithm");
                }
            }
        }

        InkManipulationMode mode;
        public InkManipulationMode Mode {
            get { return mode; }
            set {
                if(mode != value) {
                    mode = value;
                    foreach(var i in CanvasCollection) i.Mode = Mode;
                    OnPropertyChanged("Mode");
                }
            }
        }
        Color penColor;
        public Color PenColor {
            get { return penColor; }
            set {
                if(penColor != value) {
                    penColor = value;
                    foreach(var c in CanvasCollection) c.PenColor = penColor;
                    OnPropertyChanged("PenColor");
                }
            }
        }
        double penThickness;
        public double PenThickness {
            get { return penThickness; }
            set {
                if(penThickness != value) {
                    penThickness = value;
                    foreach(var c in CanvasCollection) c.PenThickness = penThickness;
                    OnPropertyChanged("PenThickness");
                }
            }
        }
        public static DoubleCollection DashArray_Dashed = new DoubleCollection(new double[] { 2, 1 });
        public static DoubleCollection DashArray_Normal = DrawingAttributesPlus.NormalDashArray;
        bool penDashed = false;
        public bool PenDashed {
            get { return penDashed; }
            set {
                penDashed = value;
                foreach(var c in CanvasCollection) c.PenDashArray = value ? DashArray_Dashed : DashArray_Normal;
                OnPropertyChanged("PenDashed");
            }
        }

        double scale = 1;
        public double Scale {
            get { return scale; }
            set {
                Matrix m = ((MatrixTransform) innerCanvas.RenderTransform).Matrix;
                m.Scale(value / scale, value / scale);
                ((MatrixTransform) innerCanvas.RenderTransform).Matrix = m;
                scale = value;
                Scroll();
                OnPropertyChanged("Scale");
            }
        }
        bool ignorePressure = true;
        public bool IgnorePressure {
            get { return ignorePressure; }
            set {
                ignorePressure = value;
                foreach(var c in CanvasCollection) {
                    c.IgnorePressure = value;
                    c.ReDraw();
                }
                OnPropertyChanged("IgnorePressure");
            }
        }

        public new Brush Background {
            get { return base.Background; }
            set { base.Background = value; innerCanvas.Background = value; OnPropertyChanged("Background"); }
        }

        public int Count {
            get { return CanvasCollection.Count; }
        }
        public abInkCanvas this[int i] {
            get { return CanvasCollection[i]; }
        }
        #endregion

        #region 選択
        RectTracker SelectedRectTracker = new RectTracker();
        abInkCanvas CanvasContainingSelection = null;
        #endregion

        public abInkCanvasCollection() {
            SizeChanged += InkCanvasCollection_SizeChanged;

            Mode = InkManipulationMode.Inking;
            PenThickness = 2;
            Background = Brushes.Gray;

            IsManipulationEnabled = true;

            innerCanvas.Width = 0;
            innerCanvas.Height = 0;
            innerCanvas.Background = Background;
            innerCanvas.RenderTransform = new MatrixTransform();
            innerCanvas.MouseDown += innerCanvas_MouseDown;
            innerCanvas.TouchDown += innerCanvas_TouchDown;
            Children.Add(innerCanvas);

            SelectedRectTracker.MouseMove += SelectedRectTracker_MouseMove;
            SelectedRectTracker.TrackerStart += SelectedRectTracker_TrackerStart;
            SelectedRectTracker.TrackerSizeChanged += SelectedRectTracker_TrackerSizeChanged;
            SelectedRectTracker.TrackerEnd += SelectedRectTracker_TrackerEnd;
            innerCanvas.Children.Add(SelectedRectTracker);
            SelectedRectTracker.Visibility = Visibility.Hidden;
            Canvas.SetZIndex(SelectedRectTracker, 10);
        }

        void VerticalArrangeCanvas() {
            if(CanvasCollection.Count == 0) return;
            double height = 0;
            double width = 0;
            for(int i = 0 ; i < CanvasCollection.Count ; ++i) {
                Canvas.SetTop(CanvasCollection[i], height);
                height += LengthBetweenCanvas + CanvasCollection[i].Height;
                width = Math.Max(width,CanvasCollection[i].Width);
            }
            height -= LengthBetweenCanvas;
            innerCanvas.Height = height;
            innerCanvas.Width = width;
            Scroll();
        }

        #region スクロール系
        public void Scroll(Vector scroll = new Vector()) {
            Vector toadjust = GetAdjustedVector(scroll);
            ScrollWithoutAdjust(scroll - toadjust);
        }
        void ScrollWithoutAdjust(Vector vec) {
            Matrix m = ((MatrixTransform) innerCanvas.RenderTransform).Matrix;
            m.Translate(vec.X, vec.Y);
            ((MatrixTransform) innerCanvas.RenderTransform).Matrix = m;
            CalculateCurrentPage(true);
        }

        protected override void OnManipulationDelta(System.Windows.Input.ManipulationDeltaEventArgs e) {
            var adjust = GetAdjustedVector(e.DeltaManipulation.Translation);
            ScrollWithoutAdjust(e.DeltaManipulation.Translation - adjust);
            //ScrollWithoutAdust(e.DeltaManipulation.Translation);
            if(e.IsInertial) {
                e.ReportBoundaryFeedback(new ManipulationDelta(adjust, 0, new Vector(), new Vector()));
            }
            //e.Handled = true;
            base.OnManipulationDelta(e);
        }
        protected override void OnManipulationInertiaStarting(ManipulationInertiaStartingEventArgs e) {
            e.TranslationBehavior.DesiredDeceleration = 0.005;
            /*
            var adjust = GetAdjustedVector();
            if(adjust.Length > 0) {
                var v = -adjust;
                v.Normalize();
                e.TranslationBehavior.InitialVelocity = v;
                e.TranslationBehavior.DesiredDisplacement = adjust.Length;
            }*/
            //e.Handled = true;
            base.OnManipulationInertiaStarting(e);
        }
        protected override void OnManipulationStarting(ManipulationStartingEventArgs e) {
            var rect = innerCanvas.RenderTransform.TransformBounds(new Rect(0, 0, innerCanvas.Width, 0));
            if(rect.Width < ActualWidth + 2) e.Mode = ManipulationModes.TranslateY;
            else e.Mode = ManipulationModes.Translate;
            e.IsSingleTouchEnabled = true;
            //e.Handled = true;
            base.OnManipulationStarting(e);
        }
        Vector GetAdjustedVector(Vector scroll = new Vector()) {
            return GetAdjustedVector(scroll, RenderSize);
        }
        Vector GetAdjustedVector(Vector scroll,Size windowSize) {
            Vector rv = new Vector();
            if(CanvasCollection.Count == 0) return rv;
            var canvas = CanvasCollection[0];
            Rect bounds;
            try {
                //bounds = (new TranslateTransform(scroll.X, scroll.Y)).TransformBounds(innerCanvas.RenderTransform.TransformBounds(VisualTreeHelper.GetDrawing(innerCanvas).Bounds));
                bounds = (new TranslateTransform(scroll.X, scroll.Y)).TransformBounds(innerCanvas.RenderTransform.TransformBounds(new Rect(-innerCanvas.Width/2,0,innerCanvas.Width,innerCanvas.Height)));
            }
            catch(NullReferenceException) { return rv; }

            if(bounds.Width != 0) {
                if(bounds.Width < windowSize.Width + 2) {
                    rv.X = bounds.Left - (windowSize.Width - bounds.Width) / 2;
                } else {
                    /*
                    if(bounds.Left > ActualWidth - sukima) rv.X = bounds.Left - (ActualWidth - sukima);
                    else if(bounds.Right < sukima) rv.X = bounds.Right - sukima;
                     */ 
                    if(bounds.Left > 0) rv.X = bounds.Left;
                    else if(bounds.Right < windowSize.Width) rv.X = bounds.Right - windowSize.Width;
                }
            }

            if(bounds.Height != 0) {
                if(bounds.Top > windowSize.Height - sukima) rv.Y = bounds.Top - (windowSize.Height - sukima);
                //if(bounds.Top > 0) rv.Y = bounds.Top;
                else if(bounds.Bottom < sukima) rv.Y = bounds.Bottom - sukima;
            }

            //System.Diagnostics.Debug.WriteLine("scroll: " + scroll + ", bounds: " + bounds + ", rv; " + rv);

            return rv;
        }
        protected override void OnMouseWheel(MouseWheelEventArgs e) {
            Scroll(new Vector(0, e.Delta / 3));
        }

        void InkCanvasCollection_SizeChanged(object sender, SizeChangedEventArgs e) {
            Scroll(new Vector((e.NewSize.Width - e.PreviousSize.Width)/2,0));
        }
        #endregion

        public void AddCanvas(abInkData d,Size size,Color background) {
            InsertCanvas(d, size,background,CanvasCollection.Count);
        }
        public void InsertCanvas(abInkData d, Size size,Color background,int index) {
            d.DrawingAlgorithm = DrawingAlgorithm;
            abInkCanvas canvas = new abInkCanvas(d, size.Width, size.Height);
            canvas.PropertyChanged += canvas_PropertyChanged;
            canvas.IgnorePressure = ignorePressure;
            canvas.Background = new SolidColorBrush(background);
            canvas.Background.Freeze();
            canvas.Mode = Mode;

            CanvasCollection.Insert(index, canvas);
            canvas.UndoChainChanged += InkCanvasCollection_UndoChainChanged;
            canvas.InkData.StrokeSelectedChanged += ((s, e) => { InkData_StrokeSelectedChanged(canvas, e); });
            canvas.InkData.StrokeAdded += ((s, e) => { InkData_StrokeAdded(canvas, e); });
            canvas.InkData.StrokeChanged += ((s, e) => { InkData_StrokeChanged(canvas, e); });
            canvas.InkData.StrokeDeleted += ((s, e) => { InkData_StrokeDeleted(canvas, e); });
            canvas.PenThickness = PenThickness;
            canvas.PenColor = PenColor;
            canvas.PenDashArray = PenDashed ? DashArray_Dashed : DashArray_Normal;
            canvas.Mode = Mode;
            canvas.InkData.DrawingAlgorithm = DrawingAlgorithm;
            canvas.ReDraw();
            AddUndoChain(new AddCanvasCommand(canvas, index));
            innerCanvas.Children.Add(canvas);
            innerCanvas.Height += LengthBetweenCanvas + canvas.Height;
            VerticalArrangeCanvas();
            Canvas.SetLeft(canvas, -size.Width/2);
            OnPropertyChanged("Updated");
            OnPropertyChanged("Count");
            if(CurrentPage == -1) CurrentPage = 0;
        }

        void canvas_PropertyChanged(object sender, PropertyChangedEventArgs e) {
            switch(e.PropertyName) {
            case "Width":
                var c = (abInkCanvas)sender;
                Canvas.SetLeft(c, -c.Width / 2);
                break;
            default:
                break;
            }
        }

        public void DeleteCanvas(int index) {
            if(index < 0 || index >= CanvasCollection.Count) return;
            abInkCanvas ic = CanvasCollection[index];
            CanvasCollection.RemoveAt(index);
            innerCanvas.Children.Remove(ic);
            AddUndoChain(new DeleteCanvasCommand(ic, index));
            VerticalArrangeCanvas();
            OnPropertyChanged("Updated");
            OnPropertyChanged("Count");
            return;
        }

        #region 選択関係
        void innerCanvas_TouchDown(object sender, TouchEventArgs e) {
            if(SelectedRectTracker.Visibility == Visibility.Visible) {
                var pt = e.GetTouchPoint(innerCanvas).Position;
                CheckPointInSelection(pt.X, pt.Y);
            }
        }

        void innerCanvas_MouseDown(object sender, MouseButtonEventArgs e) {
            if(SelectedRectTracker.Visibility == Visibility.Visible) {
                var pt = e.GetPosition(innerCanvas);
                CheckPointInSelection(pt.X, pt.Y);
            }
        }
        void CheckPointInSelection(double x,double y) {
            double sx = Canvas.GetLeft(SelectedRectTracker);
            double sy = Canvas.GetTop(SelectedRectTracker);
            if(x < sx || x > sx + SelectedRectTracker.Width || y < sy || y > sy + SelectedRectTracker.Height) {
                // RectTrackerから外れたら選択を解除する．
                if(CanvasContainingSelection != null) CanvasContainingSelection.ClearSelected();
            }
        }


        void SetSelectedRectTracker(abInkCanvas can) {
            int count = 0;
            Rect bound = new Rect(Canvas.GetLeft(can), Canvas.GetTop(can), can.Width, can.Height);
            bound = can.RenderTransform.TransformBounds(bound);
            StrokeDataCollection Strokes = can.InkData.Strokes;
            Rect rect = new Rect();
            foreach(var s in Strokes) {
                if(s.Selected) {
                    if(count == 0) rect = s.GetBounds();
                    else rect.Union(s.GetBounds());
                    ++count;
                }
            }
            if(count == 0) {
                if(SelectedRectTracker.Visibility == Visibility.Visible) {
                    SelectedRectTracker.Visibility = Visibility.Hidden;
                    foreach(var c in CanvasCollection) c.IsEnabled = true;
                }
            } else {
                if(CanvasContainingSelection != null && can != CanvasContainingSelection) {
                    CanvasContainingSelection.ClearSelected();
                }
                CanvasContainingSelection = can;
                if(SelectedRectTracker.Mode == RectTracker.TrackMode.None) {
                    SelectedRectTracker.Move(new Rect(bound.Left + rect.X, bound.Top + rect.Y, rect.Width, rect.Height));
                } else if(SelectedRectTracker.Mode == RectTracker.TrackMode.Move) {
                    SelectedRectTracker.Move(new Point(bound.Left + rect.X, bound.Top + rect.Y));
                }
                SelectedRectTracker.MaxSize = bound;
                if(SelectedRectTracker.Visibility == Visibility.Hidden) {
                    SelectedRectTracker.Visibility = Visibility.Visible;
                    // 選択位置変更時はペンによる描画を抑制する．
                    foreach(var c in CanvasCollection) c.IsEnabled = false;
                }
            }
        }

        // 前回のRectTrackerのRectを保存しておく．Y座標は現在のCanvasのトップの分をひいておく．
        Rect SelectedRect;
        void SelectedRectTracker_TrackerEnd(object sender, RectTracker.TrackerEventArgs rect) {
            Rect hoseiRect = new Rect(rect.Rect.X - Canvas.GetLeft(CanvasContainingSelection), rect.Rect.Y - Canvas.GetTop(CanvasContainingSelection), rect.Rect.Width, rect.Rect.Height);
            SelectedRect = hoseiRect;
            CanvasContainingSelection.InkData.MoveSelected(SelectedRect, hoseiRect);
            CanvasContainingSelection.InkData.EndUndoGroup();
        }

        void SelectedRectTracker_TrackerSizeChanged(object sender, RectTracker.TrackerEventArgs rect) {
            Rect hoseiRect = new Rect(rect.Rect.X - Canvas.GetLeft(CanvasContainingSelection), rect.Rect.Y - Canvas.GetTop(CanvasContainingSelection), rect.Rect.Width, rect.Rect.Height);
            CanvasContainingSelection.InkData.MoveSelected(SelectedRect, hoseiRect);
            SelectedRect = hoseiRect;
        }

        void SelectedRectTracker_TrackerStart(object sender, RectTracker.TrackerEventArgs rect) {
            Rect hoseiRect = new Rect(rect.Rect.X - Canvas.GetLeft(CanvasContainingSelection), rect.Rect.Y - Canvas.GetTop(CanvasContainingSelection), rect.Rect.Width, rect.Rect.Height);
            SelectedRect = hoseiRect;
            CanvasContainingSelection.InkData.BeginUndoGroup();
        }

        void InkData_StrokeDeleted(abInkCanvas sender, abInkData.StrokeChangedEventArgs e) {
            if(CanvasContainingSelection != null && CanvasContainingSelection.Equals(sender)) {
                foreach(var s in e.Strokes) {
                    if(s.Selected){
						SetSelectedRectTracker(sender);
						return;
					}
                }
            }
        }

        void InkData_StrokeChanged(abInkCanvas sender, abInkData.StrokeChangedEventArgs e) {
            //System.Diagnostics.Debug.WriteLine("StrokeChanged");
            foreach(var s in e.Strokes) {
                if(s.Selected){
					SetSelectedRectTracker(sender);
					return;
				}
            }
        }

        void InkData_StrokeAdded(abInkCanvas sender, abInkData.StrokeChangedEventArgs e) {
            foreach(var s in e.Strokes) {
                if(s.Selected){
					SetSelectedRectTracker(sender);
					return;
				}
            }
        }

        void InkData_StrokeSelectedChanged(abInkCanvas sender, abInkData.StrokeChangedEventArgs e) {
            SetSelectedRectTracker(sender);
        }

        void SelectedRectTracker_MouseMove(object sender, MouseEventArgs e) {
            if(SelectedRectTracker.Mode == RectTracker.TrackMode.Move) {
                var pt = e.GetPosition(innerCanvas);
                //pt.Yの入っているキャンバスを特定
                int index = -1;
                for(int i = 0 ; i < CanvasCollection.Count ; ++i) {
                    double top = Canvas.GetTop(CanvasCollection[i]);
                    double bottom = top + CanvasCollection[i].Height;
                    if(pt.Y >= top && pt.Y <= bottom) {
                        index = i;
                        break;
                    }
                }
                if(index != -1 && !CanvasCollection[index].Equals(CanvasContainingSelection)) {
                    // 別のページに移動したので，選択を移動する．
                    CanvasContainingSelection.InkData.EndUndoGroup();
                    double shifty = Canvas.GetTop(CanvasContainingSelection) - Canvas.GetTop(CanvasCollection[index]);
                    var currentcanvas = CanvasContainingSelection;
                    var selstroke = currentcanvas.InkData.GetSelectedStrokes();
                    currentcanvas.InkData.DeleteSelected();
                    CanvasCollection[index].InkData.AddStroke(selstroke);
                    CanvasCollection[index].InkData.MoveSelected(new Rect(0, 0, 1, 1), new Rect(0, shifty, 1, 1));
                    CanvasCollection[index].InkData.BeginUndoGroup();
                    SelectedRect = new Rect(SelectedRect.X, SelectedRect.Y + shifty, SelectedRect.Width, SelectedRect.Height);
                }
            }
        }
        #endregion

        #region Undo/Redo関係
        interface UndoCommand {
            void Undo(abInkCanvasCollection icc);
            void Redo(abInkCanvasCollection icc);
        }
        class UndoGroup : UndoCommand {
            List<UndoCommand> Commands = new List<UndoCommand>();
            public int Count { get { return Commands.Count; } }
            public UndoGroup() { }
            public void Add(UndoCommand c) { Commands.Add(c); }
            public void Undo(abInkCanvasCollection data) {
                for(int i = Commands.Count - 1 ; i >= 0 ; --i) Commands[i].Undo(data);
            }
            public void Redo(abInkCanvasCollection data) {
                for(int i = 0 ; i < Commands.Count ; ++i) Commands[i].Redo(data);
            }
            // Commandも全部表示するようにしておく．Debug用．
            public override string ToString() {
                string rv = base.ToString() + "[";
                for(int i = 0 ; i < Commands.Count ; ++i) {
                    if(i == 0) rv += Commands[i].ToString();
                    else rv += " ; " + Commands[i].ToString();
                }
                return rv + "]";
            }
        }
        class CanvasUndoCommand : UndoCommand {
            public abInkCanvas InkCanvas;
            public CanvasUndoCommand(abInkCanvas c) { InkCanvas = c; }
            public void Undo(abInkCanvasCollection icc) { InkCanvas.Undo(); }
            public void Redo(abInkCanvasCollection icc) { InkCanvas.Redo(); }
        }
        class AddCanvasCommand : UndoCommand {
            abInkCanvas InkCanvas;
            int index;
            public AddCanvasCommand(abInkCanvas c, int i) { InkCanvas = c; index = i; }
            public void Undo(abInkCanvasCollection icc) {
                icc.CanvasCollection.RemoveAt(index);
                icc.innerCanvas.Children.Remove(InkCanvas);
                icc.VerticalArrangeCanvas();
            }
            public void Redo(abInkCanvasCollection icc) {
                icc.CanvasCollection.Insert(index, InkCanvas);
                icc.innerCanvas.Children.Add(InkCanvas);
                icc.VerticalArrangeCanvas();
            }
        }
        class DeleteCanvasCommand : UndoCommand {
            abInkCanvas InkCanvas;
            int index;
            public DeleteCanvasCommand(abInkCanvas c, int i) { InkCanvas = c; index = i; }
            public void Undo(abInkCanvasCollection icc) {
                icc.CanvasCollection.Insert(index, InkCanvas);
                icc.innerCanvas.Children.Add(InkCanvas);
                icc.VerticalArrangeCanvas();
            }
            public void Redo(abInkCanvasCollection icc) {
                icc.CanvasCollection.RemoveAt(index);
                icc.innerCanvas.Children.Remove(InkCanvas);
                icc.VerticalArrangeCanvas();
            }

        }

        List<UndoCommand> UndoStack = new List<UndoCommand>(), RedoStack = new List<UndoCommand>();
        int EditCount = 0;

        void InkCanvasCollection_UndoChainChanged(object sender, EventArgs e) {
            AddUndoChain(new CanvasUndoCommand((abInkCanvas) sender));
            OnUndoChainChanged(new UndoChainChangedEventArgs());
        }
        void AddUndoChain(UndoCommand c) {
            if(c is UndoGroup) {
                if(((UndoGroup) c).Count == 0) return;
            }
            RedoStack.Clear();
            UndoStack.Add(c);
            ++EditCount;
            OnUndoChainChanged(new UndoChainChangedEventArgs());
            if(UndoStack.Count > 1000) UndoStack.RemoveAt(0);
        }
        public void ClearUndoChain() {
            RedoStack.Clear();
            UndoStack.Clear();
            OnUndoChainChanged(new UndoChainChangedEventArgs());
        }
        public void Undo() {
            if(UndoStack.Count == 0) return;
            --EditCount;
            UndoStack.Last().Undo(this);
            RedoStack.Add(UndoStack.Last());
            UndoStack.RemoveAt(UndoStack.Count - 1);
            OnUndoChainChanged(new UndoChainChangedEventArgs());
        }
        public void Redo() {
            if(RedoStack.Count == 0) return;
            ++EditCount;
            RedoStack.Last().Redo(this);
            UndoStack.Add(RedoStack.Last());
            RedoStack.RemoveAt(RedoStack.Count - 1);
            OnUndoChainChanged(new UndoChainChangedEventArgs());
        }
        public bool CanUndo() {
            return (UndoStack.Count != 0);
        }
        public bool CanRedo() {
            return (RedoStack.Count != 0);
        }
        public class UndoChainChangedEventArgs : EventArgs { }
        public delegate void UndoChainChangedEventhandelr(object sender, UndoChainChangedEventArgs e);
        public event UndoChainChangedEventhandelr UndoChainChanged = ((sender, t) => { });
        protected virtual void OnUndoChainChanged(UndoChainChangedEventArgs e) {
            OnPropertyChanged("Updated");
            UndoChainChanged(this, e);
        }
        #endregion

        public void Delete() {
            foreach(var c in CanvasCollection) c.InkData.DeleteSelected();
        }
        public void SelectAll() {
            CanvasCollection[CurrentPage].InkData.SelectAll();
        }
        public void Paste() {
            CanvasCollection[CurrentPage].Paste();
        }
        public void Copy() {
            if(CanvasContainingSelection != null) CanvasContainingSelection.Copy();
        }
        public void Cut() {
            if(CanvasContainingSelection != null) CanvasContainingSelection.Cut();
        }
        public void ClearSelected() {
            if(CanvasContainingSelection != null) CanvasContainingSelection.ClearSelected();
        }
        public bool Updated {
            get {
                if(EditCount == 0) return false;
                else if(EditCount > 0) {
                    for(int i = UndoStack.Count - 1 ; i >= UndoStack.Count - EditCount && i >= 0 ; --i) {
                        if(!(UndoStack[i] is CanvasUndoCommand)) return true;
                        if(((CanvasUndoCommand) UndoStack[i]).InkCanvas.InkData.Updated) return true;
                    }
                    return false;
                } else {
                    for(int i = RedoStack.Count - 1 ; i >= RedoStack.Count + EditCount && i >= 0 ; --i) {
                        if(!(RedoStack[i] is CanvasUndoCommand)) return true;
                        if(((CanvasUndoCommand) RedoStack[i]).InkCanvas.InkData.Updated) return true;
                    }
                    return false;
                }
            }
        }
        public void ClearUpdated() {
            foreach(var c in CanvasCollection) c.InkData.ClearUpdated();
            EditCount = 0;
            OnPropertyChanged("Updated");
        }
        public void Clear() {
            foreach(var c in CanvasCollection) {
                innerCanvas.Children.Remove(c);
            }
            CanvasCollection.Clear();
        }

        int currentPage = -1;
        public int CurrentPage {
            get { return currentPage; }
            set { currentPage = value; OnPropertyChanged("CurrentPage"); }
        }

        void CalculateCurrentPage(bool callSetPort) {
            if(Count == 0) {
                currentPage = -1;
                return;
            }
            if(currentPage < 0) currentPage = 0;
            else if(currentPage >= Count) currentPage = Count - 1;

            var transform = innerCanvas.RenderTransform;
            var currentRect = transform.TransformBounds(new Rect(Canvas.GetLeft(CanvasCollection[currentPage]),Canvas.GetTop(CanvasCollection[currentPage]),CanvasCollection[currentPage].Width,CanvasCollection[currentPage].Height));
            int start, direction;
            if(currentRect.Top < 0) {
                start = currentPage;
                direction = 1;
            } else if(currentRect.Bottom > ActualHeight) {
                start = currentPage;
                direction = -1;
            } else {
                direction = 1;
                if(currentPage != 0) {
                    start = currentPage - 1;
                } else start = 0;
            }
            int rv = start;
            double maxh = 0;
            if(callSetPort) {
                for(int i = start - direction ; i >= 0 && i < Count ; i -= direction) {
                    CanvasCollection[i].SetViewport(new Rect());
                }
            }
            if(direction == 1) {
                int i;
                for(i = start ; i < Count ; ++i) {
                    var c = CanvasCollection[i];
                    var rect = transform.TransformBounds(new Rect(Canvas.GetLeft(c), Canvas.GetTop(c), c.Width, c.Height));
                    double top = Canvas.GetTop(c);
                    double h;
                    if(rect.Bottom < 0) {// まだ画面外
                        h = 0;
                        if(callSetPort) c.SetViewport(new Rect());
                    } else if(rect.Top > ActualHeight) {// もう画面外
                        if(callSetPort) c.SetViewport(new Rect());
                        break;
                    } else {
                        var viewRect = new Rect(new Point(Math.Max(0, rect.Left), Math.Max(0, rect.Top)), new Point(Math.Min(ActualWidth, rect.Right), Math.Min(ActualHeight, rect.Bottom)));
                        h = viewRect.Height;
                        if(callSetPort) {
                            var topleft = transform.Inverse.Transform(rect.TopLeft);
                            var viewport = transform.Inverse.TransformBounds(viewRect);
                            c.SetViewport(new Rect(viewport.Left - topleft.X, viewport.Top - topleft.Y, viewport.Width, viewport.Height));
                        }
                    }
                    if(maxh < h) {
                        maxh = h;
                        rv = i;
                    }
                }
                if(callSetPort){
                    for( ; i < Count ; ++i) CanvasCollection[i].SetViewport(new Rect());
                }
            } else {
                int i;
                for(i = start ; i >= 0 ; --i) {
                    var c = CanvasCollection[i];
                    var rect = transform.TransformBounds(new Rect(Canvas.GetLeft(c), Canvas.GetTop(c), c.Width, c.Height));
                    double h;
                    if(rect.Top > ActualHeight) {// まだ画面外
                        h = 0;
                        if(callSetPort) c.SetViewport(new Rect());
                    } else if(rect.Bottom < 0) {// もう画面外
                        if(callSetPort) c.SetViewport(new Rect());
                        break;
                    } else {
                        var viewRect = new Rect(new Point(Math.Max(0, rect.Left), Math.Max(0, rect.Top)), new Point(Math.Min(ActualWidth, rect.Right), Math.Min(ActualHeight, rect.Bottom)));
                        h = viewRect.Height;
                        if(callSetPort) {
                            var topleft = transform.Inverse.Transform(rect.TopLeft);
                            var viewport = transform.Inverse.TransformBounds(viewRect);
                            c.SetViewport(new Rect(viewport.Left - topleft.X, viewport.Top - topleft.Y, viewport.Width, viewport.Height));
                        }
                    }
                    if(maxh < h) {
                        maxh = h;
                        rv = i;
                    }
                }
                if(callSetPort) {
                    for( ; i >=0 ; --i) CanvasCollection[i].SetViewport(new Rect());
                }
            }
            currentPage = rv;
            OnPropertyChanged("CurrentPage");
        }

        /*
        public int CurrentPage {
            get {
                if(Count == 0) return -1;

                int rv = 0;
                double maxval = -10;
                const double x = 0;
                double y = 0;
                for(int i = 0 ; i < CanvasCollection.Count ; ++i) {
                    Point lefttop = innerCanvas.RenderTransform.Transform(new Point(x, y));
                    if(lefttop.Y > ActualHeight) break;
                    double height = CanvasCollection[i].Height;
                    double h;
                    if(lefttop.Y + height < 0 || lefttop.Y > ActualHeight) h = 0;//画面外
                    else h = Math.Min(ActualHeight, lefttop.Y + height) - Math.Max(0, lefttop.Y);
                    if(maxval < h) {
                        maxval = h;
                        rv = i;
                    }
                    y += CanvasCollection[i].Height + LengthBetweenCanvas;
                }
                return rv;
            }
            set {
                if(CurrentPage == value) return;
                if(value < 0 || value >= CanvasCollection.Count) return;
                double y = 0;
                for(int i = 0 ; i < value ; ++i) {
                    y += LengthBetweenCanvas + CanvasCollection[i].Height;
                }
                var trans = (MatrixTransform) innerCanvas.RenderTransform;
                Point pt = trans.Transform(new Point(0, y));
                // pt.Y --> 0となるように変換する．
                Matrix m = trans.Matrix;
                m.Translate(0, -pt.Y);
                trans.Matrix = m;
                OnPropertyChanged("CurrentPage");
            }
        }*/

        /*
        void ForDebugPtsDrwaing(PointCollection stc, Brush brush) {
            int i = 0;
            foreach(var pt in stc) {
                var ell = new System.Windows.Shapes.Ellipse() {
                    Width = 1,
                    Height = 1,
                    Fill = brush
                };
                SetLeft(ell, pt.X - 0.5);
                SetTop(ell, pt.Y - 0.5);
                CanvasCollection[0].StrokeChildren.Add(ell);
                if(i == 2000) break;
                ++i;
            }
        }*/

        public IEnumerable<abInkCanvas> GetInkCanvases(DrawingAlgorithm algo) {
            for(int i = 0 ; i < Count ; ++i) {
                var c = CanvasCollection[i].Clone();
                c.InkData.DrawingAlgorithm = algo;
                c.ReDraw();
                yield return c;
            }
        }

        public IEnumerator<abInkCanvas> GetEnumerator() {
            return CanvasCollection.GetEnumerator();
        }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return CanvasCollection.GetEnumerator();
        }
    }


    public class Stopwatch {
        public System.Diagnostics.Stopwatch watch;
        public Stopwatch() { watch = System.Diagnostics.Stopwatch.StartNew(); }
        public void CheckTime(string str) {
            watch.Stop();
            System.Diagnostics.Debug.WriteLine(str + "： " + watch.Elapsed.ToString());
            //MessageBox.Show(str + "： " + watch.Elapsed.ToString());
            watch.Reset();
            watch.Start();
        }
    }

}