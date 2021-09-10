using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Shapes;
using System.Windows.Media;
using System.Windows.Controls;
using System.Windows.Input;

namespace abJournal {
    // 選択の時に現れる四角のやつ．
    // 拡大縮小と移動ができる．
    // MFCにCRectTrackerってのがあるらしいからたぶんそれ．

    // マウスのボタン押す/移動/ボタン放すにあわせて
    // TrackerStart / TrackerSizeChanged / TrackerEnd が呼ばれるので，それで反応する．
    // 引数内のRectは今のRectTrackerのサイズ．
    class RectTracker : Canvas {
        // 選択位置を表す四角形（MainRectangle）と四隅四辺にある小さな四角形
        Rectangle MainRectangle = new Rectangle(), RectRU = new Rectangle(), RectU = new Rectangle(),
            RectLU = new Rectangle(), RectL = new Rectangle(), RectLD = new Rectangle(),
            RectD = new Rectangle(), RectRD = new Rectangle(), RectR = new Rectangle();

        public enum TrackMode {
            None, Move, ResizeTop, ResizeTopLeft, ResizeLeft, ResizeBottomLeft,
            ResizeBottom, ResizeBottmRight, ResizeRight, ResizeTopRight
        };
        public TrackMode Mode { get; set; }

        // 小さな四角形のサイズ
        double SmallRectSize = 10;

        public RectTracker(double x, double y, double width, double height) {
            Mode = TrackMode.None;
            base.Width = width; base.Height = height;
            base.Background = Brushes.Transparent;
            base.MinHeight = SmallRectSize * 2 + 4; base.MinWidth = SmallRectSize * 2 + 4;
            SetDefaultToComponents();
            SetSmallRectDefaultSize();
            SetSmallRectSizeFromHeight();
            SetSmallRectSizeFromWidth();
            Canvas.SetLeft(this, x); Canvas.SetTop(this, y);
        }

        public RectTracker() {
            SetDefaultToComponents();
            SetSmallRectDefaultSize();
            SetSmallRectSizeFromHeight();
            SetSmallRectSizeFromWidth();
        }

        // この範囲を超えないようにする．
        public Rect MaxSize { get; set; }

