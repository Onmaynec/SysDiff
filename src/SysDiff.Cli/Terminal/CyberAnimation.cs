namespace SysDiff.Cli;

public static class CyberAnimation
{
    private const string SpinnerFrames = "⠋⠙⠹⠸⠼⠴⠦⠧⠇⠏";

    public static char Spinner(int tick) => SpinnerFrames[Math.Abs(tick) % SpinnerFrames.Length];

    public static string Pulse(int tick) => Math.Abs(tick) % 4 switch
    {
        0 => "·",
        1 => "∙",
        2 => "●",
        _ => "∙"
    };

    public static string BuildProgressBar(double progress, int width, int tick = 0)
    {
        int safeWidth = Math.Max(4, width);
        double normalized = Math.Clamp(progress, 0d, 1d);
        int filled = (int)Math.Round(normalized * safeWidth, MidpointRounding.AwayFromZero);
        filled = Math.Clamp(filled, 0, safeWidth);
        string result = new string('█', filled) + new string('░', safeWidth - filled);
        if (normalized < 1d && safeWidth > 2)
        {
            int scanner = Math.Abs(tick) % safeWidth;
            char[] chars = result.ToCharArray();
            chars[scanner] = chars[scanner] == '█' ? '▓' : '▒';
            result = new string(chars);
        }

        return result;
    }

    public static string BuildScanner(int tick, int width)
    {
        int safeWidth = Math.Max(8, width);
        char[] line = Enumerable.Repeat('·', safeWidth).ToArray();
        int position = Math.Abs(tick) % safeWidth;
        line[position] = '█';
        if (position > 0)
        {
            line[position - 1] = '▓';
        }
        if (position > 1)
        {
            line[position - 2] = '▒';
        }
        return new string(line);
    }

    public static string Reveal(string value, int visibleCharacters)
    {
        ArgumentNullException.ThrowIfNull(value);
        int count = Math.Clamp(visibleCharacters, 0, value.Length);
        return value[..count] + new string(' ', value.Length - count);
    }
}
