[CmdletBinding()]
param(
    [string]$Executable = ".\artifacts\publish\win-x64\sysdiff.exe"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $Executable)) {
    throw "Файл не найден: $Executable"
}

& $Executable --version
if ($LASTEXITCODE -ne 0) {
    throw "Команда --version завершилась с кодом $LASTEXITCODE"
}

& $Executable --help
if ($LASTEXITCODE -ne 0) {
    throw "Команда --help завершилась с кодом $LASTEXITCODE"
}

Write-Host "✅ Smoke-тест пройден." -ForegroundColor Green
