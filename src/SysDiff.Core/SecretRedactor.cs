using System.Security.Cryptography;
using System.Text;
using SysDiff.Domain;

namespace SysDiff.Core;

public static class SecretRedactor
{
    private static readonly string[] SensitiveNames =
    [
        "password",
        "token",
        "secret",
        "credential",
        "apikey",
        "api_key",
        "privatekey"
    ];

    public static ArtifactValue Protect(string name, object? value, string type)
    {
        string text = Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture)
            ?? string.Empty;

        if (!SensitiveNames.Any(x => name.Contains(x, StringComparison.OrdinalIgnoreCase)))
        {
            return new ArtifactValue
            {
                Value = text,
                Type = type
            };
        }

        return new ArtifactValue
        {
            Value = "<redacted>",
            Type = type,
            Redacted = true,
            Hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))
        };
    }
}