        // 設定によらない初期化（一度呼び出せば十分）
        void SetDefaultToComponents() {
            MaxSize = new Rect(double.MinValue, double.MinValue, double.MaxValue, double.MaxValue);
            this.Background = Brushes.Transparent;

            MainRectangle.Stroke = Brushes.Black; MainRectangle.StrokeThickness = 1;
            MainRectangle.StrokeDashArray = new DoubleCollection(new double[] { 2, 2 });
            MainRectangle.Fill = Brushes.Transparent;
            Children.Add(MainRectangle);
            RectRU.Stroke = Brushes.Black; RectRU.StrokeThickness = 1; RectRU.Fill = Brushes.Transparent;
            Children.Add(RectRU);
            RectU.Stroke = Brushes.Black; RectU.StrokeThickness = 1; RectU.Fill = Brushes.Transparent;
            Children.Add(RectU);
            RectLU.Stroke = Brushes.Black; RectLU.StrokeThickness = 1; RectLU.Fill = Brushes.Transparent;
            Children.Add(RectLU);
            RectL.Stroke = Brushes.Black; RectL.StrokeThickness = 1; RectL.Fill = Brushes.Transparent;
            Children.Add(RectL);
            RectLD.Stroke = Brushes.Black; RectLD.StrokeThickness = 1; RectLD.Fill = Brushes.Transparent;
            Children.Add(RectLD);
            RectD.Stroke = Brushes.Black; RectD.StrokeThickness = 1; RectD.Fill = Brushes.Transparent;
            Children.Add(RectD);
            RectRD.Stroke = Brushes.Black; RectRD.StrokeThickness = 1; RectRD.Fill = Brushes.Transparent;
            Children.Add(RectRD);
            RectR.Stroke = Brushes.Black; RectR.StrokeThickness = 1; RectR.Fill = Brushes.Transparent;
            Children.Add(RectR);

            Cursor = Cursors.SizeAll;
            MainRectangle.MouseLeftButtonDown += ((s, e) => { RectTracker_MouseLeftButtonDown(s, e, MainRectangle); });
            MainRectangle.MouseMove += SubRectTracker_MouseMove;
            MainRectangle.MouseLeftButtonUp += SubRectTracker_MouseUp;

            RectRU.Cursor = Cursors.SizeNESW;
            RectRU.MouseLeftButtonDown += ((s, e) => { RectTracker_MouseLeftButtonDown(s, e, RectRU); });
            RectRU.MouseMove += SubRectTracker_MouseMove;
            RectRU.MouseLeftButtonUp += SubRectTracker_MouseUp;

            RectU.Cursor = Cursors.SizeNS;
            RectU.MouseLeftButtonDown += ((s, e) => { RectTracker_MouseLeftButtonDown(s, e, RectU); });
            RectU.MouseMove += SubRectTracker_MouseMove;
            RectU.MouseLeftButtonUp += SubRectTracker_MouseUp;

            RectL.Cursor = Cursors.SizeWE;
            RectL.MouseLeftButtonDown += ((s, e) => { RectTracker_MouseLeftButtonDown(s, e, RectL); });
            RectL.MouseMove += SubRectTracker_MouseMove;
            RectL.MouseLeftButtonUp += SubRectTracker_MouseUp;

            RectLU.Cursor = Cursors.SizeNWSE;
            RectLU.MouseLeftButtonDown += ((s, e) => { RectTracker_MouseLeftButtonDown(s, e, RectLU); });
            RectLU.MouseMove += SubRectTracker_MouseMove;
            RectLU.MouseLeftButtonUp += SubRectTracker_MouseUp;

            RectLD.Cursor = Cursors.SizeNESW;
            RectLD.MouseLeftButtonDown += ((s, e) => { RectTracker_MouseLeftButtonDown(s, e, RectLD); });
            RectLD.MouseMove += SubRectTracker_MouseMove;
            RectLD.MouseLeftButtonUp += SubRectTracker_MouseUp;

            RectD.Cursor = Cursors.SizeNS;
            RectD.MouseLeftButtonDown += ((s, e) => { RectTracker_MouseLeftButtonDown(s, e, RectD); });
            RectD.MouseMove += SubRectTracker_MouseMove;
            RectD.MouseLeftButtonUp += SubRectTracker_MouseUp;

            RectRD.Cursor = Cursors.SizeNWSE;
            RectRD.MouseLeftButtonDown += ((s, e) => { RectTracker_MouseLeftButtonDown(s, e, RectRD); });
            RectRD.MouseMove += SubRectTracker_MouseMove;
            RectRD.MouseLeftButtonUp += SubRectTracker_MouseUp;

            RectR.Cursor = Cursors.SizeWE;
            RectR.MouseLeftButtonDown += ((s, e) => { RectTracker_MouseLeftButtonDown(s, e, RectR); });
            RectR.MouseMove += SubRectTracker_MouseMove;
            RectR.MouseLeftButtonUp += SubRectTracker_MouseUp;
        }

        TrackMode GetTrackMode(object obj) {
            if (obj.Equals(RectU)) return TrackMode.ResizeTop;
            else if (obj.Equals(RectLU)) return TrackMode.ResizeTopLeft;
            else if (obj.Equals(RectL)) return TrackMode.ResizeLeft;
            else if (obj.Equals(RectLD)) return TrackMode.ResizeBottomLeft;
            else if (obj.Equals(RectD)) return TrackMode.ResizeBottom;
            else if (obj.Equals(RectRD)) return TrackMode.ResizeBottmRight;
            else if (obj.Equals(RectR)) return TrackMode.ResizeRight;
            else if (obj.Equals(RectRU)) return TrackMode.ResizeTopRight;
            else return TrackMode.Move;
        }
        /*
        Rectangle GetRectangle(TrackMode mode) {
            switch(mode) {
            case TrackMode.ResizeBottmRight: return RectRD;
            case TrackMode.ResizeBottom: return RectD;
            case TrackMode.ResizeBottomLeft: return RectLD;
            case TrackMode.ResizeLeft: return RectL;
            case TrackMode.ResizeRight: return RectR;
            case TrackMode.ResizeTop: return RectU;
            case TrackMode.ResizeTopLeft: return RectLU;
            case TrackMode.ResizeTopRight: return RectRU;
            case TrackMode.Move: return MainRectangle;
            default: return null;
            }
        }
        */

        public void Move(Point pt) {
            if (Mode != TrackMode.None) {
                double x = Canvas.GetLeft(this);
                double y = Canvas.GetTop(this);
                if (x != double.NaN && y != double.NaN) {
                    Vector vec = new Vector(pt.X - x, pt.Y - y);
                    StartPoint += vec;
                    StartRect = new Rect(StartRect.TopLeft + vec, StartRect.Size);
                    PrevRect = new Rect(PrevRect.TopLeft + vec, PrevRect.Size);
                }
            }
            Canvas.SetLeft(this, pt.X);
            Canvas.SetTop(this, pt.Y);
            return;
        }

