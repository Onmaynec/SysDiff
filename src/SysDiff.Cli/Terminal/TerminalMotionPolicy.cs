namespace SysDiff.Cli;

public sealed class TerminalMotionPolicy
{
    public TerminalMotionPolicy(bool animationsEnabled, bool colorsEnabled, int frameDelayMilliseconds)
    {
        AnimationsEnabled = animationsEnabled;
        ColorsEnabled = colorsEnabled;
        FrameDelayMilliseconds = Math.Clamp(frameDelayMilliseconds, 0, 250);
    }

    public bool AnimationsEnabled { get; }

    public bool ColorsEnabled { get; }

    public int FrameDelayMilliseconds { get; }

    public static TerminalMotionPolicy Detect()
    {
        bool interactive = TerminalCapabilities.IsInteractive;
        bool animations = ShouldAnimate(
            interactive,
            Console.IsOutputRedirected,
            Environment.GetEnvironmentVariable("SYSDIFF_NO_ANIMATIONS"),
            Environment.GetEnvironmentVariable("CI"));
        bool colors = ShouldUseColors(
            interactive,
            Environment.GetEnvironmentVariable("NO_COLOR"));
        return new TerminalMotionPolicy(animations, colors, animations ? 45 : 0);
    }

    public static bool ShouldAnimate(
        bool interactive,
        bool outputRedirected,
        string? disableAnimations,
        string? ci)
    {
        return interactive
            && !outputRedirected
            && !IsTruthy(disableAnimations)
            && !IsTruthy(ci);
    }

    public static bool ShouldUseColors(bool interactive, string? noColor) =>
        interactive && !IsTruthy(noColor);

    public static bool IsTruthy(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("yes", StringComparison.OrdinalIgnoreCase)
            || value.Trim().Equals("on", StringComparison.OrdinalIgnoreCase);
    }
}
