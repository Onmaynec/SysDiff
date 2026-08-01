using System.Security.Cryptography;
using SysDiff.Cli;

namespace SysDiff.Cli.Tests;

public sealed class UpdateIntegrityTests
{
    [Fact]
    public void VerifyFileHash_AcceptsMatchingSha256()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "SysDiff release payload");
            string hash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(path)));

            Assert.True(UpdateService.VerifyFileHash(path, hash.ToLowerInvariant()));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void VerifyFileHash_RejectsTamperedFile()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, "original");
            string hash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(path)));
            File.AppendAllText(path, "tampered");

            Assert.False(UpdateService.VerifyFileHash(path, hash));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void VerifyFileHash_ReturnsFalseForMissingFile()
    {
        string path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}");

        Assert.False(UpdateService.VerifyFileHash(path, new string('a', 64)));
    }
}
