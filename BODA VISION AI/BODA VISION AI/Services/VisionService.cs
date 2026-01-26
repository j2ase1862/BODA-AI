using BODA_VISION_AI.Models;
using BODA_VISION_AI.VisionTools.BlobAnalysis;
using BODA_VISION_AI.VisionTools.ImageProcessing;
using BODA_VISION_AI.VisionTools.Measurement;
using BODA_VISION_AI.VisionTools.PatternMatching;
using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace BODA_VISION_AI.Services
{
    /// <summary>
    /// 비전 처리 서비스
    /// Cognex VisionPro의 CogJobManager 역할을 대체
    /// </summary>
    public class VisionService : ObservableObject
    {
        private static VisionService? _instance;
        public static VisionService Instance => _instance ??= new VisionService();

        // 현재 이미지
        private Mat? _currentImage;
        public Mat? CurrentImage
        {
            get => _currentImage;
            set
            {
                _currentImage?.Dispose();
                SetProperty(ref _currentImage, value);
                UpdateDisplayImage();
            }
        }

        // 화면 표시용 이미지
        private ImageSource? _displayImage;
        public ImageSource? DisplayImage
        {
            get => _displayImage;
            private set => SetProperty(ref _displayImage, value);
        }

        // 오버레이 이미지
        private ImageSource? _overlayImage;
        public ImageSource? OverlayImage
        {
            get => _overlayImage;
            private set => SetProperty(ref _overlayImage, value);
        }

        // 도구 목록
        public ObservableCollection<VisionToolBase> Tools { get; } = new();

        // 실행 결과 목록
        public ObservableCollection<VisionResult> Results { get; } = new();

        // 전체 실행 시간
        private double _totalExecutionTime;
        public double TotalExecutionTime
        {
            get => _totalExecutionTime;
            private set => SetProperty(ref _totalExecutionTime, value);
        }

        // 실행 상태
        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            private set => SetProperty(ref _isRunning, value);
        }

        // 마지막 실행 성공 여부
        private bool _lastRunSuccess;
        public bool LastRunSuccess
        {
            get => _lastRunSuccess;
            private set => SetProperty(ref _lastRunSuccess, value);
        }

        private VisionService() { }

        /// <summary>
        /// 이미지 파일 로드
        /// </summary>
        public bool LoadImage(string filePath)
        {
            try
            {
                var image = Cv2.ImRead(filePath);
                if (image.Empty())
                    return false;

                CurrentImage = image;
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Mat 이미지 설정
        /// </summary>
        public void SetImage(Mat image)
        {
            CurrentImage = image.Clone();
        }

        /// <summary>
        /// 화면 표시 이미지 업데이트
        /// </summary>
        private void UpdateDisplayImage()
        {
            if (CurrentImage == null || CurrentImage.Empty())
            {
                DisplayImage = null;
                return;
            }

            try
            {
                DisplayImage = CurrentImage.ToWriteableBitmap();
            }
            catch
            {
                DisplayImage = null;
            }
        }

        /// <summary>
        /// 도구 추가
        /// </summary>
        public void AddTool(VisionToolBase tool)
        {
            Tools.Add(tool);
        }

        /// <summary>
        /// 도구 제거
        /// </summary>
        public void RemoveTool(VisionToolBase tool)
        {
            Tools.Remove(tool);
        }

        /// <summary>
        /// 도구 순서 변경
        /// </summary>
        public void MoveTool(int fromIndex, int toIndex)
        {
            if (fromIndex >= 0 && fromIndex < Tools.Count &&
                toIndex >= 0 && toIndex < Tools.Count)
            {
                Tools.Move(fromIndex, toIndex);
            }
        }

        /// <summary>
        /// 모든 도구 제거
        /// </summary>
        public void ClearTools()
        {
            Tools.Clear();
        }

        /// <summary>
        /// 단일 도구 실행
        /// </summary>
        public VisionResult ExecuteTool(VisionToolBase tool, Mat? inputImage = null)
        {
            var image = inputImage ?? CurrentImage;
            if (image == null || image.Empty())
            {
                return new VisionResult
                {
                    Success = false,
                    Message = "입력 이미지가 없습니다."
                };
            }

            if (!tool.IsEnabled)
            {
                return new VisionResult
                {
                    Success = true,
                    Message = "도구가 비활성화되어 있습니다.",
                    OutputImage = image.Clone()
                };
            }

            return tool.Execute(image);
        }

        /// <summary>
        /// 모든 도구 순차 실행
        /// </summary>
        public async Task<List<VisionResult>> ExecuteAllAsync()
        {
            return await Task.Run(() => ExecuteAll());
        }

        /// <summary>
        /// 모든 도구 순차 실행 (동기)
        /// </summary>
        public List<VisionResult> ExecuteAll()
        {
            Results.Clear();
            var results = new List<VisionResult>();

            if (CurrentImage == null || CurrentImage.Empty())
            {
                var errorResult = new VisionResult
                {
                    Success = false,
                    Message = "입력 이미지가 없습니다."
                };
                results.Add(errorResult);
                Results.Add(errorResult);
                LastRunSuccess = false;
                return results;
            }

            IsRunning = true;
            var sw = Stopwatch.StartNew();

            Mat workingImage = CurrentImage.Clone();
            bool allSuccess = true;

            try
            {
                foreach (var tool in Tools)
                {
                    if (!tool.IsEnabled)
                        continue;

                    var result = tool.Execute(workingImage);
                    results.Add(result);
                    Results.Add(result);

                    if (!result.Success)
                    {
                        allSuccess = false;
                    }

                    // 출력 이미지가 있으면 다음 도구의 입력으로 사용
                    if (result.OutputImage != null && !result.OutputImage.Empty())
                    {
                        workingImage.Dispose();
                        workingImage = result.OutputImage.Clone();
                    }

                    // 오버레이 이미지 업데이트
                    if (result.OverlayImage != null && !result.OverlayImage.Empty())
                    {
                        OverlayImage = result.OverlayImage.ToWriteableBitmap();
                    }
                }
            }
            finally
            {
                workingImage.Dispose();
            }

            sw.Stop();
            TotalExecutionTime = sw.Elapsed.TotalMilliseconds;
            LastRunSuccess = allSuccess;
            IsRunning = false;

            return results;
        }

        /// <summary>
        /// 도구 타입에 따른 새 인스턴스 생성
        /// </summary>
        public static VisionToolBase? CreateTool(string toolType)
        {
            return toolType switch
            {
                // Image Processing
                "GrayscaleTool" => new GrayscaleTool(),
                "BlurTool" => new BlurTool(),
                "ThresholdTool" => new ThresholdTool(),
                "EdgeDetectionTool" => new EdgeDetectionTool(),
                "MorphologyTool" => new MorphologyTool(),
                "HistogramTool" => new HistogramTool(),

                // Pattern Matching
                "TemplateMatchTool" => new TemplateMatchTool(),
                "FeatureMatchTool" => new FeatureMatchTool(),

                // Blob Analysis
                "BlobTool" => new BlobTool(),

                // Measurement
                "CaliperTool" => new CaliperTool(),
                "LineFitTool" => new LineFitTool(),
                "CircleFitTool" => new CircleFitTool(),

                _ => null
            };
        }

        /// <summary>
        /// 사용 가능한 모든 도구 타입 목록
        /// </summary>
        public static Dictionary<string, string[]> GetAvailableTools()
        {
            return new Dictionary<string, string[]>
            {
                ["Image Processing"] = new[]
                {
                    "GrayscaleTool",
                    "BlurTool",
                    "ThresholdTool",
                    "EdgeDetectionTool",
                    "MorphologyTool",
                    "HistogramTool"
                },
                ["Pattern Matching"] = new[]
                {
                    "TemplateMatchTool",
                    "FeatureMatchTool"
                },
                ["Blob Analysis"] = new[]
                {
                    "BlobTool"
                },
                ["Measurement"] = new[]
                {
                    "CaliperTool",
                    "LineFitTool",
                    "CircleFitTool"
                }
            };
        }

        /// <summary>
        /// 도구 타입에 따른 표시 이름
        /// </summary>
        public static string GetToolDisplayName(string toolType)
        {
            return toolType switch
            {
                "GrayscaleTool" => "Grayscale",
                "BlurTool" => "Blur",
                "ThresholdTool" => "Threshold",
                "EdgeDetectionTool" => "Edge Detection",
                "MorphologyTool" => "Morphology",
                "HistogramTool" => "Histogram",
                "TemplateMatchTool" => "Template Match",
                "FeatureMatchTool" => "Feature Match",
                "BlobTool" => "Blob Analysis",
                "CaliperTool" => "Caliper",
                "LineFitTool" => "Line Fit",
                "CircleFitTool" => "Circle Fit",
                _ => toolType
            };
        }

        /// <summary>
        /// 리소스 정리
        /// </summary>
        public void Dispose()
        {
            CurrentImage?.Dispose();
            CurrentImage = null;
        }
    }
}
