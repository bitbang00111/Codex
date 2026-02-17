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

    public float BodyOpacity { get; init; } = 0.33f;

    public float HaloOpacity { get; init; } = 0.14f;

    public double BlurSigma { get; init; } = 3.2;

    public int LandmarkSize { get; init; } = 2;

    public bool ShowLandmarks { get; init; } = false;

    public bool ShowHandednessLabel { get; init; } = false;

    public float SmoothingAlpha { get; init; } = 0.45f;
}
