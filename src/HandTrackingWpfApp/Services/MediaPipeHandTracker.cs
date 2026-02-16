using HandTrackingWpfApp.Models;
using OpenCvSharp;

namespace HandTrackingWpfApp.Services;

/// <summary>
/// Two-stage tracker:
/// 1) Tries to execute MediaPipe.NET through reflection (version-tolerant).
/// 2) Falls back to OpenCV contour-based hand tracking when MediaPipe runtime assets are unavailable.
/// </summary>
public sealed class MediaPipeHandTracker : IHandTracker
{
    private readonly IReflectionMediaPipeRunner? _mediaPipeRunner;
    private readonly OpenCvFallbackHandTracker _fallbackTracker = new();

    public MediaPipeHandTracker()
    {
        _mediaPipeRunner = ReflectionMediaPipeRunnerFactory.TryCreate();
    }

    public TrackingResult Track(Mat bgrFrame)
    {
        if (_mediaPipeRunner is not null)
        {
            var mediaPipeResult = _mediaPipeRunner.TryTrack(bgrFrame);
            if (mediaPipeResult is not null && mediaPipeResult.Hands.Count > 0)
            {
                return mediaPipeResult;
            }
        }

        return _fallbackTracker.Track(bgrFrame);
    }

    public void Dispose()
    {
        _mediaPipeRunner?.Dispose();
        GC.SuppressFinalize(this);
    }

    private interface IReflectionMediaPipeRunner : IDisposable
    {
        TrackingResult? TryTrack(Mat bgrFrame);
    }

    private static class ReflectionMediaPipeRunnerFactory
    {
        public static IReflectionMediaPipeRunner? TryCreate()
        {
            // This loader intentionally supports multiple package naming variants and
            // returns null if it cannot safely activate a supported MediaPipe runner.
            var candidateTypeNames = new[]
            {
                "Mediapipe.Net.Solutions.Hands.HandsSolution, Mediapipe.Net",
                "Mediapipe.Solutions.Hands.Hands, Mediapipe.Net",
                "Mediapipe.Hands, Mediapipe.Net"
            };

            foreach (var typeName in candidateTypeNames)
            {
                var type = Type.GetType(typeName, throwOnError: false);
                if (type is null)
                {
                    continue;
                }

                try
                {
                    return new GenericReflectionRunner(type);
                }
                catch
                {
                    // Try next known variant.
                }
            }

            return null;
        }

        private sealed class GenericReflectionRunner : IReflectionMediaPipeRunner
        {
            private readonly object _instance;
            private readonly System.Reflection.MethodInfo _processMethod;

            public GenericReflectionRunner(Type runnerType)
            {
                _instance = Activator.CreateInstance(runnerType)
                    ?? throw new InvalidOperationException($"Failed to instantiate {runnerType.FullName}.");

                _processMethod = runnerType.GetMethods()
                    .FirstOrDefault(m => string.Equals(m.Name, "Process", StringComparison.OrdinalIgnoreCase)
                        && m.GetParameters().Length == 1)
                    ?? throw new InvalidOperationException($"No Process(frame) method found on {runnerType.FullName}.");
            }

            public TrackingResult? TryTrack(Mat bgrFrame)
            {
                // Convert BGR frame to RGB for MediaPipe convention.
                using var rgb = new Mat();
                Cv2.CvtColor(bgrFrame, rgb, ColorConversionCodes.BGR2RGB);

                var result = _processMethod.Invoke(_instance, new object[] { rgb });
                if (result is null)
                {
                    return null;
                }

                // Because APIs differ across MediaPipe.NET versions, parse via reflection defensively.
                return ReflectionResultParser.Parse(result, bgrFrame.Width, bgrFrame.Height);
            }

