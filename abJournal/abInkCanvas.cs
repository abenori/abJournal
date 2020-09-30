using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Ink;
using System.Windows.Media;
using System.ComponentModel;
using System.Windows.Input.StylusPlugIns;

namespace abJournal {
    public class abInkCanvas : InkCanvas {
        public DrawingAttributesPlus DefaultDrawingAttributesPlus { get; set; } = new DrawingAttributesPlus();

        public event PropertyChangedEventHandler PropertyChanged;
        public abInkCanvas(double width, double height) {
            Init(new List<abStroke>(), new DrawingAttributes(), new DrawingAttributesPlus(), width, height);
        }
        public abInkCanvas(List<abStroke> strokes, DrawingAttributes dattr, DrawingAttributesPlus dattrp, double width, double height) {
            Init(strokes, dattr, dattrp, width, height);
        }
        private void Init(List<abStroke> strokes, DrawingAttributes dattr, DrawingAttributesPlus dattrp, double width, double height) {
            VisualChildren = new VisualCollection(this);
            Focusable = false;
            base.Width = width;
            base.Height = height;
            foreach (var s in strokes) {
                base.Strokes.Add(s);
            }
            DefaultDrawingAttributes = dattr.Clone();
            DefaultDrawingAttributesPlus = dattrp.Clone();
            Background = Brushes.White;
            abDynamicRender abDynamicRender = new abDynamicRender();
            abDynamicRender.DrawingAttributes = DefaultDrawingAttributes;
            abDynamicRender.DrawingAttributesPlus = DefaultDrawingAttributesPlus;
            DynamicRenderer = abDynamicRender;
            EditingMode = InkCanvasEditingMode.Ink;
            EditingModeInverted = InkCanvasEditingMode.EraseByStroke;
            UseCustomCursor = true;
            SetCursor();
            ClipToBounds = false;
            DefaultDrawingAttributes.AttributeChanged += DefaultDrawingAttributes_AttributeChanged;
            EditingModeChanged += AbInkCanvas_EditingModeChanged;
        }

        private void AbInkCanvas_EditingModeChanged(object sender, RoutedEventArgs e) {
            SetCursor();
        }

        private void DefaultDrawingAttributes_AttributeChanged(object sender, PropertyDataChangedEventArgs e) {
            SetCursor();
        }

        public new InkCanvasEditingMode EditingMode {
            get { return base.EditingMode; }
            set { base.EditingMode = value; SetCursor(); }
        }

        InkCanvasEditingMode? SavedEditingMode = null;
        void SaveEditingMode() { SavedEditingMode = EditingMode; }
        void RestoreEditingMode() {
            EditingMode = SavedEditingMode ?? EditingMode;
            SavedEditingMode = null;
        }

        protected override void OnPreviewMouseDown(MouseButtonEventArgs e) {
            if (EditingMode == InkCanvasEditingMode.EraseByStroke) BeginUndoGroup();
            base.OnPreviewMouseDown(e);
        }
        protected override void OnPreviewMouseUp(MouseButtonEventArgs e) {
            EndUndoGroup();
            base.OnPreviewMouseUp(e);
        }

        protected override void OnPreviewStylusDown(StylusDownEventArgs e) {
            if (e.StylusDevice.TabletDevice.Type != TabletDeviceType.Stylus) {
                SaveEditingMode();
                EditingMode = InkCanvasEditingMode.None;
            } else {
                // VAIO Duo 13の場合
                // どっちも押していない：Name = "Stylus", Button[0] = Down, Button[1] = Up
                // 上のボタンを押している：Name = "Eraser"，Button[0] = Down, Button[1] = Up
                // 下のボタンを押している：Name = "Stylus"，Button[0] = Down, Button[1] = Down
                if (e.StylusDevice.Name == "Eraser") {
                    SaveEditingMode();
                    EditingMode = InkCanvasEditingMode.EraseByStroke;
                } else if (
                     e.StylusDevice.StylusButtons.Count > 1 &&
                     e.StylusDevice.StylusButtons[1].StylusButtonState == StylusButtonState.Down
                 ) {
                    SaveEditingMode();
                    EditingMode = InkCanvasEditingMode.Select;
                }
            }
            base.OnPreviewStylusDown(e);
        }

