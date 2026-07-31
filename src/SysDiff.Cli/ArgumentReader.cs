namespace SysDiff.Cli;

internal sealed class ArgumentReader
{
    private readonly List<string> _positionals = [];
    private readonly Dictionary<string, string?> _options =
        new(StringComparer.OrdinalIgnoreCase);

    public ArgumentReader(IEnumerable<string> args)
    {
        string[] values = [.. args];

        for (int index = 0; index < values.Length; index++)
        {
            string current = values[index];

            if (!current.StartsWith("--", StringComparison.Ordinal))
            {
                _positionals.Add(current);
                continue;
            }

            string key = current[2..];
            string? value = null;

            int equalsIndex = key.IndexOf('=');
            if (equalsIndex >= 0)
            {
                value = key[(equalsIndex + 1)..];
                key = key[..equalsIndex];
            }
            else if (index + 1 < values.Length
                     && !values[index + 1].StartsWith("-", StringComparison.Ordinal))
            {
                value = values[++index];
            }

            _options[key] = value;
        }
    }

    public IReadOnlyList<string> Positionals => _positionals;

    public bool Has(string name) => _options.ContainsKey(name);

    public string? Get(string name) =>
        _options.TryGetValue(name, out string? value) ? value : null;

    public string Get(string name, string fallback) =>
        Get(name) is { Length: > 0 } value ? value : fallback;

    public int GetInt(string name, int fallback) =>
        int.TryParse(Get(name), out int value) ? value : fallback;
}
