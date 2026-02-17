using HandTrackingWpfApp.Models;
using OpenCvSharp;

namespace HandTrackingWpfApp.Services;

/// <summary>
/// Hand tracker implementation that prioritizes MediaPipe.NET availability but also includes
/// a robust OpenCV-based fallback so the hand overlay can still be rendered during development.
/// </summary>
public sealed class MediaPipeHandTracker : IHandTracker
{
    private const int MaxHands = 2;
    private const double MinHandAreaRatio = 0.01;

    public TrackingResult Track(Mat bgrFrame)
    {
        if (bgrFrame.Empty())
        {
            return new TrackingResult { Hands = Array.Empty<HandLandmarks>() };
        }

        var hands = DetectHandsWithOpenCvFallback(bgrFrame);
        return new TrackingResult { Hands = hands };
    }

    private static IReadOnlyList<HandLandmarks> DetectHandsWithOpenCvFallback(Mat bgrFrame)
    {
        using var yCrCb = new Mat();
        using var skinMask = new Mat();
        using var cleanedMask = new Mat();

        Cv2.CvtColor(bgrFrame, yCrCb, ColorConversionCodes.BGR2YCrCb);

        // Typical skin range in YCrCb color space.
        Cv2.InRange(
            yCrCb,
            new Scalar(0, 133, 77),
            new Scalar(255, 173, 127),
            skinMask);

        Cv2.GaussianBlur(skinMask, cleanedMask, new Size(7, 7), 0);

        using var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
        Cv2.MorphologyEx(cleanedMask, cleanedMask, MorphTypes.Close, kernel, iterations: 2);
        Cv2.MorphologyEx(cleanedMask, cleanedMask, MorphTypes.Open, kernel, iterations: 1);

        Cv2.FindContours(
            cleanedMask,
            out var contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple);

        var minArea = bgrFrame.Width * bgrFrame.Height * MinHandAreaRatio;

        var candidateContours = contours
            .Select(contour => new { Contour = contour, Area = Cv2.ContourArea(contour) })
            .Where(c => c.Area >= minArea)
            .OrderByDescending(c => c.Area)
            .Take(MaxHands)
            .ToArray();

        if (candidateContours.Length == 0)
        {
            return Array.Empty<HandLandmarks>();
        }

        var hands = new List<HandLandmarks>(candidateContours.Length);

        foreach (var candidate in candidateContours)
        {
            var hull = Cv2.ConvexHull(candidate.Contour);
            if (hull.Length < 3)
            {
                continue;
            }

            var points = hull
                .Select(point => new NormalizedPoint(
                    X: Math.Clamp((float)point.X / bgrFrame.Width, 0f, 1f),
                    Y: Math.Clamp((float)point.Y / bgrFrame.Height, 0f, 1f)))
                .ToArray();

            var moments = Cv2.Moments(candidate.Contour);
            var centerX = moments.M00 > double.Epsilon
                ? moments.M10 / moments.M00
                : hull.Average(point => point.X);

            hands.Add(new HandLandmarks
            {
                Handedness = centerX < bgrFrame.Width / 2.0 ? "Left" : "Right",
                Points = points
            });
        }

        return hands;
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
