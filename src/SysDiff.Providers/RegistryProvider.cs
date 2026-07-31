using System.Security.Cryptography;
using Microsoft.Win32;
using SysDiff.Core;
using SysDiff.Domain;

namespace SysDiff.Providers;

public sealed class RegistryProvider : ISnapshotProvider
{
    public string Id => "registry";

    public string DisplayName => "Реестр Windows";

    public bool RequiresAdministrator => false;

    public Task<ProviderSnapshotResult> CaptureAsync(
        SnapshotContext context,
        CancellationToken cancellationToken)
    {
        DateTimeOffset started = DateTimeOffset.UtcNow;
        var artifacts = new List<SystemArtifact>();
        var warnings = new List<string>();

        if (!context.Profile.Providers.TryGetValue(Id, out ProviderOptions? options))
        {
            return Task.FromResult(new ProviderSnapshotResult
            {
                ProviderId = Id,
                DisplayName = DisplayName,
                Status = ProviderStatus.Skipped,
                StartedAtUtc = started,
                FinishedAtUtc = DateTimeOffset.UtcNow,
                Warnings = ["Провайдер отсутствует в профиле."]
            });
        }

        foreach (string rootSpec in options.Roots)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                (RegistryHive hive, RegistryView view, string subKey, string identityPrefix) =
                    ParseRoot(rootSpec);

                using RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view);
                using RegistryKey? rootKey = string.IsNullOrEmpty(subKey)
                    ? baseKey
                    : baseKey.OpenSubKey(subKey, writable: false);

                if (rootKey is null)
                {
                    warnings.Add($"Раздел не найден: {rootSpec}");
                    continue;
                }

