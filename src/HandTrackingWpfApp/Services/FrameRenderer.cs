using HandTrackingWpfApp.Models;
using OpenCvSharp;

namespace HandTrackingWpfApp.Services;

public sealed class FrameRenderer
{
    private static readonly int[] PalmContourIndices = [0, 1, 2, 5, 9, 13, 17];

    private static readonly int[][] FingerChains =
    [
        [1, 2, 3, 4],
        [5, 6, 7, 8],
        [9, 10, 11, 12],
        [13, 14, 15, 16],
        [17, 18, 19, 20]
    ];

    private static readonly Scalar BodyColor = new(175, 155, 142); // #8E9BAF in BGR
    private static readonly Scalar HaloColor = new(232, 212, 199); // #C7D4E8 in BGR
    private static readonly Scalar LandmarkColor = new(245, 245, 245);

    private readonly Dictionary<string, SmoothedHandState> _smoothedHands = new(StringComparer.OrdinalIgnoreCase);
    private readonly GhostRenderSettings _settings;

    public FrameRenderer(GhostRenderSettings? settings = null)
    {
        _settings = settings ?? GhostRenderSettings.Default;
    }

    public Mat Render(Mat sourceBgr, TrackingResult trackingResult)
    {
        if (!_settings.EnableGhostStyle)
        {
            return sourceBgr.Clone();
        }

        var composed = sourceBgr.Clone();
        using var bodyMask = new Mat(sourceBgr.Size(), MatType.CV_8UC1, Scalar.Black);

        foreach (var hand in trackingResult.Hands)
        {
            var smoothedPoints = SmoothHand(hand);
            var framePoints = ToFramePoints(smoothedPoints, sourceBgr.Size());

            using var handMask = new Mat(sourceBgr.Size(), MatType.CV_8UC1, Scalar.Black);
            DrawPalm(handMask, framePoints);
            DrawFingers(handMask, framePoints, sourceBgr.Size());

            using var morphologyKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
            Cv2.MorphologyEx(handMask, handMask, MorphTypes.Close, morphologyKernel);
            Cv2.MorphologyEx(handMask, handMask, MorphTypes.Open, morphologyKernel);

            Cv2.Max(bodyMask, handMask, bodyMask);

            if (_settings.ShowLandmarks)
            {
                DrawLandmarks(composed, framePoints, hand.Handedness);
            }
        }

        using var bodySoftMask = CreateBodySoftMask(bodyMask, _settings.BlurSigma);
        using var haloSoftMask = CreateHaloSoftMask(bodyMask, _settings.BlurSigma);

        BlendTintByMask(composed, haloSoftMask, HaloColor, _settings.HaloOpacity);
        BlendTintByMask(composed, bodySoftMask, BodyColor, _settings.BodyOpacity);

        CleanupStaleHands();
        return composed;
    }

    private static Mat CreateBodySoftMask(Mat bodyMask, double sigma)
    {
        var bodySoftMask = new Mat();
        Cv2.GaussianBlur(bodyMask, bodySoftMask, KernelFromSigma(sigma), sigma);
        Cv2.Threshold(bodySoftMask, bodySoftMask, 10, 255, ThresholdTypes.Tozero);
        return bodySoftMask;
    }

    private static Mat CreateHaloSoftMask(Mat bodyMask, double sigma)
    {
        using var dilated = new Mat();
        using var ringMask = new Mat();
        using var ringKernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(17, 17));

        Cv2.Dilate(bodyMask, dilated, ringKernel);
        Cv2.Subtract(dilated, bodyMask, ringMask);

