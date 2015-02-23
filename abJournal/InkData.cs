using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Runtime.Serialization;
using System.Xml;
using ProtoBuf;

namespace ablib {
    /**
     * 管理したいもの
     * Stroke
     * テキスト
     * 選択状態
     */
    public enum InkManipulationMode { Inking = 0, Erasing = 1, Selecting = 2 };
    public enum DrawingAlgorithm { dotNet = 0, Type1 = 1, Type1WithHosei = 2, Line = 3 };
    [ProtoContract]
    public class DrawingAttributesPlus : System.ComponentModel.INotifyPropertyChanged {
        public static DoubleCollection NormalDashArray = new DoubleCollection();
        public DrawingAttributesPlus() {
            DashArray = new DoubleCollection();
        }
        DoubleCollection dashArray;
        [ProtoMember(1)]
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
    [ProtoContract]
    public partial class StrokeData : Stroke {
        public DrawingAlgorithm algorithm = DrawingAlgorithm.dotNet;
        public DrawingAlgorithm Algorithm {
            set { if(value != algorithm)redraw = true; algorithm = value; }
        }
        bool selected;
        public bool Selected {
            get { return selected; }
            set { if(selected != value)redraw = true; selected = value; }
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
            set { base.DrawingAttributes = value; redraw = true; brush = null; }
        }
        DrawingAttributesPlus drawingAttributesPlus = new DrawingAttributesPlus();
        [ProtoMember(1)]
        public DrawingAttributesPlus DrawingAttributesPlus {
            get { return drawingAttributesPlus; }
            set { drawingAttributesPlus = value; redraw = true; }
        }

        // protobuf用．空っぽにしたりSkipConstructor=trueにしたりすると駄目みたい．
        // よくわからない……．
        StrokeData() : base(new StylusPointCollection(new Point[]{new Point(0,0)})){
            DrawingAttributes.AttributeChanged += ((s, e) => { redraw = true; brush = null; });
            DrawingAttributesPlus.PropertyChanged += ((s, e) => { redraw = true; });
            visual = new DrawingVisual();
        }
        public StrokeData(StylusPointCollection spc, DrawingAttributes att, DrawingAttributesPlus attplus, DrawingAlgorithm algo, bool selecting = false)
            : base(spc, att.Clone()) {
            DrawingAttributes.AttributeChanged += ((s, e) => { redraw = true; brush = null; });
            DrawingAttributesPlus.PropertyChanged += ((s, e) => { redraw = true; });
            visual = new DrawingVisual();
            Selected = selecting;
            drawingAttributesPlus = attplus;
            Algorithm = algo;
        }

        public override void Transform(Matrix transformMatrix, bool applyToStylusTip) {
            if(matrixTransform == null) {
                matrixTransform = new MatrixTransform(transformMatrix);
                visual.Transform = matrixTransform;
            } else {
                matrixTransform.Matrix = matrixTransform.Matrix * transformMatrix;
            }
            base.Transform(transformMatrix, applyToStylusTip);
        }
        DrawingVisual visual;
        bool redraw = true;
        MatrixTransform matrixTransform = null;

