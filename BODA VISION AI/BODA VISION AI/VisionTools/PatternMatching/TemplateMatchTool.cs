using BODA_VISION_AI.Models;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace BODA_VISION_AI.VisionTools.PatternMatching
{
    /// <summary>
    /// 템플릿 매칭 도구 (Cognex VisionPro CogPMAlignTool 대체)
    /// 패턴 이미지를 기반으로 위치, 회전, 스케일 검출
    /// </summary>
    public class TemplateMatchTool : VisionToolBase
    {
        private Mat? _templateImage;
        public Mat? TemplateImage
        {
            get => _templateImage;
            set => SetProperty(ref _templateImage, value);
        }

        private string _templatePath = string.Empty;
        public string TemplatePath
        {
            get => _templatePath;
            set => SetProperty(ref _templatePath, value);
        }

        private TemplateMatchModes _matchMethod = TemplateMatchModes.CCoeffNormed;
        public TemplateMatchModes MatchMethod
        {
            get => _matchMethod;
            set => SetProperty(ref _matchMethod, value);
        }

        private double _matchThreshold = 0.8;
        public double MatchThreshold
        {
            get => _matchThreshold;
            set => SetProperty(ref _matchThreshold, Math.Clamp(value, 0, 1));
        }

        private int _maxResults = 10;
        public int MaxResults
        {
            get => _maxResults;
            set => SetProperty(ref _maxResults, Math.Max(1, value));
        }

        // Multi-Scale 매칭용
        private bool _enableMultiScale = false;
        public bool EnableMultiScale
        {
            get => _enableMultiScale;
            set => SetProperty(ref _enableMultiScale, value);
        }

        private double _minScale = 0.8;
        public double MinScale
        {
            get => _minScale;
            set => SetProperty(ref _minScale, Math.Max(0.1, value));
        }

        private double _maxScale = 1.2;
        public double MaxScale
        {
            get => _maxScale;
            set => SetProperty(ref _maxScale, Math.Max(MinScale, value));
        }

        private double _scaleStep = 0.1;
        public double ScaleStep
        {
            get => _scaleStep;
            set => SetProperty(ref _scaleStep, Math.Max(0.01, value));
        }

        // Multi-Angle 매칭용
        private bool _enableMultiAngle = false;
        public bool EnableMultiAngle
        {
            get => _enableMultiAngle;
            set => SetProperty(ref _enableMultiAngle, value);
        }

        private double _minAngle = -15;
        public double MinAngle
        {
            get => _minAngle;
            set => SetProperty(ref _minAngle, value);
        }

        private double _maxAngle = 15;
        public double MaxAngle
        {
            get => _maxAngle;
            set => SetProperty(ref _maxAngle, value);
        }

        private double _angleStep = 5;
        public double AngleStep
        {
            get => _angleStep;
            set => SetProperty(ref _angleStep, Math.Max(0.1, value));
        }

        public TemplateMatchTool()
        {
            Name = "Template Match";
            ToolType = "TemplateMatchTool";
        }

        /// <summary>
        /// 템플릿 이미지 학습 (Train)
        /// </summary>
        public bool TrainPattern(Mat patternImage)
        {
            try
            {
                TemplateImage?.Dispose();
                TemplateImage = patternImage.Clone();
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 파일에서 템플릿 로드
        /// </summary>
        public bool LoadTemplate(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                    return false;

                TemplateImage?.Dispose();
                TemplateImage = Cv2.ImRead(filePath);
                TemplatePath = filePath;
                return !TemplateImage.Empty();
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 템플릿을 파일로 저장
        /// </summary>
        public bool SaveTemplate(string filePath)
        {
            try
            {
                if (TemplateImage == null || TemplateImage.Empty())
                    return false;

                return Cv2.ImWrite(filePath, TemplateImage);
            }
            catch
            {
                return false;
            }
        }

        public override VisionResult Execute(Mat inputImage)
        {
            var result = new VisionResult();
            var sw = Stopwatch.StartNew();

            try
            {
                if (TemplateImage == null || TemplateImage.Empty())
                {
                    result.Success = false;
                    result.Message = "템플릿 이미지가 설정되지 않았습니다.";
                    return result;
                }

                Mat workImage = GetROIImage(inputImage);
                var matches = new List<MatchResult>();

                if (EnableMultiScale || EnableMultiAngle)
                {
                    matches = ExecuteMultiScaleAngleMatch(workImage);
                }
                else
                {
                    matches = ExecuteSimpleMatch(workImage);
                }

                // 결과 정렬 및 NMS(Non-Maximum Suppression) 적용
                matches = ApplyNonMaximumSuppression(matches);
                matches = matches.Take(MaxResults).ToList();

                // ROI 오프셋 계산
                int offsetX = 0, offsetY = 0;
                if (UseROI && ROI.Width > 0 && ROI.Height > 0)
                {
                    var adjustedROI = GetAdjustedROI(inputImage);
                    offsetX = adjustedROI.X;
                    offsetY = adjustedROI.Y;
                }

                // 결과 이미지에 매칭 위치 표시
                Mat overlayImage = inputImage.Clone();

                // ROI 영역 표시
                if (UseROI && ROI.Width > 0 && ROI.Height > 0)
                {
                    Cv2.Rectangle(overlayImage, GetAdjustedROI(inputImage), new Scalar(0, 200, 200), 2);
                }

                foreach (var match in matches)
                {
                    // ROI 오프셋 적용
                    double drawX = match.X + offsetX;
                    double drawY = match.Y + offsetY;
                    double drawCenterX = match.CenterX + offsetX;
                    double drawCenterY = match.CenterY + offsetY;

                    // 매칭 영역 사각형
                    Cv2.Rectangle(overlayImage,
                        new Point((int)drawX, (int)drawY),
                        new Point((int)(drawX + match.Width), (int)(drawY + match.Height)),
                        new Scalar(0, 255, 0), 2);

                    // 중심점 표시
                    Cv2.DrawMarker(overlayImage,
                        new Point((int)drawCenterX, (int)drawCenterY),
                        new Scalar(0, 0, 255), MarkerTypes.Cross, 20, 2);

                    // 점수 표시
                    Cv2.PutText(overlayImage,
                        $"{match.Score:F2}",
                        new Point((int)drawX, (int)drawY - 5),
                        HersheyFonts.HersheySimplex, 0.5, new Scalar(255, 255, 0), 1);

                    // Graphics 추가 (오프셋 적용된 좌표)
                    result.Graphics.Add(new GraphicOverlay
                    {
                        Type = GraphicType.Rectangle,
                        Position = new Point2d(drawX, drawY),
                        Width = match.Width,
                        Height = match.Height,
                        Color = new Scalar(0, 255, 0)
                    });

                    result.Graphics.Add(new GraphicOverlay
                    {
                        Type = GraphicType.Crosshair,
                        Position = new Point2d(drawCenterX, drawCenterY),
                        Color = new Scalar(0, 0, 255)
                    });
                }

                // ROI가 사용된 경우 OutputImage도 원본 크기로 적용
                Mat finalOutput;
                if (UseROI && ROI.Width > 0 && ROI.Height > 0)
                {
                    finalOutput = ApplyROIResult(inputImage, workImage);
                }
                else
                {
                    finalOutput = workImage;
                }

                result.Success = matches.Count > 0;
                result.Message = $"매칭 완료: {matches.Count}개 발견";
                result.OutputImage = finalOutput;
                result.OverlayImage = overlayImage;
                result.Data["MatchCount"] = matches.Count;
                result.Data["Matches"] = matches;

                if (matches.Count > 0)
                {
                    result.Data["BestScore"] = matches[0].Score;
                    result.Data["BestX"] = matches[0].CenterX;
                    result.Data["BestY"] = matches[0].CenterY;
                    result.Data["BestAngle"] = matches[0].Angle;
                    result.Data["BestScale"] = matches[0].Scale;
                }

                if (workImage != inputImage)
                    workImage.Dispose();
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.Message = $"Template Match 실패: {ex.Message}";
            }

            sw.Stop();
            ExecutionTime = sw.Elapsed.TotalMilliseconds;
            LastResult = result;
            return result;
        }

        private List<MatchResult> ExecuteSimpleMatch(Mat searchImage)
        {
            var matches = new List<MatchResult>();

            Mat graySearch = new Mat();
            Mat grayTemplate = new Mat();

            if (searchImage.Channels() > 1)
                Cv2.CvtColor(searchImage, graySearch, ColorConversionCodes.BGR2GRAY);
            else
                graySearch = searchImage.Clone();

            if (TemplateImage!.Channels() > 1)
                Cv2.CvtColor(TemplateImage, grayTemplate, ColorConversionCodes.BGR2GRAY);
            else
                grayTemplate = TemplateImage.Clone();

            Mat resultMat = new Mat();
            Cv2.MatchTemplate(graySearch, grayTemplate, resultMat, MatchMethod);

            // 다중 매칭 결과 추출
            while (true)
            {
                Cv2.MinMaxLoc(resultMat, out double minVal, out double maxVal, out Point minLoc, out Point maxLoc);

                double score = (MatchMethod == TemplateMatchModes.SqDiff || MatchMethod == TemplateMatchModes.SqDiffNormed)
                    ? 1 - minVal : maxVal;
                Point matchLoc = (MatchMethod == TemplateMatchModes.SqDiff || MatchMethod == TemplateMatchModes.SqDiffNormed)
                    ? minLoc : maxLoc;

                if (score < MatchThreshold)
                    break;

                matches.Add(new MatchResult
                {
                    X = matchLoc.X,
                    Y = matchLoc.Y,
                    Width = grayTemplate.Width,
                    Height = grayTemplate.Height,
                    CenterX = matchLoc.X + grayTemplate.Width / 2.0,
                    CenterY = matchLoc.Y + grayTemplate.Height / 2.0,
                    Score = score,
                    Scale = 1.0,
                    Angle = 0
                });

                // 해당 영역 마스킹 (추가 검출 방지)
                Cv2.Rectangle(resultMat, matchLoc,
                    new Point(matchLoc.X + grayTemplate.Width, matchLoc.Y + grayTemplate.Height),
                    new Scalar(0), -1);

                if (matches.Count >= MaxResults * 2) // NMS 전이므로 여유있게
                    break;
            }

            graySearch.Dispose();
            grayTemplate.Dispose();
            resultMat.Dispose();

            return matches;
        }

        private List<MatchResult> ExecuteMultiScaleAngleMatch(Mat searchImage)
        {
            var matches = new List<MatchResult>();

            Mat graySearch = new Mat();
            if (searchImage.Channels() > 1)
                Cv2.CvtColor(searchImage, graySearch, ColorConversionCodes.BGR2GRAY);
            else
                graySearch = searchImage.Clone();

            Mat grayTemplate = new Mat();
            if (TemplateImage!.Channels() > 1)
                Cv2.CvtColor(TemplateImage, grayTemplate, ColorConversionCodes.BGR2GRAY);
            else
                grayTemplate = TemplateImage.Clone();

            double bestScore = 0;
            double bestScale = 1.0;
            double bestAngle = 0;

            for (double scale = MinScale; scale <= MaxScale; scale += ScaleStep)
            {
                int newWidth = (int)(grayTemplate.Width * scale);
                int newHeight = (int)(grayTemplate.Height * scale);

                if (newWidth < 10 || newHeight < 10)
                    continue;

                Mat scaledTemplate = new Mat();
                Cv2.Resize(grayTemplate, scaledTemplate, new Size(newWidth, newHeight));

                double angleStart = EnableMultiAngle ? MinAngle : 0;
                double angleEnd = EnableMultiAngle ? MaxAngle : 0;
                double angleStepVal = EnableMultiAngle ? AngleStep : 1;

                for (double angle = angleStart; angle <= angleEnd; angle += angleStepVal)
                {
                    Mat rotatedTemplate = scaledTemplate;

                    if (Math.Abs(angle) > 0.1)
                    {
                        var center = new Point2f(scaledTemplate.Width / 2f, scaledTemplate.Height / 2f);
                        var rotMatrix = Cv2.GetRotationMatrix2D(center, angle, 1.0);
                        rotatedTemplate = new Mat();
                        Cv2.WarpAffine(scaledTemplate, rotatedTemplate, rotMatrix, scaledTemplate.Size());
                        rotMatrix.Dispose();
                    }

                    if (rotatedTemplate.Width > graySearch.Width || rotatedTemplate.Height > graySearch.Height)
                    {
                        if (rotatedTemplate != scaledTemplate)
                            rotatedTemplate.Dispose();
                        continue;
                    }

                    Mat resultMat = new Mat();
                    Cv2.MatchTemplate(graySearch, rotatedTemplate, resultMat, MatchMethod);

                    Cv2.MinMaxLoc(resultMat, out double minVal, out double maxVal, out Point minLoc, out Point maxLoc);

                    double score = (MatchMethod == TemplateMatchModes.SqDiff || MatchMethod == TemplateMatchModes.SqDiffNormed)
                        ? 1 - minVal : maxVal;
                    Point matchLoc = (MatchMethod == TemplateMatchModes.SqDiff || MatchMethod == TemplateMatchModes.SqDiffNormed)
                        ? minLoc : maxLoc;

                    if (score >= MatchThreshold)
                    {
                        matches.Add(new MatchResult
                        {
                            X = matchLoc.X,
                            Y = matchLoc.Y,
                            Width = rotatedTemplate.Width,
                            Height = rotatedTemplate.Height,
                            CenterX = matchLoc.X + rotatedTemplate.Width / 2.0,
                            CenterY = matchLoc.Y + rotatedTemplate.Height / 2.0,
                            Score = score,
                            Scale = scale,
                            Angle = angle
                        });
                    }

                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestScale = scale;
                        bestAngle = angle;
                    }

                    resultMat.Dispose();
                    if (rotatedTemplate != scaledTemplate)
                        rotatedTemplate.Dispose();
                }

                scaledTemplate.Dispose();
            }

            graySearch.Dispose();
            grayTemplate.Dispose();

            return matches;
        }

        private List<MatchResult> ApplyNonMaximumSuppression(List<MatchResult> matches, double overlapThreshold = 0.5)
        {
            if (matches.Count == 0)
                return matches;

            // 점수순 정렬
            var sorted = matches.OrderByDescending(m => m.Score).ToList();
            var selected = new List<MatchResult>();

            while (sorted.Count > 0)
            {
                var best = sorted[0];
                selected.Add(best);
                sorted.RemoveAt(0);

                sorted.RemoveAll(m => CalculateIoU(best, m) > overlapThreshold);
            }

            return selected;
        }

        private double CalculateIoU(MatchResult a, MatchResult b)
        {
            double x1 = Math.Max(a.X, b.X);
            double y1 = Math.Max(a.Y, b.Y);
            double x2 = Math.Min(a.X + a.Width, b.X + b.Width);
            double y2 = Math.Min(a.Y + a.Height, b.Y + b.Height);

            if (x2 < x1 || y2 < y1)
                return 0;

            double intersection = (x2 - x1) * (y2 - y1);
            double areaA = a.Width * a.Height;
            double areaB = b.Width * b.Height;
            double union = areaA + areaB - intersection;

            return intersection / union;
        }

        public override VisionToolBase Clone()
        {
            var clone = new TemplateMatchTool
            {
                Name = this.Name,
                ToolType = this.ToolType,
                IsEnabled = this.IsEnabled,
                ROI = this.ROI,
                UseROI = this.UseROI,
                MatchMethod = this.MatchMethod,
                MatchThreshold = this.MatchThreshold,
                MaxResults = this.MaxResults,
                EnableMultiScale = this.EnableMultiScale,
                MinScale = this.MinScale,
                MaxScale = this.MaxScale,
                ScaleStep = this.ScaleStep,
                EnableMultiAngle = this.EnableMultiAngle,
                MinAngle = this.MinAngle,
                MaxAngle = this.MaxAngle,
                AngleStep = this.AngleStep,
                TemplatePath = this.TemplatePath
            };

            if (TemplateImage != null && !TemplateImage.Empty())
                clone.TemplateImage = TemplateImage.Clone();

            return clone;
        }
    }

    /// <summary>
    /// 매칭 결과 데이터
    /// </summary>
    public class MatchResult
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double CenterX { get; set; }
        public double CenterY { get; set; }
        public double Score { get; set; }
        public double Scale { get; set; }
        public double Angle { get; set; }
    }
}
