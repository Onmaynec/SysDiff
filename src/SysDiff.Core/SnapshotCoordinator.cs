using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using SysDiff.Domain;

namespace SysDiff.Core;

public sealed class SnapshotCoordinator
{
    private readonly IReadOnlyDictionary<string, ISnapshotProvider> _providers;
    private readonly ISnapshotStore _store;
    private readonly PrivacyRedactor _privacyRedactor;
    private readonly ILogger<SnapshotCoordinator> _logger;

    public SnapshotCoordinator(
        IEnumerable<ISnapshotProvider> providers,
        ISnapshotStore store,
        PrivacyRedactor privacyRedactor,
        ILogger<SnapshotCoordinator> logger)
    {
        _providers = providers.ToDictionary(x => x.Id, StringComparer.OrdinalIgnoreCase);
        _store = store;
        _privacyRedactor = privacyRedactor;
        _logger = logger;
    }

    public async Task<SnapshotRecord> CaptureAsync(
        string name,
        CaptureProfile profile,
        string dataDirectory,
        bool isAdministrator,
        IProgress<SnapshotProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var snapshot = new SnapshotRecord
        {
            Name = name.Trim(),
            ProfileName = profile.Name,
            Status = SnapshotStatus.InProgress,
            WindowsEdition = RuntimeInformation.OSDescription,
            WindowsBuild = Environment.OSVersion.Version.ToString(),
            MachineFingerprint = MachineIdentity.CreateFingerprint()
        };

        await _store.SaveSnapshotAsync(snapshot, cancellationToken);

        var results = new List<ProviderSnapshotResult>();
        var artifacts = new List<SystemArtifact>();

        try
        {
            foreach ((string providerId, ProviderOptions options) in profile.Providers)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!options.Enabled)
                {
                    continue;
                }

                if (!_providers.TryGetValue(providerId, out ISnapshotProvider? provider))
                {
                    results.Add(new ProviderSnapshotResult
                    {
                        ProviderId = providerId,
                        DisplayName = providerId,
                        Status = ProviderStatus.Skipped,
                        StartedAtUtc = DateTimeOffset.UtcNow,
                        FinishedAtUtc = DateTimeOffset.UtcNow,
                        Warnings = [$"Провайдер «{providerId}» не зарегистрирован."]
                    });
                    continue;
                }

                if (provider.RequiresAdministrator && !isAdministrator)
                {
                    _logger.LogWarning(
                        "Провайдер {ProviderId} выполняется без прав администратора.",
                        provider.Id);
                }

                ProviderSnapshotResult result;
                try
                {
                    result = await provider.CaptureAsync(
                        new SnapshotContext
                        {
                            Profile = profile,
                            DataDirectory = dataDirectory,
                            IsAdministrator = isAdministrator,
                            Progress = progress
                        },
                        cancellationToken);
                    result = _privacyRedactor.RedactResult(result);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Ошибка провайдера {ProviderId}", provider.Id);
                    result = new ProviderSnapshotResult
                    {
                        ProviderId = provider.Id,
                        DisplayName = provider.DisplayName,
                        Status = ProviderStatus.Failed,
                        StartedAtUtc = DateTimeOffset.UtcNow,
                        FinishedAtUtc = DateTimeOffset.UtcNow,
                        Errors = [_privacyRedactor.Redact(exception.Message)],
                        RequiresAdministrator = provider.RequiresAdministrator
                    };
                }

                results.Add(result);
                artifacts.AddRange(result.Artifacts);
            }

            SnapshotStatus finalStatus = results.Any(x => x.Status is ProviderStatus.Failed or ProviderStatus.Partial)
                ? SnapshotStatus.Partial
                : SnapshotStatus.Completed;

            snapshot = snapshot with
            {
                Status = finalStatus,
                ProviderResults = results,
                Artifacts = artifacts
            };

            await _store.SaveSnapshotAsync(snapshot, cancellationToken);
            return snapshot;
        }
        catch (OperationCanceledException)
        {
            snapshot = snapshot with
            {
                Status = SnapshotStatus.Cancelled,
                ProviderResults = results,
                Artifacts = artifacts
            };

            await _store.SaveSnapshotAsync(snapshot, CancellationToken.None);
            throw;
        }
        catch
        {
            snapshot = snapshot with
            {
                Status = SnapshotStatus.Failed,
                ProviderResults = results,
                Artifacts = artifacts
            };

            await _store.SaveSnapshotAsync(snapshot, CancellationToken.None);
            throw;
        }
    }
}
