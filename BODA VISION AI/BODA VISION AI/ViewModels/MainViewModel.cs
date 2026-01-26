using BODA_VISION_AI.Models;
using BODA_VISION_AI.Services;
using BODA_VISION_AI.VisionTools.BlobAnalysis;
using BODA_VISION_AI.VisionTools.ImageProcessing;
using BODA_VISION_AI.VisionTools.Measurement;
using BODA_VISION_AI.VisionTools.PatternMatching;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CvRect = OpenCvSharp.Rect;

namespace BODA_VISION_AI.ViewModels
{
    public class MainViewModel : ObservableObject
    {
        #region Fields
        private readonly string _appName = "BODA VISION AI";
        private readonly string _appVersion = "1.0.0";
        private Mat? _currentImage;
        #endregion

        #region Properties
        public string AppName => _appName;
        public string AppVersion => _appVersion;

        // 도구 트리 (사이드바)
        public ObservableCollection<ToolCategory> ToolTree { get; } = new();

        // 워크스페이스에 배치된 도구들
        public ObservableCollection<ToolItem> DroppedTools { get; } = new();

        // 실행 큐 (순서대로 실행될 도구들)
        public ObservableCollection<VisionToolBase> ExecutionQueue => VisionService.Instance.Tools;

        // 실행 결과
        public ObservableCollection<VisionResult> Results => VisionService.Instance.Results;

        // 현재 이미지
        public Mat? CurrentImage
        {
            get => _currentImage;
            set
            {
                _currentImage?.Dispose();
                SetProperty(ref _currentImage, value);
                if (value != null)
                    VisionService.Instance.SetImage(value);
                UpdateDisplayImage();
                NotifyCommandsCanExecuteChanged();
            }
        }

        // 표시용 이미지
        private ImageSource? _displayImage;
        public ImageSource? DisplayImage
        {
            get => _displayImage;
            set => SetProperty(ref _displayImage, value);
        }

        // 오버레이 이미지
        private ImageSource? _overlayImage;
        public ImageSource? OverlayImage
        {
            get => _overlayImage;
            set => SetProperty(ref _overlayImage, value);
        }

        // 결과 이미지
        private ImageSource? _resultImage;
        public ImageSource? ResultImage
        {
            get => _resultImage;
            set => SetProperty(ref _resultImage, value);
        }

        // ImageCanvas용 원본 Mat
        public Mat? SourceMat => CurrentImage;

        // ROI 컬렉션
        private ObservableCollection<ROIShape>? _roiShapes;
        public ObservableCollection<ROIShape>? ROIShapes
        {
            get => _roiShapes ??= new ObservableCollection<ROIShape>();
            set => SetProperty(ref _roiShapes, value);
        }

        // 선택된 ROI
        private ROIShape? _selectedROI;
        public ROIShape? SelectedROI
        {
            get => _selectedROI;
            set
            {
                SetProperty(ref _selectedROI, value);
                // 선택된 ROI를 현재 도구에 적용
                ApplyROIToSelectedTool();
            }
        }

        // 선택된 도구
        private ToolItem? _selectedTool;
        public ToolItem? SelectedTool
        {
            get => _selectedTool;
            set
            {
                SetProperty(ref _selectedTool, value);
                OnPropertyChanged(nameof(SelectedVisionTool));
                NotifyCommandsCanExecuteChanged();
            }
        }

        // 선택된 비전 도구 (설정 패널용)
        public VisionToolBase? SelectedVisionTool => SelectedTool?.VisionTool;

        // 실행 중 여부
        private bool _isRunning;
        public bool IsRunning
        {
            get => _isRunning;
            set => SetProperty(ref _isRunning, value);
        }

        // 상태 메시지
        private string _statusMessage = "Ready";
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        // 실행 시간
        private string _executionTimeText = "";
        public string ExecutionTimeText
        {
            get => _executionTimeText;
            set => SetProperty(ref _executionTimeText, value);
        }
        #endregion

        #region Commands
        public RelayCommand CloseCommand { get; }
        public RelayCommand OpenImageFileCommand { get; }
        public RelayCommand RunAllCommand { get; }
        public RelayCommand RunSelectedCommand { get; }
        public RelayCommand ClearToolsCommand { get; }
        public RelayCommand<ToolItem> RemoveToolCommand { get; }
        public RelayCommand<ToolItem> MoveToolUpCommand { get; }
        public RelayCommand<ToolItem> MoveToolDownCommand { get; }
        public RelayCommand TrainPatternCommand { get; }
        #endregion

