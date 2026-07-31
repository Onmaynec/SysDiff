[CmdletBinding()]
param(
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

Push-Location $root
try {
    dotnet restore .\SysDiff.sln
    dotnet test .\SysDiff.sln `
        --configuration $Configuration `
        --no-restore `
        --collect:"XPlat Code Coverage"

    Get-ChildItem .\schemas\*.json | ForEach-Object {
        $null = Get-Content $_.FullName -Raw | ConvertFrom-Json
        Write-Host "✅ JSON корректен: $($_.Name)" -ForegroundColor Green
    }
}
finally {
    Pop-Location
}
