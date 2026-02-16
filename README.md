# HandTrackingWpfApp

Desktop WPF application (C#) for Windows 11 x64 that captures webcam frames with OpenCvSharp and is structured to process up to 2 hands using MediaPipe.NET.

## Features

- WPF desktop app (no web components).
- Camera selector.
- `Start` / `Stop` controls.
- 1080x720 / 60 FPS target capture settings.
- Rendering pipeline ready for:
  - camera video stream,
  - semi-transparent hand silhouette overlay,
  - hand labels.
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

## MediaPipe.NET notes

This repository already references MediaPipe.NET and OpenCvSharp.

To enable production-grade two-hand landmark tracking, make sure the required MediaPipe native assets and model files for the Hands pipeline are available in your runtime environment (depends on the MediaPipe.NET package/version you use).

`MediaPipeHandTracker` is intentionally isolated so you can replace/extend the internals with your preferred MediaPipe graph/model setup without touching UI/camera/rendering code.

## Troubleshooting

- **No camera in selector**
  - Verify webcam is connected.
  - Check Windows Camera privacy permissions.

- **Camera starts but low frame rate**
  - Lower resolution/FPS in `MainWindow.xaml.cs` if camera hardware does not support 1080x720@60.

- **MediaPipe runtime errors**
  - Verify matching x64 native dependencies.
  - Verify model file paths and graph configuration for your package version.

