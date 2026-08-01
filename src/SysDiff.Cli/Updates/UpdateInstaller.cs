using System.Diagnostics;
using System.Text;

namespace SysDiff.Cli;

public sealed class UpdateInstaller
{
    private readonly AppPaths _paths;
    private readonly UpdateSettingsStore _store;

    public UpdateInstaller(AppPaths paths, UpdateSettingsStore store)
    {
        _paths = paths;
        _store = store;
    }

    public bool CanSelfUpdate(out string reason)
    {
        if (!OperatingSystem.IsWindows())
        {
            reason = "Self-update поддерживается только в Windows.";
            return false;
        }

        string? processPath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(processPath)
            || !Path.GetFileName(processPath).Equals("sysdiff.exe", StringComparison.OrdinalIgnoreCase))
        {
            reason = "Self-update доступен только для опубликованного sysdiff.exe, но не для dotnet run.";
            return false;
        }

        string? directory = Path.GetDirectoryName(processPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            reason = "Каталог установленного sysdiff.exe не найден.";
            return false;
        }

        reason = string.Empty;
        return true;
    }

    public async Task<UpdateInstallPlan> ScheduleAsync(
        bool restartAfterInstall,
        CancellationToken cancellationToken)
    {
        if (!CanSelfUpdate(out string reason))
        {
            throw new InvalidOperationException(reason);
        }

        UpdateState state = await _store.LoadStateAsync(cancellationToken);
        if (state.Status != UpdateStatus.Downloaded
            || state.Manifest is null
            || string.IsNullOrWhiteSpace(state.StagingDirectory))
        {
            throw new InvalidOperationException(
                "Сначала загрузите обновление командой sysdiff update download.");
        }

        string targetExecutable = Environment.ProcessPath!;
        UpdateInstallPlan plan = CreatePlan(
            state,
            targetExecutable,
            _paths.UpdatesDirectory,
            restartAfterInstall);
        Directory.CreateDirectory(_paths.UpdatesDirectory);
        await File.WriteAllTextAsync(
            plan.HelperScript,
            BuildHelperScript(),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            cancellationToken);

        var startInfo = new ProcessStartInfo
        {
            FileName = "powershell.exe",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = _paths.UpdatesDirectory
        };
        startInfo.ArgumentList.Add("-NoLogo");
        startInfo.ArgumentList.Add("-NoProfile");
        startInfo.ArgumentList.Add("-NonInteractive");
        startInfo.ArgumentList.Add("-ExecutionPolicy");
        startInfo.ArgumentList.Add("Bypass");
        startInfo.ArgumentList.Add("-File");
        startInfo.ArgumentList.Add(plan.HelperScript);
        startInfo.ArgumentList.Add("-ParentProcessId");
        startInfo.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("-SourceExecutable");
        startInfo.ArgumentList.Add(plan.SourceExecutable);
        startInfo.ArgumentList.Add("-TargetExecutable");
        startInfo.ArgumentList.Add(plan.TargetExecutable);
        startInfo.ArgumentList.Add("-BackupExecutable");
        startInfo.ArgumentList.Add(plan.BackupExecutable);
        startInfo.ArgumentList.Add("-ExpectedVersion");
        startInfo.ArgumentList.Add(plan.Version);
        startInfo.ArgumentList.Add("-LogPath");
        startInfo.ArgumentList.Add(plan.LogPath);
        startInfo.ArgumentList.Add("-RestartAfterInstall");
        startInfo.ArgumentList.Add(restartAfterInstall ? "true" : "false");

        _ = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Не удалось запустить update helper.");
        await _store.SaveStateAsync(
            state with { Status = UpdateStatus.InstallScheduled, Error = null },
            cancellationToken);
        return plan;
    }

    public static UpdateInstallPlan CreatePlan(
        UpdateState state,
        string targetExecutable,
        string updatesDirectory,
        bool restartAfterInstall)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetExecutable);
        ArgumentException.ThrowIfNullOrWhiteSpace(updatesDirectory);
        ReleaseManifest manifest = state.Manifest
            ?? throw new InvalidOperationException("Update state не содержит manifest.");
        if (string.IsNullOrWhiteSpace(state.StagingDirectory))
        {
            throw new InvalidOperationException("Update state не содержит staging directory.");
        }

        string sourceExecutable = Path.Combine(state.StagingDirectory, "sysdiff.exe");
        if (!File.Exists(sourceExecutable))
        {
            throw new FileNotFoundException("Staged sysdiff.exe не найден.", sourceExecutable);
        }

        string target = Path.GetFullPath(targetExecutable);
        string helper = Path.Combine(updatesDirectory, $"apply-{manifest.Version}.ps1");
        string log = Path.Combine(updatesDirectory, $"apply-{manifest.Version}.log");
        return new UpdateInstallPlan(
            manifest.Version,
            Path.GetFullPath(sourceExecutable),
            target,
            target + ".backup",
            Path.GetFullPath(helper),
            Path.GetFullPath(log),
            restartAfterInstall);
    }

    public static string BuildHelperScript() =>
        """
        param(
            [Parameter(Mandatory = $true)][int]$ParentProcessId,
            [Parameter(Mandatory = $true)][string]$SourceExecutable,
            [Parameter(Mandatory = $true)][string]$TargetExecutable,
            [Parameter(Mandatory = $true)][string]$BackupExecutable,
            [Parameter(Mandatory = $true)][string]$ExpectedVersion,
            [Parameter(Mandatory = $true)][string]$LogPath,
            [Parameter(Mandatory = $true)][bool]$RestartAfterInstall
        )

        $ErrorActionPreference = "Stop"

        function Write-UpdateLog([string]$Message) {
            $line = "{0:o} {1}" -f [DateTimeOffset]::UtcNow, $Message
            Add-Content -LiteralPath $LogPath -Value $line -Encoding UTF8
        }

        try {
            Write-UpdateLog "Waiting for SysDiff PID $ParentProcessId"
            try {
                Wait-Process -Id $ParentProcessId -Timeout 120 -ErrorAction Stop
            }
            catch {
                if (Get-Process -Id $ParentProcessId -ErrorAction SilentlyContinue) {
                    throw "SysDiff process did not exit within 120 seconds."
                }
            }

            Start-Sleep -Milliseconds 350
            if (-not (Test-Path -LiteralPath $SourceExecutable -PathType Leaf)) {
                throw "Staged executable not found: $SourceExecutable"
            }

            $targetDirectory = Split-Path -Parent $TargetExecutable
            if (-not (Test-Path -LiteralPath $targetDirectory -PathType Container)) {
                throw "Target directory not found: $targetDirectory"
            }

            if (Test-Path -LiteralPath $TargetExecutable -PathType Leaf) {
                Copy-Item -LiteralPath $TargetExecutable -Destination $BackupExecutable -Force
            }

            $pendingExecutable = "$TargetExecutable.pending"
            Copy-Item -LiteralPath $SourceExecutable -Destination $pendingExecutable -Force
            Move-Item -LiteralPath $pendingExecutable -Destination $TargetExecutable -Force

            $versionOutput = (& $TargetExecutable --version 2>&1 | Out-String).Trim()
            if ($LASTEXITCODE -ne 0 -or $versionOutput -ne "SysDiff $ExpectedVersion") {
                throw "Installed executable verification failed: $versionOutput"
            }

            Remove-Item -LiteralPath $BackupExecutable -Force -ErrorAction SilentlyContinue
            Write-UpdateLog "SysDiff $ExpectedVersion installed successfully."
            if ($RestartAfterInstall) {
                Start-Process -FilePath $TargetExecutable
            }
            exit 0
        }
        catch {
            Write-UpdateLog "Update failed: $($_.Exception.Message)"
            if (Test-Path -LiteralPath $BackupExecutable -PathType Leaf) {
                Copy-Item -LiteralPath $BackupExecutable -Destination $TargetExecutable -Force
                Write-UpdateLog "Previous executable restored."
            }
            exit 1
        }
        """;
}
