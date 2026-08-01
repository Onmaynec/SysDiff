[CmdletBinding()]
param(
    [string]$Executable = ".\artifacts\publish\win-x64\sysdiff.exe",
    [string]$ExpectedVersion = "0.5.0"
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
if ($LASTEXITCODE -ne 0 -or $helpOutput -notmatch "SYSDIFF CYBER CONSOLE 0.5") {
    throw "Команда --help не содержит справку Cyber Console 0.5"
}

$doctorOutput = (& $Executable doctor | Out-String)
if ($LASTEXITCODE -ne 0 -or $doctorOutput -notmatch "Диагностика SysDiff") {
    throw "Команда doctor не прошла smoke-проверку"
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
if ($LASTEXITCODE -ne 0 -or $tuiOutput -notmatch "SYSDIFF CYBER CONSOLE 0.5.0") {
    throw "Cyber Console smoke frame не сформирован"
}
if ($tuiOutput -notmatch "ACTION CONSOLE" -or $tuiOutput -notmatch "COMMAND DECK") {
    throw "Smoke frame не содержит ключевые блоки Cyber Console"
}

Write-Host "✅ Smoke-тест SysDiff $ExpectedVersion и Cyber Console пройден." -ForegroundColor Green
