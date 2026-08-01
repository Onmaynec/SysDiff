[CmdletBinding()]
param(
    [string]$Executable = ".\artifacts\publish\win-x64\sysdiff.exe",
    [string]$ExpectedVersion = "0.6.0"
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
if ($LASTEXITCODE -ne 0 -or $helpOutput -notmatch "SYSDIFF CYBER CONSOLE 0.6") {
    throw "Команда --help не содержит справку Cyber Console 0.6"
}
if ($helpOutput -notmatch "DRIFT OPERATIONS 0.6" -or $helpOutput -notmatch "baseline set") {
    throw "Команда --help не содержит Drift Operations"
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

$env:SYSDIFF_NO_ANIMATIONS = "1"
$env:NO_COLOR = "1"
try {
    $tuiOutput = (& $Executable --tui-smoke | Out-String)
}
finally {
    Remove-Item Env:SYSDIFF_NO_ANIMATIONS -ErrorAction SilentlyContinue
    Remove-Item Env:NO_COLOR -ErrorAction SilentlyContinue
}
if ($LASTEXITCODE -ne 0 -or $tuiOutput -notmatch "SYSDIFF CYBER CONSOLE 0.6.0") {
    throw "Cyber Console smoke frame не сформирован"
}
if ($tuiOutput -notmatch "DRIFT OPERATIONS" -or $tuiOutput -notmatch "BASELINE:" -or $tuiOutput -notmatch "ACTIVE CASE:") {
    throw "Smoke frame не содержит ключевые блоки Drift Operations"
}

Write-Host "✅ Smoke-тест SysDiff $ExpectedVersion и Drift Operations пройден." -ForegroundColor Green
