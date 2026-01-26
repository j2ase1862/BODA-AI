# BODA VISION AI

OpenCvSharp 기반 머신 비전 시스템 - Cognex VisionPro 대체

## Project Overview

- **Framework**: .NET 8.0 (Windows)
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Architecture**: MVVM (Model-View-ViewModel)
- **Vision Library**: OpenCvSharp4
- **Namespace**: `BODA_VISION_AI`

## Tech Stack

- **CommunityToolkit.Mvvm** (8.4.0) - MVVM implementation
- **OpenCvSharp4** (4.11.0) - OpenCV .NET binding
- **OpenCvSharp4.runtime.win** - Windows runtime
- **OpenCvSharp4.WpfExtensions** - WPF integration

## Project Structure

```
BODA VISION AI/
├── App.xaml / App.xaml.cs
├── AssemblyInfo.cs
├── BODA VISION AI.csproj
├── Models/
│   ├── VisionToolBase.cs       # 비전 도구 기본 클래스
│   └── ToolItem.cs             # 도구 아이템 모델
├── Services/
│   └── VisionService.cs        # 비전 처리 서비스 (싱글톤)
├── Styles/
│   └── MenuStyles.xaml         # 다크 테마 스타일
├── ViewModels/
│   └── MainViewModel.cs        # 메인 뷰모델
├── Views/
│   ├── MainView.xaml           # 메인 윈도우
│   ├── MainView.xaml.cs
│   └── ToolSettings/
│       ├── ToolSettingsView.xaml     # 도구 설정 패널
│       └── ToolSettingsView.xaml.cs
└── VisionTools/
    ├── ImageProcessing/
    │   ├── GrayscaleTool.cs    # Grayscale 변환
    │   ├── BlurTool.cs         # Blur (Gaussian, Median, etc.)
    │   ├── ThresholdTool.cs    # 이진화 (Otsu, Adaptive)
    │   ├── EdgeDetectionTool.cs # Edge 검출 (Canny, Sobel)
    │   ├── MorphologyTool.cs   # 형태학적 처리
    │   └── HistogramTool.cs    # 히스토그램 분석/CLAHE
    ├── PatternMatching/
    │   ├── TemplateMatchTool.cs # 템플릿 매칭 (Multi-Scale/Angle)
    │   └── FeatureMatchTool.cs  # Feature 매칭 (ORB, AKAZE)
    ├── BlobAnalysis/
    │   └── BlobTool.cs          # Blob 분석 (Contour 기반)
    └── Measurement/
        ├── CaliperTool.cs       # Caliper (Edge 검출)
        ├── LineFitTool.cs       # 직선 피팅 (RANSAC)
        └── CircleFitTool.cs     # 원 피팅 (RANSAC)
```

## Vision Tools (Cognex VisionPro 대체)

### Image Processing
| Tool | 설명 | Cognex 대체 |
|------|------|-------------|
| Grayscale | 흑백 변환 | CogImageConvertTool |
| Blur | 블러 처리 (Gaussian, Median, Bilateral) | CogImageFilterTool |
| Threshold | 이진화 (Fixed, Otsu, Adaptive) | CogBlobTool Threshold |
| Edge Detection | 에지 검출 (Canny, Sobel, Laplacian) | CogSobelEdgeTool |
| Morphology | 형태학적 처리 (Dilate, Erode, Open, Close) | CogBlobTool Morphology |
| Histogram | 히스토그램 분석/평활화/CLAHE | CogHistogramTool |

### Pattern Matching
| Tool | 설명 | Cognex 대체 |
|------|------|-------------|
| Template Match | 템플릿 매칭 (Multi-Scale, Multi-Angle) | CogPMAlignTool |
| Feature Match | Feature 기반 매칭 (ORB, AKAZE, BRISK) | CogPatMaxTool |

### Blob Analysis
| Tool | 설명 | Cognex 대체 |
|------|------|-------------|
| Blob | Blob 분석 (Area, Circularity, Convexity 등) | CogBlobTool |

### Measurement
| Tool | 설명 | Cognex 대체 |
|------|------|-------------|
| Caliper | Edge 거리 측정 | CogCaliperTool |
| Line Fit | 직선 검출 및 피팅 | CogFindLineTool |
| Circle Fit | 원 검출 및 피팅 | CogFindCircleTool |

## UI Layout

```
┌─────────────────────────────────────────────────────────────┐
│ Menu Bar                                                    │
├─────────────────────────────────────────────────────────────┤
│ Toolbar: [Open Image] [Run All] [Run Selected] [Train] ... │
├───────────┬──────────────────────────────┬──────────────────┤
│ Tool      │ Tool Workspace               │ Tool Settings    │
│ Palette   │ (Drag & Drop Area)           │ (Selected Tool)  │
│           ├──────────────┬───────────────┤                  │
│ - Image   │ Original     │ Result        │ - Parameters     │
│   Process │ Image        │ Image         │ - ROI            │
│ - Pattern │              │               │ - Options        │
│ - Blob    │              │               │                  │
│ - Measure │              │               │                  │
├───────────┴──────────────┴───────────────┴──────────────────┤
│ Status Bar: Ready | Execution Time: 0.00ms | Tools: 0      │
└─────────────────────────────────────────────────────────────┘
```

## Usage

1. **이미지 로드**: File > Open Image File 또는 [Open Image] 버튼
2. **도구 추가**: 왼쪽 Tool Palette에서 도구를 Workspace로 드래그
3. **도구 설정**: 워크스페이스에서 도구 클릭 → 오른쪽 패널에서 파라미터 설정
4. **실행**: [Run All] 전체 실행 또는 [Run Selected] 선택 도구 실행
5. **패턴 학습**: Template Match 또는 Feature Match 도구 선택 후 [Train Pattern]

## Key Features

- **Drag & Drop 도구 배치**: Cognex QuickBuild 스타일의 직관적인 UI
- **실시간 파라미터 조정**: 설정 변경 즉시 반영
- **다양한 비전 알고리즘**: OpenCV 기반의 산업용 비전 도구
- **MVVM 아키텍처**: 확장 가능한 구조

## Build & Run

```bash
cd "BODA VISION AI/BODA VISION AI"
dotnet build
dotnet run
```

## Development Notes

- 모든 비전 도구는 `VisionToolBase`를 상속
- `VisionService`는 싱글톤으로 도구 실행 관리
- `VisionResult`에 처리 결과와 오버레이 그래픽 포함
- ROI 기능 모든 도구에서 사용 가능
