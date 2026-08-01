using System.Globalization;

namespace SysDiff.Cli;

public sealed class ReleaseVersion : IComparable<ReleaseVersion>, IEquatable<ReleaseVersion>
{
    private readonly string[] _preRelease;

    private ReleaseVersion(int major, int minor, int patch, string[] preRelease)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
        _preRelease = preRelease;
    }

    public int Major { get; }

    public int Minor { get; }

    public int Patch { get; }

    public IReadOnlyList<string> PreRelease => _preRelease;

    public bool IsPreRelease => _preRelease.Length > 0;

    public static ReleaseVersion Parse(string value)
    {
        if (!TryParse(value, out ReleaseVersion? result))
        {
            throw new FormatException($"Некорректная версия SemVer: {value}");
        }

        return result;
    }

    public static bool TryParse(string? value, out ReleaseVersion? result)
    {
        result = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        string text = value.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        int buildSeparator = text.IndexOf('+');
        string? buildMetadata = null;
        if (buildSeparator >= 0)
        {
            buildMetadata = text[(buildSeparator + 1)..];
            text = text[..buildSeparator];
            if (!ValidateIdentifierList(buildMetadata, allowNumericLeadingZero: true))
            {
                return false;
            }
        }

        int preSeparator = text.IndexOf('-');
        string? preReleaseText = null;
        if (preSeparator >= 0)
        {
            preReleaseText = text[(preSeparator + 1)..];
            text = text[..preSeparator];
            if (!ValidateIdentifierList(preReleaseText, allowNumericLeadingZero: false))
            {
                return false;
            }
        }

        string[] core = text.Split('.');
        if (core.Length != 3
            || !TryParseCoreNumber(core[0], out int major)
            || !TryParseCoreNumber(core[1], out int minor)
            || !TryParseCoreNumber(core[2], out int patch))
        {
            return false;
        }

        string[] preRelease = string.IsNullOrEmpty(preReleaseText)
            ? []
            : preReleaseText.Split('.');
        result = new ReleaseVersion(major, minor, patch, preRelease);
        return true;
    }

    public int CompareTo(ReleaseVersion? other)
    {
        if (other is null)
        {
            return 1;
        }

        int result = Major.CompareTo(other.Major);
        if (result != 0)
        {
            return result;
        }

        result = Minor.CompareTo(other.Minor);
        if (result != 0)
        {
            return result;
        }

        result = Patch.CompareTo(other.Patch);
        if (result != 0)
        {
            return result;
        }

        if (!IsPreRelease && !other.IsPreRelease)
        {
            return 0;
        }

        if (!IsPreRelease)
        {
            return 1;
        }

        if (!other.IsPreRelease)
        {
            return -1;
        }

        int count = Math.Min(_preRelease.Length, other._preRelease.Length);
        for (int index = 0; index < count; index++)
        {
            string left = _preRelease[index];
            string right = other._preRelease[index];
            bool leftNumeric = IsNumeric(left);
            bool rightNumeric = IsNumeric(right);

            if (leftNumeric && rightNumeric)
            {
                result = CompareNumericIdentifier(left, right);
            }
            else if (leftNumeric)
            {
                result = -1;
            }
            else if (rightNumeric)
            {
                result = 1;
            }
            else
            {
                result = string.Compare(left, right, StringComparison.Ordinal);
            }

            if (result != 0)
            {
                return result;
            }
        }

        return _preRelease.Length.CompareTo(other._preRelease.Length);
    }

    public bool Equals(ReleaseVersion? other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is ReleaseVersion other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Major);
        hash.Add(Minor);
        hash.Add(Patch);
        foreach (string identifier in _preRelease)
        {
            hash.Add(identifier, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    public override string ToString()
    {
        string core = string.Create(
            CultureInfo.InvariantCulture,
            $"{Major}.{Minor}.{Patch}");
        return IsPreRelease ? $"{core}-{string.Join('.', _preRelease)}" : core;
    }

    public static bool operator >(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) > 0;

    public static bool operator <(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) < 0;

    public static bool operator >=(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) >= 0;

    public static bool operator <=(ReleaseVersion left, ReleaseVersion right) => left.CompareTo(right) <= 0;

    private static bool TryParseCoreNumber(string value, out int result)
    {
        result = 0;
        return value.Length > 0
            && (value.Length == 1 || value[0] != '0')
            && value.All(character => character is >= '0' and <= '9')
            && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result);
    }

    private static bool ValidateIdentifierList(string value, bool allowNumericLeadingZero)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (string identifier in value.Split('.'))
        {
            if (identifier.Length == 0
                || identifier.Any(character =>
                    !(character is >= '0' and <= '9')
                    && !(character is >= 'A' and <= 'Z')
                    && !(character is >= 'a' and <= 'z')
                    && character != '-'))
            {
                return false;
            }

            if (!allowNumericLeadingZero
                && IsNumeric(identifier)
                && identifier.Length > 1
                && identifier[0] == '0')
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsNumeric(string value) =>
        value.Length > 0 && value.All(character => character is >= '0' and <= '9');

    private static int CompareNumericIdentifier(string left, string right)
    {
        int length = left.Length.CompareTo(right.Length);
        return length != 0 ? length : string.Compare(left, right, StringComparison.Ordinal);
    }
}
