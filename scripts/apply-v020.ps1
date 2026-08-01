$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Set-Utf8File {
    param(
        [Parameter(Mandatory)] [string] $Path,
        [Parameter(Mandatory)] [string] $Content
    )

    [System.IO.File]::WriteAllText(
        (Resolve-Path $Path),
        $Content,
        [System.Text.UTF8Encoding]::new($false))
}

$commandPath = "src/SysDiff.Cli/CommandApp.cs"
$command = Get-Content $commandPath -Raw
$command = $command.Replace('Console.WriteLine("SysDiff 0.1.0");', 'Console.WriteLine("SysDiff 0.2.0");')

$watchMethod = @'
    private async Task<int> RunWatchAsync(
        string[] args,
        CancellationToken cancellationToken)
    {
        var reader = new ArgumentReader(args);
        string profileName = reader.Get("profile", "standard");
        CaptureProfile profile = _profiles.Get(profileName);
        string session = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        string beforeName = $"watch-{session}-before";
        string afterName = $"watch-{session}-after";
        bool waitForChildren = reader.Has("wait-for-children");
        int timeoutSeconds = reader.GetInt("timeout", 0);
        int delaySeconds = reader.GetInt("stabilization-delay", 3);

        if (timeoutSeconds < 0)
        {
            throw new ArgumentException("--timeout не может быть отрицательным.");
        }

        if (delaySeconds < 0)
        {
            throw new ArgumentException("--stabilization-delay не может быть отрицательным.");
        }

        Console.WriteLine("1/4 Создание начального снимка…");
        SnapshotRecord before = await _coordinator.CaptureAsync(
            beforeName,
            profile,
            _paths.DataDirectory,
            IsAdministrator(),
            progress: null,
            cancellationToken);

        bool timedOut = false;
        if (reader.Has("no-launch"))
        {
            Console.WriteLine();
            Console.WriteLine("Установите или запустите исследуемую программу.");
            Console.WriteLine("После завершения вернитесь в SysDiff и нажмите Enter.");
            Console.ReadLine();
        }
        else
        {
            string executable = RequirePosition(reader, 0, "Укажите исполняемый файл или --no-launch.");
            string expanded = Environment.ExpandEnvironmentVariables(executable);
            string workingDirectory = reader.Get(
                "working-directory",
                Path.GetDirectoryName(Path.GetFullPath(expanded)) ?? Environment.CurrentDirectory);

            var startInfo = new ProcessStartInfo
            {
                FileName = expanded,
                Arguments = reader.Get("arguments", string.Empty),
                WorkingDirectory = workingDirectory,
                UseShellExecute = true
            };

            Console.WriteLine($"2/4 Запуск: {expanded}");
            using Process process = Process.Start(startInfo)
                ?? throw new InvalidOperationException("Не удалось запустить процесс.");

            TimeSpan? timeout = timeoutSeconds > 0
                ? TimeSpan.FromSeconds(timeoutSeconds)
                : null;
            ProcessTreeWaitResult waitResult = await ProcessTreeWaiter.WaitAsync(
                process,
                waitForChildren,
                timeout,
                cancellationToken);

            timedOut = waitResult.TimedOut;
            Console.WriteLine(
                $"Процессов замечено: {waitResult.ObservedProcesses}; длительность: {waitResult.Duration:g}; код: {waitResult.ExitCode?.ToString() ?? "н/д"}.");

            if (timedOut)
            {
                Console.WriteLine("⚠ Тайм-аут ожидания достигнут. Процессы не завершаются автоматически; итоговый снимок будет создан сейчас.");
            }
        }

        if (delaySeconds > 0)
        {
            Console.WriteLine($"3/4 Ожидание стабилизации: {delaySeconds} сек.");
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
        }

        Console.WriteLine("4/4 Создание итогового снимка и сравнение…");
        SnapshotRecord after = await _coordinator.CaptureAsync(
            afterName,
            profile,
            _paths.DataDirectory,
            IsAdministrator(),
            progress: null,
            cancellationToken);

        NoiseMode noiseMode = Enum.TryParse(
            reader.Get("noise", "Balanced"),
            ignoreCase: true,
            out NoiseMode parsedNoise)
            ? parsedNoise
            : throw new ArgumentException("Допустимые режимы шума: Raw, Balanced, Strict.");

        ComparisonResult comparison = _comparisonEngine.Compare(before, after, noiseMode);
        await _store.SaveComparisonAsync(comparison, cancellationToken);

        string reportPath = reader.Get(
            "report",
            Path.Combine(_paths.ReportsDirectory, $"watch-{session}.html"));
        string html = _htmlRenderer.Render(before, after, comparison);
        string fullReportPath = Path.GetFullPath(reportPath);
        Directory.CreateDirectory(Path.GetDirectoryName(fullReportPath) ?? ".");
        await File.WriteAllTextAsync(fullReportPath, html, cancellationToken);

        Console.WriteLine($"Готово. Найдено изменений: {comparison.Changes.Count:N0}");
        Console.WriteLine($"Скрыто как шум: {comparison.HiddenAsNoise:N0}");
        Console.WriteLine($"HTML-отчёт: {fullReportPath}");

        return timedOut
            || before.Status == SnapshotStatus.Partial
            || after.Status == SnapshotStatus.Partial
            ? 7
            : 0;
    }

    private async Task<int> RunDoctorAsync