        #region Constructor
        public MainViewModel()
        {
            InitializeToolTree();

            CloseCommand = new RelayCommand(CloseApplication);
            OpenImageFileCommand = new RelayCommand(OpenImageFile);
            RunAllCommand = new RelayCommand(async () => await RunAllTools(), () => !IsRunning && CurrentImage != null);
            RunSelectedCommand = new RelayCommand(RunSelectedTool, () => !IsRunning && SelectedTool != null && CurrentImage != null);
            ClearToolsCommand = new RelayCommand(ClearAllTools);
            RemoveToolCommand = new RelayCommand<ToolItem>(RemoveTool);
            MoveToolUpCommand = new RelayCommand<ToolItem>(MoveToolUp);
            MoveToolDownCommand = new RelayCommand<ToolItem>(MoveToolDown);
            TrainPatternCommand = new RelayCommand(TrainPattern, () => SelectedVisionTool is TemplateMatchTool or FeatureMatchTool);
        }
        #endregion

        #region Methods
        private void InitializeToolTree()
        {
            // Image Processing 카테고리
            var imageProcessing = new ToolCategory { CategoryName = "Image Processing" };
            imageProcessing.Tools.Add(new ToolItem { Name = "Grayscale", ToolType = "GrayscaleTool" });
            imageProcessing.Tools.Add(new ToolItem { Name = "Blur", ToolType = "BlurTool" });
            imageProcessing.Tools.Add(new ToolItem { Name = "Threshold", ToolType = "ThresholdTool" });
            imageProcessing.Tools.Add(new ToolItem { Name = "Edge Detection", ToolType = "EdgeDetectionTool" });
            imageProcessing.Tools.Add(new ToolItem { Name = "Morphology", ToolType = "MorphologyTool" });
            imageProcessing.Tools.Add(new ToolItem { Name = "Histogram", ToolType = "HistogramTool" });
            ToolTree.Add(imageProcessing);

            // Pattern Matching 카테고리
            var patternMatching = new ToolCategory { CategoryName = "Pattern Matching" };
            patternMatching.Tools.Add(new ToolItem { Name = "Template Match", ToolType = "TemplateMatchTool" });
            patternMatching.Tools.Add(new ToolItem { Name = "Feature Match", ToolType = "FeatureMatchTool" });
            ToolTree.Add(patternMatching);

            // Blob Analysis 카테고리
            var blobAnalysis = new ToolCategory { CategoryName = "Blob Analysis" };
            blobAnalysis.Tools.Add(new ToolItem { Name = "Blob", ToolType = "BlobTool" });
            ToolTree.Add(blobAnalysis);

            // Measurement 카테고리
            var measurement = new ToolCategory { CategoryName = "Measurement" };
            measurement.Tools.Add(new ToolItem { Name = "Caliper", ToolType = "CaliperTool" });
            measurement.Tools.Add(new ToolItem { Name = "Line Fit", ToolType = "LineFitTool" });
            measurement.Tools.Add(new ToolItem { Name = "Circle Fit", ToolType = "CircleFitTool" });
            ToolTree.Add(measurement);
        }

        private void CloseApplication()
        {
            Application.Current.Shutdown();
        }

