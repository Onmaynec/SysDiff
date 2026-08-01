using System.Text.RegularExpressions;
using SysDiff.Domain;

namespace SysDiff.Core;

public sealed partial class PrivacyRedactor
{
    private readonly string? _userProfile;

    public PrivacyRedactor()
    {
        string value = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _userProfile = string.IsNullOrWhiteSpace(value)
            ? null
            : NormalizeSeparators(value.TrimEnd('\\', '/'));
    }

    public string Redact(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        string result = NormalizeSeparators(value);
        if (!string.IsNullOrWhiteSpace(_userProfile))
        {
            result = result.Replace(
                _userProfile,
                "%USERPROFILE%",
                StringComparison.OrdinalIgnoreCase);
        }

        return UserProfilePattern().Replace(result, "%USERPROFILE%$2");
    }

    public SystemArtifact RedactArtifact(SystemArtifact artifact)
    {
        var properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase);
        foreach ((string name, ArtifactValue value) in artifact.Properties)
        {
            properties[name] = value with
            {
                Value = value.Value is null ? null : Redact(value.Value)
            };
        }

        return artifact with
        {
            Identity = Redact(artifact.Identity),
            DisplayName = Redact(artifact.DisplayName),
            Properties = properties
        };
    }

    public ProviderSnapshotResult RedactResult(ProviderSnapshotResult result) => result with
    {
        Artifacts = result.Artifacts.Select(RedactArtifact).ToList(),
        Warnings = result.Warnings.Select(Redact).ToList(),
        Errors = result.Errors.Select(Redact).ToList()
    };

    private static string NormalizeSeparators(string value) => value.Replace('/', '\\');

    [GeneratedRegex(@"(?i)\b[A-Z]:\\Users\\[^\\\r\n]+(\\|$)", RegexOptions.CultureInvariant)]
    private static partial Regex UserProfilePattern();
}
