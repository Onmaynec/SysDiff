using System.IO.Compression;
using SysDiff.Domain;
using SysDiff.Storage;

namespace SysDiff.Core.Tests;

public sealed class SnapshotArchiveTests
{
    [Fact]
    public async Task ExportImport_RoundTripPreservesArtifacts()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sdshot");
        var store = new MemoryStore();
        var service = new SnapshotArchiveService(store);
        var snapshot = new SnapshotRecord
        {
            Name = "roundtrip",
            Status = SnapshotStatus.Completed,
            Artifacts =
            [
                new SystemArtifact
                {
                    ProviderId = "test",
                    ArtifactType = "Item",
                    Identity = "test://one",
                    DisplayName = "One",
                    Properties = new Dictionary<string, ArtifactValue>
                    {
                        ["Value"] = ArtifactValue.From("demo")
                    }
                }
            ]
        };

        try
        {
            await service.ExportAsync(snapshot, path, CancellationToken.None);
            SnapshotRecord imported = await service.ImportAsync(path, CancellationToken.None);

            Assert.Equal(snapshot.Id, imported.Id);
            Assert.Equal("test://one", Assert.Single(imported.Artifacts).Identity);
            Assert.NotNull(store.Snapshot);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task Import_RejectsModifiedChecksumFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.sdshot");
        var store = new MemoryStore();
        var service = new SnapshotArchiveService(store);
        var snapshot = new SnapshotRecord
        {
            Name = "corrupted",
            Status = SnapshotStatus.Completed
        };

        try
        {
            await service.ExportAsync(snapshot, path, CancellationToken.None);
            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.ReadWrite))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Update))
            {
                ZipArchiveEntry entry = archive.GetEntry("checksums.sha256")!;
                entry.Delete();
                ZipArchiveEntry replacement = archive.CreateEntry("checksums.sha256");
                await using Stream writer = replacement.Open();
                await writer.WriteAsync("invalid"u8.ToArray());
            }

            await Assert.ThrowsAsync<InvalidDataException>(() =>
                service.ImportAsync(path, CancellationToken.None));
        }
        finally
        {
            File.Delete(path);
        }
    }

    private sealed class MemoryStore : ISnapshotStore
    {
        public SnapshotRecord? Snapshot { get; private set; }

        public Task InitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveSnapshotAsync(SnapshotRecord snapshot, CancellationToken cancellationToken)
        {
            Snapshot = snapshot;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SnapshotRecord>> ListSnapshotsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<SnapshotRecord>>(
                Snapshot is null ? [] : [Snapshot]);

        public Task<SnapshotRecord?> GetSnapshotAsync(
            string nameOrId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Snapshot);

        public Task DeleteSnapshotAsync(string nameOrId, CancellationToken cancellationToken)
        {
            Snapshot = null;
            return Task.CompletedTask;
        }

        public Task SaveComparisonAsync(
            ComparisonResult comparison,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<ComparisonResult?> GetComparisonAsync(
            Guid id,
            CancellationToken cancellationToken) => Task.FromResult<ComparisonResult?>(null);
    }
}
