[CmdletBinding()]
param(
    [string]$Executable = ".\artifacts\publish\win-x64\sysdiff.exe",
    [string]$ExpectedVersion = "0.9.0"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Executable)) {
    throw "Файл не найден: $Executable"
}

$versionOutput = (& $Executable --version | Out-String).Trim()
if ($LASTEXITCODE -ne 0) {
    throw "Команда --version завершилась с кодом $LASTEXITCODE"
}
if ($versionOutput -ne "SysDiff $ExpectedVersion") {
    throw "Ожидалась версия SysDiff $ExpectedVersion, получено: $versionOutput"
}

$fileVersion = (Get-Item $Executable).VersionInfo.ProductVersion
if (-not $fileVersion.StartsWith($ExpectedVersion, [System.StringComparison]::Ordinal)) {
    throw "Метаданные sysdiff.exe содержат версию $fileVersion вместо $ExpectedVersion"
}

$helpOutput = (& $Executable --help | Out-String)
if ($LASTEXITCODE -ne 0 -or $helpOutput -notmatch "MIGRATION LAB 0.9") {
    throw "Команда --help не содержит Migration Lab 0.9"
}
if ($helpOutput -notmatch "migration apply --yes" -or
    $helpOutput -notmatch "compatibility inspect" -or
    $helpOutput -notmatch "update install --yes" -or
    $helpOutput -notmatch "DRIFT OPERATIONS 0.6") {
    throw "Команда --help не содержит migration, compatibility, updater и Drift Operations"
}

$migrationStatus = (& $Executable migration status --json | Out-String)
if ($LASTEXITCODE -ne 0 -or
    $migrationStatus -notmatch '"status": "Current"' -or
    $migrationStatus -notmatch '"userVersion": 9' -or
    $migrationStatus -notmatch '"supportedUserVersion": 9' -or
    $migrationStatus -notmatch '"pendingMigrations": \[\]') {
    throw "Команда migration status --json не прошла smoke-проверку"
}

$migrationHistory = (& $Executable migration history --json | Out-String)
if ($LASTEXITCODE -ne 0 -or
    $migrationHistory -notmatch '"id": "0.9.0-migration-lab"' -or
    $migrationHistory -notmatch '"status": "Applied"') {
    throw "Команда migration history --json не прошла smoke-проверку"
}

$compatibilityStatus = (& $Executable compatibility status --json | Out-String)
if ($LASTEXITCODE -ne 0 -or
    $compatibilityStatus -notmatch '"productVersion": "0.9.0"' -or
    $compatibilityStatus -notmatch '"currentFormatVersion": 1' -or
    $compatibilityStatus -notmatch '"currentSchemaVersion": 1') {
    throw "Команда compatibility status --json не прошла smoke-проверку"
}

$doctorOutput = (& $Executable doctor | Out-String)
if ($LASTEXITCODE -ne 0 -or $doctorOutput -notmatch "Диагностика SysDiff") {
    throw "Команда doctor не прошла smoke-проверку"
}

$timelineOutput = (& $Executable timeline list --limit 5 | Out-String)
if ($LASTEXITCODE -ne 0 -or $timelineOutput -notmatch "Timeline пока пуста|Snapshot:|Comparison:") {
    throw "Команда timeline list не прошла smoke-проверку"
}

$caseOutput = (& $Executable case list | Out-String)
if ($LASTEXITCODE -ne 0 -or $caseOutput -notmatch "Кейсов пока нет") {
    throw "Команда case list не прошла smoke-проверку"
}

$updateStatus = (& $Executable update status --json | Out-String)
if ($LASTEXITCODE -ne 0 -or
    $updateStatus -notmatch '"currentVersion": "0.9.0"' -or
    $updateStatus -notmatch '"status":') {
    throw "Команда update status --json не прошла smoke-проверку"
}

$updateSettings = (& $Executable update settings --auto-check false --auto-download false --interval-hours 24 --json | Out-String)
if ($LASTEXITCODE -ne 0 -or
    $updateSettings -notmatch '"autoCheck": false' -or
    $updateSettings -notmatch '"autoDownload": false' -or
    $updateSettings -notmatch '"checkIntervalHours": 24') {
    throw "Команда update settings не прошла smoke-проверку"
}

$env:SYSDIFF_NO_ANIMATIONS = "1"
$env:NO_COLOR = "1"
try {
    $tuiOutput = (& $Executable --tui-smoke | Out-String)
}
finally {
    Remove-Item Env:SYSDIFF_NO_ANIMATIONS -ErrorAction SilentlyContinue
    Remove-Item Env:NO_COLOR -ErrorAction SilentlyContinue
}
if ($LASTEXITCODE -ne 0 -or $tuiOutput -notmatch "SYSDIFF CYBER CONSOLE 0.9.0") {
    throw "Cyber Console smoke frame не сформирован"
}
if ($tuiOutput -notmatch "MIGRATION LAB" -or
    $tuiOutput -notmatch "DRY-RUN: DEFAULT" -or
    $tuiOutput -notmatch "APPLY: CONFIRM" -or
    $tuiOutput -notmatch "ROLLBACK: ATOMIC") {
    throw "Smoke frame не содержит ключевые блоки Migration Lab"
}

Write-Host "✅ Smoke-тест SysDiff $ExpectedVersion и Migration Lab пройден." -ForegroundColor Green