        protected override void OnPreviewStylusUp(StylusEventArgs e) {
            RestoreEditingMode();
            base.OnPreviewStylusUp(e);
        }

        protected void OnPropertyChanged(string name) {
            if (PropertyChanged != null) {
                var e = new PropertyChangedEventArgs(name);
                PropertyChanged(this, e);
            }
        }

        protected override void OnStrokeCollected(InkCanvasStrokeCollectedEventArgs e) {
            this.Strokes.Remove(e.Stroke);
            var abStroke = new abStroke(e.Stroke.StylusPoints, e.Stroke.DrawingAttributes, DefaultDrawingAttributesPlus);
            this.Strokes.Add(abStroke);
            InkCanvasStrokeCollectedEventArgs args = new InkCanvasStrokeCollectedEventArgs(abStroke);
            AddUndo(new AddStrokeCommand(abStroke));
            base.OnStrokeCollected(args);
        }

        public void Copy() { CopySelection(); }
        public void Cut() { CutSelection(); }
        public void SelectAll() { Select(Strokes); }
        public bool Updated { get { return EditCount != 0; } }
        public void ClearUpdated() { EditCount = 0; }
        public void DeleteSelection() {
            BeginUndoGroup();
            var selected = GetSelectedStrokes();
            foreach (var s in selected) {
                AddUndo(new DeleteStrokeCommand(s as abStroke));
                Strokes.Remove(s);
            }
            EndUndoGroup();
        }

        public class UndoChainChangedEventArgs : EventArgs { }
        public delegate void UndoChainChangedEventhandler(object sender, UndoChainChangedEventArgs e);
        public event UndoChainChangedEventhandler UndoChainChanged;
        UndoGroup UndoGroup = null;
        List<UndoCommand> UndoStack = new List<UndoCommand>();
        int CurrentUndoPosition = 0;
        int EditCount = 0;
        void BeginUndoGroup() { UndoGroup = new UndoGroup(); }
        void EndUndoGroup() {
            if (UndoGroup != null) {
                if (UndoGroup.Count > 0) {
                    UndoStack.RemoveRange(CurrentUndoPosition, UndoStack.Count - CurrentUndoPosition);
                    UndoStack.Add(UndoGroup);
                    ++CurrentUndoPosition;
                    ++EditCount;
                }
                UndoGroup = null;
                UndoChainChanged(this, new UndoChainChangedEventArgs());
            }
        }
        bool do_not_add_to_undo_stack = false;
        public void StopAddToUndo() { do_not_add_to_undo_stack = true; }
        public void StartAddToUndo() { do_not_add_to_undo_stack = false; }