'@

$pattern = '(?s)    private async Task<int> RunWatchAsync\(.+?\n    private async Task<int> RunDoctorAsync'
$updated = [System.Text.RegularExpressions.Regex]::Replace($command, $pattern, $watchMethod, 1)
if ($updated -eq $command) {
    throw "Не удалось заменить RunWatchAsync в CommandApp.cs"
}
Set-Utf8File -Path $commandPath -Content $updated

$packagePath = "scripts/package.ps1"
$package = Get-Content $packagePath -Raw
$package = $package.Replace('[string] $Version = "0.1.0"', '[string] $Version = "0.2.0"')
Set-Utf8File -Path $packagePath -Content $package

$readmePaths = @("README.md", "README_RU.md") | Where-Object { Test-Path $_ }
foreach ($readmePath in $readmePaths) {
    $readme = Get-Content $readmePath -Raw
    $readme = $readme.Replace("## ✨ Возможности MVP", "## ✨ Возможности 0.2.0")
    $readme = $readme.Replace("SysDiff-0.1.0-win-x64.zip", "SysDiff-0.2.0-win-x64.zip")
    $readme = $readme.Replace("SysDiff-0.1.0-win-x64.zip.sha256", "SysDiff-0.2.0-win-x64.zip.sha256")
    $readme = $readme.Replace(
        "- 👀 сценарий `watch` для запуска установщика между снимками;",
        "- 👀 сценарий `watch` с тайм-аутом и ожиданием дерева дочерних процессов;`n- 🧱 снимки Windows Firewall, установленных приложений, драйверов и сертификатов;")
    $readme = $readme.Replace(
        "| `environment` | пользовательские и системные переменные, элементы PATH по отдельности | MVP |",
        "| `environment` | пользовательские и системные переменные, элементы PATH по отдельности | 0.1 |`n| `firewall` | правила, направления, действия, профили, порты, адреса и программы | 0.2 |`n| `installed-apps` | приложения из uninstall-разделов HKCU/HKLM, x86/x64 | 0.2 |`n| `drivers` | системные драйверы, пути, состояния, SHA-256 и подписи | 0.2 |`n| `certificates` | хранилища Windows, сроки, назначения и доверие без экспорта ключей | 0.2 |")
    $readme = $readme.Replace(
        "- Текущий `watch` ожидает завершения основного процесса и не гарантирует ожидание всех дочерних процессов.",
        "- `watch --wait-for-children` отслеживает дерево процессов по снимкам Toolhelp, но не внедряется в процессы и может не увидеть очень короткоживущий процесс между опросами.")
    $readme = $readme.Replace(
        "- Подписи файлов, ACL, Firewall, приложения, драйверы и сертификаты запланированы для следующих версий.",
        "- Проверка подписи драйвера в 0.2.0 подтверждает наличие читаемого сертификата, но не заменяет полную проверку доверия Authenticode.")
    $readme = $readme.Replace(
        "- **0.2.0:** Firewall, Installed Apps, драйверы, сертификаты, улучшенный `watch`;",
        "- **0.2.0:** Firewall, Installed Apps, драйверы, сертификаты и улучшенный `watch` — готово;")
    Set-Utf8File -Path $readmePath -Content $readme
}
