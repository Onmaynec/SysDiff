using System.Security.Principal;
using System.Text.RegularExpressions;

namespace SysDiff.Providers;

internal static class ProviderUtilities
{
    public static bool IsAdministrator()
    {
        using WindowsIdentity identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static string ExpandPath(string path) =>
        Environment.ExpandEnvironmentVariables(path.Trim());

    public static string NormalizePath(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string? root = Path.GetPathRoot(fullPath);
        string normalized = string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        return normalized.Replace('\\', '/');
    }

    public static string FileIdentity(string path) =>
        $"file://{NormalizePath(path)}";

    public static bool MatchesAny(string value, IEnumerable<string> patterns)
    {
        foreach (string rawPattern in patterns)
        {
            string pattern = ExpandPath(rawPattern).Replace('/', '\\');
            string candidate = value.Replace('/', '\\');

            if (!pattern.Contains('*') && !pattern.Contains('?'))
            {
                if (candidate.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                continue;
            }

            string regex = "^" + Regex.Escape(pattern)
                .Replace(@"\*\*", ".*", StringComparison.Ordinal)
                .Replace(@"\*", @"[^\\]*", StringComparison.Ordinal)
                .Replace(@"\?", ".", StringComparison.Ordinal) + "$";

            if (Regex.IsMatch(candidate, regex, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
            {
                return true;
            }
        }

        return false;
    }
}
