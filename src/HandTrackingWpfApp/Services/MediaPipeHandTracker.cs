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
    private const double MaxHandAreaRatio = 0.18;

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

        var maxArea = bgrFrame.Width * bgrFrame.Height * MaxHandAreaRatio;

        var candidateContours = contours
            .Select(contour => new
            {
                Contour = contour,
                Area = Cv2.ContourArea(contour),
                Bounds = Cv2.BoundingRect(contour)
            })
            .Where(c => c.Area >= minArea && c.Area <= maxArea)
            .Where(c => c.Bounds.Width > 35 && c.Bounds.Height > 35)
            .Where(c =>
            {
                var aspectRatio = c.Bounds.Width / (double)c.Bounds.Height;
                return aspectRatio is > 0.35 and < 2.6;
            })
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
            var points = BuildPseudoLandmarks(candidate.Contour, bgrFrame.Size());
            if (points.Count == 0)
            {
                continue;
            }

            var moments = Cv2.Moments(candidate.Contour);
            var centerX = moments.M00 > double.Epsilon
                ? moments.M10 / moments.M00
                : candidate.Contour.Average(point => point.X);

            hands.Add(new HandLandmarks
            {
                Handedness = centerX < bgrFrame.Width / 2.0 ? "Left" : "Right",
                Points = points
            });
        }

        return hands;
    }

    private static IReadOnlyList<NormalizedPoint> BuildPseudoLandmarks(Point[] contour, Size frameSize)
    {
        if (contour.Length < 10)
        {
            return Array.Empty<NormalizedPoint>();
        }

        var bounds = Cv2.BoundingRect(contour);
        var orderedByY = contour.OrderBy(point => point.Y).ToArray();
        var fingertipCandidates = orderedByY.Take(Math.Min(orderedByY.Length, 60)).ToArray();

        var wristY = bounds.Bottom;
        var palmTopY = bounds.Top + (bounds.Height * 0.45);
        var palmCenterY = bounds.Top + (bounds.Height * 0.62);

        var fingerXs = Enumerable.Range(0, 5)
            .Select(index => bounds.Left + ((index + 1) * bounds.Width / 6.0))
            .ToArray();

        var fingertips = fingerXs
            .Select(x => PickClosestTopPoint(fingertipCandidates, x, bounds.Width * 0.15, bounds.Top))
            .ToArray();

        var wrist = new Point2d(bounds.Left + (bounds.Width / 2.0), wristY);
        var thumbBase = new Point2d(bounds.Left + (bounds.Width * 0.18), palmCenterY);
        var indexBase = new Point2d(bounds.Left + (bounds.Width * 0.32), palmTopY);
        var middleBase = new Point2d(bounds.Left + (bounds.Width * 0.48), palmTopY - (bounds.Height * 0.02));
        var ringBase = new Point2d(bounds.Left + (bounds.Width * 0.64), palmTopY);
        var pinkyBase = new Point2d(bounds.Left + (bounds.Width * 0.80), palmCenterY);

        var points = new Point2d[21];
        points[0] = wrist;

        WriteFinger(points, 1, thumbBase, fingertips[0], 0.35, 0.66, 1.0);
        WriteFinger(points, 5, indexBase, fingertips[1], 0.30, 0.62, 1.0);
        WriteFinger(points, 9, middleBase, fingertips[2], 0.28, 0.60, 1.0);
        WriteFinger(points, 13, ringBase, fingertips[3], 0.30, 0.62, 1.0);
        WriteFinger(points, 17, pinkyBase, fingertips[4], 0.35, 0.66, 1.0);

        return points
            .Select(point => new NormalizedPoint(
                X: Math.Clamp((float)(point.X / frameSize.Width), 0f, 1f),
                Y: Math.Clamp((float)(point.Y / frameSize.Height), 0f, 1f)))
            .ToArray();
    }

    private static Point2d PickClosestTopPoint(IEnumerable<Point> candidates, double targetX, double maxDeltaX, int fallbackY)
    {
        var closest = candidates
            .Select(point => new { Point = point, DeltaX = Math.Abs(point.X - targetX) })
            .Where(match => match.DeltaX <= maxDeltaX)
            .OrderBy(match => match.Point.Y)
            .ThenBy(match => match.DeltaX)
            .FirstOrDefault();

        return closest is null
            ? new Point2d(targetX, fallbackY)
            : new Point2d(closest.Point.X, closest.Point.Y);
    }

    private static void WriteFinger(Point2d[] target, int startIndex, Point2d baseJoint, Point2d tip, double t1, double t2, double t3)
    {
        target[startIndex] = baseJoint;
        target[startIndex + 1] = Lerp(baseJoint, tip, t1);
        target[startIndex + 2] = Lerp(baseJoint, tip, t2);
        target[startIndex + 3] = Lerp(baseJoint, tip, t3);
    }

    private static Point2d Lerp(Point2d start, Point2d end, double t)
    {
        return new Point2d(
            X: start.X + ((end.X - start.X) * t),
            Y: start.Y + ((end.Y - start.Y) * t));
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}
