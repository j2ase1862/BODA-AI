using BODA_VISION_AI.Models;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BODA_VISION_AI.VisionTools.Measurement
{
    /// <summary>
    /// Caliper 도구 (Cognex VisionPro CogCaliperTool 대체)
    /// 에지 검출을 통한 거리 측정
    /// </summary>
    public class CaliperTool : VisionToolBase
    {
        // 검색 영역 설정
        private Point2d _startPoint = new Point2d(0, 0);
        public Point2d StartPoint
        {
            get => _startPoint;
            set => SetProperty(ref _startPoint, value);
        }

        private Point2d _endPoint = new Point2d(100, 0);
        public Point2d EndPoint
        {
            get => _endPoint;
            set => SetProperty(ref _endPoint, value);
        }

        private double _searchWidth = 20;
        public double SearchWidth
        {
            get => _searchWidth;
            set => SetProperty(ref _searchWidth, Math.Max(1, value));
        }

        // Edge 검출 설정
        private EdgePolarity _polarity = EdgePolarity.DarkToLight;
        public EdgePolarity Polarity
        {
            get => _polarity;
            set => SetProperty(ref _polarity, value);
        }

        private double _edgeThreshold = 30;
        public double EdgeThreshold
        {
            get => _edgeThreshold;
            set => SetProperty(ref _edgeThreshold, Math.Max(1, value));
        }

        private int _filterHalfWidth = 2;
        public int FilterHalfWidth
        {
            get => _filterHalfWidth;
            set => SetProperty(ref _filterHalfWidth, Math.Max(1, value));
        }

        // 측정 모드
        private CaliperMode _mode = CaliperMode.SingleEdge;
        public CaliperMode Mode
        {
            get => _mode;
            set => SetProperty(ref _mode, value);
        }

        // Edge Pair 설정 (Width 측정용)
        private double _expectedWidth = 50;
        public double ExpectedWidth
        {
            get => _expectedWidth;
            set => SetProperty(ref _expectedWidth, Math.Max(1, value));
        }

        private double _widthTolerance = 20;
        public double WidthTolerance
        {
            get => _widthTolerance;
            set => SetProperty(ref _widthTolerance, Math.Max(0, value));
        }

        private int _maxEdges = 10;
        public int MaxEdges
        {
            get => _maxEdges;
            set => SetProperty(ref _maxEdges, Math.Max(1, value));
        }

        public CaliperTool()
        {
            Name = "Caliper";
            ToolType = "CaliperTool";
        }

        public override VisionResult Execute(Mat inputImage)
        {
            var result = new VisionResult();
            var sw = Stopwatch.StartNew();

            try
            {
                Mat workImage = GetROIImage(inputImage);
                Mat grayImage = new Mat();

                if (workImage.Channels() > 1)
                    Cv2.CvtColor(workImage, grayImage, ColorConversionCodes.BGR2GRAY);
                else
                    grayImage = workImage.Clone();

                // 검색 라인의 방향 계산
                double dx = EndPoint.X - StartPoint.X;
                double dy = EndPoint.Y - StartPoint.Y;
                double length = Math.Sqrt(dx * dx + dy * dy);

                if (length < 1)
                {
                    result.Success = false;
                    result.Message = "검색 라인이 너무 짧습니다.";
                    return result;
                }

                // 단위 벡터
                double ux = dx / length;
                double uy = dy / length;

                // 수직 벡터
                double vx = -uy;
                double vy = ux;

                // 검색 라인을 따라 프로파일 추출
                var profile = ExtractProfile(grayImage, StartPoint, ux, uy, vx, vy, length);

                // Edge 검출
                var edges = DetectEdges(profile, length);

                // 결과 이미지 생성
                Mat overlayImage = inputImage.Clone();
                if (overlayImage.Channels() == 1)
                    Cv2.CvtColor(overlayImage, overlayImage, ColorConversionCodes.GRAY2BGR);

                // 검색 영역 표시
                DrawSearchRegion(overlayImage, StartPoint, EndPoint, SearchWidth, vx, vy);

                if (Mode == CaliperMode.SingleEdge)
                {
                    // 단일 Edge 결과
                    if (edges.Count > 0)
                    {
                        var edge = edges[0];
                        var edgePoint = new Point2d(
                            StartPoint.X + ux * edge.Position,
                            StartPoint.Y + uy * edge.Position);

                        DrawEdge(overlayImage, edgePoint, vx, vy, SearchWidth);

                        result.Data["EdgeX"] = edgePoint.X;
                        result.Data["EdgeY"] = edgePoint.Y;
                        result.Data["EdgeScore"] = edge.Score;
                        result.Data["EdgePolarity"] = edge.Polarity.ToString();

                        result.Graphics.Add(new GraphicOverlay
                        {
                            Type = GraphicType.Line,
                            Position = new Point2d(edgePoint.X + vx * SearchWidth / 2, edgePoint.Y + vy * SearchWidth / 2),
                            EndPosition = new Point2d(edgePoint.X - vx * SearchWidth / 2, edgePoint.Y - vy * SearchWidth / 2),
                            Color = new Scalar(0, 255, 0)
                        });

                        result.Success = true;
                        result.Message = $"Edge 검출 완료: ({edgePoint.X:F1}, {edgePoint.Y:F1})";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "Edge를 찾을 수 없습니다.";
                    }
                }
                else if (Mode == CaliperMode.EdgePair)
                {
                    // Edge Pair (Width) 측정
                    var pairs = FindEdgePairs(edges, ux, uy);

                    if (pairs.Count > 0)
                    {
                        var pair = pairs[0];

                        DrawEdge(overlayImage, pair.Edge1Point, vx, vy, SearchWidth, new Scalar(0, 255, 0));
                        DrawEdge(overlayImage, pair.Edge2Point, vx, vy, SearchWidth, new Scalar(0, 255, 255));

                        // Width 라인
                        Cv2.Line(overlayImage,
                            new Point((int)pair.Edge1Point.X, (int)pair.Edge1Point.Y),
                            new Point((int)pair.Edge2Point.X, (int)pair.Edge2Point.Y),
                            new Scalar(255, 0, 255), 2);

                        // Width 값 표시
                        var midPoint = new Point(
                            (int)((pair.Edge1Point.X + pair.Edge2Point.X) / 2),
                            (int)((pair.Edge1Point.Y + pair.Edge2Point.Y) / 2));
                        Cv2.PutText(overlayImage, $"{pair.Width:F2}px",
                            new Point(midPoint.X + 5, midPoint.Y - 5),
                            HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 255), 1);

                        result.Data["Width"] = pair.Width;
                        result.Data["Edge1X"] = pair.Edge1Point.X;
                        result.Data["Edge1Y"] = pair.Edge1Point.Y;
                        result.Data["Edge2X"] = pair.Edge2Point.X;
                        result.Data["Edge2Y"] = pair.Edge2Point.Y;
                        result.Data["CenterX"] = (pair.Edge1Point.X + pair.Edge2Point.X) / 2;
                        result.Data["CenterY"] = (pair.Edge1Point.Y + pair.Edge2Point.Y) / 2;

                        result.Success = true;
                        result.Message = $"Width 측정 완료: {pair.Width:F2}px";
                    }
                    else
                    {
                        result.Success = false;
                        result.Message = "Edge Pair를 찾을 수 없습니다.";
                    }
                }

                result.Data["EdgeCount"] = edges.Count;
                result.Data["Edges"] = edges;
                result.OutputImage = grayImage;
                result.OverlayImage = overlayImage;

                if (workImage != inputImage)
                    workImage.Dispose();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Caliper 실행 실패: {ex.Message}";
            }

            sw.Stop();
            ExecutionTime = sw.Elapsed.TotalMilliseconds;
            LastResult = result;
            return result;
        }

        private double[] ExtractProfile(Mat image, Point2d start, double ux, double uy, double vx, double vy, double length)
        {
            int profileLength = (int)length;
            var profile = new double[profileLength];

            for (int i = 0; i < profileLength; i++)
            {
                double sum = 0;
                int count = 0;

                // SearchWidth를 따라 평균값 계산
                for (int j = -(int)(SearchWidth / 2); j <= (int)(SearchWidth / 2); j++)
                {
                    double px = start.X + ux * i + vx * j;
                    double py = start.Y + uy * i + vy * j;

                    int ix = (int)px;
                    int iy = (int)py;

                    if (ix >= 0 && ix < image.Width && iy >= 0 && iy < image.Height)
                    {
                        sum += image.At<byte>(iy, ix);
                        count++;
                    }
                }

                profile[i] = count > 0 ? sum / count : 0;
            }

            return profile;
        }

        private List<EdgeResult> DetectEdges(double[] profile, double length)
        {
            var edges = new List<EdgeResult>();

            // Sobel 필터로 그래디언트 계산
            var gradient = new double[profile.Length];
            for (int i = FilterHalfWidth; i < profile.Length - FilterHalfWidth; i++)
            {
                double sum = 0;
                for (int j = -FilterHalfWidth; j <= FilterHalfWidth; j++)
                {
                    sum += profile[i + j] * j;
                }
                gradient[i] = sum / (FilterHalfWidth * 2 + 1);
            }

            // Edge 검출 (Local Maxima/Minima)
            for (int i = FilterHalfWidth + 1; i < gradient.Length - FilterHalfWidth - 1; i++)
            {
                double g = gradient[i];
                bool isLocalExtreme = false;
                EdgePolarity polarity = EdgePolarity.DarkToLight;

                // Dark to Light (positive gradient peak)
                if (g > 0 && Math.Abs(g) > EdgeThreshold)
                {
                    if (g > gradient[i - 1] && g > gradient[i + 1])
                    {
                        isLocalExtreme = true;
                        polarity = EdgePolarity.DarkToLight;
                    }
                }
                // Light to Dark (negative gradient peak)
                else if (g < 0 && Math.Abs(g) > EdgeThreshold)
                {
                    if (g < gradient[i - 1] && g < gradient[i + 1])
                    {
                        isLocalExtreme = true;
                        polarity = EdgePolarity.LightToDark;
                    }
                }

                if (isLocalExtreme)
                {
                    // Polarity 필터
                    if (Polarity == EdgePolarity.Any ||
                        (Polarity == EdgePolarity.DarkToLight && polarity == EdgePolarity.DarkToLight) ||
                        (Polarity == EdgePolarity.LightToDark && polarity == EdgePolarity.LightToDark))
                    {
                        edges.Add(new EdgeResult
                        {
                            Position = i,
                            Score = Math.Abs(g),
                            Polarity = polarity
                        });
                    }
                }
            }

            // 점수순 정렬
            edges = edges.OrderByDescending(e => e.Score).Take(MaxEdges).ToList();

            return edges;
        }

        private List<EdgePairResult> FindEdgePairs(List<EdgeResult> edges, double ux, double uy)
        {
            var pairs = new List<EdgePairResult>();

            for (int i = 0; i < edges.Count; i++)
            {
                for (int j = i + 1; j < edges.Count; j++)
                {
                    var e1 = edges[i];
                    var e2 = edges[j];

                    // Polarity가 반대인지 확인
                    if ((e1.Polarity == EdgePolarity.DarkToLight && e2.Polarity == EdgePolarity.LightToDark) ||
                        (e1.Polarity == EdgePolarity.LightToDark && e2.Polarity == EdgePolarity.DarkToLight))
                    {
                        double width = Math.Abs(e2.Position - e1.Position);

                        // Width 허용 범위 확인
                        if (Math.Abs(width - ExpectedWidth) <= WidthTolerance)
                        {
                            var first = e1.Position < e2.Position ? e1 : e2;
                            var second = e1.Position < e2.Position ? e2 : e1;

                            pairs.Add(new EdgePairResult
                            {
                                Edge1Point = new Point2d(
                                    StartPoint.X + ux * first.Position,
                                    StartPoint.Y + uy * first.Position),
                                Edge2Point = new Point2d(
                                    StartPoint.X + ux * second.Position,
                                    StartPoint.Y + uy * second.Position),
                                Width = width,
                                Score = (first.Score + second.Score) / 2
                            });
                        }
                    }
                }
            }

            // Width가 ExpectedWidth에 가까운 순으로 정렬
            return pairs.OrderBy(p => Math.Abs(p.Width - ExpectedWidth)).ToList();
        }

        private void DrawSearchRegion(Mat image, Point2d start, Point2d end, double width, double vx, double vy)
        {
            var corners = new Point[]
            {
                new Point((int)(start.X + vx * width / 2), (int)(start.Y + vy * width / 2)),
                new Point((int)(start.X - vx * width / 2), (int)(start.Y - vy * width / 2)),
                new Point((int)(end.X - vx * width / 2), (int)(end.Y - vy * width / 2)),
                new Point((int)(end.X + vx * width / 2), (int)(end.Y + vy * width / 2))
            };

            Cv2.Polylines(image, new[] { corners }, true, new Scalar(128, 128, 128), 1);

            // 중심선
            Cv2.Line(image,
                new Point((int)start.X, (int)start.Y),
                new Point((int)end.X, (int)end.Y),
                new Scalar(0, 128, 255), 1);
        }

        private void DrawEdge(Mat image, Point2d point, double vx, double vy, double width, Scalar? color = null)
        {
            var c = color ?? new Scalar(0, 255, 0);
            Cv2.Line(image,
                new Point((int)(point.X + vx * width / 2), (int)(point.Y + vy * width / 2)),
                new Point((int)(point.X - vx * width / 2), (int)(point.Y - vy * width / 2)),
                c, 2);
        }

        public override VisionToolBase Clone()
        {
            return new CaliperTool
            {
                Name = this.Name,
                ToolType = this.ToolType,
                IsEnabled = this.IsEnabled,
                ROI = this.ROI,
                UseROI = this.UseROI,
                StartPoint = this.StartPoint,
                EndPoint = this.EndPoint,
                SearchWidth = this.SearchWidth,
                Polarity = this.Polarity,
                EdgeThreshold = this.EdgeThreshold,
                FilterHalfWidth = this.FilterHalfWidth,
                Mode = this.Mode,
                ExpectedWidth = this.ExpectedWidth,
                WidthTolerance = this.WidthTolerance,
                MaxEdges = this.MaxEdges
            };
        }
    }

    public class EdgeResult
    {
        public double Position { get; set; }
        public double Score { get; set; }
        public EdgePolarity Polarity { get; set; }
    }

    public class EdgePairResult
    {
        public Point2d Edge1Point { get; set; }
        public Point2d Edge2Point { get; set; }
        public double Width { get; set; }
        public double Score { get; set; }
    }

    public enum EdgePolarity
    {
        DarkToLight,
        LightToDark,
        Any
    }

    public enum CaliperMode
    {
        SingleEdge,
        EdgePair
    }
}
