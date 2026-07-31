using System.Collections;
using SysDiff.Domain;

namespace SysDiff.Providers;

public sealed class EnvironmentProvider : ISnapshotProvider
{
    public string Id => "environment";

    public string DisplayName => "Переменные окружения";

    public bool RequiresAdministrator => false;

    public Task<ProviderSnapshotResult> CaptureAsync(
        SnapshotContext context,
        CancellationToken cancellationToken)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        var artifacts = new List<SystemArtifact>();
        var warnings = new List<string>();

        CaptureScope(
            EnvironmentVariableTarget.User,
            "user",
            artifacts,
            warnings,
            cancellationToken);

        CaptureScope(
            EnvironmentVariableTarget.Machine,
            "machine",
            artifacts,
            warnings,
            cancellationToken);

        return Task.FromResult(new ProviderSnapshotResult
        {
            ProviderId = Id,
            DisplayName = DisplayName,
            Status = warnings.Count > 0 ? ProviderStatus.Partial : ProviderStatus.Success,
            StartedAtUtc = started,
            FinishedAtUtc = DateTimeOffset.UtcNow,
            ArtifactCount = artifacts.Count,
            Artifacts = artifacts,
            Warnings = warnings
        });
    }

    private static void CaptureScope(
        EnvironmentVariableTarget target,
        string scope,
        List<SystemArtifact> artifacts,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        try
        {
            IDictionary variables = Environment.GetEnvironmentVariables(target);

            foreach (DictionaryEntry entry in variables)
            {
                cancellationToken.ThrowIfCancellationRequested();

                string name = Convert.ToString(
                    entry.Key,
                    System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
                string value = Convert.ToString(
                    entry.Value,
                    System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;

                if (name.Equals("PATH", StringComparison.OrdinalIgnoreCase))
                {
                    AddPathArtifacts(scope, value, artifacts);
                    continue;
                }

                artifacts.Add(new SystemArtifact
                {
                    ProviderId = "environment",
                    ArtifactType = "EnvironmentVariable",
                    Identity = $"environment://{scope}/{Uri.EscapeDataString(name.ToUpperInvariant())}",
                    DisplayName = $"{scope}:{name}",
                    Properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["Scope"] = ArtifactValue.From(scope),
                        ["Name"] = ArtifactValue.From(name),
                        ["Value"] = ArtifactValue.From(value)
                    }
                });
            }
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException
            or System.Security.SecurityException)
        {
            warnings.Add($"{scope}: {exception.Message}");
        }
    }

    private static void AddPathArtifacts(
        string scope,
        string rawPath,
        List<SystemArtifact> artifacts)
    {
        string[] entries = rawPath
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        for (int index = 0; index < entries.Length; index++)
        {
            string entry = entries[index];
            string expanded = Environment.ExpandEnvironmentVariables(entry);
            string identityValue = expanded
                .TrimEnd('\\', '/')
                .Replace('\\', '/')
                .ToUpperInvariant();

            artifacts.Add(new SystemArtifact
            {
                ProviderId = "environment",
                ArtifactType = "PathEntry",
                Identity = $"environment://{scope}/path/{Uri.EscapeDataString(identityValue)}",
                DisplayName = $"{scope}:PATH → {entry}",
                Properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Scope"] = ArtifactValue.From(scope),
                    ["Name"] = ArtifactValue.From("PATH"),
                    ["Value"] = ArtifactValue.From(entry),
                    ["ExpandedValue"] = ArtifactValue.From(expanded),
                    ["Order"] = ArtifactValue.From(index, "Int32"),
                    ["Exists"] = ArtifactValue.From(Directory.Exists(expanded), "Boolean")
                }
            });
        }
    }
}