        public void Move(Rect rect) {
            if (Mode != TrackMode.None) {
                double x = Canvas.GetLeft(this);
                double y = Canvas.GetTop(this);
                if (x != double.NaN && y != double.NaN) {
                    Vector vec = new Vector(rect.X - x, rect.Y - y);
                    StartPoint += vec;
                    StartRect = new Rect(StartRect.TopLeft + vec, StartRect.Size);
                    PrevRect = new Rect(PrevRect.TopLeft + vec, PrevRect.Size);
                }
            }
            Canvas.SetLeft(this, rect.X);
            Canvas.SetTop(this, rect.Y);
            Height = rect.Height;
            Width = rect.Width;
        }

        // マウスイベント用の一時変数
        Point StartPoint;
        Rect StartRect;
        Rect PrevRect;
        double HeightwaruWidth;
        void SubRectTracker_MouseUp(object sender, MouseButtonEventArgs e) {
            if (Mode != TrackMode.None) {
                //GetRectangle(Mode).ReleaseMouseCapture();
                ((UIElement)sender).ReleaseMouseCapture();
                e.Handled = true;
                double x = Canvas.GetLeft(this), y = Canvas.GetTop(this);
                OnTrackerEnd(new TrackerEventArgs(new Rect(x, y, Width, Height)));
            }
            MouseUp(sender, e);
            Mode = TrackMode.None;
        }

        void RectTracker_MouseLeftButtonDown(object sender, MouseButtonEventArgs e, UIElement rect) {
            RectTracker_MouseLeftButtonDown(sender, e);
        }
        void RectTracker_MouseLeftButtonDown(object sender, MouseButtonEventArgs e) {
            ((UIElement)sender).CaptureMouse();
            e.Handled = true;
            //StartPoint = e.GetPosition(this);
            StartPoint = e.GetPosition((IInputElement)VisualParent);
            StartRect = new Rect(Canvas.GetLeft(this), Canvas.GetTop(this), Width, Height);
            HeightwaruWidth = Height / Width;
            Mode = GetTrackMode(sender);
            PrevRect = StartRect;
            OnTrackerStart(new TrackerEventArgs(PrevRect));
            MouseDown(sender, e);
        }

