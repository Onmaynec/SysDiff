using SysDiff.Domain;
using SysDiff.ProviderSdk;

[assembly: SysDiffProviderPlugin(ProviderSdkInfo.CurrentVersion, DisplayName = "Sample Provider")]

namespace SysDiff.SampleProvider;

public sealed class SampleProvider : ISnapshotProvider
{
    public string Id => "sample";

    public string DisplayName => "Пример внешнего провайдера";

    public bool RequiresAdministrator => false;

    public Task<ProviderSnapshotResult> CaptureAsync(
        SnapshotContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var artifact = new SystemArtifact
        {
            ProviderId = Id,
            ArtifactType = "Sample",
            Identity = "sample://environment/runtime",
            DisplayName = ".NET Runtime",
            Properties = new Dictionary<string, ArtifactValue>(StringComparer.OrdinalIgnoreCase)
            {
                ["Version"] = ArtifactValue.From(Environment.Version),
                ["ProcessArchitecture"] = ArtifactValue.From(
                    System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture)
            }
        };

        return Task.FromResult(new ProviderSnapshotResult
        {
            ProviderId = Id,
            DisplayName = DisplayName,
            Status = ProviderStatus.Success,
            StartedAtUtc = now,
            FinishedAtUtc = DateTimeOffset.UtcNow,
            ArtifactCount = 1,
            Artifacts = [artifact]
        });
    }
}
