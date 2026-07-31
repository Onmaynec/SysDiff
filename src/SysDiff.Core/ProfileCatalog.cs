using SysDiff.Domain;

namespace SysDiff.Core;

public sealed class ProfileCatalog
{
    private readonly Dictionary<string, CaptureProfile> _profiles;

    public ProfileCatalog()
    {
        _profiles = new Dictionary<string, CaptureProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["minimal"] = CreateMinimal(),
            ["standard"] = CreateStandard(),
            ["full"] = CreateFull()
        };
    }

    public IReadOnlyCollection<CaptureProfile> All => _profiles.Values;

    public CaptureProfile Get(string name)
    {
        if (_profiles.TryGetValue(name, out CaptureProfile? profile))
        {
            return profile;
        }

        throw new ArgumentException($"Профиль «{name}» не найден.", nameof(name));
    }

    public void AddOrReplace(CaptureProfile profile) => _profiles[profile.Name] = profile;

    private static CaptureProfile CreateMinimal() =>
        new()
        {
            Name = "minimal",
            Description = "Быстрый снимок служб, задач, автозагрузки и окружения.",
            Providers = new Dictionary<string, ProviderOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["services"] = new(),
                ["scheduled-tasks"] = new(),
                ["startup"] = new(),
                ["environment"] = new()
            }
        };

    private static CaptureProfile CreateStandard() =>
        new()
        {
            Name = "standard",
            Description = "Сбалансированный профиль для анализа установщиков.",
            Providers = new Dictionary<string, ProviderOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["filesystem"] = new()
                {
                    Roots =
                    [
                        "%ProgramFiles%",
                        "%ProgramFiles(x86)%",
                        "%ProgramData%",
                        "%AppData%",
                        "%LocalAppData%"
                    ],
                    Exclude =
                    [
                        "**\\Cache\\**",
                        "**\\Logs\\**",
                        "**\\Temp\\**"
                    ],
                    HashMode = HashMode.Smart,
                    MaximumDepth = 8
                },
                ["registry"] = new()
                {
                    Roots =
                    [
                        "HKCU\\Software",
                        "HKLM64\\Software",
                        "HKLM32\\Software"
                    ],
                    MaximumDepth = 6,
                    MaximumArtifacts = 250_000
                },
                ["services"] = new(),
                ["scheduled-tasks"] = new(),
                ["startup"] = new(),
                ["environment"] = new()
            }
        };

    private static CaptureProfile CreateFull() =>
        new()
        {
            Name = "full",
            Description = "Расширенный ресурсоёмкий профиль с полным хешированием.",
            Providers = new Dictionary<string, ProviderOptions>(StringComparer.OrdinalIgnoreCase)
            {
                ["filesystem"] = new()
                {
                    Roots =
                    [
                        "%SystemDrive%\\",
                        "%ProgramFiles%",
                        "%ProgramFiles(x86)%",
                        "%ProgramData%",
                        "%UserProfile%"
                    ],
                    Exclude =
                    [
                        "**\\$Recycle.Bin\\**",
                        "**\\System Volume Information\\**",
                        "**\\pagefile.sys",
                        "**\\hiberfil.sys"
                    ],
                    HashMode = HashMode.Full,
                    MaximumDepth = 32,
                    MaximumArtifacts = 1_500_000
                },
                ["registry"] = new()
                {
                    Roots =
                    [
                        "HKCU",
                        "HKLM64",
                        "HKLM32",
                        "HKCR64",
                        "HKCR32"
                    ],
                    MaximumDepth = 32,
                    MaximumArtifacts = 1_000_000
                },
                ["services"] = new(),
                ["scheduled-tasks"] = new(),
                ["startup"] = new(),
                ["environment"] = new()
            }
        };
}
