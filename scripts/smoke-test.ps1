[CmdletBinding()]
param(
    [string]$Executable = ".\artifacts\publish\win-x64\sysdiff.exe",
    [string]$ExpectedVersion = "0.2.0"
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

& $Executable --help
if ($LASTEXITCODE -ne 0) {
    throw "Команда --help завершилась с кодом $LASTEXITCODE"
}

Write-Host "✅ Smoke-тест SysDiff $ExpectedVersion пройден." -ForegroundColor Green
