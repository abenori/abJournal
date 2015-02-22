using System;
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
using ablib;

namespace ablib {
    // InkCanvasたちからなる「文書用」
    // InkCanvasの適切な配置とかを担当．
    // 継承しているCanvasの中にもう一つCanvas（innerCanvas）を置き，その中にablib.InkCanvasを並べる．
    public class InkCanvasCollection : Canvas, IEnumerable<ablib.InkCanvas> {
        List<ablib.InkCanvas> CanvasCollection = new List<ablib.InkCanvas>();
        Canvas innerCanvas = new Canvas();
        const double sukima = 100;
        const double LengthBetweenCanvas = 10;
        #region 公開用のプロパティ
        string filename = "";
        public string FileName { get { return filename; } }

        [ProtoContract(SkipConstructor = true)]
        public class CanvasCollectionInfo {
            public CanvasCollectionInfo() {
                InkCanvasInfo = new InkCanvas.InkCanvasInfo();
                InkCanvasInfo.Size = Paper.GetSize(Paper.PaperSize.A4);
                Date = DateTime.Now;
                //ShowDate = true;
                //ShowTitle = true;
            }
            [ProtoMember(1)]
            public DateTime Date { get; set; }
            [ProtoMember(2)]
            public bool ShowDate { get; set; }
            [ProtoMember(3)]
            public bool ShowTitle { get; set; }
            [ProtoMember(4)]
            public string Title { get; set; }
            [ProtoMember(5)]
            public InkCanvas.InkCanvasInfo InkCanvasInfo { get; set; }

            public CanvasCollectionInfo DeepCopy() {
                CanvasCollectionInfo rv = new CanvasCollectionInfo();
                rv.Date = Date;
                rv.ShowDate = ShowDate;
                rv.ShowTitle = ShowTitle;
                rv.InkCanvasInfo = InkCanvasInfo.DeepCopy();
                if(Title != null) rv.Title = (string) Title.Clone();
                return rv;
            }
        }
        DrawingAlgorithm drawingAlgorithm = DrawingAlgorithm.dotNet;
        public DrawingAlgorithm DrawingAlgorithm {
            get { return drawingAlgorithm; }
            set {
                drawingAlgorithm = value;
                foreach(var c in CanvasCollection) c.InkData.DrawingAlgorithm = value;
                ReDraw();
            }
        }
        public CanvasCollectionInfo info;
        public CanvasCollectionInfo Info {
            get { return info; }
            set {
                info = value;
                if(Count > 0) {
                    DrawNoteContents(CanvasCollection[0], info);
                }
            }
        }

        InkManipulationMode mode;
        public InkManipulationMode Mode {
            get { return mode; }
            set {
                mode = value;
                foreach(var i in CanvasCollection) i.Mode = Mode;
            }
        }
        Color penColor;
        public Color PenColor {
            get { return penColor; }
            set {
                penColor = value;
                foreach(var c in CanvasCollection) c.PenColor = penColor;
            }
        }
        double penThickness;
        public double PenThickness {
            get { return penThickness; }
            set {
                penThickness = value;
                foreach(var c in CanvasCollection) c.PenThickness = penThickness;
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
            }
        }

        double scale = 1;
        public double Scale {
            get { return scale; }
            set {
                Matrix m = ((MatrixTransform) innerCanvas.RenderTransform).Matrix;
                m.Scale(value / scale, value / scale);
                ((MatrixTransform) innerCanvas.RenderTransform).Matrix = m;
                Scroll();
                scale = value;
            }
        }
        bool ignorePressure = true;
        public bool IgnorePressure {
            get { return ignorePressure; }
            set {
                ignorePressure = value;
                foreach(var c in CanvasCollection) c.IgnorePressure = value;
                ReDraw();
            }
        }

        public new Brush Background {
            get { return base.Background; }
            set { base.Background = value; innerCanvas.Background = value; }
        }

        public int Count {
            get { return CanvasCollection.Count; }
        }
        public InkCanvas this[int i] {
            get { return CanvasCollection[i]; }
        }
        #endregion

        #region 選択
        RectTracker SelectedRectTracker = new RectTracker();
        InkCanvas CanvasContainingSelection = null;
        #endregion

