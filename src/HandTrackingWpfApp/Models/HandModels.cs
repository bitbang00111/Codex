namespace HandTrackingWpfApp.Models;

public sealed record NormalizedPoint(float X, float Y);

public sealed class HandLandmarks
{
    public required string Handedness { get; init; }

    public required IReadOnlyList<NormalizedPoint> Points { get; init; }
}

public sealed class TrackingResult
{
    public required IReadOnlyList<HandLandmarks> Hands { get; init; }
}
