namespace SysDiff.Cli;

public enum UpdateStatus
{
    Unknown,
    Disabled,
    NotDue,
    Current,
    Available,
    Downloaded,
    InstallScheduled,
    Error
}

public sealed record UpdateSettings
{
    public bool AutoCheck { get; init; } = true;

    public bool AutoDownload { get; init; }

    public int CheckIntervalHours { get; init; } = 24;

    public string Channel { get; init; } = "stable";

    public DateTimeOffset? LastCheckedAtUtc { get; init; }

    public string? IgnoredVersion { get; init; }
}

public sealed record UpdateState
{
    public UpdateStatus Status { get; init; } = UpdateStatus.Unknown;

    public DateTimeOffset? LastCheckedAtUtc { get; init; }

    public ReleaseManifest? Manifest { get; init; }

    public string? PackagePath { get; init; }

    public string? StagingDirectory { get; init; }

    public string? Error { get; init; }
}

public sealed record UpdateCheckResult(
    UpdateStatus Status,
    string CurrentVersion,
    ReleaseManifest? Manifest,
    string? Message)
{
    public bool IsUpdateAvailable => Status is UpdateStatus.Available or UpdateStatus.Downloaded;
}

public sealed record UpdateDownloadResult(
    ReleaseManifest Manifest,
    string PackagePath,
    string StagingDirectory,
    string ExecutablePath);

public sealed record UpdateInstallPlan(
    string Version,
    string SourceExecutable,
    string TargetExecutable,
    string BackupExecutable,
    string HelperScript,
    string LogPath,
    bool RestartAfterInstall);
