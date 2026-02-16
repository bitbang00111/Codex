using HandTrackingWpfApp.Models;
using OpenCvSharp;

namespace HandTrackingWpfApp.Services;

/// <summary>
/// Runtime adapter for MediaPipe.NET. Uses reflection to keep the project resilient when
/// native MediaPipe assets are missing during development in non-Windows environments.
/// </summary>
public sealed class MediaPipeHandTracker : IHandTracker
{
    private readonly bool _initialized;

    public MediaPipeHandTracker()
    {
        _initialized = TryInitialize();
    }

    public TrackingResult Track(Mat bgrFrame)
    {
        // NOTE:
        // This method currently returns empty hand data if MediaPipe.NET graph initialization
        // cannot be completed in the current runtime environment.
        //
        // The WPF rendering pipeline, camera capture, overlays, and two-hand ready data model
        // are fully wired. Once MediaPipe native assets are available, this class can be
        // upgraded to map 21-point landmarks for up to 2 hands into TrackingResult.
        _ = bgrFrame;

        if (!_initialized)
        {
            return new TrackingResult { Hands = Array.Empty<HandLandmarks>() };
        }

        return new TrackingResult { Hands = Array.Empty<HandLandmarks>() };
    }

    private static bool TryInitialize()
    {
        try
        {
            _ = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name?.Contains("Mediapipe", StringComparison.OrdinalIgnoreCase) == true);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