        void AddUndo(UndoCommand undo) {
            if (do_not_add_to_undo_stack) return;
            if (UndoGroup == null) {
                UndoStack.RemoveRange(CurrentUndoPosition, UndoStack.Count - CurrentUndoPosition);
                ++CurrentUndoPosition;
                ++EditCount;
                UndoStack.Add(undo);
                UndoChainChanged(this, new UndoChainChangedEventArgs());
            } else UndoGroup.Add(undo);
        }
        protected override void OnStrokeErasing(InkCanvasStrokeErasingEventArgs e) {
            if (e.Stroke is abStroke) {
                AddUndo(new DeleteStrokeCommand(e.Stroke as abStroke));
            }
            System.Diagnostics.Debug.WriteLine("OnStrokeErasing");
            base.OnStrokeErasing(e);
        }
        protected override void OnStrokesReplaced(InkCanvasStrokesReplacedEventArgs e) {
            var undog = new UndoGroup();
            undog.Add(new DeleteStrokeCommand(e.PreviousStrokes));
            undog.Add(new AddStrokeCommand(e.NewStrokes));
            AddUndo(undog);
            base.OnStrokesReplaced(e);
        }
        protected override void OnSelectionMoving(InkCanvasSelectionEditingEventArgs e) {
            var matrix = new Matrix(1, 0, 0, 1, e.NewRectangle.Left - e.OldRectangle.Left, e.NewRectangle.Top - e.OldRectangle.Top);
            AddUndo(new AffineTransformStrokeCommand(GetSelectedStrokes(), matrix));
            base.OnSelectionMoving(e);
        }
        protected override void OnSelectionResizing(InkCanvasSelectionEditingEventArgs e) {
            var xscale = e.NewRectangle.Width / e.OldRectangle.Width;
            var yscale = e.NewRectangle.Height / e.OldRectangle.Height;
            var xshift = e.NewRectangle.Left - e.OldRectangle.Left * xscale;
            var yshift = e.NewRectangle.Top - e.OldRectangle.Top * yscale;
            AddUndo(new AffineTransformStrokeCommand(GetSelectedStrokes(), new Matrix(xscale, 0, 0, yscale, xshift, yshift)));
            base.OnSelectionResizing(e);
        }
        protected override void OnSelectionChanging(InkCanvasSelectionChangingEventArgs e) {
            AddUndo(new SelectCommand(GetSelectedStrokes(), e.GetSelectedStrokes()));
            base.OnSelectionChanging(e);
        }
        public bool Undo() {
            if (CurrentUndoPosition <= 0 || CurrentUndoPosition > UndoStack.Count) return false;
            else {
                --CurrentUndoPosition;
                --EditCount;
                UndoStack[CurrentUndoPosition].Undo(this);
                return true;
            }
        }
        public bool Redo() {
            if (CurrentUndoPosition < 0 || CurrentUndoPosition >= UndoStack.Count) return false;
            else {
                UndoStack[CurrentUndoPosition].Redo(this);
                ++CurrentUndoPosition;
                ++EditCount;
                return true;
            }
        }

        public Rect Viewport { get; protected set; }
        public class ViewportChangedEventArgs : EventArgs {
            public Rect OldViewport { get; private set; }
            public Rect NewViewport { get; private set; }
            public ViewportChangedEventArgs(Rect o, Rect n) {
                OldViewport = o;
                NewViewport = n;
            }
        }
        public void SetViewport(Rect rc) {
            OnViewportChanged(new ViewportChangedEventArgs(Viewport, rc));
            Viewport = rc;
        }
        protected virtual void OnViewportChanged(ViewportChangedEventArgs e) {
            ViewportChanged(this, e);
            //System.Diagnostics.Debug.WriteLine("OnViewportChanged: old = " + e.OldViewport.ToString() + ", nwe = " + e.NewViewport.ToString());
        }
        public delegate void ViewportChangedEventHandler(object sender, ViewportChangedEventArgs e);
        public event ViewportChangedEventHandler ViewportChanged = ((s, e) => { });
        // Canvasに入る/から出る時にabInkCanvasCollectionから呼び出される．
        public void RemovedFromView() { }
        public void AddedToView() { }
        public void ReDraw() { base.InvalidateVisual(); }
        public void ClearSelected() { Select(new StrokeCollection()); }

