[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

Push-Location $root
try {
    Write-Host "🔧 Восстановление зависимостей..." -ForegroundColor Cyan
    dotnet restore .\SysDiff.sln

    Write-Host "🏗️ Сборка SysDiff..." -ForegroundColor Cyan
    dotnet build .\SysDiff.sln `
        --configuration $Configuration `
        --no-restore

    Write-Host "✅ Сборка завершена." -ForegroundColor Green
}
finally {
    Pop-Location
}