        public void ReDraw() { redraw = true; }
        public DrawingVisual GetVisual(bool selecting) {
            if(Selected != selecting) {
                redraw = true;
                var save = visual;
                visual = new DrawingVisual();
                GetVisual(DrawingAttributes, DrawingAttributesPlus, selecting, algorithm);
                var rv = visual;
                visual = save;
                return rv;
            } else return visual;
        }
        public DrawingVisual GetVisual() {
            return GetVisual(DrawingAttributes, DrawingAttributesPlus, Selected,algorithm);
        }
        // Strokg.GetGeometry = 厚みがある：「二重線」ができる
        // GetOriginalGeometryType1：線でひく：破線が引ける
        // これらの特徴のため，Algoithmが無視されることがある
        public DrawingVisual GetVisual(DrawingAttributes dattr, DrawingAttributesPlus dattrPlus, bool selecting, DrawingAlgorithm algo) {
            if(!redraw) return visual;
            redraw = false;
            matrixTransform = null;
            
            using(var dc = visual.RenderOpen()) {
                if(selecting) {
                    var geom = base.GetGeometry(dattr);
                    dc.DrawGeometry(null, new Pen(Brush, dattr.Width / 5), geom);
                } else {
                    switch(algo) {
                    case DrawingAlgorithm.Type1WithHosei:
                        DrawOriginalType1(dc, MabikiPointsType1(GetHoseiPoints(StylusPoints),dattr),dattr,dattrPlus);
                        break;
                    case DrawingAlgorithm.Type1:
                        DrawOriginalType1(dc, MabikiPointsType1(StylusPoints, dattr), dattr, dattrPlus);
                        break;
                    case DrawingAlgorithm.Line:
                        DrawOriginalLine(dc, StylusPoints, dattr, dattrPlus);
                        break;
                    default:
                        if(dattrPlus.IsNormalDashArray) {
                            base.Draw(dc, dattr);
                        } else {
                            DrawOriginalType1(dc, GetHoseiPoints(StylusPoints),dattr,dattrPlus);
                        }
                        break;
                    }
                }
            }
            return visual;
        }

        public new StrokeData Clone() {
            return new StrokeData(StylusPoints, DrawingAttributes, DrawingAttributesPlus, algorithm);
        }

    }

    [ProtoContract]
    public class StrokeDataCollection : List<StrokeData> {
        public StrokeDataCollection() { }
        public StrokeDataCollection(int capacity) : base(capacity) { }
        public StrokeDataCollection(IEnumerable<StrokeData> collection) : base(collection) { }
    }