        public InkCanvasCollection() {
            SizeChanged += InkCanvasCollection_SizeChanged;

            Info = new CanvasCollectionInfo() { ShowDate = true, ShowTitle = true };
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

        public void AddCanvas() { AddCanvas(new InkData(), Info.InkCanvasInfo); }
        public void AddCanvas(InkData d, InkCanvas.InkCanvasInfo info) {
            InsertCanvas(d, info, CanvasCollection.Count);
        }
        public void InsertCanvas(int index) {
            InsertCanvas(new InkData(), Info.InkCanvasInfo, index);
        }

        public void InsertCanvas(InkData d, InkCanvas.InkCanvasInfo canvasinfo, int index) {
            ablib.InkCanvas canvas = new ablib.InkCanvas(d, canvasinfo.Size.Width, canvasinfo.Size.Height);
            canvas.IgnorePressure = ignorePressure;
            canvas.BackGroundColor = canvasinfo.BackGround;
            canvas.Mode = Mode;
            canvas.VerticalRule = canvasinfo.VerticalRule.DeepCopy();
            canvas.HorizontalRule = canvasinfo.HorizontalRule.DeepCopy();

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
            Canvas.SetLeft(canvas, -canvasinfo.Size.Width/2);
            //Scale = 12;
            if(index == 0) DrawNoteContents(canvas, Info);
            DrawRules(canvas, canvasinfo.HorizontalRule, canvasinfo.VerticalRule, (index == 0) && Info.ShowTitle);
        }

        public void DeleteCanvas(int index) {
            if(index < 0 || index >= CanvasCollection.Count) return;
            InkCanvas ic = CanvasCollection[index];
            CanvasCollection.RemoveAt(index);
            innerCanvas.Children.Remove(ic);
            AddUndoChain(new DeleteCanvasCommand(ic, index));
            VerticalArrangeCanvas();
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


        void SetSelectedRectTracker(InkCanvas can) {
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

        void InkData_StrokeDeleted(InkCanvas sender, InkData.StrokeChangedEventArgs e) {
            if(CanvasContainingSelection != null && CanvasContainingSelection.Equals(sender)) {
                foreach(var s in e.Strokes) {
                    if(s.Selected){
						SetSelectedRectTracker(sender);
						return;
					}
                }
            }
        }

        void InkData_StrokeChanged(InkCanvas sender, InkData.StrokeChangedEventArgs e) {
            //System.Diagnostics.Debug.WriteLine("StrokeChanged");
            foreach(var s in e.Strokes) {
                if(s.Selected){
					SetSelectedRectTracker(sender);
					return;
				}
            }
        }

        void InkData_StrokeAdded(InkCanvas sender, InkData.StrokeChangedEventArgs e) {
            foreach(var s in e.Strokes) {
                if(s.Selected){
					SetSelectedRectTracker(sender);
					return;
				}
            }
        }

        void InkData_StrokeSelectedChanged(InkCanvas sender, InkData.StrokeChangedEventArgs e) {
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
                    var selstroke = currentcanvas.InkData.GetSelectedStrkes();
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
            void Undo(InkCanvasCollection icc);
            void Redo(InkCanvasCollection icc);
        }
        class UndoGroup : UndoCommand {
            List<UndoCommand> Commands = new List<UndoCommand>();
            public int Count { get { return Commands.Count; } }
            public UndoGroup() { }
            public void Add(UndoCommand c) { Commands.Add(c); }
            public void Undo(InkCanvasCollection data) {
                for(int i = Commands.Count - 1 ; i >= 0 ; --i) Commands[i].Undo(data);
            }
            public void Redo(InkCanvasCollection data) {
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
            public ablib.InkCanvas InkCanvas;
            public CanvasUndoCommand(ablib.InkCanvas c) { InkCanvas = c; }
            public void Undo(InkCanvasCollection icc) { InkCanvas.Undo(); }
            public void Redo(InkCanvasCollection icc) { InkCanvas.Redo(); }
        }
        class AddCanvasCommand : UndoCommand {
            ablib.InkCanvas InkCanvas;
            int index;
            public AddCanvasCommand(ablib.InkCanvas c, int i) { InkCanvas = c; index = i; }
            public void Undo(InkCanvasCollection icc) {
                icc.CanvasCollection.RemoveAt(index);
                icc.innerCanvas.Children.Remove(InkCanvas);
                icc.VerticalArrangeCanvas();
            }
            public void Redo(InkCanvasCollection icc) {
                icc.CanvasCollection.Insert(index, InkCanvas);
                icc.innerCanvas.Children.Add(InkCanvas);
                icc.VerticalArrangeCanvas();
            }
        }
        class DeleteCanvasCommand : UndoCommand {
            ablib.InkCanvas InkCanvas;
            int index;
            public DeleteCanvasCommand(ablib.InkCanvas c, int i) { InkCanvas = c; index = i; }
            public void Undo(InkCanvasCollection icc) {
                icc.CanvasCollection.Insert(index, InkCanvas);
                icc.innerCanvas.Children.Add(InkCanvas);
                icc.VerticalArrangeCanvas();
            }
            public void Redo(InkCanvasCollection icc) {
                icc.CanvasCollection.RemoveAt(index);
                icc.innerCanvas.Children.Remove(InkCanvas);
                icc.VerticalArrangeCanvas();
            }

        }

        List<UndoCommand> UndoStack = new List<UndoCommand>(), RedoStack = new List<UndoCommand>();
        int EditCount = 0;

        void InkCanvasCollection_UndoChainChanged(object sender, EventArgs e) {
            AddUndoChain(new CanvasUndoCommand((ablib.InkCanvas) sender));
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
        }

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
            }
        }

        [ProtoContract]
        public class ablibInkCanvasCollectionSavingProtobufData {
            [ProtoContract(SkipConstructor = true)]
            public class CanvasData {
                public CanvasData(InkData d, InkCanvas.InkCanvasInfo i) {
                    Data = d;
                    Info = i.DeepCopy();
                }
                [ProtoMember(1)]
                public InkData Data;
                [ProtoMember(2)]
                public InkCanvas.InkCanvasInfo Info;
            }
            [ProtoMember(1)]
            public List<CanvasData> Data { get; set; }
            [ProtoMember(2)]
            public CanvasCollectionInfo Info { get; set; }
            public ablibInkCanvasCollectionSavingProtobufData() {
                Data = new List<CanvasData>();
                Info = new CanvasCollectionInfo();
            }
        }

        public class ablibInkCanvasCollectionSavingData {
            public class CanvasData {
                public CanvasData() {
                    Data = new ablib.Saving.InkData();
                    Info = new InkCanvas.InkCanvasInfo();
                }
                public CanvasData(ablib.Saving.InkData d, InkCanvas.InkCanvasInfo i) {
                    Data = d;
                    Info = i.DeepCopy();
                }
                public ablib.Saving.InkData Data;
                public InkCanvas.InkCanvasInfo Info;
            }
            public List<CanvasData> Data { get; set; }
            public CanvasCollectionInfo Info { get; set; }
            public ablibInkCanvasCollectionSavingData() {
                Data = new List<CanvasData>();
                Info = new CanvasCollectionInfo();
            }
        }
        public static string GetSchema() {
            return InkData.SetProtoBufTypeModel(ProtoBuf.Meta.TypeModel.Create()).GetSchema(typeof(ablibInkCanvasCollectionSavingProtobufData));
        }
        public void Save() {
            Save(FileName);
        }
        public void Save(string file) {
            ablibInkCanvasCollectionSavingProtobufData data = new ablibInkCanvasCollectionSavingProtobufData();
            foreach(var c in CanvasCollection) {
                data.Data.Add(new ablibInkCanvasCollectionSavingProtobufData.CanvasData(c.InkData, c.Info));
            }
            data.Info = Info;
            var model = InkData.SetProtoBufTypeModel(ProtoBuf.Meta.TypeModel.Create());
            using(var wfs = new System.IO.FileStream(file, System.IO.FileMode.Create)) {
                //using(var zs = new System.IO.Compression.GZipStream(wfs, System.IO.Compression.CompressionLevel.Optimal)) {
                model.Serialize(wfs, data);
            }
            filename = file;
        }

        public void SavePDF(string file) {
			double scale = (double) 720 / (double) 254 / Paper.mmToSize;
            using(var doc = new PdfSharp.Pdf.PdfDocument()) {
                for(int i = 0 ; i < Count ; ++i) {
                    var page = doc.AddPage();
                    var ps = Paper.GetPaperSize(new Size(CanvasCollection[i].Width, CanvasCollection[i].Height));
                    // 1 = 1/72インチ = 25.4/72 mm
                    switch(ps) {
                    case Paper.PaperSize.A0: page.Size = PdfSharp.PageSize.A0; break;
                    case Paper.PaperSize.A1: page.Size = PdfSharp.PageSize.A1; break;
                    case Paper.PaperSize.A2: page.Size = PdfSharp.PageSize.A2; break;
                    case Paper.PaperSize.A3: page.Size = PdfSharp.PageSize.A3; break;
                    case Paper.PaperSize.A4: page.Size = PdfSharp.PageSize.A4; break;
                    case Paper.PaperSize.A5: page.Size = PdfSharp.PageSize.A5; break;
                    case Paper.PaperSize.B0: page.Size = PdfSharp.PageSize.B0; break;
                    case Paper.PaperSize.B1: page.Size = PdfSharp.PageSize.B1; break;
                    case Paper.PaperSize.B2: page.Size = PdfSharp.PageSize.B2; break;
                    case Paper.PaperSize.B3: page.Size = PdfSharp.PageSize.B3; break;
                    case Paper.PaperSize.B4: page.Size = PdfSharp.PageSize.B4; break;
                    case Paper.PaperSize.B5: page.Size = PdfSharp.PageSize.B5; break;
                    case Paper.PaperSize.Letter: page.Size = PdfSharp.PageSize.Letter; break;
                    case Paper.PaperSize.Tabloid: page.Size = PdfSharp.PageSize.Tabloid; break;
                    case Paper.PaperSize.Ledger: page.Size = PdfSharp.PageSize.Ledger; break;
                    case Paper.PaperSize.Legal: page.Size = PdfSharp.PageSize.Legal; break;
                    case Paper.PaperSize.Folio: page.Size = PdfSharp.PageSize.Folio; break;
                    case Paper.PaperSize.Quarto: page.Size = PdfSharp.PageSize.Quarto; break;
                    case Paper.PaperSize.Executive: page.Size = PdfSharp.PageSize.Executive; break;
                    case Paper.PaperSize.Statement: page.Size = PdfSharp.PageSize.Statement; break;
                    case Paper.PaperSize.Other:
                        page.Width = CanvasCollection[i].Width * scale;
                        page.Height = CanvasCollection[i].Height * scale;
                        break;
                    default:
                        var s = Paper.GetmmSize(ps);
                        page.Width = s.Width * 720 / 254;
                        page.Height = s.Height * 720 / 254;
                        break;
                    }
                    var g = PdfSharp.Drawing.XGraphics.FromPdfPage(page);
                    g.ScaleTransform(scale);
                    if(i == 0) DrawNoteContents(g, CanvasCollection[i], Info);
                    DrawRules(g, CanvasCollection[i], (i == 0 && Info.ShowTitle));
                    CanvasCollection[i].InkData.AddPdfGraphic(g);
                }
                doc.Info.Creator = "abJournal";
                doc.Info.Title = Info.Title;
                doc.Info.CreationDate = Info.Date;
                doc.Info.ModificationDate = DateTime.Now;
                doc.Save(new System.IO.FileStream(file, System.IO.FileMode.Create));
            }
        }

        public void ReDraw() {
            foreach(var c in CanvasCollection) c.ReDraw();
            if(CanvasCollection.Count > 0) DrawNoteContents(CanvasCollection[0], Info);
            for(int i = 0 ; i < Count ; ++i) {
                DrawRules(CanvasCollection[i], CanvasCollection[i].HorizontalRule, CanvasCollection[i].VerticalRule, (i == 0 && Info.ShowTitle));
            }
        }

        public void Open(string file) {
            var watch = new Stopwatch();
            using(var fs = new System.IO.FileStream(file, System.IO.FileMode.Open)) {
                var model = InkData.SetProtoBufTypeModel(ProtoBuf.Meta.TypeModel.Create());
                ablibInkCanvasCollectionSavingProtobufData protodata = null;
                // protobufデシリアライズ
                try { protodata = (ablibInkCanvasCollectionSavingProtobufData) model.Deserialize(fs, new ablibInkCanvasCollectionSavingProtobufData(), typeof(ablibInkCanvasCollectionSavingProtobufData)); }
                catch(Exception e) { System.Diagnostics.Debug.WriteLine(e.Message); }
                // zip解凍＋protobufデシリアライズ
                if(protodata == null) {
                    using(var zs = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress)) {
                        try { protodata = (ablibInkCanvasCollectionSavingProtobufData) model.Deserialize(zs, new ablibInkCanvasCollectionSavingProtobufData(), typeof(ablibInkCanvasCollectionSavingProtobufData)); }
                        catch(Exception e) { System.Diagnostics.Debug.WriteLine(e.Message); }
                    }
                }
                if(protodata != null) {
                    foreach(var c in CanvasCollection) innerCanvas.Children.Remove(c);
                    CanvasCollection.Clear();
                    foreach(var d in protodata.Data) {
                        d.Data.DrawingAlgorithm = DrawingAlgorithm;
                        AddCanvas(d.Data, d.Info);
                    }
                    Info = protodata.Info;
                } else {
                    System.Diagnostics.Debug.WriteLine("Protobufデシリアライズでエラー");
                    var xml = new System.Xml.Serialization.XmlSerializer(typeof(ablibInkCanvasCollectionSavingData));
                    ablibInkCanvasCollectionSavingData data = null;
                    // gzip解凍+XMLデシリアライズ
                    using(var zs = new System.IO.Compression.GZipStream(fs, System.IO.Compression.CompressionMode.Decompress)) {
                        try { data = (ablibInkCanvasCollectionSavingData) xml.Deserialize(zs); }
                        catch(System.IO.InvalidDataException) { }

                    }
                    // 単なるXMLデシリアライズ
                    if(data == null) {
                        data = (ablibInkCanvasCollectionSavingData) xml.Deserialize(fs);
                    }

                    foreach(var c in CanvasCollection) innerCanvas.Children.Remove(c);
                    CanvasCollection.Clear();
                    foreach(var d in data.Data) {
                        InkData id = new InkData();
                        id.LoadSavingData(d.Data);
                        id.DrawingAlgorithm = DrawingAlgorithm;
                        AddCanvas(id, d.Info);
                    }
                    Info = data.Info;
                }
            }
            if(Count == 0) AddCanvas();
            filename = file;
            ClearUpdated();
            ClearUndoChain();
            DrawNoteContents(CanvasCollection[0], Info);
            watch.CheckTime("Openにかかった時間");
            //CanvasCollection[0].ReDraw();
            //foreach(var str in CanvasCollection[0].InkData.Strokes) {
            //ForDebugPtsDrwaing(new PointCollection(str.StylusPoints.Where(s => true).Select(p => p.ToPoint())), Brushes.Red);
            //}
            //ForDebugPtsDrwaing(new PointCollection(StrokeData.HoseiPts.Select(p => p.ToPoint())), Brushes.Blue);
            //ForDebugPtsDrwaing(StrokeData.CuspPts, Brushes.Green);
            //Scale = 13;
        }

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
                CanvasCollection[0].Children.Add(ell);
                if(i == 2000) break;
                ++i;
            }
        }


