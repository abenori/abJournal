using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;

namespace abJournal {
    class DrawingStrokes {
        static List<Double> Weight;
        static DrawingStrokes() {
            // 手ぶれ？補正のWeightの初期化
            double s = 1;
            Weight = new List<double>();
            for (int i = 0; i < 20; ++i) {
                Weight.Add(Math.Exp(-i * i / (2 * s * s)));
            }
        }

        public static StylusPointCollection GetHoseiPoints(StylusPointCollection Points) {
            //HoseiPts.Clear();
            StylusPointCollection rv = new StylusPointCollection(Points.Count);
            // 手ぶれ？補正
            // 参考：http://www24.atwiki.jp/sigetch_2007/pages/18.html
            // 単に周辺の点（最大2N+1個）を重み付きで足しているだけです．
            // 重みはe^{-x^2}がよさげ（上のURLから）なのでそうしている．
            const int N = 3;
            for (int i = 0; i < Points.Count; ++i) {
                var pt = new StylusPoint();
                double wsum = 0;
                int first = Math.Max(0, i - N), last = Math.Min(Points.Count - 1, i + N);
                for (int j = first; j <= last; ++j) {
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

        public static void DrawOriginalType1(DrawingContext dc, StylusPointCollection Points, DrawingAttributes dattr, DrawingAttributesPlus dattrplus, Pen pen) {
            if (Points.Count <= 1) {
                return;
            }
            if (dattr.IgnorePressure) {
                StreamGeometry geom = new StreamGeometry();
                using (var ctx = geom.Open()) {
                    ctx.BeginFigure(Points[0].ToPoint(), false, false);
                    PointCollection ctrl1 = new PointCollection(), ctrl2 = new PointCollection();
                    GenerateBezierControlPointsType1(Points, ref ctrl1, ref ctrl2);
                    for (int i = 1; i < Points.Count; ++i) {
                        ctx.BezierTo(ctrl1[i - 1], ctrl2[i - 1], Points[i].ToPoint(), true, false);
                    }
                }
                geom.Freeze();
                dc.DrawGeometry(null, pen, geom);
            } else {
                PointCollection ctrl1 = new PointCollection(), ctrl2 = new PointCollection();
                GenerateBezierControlPointsType1(Points, ref ctrl1, ref ctrl2);
                var group = new DrawingGroup();
                double dashOffset = 0;
                for (int i = 1; i < Points.Count; ++i) {
                    StreamGeometry geom = new StreamGeometry();
                    using (var ctx = geom.Open()) {
                        ctx.BeginFigure(Points[i - 1].ToPoint(), false, false);
                        ctx.BezierTo(ctrl1[i - 1], ctrl2[i - 1], Points[i].ToPoint(), true, false);
                    }
                    geom.Freeze();
                    var p = pen.Clone();
                    p.Thickness *= Points[i - 1].PressureFactor * 2;
                    if (p.DashStyle.Dashes.Count > 0) {
                        p.DashStyle.Offset = dashOffset;
                        double dx = Points[i].X - Points[i - 1].X, dy = Points[i - 1].Y - Points[i].Y;
                        dashOffset += Math.Sqrt(dx * dx + dy * dy) / pen.Thickness;
                    }
                    p.Freeze();
                    group.Children.Add(new GeometryDrawing(null, p, geom));
                }
                group.Freeze();
                dc.DrawDrawing(group);
            }
        }

        public static void DrawOriginalLine(DrawingContext dc, StylusPointCollection Points, DrawingAttributes dattr, DrawingAttributesPlus dattrplus, Pen pen) {
            if (Points.Count <= 1) {
                return;
            }
            if (dattr.IgnorePressure) {
                for (int i = 1; i < Points.Count; ++i) {
                    dc.DrawLine(pen, Points[i - 1].ToPoint(), Points[i].ToPoint());
                }
            } else {
                double dashOffset = 0;
                for (int i = 1; i < Points.Count; ++i) {
                    var p = pen.Clone();
                    p.Thickness *= Points[i - 1].PressureFactor * 2;
                    if (p.DashStyle.Dashes.Count > 0) {
                        p.DashStyle.Offset = dashOffset;
                        double dx = Points[i].X - Points[i - 1].X, dy = Points[i].Y - Points[i - 1].Y;
                        dashOffset += Math.Sqrt(dx * dx + dy * dy) / pen.Thickness;
                    }
                    p.Freeze();
                    dc.DrawLine(p, Points[i - 1].ToPoint(), Points[i].ToPoint());
                }
            }
        }


        // 「不要そう」な点を消す．
        // 点から点に線をひいて，どのくらいずれているか計測する．
        // ずれが大きくないならば，その間を間引く
        // i : 手元の点，j：先の点
        public static StylusPointCollection MabikiPointsType1(StylusPointCollection Points, DrawingAttributes dattr) {
            var nokoriPoints = new StylusPointCollection(Points.Description, Points.Count / 2);
            nokoriPoints.Add(Points[0]);
            for (int i = 0; i < Points.Count - 1; ++i) {
                double pressuresum = 0;
                for (int j = i + 2; j < Points.Count; ++j) {
                    pressuresum += Points[j].PressureFactor;
                    double pressuremean = pressuresum / (j - i - 1);
                    bool mabiku = true;
                    if ((new Vector(Points[j].X - Points[i].X, Points[j].Y - Points[i].Y)).Length > 10) {
                        mabiku = false;
                    } else {
                        for (int k = i + 1; k < j; ++k) {
                            Vector vec1 = new Vector(Points[k].X - Points[i].X, Points[k].Y - Points[i].Y);
                            Vector vec2 = new Vector(Points[j].X - Points[k].X, Points[j].Y - Points[k].Y);
                            if (Math.Abs(Vector.AngleBetween(vec1, vec2)) > 20) {
                                mabiku = false;
                                break;
                            } else if (!dattr.IgnorePressure && Math.Abs(Points[k].PressureFactor - pressuremean) > 0.1) {
                                mabiku = false;
                                break;
                            }
                        }
                    }
                    if (!mabiku) {
                        //System.Diagnostics.Debug.WriteLine("added points at : " + (j - 1).ToString());
                        nokoriPoints.Add(Points[j - 1]);
                        i = j - 2;
                        break;
                    }
                }
            }
            nokoriPoints.Add(Points.Last());
            //System.Diagnostics.Debug.WriteLine("nokori / zentai = " + nokoriPoints.Count.ToString() + " / " + Points.Count.ToString() + " = "  + ((double)nokoriPoints.Count/(double)Points.Count).ToString());
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
        public static void GenerateBezierControlPointsType1(StylusPointCollection Points, ref PointCollection ctrlpt1, ref PointCollection ctrlpt2) {
            System.Diagnostics.Debug.Assert(Points.Count >= 2);
            ctrlpt1.Clear(); ctrlpt2.Clear();
            //Point firstCtrlPoint = new Point(Points[0].ToPoint().X * 0.67 + Points[1].ToPoint().X * 0.33,Points[0].ToPoint().Y * 0.67 + Points[1].ToPoint().Y * 0.33);
            //Point firstCtrlPoint = new Point(Points[0].ToPoint().X * 0.99 + Points[1].ToPoint().X * 0.01, Points[0].ToPoint().Y * 0.99 + Points[1].ToPoint().Y * 0.01);
            Point firstCtrlPoint = Points[0].ToPoint();
            double prevLength = (Points[1].ToPoint() - Points[0].ToPoint()).Length;
            for (int i = 1; i < Points.Count - 1; ++i) {
                double length = (Points[i + 1].ToPoint() - Points[i].ToPoint()).Length;
                if (prevLength + length == 0) {
                    ctrlpt1.Add(Points[i].ToPoint());
                    ctrlpt2.Add(Points[i].ToPoint());
                    firstCtrlPoint = Points[i].ToPoint();
                } else {
                    Vector vec = (Points[i + 1].ToPoint() - Points[i - 1].ToPoint()) / 2;
                    ctrlpt1.Add(firstCtrlPoint);
                    ctrlpt2.Add(Points[i].ToPoint() - (vec * prevLength / (length + prevLength)));
                    firstCtrlPoint = Points[i].ToPoint() + (vec * length) / (length + prevLength);
                    prevLength = length;
                }
            }
            ctrlpt1.Add(firstCtrlPoint);
            ctrlpt2.Add(Points.Last().ToPoint());
        }

        public static void DrawPath(iTextSharp.text.pdf.PdfWriter writer, double scale, List<StrokeDataStruct> strokes) {
            foreach (var s in strokes) {
                writer.DirectContent.SetColorStroke(new iTextSharp.text.BaseColor(
                        s.DrawingAttributes.Color.R,
                        s.DrawingAttributes.Color.G,
                        s.DrawingAttributes.Color.B,
                        s.DrawingAttributes.Color.A
                    ));
                if (s.DrawingAttributesPlus.IsNormalDashArray) {
                    writer.DirectContent.SetLineDash(s.DrawingAttributesPlus.DashArray.Select(d => d * scale).ToArray(), 0);
                }
                var state = new iTextSharp.text.pdf.PdfGState();
                state.StrokeOpacity = s.DrawingAttributes.IsHighlighter ? 0.5f : 1.0f;
                writer.DirectContent.SetGState(state);
                writer.DirectContent.SetLineWidth(s.DrawingAttributes.Width * scale);
                StylusPointCollection pts = MabikiPointsType1(s.StylusPoints, s.DrawingAttributes);
                PointCollection cpt1 = new PointCollection(), cpt2 = new PointCollection();
                GenerateBezierControlPointsType1(pts, ref cpt1, ref cpt2);
                writer.DirectContent.MoveTo(scale * pts[0].X, writer.PageSize.Height - scale * pts[0].Y);
                for (int i = 0; i < pts.Count - 1; ++i) {
                    if (!s.DrawingAttributes.IgnorePressure) {
                        writer.DirectContent.SetLineWidth(s.DrawingAttributes.Width * pts[i].PressureFactor * scale * 2);
                    }
                    writer.DirectContent.CurveTo(scale * cpt1[i].X, writer.PageSize.Height - scale * cpt1[i].Y,
                        scale * cpt2[i].X, writer.PageSize.Height - scale * cpt2[i].Y,
                        scale * pts[i + 1].X, writer.PageSize.Height - scale * pts[i + 1].Y);
                }
                writer.DirectContent.Stroke();
            }
        }
    }
}
