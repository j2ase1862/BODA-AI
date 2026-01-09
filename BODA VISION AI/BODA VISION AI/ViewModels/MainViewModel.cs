using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;
using OpenCvSharp.WpfExtensions; // 이 네임스페이스가 필수입니다.
using Microsoft.Win32;
using System.Windows.Media;

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

    public class MainViewModel : ObservableObject
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

        // [중요 2] View와 바인딩 될 이미지 프로퍼티 추가
        private ImageSource _displayImage;
        public ImageSource DisplayImage
        {
            get => _displayImage;
            set => SetProperty(ref _displayImage, value); // 값이 바뀌면 View에 자동 반영
        }
        #endregion


        #region Commands
        public ICommand CloseCommand { get; }
        public ICommand OpenImageFileCommand { get; }
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
            OpenImageFileCommand = new RelayCommand(OpenImageFile);
        }
        #endregion


        #region Methods
        private void CloseApplication()
        {
            System.Windows.Application.Current.Shutdown();
        }

        private void OpenImageFile()
        {
            OpenFileDialog dlg = new OpenFileDialog();
            dlg.Title = "이미지 파일 열기";
            dlg.Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff|All Files|*.*";

            // 다이얼로그 보여주기 (.NET Core/5+ 이후 ShowDialog 반환타입은 bool?)
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    // 1. OpenCvSharp으로 이미지 읽기 (메모리 관리를 위해 using 사용)
                    using (var mat = Cv2.ImRead(dlg.FileName))
                    {
                        if (!mat.Empty())
                        {
                            // 2. Mat -> WriteableBitmap 변환 후 프로퍼티에 할당
                            // ToWriteableBitmap()은 데이터를 복사하므로 mat은 Dispose 되어도 됨
                            DisplayImage = mat.ToWriteableBitmap();
                        }
                    }
                }
                catch (Exception ex)
                {
                    // 에러 처리 (필요하다면 MessageBox 등으로 알림)
                    System.Diagnostics.Debug.WriteLine($"이미지 로드 실패: {ex.Message}");
                }
            }
        }

        #endregion
    }
}