        void SubRectTracker_MouseMove(object sender, MouseEventArgs e) {
            if (Mode != TrackMode.None) {
                e.Handled = true;
                //Point p = e.GetPosition(this);
                Point p = e.GetPosition((IInputElement)VisualParent);
                //Debug.WriteLine("MouseMove: p = " + p.ToString() + ", StartPoint = " + StartPoint.ToString());

                double x = StartRect.X, y = StartRect.Y;
                if (Mode == TrackMode.Move) {
                    // 全体の移動
                    double sx = p.X - StartPoint.X;
                    double sy = p.Y - StartPoint.Y;
                    x = StartRect.X + sx;
                    y = StartRect.Y + sy;
                    // MaxSizeに入るように補正
                    if (x < MaxSize.Left) x = MaxSize.Left;
                    else if (x + Width > MaxSize.Right) x = MaxSize.Right - Width;
                    if (y < MaxSize.Top) y = MaxSize.Top;
                    else if (y + Height > MaxSize.Bottom) y = MaxSize.Bottom - Height;
                    Canvas.SetLeft(this, x);
                    Canvas.SetTop(this, y);

                    //System.Diagnostics.Debug.WriteLine("x = " + x + ", y = " + y + ", Height = " + Height + ", Width = " + Width);
                } else {
                    // heightの計算
                    double height;
                    if (Mode == TrackMode.ResizeTop || Mode == TrackMode.ResizeTopLeft || Mode == TrackMode.ResizeTopRight) {
                        height = StartRect.Height + (StartPoint.Y - p.Y);
                    } else height = StartRect.Height + (p.Y - StartPoint.Y);
                    if (height < MinHeight) height = MinHeight;
                    else if (height > MaxHeight) height = MaxHeight;

                    // widthの計算
                    double width;
                    if (Mode == TrackMode.ResizeLeft || Mode == TrackMode.ResizeTopLeft || Mode == TrackMode.ResizeBottomLeft) {
                        width = StartRect.Width + (StartPoint.X - p.X);
                    } else width = StartRect.Width + (p.X - StartPoint.X);
                    if (width < MinWidth) width = MinWidth;
                    else if (width > MaxWidth) width = MaxWidth;

                    // 隅っこの場合は縦横の比率を一定にするようにする．
                    // 一定と仮定して短い方を補正
                    if (Mode == TrackMode.ResizeTopRight || Mode == TrackMode.ResizeBottmRight ||
                    Mode == TrackMode.ResizeTopLeft || Mode == TrackMode.ResizeBottomLeft) {
                        if (height < width * HeightwaruWidth) {
                            height = width * HeightwaruWidth;
                        } else {
                            width = height / HeightwaruWidth;
                        }
                        // そうでない場合は幅または高さは一定となる
                    } else if (Mode == TrackMode.ResizeTop || Mode == TrackMode.ResizeBottom) {
                        width = StartRect.Width;
                    } else if (Mode == TrackMode.ResizeRight || Mode == TrackMode.ResizeLeft) {
                        height = StartRect.Height;
                    }

                    if (Mode == TrackMode.ResizeTopRight) {
                        y = StartRect.Y + StartRect.Height - height;
                    } else if (Mode == TrackMode.ResizeBottomLeft) {
                        x = StartRect.X + StartRect.Width - width;
                    } else if (Mode == TrackMode.ResizeTopLeft) {
                        y = StartRect.Y + StartRect.Height - height;
                        x = StartRect.X + StartRect.Width - width;
                    } else if (Mode == TrackMode.ResizeTop) {
                        y = StartRect.Y + StartRect.Height - height;
                    } else if (Mode == TrackMode.ResizeLeft) {
                        x = StartRect.X + StartRect.Width - width;
                    }
                    // MaxSizeに入るように補正
                    if (x < MaxSize.Left) {
                        width += x - MaxSize.Left;
                        x = MaxSize.Left;
                    } else if (x + width > MaxSize.Right) width = MaxSize.Right - x;
                    if (y < MaxSize.Top) {
                        height += y - MaxSize.Top;
                        y = MaxSize.Top;
                    } else if (y + height > MaxSize.Bottom) height = MaxSize.Bottom - y;

                    // 更に隅から始まった場合は縦横の比率を調整する．（今度は短い方に合わせる．）
                    if (Mode == TrackMode.ResizeBottmRight) {
                        if (height > width * HeightwaruWidth) {
                            height = width * HeightwaruWidth;
                        } else {
                            width = height / HeightwaruWidth;
                        }
                    } else if (Mode == TrackMode.ResizeTopRight) {
                        if (height > width * HeightwaruWidth) {
                            double d = width * HeightwaruWidth;
                            y += -d + height;
                            height = d;
                        } else {
                            width = height / HeightwaruWidth;
                        }
                    } else if (Mode == TrackMode.ResizeTopLeft) {
                        if (height > width * HeightwaruWidth) {
                            double d = width * HeightwaruWidth;
                            y += -d + height;
                            height = d;
                        } else {
                            double d = height / HeightwaruWidth;
                            x += -d + width;
                            width = d;
                        }
                    } else if (Mode == TrackMode.ResizeBottomLeft) {
                        if (height > width * HeightwaruWidth) {
                            height = width * HeightwaruWidth;
                        } else {
                            double d = height / HeightwaruWidth;
                            x += -d + width;
                            width = d;
                        }
                    }

                    // StartPointは相対位置なので，Controlが動くたびに計算し直す．
                    // PointToScreenで絶対位置にすれば良い気もするけど，何故かうまくいかない．
                    // scalingの問題？
                    // e.GetPositionを親中心にしたので，不要になった．
                    /*
                    if(Mode == TrackMode.ResizeTopRight) {
                        StartPoint.Y += -Height + height;
                    } else if(Mode == TrackMode.ResizeBottomLeft) {
                        StartPoint.X += -Width + width;
                    } else if(Mode == TrackMode.ResizeTopLeft) {
                        StartPoint.Y += -Height + height;
                        StartPoint.X += -Width + width;
                    } else if(Mode == TrackMode.ResizeTop) {
                        StartPoint.Y += -Height + height;
                    } else if(Mode == TrackMode.ResizeLeft) {
                        StartPoint.X += -Width + width;
                    }*/

                    if (x != StartRect.X) Canvas.SetLeft(this, x);
                    if (y != StartRect.Y) Canvas.SetTop(this, y);

                    if (Height != height) Height = height;
                    if (Width != width) Width = width;
                }
                PrevRect = new Rect(x, y, Width, Height);
                OnTrackerSizeChanged(new TrackerEventArgs(PrevRect));
            }
            MouseMove(sender, e);
        }