            public void Dispose()
            {
                if (_instance is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
        }

        private static class ReflectionResultParser
        {
            public static TrackingResult Parse(object result, int width, int height)
            {
                _ = width;
                _ = height;

                var hands = new List<HandLandmarks>();
                var resultType = result.GetType();

                var landmarkListProp = resultType.GetProperties()
                    .FirstOrDefault(p => p.Name.Contains("MultiHandLandmarks", StringComparison.OrdinalIgnoreCase)
                        || p.Name.Contains("HandLandmarks", StringComparison.OrdinalIgnoreCase));

                var handednessProp = resultType.GetProperties()
                    .FirstOrDefault(p => p.Name.Contains("Handedness", StringComparison.OrdinalIgnoreCase));

                var landmarksObj = landmarkListProp?.GetValue(result);
                var handednessObj = handednessProp?.GetValue(result);

                var handLandmarkCollections = ToObjectList(landmarksObj);
                var handednessCollections = ToObjectList(handednessObj);

                for (var i = 0; i < handLandmarkCollections.Count; i++)
                {
                    var points = ExtractPoints(handLandmarkCollections[i]);
                    if (points.Count == 0)
                    {
                        continue;
                    }

                    var label = ExtractHandedness(handednessCollections, i);
                    hands.Add(new HandLandmarks
                    {
                        Handedness = label,
                        Points = points
                    });
                }

                return new TrackingResult { Hands = hands.Take(2).ToArray() };
            }

            private static List<NormalizedPoint> ExtractPoints(object handCollection)
            {
                var points = new List<NormalizedPoint>();
                foreach (var item in ToObjectList(handCollection))
                {
                    var type = item.GetType();
                    var x = TryGetFloat(type, item, "X") ?? TryGetFloat(type, item, "x");
                    var y = TryGetFloat(type, item, "Y") ?? TryGetFloat(type, item, "y");

                    if (x.HasValue && y.HasValue)
                    {
                        points.Add(new NormalizedPoint(Math.Clamp(x.Value, 0f, 1f), Math.Clamp(y.Value, 0f, 1f)));
                    }
                }

                return points;
            }

            private static string ExtractHandedness(List<object> handednessCollections, int index)
            {
                if (index >= handednessCollections.Count)
                {
                    return index == 0 ? "Left" : "Right";
                }

                var handedness = handednessCollections[index];
                var type = handedness.GetType();
                var labelProp = type.GetProperties().FirstOrDefault(p => p.Name.Contains("Label", StringComparison.OrdinalIgnoreCase));
                var label = labelProp?.GetValue(handedness)?.ToString();
                return string.IsNullOrWhiteSpace(label) ? (index == 0 ? "Left" : "Right") : label;
            }

            private static float? TryGetFloat(Type type, object instance, string memberName)
            {
                var prop = type.GetProperty(memberName);
                if (prop is not null)
                {
                    var val = prop.GetValue(instance);
                    return val is null ? null : Convert.ToSingle(val);
                }

                var field = type.GetField(memberName);
                if (field is not null)
                {
                    var val = field.GetValue(instance);
                    return val is null ? null : Convert.ToSingle(val);
                }

                return null;
            }

            private static List<object> ToObjectList(object? value)
            {
                var list = new List<object>();
                if (value is null)
                {
                    return list;
                }

                if (value is System.Collections.IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        if (item is null)
                        {
                            continue;
                        }

                        // Common MediaPipe structures may expose a nested Landmark collection.
                        var landmarksProp = item.GetType().GetProperties()
                            .FirstOrDefault(p => p.Name.Contains("Landmark", StringComparison.OrdinalIgnoreCase)
                                && p.GetIndexParameters().Length == 0);

                        if (landmarksProp?.GetValue(item) is System.Collections.IEnumerable nested)
                        {
                            foreach (var nestedItem in nested)
                            {
                                if (nestedItem is not null)
                                {
                                    list.Add(nestedItem);
                                }
                            }
                        }
                        else
                        {
                            list.Add(item);
                        }
                    }
                }

                return list;
            }
        }
    }

    private sealed class OpenCvFallbackHandTracker
    {
        private readonly Scalar _skinLower = new(0, 40, 60);
        private readonly Scalar _skinUpper = new(25, 190, 255);

        public TrackingResult Track(Mat bgrFrame)
        {
            using var ycrcb = new Mat();
            Cv2.CvtColor(bgrFrame, ycrcb, ColorConversionCodes.BGR2YCrCb);

            using var mask = new Mat();
            Cv2.InRange(ycrcb, _skinLower, _skinUpper, mask);

            using var blurred = new Mat();
            Cv2.GaussianBlur(mask, blurred, new Size(7, 7), 0);

            using var morph = new Mat();
            var kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(5, 5));
            Cv2.MorphologyEx(blurred, morph, MorphTypes.Close, kernel, iterations: 2);
            Cv2.MorphologyEx(morph, morph, MorphTypes.Open, kernel, iterations: 1);

            Cv2.FindContours(morph, out var contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

            var candidates = contours
                .Select(c => (Contour: c, Area: Cv2.ContourArea(c), Bounds: Cv2.BoundingRect(c)))
                .Where(c => c.Area > 3500)
                .OrderByDescending(c => c.Area)
                .Take(2)
                .ToArray();

            var hands = new List<HandLandmarks>(2);

            foreach (var candidate in candidates)
            {
                var hullIdx = Cv2.ConvexHullIndices(candidate.Contour);
                if (hullIdx.Length < 3)
                {
                    continue;
                }

                var hullPoints = Cv2.ConvexHull(candidate.Contour)
                    .Select(p => new NormalizedPoint(
                        X: (float)p.X / bgrFrame.Width,
                        Y: (float)p.Y / bgrFrame.Height))
                    .ToArray();

                if (hullPoints.Length < 3)
                {
                    continue;
                }

                var centerX = candidate.Bounds.X + candidate.Bounds.Width / 2.0;
                var handedness = centerX < bgrFrame.Width / 2.0 ? "Left" : "Right";

                hands.Add(new HandLandmarks
                {
                    Handedness = handedness,
                    Points = hullPoints
                });
            }

            return new TrackingResult { Hands = hands };
        }
    }
}