        public static Cursor ErasingCursor = null;
        static Dictionary<Tuple<Color, double>, Cursor> InkingCursors = new Dictionary<Tuple<Color, double>, Cursor>();
        static Cursor MakeInkingCursor(double thickness, Color color) {
            var key = new Tuple<Color, double>(color, thickness);
            try { return InkingCursors[key]; }
            catch (KeyNotFoundException) {
                thickness *= 2;
                const int cursorsize = 254;
                using (var img = new System.Drawing.Bitmap(cursorsize, cursorsize))
                using (var g = System.Drawing.Graphics.FromImage(img)) {
                    g.FillRectangle(System.Drawing.Brushes.White, new System.Drawing.Rectangle(0, 0, cursorsize, cursorsize));
                    g.FillEllipse(
                        new System.Drawing.SolidBrush(System.Drawing.Color.FromArgb(color.A, color.R, color.G, color.B)),
                        new System.Drawing.Rectangle((int)(cursorsize / 2 - thickness / 2), (int)(cursorsize / 2 - thickness / 2), (int)thickness, (int)thickness));
                    var c = Img2Cursor.MakeCursor(img, new Point(cursorsize / 2, cursorsize / 2), new Point(0, 0));
                    InkingCursors[key] = c;
                    return c;
                }
            }
        }
        void SetCursor() {
            switch (EditingMode) {
            case InkCanvasEditingMode.Ink:
                SetCursor(MakeInkingCursor(DefaultDrawingAttributes.Width, DefaultDrawingAttributes.Color)); break;
            case InkCanvasEditingMode.EraseByStroke:
                SetCursor(ErasingCursor); break;
            default:
                SetCursor(Cursors.Cross); break;
            }
        }
        void SetCursor(Cursor c) {
            if (c != Cursor) Cursor = c;
        }

        public VisualCollection VisualChildren { get; private set; }
        protected override int VisualChildrenCount => base.VisualChildrenCount + VisualChildren.Count;
        protected override Visual GetVisualChild(int index) {
            if (index < base.VisualChildrenCount) return base.GetVisualChild(index);
            else return VisualChildren[index - base.VisualChildrenCount];
            //if (index < VisualChildren.Count) return VisualChildren[index];
            //else return base.GetVisualChild(index - VisualChildren.Count);
        }
        protected override void OnRender(DrawingContext drawingContext) {
            if (Background != null) {
                drawingContext.DrawRectangle(Background, null, new Rect(0, 0, RenderSize.Width, RenderSize.Height));
            }
            base.OnRender(drawingContext);
        }
    }

    public class abStroke : Stroke {
        public DrawingAttributesPlus DrawingAttributesPlus { get; set; }
        Pen Pen;
        public abStroke(StylusPointCollection pts) : base(pts) { }
        public abStroke(StylusPointCollection pts, DrawingAttributes datt, DrawingAttributesPlus dattp)
            : base(pts, datt) {
            DrawingAttributesPlus = dattp.Clone();
            DrawingAttributes.PropertyDataChanged += (s, e) => { Pen = null; };
            DrawingAttributesPlus.PropertyChanged += (s, e) => { Pen = null; };
        }

        void GetPen() {
            if (Pen == null) {
                var brush = new SolidColorBrush(DrawingAttributes.Color);
                brush.Opacity = DrawingAttributes.IsHighlighter ? 0.5 : 1;
                Pen = new Pen(brush, DrawingAttributes.Width);
                Pen.EndLineCap = Pen.StartLineCap = PenLineCap.Round;
                if (!DrawingAttributesPlus.IsNormalDashArray) {
                    Pen.DashStyle = new DashStyle(DrawingAttributesPlus.DashArray, 0);
                    Pen.DashCap = PenLineCap.Flat;
                }
                Pen.Freeze();
            }
        }

        protected override void DrawCore(DrawingContext drawingContext, DrawingAttributes drawingAttributes) {
            if (DrawingAttributesPlus.IsNormalDashArray) base.DrawCore(drawingContext, drawingAttributes);
            else {
                GetPen();
                DrawingStrokes.DrawOriginalType1(drawingContext, DrawingStrokes.MabikiPointsType1(StylusPoints, drawingAttributes), drawingAttributes, DrawingAttributesPlus, Pen);
            }
        }
    }

