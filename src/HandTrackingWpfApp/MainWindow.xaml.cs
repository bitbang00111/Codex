using HandTrackingWpfApp.Services;
using HandTrackingWpfApp.Utils;
using OpenCvSharp;
using System.Windows;
using System.Windows.Threading;

namespace HandTrackingWpfApp;

public partial class MainWindow : Window
{
    private readonly CameraCaptureService _cameraService = new();
    private readonly IHandTracker _handTracker = new MediaPipeHandTracker();
    private readonly FrameRenderer _renderer = new();
    private readonly DispatcherTimer _frameTimer = new();

    private int _selectedCameraIndex;

    public MainWindow()
    {
        InitializeComponent();

        Loaded += OnLoaded;
        Closed += OnClosed;

        _frameTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / 60.0);
        _frameTimer.Tick += FrameTimerOnTick;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var cameraIndexes = _cameraService.DiscoverCameraIndexes();
        if (cameraIndexes.Count == 0)
        {
            SetStatus("No camera found. Connect a camera and restart the application.");
            StartButton.IsEnabled = false;
            CameraSelector.IsEnabled = false;
            return;
        }

        foreach (var cameraIndex in cameraIndexes)
        {
            CameraSelector.Items.Add(cameraIndex);
        }

        CameraSelector.SelectedIndex = 0;
        SetStatus("Ready. Select a camera and press Start.");
    }

    private void StartButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (CameraSelector.SelectedItem is not int cameraIndex)
        {
            SetStatus("Select a valid camera.");
            return;
        }

        _selectedCameraIndex = cameraIndex;

        try
        {
            _cameraService.Open(_selectedCameraIndex, width: 1080, height: 720, fps: 60);
            _frameTimer.Start();

            StartButton.IsEnabled = false;
            StopButton.IsEnabled = true;
            CameraSelector.IsEnabled = false;

            SetStatus($"Streaming from camera {_selectedCameraIndex} at 1080x720 / 60 FPS target.");
        }
        catch (Exception ex)
        {
            SetStatus($"Failed to start camera: {ex.Message}");
        }
    }

    private void StopButton_OnClick(object sender, RoutedEventArgs e)
    {
        StopStreaming();
        SetStatus("Stopped.");
    }

    private void FrameTimerOnTick(object? sender, EventArgs e)
    {
        using var frame = new Mat();
        if (!_cameraService.TryRead(frame))
        {
            return;
        }

        var trackingResult = _handTracker.Track(frame);
        using var renderedFrame = _renderer.Render(frame, trackingResult);

        CameraImage.Source = MatBitmapSourceConverter.ToBitmapSource(renderedFrame);
    }

    private void StopStreaming()
    {
        _frameTimer.Stop();
        _cameraService.Dispose();

        StartButton.IsEnabled = true;
        StopButton.IsEnabled = false;
        CameraSelector.IsEnabled = true;
    }

    private void SetStatus(string message)
    {
        StatusText.Text = message;
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _frameTimer.Stop();
        _handTracker.Dispose();
        _cameraService.Dispose();
    }
}