        var haloSoftMask = new Mat();
        Cv2.GaussianBlur(ringMask, haloSoftMask, new Size(0, 0), sigma * 1.8);
        Cv2.Threshold(haloSoftMask, haloSoftMask, 16, 255, ThresholdTypes.Tozero);
        return haloSoftMask;
    }

    private List<NormalizedPoint> SmoothHand(HandLandmarks hand)
    {
        var key = GetHandKey(hand);
        if (!_smoothedHands.TryGetValue(key, out var state) || state.Points.Count != hand.Points.Count)
        {
            state = new SmoothedHandState(hand.Points.ToList());
            _smoothedHands[key] = state;
            return state.Points;
        }

        var alpha = _settings.SmoothingAlpha;
        for (var i = 0; i < hand.Points.Count; i++)
        {
            var current = hand.Points[i];
            var previous = state.Points[i];

            state.Points[i] = new NormalizedPoint(
                X: (alpha * current.X) + ((1f - alpha) * previous.X),
                Y: (alpha * current.Y) + ((1f - alpha) * previous.Y));
        }

        state.LastSeenFrame = DateTime.UtcNow;
        return state.Points;
    }

    private string GetHandKey(HandLandmarks hand)
    {
        var handedness = string.IsNullOrWhiteSpace(hand.Handedness) ? "Unknown" : hand.Handedness;
        return handedness.Trim();
    }

    private static Point[] ToFramePoints(IReadOnlyList<NormalizedPoint> points, Size frameSize)
    {
        var maxX = Math.Max(0, frameSize.Width - 1);
        var maxY = Math.Max(0, frameSize.Height - 1);

        return points
            .Select(point => new Point(
                x: Math.Clamp((int)(point.X * frameSize.Width), 0, maxX),
                y: Math.Clamp((int)(point.Y * frameSize.Height), 0, maxY)))
            .ToArray();
    }

    private static void DrawPalm(Mat mask, IReadOnlyList<Point> points)
    {
        if (!TrySelectPoints(points, PalmContourIndices, out var palmContour))
        {
            return;
        }

        Cv2.FillConvexPoly(mask, palmContour, Scalar.White, LineTypes.AntiAlias);
    }

    private static void DrawFingers(Mat mask, IReadOnlyList<Point> points, Size frameSize)
    {
        var baseThickness = Math.Max(5, Math.Min(frameSize.Width, frameSize.Height) / 52);

        foreach (var chain in FingerChains)
        {
            for (var segmentIndex = 0; segmentIndex < chain.Length - 1; segmentIndex++)
            {
                var startIndex = chain[segmentIndex];
                var endIndex = chain[segmentIndex + 1];
                if (startIndex >= points.Count || endIndex >= points.Count)
                {
                    continue;
                }

                var thickness = Math.Max(2, baseThickness - (segmentIndex * 1));
                Cv2.Line(mask, points[startIndex], points[endIndex], Scalar.White, thickness, LineTypes.AntiAlias);
                Cv2.Circle(mask, points[endIndex], Math.Max(2, thickness / 3), Scalar.White, -1, LineTypes.AntiAlias);
            }
        }
    }

    private void DrawLandmarks(Mat target, IReadOnlyList<Point> points, string handedness)
    {
        foreach (var point in points)
        {
            Cv2.Circle(target, point, _settings.LandmarkSize, LandmarkColor, -1, LineTypes.AntiAlias);
        }

        if (!_settings.ShowHandednessLabel || points.Count == 0)
        {
            return;
        }

        var anchor = points[0];
        Cv2.PutText(
            target,
            handedness,
            new Point(anchor.X + 10, anchor.Y - 10),
            HersheyFonts.HersheySimplex,
            0.45,
            LandmarkColor,
            1,
            LineTypes.AntiAlias);
    }

    private static bool TrySelectPoints(IReadOnlyList<Point> points, IReadOnlyList<int> indices, out Point[] selected)
    {
        selected = indices.Where(index => index < points.Count).Select(index => points[index]).ToArray();
        return selected.Length >= 3;
    }

    private static void BlendTintByMask(Mat targetBgr, Mat mask8u, Scalar tintBgr, float opacity)
    {
        using var targetFloat = new Mat();
        targetBgr.ConvertTo(targetFloat, MatType.CV_32FC3, 1.0 / 255.0);

        using var alphaFloat = new Mat();
        mask8u.ConvertTo(alphaFloat, MatType.CV_32FC1, opacity / 255.0);

        using var alpha3 = new Mat();
        Cv2.Merge([alphaFloat, alphaFloat, alphaFloat], alpha3);

        using var oneMinusAlpha = new Mat();
        Cv2.Subtract(Scalar.All(1.0), alpha3, oneMinusAlpha);

        using var tintFloat = new Mat(targetBgr.Size(), MatType.CV_32FC3, new Scalar(tintBgr.Val0 / 255.0, tintBgr.Val1 / 255.0, tintBgr.Val2 / 255.0));
        using var tintedPortion = new Mat();
        Cv2.Multiply(tintFloat, alpha3, tintedPortion);

        using var originalPortion = new Mat();
        Cv2.Multiply(targetFloat, oneMinusAlpha, originalPortion);

        using var merged = new Mat();
        Cv2.Add(originalPortion, tintedPortion, merged);
        merged.ConvertTo(targetBgr, MatType.CV_8UC3, 255.0);
    }

    private static Size KernelFromSigma(double sigma)
    {
        var radius = Math.Max(1, (int)Math.Ceiling(sigma * 2.5));
        var size = (radius * 2) + 1;
        return new Size(size, size);
    }

    private void CleanupStaleHands()
    {
        var staleThreshold = DateTime.UtcNow.AddSeconds(-2);
        var staleKeys = _smoothedHands
            .Where(pair => pair.Value.LastSeenFrame < staleThreshold)
            .Select(pair => pair.Key)
            .ToArray();

        foreach (var staleKey in staleKeys)
        {
            _smoothedHands.Remove(staleKey);
        }
    }

    private sealed class SmoothedHandState(List<NormalizedPoint> points)
    {
        public List<NormalizedPoint> Points { get; } = points;

        public DateTime LastSeenFrame { get; set; } = DateTime.UtcNow;
    }
}