    public class abDynamicRender : DynamicRenderer {
        private Point? prevPoint = null;
        public DrawingAttributesPlus DrawingAttributesPlus { get; set; }
        double DashOffset = 0;
        Pen Pen = null;
        void GetPen() {
            if (Pen == null) {
                var brush = new SolidColorBrush(DrawingAttributes.Color);
                brush.Opacity = DrawingAttributes.IsHighlighter ? 0.5 : 1;
                Pen = new Pen(brush, DrawingAttributes.Width);
                Pen.DashStyle = new DashStyle(DrawingAttributesPlus.DashArray, 0);
                Pen.DashCap = PenLineCap.Flat;
                Pen.Freeze();
            }
        }
        protected override void OnStylusDown(RawStylusInput rawStylusInput) {
            prevPoint = null;
            DashOffset = 0;
            base.OnStylusDown(rawStylusInput);
        }
        protected override void OnDraw(DrawingContext drawingContext, StylusPointCollection stylusPoints, Geometry geometry, Brush fillBrush) {
            if (DrawingAttributesPlus.IsNormalDashArray) base.OnDraw(drawingContext, stylusPoints, geometry, fillBrush);
            else {
                GetPen();
                int i = 0;
                if (prevPoint == null) {
                    prevPoint = stylusPoints[0].ToPoint();
                    i = 1;
                }
                for (; i < stylusPoints.Count; ++i) {
                    var pt = stylusPoints[i];
                    var p = Pen.Clone();
                    if (!DrawingAttributes.IgnorePressure) p.Thickness *= pt.PressureFactor * 2;
                    if (!DrawingAttributesPlus.IsNormalDashArray) {
                        p.DashStyle.Offset = DashOffset;
                    }
                    drawingContext.DrawLine(p, prevPoint.Value, stylusPoints[i].ToPoint());
                    DashOffset += (Math.Sqrt((prevPoint.Value.X - pt.X) * (prevPoint.Value.X - pt.X) + (prevPoint.Value.Y - pt.Y) * (prevPoint.Value.Y - pt.Y))) / Pen.Thickness;
                    prevPoint = stylusPoints[i].ToPoint();
                }
            }
        }
    }

    interface UndoCommand {
        void Undo(abInkCanvas stroke);
        void Redo(abInkCanvas stroke);
    }
    interface UndoCommandComibinable : UndoCommand {
        // UndoCommand.Undoの呼び出しがかなり遅いっぽいので，
        // UndoGroupで使う場合に，くっつけられるUndoCommandはくっつけるようにする．
        void Combine(UndoCommand c);
    }

    class UndoGroup : UndoCommand, IEnumerable<UndoCommand> {
        List<UndoCommand> Commands = new List<UndoCommand>();
        public int Count { get { return Commands.Count; } }
        public UndoGroup() { }
        public void Add(UndoCommand c) { Commands.Add(c); }
        public void Undo(abInkCanvas data) {
            for (int i = Commands.Count - 1; i >= 0; --i) Commands[i].Undo(data);
        }
        public void Redo(abInkCanvas data) {
            for (int i = 0; i < Commands.Count; ++i) Commands[i].Redo(data);
        }
        // undoCommandCombinableなコマンドを全て展開し，Combineでくっつけておく．
        public void Normalize() {
            List<UndoCommand> cmds = new List<UndoCommand>();
            UndoGroup.ExpandGroups(this, ref cmds);
            Commands.Clear();
            for (int i = 0; i < cmds.Count; ++i) {
                if (cmds[i] is UndoCommandComibinable) {
                    UndoCommandComibinable cmd = cmds[i] as UndoCommandComibinable;
                    System.Type type = cmd.GetType();
                    ++i;
                    for (; i < cmds.Count; ++i) {
                        if (type == cmds[i].GetType()) {
                            cmd.Combine(cmds[i]);
                        } else break;
                    }
                    Commands.Add(cmd);
                } else {
                    Commands.Add(cmds[i]);
                }
            }
        }
        // UndoGroupを全て展開し，一連のUndoCommandの配列とする．
        static void ExpandGroups(UndoGroup undo, ref List<UndoCommand> cmds) {
            for (int i = 0; i < undo.Count; ++i) {
                if (undo.Commands[i] is UndoGroup) UndoGroup.ExpandGroups((UndoGroup)undo.Commands[i], ref cmds);
                else cmds.Add(undo.Commands[i]);
            }
        }
        public IEnumerator<UndoCommand> GetEnumerator() { return Commands.GetEnumerator(); }
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return Commands.GetEnumerator(); }

