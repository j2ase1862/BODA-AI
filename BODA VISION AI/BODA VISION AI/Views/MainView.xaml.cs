using BODA_VISION_AI.ViewModels;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace BODA_VISION_AI
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainView : Window
    {
        public MainView()
        {
            InitializeComponent();

            DataContext = new MainViewModel();
        }

        private void Sidebar2_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent("Object"))
            {
                // 1. 드래그된 원본 데이터 가져오기
                var sourceTool = e.Data.GetData("Object") as ToolItem;
                var vm = this.DataContext as MainViewModel;

                // sender는 이벤트를 발생시킨 ItemsControl (Canvas를 포함한 컨테이너)
                var dropContainer = sender as UIElement;

                if (vm != null && sourceTool != null && dropContainer != null)
                {
                    // 2. 드롭된 위치(좌표) 계산
                    Point position = e.GetPosition(dropContainer);

                    // 3. *중요* 새로운 아이템 인스턴스 생성 (복제 + 좌표 설정)
                    var newTool = new ToolItem
                    {
                        Name = sourceTool.Name,       // 이름 복사
                        ToolType = sourceTool.ToolType, // 타입 복사
                        X = position.X,               // X 좌표 설정
                        Y = position.Y                // Y 좌표 설정
                    };

                    // 4. 컬렉션에 추가 -> UI에 자동 반영됨
                    vm.DroppedTools.Add(newTool);
                }
            }
        }

        private void ToolItem_PreviewMouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement fe && fe.DataContext is ToolItem tool)
            {
                // 드래그 데이터 패키지 생성
                DataObject data = new DataObject();
                data.SetData("Object", tool);

                // 드래그 시작
                DragDrop.DoDragDrop(fe, data, DragDropEffects.Copy);
            }
        }

        // 이동 로직을 위한 변수들
        private bool _isDragging = false;
        private Point _mouseOffset; // 아이템 내에서 마우스가 클릭된 상대 위치

        private void Item_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                // 1. 드래그 시작 상태로 변경
                _isDragging = true;

                // 2. 클릭한 지점과 아이템 좌상단 사이의 오프셋 저장
                _mouseOffset = e.GetPosition(element);

                // 3. 마우스 이벤트를 이 컨트롤이 독점하도록 캡처 (화면 밖으로 나가도 추적)
                element.CaptureMouse();
            }
        }

        private void Item_MouseMove(object sender, MouseEventArgs e)
        {
            var element = sender as FrameworkElement;

            // 드래그 중이고, 데이터 컨텍스트가 ToolItem인 경우
            if (_isDragging && element != null && element.DataContext is ToolItem tool)
            {
                // 1. Canvas(Sidebar2) 기준의 마우스 좌표를 구함
                // 주의: Sidebar2 이름을 x:Name="Sidebar2"로 지정해야 접근 가능
                // 만약 x:Name이 없다면 element의 부모를 찾아 Canvas를 구해야 함
                var canvas = FindParent<Canvas>(element);
                if (canvas == null) return;

                Point currentPoint = e.GetPosition(canvas);

                // 2. 오프셋을 고려하여 아이템의 새로운 X, Y 설정
                // (마우스 현재 위치 - 클릭했던 아이템 내부 위치)
                tool.X = currentPoint.X - _mouseOffset.X;
                tool.Y = currentPoint.Y - _mouseOffset.Y;
            }
        }

        private void Item_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element != null)
            {
                // 1. 드래그 종료
                _isDragging = false;

                // 2. 마우스 캡처 해제
                element.ReleaseMouseCapture();
            }
        }

        // 부모 컨트롤 찾기 헬퍼 메서드
        private static T FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            if (parentObject is T parent) return parent;
            return FindParent<T>(parentObject);
        }
    }
}