using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace ablib {
    /**
     * 管理したいもの
     * Stroke
     * テキスト
     * 選択状態
     */
    public enum InkManipulationMode { Inking = 0, Erasing = 1, Selecting = 2 };
    public enum DrawingAlgorithm { dotNet = 0, Type1 = 1, Type1WithHosei = 2 }
    public class DrawingAttributesPlus : System.ComponentModel.INotifyPropertyChanged {
        public static DoubleCollection NormalDashArray = new DoubleCollection();
        public DrawingAttributesPlus() {
            DashArray = new DoubleCollection();
        }
        DoubleCollection dashArray;
        public DoubleCollection DashArray {
            get { return dashArray; }
            set { dashArray = value; OnPropertyChanged("DashArray"); }
        }
        public bool IsNormalDashArray {
            get { return (dashArray.Count == 0); }
        }
        public DrawingAttributesPlus Clone() {
            DrawingAttributesPlus rv = new DrawingAttributesPlus();
            for(int i = 0 ; i < DashArray.Count ; ++i) {
                rv.dashArray.Add(dashArray[i]);
            }
            return rv;
        }
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) {
            if(PropertyChanged != null) {
                PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(name));
            }
        }
    }
    public partial class StrokeData : Stroke {
        public DrawingAlgorithm algorithm = DrawingAlgorithm.dotNet;
        public DrawingAlgorithm Algorithm {
            set { if(value != algorithm)geometry = null; algorithm = value; }
        }
        bool selected;
        public bool Selected {
            get { return selected; }
            set { if(selected != value)geometry = null; selected = value; }
        }
        Brush brush = null;
        public Brush Brush {
            get {
                if(brush == null)brush = new SolidColorBrush(DrawingAttributes.Color);
                return brush; 
            }
        }
        public new DrawingAttributes DrawingAttributes {
            get { return base.DrawingAttributes; }
            set { base.DrawingAttributes = value; geometry = null; brush = null; }
        }
        DrawingAttributesPlus drawingAttributesPlus = new DrawingAttributesPlus();
        public DrawingAttributesPlus DrawingAttributesPlus {
            get { return drawingAttributesPlus; }
            set { drawingAttributesPlus = value; geometry = null; }
        }

        public StrokeData(StylusPointCollection spc, DrawingAttributes att, DrawingAttributesPlus attplus, DrawingAlgorithm algo, bool selecting = false)
            : base(spc, att.Clone()) {
            DrawingAttributes.AttributeChanged += ((s, e) => { geometry = null; brush = null; });
            DrawingAttributesPlus.PropertyChanged += ((s, e) => { geometry = null; });
            Selected = selecting;
            drawingAttributesPlus = attplus;
            Algorithm = algo;
        }

        public override void Transform(Matrix transformMatrix, bool applyToStylusTip) {
            if(geometry != null) {
                if(matrixTransform == null) {
                    matrixTransform = new MatrixTransform(transformMatrix);
                    geometry = geometry.Clone();
                    geometry.Transform = matrixTransform;
                } else {
                    matrixTransform.Matrix = matrixTransform.Matrix * transformMatrix;
                }
            }
            base.Transform(transformMatrix, applyToStylusTip);
        }
        Geometry geometry = null;
        MatrixTransform matrixTransform = null;

        public void GetPath(ref System.Windows.Shapes.Path p, DrawingAttributes dattr, DrawingAttributesPlus dattrPlus, bool selecting,DrawingAlgorithm algo) {
            Geometry geom = geometry;
            geometry = null;
            GetPathImpl(ref p, dattr, dattrPlus, selecting,algo);
            geometry = geom;
        }
        public void GetPath(ref System.Windows.Shapes.Path p, bool selecting) {
            Geometry geom = geometry;
            if(Selected != selecting) geometry = null;
            GetPathImpl(ref p, DrawingAttributes, DrawingAttributesPlus, selecting, algorithm);
            geometry = geom;
        }
        public void GetPath(ref System.Windows.Shapes.Path p) {
            GetPathImpl(ref p, DrawingAttributes, DrawingAttributesPlus, Selected,algorithm);
        }
        // Strokg.GetGeometry = 厚みがある：「二重線」ができる．
        // GetOriginalGeometryType1：線でひく：破線が引ける
        // これらの特徴のため，Algoithmが無視されることがある
        void GetPathImpl(ref System.Windows.Shapes.Path p, DrawingAttributes dattr, DrawingAttributesPlus dattrPlus, bool selecting, DrawingAlgorithm algo) {
            if(geometry == null) matrixTransform = null;
            if(selecting) {
                if(geometry == null) geometry = base.GetGeometry();
                p.Fill = null;
                p.Stroke = Brush;
                p.StrokeThickness = dattr.Width / 5;
            } else {
                switch(algo) {
                case DrawingAlgorithm.Type1:
                    if(geometry == null) geometry = GetOriginalGeometryType1(MabikiPointsType1(StylusPoints));
                    p.Fill = null;
                    p.Stroke = Brush;
                    p.StrokeThickness = dattr.Width;
                    p.StrokeDashArray = dattrPlus.DashArray;
                    p.StrokeEndLineCap = PenLineCap.Round;
                    p.StrokeStartLineCap = PenLineCap.Round;
                    break;
                case DrawingAlgorithm.Type1WithHosei:
                    if(geometry == null) geometry = GetOriginalGeometryType1(MabikiPointsType1(GetHoseiPoints(StylusPoints)));
                    p.Fill = null;
                    p.Stroke = Brush;
                    p.StrokeThickness = dattr.Width;
                    p.StrokeDashArray = dattrPlus.DashArray;
                    p.StrokeEndLineCap = PenLineCap.Round;
                    p.StrokeStartLineCap = PenLineCap.Round;
                    break;
                default:
                    if(!dattrPlus.IsNormalDashArray) {
                        if(geometry == null) geometry = GetOriginalGeometryType1(MabikiPointsType1(GetHoseiPoints(StylusPoints)));
                        p.Fill = null;
                        p.Stroke = Brush;
                        p.StrokeThickness = dattr.Width;
                        p.StrokeDashArray = dattrPlus.DashArray;
                        p.StrokeEndLineCap = PenLineCap.Round;
                        p.StrokeStartLineCap = PenLineCap.Round;
                    } else {
                        if(geometry == null) geometry = base.GetGeometry();
                        p.Fill = Brush;
                        p.Stroke = null;
                        p.StrokeThickness = 0;
                    }
                    break;
                }
            }
            p.Data = geometry;
        }

    }
    public class StrokeDataCollection : List<StrokeData> {
        public StrokeDataCollection() { }
        public StrokeDataCollection(int capacity) : base(capacity) { }
        public StrokeDataCollection(IEnumerable<StrokeData> collection) : base(collection) { }
    }

    public class TextData {
        public string Text { get; set; }
        public bool Selected { get; set; }
        public Rect Rect { get; set; }
        public Typeface Font { get; set; }
        public Color Color { get; set; }
        public TextData(string text) {
            Rect = new Rect();
            Font = new Typeface("ＭＳ ゴシック");
            Text = text;
        }
        public TextData(string text,Rect rect,Typeface font,Color color){
			Text = text; Rect = rect; Font = font; Color = color;
		}
    }
    public class TextDataCollection : List<TextData> { }
    public class InkData {
        public StrokeDataCollection Strokes { get; set; }
        public TextDataCollection Texts { get; set; }

        public InkData() {
            Strokes = new StrokeDataCollection();
            Texts = new TextDataCollection();
        }
        DrawingAlgorithm drawingAlgorithm = DrawingAlgorithm.dotNet;
        public DrawingAlgorithm DrawingAlgorithm {
            get { return drawingAlgorithm; }
            set {
                drawingAlgorithm = value;
                foreach(var s in Strokes) s.Algorithm = value;
            }
        }
        public bool Updated {
            get {
                if(EditCount == 0) return false;
                else if(EditCount > 0) {
                    for(int i = EditCount - 1 ; i >= 0 && i < UndoStack.Count ; --i) {
                        if(!(UndoStack[i] is SelectChangeCommand)) return true;
                    }
                    return false;
                } else {
                    for(int i = -EditCount - 1 ; i >= 0 && i < RedoStack.Count ; --i) {
                        if(!(RedoStack[i] is SelectChangeCommand)) return true;
                    }
                    return false;
                }
            }
        }
        public void ClearUpdated() { EditCount = 0; }


        public void AddStroke(StrokeDataCollection sc) {
            Strokes.AddRange(sc);
            AddUndoList(new AddStrokeCommand(sc));
            OnStrokeAdded(new StrokeChangedEventArgs(sc));
        }
        public void Select(StylusPointCollection spc, int percent) {
            PointCollection pc = new PointCollection(spc.Select(p => p.ToPoint()));
            var changed = new StrokeDataCollection(Strokes.Where(s =>{
                bool hittest = s.HitTest(pc, percent);
                if(hittest != s.Selected) {
                    s.Selected = hittest;
                    return true;
                } else return false;
            }));
            AddUndoList(new SelectChangeCommand(changed));
            OnStrokeSelectedChanged(new StrokeChangedEventArgs(changed));
        }
        public void SelectAll() {
            var changed = new StrokeDataCollection(Strokes.Where(s => {
                if(!s.Selected) {
                    s.Selected = true;
                    return true;
                } else return false;
            }));
            AddUndoList(new SelectChangeCommand(changed));
            StrokeSelectedChanged(this, new StrokeChangedEventArgs(changed));
        }

        // moveと言いながら拡大縮小もしてしまう．
        // つまりoldRect -> newRectとするようなアフィン変換（回転なし）で変換する．
        public void MoveSelected(Rect oldRect, Rect newRect) {
            double ax = newRect.Width / oldRect.Width;
            double bx = newRect.X - ax * oldRect.X;
            double ay = newRect.Height / oldRect.Height;
            double by = newRect.Y - ay * oldRect.Y;
            Matrix matrix = new Matrix(ax, 0, 0, ay, bx, by);
            List<Matrix> matrices = new List<Matrix>();
            StrokeDataCollection changed = new StrokeDataCollection(Strokes.Where(s => {
                if(s.Selected) {
                    s.Transform(matrix, false);
                    matrices.Add(matrix);
                    return true;
                } else return false;
            }));
            OnStrokeChanged(new StrokeChangedEventArgs(changed));
            AddUndoList(new MoveStrokeCommand(changed, matrices));
        }
        public void DeleteSelected() {
            StrokeDataCollection deleted = new StrokeDataCollection();
            Strokes.RemoveAll(s => {
                if(s.Selected) {
                    deleted.Add(s);
                    return true;
                } else return false;

            });
            OnStrokeDeleted(new StrokeChangedEventArgs(deleted));
            AddUndoList(new DeleteStrokeCommand(deleted));
        }
        public void ClearSelected() {
            var changed = new StrokeDataCollection(Strokes.Where(s => {
                if(s.Selected){
                    s.Selected = false;
                    return true;
                }else return false;
            }));
            OnStrokeSelectedChanged(new StrokeChangedEventArgs(changed));
            AddUndoList(new SelectChangeCommand(changed));
        }
        public StrokeDataCollection GetSelectedStrkes() {
            return new StrokeDataCollection(Strokes.Where(s => s.Selected));
        }

        #region ProcessPoint***関連
        StylusPointCollection ProcessStylusPointCollection;
        DrawingAttributes ProcessDrawingAttributes;
        InkManipulationMode ProcessMode;
        UndoGroup ProcessUndoGroup;
        DrawingAttributesPlus ProcessDrawingAttributesPlus;
        public void ProcessPointerDown(InkManipulationMode mode, DrawingAttributes attr, DrawingAttributesPlus dattrplus, StylusPoint point) {
            ProcessStylusPointCollection = new StylusPointCollection(point.Description); ;
            ProcessDrawingAttributes = attr.Clone();
            ProcessMode = mode;
            ProcessDrawingAttributesPlus = dattrplus.Clone();
            ProcessUndoGroup = new UndoGroup();
            ProcessPointerUpdate(point);
        }
        public void ProcessPointerUpdate(StylusPoint point) {
            ProcessStylusPointCollection.Add(point);
            if(ProcessMode == InkManipulationMode.Erasing) {
                StrokeDataCollection deleted = new StrokeDataCollection();
                Strokes.RemoveAll(s => {
                    if(s.HitTest(point.ToPoint(), 3)) {
                        ProcessUndoGroup.Add(new DeleteStrokeCommand(s));
                        deleted.Add(s);
                        return true;
                    } else return false;
                });
                OnStrokeDeleted(new StrokeChangedEventArgs(deleted));
            } else if(ProcessMode == InkManipulationMode.Selecting) {
                //if(ProcessStylusPointCollection.Count % 50 == 0) {
                //Select(ProcessStylusPointCollection, 3);
                //}
            }
        }
        public void ProcessPointerUp() {
            if(ProcessMode == InkManipulationMode.Inking) {
                Strokes.Add(new StrokeData(ProcessStylusPointCollection, ProcessDrawingAttributes, ProcessDrawingAttributesPlus, DrawingAlgorithm));
                AddUndoList(new AddStrokeCommand(Strokes.Last()));
                OnStrokeAdded(new StrokeChangedEventArgs(Strokes.Last()));
            } else if(ProcessMode == InkManipulationMode.Erasing) {
                AddUndoList(ProcessUndoGroup);
            } else if(ProcessMode == InkManipulationMode.Selecting) {
                Select(ProcessStylusPointCollection, 80);
            }
        }
        #endregion

        #region RedoUndo関連
        // RedoUndo管理

        // UndoListに追加，Redoで+1，Undoで-1する．
        // 更新していないのとEditCount == 0が同値
        // BeginUndoGroup/EndUndoGroupの間は無視する．
        int EditCount = 0;

        List<UndoCommand> UndoStack = new List<UndoCommand>(), RedoStack = new List<UndoCommand>();
        int MaxUndoSize = 1000;
        void AddUndoList(UndoCommand undo) {
            if(undo is UndoGroup) {
                if(((UndoGroup) undo).Count == 0) return;
            }
            if(undoGroup != null) {
                undoGroup.Add(undo);
                return;
            }
            //System.Diagnostics.Debug.WriteLine("Added undo Hash = " + undo.GetHashCode() + ", " + undo.ToString());
            if(undo is UndoGroup) ((UndoGroup) undo).Normalize();
            RedoStack.Clear();
            UndoStack.Add(undo);
            if(UndoStack.Count > MaxUndoSize) {
                UndoStack.RemoveAt(0);
            }
            ++EditCount;
            OnUndoChainChanged(new UndoChainChangedEventArgs());
        }
        public bool Undo() {
            if(UndoStack.Count == 0) return false;
            UndoCommand undo = UndoStack.Last();
            System.Diagnostics.Debug.WriteLine("Undo: " + undo.ToString());
            undo.Undo(this);
            UndoStack.RemoveAt(UndoStack.Count - 1);
            RedoStack.Add(undo);
            --EditCount;
            return true;
        }
        public bool Redo() {
            if(RedoStack.Count == 0) return false;
            UndoCommand redo = RedoStack.Last();
            redo.Redo(this);
            RedoStack.RemoveAt(RedoStack.Count - 1);
            UndoStack.Add(redo);
            ++EditCount;
            return true;
        }

        UndoGroup undoGroup = null;
        public void BeginUndoGroup() {
            if(undoGroup != null) return;
            undoGroup = new UndoGroup();
        }
        public void EndUndoGroup() {
            if(undoGroup == null) return;
            if(undoGroup.Count != 0) {
                // AddUndoList内でundoGroup = nullか否かで場合わけしている．
                UndoGroup u = undoGroup;
                undoGroup = null;
                AddUndoList(u);
            }
        }

        interface UndoCommand {
            void Undo(InkData data);
            void Redo(InkData data);
        }
        interface UndoCommandComibinable : UndoCommand {
            // UndoCommand.Undoの呼び出しがかなり遅いっぽいので，
            // undoGroupで使う場合に，くっつけられるUndoCommandはくっつけるようにする．
            void Combine(UndoCommandComibinable c);
        }

        class UndoGroup : UndoCommand {
            List<UndoCommand> Commands = new List<UndoCommand>();
            public int Count { get { return Commands.Count; } }
            public UndoGroup() { }
            public void Add(UndoCommand c) { Commands.Add(c); }
            public void Undo(InkData data) {
                for(int i = Commands.Count - 1 ; i >= 0 ; --i) Commands[i].Undo(data);
            }
            public void Redo(InkData data) {
                for(int i = 0 ; i < Commands.Count ; ++i) Commands[i].Redo(data);
            }
            // undoCommandCombinableなコマンドを全て展開し，Combineでくっつけておく．
            public void Normalize() {
                List<UndoCommand> cmds = new List<UndoCommand>();
                UndoGroup.ExpandGroups(this, ref cmds);
                Commands.Clear();
                for(int i = 0 ; i < cmds.Count ; ++i) {
                    if(cmds[i] is UndoCommandComibinable) {
                        UndoCommandComibinable cmd = cmds[i] as UndoCommandComibinable;
                        System.Type type = cmd.GetType();
                        ++i;
                        for( ; i < cmds.Count ; ++i) {
                            if(type == cmds[i].GetType()) {
                                cmd.Combine((UndoCommandComibinable) cmds[i]);
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
                for(int i = 0 ; i < undo.Count ; ++i) {
                    if(undo.Commands[i] is UndoGroup) UndoGroup.ExpandGroups((UndoGroup) undo.Commands[i], ref cmds);
                    else cmds.Add(undo.Commands[i]);
                }
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

        // deleteした時に入れる
        class DeleteStrokeCommand : UndoCommandComibinable {
            public StrokeDataCollection stroke;
            public DeleteStrokeCommand(StrokeData s) {
                stroke = new StrokeDataCollection(); stroke.Add(s);
            }
            public DeleteStrokeCommand(StrokeDataCollection s) { stroke = s; }
            public void Undo(InkData data) {
                foreach(var s in stroke) data.Strokes.Add(s);
                data.StrokeAdded(data, new StrokeChangedEventArgs(stroke));
            }
            public void Redo(InkData data) {
                foreach(var s in stroke) data.Strokes.Remove(s);
                data.StrokeDeleted(data, new StrokeChangedEventArgs(stroke));
            }
            public void Combine(UndoCommandComibinable del) {
                stroke.AddRange((del as DeleteStrokeCommand).stroke);
            }
        }
        class AddStrokeCommand : UndoCommandComibinable {
            StrokeDataCollection stroke;
            public AddStrokeCommand(StrokeDataCollection s) { stroke = s; }
            public AddStrokeCommand(StrokeData s) {
                stroke = new StrokeDataCollection(); stroke.Add(s);
            }
            public void Redo(InkData data) {
                foreach(var s in stroke) {
                    data.Strokes.Add(s);
                }
                data.OnStrokeAdded(new StrokeChangedEventArgs(stroke));
            }
            public void Undo(InkData data) {
                foreach(var s in stroke) {
                    data.Strokes.Remove(s);
                }
                data.StrokeDeleted(data, new StrokeChangedEventArgs(stroke));
            }
            public void Combine(UndoCommandComibinable add) {
                stroke.AddRange((add as AddStrokeCommand).stroke);
            }
        }
        class SelectChangeCommand : UndoCommand {
            public StrokeDataCollection Strokes;
            public SelectChangeCommand(StrokeDataCollection strokes) {
                Strokes = strokes;
            }
            public void Undo(InkData data) {
                for(int i = 0 ; i < Strokes.Count ; ++i) {
                    Strokes[i].Selected = !Strokes[i].Selected;
                }
                data.StrokeSelectedChanged(data, new StrokeChangedEventArgs(Strokes));
            }
            public void Redo(InkData data) {
                Undo(data);
            }
        }

        class MoveStrokeCommand : UndoCommandComibinable {
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
            Dictionary<StrokeData, MatrixPair> Matrices;
            public MoveStrokeCommand(StrokeDataCollection sdc, List<Matrix> matrices) {
                if(sdc.Count != matrices.Count) throw new ArgumentException("Count is not matched");
                Matrices = new Dictionary<StrokeData, MatrixPair>();
                for(int i = 0 ; i < sdc.Count ; ++i) {
                    Matrices.Add(sdc[i], new MatrixPair(matrices[i]));
                }
            }
            public void Undo(InkData data) {
                StrokeDataCollection sdc = new StrokeDataCollection();
                foreach(var x in Matrices) {
                    x.Key.Transform(x.Value.invMatrix, false);
                    sdc.Add(x.Key);
                }
                data.StrokeChanged(data, new StrokeChangedEventArgs(sdc));
            }
            public void Redo(InkData data) {
                StrokeDataCollection sdc = new StrokeDataCollection();
                foreach(var x in Matrices) {
                    x.Key.Transform(x.Value.matrix, true);
                    sdc.Add(x.Key);
                }
                data.StrokeChanged(data, new StrokeChangedEventArgs(sdc));
            }
            // this * moveを各行列に対して計算する
            public void Combine(UndoCommandComibinable m) {
                var move = m as MoveStrokeCommand;
                foreach(var x in move.Matrices) {
                    if(!Matrices.ContainsKey(x.Key)) Matrices.Add(x.Key, x.Value);
                    else Matrices[x.Key] = Matrices[x.Key].Combine(x.Value);
                }
            }
        }
        #endregion

        protected virtual void OnStrokeDeleted(StrokeChangedEventArgs e) {
            StrokeDeleted(this, e);
        }
        protected virtual void OnStrokeAdded(StrokeChangedEventArgs e) {
            StrokeAdded(this, e);
        }
        protected virtual void OnStrokeChanged(StrokeChangedEventArgs e) {
            StrokeChanged(this, e);
        }
        protected virtual void OnStrokeSelectedChanged(StrokeChangedEventArgs e) {
            StrokeSelectedChanged(this, e);
        }
        protected virtual void OnUndoChainChanged(UndoChainChangedEventArgs e) {
            UndoChainChanged(this, e);
        }

        #region イベントハンドラ
        public class StrokeChangedEventArgs : EventArgs {
            public StrokeDataCollection Strokes { get; set; }
            public StrokeChangedEventArgs(StrokeData stroke) { Strokes = new StrokeDataCollection(); Strokes.Add(stroke); }
            public StrokeChangedEventArgs(StrokeDataCollection sdc) { Strokes = new StrokeDataCollection(sdc); }
        }
        public delegate void StrokeChangedEventHandler(object sender, StrokeChangedEventArgs e);
        public event StrokeChangedEventHandler StrokeDeleted = ((sender, s) => { });
        public event StrokeChangedEventHandler StrokeChanged = ((sender, s) => { });
        public event StrokeChangedEventHandler StrokeAdded = ((sender, s) => { });
        public event StrokeChangedEventHandler StrokeSelectedChanged = ((sender, s) => { });

        public class TextChangedEventArgs : EventArgs {
            public TextDataCollection Text { get; set; }
            public TextChangedEventArgs(TextData t) { Text = new TextDataCollection(); Text.Add(t); }
        }
        public delegate void TextChangedEventHandler(object sender, TextChangedEventArgs e);
        public event TextChangedEventHandler TextDelete = ((sender, t) => { });
        public event TextChangedEventHandler TextChanged = ((sender, t) => { });
        public event TextChangedEventHandler TextAdded = ((sender, t) => { });
        public event TextChangedEventHandler TextSelectedChanged = ((sender, t) => { });

        public class UndoChainChangedEventArgs : EventArgs { }
        public delegate void UndoChainChangedEventhandelr(object sender, UndoChainChangedEventArgs e);
        public event UndoChainChangedEventhandelr UndoChainChanged = ((sender, t) => { });
        #endregion

        public void Paste() {
            ClearSelected();
            Point basePoint = new Point(20, 20);
            Matrix shift = new Matrix(1, 0, 0, 1, basePoint.X, basePoint.Y);
            DoubleCollection DashArray = new DoubleCollection(new double[] { 1, 1 });
            if(Clipboard.ContainsData(StrokeCollection.InkSerializedFormat)) {
                System.IO.MemoryStream stream = (System.IO.MemoryStream) Clipboard.GetData(StrokeCollection.InkSerializedFormat);
                StrokeCollection strokes = new StrokeCollection(stream);
                StrokeDataCollection strokeData = new StrokeDataCollection();
                DrawingAttributesPlus dattrplus = new DrawingAttributesPlus();
                foreach(var s in strokes) {
                    StrokeData sd = new StrokeData(s.StylusPoints, s.DrawingAttributes, dattrplus, DrawingAlgorithm, true);
                    sd.Transform(shift, true);
                    Strokes.Add(sd);
                    strokeData.Add(sd);
                }
                AddUndoList(new AddStrokeCommand(strokeData));
                OnStrokeAdded(new StrokeChangedEventArgs(strokeData));
            }
        }
        public void Cut() {
            Copy();
            DeleteSelected();
        }
        public void Copy() {
            System.IO.MemoryStream stream = new System.IO.MemoryStream();
            StrokeCollection sc = new StrokeCollection();
            foreach(var s in Strokes) {
                if(s.Selected) {
                    sc.Add(new Stroke(s.StylusPoints, s.DrawingAttributes));
                }
            }
            sc.Save(stream);
            DataObject obj = new DataObject(StrokeCollection.InkSerializedFormat, stream);
            Clipboard.SetDataObject(obj, true);
        }

        public ablib.Saving.InkData GetSavingData() {
            return new ablib.Saving.InkData(this);
        }
        public void LoadSavingData(ablib.Saving.InkData data) {
            Strokes = data.Strokes.ToOriginalType();
            foreach(var s in Strokes) {
                //s.DrawingAttributesPlus.DashArray.Clear();
            }
            Texts = data.Texts.ToOriginalType();
        }

        public void AddPdfGraphic(PdfSharp.Drawing.XGraphics g) {
            foreach(var s in Strokes) {
                var pen = new PdfSharp.Drawing.XPen(
                    PdfSharp.Drawing.XColor.FromArgb(
                        s.DrawingAttributes.Color.A,
                        s.DrawingAttributes.Color.R,
                        s.DrawingAttributes.Color.G,
                        s.DrawingAttributes.Color.B
                    ), s.DrawingAttributes.Width);
                if(!s.DrawingAttributesPlus.IsNormalDashArray) {
                    pen.DashPattern = s.DrawingAttributesPlus.DashArray.ToArray();
                }
                pen.LineCap = PdfSharp.Drawing.XLineCap.Round;
                g.DrawPath(pen, s.GetPDFPath());                
            }
        }
        /*
        public void AddPdfGraphic(iTextSharp.text.pdf.PdfWriter writer,float height) {
            foreach(var s in Strokes) s.GetPDFPath(writer.DirectContent, height);
        }*/
    }

    namespace Saving {
        // 保存用のデータ構造
        public class StylusPoint {
            public float PressureFactor;
            public double X, Y;
            public StylusPoint(System.Windows.Input.StylusPoint pt) {
                PressureFactor = pt.PressureFactor;
                X = pt.X; Y = pt.Y;
            }
            public StylusPoint() { }
            public System.Windows.Input.StylusPoint ToOriginalType() {
                return new System.Windows.Input.StylusPoint(X, Y, PressureFactor);
            }
        }
        public class StylusPointCollection : List<StylusPoint> {
            public StylusPointCollection(System.Windows.Input.StylusPointCollection pts) : base(pts.Count){
                foreach(var pt in pts) base.Add(new StylusPoint(pt));
            }
            public StylusPointCollection() { }
            public System.Windows.Input.StylusPointCollection ToOriginalType() {
                var rv = new System.Windows.Input.StylusPointCollection(Count);
                for(int i = 0 ; i < Count ; ++i) rv.Add(this[i].ToOriginalType());
                return rv;
            }
        }
        public class Stroke {
            public StylusPointCollection StylusPoints;
            public System.Windows.Ink.DrawingAttributes DrawingAttributes;
            public Stroke(System.Windows.Ink.Stroke stroke) {
                StylusPoints = new StylusPointCollection(stroke.StylusPoints);
                DrawingAttributes = stroke.DrawingAttributes.Clone();
            }
            public Stroke() { }
            public System.Windows.Ink.Stroke GetOriginalType() {
                return new System.Windows.Ink.Stroke(StylusPoints.ToOriginalType(), DrawingAttributes);
            }
        }
        public class StrokeData : Stroke {
            public ablib.DrawingAttributesPlus DrawingAttributesPlus;
            public StrokeData(ablib.StrokeData strokeData)
                : base(strokeData) {
                DrawingAttributesPlus = strokeData.DrawingAttributesPlus.Clone();
            }
            public StrokeData() {
                DrawingAttributesPlus = new DrawingAttributesPlus();
            }
            public ablib.StrokeData ToOriginalType() {
                return new ablib.StrokeData(StylusPoints.ToOriginalType(),DrawingAttributes,DrawingAttributesPlus,DrawingAlgorithm.dotNet);
            }
        }
        public class StrokeDataCollection : List<StrokeData> {
            public StrokeDataCollection(ablib.StrokeDataCollection sdc) : base(sdc.Count){
                foreach(var sd in sdc) base.Add(new StrokeData(sd));
            }
            public StrokeDataCollection() { }
            public ablib.StrokeDataCollection ToOriginalType() {
                var rv = new ablib.StrokeDataCollection(Count);
                for(int i = 0 ; i < Count ; ++i) rv.Add(this[i].ToOriginalType());
                return rv;
            }
        }
	    public class TextData {
	        public string Text { get; set; }
	        public Rect Rect { get; set; }
            public string FontFamily { get; set; }
            FontStyle FontStyle { get; set; }
            FontWeight FontWeight { get; set; }
            FontStretch FontStretch { get; set; }
	        public Color Color { get; set; }
	        public TextData(){}
	        public TextData(ablib.TextData td){
				Text = td.Text;
				Rect = td.Rect;
                FontFamily = td.Font.FontFamily.FamilyNames[System.Windows.Markup.XmlLanguage.GetLanguage(System.Globalization.CultureInfo.CurrentCulture.Name)];
                FontStyle = td.Font.Style;
                FontWeight = td.Font.Weight;
                FontStretch = td.Font.Stretch;
				Color = td.Color;
			}
			public ablib.TextData ToOriginalType(){
				return new ablib.TextData(Text,Rect,new Typeface(new FontFamily(FontFamily),FontStyle,FontWeight,FontStretch),Color);
			}
	    }
	    public class TextDataCollection : List<TextData> { 
			public TextDataCollection(ablib.TextDataCollection tdc) : base(tdc.Count){
				foreach(var td in tdc)base.Add(new TextData(td));
			}
			public TextDataCollection(){}
			public ablib.TextDataCollection ToOriginalType(){
				var rv = new ablib.TextDataCollection();
				for(int i = 0 ; i < Count ; ++i)rv.Add(this[i].ToOriginalType());
				return rv;
			}
		}
        public class InkData {
            public StrokeDataCollection Strokes;
            public TextDataCollection Texts;
            public InkData(ablib.InkData data) {
                Strokes = new StrokeDataCollection(data.Strokes);
                Texts = new TextDataCollection(data.Texts);
            }
            public InkData() { }
        }
    }

    #region Stroke描画の実装
    partial class StrokeData {
        //public static StylusPointCollection HoseiPts = new StylusPointCollection();
        static DoubleCollection Weight = new DoubleCollection();
        static StrokeData() {
            // 手ぶれ？補正のWeightの初期化
            double s = 1;
            for(int i = 0 ; i < 20 ; ++i) {
                Weight.Add(Math.Exp(-i * i / (2 * s * s)));
            }
        }

        static StylusPointCollection GetHoseiPoints(StylusPointCollection Points) {
            //HoseiPts.Clear();
            StylusPointCollection rv = new StylusPointCollection(Points.Count);
            // 手ぶれ？補正
            // 参考：http://www24.atwiki.jp/sigetch_2007/pages/18.html
            // 単に周辺の点を重み付きで足しているだけです．
            // 重みはe^{-x^2}がよさげ（上のURLから）なのでそうしている．
            for(int i = 0 ; i < Points.Count ; ++i) {
                var pt = new StylusPoint();
                double wsum = 0;
                for(int j = Math.Max(0, i - 3) ; j < Math.Min(Points.Count, i + 3) ; ++j) {
                    double w = Weight[Math.Abs(j - i)];
                    wsum += w;
                    pt.X += Points[j].X * w;
                    pt.Y += Points[j].Y * w;
                }
                pt.X /= wsum;
                pt.Y /= wsum;
                pt.PressureFactor = Points[i].PressureFactor;
                rv.Add(pt);
                //HoseiPts.Add(pt);
            }
            return rv;

        }

        Geometry GetOriginalGeometryType1(PointCollection Points) {
            if(Points.Count <= 1) {
                return new StreamGeometry();
            }
            StreamGeometry rv = new StreamGeometry();
            using(var ctx = rv.Open()) {
                ctx.BeginFigure(Points[0], false, false);
                PointCollection ctrl1 = new PointCollection(), ctrl2 = new PointCollection();
                GenerateBezierControlPointsType1(Points, ref ctrl1, ref ctrl2);
                for(int i = 1 ; i < Points.Count ; ++i) {
                    ctx.BezierTo(ctrl1[i - 1], ctrl2[i - 1], Points[i], true, false);
                }
            }
            rv.Freeze();
            return rv;
        }

        // 「不要そう」な点を消す．
        // 点から点に線をひいて，どのくらいずれているか計測する．
        // ずれが大きくないならば，その間を間引く
        // i : 手元の点，j：先の点
        PointCollection MabikiPointsType1(StylusPointCollection Points) {
            var nokoriPoints = new PointCollection(Points.Count / 2);
            nokoriPoints.Add(Points[0].ToPoint());
            for(int i = 0 ; i < Points.Count - 1 ; ++i) {
                for(int j = i + 2 ; j < Points.Count - 1 ; ++j) {
                    // 間を結ぶ直線の法線
                    var hou = new Vector(Points[j].Y - Points[i].Y, -Points[j].X + Points[i].X);
                    double c = -hou.X * Points[i].X - hou.Y * Points[i].Y;
                    // 直線：(hou,x) + c = 0
                    c = c / hou.Length;
                    hou.Normalize();
                    bool mabiku = false;
                    for(int k = i + 1 ; k < j ; ++k) {
                        double length = Math.Abs(hou.X * Points[k].X + hou.Y * Points[k].Y + c);
                        if(length > 0.1) {
                            mabiku = true;
                            break;
                        }
                    }
                    if(mabiku) {
                        nokoriPoints.Add(Points[j].ToPoint());
                        i = j;
                        break;
                    }
                }
            }
            nokoriPoints.Add(Points.Last().ToPoint());
            return nokoriPoints;
        }
        /*
        // とんがり点を探す．これは間引かない．
        hoseiPoints[0] = new PointWithBool(hoseiPoints[0].Point, true);
        hoseiPoints[hoseiPoints.Count - 1] = new PointWithBool(hoseiPoints[hoseiPoints.Count - 1].Point, true);
        if(hoseiPoints.Count > 2) {
            Vector prevSa = hoseiPoints[1].Point - hoseiPoints[0].Point;
            prevSa.Normalize();
            for(int i = 1 ; i < hoseiPoints.Count - 1 ; ++i) {
                Vector Sa = hoseiPoints[i + 1].Point - hoseiPoints[i].Point;
                Sa.Normalize();
                if(prevSa * Sa < 0.9) {
                    hoseiPoints[i] = new PointWithBool(hoseiPoints[i].Point, true);
                    CuspPts.Add(hoseiPoints[i].Point);
                }
                prevSa = Sa;
            }
        }
        */


        PointCollection StylusPointCollection2PointCollection(StylusPointCollection Points) {
            return new PointCollection(Points.Select(p => p.ToPoint()));
        }

        // Bezier曲線の制御点を計算する
        // http://www.antigrain.com/research/bezier_interpolation/
        // から．
        // 端のところのcontrol pointは最初の点にする．
        void GenerateBezierControlPointsType1(PointCollection Points, ref PointCollection ctrlpt1, ref PointCollection ctrlpt2) {
            System.Diagnostics.Debug.Assert(Points.Count >= 2);
            ctrlpt1.Clear(); ctrlpt2.Clear();
            Point firstCtrlPoint = Points[0];
            double prevLength = (Points[1] - Points[0]).Length;
            for(int i = 1 ; i < Points.Count - 1 ; ++i) {
                double length = (Points[i + 1] - Points[i]).Length;
                Vector vec = (Points[i + 1] - Points[i - 1]) / 2;
                ctrlpt1.Add(firstCtrlPoint);
                ctrlpt2.Add(Points[i] - (vec * prevLength / (length + prevLength)));
                firstCtrlPoint = Points[i] + (vec * length) / (length + prevLength);
                prevLength = length;
            }
            ctrlpt1.Add(firstCtrlPoint);
            ctrlpt2.Add(Points.Last());
        }

        public PdfSharp.Drawing.XGraphicsPath GetPDFPath() {
            var path = new PdfSharp.Drawing.XGraphicsPath();
            PointCollection pts = MabikiPointsType1(StylusPoints);
            PointCollection cpt1 = new PointCollection(), cpt2 = new PointCollection();
            GenerateBezierControlPointsType1(pts, ref cpt1, ref cpt2);

            path.StartFigure();
            for(int i = 0 ; i < pts.Count - 1 ; ++i) {
                path.AddBezier(pts[i].X, pts[i].Y, cpt1[i].X, cpt1[i].Y, cpt2[i].X, cpt2[i].Y, pts[i + 1].X, pts[i + 1].Y);
            }
            return path;
        }

        /*
        public void GetPDFPath(iTextSharp.text.pdf.PdfContentByte cb,float height) {
            cb.SetColorStroke(new iTextSharp.text.BaseColor(
                DrawingAttributes.Color.R,
                DrawingAttributes.Color.G,
                DrawingAttributes.Color.B,
                DrawingAttributes.Color.A
                ));
            cb.SetLineWidth((float) DrawingAttributes.Width);
            PointCollection pts = new PointCollection(StylusPoints.Count / 3);
            MabikiPoints(StylusPoints, ref pts);
            PointCollection cpt1 = new PointCollection(), cpt2 = new PointCollection();
            GenerateBezierControlPointsType1(pts, ref cpt1, ref cpt2);
            cb.MoveTo((float) pts[0].X, height - (float) pts[0].Y);
            for(int i = 0 ; i < pts.Count - 1 ; ++i) {
                cb.CurveTo((float) cpt1[i].X, height - (float) cpt1[i].Y, (float) cpt2[i].X, height - (float) cpt2[i].Y, (float) pts[i + 1].X, height - (float) pts[i + 1].Y);
            }
            cb.Stroke();
        }*/

    }

    /*
    partial class StrokeData {
        // ベジェ曲線の制御点を返す関数．
        // 中身は適当で，x,y座標がともにtの関数としてC^2になるようになっているだけ．
        // 後は端点の2次導関数 = 0
        // Stroke.GetGeometryで十分なので削除．
        public void GetBezierControlPoint(out PointCollection pt1, out PointCollection pt2) {
            
            if(BezierControlPoints1 != null) {
                pt1 = BezierControlPoints1;
                pt2 = BezierControlPoints2;
                return;
            }
            // 連立一次方程式を解いて，右側の点を計算していく
            // こんなんだと思う
            // 係数行列：
            //  1 2/7
            //  1   4   1
            //  1   4   1
            //  .....
            //  1   4   1
            //  1   2
            //
            // 右辺は点のx座標をx0,,,,x_Nとして
            // (x0 + 8*x1)/7
            // 2*x1 + 4*x2
            // 2*x2 + 4*x3
            // ....
            // 2*x(N - 2) + 4*x(N - 1)
            // xN + 2*x(N - 1)
            // 
            // y座標も同様
            int N = StylusPoints.Count - 1;// 全部でN+1点
            if(N == 0) {
                pt1 = new PointCollection();
                pt2 = new PointCollection();
                return;
            } else if(N == 1) {
                pt1 = new PointCollection();
                pt2 = new PointCollection();
                pt1.Add(new Point((2 * StylusPoints[0].X + StylusPoints[1].X) / 3, (2 * StylusPoints[0].Y + StylusPoints[1].Y) / 3));
                pt2.Add(new Point((StylusPoints[0].X + 2 * StylusPoints[1].X) / 3, (StylusPoints[0].Y + 2 * StylusPoints[1].Y) / 3));
                return;
            }
            Point[] ctrlpt = new Point[StylusPoints.Count - 1];
            double[] a = new double[StylusPoints.Count - 1];
            ctrlpt[0].X = (StylusPoints[0].X + 8 * StylusPoints[1].X) / 7;
            ctrlpt[0].Y = (StylusPoints[0].Y + 8 * StylusPoints[1].Y) / 7;
            for(int i = 1 ; i <= N - 2 ; ++i) {
                ctrlpt[i].X = 2 * StylusPoints[i].X + 4 * StylusPoints[i + 1].X;
                ctrlpt[i].Y = 2 * StylusPoints[i].Y + 4 * StylusPoints[i + 1].Y;
            }
            ctrlpt[N - 1].X = 2 * StylusPoints[N - 1].X + StylusPoints[N].X;
            ctrlpt[N - 1].Y = 2 * StylusPoints[N - 1].Y + StylusPoints[N].Y;
            a[0] = 2.0 / 7;
            for(int i = 0 ; i < N - 2 ; ++i) {
                a[i + 1] = 1 / (4 - a[i]);
                ctrlpt[i + 1].X = a[i + 1] * (ctrlpt[i + 1].X - ctrlpt[i].X);
                ctrlpt[i + 1].Y = a[i + 1] * (ctrlpt[i + 1].Y - ctrlpt[i].Y);
            }
            a[N - 1] = 1 / (2 - a[N - 2]);
            ctrlpt[N - 1].X = a[N - 1] * (ctrlpt[N - 1].X - ctrlpt[N - 2].X);
            ctrlpt[N - 1].Y = a[N - 1] * (ctrlpt[N - 1].Y - ctrlpt[N - 2].Y);
            for(int i = N - 2 ; i >= 0 ; --i) {
                ctrlpt[i].X = ctrlpt[i].X - a[i] * ctrlpt[i + 1].X; 
                ctrlpt[i].Y = ctrlpt[i].Y - a[i] * ctrlpt[i + 1].Y;
            }
            pt2 = new PointCollection(ctrlpt);
            // もう一つ点は，今得た点をp，もう一つをqとした時
            // p(i - 1) + qi = 2*xi ( 1 <= i <= N - 1)
            // p0 - 2*q0 = -x0
            // で計算する
            for(int i = N - 1 ; i >= 1 ; --i) {
                ctrlpt[i].X = 2 * StylusPoints[i].X - ctrlpt[i - 1].X;
                ctrlpt[i].Y = 2 * StylusPoints[i].Y - ctrlpt[i - 1].Y;
            }
            ctrlpt[0].X = (ctrlpt[0].X + StylusPoints[0].X) / 2;
            ctrlpt[0].Y = (ctrlpt[0].Y + StylusPoints[0].Y) / 2;
            pt1 = new PointCollection(ctrlpt);
            return;
        }
    }
     */
    #endregion
}

