# BODA VISION AI

A WPF desktop application for image processing tools with drag-and-drop functionality.

## Project Overview

- **Framework**: .NET 8.0 (Windows)
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Architecture**: MVVM (Model-View-ViewModel)
- **Namespace**: `BODA_VISION_AI`

## Tech Stack

- **CommunityToolkit.Mvvm** (8.4.0) - MVVM implementation with RelayCommand support

## Project Structure

```
BODA VISION AI/
├── App.xaml              # Application entry point and resource dictionaries
├── App.xaml.cs
├── AssemblyInfo.cs
├── BODA VISION AI.csproj
├── Styles/
│   └── MenuStyles.xaml   # Custom menu and UI styles (dark theme)
├── ViewModels/
│   └── MainViewModel.cs  # Main view model with tool tree and dropped tools
└── Views/
    └── MainView.xaml     # Main window with sidebar and drag-drop area
    └── MainView.xaml.cs  # Code-behind for drag-drop handlers
```

## Key Components

### MainViewModel
- `ToolTree`: ObservableCollection of `ToolCategory` for the sidebar tree view
- `DroppedTools`: ObservableCollection of `ToolItem` for dropped tool instances
- `CloseCommand`: Application close command

### Models
- `ToolItem`: Represents a tool with `Name` and `ToolType` properties
- `ToolCategory`: Groups tools by category with `CategoryName` and `Tools` collection

### UI Layout
- **Left Sidebar (Column 0)**: TreeView with draggable tool items
- **Main Area (Column 1)**: Drop zone for tool instances (WrapPanel)
- **Right Area (Column 2)**: Reserved for additional content

## Build & Run

```bash
dotnet build
dotnet run
```

## Styling

The app uses a dark theme with custom styles defined in `Styles/MenuStyles.xaml`:
- Dark background (#2D2D30, #1E1E1E)
- White/LightGray text
- Gold accent color (#FFD700)

## Development Notes

- Comments and code are written in Korean
- The application runs in borderless fullscreen mode (WindowStyle="None")
- Drag-and-drop is implemented in code-behind (`MainView.xaml.cs`)
- Use `RelayCommand` from CommunityToolkit.Mvvm for commands
