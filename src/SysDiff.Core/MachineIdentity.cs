using System.Security.Cryptography;
using System.Text;

namespace SysDiff.Core;

public static class MachineIdentity
{
    public static string CreateFingerprint()
    {
        string source = string.Join('|',
            "SysDiff-Machine-v1",
            Environment.MachineName,
            Environment.OSVersion.VersionString,
            Environment.Is64BitOperatingSystem ? "x64" : "x86");

        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