                ScanKey(
                    rootKey,
                    subKey,
                    identityPrefix,
                    0,
                    options,
                    artifacts,
                    warnings,
                    context.Progress,
                    cancellationToken);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                or System.Security.SecurityException
                or IOException
                or ArgumentException)
            {
                warnings.Add($"{rootSpec}: {exception.Message}");
            }
        }

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

    private static void ScanKey(
        RegistryKey key,
        string relativePath,
        string identityPrefix,
        int depth,
        ProviderOptions options,
        List<SystemArtifact> artifacts,
        List<string> warnings,
        IProgress<SnapshotProgress>? progress,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (artifacts.Count >= options.MaximumArtifacts)
        {
            return;
        }

        string normalizedPath = relativePath.Replace('\\', '/');
        progress?.Report(new SnapshotProgress(
            "registry",
            "Сканирование реестра",
            artifacts.Count,
            $"{identityPrefix}/{normalizedPath}"));

        string[] valueNames;
        try
        {
            valueNames = key.GetValueNames();
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException
            or System.Security.SecurityException
            or IOException)
        {
            warnings.Add($"{key.Name}: {exception.Message}");
            return;
        }

        foreach (string valueName in valueNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                object? raw = key.GetValue(valueName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                RegistryValueKind kind = key.GetValueKind(valueName);
                string displayName = string.IsNullOrEmpty(valueName) ? "(По умолчанию)" : valueName;
                ArtifactValue protectedValue = CreateValue(displayName, raw, kind);

                artifacts.Add(new SystemArtifact
                {
                    ProviderId = "registry",
                    ArtifactType = "RegistryValue",
                    Identity = $"registry://{identityPrefix}/{normalizedPath}/{Uri.EscapeDataString(displayName)}",
                    DisplayName = $"{key.Name}\\{displayName}",
                    Properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["KeyPath"] = ArtifactValue.From(key.Name),
                        ["ValueName"] = ArtifactValue.From(displayName),
                        ["ValueKind"] = ArtifactValue.From(kind),
                        ["Value"] = protectedValue
                    }
                });
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                or System.Security.SecurityException
                or IOException
                or ArgumentException)
            {
                warnings.Add($"{key.Name}\\{valueName}: {exception.Message}");
            }

            if (artifacts.Count >= options.MaximumArtifacts)
            {
                warnings.Add($"Достигнут лимит объектов: {options.MaximumArtifacts}.");
                return;
            }
        }

        if (depth >= options.MaximumDepth)
        {
            return;
        }

        string[] subKeyNames;
        try
        {
            subKeyNames = key.GetSubKeyNames();
        }
        catch (Exception exception) when (
            exception is UnauthorizedAccessException
            or System.Security.SecurityException
            or IOException)
        {
            warnings.Add($"{key.Name}: {exception.Message}");
            return;
        }

        foreach (string childName in subKeyNames)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using RegistryKey? child = key.OpenSubKey(childName, writable: false);
                if (child is null)
                {
                    continue;
                }

                string childPath = string.IsNullOrEmpty(relativePath)
                    ? childName
                    : $"{relativePath}\\{childName}";

                ScanKey(
                    child,
                    childPath,
                    identityPrefix,
                    depth + 1,
                    options,
                    artifacts,
                    warnings,
                    progress,
                    cancellationToken);
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException
                or System.Security.SecurityException
                or IOException)
            {
                warnings.Add($"{key.Name}\\{childName}: {exception.Message}");
            }
        }
    }

    private static ArtifactValue CreateValue(
        string name,
        object? raw,
        RegistryValueKind kind)
    {
        if (raw is byte[] bytes)
        {
            string hash = Convert.ToHexString(SHA256.HashData(bytes));
            string preview = Convert.ToHexString(bytes.AsSpan(0, Math.Min(bytes.Length, 64)));

            return new ArtifactValue
            {
                Value = bytes.Length <= 1024 ? Convert.ToBase64String(bytes) : preview,
                Type = kind.ToString(),
                Hash = hash,
                Redacted = false
            };
        }

        if (raw is string[] strings)
        {
            return SecretRedactor.Protect(name, string.Join('\n', strings), kind.ToString());
        }

        return SecretRedactor.Protect(name, raw, kind.ToString());
    }

    private static (
        RegistryHive Hive,
        RegistryView View,
        string SubKey,
        string IdentityPrefix) ParseRoot(string rootSpec)
    {
        string normalized = rootSpec.Trim().Replace('/', '\\');
        string[] parts = normalized.Split('\\', 2);
        string root = parts[0].ToUpperInvariant();
        string subKey = parts.Length > 1 ? parts[1] : string.Empty;

        return root switch
        {
            "HKCU" or "HKEY_CURRENT_USER" =>
                (RegistryHive.CurrentUser, RegistryView.Default, subKey, "HKCU"),
            "HKLM64" =>
                (RegistryHive.LocalMachine, RegistryView.Registry64, subKey, "HKLM64"),
            "HKLM32" =>
                (RegistryHive.LocalMachine, RegistryView.Registry32, subKey, "HKLM32"),
            "HKLM" or "HKEY_LOCAL_MACHINE" =>
                (RegistryHive.LocalMachine, RegistryView.Default, subKey, "HKLM"),
            "HKCR64" =>
                (RegistryHive.ClassesRoot, RegistryView.Registry64, subKey, "HKCR64"),
            "HKCR32" =>
                (RegistryHive.ClassesRoot, RegistryView.Registry32, subKey, "HKCR32"),
            "HKCR" or "HKEY_CLASSES_ROOT" =>
                (RegistryHive.ClassesRoot, RegistryView.Default, subKey, "HKCR"),
            "HKU" or "HKEY_USERS" =>
                (RegistryHive.Users, RegistryView.Default, subKey, "HKU"),
            "HKCC" or "HKEY_CURRENT_CONFIG" =>
                (RegistryHive.CurrentConfig, RegistryView.Default, subKey, "HKCC"),
            _ => throw new ArgumentException($"Неизвестный корень реестра: {rootSpec}")
        };
    }
}
