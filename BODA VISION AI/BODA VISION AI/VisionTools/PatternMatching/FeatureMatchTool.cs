using BODA_VISION_AI.Models;
using OpenCvSharp;
using OpenCvSharp.Features2D;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace BODA_VISION_AI.VisionTools.PatternMatching
{
    /// <summary>
    /// Feature 기반 매칭 도구 (ORB, SIFT 등)
    /// Cognex VisionPro CogPatMaxTool의 기능 일부 대체
    /// 회전, 스케일 변화에 강인한 매칭
    /// </summary>
    public class FeatureMatchTool : VisionToolBase
    {
        private Mat? _templateImage;
        public Mat? TemplateImage
        {
            get => _templateImage;
            set => SetProperty(ref _templateImage, value);
        }

        private KeyPoint[]? _templateKeypoints;
        private Mat? _templateDescriptors;

        private FeatureDetectorType _detectorType = FeatureDetectorType.ORB;
        public FeatureDetectorType DetectorType
        {
            get => _detectorType;
            set => SetProperty(ref _detectorType, value);
        }

        private int _maxFeatures = 500;
        public int MaxFeatures
        {
            get => _maxFeatures;
            set => SetProperty(ref _maxFeatures, Math.Max(10, value));
        }

        private double _ratioThreshold = 0.75;
        public double RatioThreshold
        {
            get => _ratioThreshold;
            set => SetProperty(ref _ratioThreshold, Math.Clamp(value, 0.1, 1.0));
        }

        private int _minMatchCount = 10;
        public int MinMatchCount
        {
            get => _minMatchCount;
            set => SetProperty(ref _minMatchCount, Math.Max(4, value));
        }

        private bool _drawMatches = true;
        public bool DrawMatches
        {
            get => _drawMatches;
            set => SetProperty(ref _drawMatches, value);
        }

        public FeatureMatchTool()
        {
            Name = "Feature Match";
            ToolType = "FeatureMatchTool";
        }

        /// <summary>
        /// 템플릿 이미지 학습 - Feature 추출
        /// </summary>
        public bool TrainPattern(Mat patternImage)
        {
            try
            {
                TemplateImage?.Dispose();
                _templateDescriptors?.Dispose();

                TemplateImage = patternImage.Clone();

                Mat grayTemplate = new Mat();
                if (patternImage.Channels() > 1)
                    Cv2.CvtColor(patternImage, grayTemplate, ColorConversionCodes.BGR2GRAY);
                else
                    grayTemplate = patternImage.Clone();

                var detector = CreateDetector();
                _templateDescriptors = new Mat();
                detector.DetectAndCompute(grayTemplate, null, out _templateKeypoints, _templateDescriptors);

                grayTemplate.Dispose();

                return _templateKeypoints != null && _templateKeypoints.Length > MinMatchCount;
            }
            catch
            {
                return false;
            }
        }

        private Feature2D CreateDetector()
        {
            return DetectorType switch
            {
                FeatureDetectorType.ORB => ORB.Create(MaxFeatures),
                FeatureDetectorType.AKAZE => AKAZE.Create(),
                FeatureDetectorType.BRISK => BRISK.Create(),
                _ => ORB.Create(MaxFeatures)
            };
        }

        public override VisionResult Execute(Mat inputImage)
        {
            var result = new VisionResult();
            var sw = Stopwatch.StartNew();

            try
            {
                if (TemplateImage == null || _templateKeypoints == null || _templateDescriptors == null)
                {
                    result.Success = false;
                    result.Message = "템플릿이 학습되지 않았습니다.";
                    return result;
                }

                Mat workImage = GetROIImage(inputImage);
                Mat graySearch = new Mat();

                if (workImage.Channels() > 1)
                    Cv2.CvtColor(workImage, graySearch, ColorConversionCodes.BGR2GRAY);
                else
                    graySearch = workImage.Clone();

                // Feature 검출
                var detector = CreateDetector();
                Mat searchDescriptors = new Mat();
                detector.DetectAndCompute(graySearch, null, out KeyPoint[] searchKeypoints, searchDescriptors);

                if (searchKeypoints.Length < MinMatchCount || searchDescriptors.Empty())
                {
                    result.Success = false;
                    result.Message = "검색 이미지에서 충분한 Feature를 찾지 못했습니다.";
                    graySearch.Dispose();
                    searchDescriptors.Dispose();
                    return result;
                }

                // Feature 매칭
                var matcher = new BFMatcher(NormTypes.Hamming, crossCheck: false);
                var knnMatches = matcher.KnnMatch(_templateDescriptors, searchDescriptors, 2);

                // Ratio Test (Lowe's ratio test)
                var goodMatches = new List<DMatch>();
                foreach (var match in knnMatches)
                {
                    if (match.Length >= 2 && match[0].Distance < RatioThreshold * match[1].Distance)
                    {
                        goodMatches.Add(match[0]);
                    }
                }

                result.Data["TotalMatches"] = knnMatches.Length;
                result.Data["GoodMatches"] = goodMatches.Count;
                result.Data["TemplateKeypoints"] = _templateKeypoints.Length;
                result.Data["SearchKeypoints"] = searchKeypoints.Length;

                if (goodMatches.Count >= MinMatchCount)
                {
                    // Homography 계산
                    var srcPoints = goodMatches.Select(m => _templateKeypoints[m.QueryIdx].Pt).ToArray();
                    var dstPoints = goodMatches.Select(m => searchKeypoints[m.TrainIdx].Pt).ToArray();

                    Mat homography = Cv2.FindHomography(
                        InputArray.Create(srcPoints),
                        InputArray.Create(dstPoints),
                        HomographyMethods.Ransac, 5.0);

                    if (!homography.Empty())
                    {
                        // 템플릿 코너를 변환하여 검출된 위치 계산
                        var templateCorners = new Point2f[]
                        {
                            new Point2f(0, 0),
                            new Point2f(TemplateImage.Width, 0),
                            new Point2f(TemplateImage.Width, TemplateImage.Height),
                            new Point2f(0, TemplateImage.Height)
                        };

                        var detectedCorners = Cv2.PerspectiveTransform(templateCorners, homography);

                        // 중심점 계산
                        double centerX = detectedCorners.Average(p => p.X);
                        double centerY = detectedCorners.Average(p => p.Y);

                        // 스케일 추정
                        double detectedWidth = Math.Sqrt(
                            Math.Pow(detectedCorners[1].X - detectedCorners[0].X, 2) +
                            Math.Pow(detectedCorners[1].Y - detectedCorners[0].Y, 2));
                        double scale = detectedWidth / TemplateImage.Width;

                        // 회전각 추정
                        double angle = Math.Atan2(
                            detectedCorners[1].Y - detectedCorners[0].Y,
                            detectedCorners[1].X - detectedCorners[0].X) * 180 / Math.PI;

                        result.Data["CenterX"] = centerX;
                        result.Data["CenterY"] = centerY;
                        result.Data["Scale"] = scale;
                        result.Data["Angle"] = angle;
                        result.Data["DetectedCorners"] = detectedCorners;

                        // 결과 이미지 생성
                        Mat overlayImage = inputImage.Clone();

                        // 검출된 영역 표시
                        var corners = detectedCorners.Select(p => new Point((int)p.X, (int)p.Y)).ToArray();
                        Cv2.Polylines(overlayImage, new[] { corners }, true, new Scalar(0, 255, 0), 2);

                        // 중심점 표시
                        Cv2.DrawMarker(overlayImage, new Point((int)centerX, (int)centerY),
                            new Scalar(0, 0, 255), MarkerTypes.Cross, 30, 2);

                        // Graphics 추가
                        result.Graphics.Add(new GraphicOverlay
                        {
                            Type = GraphicType.Polygon,
                            Points = corners.ToList(),
                            Color = new Scalar(0, 255, 0)
                        });

                        result.Graphics.Add(new GraphicOverlay
                        {
                            Type = GraphicType.Crosshair,
                            Position = new Point2d(centerX, centerY),
                            Color = new Scalar(0, 0, 255)
                        });

                        // 매칭 결과 그리기
                        if (DrawMatches)
                        {
                            Mat matchImage = new Mat();
                            Cv2.DrawMatches(TemplateImage, _templateKeypoints, workImage, searchKeypoints,
                                goodMatches.ToArray(), matchImage);
                            result.Data["MatchImage"] = matchImage;
                        }

                        result.OverlayImage = overlayImage;
                        homography.Dispose();
                    }

                    result.Success = true;
                    result.Message = $"Feature Match 성공: {goodMatches.Count}개 매칭";
                }
                else
                {
                    result.Success = false;
                    result.Message = $"매칭 부족: {goodMatches.Count}개 (최소 {MinMatchCount}개 필요)";
                }

                result.OutputImage = workImage;

                graySearch.Dispose();
                searchDescriptors.Dispose();
                matcher.Dispose();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Feature Match 실패: {ex.Message}";
            }

            sw.Stop();
            ExecutionTime = sw.Elapsed.TotalMilliseconds;
            LastResult = result;
            return result;
        }

        public override VisionToolBase Clone()
        {
            var clone = new FeatureMatchTool
            {
                Name = this.Name,
                ToolType = this.ToolType,
                IsEnabled = this.IsEnabled,
                ROI = this.ROI,
                UseROI = this.UseROI,
                DetectorType = this.DetectorType,
                MaxFeatures = this.MaxFeatures,
                RatioThreshold = this.RatioThreshold,
                MinMatchCount = this.MinMatchCount,
                DrawMatches = this.DrawMatches
            };

            if (TemplateImage != null && !TemplateImage.Empty())
            {
                clone.TrainPattern(TemplateImage);
            }

            return clone;
        }
    }

    public enum FeatureDetectorType
    {
        ORB,
        AKAZE,
        BRISK
    }
}