        private void OpenImageFile()
        {
            var dlg = new OpenFileDialog
            {
                Title = "이미지 파일 열기",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff|All Files|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var mat = Cv2.ImRead(dlg.FileName);
                    if (!mat.Empty())
                    {
                        CurrentImage = mat;
                        StatusMessage = $"이미지 로드 완료: {System.IO.Path.GetFileName(dlg.FileName)}";
                    }
                }
                catch (Exception ex)
                {
                    StatusMessage = $"이미지 로드 실패: {ex.Message}";
                    System.Diagnostics.Debug.WriteLine($"이미지 로드 실패: {ex.Message}");
                }
            }
        }

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
                OnPropertyChanged(nameof(SourceMat));  // ImageCanvas에 이미지 변경 알림
            }
            catch
            {
                DisplayImage = null;
            }
        }

        /// <summary>
        /// 도구 드롭 처리 - 새 비전 도구 인스턴스 생성
        /// </summary>
        public ToolItem? CreateDroppedTool(ToolItem sourceTool, double x, double y)
        {
            var visionTool = VisionService.CreateTool(sourceTool.ToolType);
            if (visionTool == null)
                return null;

            var newTool = new ToolItem
            {
                Name = $"{sourceTool.Name} #{DroppedTools.Count(t => t.ToolType == sourceTool.ToolType) + 1}",
                ToolType = sourceTool.ToolType,
                X = x,
                Y = y,
                VisionTool = visionTool
            };

            DroppedTools.Add(newTool);
            VisionService.Instance.AddTool(visionTool);

            return newTool;
        }

        /// <summary>
        /// 모든 도구 실행
        /// </summary>
        private async System.Threading.Tasks.Task RunAllTools()
        {
            if (CurrentImage == null || CurrentImage.Empty())
            {
                StatusMessage = "이미지가 로드되지 않았습니다.";
                return;
            }

            IsRunning = true;
            StatusMessage = "실행 중...";

            try
            {
                var results = await VisionService.Instance.ExecuteAllAsync();

                ExecutionTimeText = $"실행 시간: {VisionService.Instance.TotalExecutionTime:F2}ms";

                // 마지막 결과 이미지 표시
                var lastResult = results.LastOrDefault(r => r.OverlayImage != null);
                if (lastResult?.OverlayImage != null)
                {
                    ResultImage = lastResult.OverlayImage.ToWriteableBitmap();
                    OverlayImage = ResultImage;
                }
                else if (results.LastOrDefault()?.OutputImage != null)
                {
                    ResultImage = results.Last().OutputImage!.ToWriteableBitmap();
                }

                int successCount = results.Count(r => r.Success);
                StatusMessage = $"실행 완료: {successCount}/{results.Count} 성공";
            }
            catch (Exception ex)
            {
                StatusMessage = $"실행 오류: {ex.Message}";
            }
            finally
            {
                IsRunning = false;
            }
        }

        /// <summary>
        /// 선택된 도구만 실행
        /// </summary>
        private void RunSelectedTool()
        {
            if (SelectedTool?.VisionTool == null || CurrentImage == null)
                return;

            IsRunning = true;
            StatusMessage = "실행 중...";

            try
            {
                var result = VisionService.Instance.ExecuteTool(SelectedTool.VisionTool, CurrentImage);

                ExecutionTimeText = $"실행 시간: {SelectedTool.VisionTool.ExecutionTime:F2}ms";

                if (result.OverlayImage != null)
                {
                    ResultImage = result.OverlayImage.ToWriteableBitmap();
                    OverlayImage = ResultImage;
                }
                else if (result.OutputImage != null)
                {
                    ResultImage = result.OutputImage.ToWriteableBitmap();
                }

                StatusMessage = result.Success
                    ? $"실행 완료: {result.Message}"
                    : $"실행 실패: {result.Message}";
            }
            catch (Exception ex)
            {
                StatusMessage = $"실행 오류: {ex.Message}";
            }
            finally
            {
                IsRunning = false;
            }
        }

        /// <summary>
        /// 모든 도구 제거
        /// </summary>
        private void ClearAllTools()
        {
            DroppedTools.Clear();
            VisionService.Instance.ClearTools();
            SelectedTool = null;
            StatusMessage = "모든 도구가 제거되었습니다.";
        }

        /// <summary>
        /// 특정 도구 제거
        /// </summary>
        private void RemoveTool(ToolItem? tool)
        {
            if (tool == null) return;

            if (tool.VisionTool != null)
                VisionService.Instance.RemoveTool(tool.VisionTool);

            DroppedTools.Remove(tool);

            if (SelectedTool == tool)
                SelectedTool = null;
        }

        /// <summary>
        /// 도구 순서 위로 이동
        /// </summary>
        private void MoveToolUp(ToolItem? tool)
        {
            if (tool == null) return;

            int index = DroppedTools.IndexOf(tool);
            if (index > 0)
            {
                DroppedTools.Move(index, index - 1);

                // 실행 큐도 동기화
                if (tool.VisionTool != null)
                {
                    int toolIndex = ExecutionQueue.IndexOf(tool.VisionTool);
                    if (toolIndex > 0)
                        VisionService.Instance.MoveTool(toolIndex, toolIndex - 1);
                }
            }
        }

        /// <summary>
        /// 도구 순서 아래로 이동
        /// </summary>
        private void MoveToolDown(ToolItem? tool)
        {
            if (tool == null) return;

            int index = DroppedTools.IndexOf(tool);
            if (index < DroppedTools.Count - 1)
            {
                DroppedTools.Move(index, index + 1);

                // 실행 큐도 동기화
                if (tool.VisionTool != null)
                {
                    int toolIndex = ExecutionQueue.IndexOf(tool.VisionTool);
                    if (toolIndex < ExecutionQueue.Count - 1)
                        VisionService.Instance.MoveTool(toolIndex, toolIndex + 1);
                }
            }
        }

        /// <summary>
        /// 패턴 학습 (Template Match, Feature Match용)
        /// </summary>
        private void TrainPattern()
        {
            if (CurrentImage == null || CurrentImage.Empty())
            {
                StatusMessage = "이미지가 로드되지 않았습니다.";
                return;
            }

            // ROI가 선택되어 있으면 ROI 영역만 학습, 아니면 전체 이미지
            Mat trainingImage = CurrentImage;

            if (SelectedROI != null)
            {
                var rect = SelectedROI.GetBoundingRect();
                // 이미지 범위 내로 클리핑
                rect = new CvRect(
                    Math.Max(0, rect.X),
                    Math.Max(0, rect.Y),
                    Math.Min(rect.Width, CurrentImage.Width - rect.X),
                    Math.Min(rect.Height, CurrentImage.Height - rect.Y));

                if (rect.Width > 10 && rect.Height > 10)
                {
                    trainingImage = new Mat(CurrentImage, rect);
                    StatusMessage = $"ROI 영역으로 학습 중... ({rect.Width}x{rect.Height})";
                }
            }

            if (SelectedVisionTool is TemplateMatchTool templateTool)
            {
                if (templateTool.TrainPattern(trainingImage))
                {
                    StatusMessage = "Template 학습 완료";
                }
                else
                {
                    StatusMessage = "Template 학습 실패";
                }
            }
            else if (SelectedVisionTool is FeatureMatchTool featureTool)
            {
                if (featureTool.TrainPattern(trainingImage))
                {
                    StatusMessage = "Feature 학습 완료";
                }
                else
                {
                    StatusMessage = "Feature 학습 실패";
                }
            }
        }

        #region ROI Methods

        /// <summary>
        /// ROI 생성 이벤트 처리
        /// </summary>
        public void OnROICreated(ROIShape roi)
        {
            StatusMessage = $"ROI 생성됨: {roi.Name} ({roi.ShapeType})";

            // ROI가 생성되면 현재 선택된 도구에 자동 적용
            if (SelectedVisionTool != null)
            {
                ApplyROIToTool(SelectedVisionTool, roi);
            }
        }

        /// <summary>
        /// ROI 수정 이벤트 처리
        /// </summary>
        public void OnROIModified(ROIShape roi)
        {
            // ROI가 수정되면 연결된 도구의 ROI도 업데이트
            if (SelectedVisionTool != null && SelectedROI == roi)
            {
                ApplyROIToTool(SelectedVisionTool, roi);
            }
        }

        /// <summary>
        /// ROI 선택 변경 이벤트 처리
        /// </summary>
        public void OnROISelectionChanged(ROIShape? roi)
        {
            SelectedROI = roi;

            if (roi != null)
            {
                StatusMessage = $"ROI 선택됨: {roi.Name}";
            }
        }

        /// <summary>
        /// 선택된 ROI를 현재 도구에 적용
        /// </summary>
        private void ApplyROIToSelectedTool()
        {
            if (SelectedVisionTool != null && SelectedROI != null)
            {
                ApplyROIToTool(SelectedVisionTool, SelectedROI);
            }
        }

        /// <summary>
        /// ROI를 특정 도구에 적용
        /// </summary>
        private void ApplyROIToTool(VisionToolBase tool, ROIShape roi)
        {
            var rect = roi.GetBoundingRect();
            tool.ROI = rect;
            tool.UseROI = true;

            StatusMessage = $"ROI 적용됨: {tool.Name} - ({rect.X}, {rect.Y}, {rect.Width}, {rect.Height})";
        }

        /// <summary>
        /// 선택된 도구의 ROI 해제
        /// </summary>
        public void ClearToolROI()
        {
            if (SelectedVisionTool != null)
            {
                SelectedVisionTool.UseROI = false;
                StatusMessage = $"ROI 해제됨: {SelectedVisionTool.Name}";
            }
        }

        #endregion

        /// <summary>
        /// 명령의 CanExecute 상태 갱신
        /// </summary>
        private void NotifyCommandsCanExecuteChanged()
        {
            RunAllCommand?.NotifyCanExecuteChanged();
            RunSelectedCommand?.NotifyCanExecuteChanged();
            TrainPatternCommand?.NotifyCanExecuteChanged();
        }

        #endregion
    }
}
