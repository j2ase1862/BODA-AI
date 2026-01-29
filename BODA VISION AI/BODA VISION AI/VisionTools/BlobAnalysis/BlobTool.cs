using BODA_VISION_AI.Models;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace BODA_VISION_AI.VisionTools.BlobAnalysis
{
    /// <summary>
    /// Blob 분석 도구 (Cognex VisionPro CogBlobTool 대체)
    /// 이진화된 이미지에서 객체(Blob)를 검출하고 분석
    /// </summary>
    public class BlobTool : VisionToolBase
    {
        // Threshold 설정 (내부 이진화용)
        private bool _useInternalThreshold = true;
        public bool UseInternalThreshold
        {
            get => _useInternalThreshold;
            set => SetProperty(ref _useInternalThreshold, value);
        }

        private double _thresholdValue = 128;
        public double ThresholdValue
        {
            get => _thresholdValue;
            set => SetProperty(ref _thresholdValue, Math.Clamp(value, 0, 255));
        }

        private bool _invertPolarity = false;
        public bool InvertPolarity
        {
            get => _invertPolarity;
            set => SetProperty(ref _invertPolarity, value);
        }

        // 면적 필터
        private double _minArea = 100;
        public double MinArea
        {
            get => _minArea;
            set => SetProperty(ref _minArea, Math.Max(0, value));
        }

        private double _maxArea = double.MaxValue;
        public double MaxArea
        {
            get => _maxArea;
            set => SetProperty(ref _maxArea, Math.Max(MinArea, value));
        }

        // 둘레 필터
        private double _minPerimeter = 0;
        public double MinPerimeter
        {
            get => _minPerimeter;
            set => SetProperty(ref _minPerimeter, Math.Max(0, value));
        }

        private double _maxPerimeter = double.MaxValue;
        public double MaxPerimeter
        {
            get => _maxPerimeter;
            set => SetProperty(ref _maxPerimeter, Math.Max(MinPerimeter, value));
        }

        // 형상 필터
        private double _minCircularity = 0;
        public double MinCircularity
        {
            get => _minCircularity;
            set => SetProperty(ref _minCircularity, Math.Clamp(value, 0, 1));
        }

        private double _maxCircularity = 1;
        public double MaxCircularity
        {
            get => _maxCircularity;
            set => SetProperty(ref _maxCircularity, Math.Clamp(value, MinCircularity, 1));
        }

        private double _minAspectRatio = 0;
        public double MinAspectRatio
        {
            get => _minAspectRatio;
            set => SetProperty(ref _minAspectRatio, Math.Max(0, value));
        }

        private double _maxAspectRatio = double.MaxValue;
        public double MaxAspectRatio
        {
            get => _maxAspectRatio;
            set => SetProperty(ref _maxAspectRatio, Math.Max(MinAspectRatio, value));
        }

        // Convexity 필터
        private double _minConvexity = 0;
        public double MinConvexity
        {
            get => _minConvexity;
            set => SetProperty(ref _minConvexity, Math.Clamp(value, 0, 1));
        }

        // 최대 Blob 수
        private int _maxBlobCount = 100;
        public int MaxBlobCount
        {
            get => _maxBlobCount;
            set => SetProperty(ref _maxBlobCount, Math.Max(1, value));
        }

        // 정렬 기준
        private BlobSortBy _sortBy = BlobSortBy.Area;
        public BlobSortBy SortBy
        {
            get => _sortBy;
            set => SetProperty(ref _sortBy, value);
        }

        private bool _sortDescending = true;
        public bool SortDescending
        {
            get => _sortDescending;
            set => SetProperty(ref _sortDescending, value);
        }

        // Contour 검출 모드
        private RetrievalModes _retrievalMode = RetrievalModes.External;
        public RetrievalModes RetrievalMode
        {
            get => _retrievalMode;
            set => SetProperty(ref _retrievalMode, value);
        }

        private ContourApproximationModes _approximationMode = ContourApproximationModes.ApproxSimple;
        public ContourApproximationModes ApproximationMode
        {
            get => _approximationMode;
            set => SetProperty(ref _approximationMode, value);
        }

        // 표시 옵션
        private bool _drawContours = true;
        public bool DrawContours
        {
            get => _drawContours;
            set => SetProperty(ref _drawContours, value);
        }

        private bool _drawBoundingBox = true;
        public bool DrawBoundingBox
        {
            get => _drawBoundingBox;
            set => SetProperty(ref _drawBoundingBox, value);
        }

        private bool _drawCenterPoint = true;
        public bool DrawCenterPoint
        {
            get => _drawCenterPoint;
            set => SetProperty(ref _drawCenterPoint, value);
        }

        private bool _drawLabels = true;
        public bool DrawLabels
        {
            get => _drawLabels;
            set => SetProperty(ref _drawLabels, value);
        }

        public BlobTool()
        {
            Name = "Blob";
            ToolType = "BlobTool";
        }

        public override VisionResult Execute(Mat inputImage)
        {
            var result = new VisionResult();
            var sw = Stopwatch.StartNew();

            try
            {
                // ROI 오프셋 계산 (ROI 좌표계 → 원본 이미지 좌표계 변환)
                // FindContours는 ROI 잘라낸 이미지 기준 좌표(0,0)를 반환하므로
                // 원본 이미지 위에 그릴 때 ROI 위치만큼 오프셋을 더해야 함
                int offsetX = 0, offsetY = 0;
                bool hasROI = UseROI && ROI.Width > 0 && ROI.Height > 0;
                Rect adjustedROI = default;
                if (hasROI)
                {
                    adjustedROI = GetAdjustedROI(inputImage);
                    offsetX = adjustedROI.X;
                    offsetY = adjustedROI.Y;
                }

                // ROI 영역 추출 (ROI가 설정된 경우 해당 영역만 처리)
                Mat workImage = GetROIImage(inputImage);
                Mat binaryImage = new Mat();

                // Grayscale 변환
                Mat grayImage = new Mat();
                if (workImage.Channels() > 1)
                    Cv2.CvtColor(workImage, grayImage, ColorConversionCodes.BGR2GRAY);
                else
                    grayImage = workImage.Clone();

                // 이진화
                if (UseInternalThreshold)
                {
                    var threshType = InvertPolarity ? ThresholdTypes.BinaryInv : ThresholdTypes.Binary;
                    Cv2.Threshold(grayImage, binaryImage, ThresholdValue, 255, threshType);
                }
                else
                {
                    // 이미 이진화된 이미지로 가정
                    binaryImage = grayImage.Clone();
                    if (InvertPolarity)
                        Cv2.BitwiseNot(binaryImage, binaryImage);
                }

                // Contour 검출 (ROI 기준 좌표계에서 수행 → 좌표는 0,0 기준)
                Cv2.FindContours(binaryImage, out Point[][] contours, out HierarchyIndex[] hierarchy,
                    RetrievalMode, ApproximationMode);

                // Blob 정보 계산 및 필터링 (ROI 기준 좌표 - 면적/형상은 위치 무관)
                var blobs = new List<BlobResult>();
                int blobId = 0;

                foreach (var contour in contours)
                {
                    var blob = CalculateBlobProperties(contour, blobId);

                    if (blob.Area >= MinArea && blob.Area <= MaxArea &&
                        blob.Perimeter >= MinPerimeter && blob.Perimeter <= MaxPerimeter &&
                        blob.Circularity >= MinCircularity && blob.Circularity <= MaxCircularity &&
                        blob.AspectRatio >= MinAspectRatio && blob.AspectRatio <= MaxAspectRatio &&
                        blob.Convexity >= MinConvexity)
                    {
                        blobs.Add(blob);
                        blobId++;
                    }
                }

                // 정렬
                blobs = SortBlobs(blobs);

                // 최대 개수 제한
                if (blobs.Count > MaxBlobCount)
                    blobs = blobs.Take(MaxBlobCount).ToList();

                // 결과 오버레이 이미지 생성 (원본 이미지 전체 크기)
                Mat overlayImage = inputImage.Clone();
                if (overlayImage.Channels() == 1)
                    Cv2.CvtColor(overlayImage, overlayImage, ColorConversionCodes.GRAY2BGR);

                // ROI 영역 경계 표시
                if (hasROI)
                {
                    Cv2.Rectangle(overlayImage, adjustedROI, new Scalar(0, 200, 200), 2);
                }

                // 진단 정보 오버레이 (디버그용 - 원인 파악 후 제거)
                int dbgY = 30;
                var dbgColor = new Scalar(0, 255, 255); // Cyan
                Cv2.PutText(overlayImage, $"InputImage: {inputImage.Width}x{inputImage.Height} Ch={inputImage.Channels()}", new Point(10, dbgY), HersheyFonts.HersheySimplex, 0.7, dbgColor, 2);
                dbgY += 30;
                Cv2.PutText(overlayImage, $"WorkImage: {workImage.Width}x{workImage.Height}", new Point(10, dbgY), HersheyFonts.HersheySimplex, 0.7, dbgColor, 2);
                dbgY += 30;
                Cv2.PutText(overlayImage, $"UseROI={UseROI} ROI=({ROI.X},{ROI.Y},{ROI.Width},{ROI.Height})", new Point(10, dbgY), HersheyFonts.HersheySimplex, 0.7, dbgColor, 2);
                dbgY += 30;
                Cv2.PutText(overlayImage, $"AdjROI=({adjustedROI.X},{adjustedROI.Y},{adjustedROI.Width},{adjustedROI.Height})", new Point(10, dbgY), HersheyFonts.HersheySimplex, 0.7, dbgColor, 2);
                dbgY += 30;
                Cv2.PutText(overlayImage, $"Offset=({offsetX},{offsetY}) hasROI={hasROI}", new Point(10, dbgY), HersheyFonts.HersheySimplex, 0.7, dbgColor, 2);
                dbgY += 30;
                Cv2.PutText(overlayImage, $"Blobs found: {blobs.Count}", new Point(10, dbgY), HersheyFonts.HersheySimplex, 0.7, dbgColor, 2);
                if (blobs.Count > 0)
                {
                    dbgY += 30;
                    var b0 = blobs[0];
                    Cv2.PutText(overlayImage, $"Blob0 ROI-relative: center=({b0.CenterX:F0},{b0.CenterY:F0}) rect=({b0.BoundingRect.X},{b0.BoundingRect.Y},{b0.BoundingRect.Width},{b0.BoundingRect.Height})", new Point(10, dbgY), HersheyFonts.HersheySimplex, 0.5, dbgColor, 1);
                    dbgY += 25;
                    Cv2.PutText(overlayImage, $"Blob0 Absolute: center=({b0.CenterX + offsetX:F0},{b0.CenterY + offsetY:F0})", new Point(10, dbgY), HersheyFonts.HersheySimplex, 0.5, dbgColor, 1);
                }

                // Blob 그래픽 그리기 (ROI 오프셋 적용하여 원본 좌표계로 변환)
                for (int i = 0; i < blobs.Count; i++)
                {
                    var blob = blobs[i];
                    var color = GetBlobColor(i);

                    // ROI 오프셋을 적용한 contour 좌표 (그리기용)
                    Point[] drawContour = OffsetPoints(blob.Contour, offsetX, offsetY);

                    if (DrawContours)
                    {
                        Cv2.DrawContours(overlayImage, new[] { drawContour }, 0, color, 2);
                    }

                    if (DrawBoundingBox)
                    {
                        var drawRect = new Rect(
                            blob.BoundingRect.X + offsetX,
                            blob.BoundingRect.Y + offsetY,
                            blob.BoundingRect.Width,
                            blob.BoundingRect.Height);
                        Cv2.Rectangle(overlayImage, drawRect, new Scalar(255, 255, 0), 1);
                    }

                    if (DrawCenterPoint)
                    {
                        Cv2.DrawMarker(overlayImage,
                            new Point((int)blob.CenterX + offsetX, (int)blob.CenterY + offsetY),
                            new Scalar(0, 0, 255), MarkerTypes.Cross, 10, 2);
                    }

                    if (DrawLabels)
                    {
                        Cv2.PutText(overlayImage, $"#{i}",
                            new Point((int)blob.CenterX + offsetX + 5, (int)blob.CenterY + offsetY - 5),
                            HersheyFonts.HersheySimplex, 0.4, new Scalar(255, 255, 255), 1);
                    }

                    result.Graphics.Add(new GraphicOverlay
                    {
                        Type = GraphicType.Polygon,
                        Points = drawContour.ToList(),
                        Color = color
                    });
                }

                // ROI가 사용된 경우 이진화 출력도 원본 크기로 변환
                Mat finalBinary;
                if (hasROI)
                {
                    finalBinary = ApplyROIResult(inputImage, binaryImage);
                    binaryImage.Dispose();
                }
                else
                {
                    finalBinary = binaryImage;
                }

                result.Success = blobs.Count > 0;
                result.Message = $"Blob 검출 완료: {blobs.Count}개";
                result.OutputImage = finalBinary;
                result.OverlayImage = overlayImage;
                result.Data["BlobCount"] = blobs.Count;
                result.Data["Blobs"] = blobs;

                // 통계 정보
                if (blobs.Count > 0)
                {
                    result.Data["TotalArea"] = blobs.Sum(b => b.Area);
                    result.Data["AverageArea"] = blobs.Average(b => b.Area);
                    result.Data["LargestBlobArea"] = blobs.Max(b => b.Area);
                    result.Data["SmallestBlobArea"] = blobs.Min(b => b.Area);
                }

                grayImage.Dispose();
                if (workImage != inputImage)
                    workImage.Dispose();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Blob 분석 실패: {ex.Message}";
            }

            sw.Stop();
            ExecutionTime = sw.Elapsed.TotalMilliseconds;
            LastResult = result;
            return result;
        }

        private BlobResult CalculateBlobProperties(Point[] contour, int id)
        {
            var blob = new BlobResult
            {
                Id = id,
                Contour = contour
            };

            // 면적
            blob.Area = Cv2.ContourArea(contour);

            // 둘레
            blob.Perimeter = Cv2.ArcLength(contour, true);

            // Bounding Rectangle
            blob.BoundingRect = Cv2.BoundingRect(contour);

            // Minimum Area Rectangle (회전된 사각형)
            if (contour.Length >= 5)
            {
                blob.MinAreaRect = Cv2.MinAreaRect(contour);
                blob.Angle = blob.MinAreaRect.Angle;
            }

            // 모멘트 계산
            var moments = Cv2.Moments(contour);
            if (moments.M00 > 0)
            {
                blob.CenterX = moments.M10 / moments.M00;
                blob.CenterY = moments.M01 / moments.M00;
            }
            else
            {
                blob.CenterX = blob.BoundingRect.X + blob.BoundingRect.Width / 2.0;
                blob.CenterY = blob.BoundingRect.Y + blob.BoundingRect.Height / 2.0;
            }

            // Circularity (4π × Area / Perimeter²)
            if (blob.Perimeter > 0)
                blob.Circularity = 4 * Math.PI * blob.Area / (blob.Perimeter * blob.Perimeter);

            // Aspect Ratio
            if (blob.BoundingRect.Height > 0)
                blob.AspectRatio = (double)blob.BoundingRect.Width / blob.BoundingRect.Height;

            // Convex Hull
            var hull = Cv2.ConvexHull(contour);
            double hullArea = Cv2.ContourArea(hull);
            blob.Convexity = hullArea > 0 ? blob.Area / hullArea : 1;

            // Equivalent Diameter
            blob.EquivalentDiameter = Math.Sqrt(4 * blob.Area / Math.PI);

            // Extent (Area / Bounding Rect Area)
            double rectArea = blob.BoundingRect.Width * blob.BoundingRect.Height;
            blob.Extent = rectArea > 0 ? blob.Area / rectArea : 0;

            // Solidity (Area / Convex Hull Area)
            blob.Solidity = hullArea > 0 ? blob.Area / hullArea : 1;

            // Fit Ellipse (5개 이상의 점 필요)
            if (contour.Length >= 5)
            {
                blob.FitEllipse = Cv2.FitEllipse(contour);
            }

            return blob;
        }

        private List<BlobResult> SortBlobs(List<BlobResult> blobs)
        {
            IEnumerable<BlobResult> sorted = SortBy switch
            {
                BlobSortBy.Area => blobs.OrderBy(b => b.Area),
                BlobSortBy.Perimeter => blobs.OrderBy(b => b.Perimeter),
                BlobSortBy.CenterX => blobs.OrderBy(b => b.CenterX),
                BlobSortBy.CenterY => blobs.OrderBy(b => b.CenterY),
                BlobSortBy.Circularity => blobs.OrderBy(b => b.Circularity),
                BlobSortBy.AspectRatio => blobs.OrderBy(b => b.AspectRatio),
                _ => blobs.OrderBy(b => b.Area)
            };

            if (SortDescending)
                sorted = sorted.Reverse();

            return sorted.ToList();
        }

        private static Point[] OffsetPoints(Point[] points, int offsetX, int offsetY)
        {
            if (offsetX == 0 && offsetY == 0)
                return points;

            var result = new Point[points.Length];
            for (int i = 0; i < points.Length; i++)
            {
                result[i] = new Point(points[i].X + offsetX, points[i].Y + offsetY);
            }
            return result;
        }

        private Scalar GetBlobColor(int index)
        {
            // 다양한 색상으로 Blob 구분
            var colors = new Scalar[]
            {
                new Scalar(0, 255, 0),     // Green
                new Scalar(255, 0, 0),     // Blue
                new Scalar(0, 255, 255),   // Yellow
                new Scalar(255, 0, 255),   // Magenta
                new Scalar(255, 255, 0),   // Cyan
                new Scalar(0, 128, 255),   // Orange
                new Scalar(128, 0, 255),   // Pink
                new Scalar(0, 255, 128),   // Spring Green
            };

            return colors[index % colors.Length];
        }

        public override VisionToolBase Clone()
        {
            return new BlobTool
            {
                Name = this.Name,
                ToolType = this.ToolType,
                IsEnabled = this.IsEnabled,
                ROI = this.ROI,
                UseROI = this.UseROI,
                UseInternalThreshold = this.UseInternalThreshold,
                ThresholdValue = this.ThresholdValue,
                InvertPolarity = this.InvertPolarity,
                MinArea = this.MinArea,
                MaxArea = this.MaxArea,
                MinPerimeter = this.MinPerimeter,
                MaxPerimeter = this.MaxPerimeter,
                MinCircularity = this.MinCircularity,
                MaxCircularity = this.MaxCircularity,
                MinAspectRatio = this.MinAspectRatio,
                MaxAspectRatio = this.MaxAspectRatio,
                MinConvexity = this.MinConvexity,
                MaxBlobCount = this.MaxBlobCount,
                SortBy = this.SortBy,
                SortDescending = this.SortDescending,
                RetrievalMode = this.RetrievalMode,
                ApproximationMode = this.ApproximationMode,
                DrawContours = this.DrawContours,
                DrawBoundingBox = this.DrawBoundingBox,
                DrawCenterPoint = this.DrawCenterPoint,
                DrawLabels = this.DrawLabels
            };
        }
    }

    /// <summary>
    /// Blob 분석 결과
    /// </summary>
    public class BlobResult
    {
        public int Id { get; set; }
        public Point[] Contour { get; set; } = Array.Empty<Point>();

        // 위치
        public double CenterX { get; set; }
        public double CenterY { get; set; }

        // 크기
        public double Area { get; set; }
        public double Perimeter { get; set; }
        public double EquivalentDiameter { get; set; }

        // 형상
        public double Circularity { get; set; }
        public double AspectRatio { get; set; }
        public double Convexity { get; set; }
        public double Solidity { get; set; }
        public double Extent { get; set; }
        public double Angle { get; set; }

        // Bounding Box
        public Rect BoundingRect { get; set; }
        public RotatedRect MinAreaRect { get; set; }
        public RotatedRect FitEllipse { get; set; }
    }

    public enum BlobSortBy
    {
        Area,
        Perimeter,
        CenterX,
        CenterY,
        Circularity,
        AspectRatio
    }
}
