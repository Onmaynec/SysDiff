[CmdletBinding()]
param(
    [string]$Version = "0.6.0",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "publish\$Runtime"
$packageRoot = Join-Path $artifacts "SysDiff-$Version-$Runtime"
$zipPath = Join-Path $artifacts "SysDiff-$Version-$Runtime.zip"

Remove-Item $publish, $packageRoot, $zipPath -Recurse -Force -ErrorAction SilentlyContinue
New-Item $publish -ItemType Directory -Force | Out-Null
New-Item $packageRoot -ItemType Directory -Force | Out-Null

Push-Location $root
try {
    Write-Host "📦 Публикация portable-сборки SysDiff $Version..." -ForegroundColor Cyan
    dotnet publish .\src\SysDiff.Cli\SysDiff.Cli.csproj `
        --configuration Release `
        --runtime $Runtime `
        --self-contained true `
        --output $publish `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false

    Copy-Item (Join-Path $publish "sysdiff.exe") $packageRoot
    Copy-Item .\sysdiff.json $packageRoot
    Copy-Item .\LICENSE $packageRoot
    Copy-Item .\THIRD_PARTY_NOTICES.md $packageRoot
    Copy-Item .\README.md (Join-Path $packageRoot "README.txt")
    Copy-Item .\samples\profiles (Join-Path $packageRoot "profiles") -Recurse
    New-Item (Join-Path $packageRoot "portable.mode") -ItemType File | Out-Null

    Compress-Archive -Path "$packageRoot\*" -DestinationPath $zipPath -CompressionLevel Optimal
    $hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    Set-Content "$zipPath.sha256" "$hash  $(Split-Path $zipPath -Leaf)" -Encoding ascii

    Write-Host "✅ Архив: $zipPath" -ForegroundColor Green
    Write-Host "🔐 SHA-256: $hash" -ForegroundColor Green
}
finally {
    Pop-Location
}
