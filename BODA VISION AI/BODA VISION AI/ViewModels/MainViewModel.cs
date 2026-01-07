using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace BODA_VISION_AI.ViewModels
{
    // ObservableObject 상속 추가
    public class ToolItem : ObservableObject
    {
        public string Name { get; set; }
        public string ToolType { get; set; }

        private double _x;
        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value); // 값이 바뀌면 UI에 알림
        }

        private double _y;
        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value); // 값이 바뀌면 UI에 알림
        }
    }


    // 트리 구조를 위한 카테고리 모델
    public class ToolCategory
    {
        public string CategoryName { get; set; }
        public ObservableCollection<ToolItem> Tools { get; set; } = new();
    }

    public class MainViewModel
    {
        // Sidebar1에 표시될 트리 데이터
        public ObservableCollection<ToolCategory> ToolTree { get; } = new();

        // Sidebar2에 생성된 컨트롤들의 목록
        public ObservableCollection<ToolItem> DroppedTools { get; } = new();


        #region Fields
        private readonly string _appName = "BODA VISION AI";
        private readonly string _appVersion = "1.0.0";
        #endregion


        #region Properties
        public string AppName => _appName;
        public string AppVersion => _appVersion;
        #endregion


        #region Commands
        public ICommand CloseCommand { get; }
        #endregion


        #region Constructor
        public MainViewModel()
        {
            // 샘플 데이터 구성
            var proc = new ToolCategory { CategoryName = "Image Processing" };
            proc.Tools.Add(new ToolItem { Name = "Grayscale", ToolType = "GrayView" });
            proc.Tools.Add(new ToolItem { Name = "Blur", ToolType = "BlurView" });

            ToolTree.Add(proc);

            CloseCommand = new RelayCommand(CloseApplication);
        }
        #endregion


        #region Methods
        private void CloseApplication()
        {
            System.Windows.Application.Current.Shutdown();
        }
        #endregion
    }
}
