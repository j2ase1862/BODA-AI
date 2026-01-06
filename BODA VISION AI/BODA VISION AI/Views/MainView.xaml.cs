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
                var tool = e.Data.GetData("Object") as ToolItem;

                // 수정: 안전한 캐스팅 (as 사용)
                if (this.DataContext is MainViewModel vm && tool != null)
                {
                    vm.DroppedTools.Add(tool);
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
    }
}