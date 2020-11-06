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

namespace abJournal {
    /**
     * 管理したいもの
     * Stroke
     * テキスト
     * 選択状態
     */
    public enum InkManipulationMode { Inking = 0, Erasing = 1, Selecting = 2 };
    public enum DrawingAlgorithm { dotNet = 0, Type1 = 1, Type1WithHosei = 2, Line = 3 };

    [ProtoContract]
    public class abInkData : System.ComponentModel.INotifyPropertyChanged{
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged = (s, e) => { };
        private void OnPropertyChanged(string name) {
            PropertyChanged(this, new System.ComponentModel.PropertyChangedEventArgs(name));
        }
        [ProtoMember(1)]
        public StrokeDataCollection Strokes { get; set; }
        [ProtoMember(2)]
        public TextDataCollection Texts { get; set; }

        public abInkData() {
            Strokes = new StrokeDataCollection();
            Texts = new TextDataCollection();
        }
        DrawingAlgorithm drawingAlgorithm = DrawingAlgorithm.dotNet;
        public DrawingAlgorithm DrawingAlgorithm {
            get { return drawingAlgorithm; }
            set {
                if(drawingAlgorithm != value) {
                    drawingAlgorithm = value;
                    foreach(var s in Strokes) s.Algorithm = value;
                    OnPropertyChanged("DrawingAlgorithm");
                }
            }
        }
        bool ignorePressure = false;
        public bool IgnorePressure {
            get { return ignorePressure; }
            set {
                if(ignorePressure != value){
                    ignorePressure = value;
                    foreach(var s in Strokes) s.DrawingAttributes.IgnorePressure = value;
                    OnPropertyChanged("IgnorePressure");
                }
            }
        }
        bool UpdatedUndoGroup(UndoGroup undo) {
            foreach(var u in undo) {
                if(UpdatedUndoCommand(u)) return true;
            }
            return false;
        }
        bool UpdatedUndoCommand(UndoCommand undo) {
            var type = undo.GetType();
            if(type != typeof(SelectChangeCommand)) {
                if(type == typeof(UndoGroup)) {
                    if(UpdatedUndoGroup((UndoGroup) undo)) return true;
                } else return true;
            }
            return false;
        }
        public bool Updated {
            get {
                if(EditCount == 0) return false;
                else if(EditCount > 0) {
					for(int i = UndoStack.Count - 1 ; i >= UndoStack.Count - EditCount && i >= 0 ; --i){
                        if(UpdatedUndoCommand(UndoStack[i])) return true;
                    }
                    return false;
                } else {
					for(int i = RedoStack.Count - 1 ; i >= RedoStack.Count + EditCount && i >= 0 ; --i){
                        if(UpdatedUndoCommand(RedoStack[i])) return true;
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
            var changed = new StrokeDataCollection();
            foreach(var s in Strokes) {
                bool hittest = s.HitTest(pc, percent);
                if(hittest != s.Selected) {
                    s.Selected = hittest;
                    changed.Add(s);
                }
            }
            if(changed.Count > 0) {
                AddUndoList(new SelectChangeCommand(changed));
                OnStrokeSelectedChanged(new StrokeChangedEventArgs(changed));
            }
        }
        public void SelectAll() {
            var changed = new StrokeDataCollection(Strokes.Where(s => {
                if(!s.Selected) {
                    s.Selected = true;
                    return true;
                } else return false;
            }));
            if(changed.Count > 0) {
                AddUndoList(new SelectChangeCommand(changed));
                StrokeSelectedChanged(this, new StrokeChangedEventArgs(changed));
            }
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
            if(changed.Count > 0) {
                OnStrokeChanged(new StrokeChangedEventArgs(changed));
                AddUndoList(new MoveStrokeCommand(changed, matrices));
            }
        }
        public void DeleteSelected() {
            StrokeDataCollection deleted = new StrokeDataCollection();
            Strokes.RemoveAll(s => {
                if(s.Selected) {
                    deleted.Add(s);
                    return true;
                } else return false;

            });
            if(deleted.Count > 0) {
                OnStrokeDeleted(new StrokeChangedEventArgs(deleted));
                AddUndoList(new DeleteStrokeCommand(deleted));
            }
        }
        public void ClearSelected() {
            var changed = new StrokeDataCollection(Strokes.Where(s => {
                if(s.Selected){
                    s.Selected = false;
                    return true;
                }else return false;
            }));
            if(changed.Count > 0) {
                OnStrokeSelectedChanged(new StrokeChangedEventArgs(changed));
                AddUndoList(new SelectChangeCommand(changed));
            }
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
            ProcessDrawingAttributes.IgnorePressure = ignorePressure;
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
        public static int MaxUndoSize = 1000;
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
            void Undo(abInkData data);
            void Redo(abInkData data);
        }
        interface UndoCommandComibinable : UndoCommand {
            // UndoCommand.Undoの呼び出しがかなり遅いっぽいので，
            // UndoGroupで使う場合に，くっつけられるUndoCommandはくっつけるようにする．
            void Combine(UndoCommand c);
        }

        class UndoGroup : UndoCommand,IEnumerable<UndoCommand> {
            List<UndoCommand> Commands = new List<UndoCommand>();
            public int Count { get { return Commands.Count; } }
            public UndoGroup() { }
            public void Add(UndoCommand c) { Commands.Add(c); }
            public void Undo(abInkData data) {
                for(int i = Commands.Count - 1 ; i >= 0 ; --i) Commands[i].Undo(data);
            }
            public void Redo(abInkData data) {
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
            public IEnumerator<UndoCommand> GetEnumerator() { return Commands.GetEnumerator(); }
            System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() { return Commands.GetEnumerator(); }

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
            public void Undo(abInkData data) {
                foreach(var s in stroke) data.Strokes.Add(s);
                data.StrokeAdded(data, new StrokeChangedEventArgs(stroke));
            }
            public void Redo(abInkData data) {
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
            public void Redo(abInkData data) {
                foreach(var s in stroke) {
                    data.Strokes.Add(s);
                }
                data.OnStrokeAdded(new StrokeChangedEventArgs(stroke));
            }
            public void Undo(abInkData data) {
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
            public void Undo(abInkData data) {
                for(int i = 0 ; i < Strokes.Count ; ++i) {
                    Strokes[i].Selected = !Strokes[i].Selected;
                }
                data.StrokeSelectedChanged(data, new StrokeChangedEventArgs(Strokes));
            }
            public void Redo(abInkData data) {
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
            public void Undo(abInkData data) {
                StrokeDataCollection sdc = new StrokeDataCollection();
                foreach(var x in Matrices) {
                    x.Key.Transform(x.Value.invMatrix, false);
                    sdc.Add(x.Key);
                }
                data.StrokeChanged(data, new StrokeChangedEventArgs(sdc));
            }
            public void Redo(abInkData data) {
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
        // Strokeが（例えば移動なので）変化した時に呼ばれる（StrokeCollectin.StrokeChangedとはちょっと違う．）
        public event StrokeChangedEventHandler StrokeChanged = ((sender, s) => { });
        public event StrokeChangedEventHandler StrokeAdded = ((sender, s) => { });
        public event StrokeChangedEventHandler StrokeDeleted = ((sender, s) => { });
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

        [Serializable]
        struct StylusPointForCopy {
            public double X, Y;
            public float PressureFactor;
        }
        [Serializable]
        class StrokeDataForCopy {
            public List<StylusPointForCopy> StylusPoints = new List<StylusPointForCopy>();
            public byte Red, Blue, Green, Alpha;
            public double Width, Height;
            public double[] DashArray;

            public StrokeDataForCopy(StrokeData sdc) {
                foreach(var pt in sdc.StylusPoints) {
                    StylusPoints.Add(new StylusPointForCopy() { X = pt.X, Y = pt.Y, PressureFactor = pt.PressureFactor });
                }
                Red = sdc.DrawingAttributes.Color.R;
                Blue = sdc.DrawingAttributes.Color.B;
                Green = sdc.DrawingAttributes.Color.G;
                Alpha = sdc.DrawingAttributes.Color.A;
                Width = sdc.DrawingAttributes.Width;
                Height = sdc.DrawingAttributes.Height;
                DashArray = new double[sdc.DrawingAttributesPlus.DashArray.Count];
                for(int i = 0 ; i < DashArray.Count() ; ++i) {
                    DashArray[i] = sdc.DrawingAttributesPlus.DashArray[i];
                }
            }
            public StrokeData ToOriginalType(bool ignorePressure,DrawingAlgorithm algo) {
                var spc = new StylusPointCollection(StylusPoints.Select(s => new StylusPoint(s.X, s.Y, s.PressureFactor)));
                var attr = new DrawingAttributes() {
                    Color = Color.FromArgb(this.Alpha, this.Red, this.Green, this.Blue),
                    Width = this.Width,
                    Height = this.Height,
                    IgnorePressure = ignorePressure
                };
                var attrplus = new DrawingAttributesPlus() {
                    DashArray = new List<double>(this.DashArray)
                };
                return new StrokeData(spc, attr, attrplus, algo);
            }
        }

        public void Paste(Point pt) {
            Paste(pt, true);
        }

        public void Paste() {
            Paste(new Point(0, 0), false);
        }

        void Paste(Point pt, bool delmargine) {
            ClearSelected();
            //DoubleCollection DashArray = new DoubleCollection(new double[] { 1, 1 });
            StrokeDataCollection strokeData = null;
            var dataObj = Clipboard.GetDataObject();
            if(dataObj != null && dataObj.GetDataPresent(typeof(List<StrokeDataForCopy>))){
                var data = (List<StrokeDataForCopy>) dataObj.GetData(typeof(List<StrokeDataForCopy>));
                strokeData = new StrokeDataCollection(data.Select(d => {
                    var r = d.ToOriginalType(IgnorePressure, DrawingAlgorithm);
                    r.Selected = true;
                    return r;
                }));
            }
            if(strokeData == null && Clipboard.ContainsData(StrokeCollection.InkSerializedFormat)) {
                System.IO.MemoryStream stream = (System.IO.MemoryStream) Clipboard.GetData(StrokeCollection.InkSerializedFormat);
                StrokeCollection strokes = new StrokeCollection(stream);
                var dattrplus = new DrawingAttributesPlus();
                strokeData = new StrokeDataCollection(strokes.Select(s => {
                    StrokeData sd = new StrokeData(s.StylusPoints, s.DrawingAttributes, dattrplus, DrawingAlgorithm, true);
                    return sd;
                }));
            }
            if(strokeData != null) {
	            var shift = new Matrix(1, 0, 0, 1, pt.X, pt.Y);
                if(delmargine) {
                    var rect = strokeData.GetBounds();
                    shift.Translate(-rect.Left, -rect.Top);
                }
                foreach(var s in strokeData) s.Transform(shift, true);
                Strokes.AddRange(strokeData);
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
            StrokeCollection sc = new StrokeCollection(Strokes.Where(s => s.Selected).Select(s => new Stroke(s.StylusPoints, s.DrawingAttributes)));
            sc.Save(stream);
            DataObject obj = new DataObject(StrokeCollection.InkSerializedFormat, stream);
            //var sdc = new List<StrokeDataForCopy>(Strokes.Select(d => new StrokeDataForCopy(d)));
            //obj.SetData(typeof(List<StrokeDataForCopy>), sdc);
            Clipboard.SetDataObject(obj, true);
        }

        public abJournal.Saving.InkData GetSavingData() {
            return new abJournal.Saving.InkData(this);
        }
        public void LoadSavingData(abJournal.Saving.InkData data) {
            Strokes = data.Strokes.ToOriginalType();
            //foreach(var s in Strokes) {
                //s.DrawingAttributesPlus.DashArray.Clear();
            //}
            Texts = data.Texts.ToOriginalType();
        }

        /*
        void ShowNodes(ContextNode node, System.IO.StreamWriter fs, int indent) {
            fs.WriteLine(new string('\t', indent) + node.ToString());
            foreach(var n in node.SubNodes) ShowNodes(n, fs, indent + 1);
        }
        */

        public static void AddTextToPDFGraphic(iTextSharp.text.pdf.PdfWriter writer, double scale, List<StrokeDataStruct> Strokes) {
            if (Strokes.Count == 0) return;
            var gstate = new iTextSharp.text.pdf.PdfGState();
            gstate.FillOpacity = 0;
            gstate.StrokeOpacity = 0;
            var font = iTextSharp.text.FontFactory.GetFont("游ゴシック", iTextSharp.text.pdf.BaseFont.IDENTITY_H, iTextSharp.text.pdf.BaseFont.NOT_EMBEDDED, 16);
            using (var analyzer = new InkAnalyzer()) {
                var strokes = new StrokeCollection(Strokes.Select(s => (Stroke)s.ToStrokeData()));
                analyzer.AddStrokes(strokes);
                analyzer.Analyze();
                var nodes = analyzer.FindNodesOfType(ContextNodeType.Line);
                foreach (var node in nodes) {
                    var rect = node.Location.GetBounds();
                    var str = ((LineNode)node).GetRecognizedString();
                    writer.DirectContent.SaveState();
                    writer.DirectContent.SetGState(gstate);
                    writer.DirectContent.BeginText();
                    writer.DirectContent.SetFontAndSize(font.BaseFont, (float)(scale * rect.Height));
                    writer.DirectContent.ShowTextAligned(iTextSharp.text.pdf.PdfContentByte.ALIGN_LEFT, str, (float)(scale * rect.Left), (float)(writer.PageSize.Height - scale * rect.Bottom), 0);
                    writer.DirectContent.EndText();
                    writer.DirectContent.RestoreState();
                }
            }
        }

        public static void AddPdfGarphic(iTextSharp.text.pdf.PdfWriter writer, double scale, List<StrokeDataStruct> Strokes) {
            if(Strokes.Count == 0) return;
            foreach(var s in Strokes) {
                writer.DirectContent.SetColorStroke(new iTextSharp.text.BaseColor(
                        s.DrawingAttributes.Color.R,
                        s.DrawingAttributes.Color.G,
                        s.DrawingAttributes.Color.B,
                        s.DrawingAttributes.Color.A
                    ));
                if(!s.DrawingAttributesPlus.IsNormalDashArray) {
                    writer.DirectContent.SetLineDash(s.DrawingAttributesPlus.DashArray.ToArray(), 0);
                    writer.DirectContent.SetLineCap(iTextSharp.text.pdf.PdfContentByte.LINE_CAP_BUTT);
                } else writer.DirectContent.SetLineCap(iTextSharp.text.pdf.PdfContentByte.LINE_CAP_ROUND);
                var state = new iTextSharp.text.pdf.PdfGState();
                state.StrokeOpacity = s.DrawingAttributes.IsHighlighter ? 0.5f : 1.0f;
                writer.DirectContent.SetGState(state);
                writer.DirectContent.SetLineWidth(s.DrawingAttributes.Width * scale);
                StrokeData.DrawPath(writer, scale, s.StylusPoints, s.DrawingAttributes);
                if(!s.DrawingAttributesPlus.IsNormalDashArray) {
                    writer.DirectContent.SetLineDash(0);
                }
            }
        }

        #region Proto-bufferのためのクラス
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
        public static ProtoBuf.Meta.RuntimeTypeModel SetProtoBufTypeModel2(ProtoBuf.Meta.RuntimeTypeModel model) {
            model.Add(typeof(Color), true);
            model[typeof(Color)].Add("A", "R", "G", "B");
            model.Add(typeof(Size), true);
            model[typeof(Size)].Add("Height", "Width");
            model.Add(typeof(StylusPoint), true);
            model[typeof(StylusPoint)].Add("X", "Y", "PressureFactor");
            model.Add(typeof(StylusPointCollection), false).SetSurrogate(typeof(ProtoStylusPointCollection));
            model.Add(typeof(DrawingAttributes), true);
            model[typeof(DrawingAttributes)].Add("Color", "FitToCurve", "Height", "IgnorePressure", "IsHighlighter", "StylusTip", "Width");
            model.Add(typeof(Rect), true);
            model[typeof(Rect)].Add("X", "Y", "Width", "Height");
            model.Add(typeof(FontFamily), false).SetSurrogate(typeof(ProtoFontFamily));
            model.Add(typeof(FontStyle), false).SetSurrogate(typeof(ProtoFontStyle));
            model.Add(typeof(FontWeight), false).SetSurrogate(typeof(ProtoFontWeight));
            return model;
        }

        #endregion

        public abInkData Clone() {
            var strokes = new StrokeDataCollection(Strokes.Count);
            foreach(var d in Strokes) strokes.Add(d.Clone());
            var texts = new TextDataCollection();
            foreach(var d in Texts) texts.Add(d);
            var rv = new abInkData();
            rv.Strokes = strokes;
            rv.Texts = texts;
            rv.DrawingAlgorithm = DrawingAlgorithm;
            return rv;
        }
    }

    namespace Saving {
        // 保存用のデータ構造（obsolete）
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
            public abJournal.DrawingAttributesPlus DrawingAttributesPlus;
            public StrokeData(abJournal.StrokeData strokeData)
                : base(strokeData) {
                DrawingAttributesPlus = strokeData.DrawingAttributesPlus.Clone();
            }
            public StrokeData() {
                DrawingAttributesPlus = new DrawingAttributesPlus();
            }
            public abJournal.StrokeData ToOriginalType() {
                return new abJournal.StrokeData(StylusPoints.ToOriginalType(), DrawingAttributes, DrawingAttributesPlus, DrawingAlgorithm.dotNet);
            }
        }
        public class StrokeDataCollection : List<StrokeData> {
            public StrokeDataCollection(abJournal.StrokeDataCollection sdc) : base(sdc.Count){
                foreach(var sd in sdc) base.Add(new StrokeData(sd));
            }
            public StrokeDataCollection() { }
            public abJournal.StrokeDataCollection ToOriginalType() {
                var rv = new abJournal.StrokeDataCollection(Count);
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
	        public TextData(abJournal.TextData td){
				Text = td.Text;
				Rect = td.Rect;
                FontFamily = td.FontFamily.FamilyNames[System.Windows.Markup.XmlLanguage.GetLanguage(System.Globalization.CultureInfo.CurrentCulture.Name)];
                FontStyle = td.FontStyle;
                FontWeight = td.FontWeight;
                FontSize = td.FontSize;
				Color = td.Color;
			}
			public abJournal.TextData ToOriginalType(){
                return new abJournal.TextData(Text, Rect, new FontFamily(FontFamily), FontSize, FontStyle, FontWeight, Color);
			}
	    }
        public class TextDataCollection : List<TextData> { 
			public TextDataCollection(abJournal.TextDataCollection tdc) : base(tdc.Count){
				foreach(var td in tdc)base.Add(new TextData(td));
			}
			public TextDataCollection(){}
			public abJournal.TextDataCollection ToOriginalType(){
				var rv = new abJournal.TextDataCollection();
				for(int i = 0 ; i < Count ; ++i)rv.Add(this[i].ToOriginalType());
				return rv;
			}
		}
        public class InkData {
            public StrokeDataCollection Strokes;
            public TextDataCollection Texts;
            public InkData(abJournal.abInkData data) {
                Strokes = new StrokeDataCollection(data.Strokes);
                Texts = new TextDataCollection(data.Texts);
            }
            public InkData() { }
        }
    }

}

