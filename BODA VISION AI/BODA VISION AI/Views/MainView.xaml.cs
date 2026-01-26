using BODA_VISION_AI.Models;
using BODA_VISION_AI.ViewModels;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace BODA_VISION_AI
{
    /// <summary>
    /// MainView.xaml에 대한 상호 작용 논리
    /// </summary>
    public partial class MainView : System.Windows.Window
    {
        private bool _isDragging = false;
        private System.Windows.Point _mouseOffset;

        public MainView()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }

        #region ImageCanvas ROI Events

        /// <summary>
        /// ROI 생성 이벤트 처리
        /// </summary>
        private void ImageCanvas_ROICreated(object? sender, ROIShape roi)
        {
            var vm = DataContext as MainViewModel;
            vm?.OnROICreated(roi);
        }

        /// <summary>
        /// ROI 수정 이벤트 처리
        /// </summary>
        private void ImageCanvas_ROIModified(object? sender, ROIShape roi)
        {
            var vm = DataContext as MainViewModel;
            vm?.OnROIModified(roi);
        }

        /// <summary>
        /// ROI 선택 변경 이벤트 처리
        /// </summary>
        private void ImageCanvas_ROISelectionChanged(object? sender, ROIShape? roi)
        {
            var vm = DataContext as MainViewModel;
            vm?.OnROISelectionChanged(roi);
        }

        #endregion

        /// <summary>
        /// 도구 드롭 처리
        /// </summary>
        private void Sidebar2_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("Object"))
            {
                var sourceTool = e.Data.GetData("Object") as ToolItem;
                var vm = DataContext as MainViewModel;
                var dropContainer = sender as UIElement;

                if (vm != null && sourceTool != null && dropContainer != null)
                {
                    System.Windows.Point position = e.GetPosition(dropContainer);

                    // ViewModel의 CreateDroppedTool 메서드를 사용하여 새 도구 생성
                    var newTool = vm.CreateDroppedTool(sourceTool, position.X, position.Y);

                    if (newTool != null)
                    {
                        // 새로 생성된 도구를 선택
                        vm.SelectedTool = newTool;
                    }
                }
            }
        }

        /// <summary>
        /// 도구 팔레트에서 드래그 시작
        /// </summary>
        private void ToolItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement fe && fe.DataContext is ToolItem tool)
            {
                DataObject data = new DataObject();
                data.SetData("Object", tool);
                DragDrop.DoDragDrop(fe, data, DragDropEffects.Copy);
            }
        }

        /// <summary>
        /// 워크스페이스 아이템 드래그 시작
        /// </summary>
        private void Item_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                _isDragging = true;
                _mouseOffset = e.GetPosition(element);
                element.CaptureMouse();

                // 클릭한 도구를 선택
                if (element.DataContext is ToolItem tool)
                {
                    var vm = DataContext as MainViewModel;
                    if (vm != null)
                    {
                        // 기존 선택 해제
                        foreach (var t in vm.DroppedTools)
                            t.IsSelected = false;

                        // 현재 도구 선택
                        tool.IsSelected = true;
                        vm.SelectedTool = tool;
                    }
                }
            }
        }

        /// <summary>
        /// 워크스페이스 아이템 드래그 이동
        /// </summary>
        private void Item_MouseMove(object sender, MouseEventArgs e)
        {
            var element = sender as FrameworkElement;

            if (_isDragging && element != null && element.DataContext is ToolItem tool)
            {
                var canvas = FindParent<Canvas>(element);
                if (canvas == null) return;

                System.Windows.Point currentPoint = e.GetPosition(canvas);
                tool.X = currentPoint.X - _mouseOffset.X;
                tool.Y = currentPoint.Y - _mouseOffset.Y;
            }
        }

        /// <summary>
        /// 워크스페이스 아이템 드래그 종료
        /// </summary>
        private void Item_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                _isDragging = false;
                element.ReleaseMouseCapture();
            }
        }

        /// <summary>
        /// 워크스페이스 아이템 우클릭 (선택)
        /// </summary>
        private void Item_MouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element?.DataContext is ToolItem tool)
            {
                var vm = DataContext as MainViewModel;
                if (vm != null)
                {
                    // 기존 선택 해제
                    foreach (var t in vm.DroppedTools)
                        t.IsSelected = false;

                    // 현재 도구 선택
                    tool.IsSelected = true;
                    vm.SelectedTool = tool;
                }
            }
        }

        /// <summary>
        /// 부모 컨트롤 찾기 헬퍼 메서드
        /// </summary>
        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }
    }
}
