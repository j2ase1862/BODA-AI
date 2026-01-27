using CommunityToolkit.Mvvm.ComponentModel;
using OpenCvSharp;
using System;
using System.Collections.Generic;

namespace BODA_VISION_AI.Models
{
    /// <summary>
    /// Cognex VisionPro 도구를 대체하는 OpenCvSharp 기반 비전 도구의 기본 클래스
    /// </summary>
    public abstract class VisionToolBase : ObservableObject
    {
        private string _name = string.Empty;
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        private string _toolType = string.Empty;
        public string ToolType
        {
            get => _toolType;
            set => SetProperty(ref _toolType, value);
        }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value);
        }

        private double _x;
        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        private double _y;
        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }

        // ROI 설정 (Cognex VisionPro의 Region 대체)
        private Rect _roi = new Rect();
        public Rect ROI
        {
            get => _roi;
            set => SetProperty(ref _roi, value);
        }

        private bool _useROI = false;
        public bool UseROI
        {
            get => _useROI;
            set => SetProperty(ref _useROI, value);
        }

        // 마지막 실행 결과
        private VisionResult? _lastResult;
        public VisionResult? LastResult
        {
            get => _lastResult;
            set => SetProperty(ref _lastResult, value);
        }

        // 실행 시간 (ms)
        private double _executionTime;
        public double ExecutionTime
        {
            get => _executionTime;
            set => SetProperty(ref _executionTime, value);
        }

        /// <summary>
        /// 도구 실행 - 파생 클래스에서 구현
        /// </summary>
        public abstract VisionResult Execute(Mat inputImage);

        /// <summary>
        /// ROI가 설정된 경우 해당 영역만 추출
        /// </summary>
        protected Mat GetROIImage(Mat inputImage)
        {
            if (!UseROI || ROI.Width <= 0 || ROI.Height <= 0)
                return inputImage.Clone();

            // ROI가 이미지 범위를 벗어나지 않도록 조정
            var adjustedROI = GetAdjustedROI(inputImage);

            return new Mat(inputImage, adjustedROI);
        }

        /// <summary>
        /// ROI를 이미지 범위에 맞게 조정하여 반환
        /// </summary>
        protected Rect GetAdjustedROI(Mat inputImage)
        {
            return new Rect(
                Math.Max(0, ROI.X),
                Math.Max(0, ROI.Y),
                Math.Min(ROI.Width, inputImage.Width - Math.Max(0, ROI.X)),
                Math.Min(ROI.Height, inputImage.Height - Math.Max(0, ROI.Y))
            );
        }

        /// <summary>
        /// 처리된 ROI 결과를 원본 이미지 크기에 맞게 적용
        /// ROI 외부 영역은 검은색(또는 지정된 색상)으로 채움
        /// </summary>
        /// <param name="inputImage">원본 입력 이미지</param>
        /// <param name="processedROI">처리된 ROI 이미지</param>
        /// <param name="fillColor">ROI 외부 영역을 채울 색상 (기본값: 검은색)</param>
        /// <returns>원본 크기의 이미지 (ROI 영역만 처리 결과 포함)</returns>
        protected Mat ApplyROIResult(Mat inputImage, Mat processedROI, Scalar? fillColor = null)
        {
            if (!UseROI || ROI.Width <= 0 || ROI.Height <= 0)
                return processedROI.Clone();

            // 원본 이미지와 같은 크기의 결과 이미지 생성
            var resultImage = new Mat(inputImage.Size(), processedROI.Type(), fillColor ?? Scalar.Black);

            // ROI 위치 계산
            var adjustedROI = GetAdjustedROI(inputImage);

            // 처리된 ROI를 결과 이미지의 해당 위치에 복사
            var destRegion = new Mat(resultImage, adjustedROI);

            // processedROI 크기가 adjustedROI와 다를 수 있으므로 크기 맞춤
            if (processedROI.Width == adjustedROI.Width && processedROI.Height == adjustedROI.Height)
            {
                processedROI.CopyTo(destRegion);
            }
            else
            {
                // 크기가 다른 경우 리사이즈
                var resized = new Mat();
                Cv2.Resize(processedROI, resized, new Size(adjustedROI.Width, adjustedROI.Height));
                resized.CopyTo(destRegion);
                resized.Dispose();
            }

            return resultImage;
        }

        /// <summary>
        /// 도구의 복제본 생성
        /// </summary>
        public abstract VisionToolBase Clone();
    }

    /// <summary>
    /// 비전 처리 결과
    /// </summary>
    public class VisionResult : ObservableObject
    {
        private bool _success;
        public bool Success
        {
            get => _success;
            set => SetProperty(ref _success, value);
        }

        private string _message = string.Empty;
        public string Message
        {
            get => _message;
            set => SetProperty(ref _message, value);
        }

        private Mat? _outputImage;
        public Mat? OutputImage
        {
            get => _outputImage;
            set => SetProperty(ref _outputImage, value);
        }

        private Mat? _overlayImage;
        public Mat? OverlayImage
        {
            get => _overlayImage;
            set => SetProperty(ref _overlayImage, value);
        }

        // 결과 데이터 (측정값, 좌표 등)
        public Dictionary<string, object> Data { get; set; } = new Dictionary<string, object>();

        // Graphics 오버레이 정보
        public List<GraphicOverlay> Graphics { get; set; } = new List<GraphicOverlay>();
    }

    /// <summary>
    /// 결과 표시를 위한 그래픽 오버레이
    /// </summary>
    public class GraphicOverlay
    {
        public GraphicType Type { get; set; }
        public Point2d Position { get; set; }
        public Point2d EndPosition { get; set; }
        public double Width { get; set; }
        public double Height { get; set; }
        public double Radius { get; set; }
        public double Angle { get; set; }
        public Scalar Color { get; set; } = new Scalar(0, 255, 0);
        public int Thickness { get; set; } = 2;
        public string? Text { get; set; }
        public List<Point>? Points { get; set; }
    }

    public enum GraphicType
    {
        Point,
        Line,
        Rectangle,
        Circle,
        Ellipse,
        Polygon,
        Text,
        Crosshair
    }
}