    [ProtoContract]
    public class TextData {
        [ProtoMember(1)]
        public string Text { get; set; }
        [ProtoMember(2)]
        public Rect Rect { get; set; }
        [ProtoMember(3)]
        public FontFamily FontFamily { get; set; }
        [ProtoMember(4)]
        public double FontSize { get; set; }
        [ProtoMember(5)]
        public FontStyle FontStyle { get; set; }
        [ProtoMember(6)]
        public FontWeight FontWeight { get; set; }
        [ProtoMember(7)]
        public Color Color { get; set; }
        public bool Selected { get; set; }
        public TextData(string text) {
            Rect = new Rect();
            FontFamily = new FontFamily("ＭＳ ゴシック");
            Text = text;
        }
        public TextData(string text,Rect rect,FontFamily family,double size,FontStyle style,FontWeight weight,Color color){
			Text = text; Rect = rect; FontFamily = family;
            FontStyle = style; FontWeight = weight; Color = color;
		}
    }
    [ProtoContract]
    public class TextDataCollection : List<TextData> { }
    [ProtoContract]
    public class InkData {
        [ProtoMember(1)]
        public StrokeDataCollection Strokes { get; set; }
        [ProtoMember(2)]
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
					for(int i = UndoStack.Count - 1 ; i >= UndoStack.Count - EditCount && i >= 0 ; --i){
                        if(!(UndoStack[i] is SelectChangeCommand)) return true;
                    }
                    return false;
                } else {
					for(int i = RedoStack.Count - 1 ; i >= RedoStack.Count + EditCount && i >= 0 ; --i){
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
        public StrokeDataCollection GetSelectedStrokes() {
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
                        deleted.Add(s);
                        return true;
                    } else return false;
                });
                ProcessUndoGroup.Add(new DeleteStrokeCommand(deleted));
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
            void Combine(UndoCommand c);
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
            public void Combine(UndoCommand del) {
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
            public void Combine(UndoCommand add) {
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
            public void Combine(UndoCommand m) {
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
            public StrokeChangedEventArgs(IEnumerable<StrokeData> sdc) { Strokes = new StrokeDataCollection(sdc); }
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

        [ProtoContract]
        class ProtoStylusPointCollection {
            [ProtoMember(1,OverwriteList=true)]
            List<StylusPoint> Points = new List<StylusPoint>();
            public ProtoStylusPointCollection(StylusPointCollection spc){
                Points = spc == null ? null : new List<StylusPoint>(spc.ToList());
                //Points = spc.ToList();
            }
            public static implicit operator StylusPointCollection(ProtoStylusPointCollection pspc) {
                return new StylusPointCollection(pspc.Points);
            }
            public static implicit operator ProtoStylusPointCollection(StylusPointCollection spc) {
                return new ProtoStylusPointCollection(spc);
            }
        }
        [ProtoContract]
        class ProtoFontFamily {
            [ProtoMember(1)]
            string name;
            public ProtoFontFamily(FontFamily ff){
                name = ff.FamilyNames[System.Windows.Markup.XmlLanguage.GetLanguage(System.Globalization.CultureInfo.CurrentCulture.ToString())];
            }
            public static implicit operator ProtoFontFamily(FontFamily ff) {
                return new ProtoFontFamily(ff);
            }
            public static implicit operator FontFamily(ProtoFontFamily pff) {
                return new FontFamily(pff.name);
            }
        }
        [ProtoContract(SkipConstructor=true)]
        class ProtoFontStyle {
            enum Style { Normal = 1, Italic = 2, Oblique = 3 };
            [ProtoMember(1)]
            Style style;
            ProtoFontStyle(Style s) { style = s; }
            public static implicit operator FontStyle(ProtoFontStyle pfs){
                switch(pfs.style) {
                case Style.Italic: return FontStyles.Italic;
                case Style.Oblique: return FontStyles.Oblique;
                default: return FontStyles.Normal;
                }
            }
            public static implicit operator ProtoFontStyle(FontStyle fs) {
                if(fs == FontStyles.Italic) return new ProtoFontStyle(Style.Italic);
                else if(fs == FontStyles.Oblique) return new ProtoFontStyle(Style.Oblique);
                else return new ProtoFontStyle(Style.Normal);
            }
        }
        [ProtoContract(SkipConstructor=true)]
        class ProtoFontWeight {
            enum Weight { Thin = 1, ExtraLight = 2, UltraLight = 3, Light = 4, Normal = 5, Regular = 6, Medium = 7, DemiBold = 8, SemiBold = 9, Bold = 10, ExtraBold = 11, UltraBold = 12, Black = 13, Heavy = 14, ExtraBlack = 15, UltraBlack = 16};
            [ProtoMember(1)]
            Weight weight;
            ProtoFontWeight(Weight w){weight = w;}
            public static implicit operator FontWeight(ProtoFontWeight pfw) {
                switch(pfw.weight) {
                case Weight.Thin: return FontWeights.Thin;
                case Weight.ExtraLight: return FontWeights.ExtraLight;
                case Weight.UltraLight: return FontWeights.UltraLight;
                case Weight.Light: return FontWeights.Light;
                case Weight.Regular: return FontWeights.Regular;
                case Weight.Medium: return FontWeights.Medium;
                case Weight.DemiBold: return FontWeights.DemiBold;
                case Weight.SemiBold: return FontWeights.SemiBold;
                case Weight.Bold: return FontWeights.Bold;
                case Weight.ExtraBold: return FontWeights.ExtraBold;
                case Weight.UltraBold: return FontWeights.UltraBold;
                case Weight.Black: return FontWeights.Black;
                case Weight.Heavy: return FontWeights.Heavy;
                case Weight.ExtraBlack: return FontWeights.ExtraBlack;
                case Weight.UltraBlack: return FontWeights.UltraBlack;
                default: return FontWeights.Normal;
                }
            }
            public static implicit operator ProtoFontWeight(FontWeight fw) {
                if(fw == FontWeights.Thin) return new ProtoFontWeight(Weight.Thin);
                else if(fw == FontWeights.ExtraLight) return new ProtoFontWeight(Weight.ExtraLight);
                else if(fw == FontWeights.UltraLight) return new ProtoFontWeight(Weight.UltraLight);
                else if(fw == FontWeights.Light) return new ProtoFontWeight(Weight.Light);
                else if(fw == FontWeights.Regular) return new ProtoFontWeight(Weight.Regular);
                else if(fw == FontWeights.Medium) return new ProtoFontWeight(Weight.Medium);
                else if(fw == FontWeights.DemiBold) return new ProtoFontWeight(Weight.DemiBold);
                else if(fw == FontWeights.SemiBold) return new ProtoFontWeight(Weight.SemiBold);
                else if(fw == FontWeights.Bold) return new ProtoFontWeight(Weight.Bold);
                else if(fw == FontWeights.ExtraBold) return new ProtoFontWeight(Weight.ExtraBold);
                else if(fw == FontWeights.UltraBold) return new ProtoFontWeight(Weight.UltraBold);
                else if(fw == FontWeights.Black) return new ProtoFontWeight(Weight.Black);
                else if(fw == FontWeights.Heavy) return new ProtoFontWeight(Weight.Heavy);
                else if(fw == FontWeights.ExtraBlack) return new ProtoFontWeight(Weight.ExtraBlack);
                else if(fw == FontWeights.UltraBlack) return new ProtoFontWeight(Weight.UltraBlack);
                else return new ProtoFontWeight(Weight.Normal);
            }
        }
        public static ProtoBuf.Meta.RuntimeTypeModel SetProtoBufTypeModel(ProtoBuf.Meta.RuntimeTypeModel model) {
            model.Add(typeof(Stroke), true);
            model[typeof(Stroke)].Add("StylusPoints","DrawingAttributes");
            model[typeof(Stroke)].AddSubType(100, typeof(StrokeData));
            model.Add(typeof(StylusPoint), true);
            model[typeof(StylusPoint)].Add("X","Y","PressureFactor");
            model.Add(typeof(DrawingAttributes), true);
            model[typeof(DrawingAttributes)].Add("Color","FitToCurve","Height","IgnorePressure","IsHighlighter","StylusTip","Width");
            model.Add(typeof(Color), true);
            model[typeof(Color)].Add("A", "R", "G", "B");
            model.Add(typeof(StylusPointCollection), false).SetSurrogate(typeof(ProtoStylusPointCollection));
            model.Add(typeof(Size), true);
            model[typeof(Size)].Add("Height", "Width");
            model.Add(typeof(Rect), true);
            model[typeof(Rect)].Add("X", "Y", "Width", "Height");
            model.Add(typeof(FontFamily), false).SetSurrogate(typeof(ProtoFontFamily));
            model.Add(typeof(FontStyle), false).SetSurrogate(typeof(ProtoFontStyle));
            model.Add(typeof(FontWeight), false).SetSurrogate(typeof(ProtoFontWeight));
            return model;
        }

        public InkData Clone() {
            var strokes = new StrokeDataCollection(Strokes.Count);
            foreach(var d in Strokes) strokes.Add(d.Clone());
            var texts = new TextDataCollection();
            foreach(var d in Texts) texts.Add(d);
            var rv = new InkData();
            rv.Strokes = strokes;
            rv.Texts = texts;
            rv.DrawingAlgorithm = DrawingAlgorithm;
            return rv;
        }
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
            double FontSize { get; set; }
            public Color Color { get; set; }
	        public TextData(){}
	        public TextData(ablib.TextData td){
				Text = td.Text;
				Rect = td.Rect;
                FontFamily = td.FontFamily.FamilyNames[System.Windows.Markup.XmlLanguage.GetLanguage(System.Globalization.CultureInfo.CurrentCulture.Name)];
                FontStyle = td.FontStyle;
                FontWeight = td.FontWeight;
                FontSize = td.FontSize;
				Color = td.Color;
			}
			public ablib.TextData ToOriginalType(){
                return new ablib.TextData(Text, Rect, new FontFamily(FontFamily), FontSize, FontStyle, FontWeight, Color);
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
            // 単に周辺の点（最大2N+1個）を重み付きで足しているだけです．
            // 重みはe^{-x^2}がよさげ（上のURLから）なのでそうしている．
            const int N = 3;
            for(int i = 0 ; i < Points.Count ; ++i) {
                var pt = new StylusPoint();
                double wsum = 0;
                int first = Math.Max(0, i - N), last = Math.Min(Points.Count - 1, i + N);
                for(int j = first ; j <= last ; ++j) {
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

        void DrawOriginalType1(DrawingContext dc,StylusPointCollection Points,DrawingAttributes dattr,DrawingAttributesPlus dattrplus) {
            if(Points.Count <= 1) {
                return;
            }
            if(dattr.IgnorePressure){
                StreamGeometry geom = new StreamGeometry();
                using(var ctx = geom.Open()) {
                    ctx.BeginFigure(Points[0].ToPoint(), false, false);
                    PointCollection ctrl1 = new PointCollection(), ctrl2 = new PointCollection();
                    GenerateBezierControlPointsType1(Points, ref ctrl1, ref ctrl2);
                    for(int i = 1 ; i < Points.Count ; ++i) {
                        ctx.BezierTo(ctrl1[i - 1], ctrl2[i - 1], Points[i].ToPoint(), true, false);
                    }
                }
                geom.Freeze();
                var pen = new Pen(Brush, dattr.Width);
                pen.DashStyle = new DashStyle(dattrplus.DashArray, 0);
                pen.DashCap = PenLineCap.Flat;
                pen.EndLineCap = pen.StartLineCap = PenLineCap.Round;
                pen.Freeze();
                dc.DrawGeometry(null,pen,geom);
            } else {
                PointCollection ctrl1 = new PointCollection(), ctrl2 = new PointCollection();
                GenerateBezierControlPointsType1(Points, ref ctrl1, ref ctrl2);
                var group = new DrawingGroup();
                double dashOffset = 0;
                for(int i = 1 ; i < Points.Count ; ++i) {
                    StreamGeometry geom = new StreamGeometry();
                    using(var ctx = geom.Open()) {
                        ctx.BeginFigure(Points[i - 1].ToPoint(), false, false);
                        ctx.BezierTo(ctrl1[i - 1], ctrl2[i - 1], Points[i].ToPoint(), true, false);
                    }
                    geom.Freeze();
                    var pen = new Pen(Brush, dattr.Width * Points[i - 1].PressureFactor * 2);
                    if(!dattrplus.IsNormalDashArray){
                        pen.DashStyle = new DashStyle(dattrplus.DashArray,dashOffset);
                        pen.DashCap = PenLineCap.Flat;
                        double dx = Points[i].X - Points[i-1].X,dy = Points[i-1].Y - Points[i].Y;
                        dashOffset +=Math.Sqrt(dx*dx+dy*dy)/pen.Thickness;
                    }
                    pen.EndLineCap = pen.StartLineCap = PenLineCap.Round;
                    pen.Freeze();
                    group.Children.Add(new GeometryDrawing(null, pen, geom));
                }
                group.Freeze();
                dc.DrawDrawing(group);
            }
        }

        void DrawOriginalLine(DrawingContext dc,StylusPointCollection Points,DrawingAttributes dattr,DrawingAttributesPlus dattrplus) {
            if(Points.Count <= 1) {
                return;
            }
            if(dattr.IgnorePressure){
                var pen = new Pen(Brush,dattr.Width);
                if(!dattrplus.IsNormalDashArray){
                    pen.DashStyle = new DashStyle(dattrplus.DashArray,0);
                    pen.DashCap = PenLineCap.Flat;
                }
                for(int i = 1 ; i < Points.Count ; ++i) {
                    dc.DrawLine(pen, Points[i - 1].ToPoint(), Points[i].ToPoint());
                }
            }else{
                double dashOffset = 0;
                for(int i = 1 ; i < Points.Count ; ++i) {
                    var pen = new Pen(Brush, dattr.Width * Points[i - 1].PressureFactor * 2);
                    if(!dattrplus.IsNormalDashArray) {
                        pen.DashStyle = new DashStyle(dattrplus.DashArray, dashOffset);
                        pen.DashCap = PenLineCap.Flat;
                        double dx = Points[i].X - Points[i-1].X,dy = Points[i].Y - Points[i-1].Y;
                        dashOffset += Math.Sqrt(dx * dx + dy * dy) / pen.Thickness;
                    }
                    dc.DrawLine(pen, Points[i - 1].ToPoint(), Points[i].ToPoint());
                }
            }
        }


        // 「不要そう」な点を消す．
        // 点から点に線をひいて，どのくらいずれているか計測する．
        // ずれが大きくないならば，その間を間引く
        // i : 手元の点，j：先の点
        StylusPointCollection MabikiPointsType1(StylusPointCollection Points,DrawingAttributes dattr) {
            var nokoriPoints = new StylusPointCollection(Points.Description,Points.Count / 2);
            nokoriPoints.Add(Points[0]);
            for(int i = 0 ; i < Points.Count - 1 ; ++i) {
                double pressuresum = 0;
                for(int j = i + 2 ; j < Points.Count - 1 ; ++j) {
                    // 間を結ぶ直線の法線
                    pressuresum += Points[j].PressureFactor;
                    double pressuremean = pressuresum / (j - i - 1);
                    var hou = new Vector(Points[j].Y - Points[i].Y, -Points[j].X + Points[i].X);
                    hou.Normalize();
                    // 直線：(hou,x) + c = 0
                    double c = -hou.X * Points[i].X - hou.Y * Points[i].Y;
                    bool mabiku = false;
                    for(int k = i + 1 ; k < j ; ++k) {
                        double length = Math.Abs(hou.X * Points[k].X + hou.Y * Points[k].Y + c);
                        if(length > 0.1) {
                            mabiku = true;
                            break;
                        }
                        if(!dattr.IgnorePressure && Math.Abs(Points[k].PressureFactor - pressuremean) > 0.1) {
                            mabiku = true;
                            break;
                        }
                    }
                    if(mabiku) {
                        nokoriPoints.Add(Points[j]);
                        i = j;
                        break;
                    }
                }
            }
            nokoriPoints.Add(Points.Last());
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

        // Bezier曲線の制御点を計算する
        // http://www.antigrain.com/research/bezier_interpolation/
        // から．
        // 端のところのcontrol pointは最初の点にする．
        void GenerateBezierControlPointsType1(StylusPointCollection Points, ref PointCollection ctrlpt1, ref PointCollection ctrlpt2) {
            System.Diagnostics.Debug.Assert(Points.Count >= 2);
            ctrlpt1.Clear(); ctrlpt2.Clear();
            Point firstCtrlPoint = Points[0].ToPoint();
            double prevLength = (Points[1].ToPoint() - Points[0].ToPoint()).Length;
            for(int i = 1 ; i < Points.Count - 1 ; ++i) {
                double length = (Points[i + 1].ToPoint() - Points[i].ToPoint()).Length;
                Vector vec = (Points[i + 1].ToPoint() - Points[i - 1].ToPoint()) / 2;
                ctrlpt1.Add(firstCtrlPoint);
                ctrlpt2.Add(Points[i].ToPoint() - (vec * prevLength / (length + prevLength)));
                firstCtrlPoint = Points[i].ToPoint() + (vec * length) / (length + prevLength);
                prevLength = length;
            }
            ctrlpt1.Add(firstCtrlPoint);
            ctrlpt2.Add(Points.Last().ToPoint());
        }

        public PdfSharp.Drawing.XGraphicsPath GetPDFPath(){
            var path = new PdfSharp.Drawing.XGraphicsPath();
            StylusPointCollection pts = MabikiPointsType1(StylusPoints,DrawingAttributes);
            PointCollection cpt1 = new PointCollection(), cpt2 = new PointCollection();
            GenerateBezierControlPointsType1(pts, ref cpt1, ref cpt2);
            path.StartFigure();
            for(int i = 0 ; i < pts.Count - 1 ; ++i) {
                path.AddBezier(pts[i].X, pts[i].Y, cpt1[i].X, cpt1[i].Y, cpt2[i].X, cpt2[i].Y, pts[i + 1].X, pts[i + 1].Y);
            }
            return path;
        }
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

