using HandTrackingWpfApp.Models;
using OpenCvSharp;

namespace HandTrackingWpfApp.Services;

public interface IHandTracker : IDisposable
{
    TrackingResult Track(Mat bgrFrame);
}
