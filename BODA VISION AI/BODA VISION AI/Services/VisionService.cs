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
using System.Linq;
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

        // 도구 간 연결 정보
        private readonly List<ToolConnectionInfo> _connections = new();

        /// <summary>
        /// 도구 간 연결 정보 (내부용)
        /// ID 기반 매칭으로 백그라운드 스레드에서도 안정적으로 동작
        /// </summary>
        private class ToolConnectionInfo
        {
            public string SourceId { get; set; } = string.Empty;
            public string TargetId { get; set; } = string.Empty;
            public ConnectionType Type { get; set; }
        }

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
            _connections.Clear();
        }

        #region Connection Management

        /// <summary>
        /// 도구 간 연결 추가
        /// </summary>
        public void AddConnection(VisionToolBase source, VisionToolBase target, ConnectionType type)
        {
            // 중복 방지 (ID 기반)
            if (_connections.Any(c => c.SourceId == source.Id && c.TargetId == target.Id && c.Type == type))
                return;

            _connections.Add(new ToolConnectionInfo
            {
                SourceId = source.Id,
                TargetId = target.Id,
                Type = type
            });
        }

        /// <summary>
        /// 도구 간 연결 제거
        /// </summary>
        public void RemoveConnection(VisionToolBase source, VisionToolBase target, ConnectionType type)
        {
            _connections.RemoveAll(c => c.SourceId == source.Id && c.TargetId == target.Id && c.Type == type);
        }

        /// <summary>
        /// 모든 연결 제거
        /// </summary>
        public void ClearConnections()
        {
            _connections.Clear();
        }

        /// <summary>
        /// 특정 도구에 대한 입력 연결 가져오기 (해당 도구가 Target인 연결들)
        /// </summary>
        private List<ToolConnectionInfo> GetInputConnections(VisionToolBase tool)
        {
            return _connections.Where(c => c.TargetId == tool.Id).ToList();
        }

        /// <summary>
        /// 연결 정보를 기반으로 도구의 입력 이미지 결정
        /// ID 기반으로 Source 도구를 찾아 결과 이미지를 반환
        /// </summary>
        private Mat? GetConnectedInputImage(VisionToolBase tool, Dictionary<string, VisionResult> resultMap)
        {
            var imageConnection = _connections
                .FirstOrDefault(c => c.TargetId == tool.Id && c.Type == ConnectionType.Image);

            if (imageConnection != null && resultMap.TryGetValue(imageConnection.SourceId, out var sourceResult))
            {
                // Image 연결: Source 도구의 출력 이미지를 사용
                if (sourceResult.OutputImage != null && !sourceResult.OutputImage.Empty())
                    return sourceResult.OutputImage.Clone();
            }

            return null;
        }

        /// <summary>
        /// Result 연결 확인: 연결된 Source 도구의 결과가 실패이면 실행 건너뛰기
        /// </summary>
        private bool ShouldSkipByResultConnection(VisionToolBase tool, Dictionary<string, VisionResult> resultMap)
        {
            var resultConnections = _connections
                .Where(c => c.TargetId == tool.Id && c.Type == ConnectionType.Result)
                .ToList();

            foreach (var conn in resultConnections)
            {
                if (resultMap.TryGetValue(conn.SourceId, out var sourceResult))
                {
                    // Result 연결: Source가 실패이면 Target도 건너뜀
                    if (!sourceResult.Success)
                        return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Coordinates 연결: Source 도구의 좌표 데이터를 Target 도구에 적용
        /// </summary>
        private void ApplyCoordinatesConnection(VisionToolBase tool, Dictionary<string, VisionResult> resultMap)
        {
            var coordConnections = _connections
                .Where(c => c.TargetId == tool.Id && c.Type == ConnectionType.Coordinates)
                .ToList();

            foreach (var conn in coordConnections)
            {
                if (resultMap.TryGetValue(conn.SourceId, out var sourceResult) && sourceResult.Data != null)
                {
                    // 좌표 데이터 전달: Source의 Data에서 좌표 추출 → Target의 ROI에 적용
                    if (sourceResult.Data.TryGetValue("CenterX", out var cx) &&
                        sourceResult.Data.TryGetValue("CenterY", out var cy))
                    {
                        double centerX = Convert.ToDouble(cx);
                        double centerY = Convert.ToDouble(cy);

                        // 좌표를 기반으로 ROI 설정 (중심점 기준 기존 ROI 크기 유지)
                        int roiWidth = tool.ROI.Width > 0 ? tool.ROI.Width : 100;
                        int roiHeight = tool.ROI.Height > 0 ? tool.ROI.Height : 100;

                        tool.ROI = new Rect(
                            (int)(centerX - roiWidth / 2),
                            (int)(centerY - roiHeight / 2),
                            roiWidth,
                            roiHeight);
                        tool.UseROI = true;
                    }

                    // BoundingRect가 있으면 직접 ROI로 사용
                    if (sourceResult.Data.TryGetValue("BoundingRect", out var rectObj) && rectObj is Rect boundingRect)
                    {
                        tool.ROI = boundingRect;
                        tool.UseROI = true;
                    }
                }
            }
        }

        #endregion

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
        /// 연결선이 있는 경우 연결에 따라 데이터를 전달하고,
        /// 연결이 없는 경우 기존 순차 파이프라인 방식으로 동작
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

            // 각 도구의 실행 결과를 추적 (ID 기반, 연결 데이터 전달용)
            var resultMap = new Dictionary<string, VisionResult>();
            Mat workingImage = CurrentImage.Clone();
            bool allSuccess = true;

            try
            {
                foreach (var tool in Tools)
                {
                    if (!tool.IsEnabled)
                        continue;

                    // 1. Result 연결 확인: Source가 실패이면 건너뛰기
                    if (ShouldSkipByResultConnection(tool, resultMap))
                    {
                        var skipResult = new VisionResult
                        {
                            Success = false,
                            Message = $"연결된 도구의 결과가 실패하여 건너뜀: {tool.Name}"
                        };
                        results.Add(skipResult);
                        Results.Add(skipResult);
                        resultMap[tool.Id] = skipResult;
                        allSuccess = false;
                        continue;
                    }

                    // 2. Coordinates 연결: Source의 좌표 데이터를 현재 도구에 적용
                    ApplyCoordinatesConnection(tool, resultMap);

                    // 3. Image 연결: 연결된 Source의 출력 이미지를 입력으로 사용
                    Mat inputImage;
                    var connectedImage = GetConnectedInputImage(tool, resultMap);
                    if (connectedImage != null)
                    {
                        inputImage = connectedImage;
                    }
                    else
                    {
                        // 연결이 없으면 기존 방식: 이전 도구의 출력(workingImage) 사용
                        inputImage = workingImage.Clone();
                    }

                    try
                    {
                        var result = tool.Execute(inputImage);
                        results.Add(result);
                        Results.Add(result);
                        resultMap[tool.Id] = result;

                        if (!result.Success)
                        {
                            allSuccess = false;
                        }

                        // Image 연결이 없는 경우에만 기존 파이프라인 유지
                        if (connectedImage == null && result.OutputImage != null && !result.OutputImage.Empty())
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
                    finally
                    {
                        if (connectedImage != null)
                            inputImage.Dispose();
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
