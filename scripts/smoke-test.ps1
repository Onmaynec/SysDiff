[CmdletBinding()]
param(
    [string]$Executable = ".\artifacts\publish\win-x64\sysdiff.exe",
    [string]$ExpectedVersion = "0.7.0"
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
if ($LASTEXITCODE -ne 0 -or $helpOutput -notmatch "RELEASE CHANNEL 0.7") {
    throw "Команда --help не содержит Release Channel 0.7"
}
if ($helpOutput -notmatch "update check" -or
    $helpOutput -notmatch "update install --yes" -or
    $helpOutput -notmatch "DRIFT OPERATIONS 0.6") {
    throw "Команда --help не содержит updater и Drift Operations"
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
    $updateStatus -notmatch '"currentVersion": "0.7.0"' -or
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
if ($LASTEXITCODE -ne 0 -or $tuiOutput -notmatch "SYSDIFF CYBER CONSOLE 0.7.0") {
    throw "Cyber Console smoke frame не сформирован"
}
if ($tuiOutput -notmatch "RELEASE CHANNEL" -or
    $tuiOutput -notmatch "UPDATE CENTER" -or
    $tuiOutput -notmatch "SHA-256" -or
    $tuiOutput -notmatch "ROLLBACK SAFE") {
    throw "Smoke frame не содержит ключевые блоки Release Channel"
}

Write-Host "✅ Smoke-тест SysDiff $ExpectedVersion и Release Channel пройден." -ForegroundColor Green
