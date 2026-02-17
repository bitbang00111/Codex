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

public sealed class GhostRenderSettings
{
    public static GhostRenderSettings Default { get; } = new();

    public bool EnableGhostStyle { get; init; } = true;

    public float BodyOpacity { get; init; } = 0.88f;

    public float HaloOpacity { get; init; } = 0.22f;

    public double BlurSigma { get; init; } = 4.2;

    public int LandmarkSize { get; init; } = 0;

    public float SmoothingAlpha { get; init; } = 0.5f;

    public bool ShowCameraFeed { get; init; } = false;
}