        #region タイトルとか描くやつ（PDF含）
        static Dictionary<InkCanvas, List<Visual>> NoteContentsElements = new Dictionary<InkCanvas, List<Visual>>();
        static void GetYohakuHankei(InkCanvas c, out double xyohaku, out double yyohaku, out double titleheight, out double hankei) {
            xyohaku = c.Width * 0.03;
            yyohaku = c.Width * 0.03;
            titleheight = c.Height * 0.06;
            hankei = c.Width * 0.02;
        }

        public static void DrawNoteContents(InkCanvas c, CanvasCollectionInfo info) {
            if(NoteContentsElements.ContainsKey(c)) {
                foreach(var shape in NoteContentsElements[c]) {
                    c.Children.Remove(shape);
                }
                NoteContentsElements.Remove(c);
            }
            NoteContentsElements.Add(c, new List<Visual>());
            var rv = new DrawingVisual();
            using(var dc = rv.RenderOpen()) {
                double xyohaku, yyohaku, height, hankei;
                GetYohakuHankei(c, out xyohaku, out yyohaku, out height, out hankei);
                if(info.ShowTitle) {
                    dc.DrawRoundedRectangle(null, new Pen(Brushes.LightGray, 1), new Rect(xyohaku, yyohaku, c.Width - 2 * xyohaku, height + 2*hankei), hankei, hankei);

                    if(info.Title != null && info.Title != "") {
                        double width = c.Width - 2 * xyohaku - 2 * hankei;
                        var pt = new Point(xyohaku + hankei, yyohaku + hankei / 2);
                        double fontsize = GuessFontSize(info.Title, "游ゴシック", width,height);
                        var text = new FormattedText(info.Title, System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("游ゴシック"), fontsize, Brushes.Black);
                        text.MaxTextWidth = width;
                        text.MaxTextHeight = GetStringSize(info.Title, "游ゴシック", fontsize).Height;
                        int n = (int) (height / text.MaxTextHeight);
                        pt.Y += (height - n * text.MaxTextHeight) / 2;
                        text.MaxTextHeight += n;
                        dc.DrawText(text, pt);
                    }

                    if(info.ShowDate) {
                        var text = new FormattedText(info.Date.ToLongDateString(), System.Globalization.CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface("游ゴシック"), 12, Brushes.Gray);
                        dc.DrawText(text, new Point(c.Width - xyohaku - text.Width - hankei,yyohaku + 2 * hankei + height - text.Height - 4));

                        var pen = new Pen(Brushes.LightGray,1);
                        pen.DashStyle = new DashStyle(new double[]{3,3},0);
                        dc.DrawLine(pen,
                            new Point(xyohaku + hankei, yyohaku + 2 * hankei + height - text.Height - 8),
                            new Point(c.Width - xyohaku - hankei, yyohaku + 2 * hankei + height - text.Height - 8)
                            );
                    }
                }
            }
            NoteContentsElements[c].Add(rv);
            c.Children.Insert(0, rv);
        }