        protected override void OnMouseDown(MouseButtonEventArgs e) {
            MouseDown(this, e);
            base.OnMouseDown(e);
        }
        protected override void OnMouseMove(MouseEventArgs e) {
            MouseMove(this, e);
            base.OnMouseMove(e);
        }
        protected override void OnMouseUp(MouseButtonEventArgs e) {
            MouseUp(this, e);
            base.OnMouseUp(e);
        }

        void SetSmallRectDefaultSize() {
            Canvas.SetLeft(MainRectangle, -MainRectangle.StrokeThickness / 2);
            Canvas.SetTop(MainRectangle, -MainRectangle.StrokeThickness / 2);
            RectRU.Height = RectRU.Width = SmallRectSize;
            Canvas.SetTop(RectRU, -SmallRectSize / 2);
            RectU.Height = RectU.Width = SmallRectSize;
            Canvas.SetTop(RectU, -SmallRectSize / 2);
            RectLU.Height = RectLU.Width = SmallRectSize;
            Canvas.SetLeft(RectLU, -SmallRectSize / 2); Canvas.SetTop(RectLU, -SmallRectSize / 2);
            RectL.Height = RectL.Width = SmallRectSize;
            Canvas.SetLeft(RectL, -SmallRectSize / 2);
            RectLD.Height = RectLD.Width = SmallRectSize;
            Canvas.SetLeft(RectLD, -SmallRectSize / 2);
            RectD.Height = RectD.Width = SmallRectSize;
            RectRD.Height = RectRD.Width = SmallRectSize;
            RectR.Height = RectR.Width = SmallRectSize;
        }

        void SetSmallRectSizeFromHeight() {
            MainRectangle.Height = base.Height + MainRectangle.StrokeThickness;
            Canvas.SetTop(RectL, base.Height / 2 - SmallRectSize / 2);
            Canvas.SetTop(RectLD, base.Height - SmallRectSize / 2);
            Canvas.SetTop(RectD, base.Height - SmallRectSize / 2);
            Canvas.SetTop(RectRD, base.Height - SmallRectSize / 2);
            Canvas.SetTop(RectR, base.Height / 2 - SmallRectSize / 2);
        }

        void SetSmallRectSizeFromWidth() {
            MainRectangle.Width = base.Width + MainRectangle.StrokeThickness;
            Canvas.SetLeft(RectD, (base.Width / 2) - SmallRectSize / 2);
            Canvas.SetLeft(RectRD, base.Width - SmallRectSize / 2);
            Canvas.SetLeft(RectR, base.Width - SmallRectSize / 2);
            Canvas.SetLeft(RectRU, base.Width - SmallRectSize / 2);
            Canvas.SetLeft(RectU, (base.Width / 2) - SmallRectSize / 2);
        }

        public new double Height {
            get { return base.Height; }

            set {
                base.Height = Math.Max(value, SmallRectSize * 2 + 4);
                SetSmallRectSizeFromHeight();
            }
        }
        public new double Width {
            get { return base.Width; }
            set {
                base.Width = Math.Max(value, SmallRectSize * 2 + 4);
                SetSmallRectSizeFromWidth();
            }
        }

        public bool IsInTracker(Point point) {
            double x = Canvas.GetLeft(this) - 3 * SmallRectSize;
            double y = Canvas.GetTop(this) - 3 * SmallRectSize;
            return (
                point.X > x &&
                point.X < x + Width + 6 * SmallRectSize &&
                point.Y > y &&
                point.Y < y + Height + 6 * SmallRectSize
            );
        }

        public class TrackerEventArgs : EventArgs {
            public Rect Rect { get; set; }
            public TrackerEventArgs(Rect r) { Rect = r; }
        }
        protected virtual void OnTrackerStart(TrackerEventArgs e) { TrackerStart(this, e); }
        protected virtual void OnTrackerSizeChanged(TrackerEventArgs e) { TrackerSizeChanged(this, e); }
        protected virtual void OnTrackerEnd(TrackerEventArgs e) { TrackerEnd(this, e); }

        public delegate void TrackerEventHandler(object sender, TrackerEventArgs rect);
        public event TrackerEventHandler TrackerStart = ((sender, r) => { });
        public event TrackerEventHandler TrackerEnd = ((sender, r) => { });
        public event TrackerEventHandler TrackerSizeChanged = ((sender, r) => { });

        public new event MouseButtonEventHandler MouseUp = ((s, e) => { });
        public new event MouseButtonEventHandler MouseDown = ((s, e) => { });
        public new event MouseEventHandler MouseMove = ((s, e) => { });
    }
}
