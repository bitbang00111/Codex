using HandTrackingWpfApp.Models;
using OpenCvSharp;

namespace HandTrackingWpfApp.Services;

public sealed class FrameRenderer
{
    private static readonly Scalar SilhouetteColor = new(70, 190, 255); // BGR
    private static readonly Scalar LandmarkColor = new(255, 255, 255);

    public Mat Render(Mat sourceBgr, TrackingResult trackingResult)
    {
        var composed = sourceBgr.Clone();
        using var overlay = sourceBgr.Clone();

        foreach (var hand in trackingResult.Hands)
        {
            var points = hand.Points
                .Select(p => new Point((int)(p.X * sourceBgr.Width), (int)(p.Y * sourceBgr.Height)))
                .ToArray();

            if (points.Length >= 3)
            {
                Cv2.FillConvexPoly(overlay, points, SilhouetteColor, LineTypes.AntiAlias);
            }

            foreach (var point in points)
            {
                Cv2.Circle(overlay, point, 4, LandmarkColor, -1, LineTypes.AntiAlias);
            }

            if (points.Length > 0)
            {
                var anchor = points[0];
                Cv2.PutText(
                    overlay,
                    hand.Handedness,
                    new Point(anchor.X + 12, anchor.Y - 12),
                    HersheyFonts.HersheySimplex,
                    0.6,
                    LandmarkColor,
                    2,
                    LineTypes.AntiAlias);
            }
        }

        Cv2.AddWeighted(overlay, 0.45, composed, 0.55, 0, composed);
        return composed;
    }
}