        static Dictionary<InkCanvas, List<UIElement>> RuleElements = new Dictionary<InkCanvas, List<UIElement>>();

        public static void DrawRules(InkCanvas c, InkCanvas.Rule Horizontal, InkCanvas.Rule Vertical, bool showTitle) {
            if(RuleElements.ContainsKey(c)) {
                foreach(var shape in RuleElements[c]) {
                    c.Children.Remove(shape);
                }
                RuleElements.Remove(c);
            }
            double xyohaku, yyohaku, height, hankei;
            GetYohakuHankei(c, out xyohaku, out yyohaku, out height, out hankei);
            if(Horizontal.Show) {
                double d = Horizontal.Interval;
                if(showTitle && !Horizontal.Show) d += yyohaku + height + 2 * hankei;
                for( ; d < c.Height ; d += Horizontal.Interval) {
                    var brush = new SolidColorBrush(Horizontal.Color);
                    if(showTitle && yyohaku < d && d < yyohaku + height) {
                        var l = new System.Windows.Shapes.Line() {
                            X1 = 0,
                            Y1 = d,
                            X2 = xyohaku,
                            Y2 = d,
                            Stroke = brush,
                            StrokeThickness = Horizontal.Thickness,
                            StrokeDashArray = Horizontal.DashArray
                        };
                        c.Children.Add(l);
                        NoteContentsElements[c].Add(l);
                        l = new System.Windows.Shapes.Line() {
                            X1 = c.Width - xyohaku,
                            Y1 = d,
                            X2 = c.Width,
                            Y2 = d,
                            Stroke = brush,
                            StrokeThickness = Horizontal.Thickness,
                            StrokeDashArray = Horizontal.DashArray
                        };
                        c.Children.Add(l);
                        RuleElements[c].Add(l);
                    } else {
                        var l = new System.Windows.Shapes.Line() {
                            X1 = 0,
                            Y1 = d,
                            X2 = c.Width,
                            Y2 = d,
                            Stroke = brush,
                            StrokeThickness = Horizontal.Thickness,
                            StrokeDashArray = Horizontal.DashArray
                        };
                        c.Children.Add(l);
                        RuleElements[c].Add(l);
                    }
                }
            }
            if(Vertical.Show) {
                var brush = new SolidColorBrush(Vertical.Color);
                for(double d = Vertical.Interval ; d < c.Width ; d += Vertical.Interval) {
                    if(showTitle && xyohaku < d && d < c.Width - xyohaku) {
                        var l = new System.Windows.Shapes.Line() {
                            X1 = d,
                            Y1 = 0,
                            X2 = d,
                            Y2 = yyohaku,
                            Stroke = brush,
                            StrokeThickness = Vertical.Thickness,
                            StrokeDashArray = Vertical.DashArray
                        };
                        c.Children.Add(l);
                        RuleElements[c].Add(l);
                        l = new System.Windows.Shapes.Line() {
                            X1 = d,
                            Y1 = yyohaku + height + 2 * hankei,
                            X2 = d,
                            Y2 = c.Height,
                            Stroke = brush,
                            StrokeThickness = Vertical.Thickness,
                            StrokeDashArray = Vertical.DashArray
                        };
                        c.Children.Add(l);
                        RuleElements[c].Add(l);
                    } else {
                        var l = new System.Windows.Shapes.Line() {
                            X1 = d,
                            Y1 = 0,
                            X2 = d,
                            Y2 = c.Height,
                            Stroke = brush,
                            StrokeThickness = Vertical.Thickness,
                            StrokeDashArray = Vertical.DashArray
                        };
                        c.Children.Add(l);
                        RuleElements[c].Add(l);
                    }
                }
            }
            if(RuleElements.ContainsKey(c)) {
                foreach(var s in RuleElements[c]) {
                    SetZIndex(s, -1);
                }
            }
        }

