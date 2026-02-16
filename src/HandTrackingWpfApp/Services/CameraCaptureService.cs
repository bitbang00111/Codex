using OpenCvSharp;

namespace HandTrackingWpfApp.Services;

public sealed class CameraCaptureService : IDisposable
{
    private VideoCapture? _capture;

    public bool IsOpen => _capture?.IsOpened() == true;

    public IReadOnlyList<int> DiscoverCameraIndexes(int maxToProbe = 8)
    {
        var indexes = new List<int>();

        for (var i = 0; i < maxToProbe; i++)
        {
            using var probe = new VideoCapture(i, VideoCaptureAPIs.DSHOW);
            if (probe.IsOpened())
            {
                indexes.Add(i);
            }
        }

        return indexes;
    }

    public void Open(int index, int width, int height, int fps)
    {
        DisposeCapture();

        _capture = new VideoCapture(index, VideoCaptureAPIs.DSHOW);
        if (!_capture.IsOpened())
        {
            throw new InvalidOperationException($"Cannot open camera index {index}.");
        }

        _capture.Set(VideoCaptureProperties.FrameWidth, width);
        _capture.Set(VideoCaptureProperties.FrameHeight, height);
        _capture.Set(VideoCaptureProperties.Fps, fps);
    }

    public bool TryRead(Mat frame)
    {
        if (_capture is null || !_capture.IsOpened())
        {
            return false;
        }

        return _capture.Read(frame) && !frame.Empty();
    }

    public void Dispose()
    {
        DisposeCapture();
        GC.SuppressFinalize(this);
    }

    private void DisposeCapture()
    {
        _capture?.Release();
        _capture?.Dispose();
        _capture = null;
    }
}
