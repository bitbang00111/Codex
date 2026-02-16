# HandTrackingWpfApp

Desktop WPF application (C#) for Windows 11 x64 that captures webcam frames with OpenCvSharp and performs two-hand tracking with MediaPipe.NET (with OpenCV fallback tracking if MediaPipe native assets are missing).

## Features

- WPF desktop app (no web components).
- Camera selector.
- `Start` / `Stop` controls.
- 1080x720 / 60 FPS target capture settings.
- Real-time tracking pipeline for up to **2 hands**.
- Rendering pipeline:
  - camera video stream,
  - semi-transparent hand silhouette overlay,
  - left/right hand labels.
- English UI.
- Basic no-camera safety handling.

## Tech stack

- .NET 8 (LTS)
- C#
- WPF
- OpenCvSharp
- MediaPipe.NET

## Project structure

- `HandTrackingWpfApp.sln`
- `src/HandTrackingWpfApp/`
  - `MainWindow.xaml` / `MainWindow.xaml.cs`
  - `Services/CameraCaptureService.cs`
  - `Services/MediaPipeHandTracker.cs`
  - `Services/FrameRenderer.cs`
  - `Models/HandModels.cs`
  - `Utils/MatBitmapSourceConverter.cs`

## Build prerequisites (Windows 11 x64)

1. Install **.NET SDK 8.0+**.
2. Install **Visual Studio 2022** with:
   - .NET desktop development workload,
   - Windows 11 SDK.
3. Ensure webcam access is enabled in Windows Privacy settings.
4. Restore NuGet packages.

## Build and run

```powershell
git clone <your-repo-url>
cd HandTrackingWpfApp
dotnet restore HandTrackingWpfApp.sln
dotnet build HandTrackingWpfApp.sln -c Release
dotnet run --project .\src\HandTrackingWpfApp\HandTrackingWpfApp.csproj
```

## MediaPipe.NET integration behavior

- The app tries to initialize and execute a MediaPipe.NET hand runner through reflection to tolerate package/API version differences.
- If MediaPipe runtime assets/models are not available at runtime, the app automatically falls back to an OpenCV-based contour tracker so hand movement overlay still works.

## Troubleshooting

- **No camera in selector**
  - Verify webcam is connected.
  - Check Windows Camera privacy permissions.

- **Camera starts but low frame rate**
  - Lower resolution/FPS in `MainWindow.xaml.cs` if camera hardware does not support 1080x720@60.

- **MediaPipe runtime errors or no landmarks from MediaPipe**
  - Verify matching x64 native dependencies.
  - Verify model file paths and graph configuration for your package version.
  - Temporary fallback tracking remains enabled to keep motion overlay operational.