        public static void DrawNoteContents(PdfSharp.Drawing.XGraphics g, InkCanvas c, CanvasCollectionInfo info) {
            var pdf_ja_font_options = new PdfSharp.Drawing.XPdfFontOptions(PdfSharp.Pdf.PdfFontEncoding.Unicode, PdfSharp.Pdf.PdfFontEmbedding.Always);

            double xyohaku, yyohaku, height, hankei;
            GetYohakuHankei(c, out xyohaku, out yyohaku, out height, out hankei);
            var titlePath = new PdfSharp.Drawing.XGraphicsPath();
            if(info.ShowTitle) {
                titlePath.StartFigure();
                //titlePath.AddLine(xyohaku + hankei,yyohaku,c.Width - xyohaku - hankei, yyohaku);
                titlePath.AddArc(c.Width - xyohaku - hankei, yyohaku, hankei, hankei, 270, 90);
                titlePath.AddLine(c.Width - xyohaku, yyohaku + hankei, c.Width - xyohaku, yyohaku + hankei + height);
                titlePath.AddArc(c.Width - xyohaku - hankei, yyohaku + hankei + height, hankei, hankei, 0, 90);
                titlePath.AddLine(c.Width - xyohaku - hankei, yyohaku + 2 * hankei + height, xyohaku + hankei, yyohaku + 2 * hankei + height);
                titlePath.AddArc(xyohaku, yyohaku + height + hankei, hankei, hankei, 90, 90);
                titlePath.AddLine(xyohaku, yyohaku + hankei + height, xyohaku, yyohaku + hankei);
                titlePath.AddArc(xyohaku, yyohaku, hankei, hankei, 180, 90);
                titlePath.CloseFigure();
                g.DrawPath(PdfSharp.Drawing.XPens.LightGray, titlePath);

                if(info.Title != null && info.Title != "") {
                    var rect = new PdfSharp.Drawing.XRect(xyohaku + hankei, yyohaku + hankei / 2, c.Width - 2 * xyohaku - 2 * hankei, height);
                    double fontsize = GuessFontSize(info.Title, "游ゴシック", rect.Width, rect.Height);
                    // 真ん中に配置するための座標計算
                    double textHeight = GetStringSize(info.Title, "游ゴシック", fontsize).Height;
                    int n = (int) (rect.Height / textHeight);
                    rect.Y += (rect.Height - n * textHeight) / 2;
                    rect.Height = n * textHeight;
                    var pdf_ja_font = new PdfSharp.Drawing.XFont("游ゴシック", fontsize, PdfSharp.Drawing.XFontStyle.Regular, pdf_ja_font_options);
                    var tf = new PdfSharp.Drawing.Layout.XTextFormatter(g);
                    tf.DrawString(info.Title, pdf_ja_font, PdfSharp.Drawing.XBrushes.Black, rect, PdfSharp.Drawing.XStringFormats.TopLeft);
                }

                if(info.ShowDate) {
                    var textSize = GetStringSize(info.Date.ToLongDateString(), "游ゴシック", 12);
                    var pdf_ja_font = new PdfSharp.Drawing.XFont("游ゴシック", 12, PdfSharp.Drawing.XFontStyle.Regular, pdf_ja_font_options);
                    g.DrawString(
                        info.Date.ToLongDateString(),
                        pdf_ja_font,
                        PdfSharp.Drawing.XBrushes.Gray,
                        c.Width - xyohaku - textSize.Width - hankei,
                        yyohaku + 2 * hankei + height - textSize.Height - 4,
                        PdfSharp.Drawing.XStringFormats.TopLeft);

                    var pen = new PdfSharp.Drawing.XPen(PdfSharp.Drawing.XColors.LightGray, 1);
                    pen.DashPattern = new double[] { 3, 3 };
                    g.DrawLine(pen,
                        xyohaku + hankei,
                        yyohaku + 2 * hankei + height - textSize.Height - 8,
                        c.Width - xyohaku - hankei - 1,// 破線がかっこ悪いので調整
                        yyohaku + 2 * hankei + height - textSize.Height - 8);
                }
            }
        }