        // Commandも全部表示するようにしておく．Debug用．
        public override string ToString() {
            string rv = base.ToString() + "[";
            for (int i = 0; i < Commands.Count; ++i) {
                if (i == 0) rv += Commands[i].ToString();
                else rv += " ; " + Commands[i].ToString();
            }
            return rv + "]";
        }
    }

    // deleteした時に入れる
    class DeleteStrokeCommand : UndoCommandComibinable {
        public StrokeCollection stroke;
        public DeleteStrokeCommand(abStroke s) {
            stroke = new StrokeCollection(); stroke.Add(s);
        }
        public DeleteStrokeCommand(StrokeCollection s) { stroke = s; }
        public void Undo(abInkCanvas data) {
            foreach (var s in stroke) data.Strokes.Add(s);
            //data.StrokeAdded(data, new StrokeChangedEventArgs(stroke));
        }
        public void Redo(abInkCanvas data) {
            foreach (var s in stroke) data.Strokes.Remove(s);
            //data.StrokeDeleted(data, new StrokeChangedEventArgs(stroke));
        }
        public void Combine(UndoCommand del) {
            foreach (var s in (del as DeleteStrokeCommand).stroke) stroke.Add(s);
        }
    }
    class AddStrokeCommand : UndoCommandComibinable {
        StrokeCollection stroke;
        public AddStrokeCommand(StrokeCollection s) { stroke = s; }
        public AddStrokeCommand(Stroke s) {
            stroke = new StrokeCollection(); stroke.Add(s);
        }
        public void Redo(abInkCanvas data) {
            data.Strokes.Add(stroke);
        }
        public void Undo(abInkCanvas data) {
            data.Strokes.Remove(stroke);
        }
        public void Combine(UndoCommand add) {
            stroke.Add((add as AddStrokeCommand).stroke);
        }
    }
    class SelectCommand : UndoCommand {
        StrokeCollection beforeStroke, afterStroke;
        public SelectCommand(StrokeCollection before, StrokeCollection after) {
            beforeStroke = before; afterStroke = after;
        }
        public void Undo(abInkCanvas c) {
            c.StopAddToUndo();
            c.Select(beforeStroke);
            c.StartAddToUndo();
        }
        public void Redo(abInkCanvas c) {
            c.StopAddToUndo();
            c.Select(afterStroke);
            c.StartAddToUndo();
        }
    }

    class AffineTransformStrokeCommand : UndoCommandComibinable {
        struct MatrixPair {
            public MatrixPair(Matrix m) {
                matrix = m; invMatrix = m;
                invMatrix.Invert();
            }
            public MatrixPair Combine(MatrixPair mp) {
                matrix *= mp.matrix;
                invMatrix = mp.invMatrix * invMatrix;
                return this;
            }
            public Matrix matrix, invMatrix;
        }
        Dictionary<Stroke, MatrixPair> Matrices;
        public AffineTransformStrokeCommand(StrokeCollection sc, Matrix matrix) {
            Matrices = new Dictionary<Stroke, MatrixPair>();
            foreach (var s in sc) Matrices[s] = new MatrixPair(matrix);
        }
        public void Undo(abInkCanvas data) {
            foreach (var x in Matrices) {
                x.Key.Transform(x.Value.invMatrix, false);
            }
        }
        public void Redo(abInkCanvas data) {
            foreach (var x in Matrices) {
                x.Key.Transform(x.Value.matrix, true);
            }
        }
        // this * moveを各行列に対して計算する
        public void Combine(UndoCommand m) {
            var move = m as AffineTransformStrokeCommand;
            foreach (var x in move.Matrices) {
                if (!Matrices.ContainsKey(x.Key)) Matrices.Add(x.Key, x.Value);
                else Matrices[x.Key] = Matrices[x.Key].Combine(x.Value);
            }
        }
    }
}
