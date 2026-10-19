using System.Windows.Media.Animation;

namespace CloudflaredTunnelHost.Infrastructure;

/// <summary>Shared timing profile for intentional, non-overlapping UI motion.</summary>
public static class MotionProfile
{
    public const int PageDurationMs = 260;
    public const int TabDurationMs = 205;
    public const double PageTravel = 18;
    public const double TabTravel = 12;
    public const int RevealFadeMs = 190;
    public const int RevealTravelMs = 260;
    public const double RevealTravel = 10;
    public const int StaggerMs = 28;
    public const int MaxStaggerSteps = 4;
    public const int PressMs = 90;
    public const int ReleaseMs = 150;
    public const double PressScale = .988;

    public static IEasingFunction EaseOut() => new CubicEase { EasingMode = EasingMode.EaseOut };
}