        public static void DrawRules(PdfSharp.Drawing.XGraphics g, InkCanvas c, bool showTitle) {
            double xyohaku, yyohaku, height, hankei;
            GetYohakuHankei(c, out xyohaku, out yyohaku, out height, out hankei);
            if(c.HorizontalRule.Show) {
                double d = c.HorizontalRule.Interval;
                if(showTitle && !c.HorizontalRule.Show) d += yyohaku + height + 2 * hankei;
                var pen = new PdfSharp.Drawing.XPen(PdfSharp.Drawing.XColor.FromArgb(
                    c.HorizontalRule.Color.A,
                    c.HorizontalRule.Color.R,
                    c.HorizontalRule.Color.G,
                    c.HorizontalRule.Color.B), c.HorizontalRule.Thickness);
                pen.DashPattern = c.HorizontalRule.DashArray.ToArray();
                for( ; d < c.Height ; d += c.HorizontalRule.Interval) {
                    if(showTitle && yyohaku < d && d < yyohaku + height) {
                        g.DrawLine(pen, 0, d, xyohaku, d);
                        g.DrawLine(pen, c.Width - xyohaku, d, c.Width, d);
                    } else {
                        g.DrawLine(pen, 0, d, c.Width, d);
                    }
                }
            }
            if(c.VerticalRule.Show) {
                var pen = new PdfSharp.Drawing.XPen(PdfSharp.Drawing.XColor.FromArgb(
                    c.VerticalRule.Color.A,
                    c.VerticalRule.Color.R,
                    c.VerticalRule.Color.G,
                    c.VerticalRule.Color.B), c.VerticalRule.Thickness);
                pen.DashPattern = c.VerticalRule.DashArray.ToArray();
                for(double d = c.VerticalRule.Interval ; d < c.Width ; d += c.VerticalRule.Interval) {
                    if(showTitle && xyohaku < d && d < c.Width - xyohaku) {
                        g.DrawLine(pen, d, 0, d, yyohaku);
                        g.DrawLine(pen, d, yyohaku + height + 2 * hankei, d, c.Height);
                    } else {
                        g.DrawLine(pen, d, 0, d, c.Height);
                    }
                }
            }
        }

        public static Size GetStringSize(string str, string fontname, double fontsize) {
            var ft = new FormattedText(str, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface(fontname),
                fontsize,
                Brushes.White);
            return new Size(ft.Width, ft.Height);
        }

        public static double GuessFontSize(string str, string fontname, double width, double height) {
            var size = GetStringSize(str, fontname, 10);
            int n = (int) Math.Sqrt(height * size.Width / (width * size.Height));
            // はみ出ることがあったので1ひいておく．
            return 10 * Math.Max(n * width / size.Width, height / ((n + 1) * size.Height)) - 1;
        }
        #endregion

        public IEnumerator<ablib.InkCanvas> GetEnumerator() {
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
