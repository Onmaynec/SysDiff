[CmdletBinding()]
param(
    [string]$Version = "0.9.0",
    [string]$Runtime = "win-x64"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$artifacts = Join-Path $root "artifacts"
$publish = Join-Path $artifacts "publish\$Runtime"
$assetName = "SysDiff-$Version-$Runtime.zip"
$packageRoot = Join-Path $artifacts "SysDiff-$Version-$Runtime"
$zipPath = Join-Path $artifacts $assetName
$checksumPath = "$zipPath.sha256"
$manifestPath = Join-Path $artifacts "release-manifest.json"

$projectPath = Join-Path $root "src\SysDiff.Cli\SysDiff.Cli.csproj"
[xml]$project = Get-Content $projectPath -Raw
$projectVersion = [string]$project.Project.PropertyGroup.Version
if ($projectVersion -ne $Version) {
    throw "Версия проекта $projectVersion не совпадает с package version $Version"
}
if ($Version -notmatch '^(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$') {
    throw "Версия должна быть стабильной SemVer X.Y.Z: $Version"
}
if ($Runtime -ne "win-x64") {
    throw "Официальный release channel 0.9 поддерживает только win-x64"
}

Remove-Item $publish, $packageRoot, $zipPath, $checksumPath, $manifestPath `
    -Recurse -Force -ErrorAction SilentlyContinue
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
        -p:Version=$Version `
        -p:PublishSingleFile=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:DebugType=None `
        -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish завершился с кодом $LASTEXITCODE"
    }

    Copy-Item (Join-Path $publish "sysdiff.exe") $packageRoot
    Copy-Item .\sysdiff.json $packageRoot
    Copy-Item .\LICENSE $packageRoot
    Copy-Item .\THIRD_PARTY_NOTICES.md $packageRoot
    Copy-Item .\README.md (Join-Path $packageRoot "README.txt")
    Copy-Item .\docs\UPDATES.md (Join-Path $packageRoot "UPDATES.txt")
    Copy-Item .\docs\COMPATIBILITY.md (Join-Path $packageRoot "COMPATIBILITY.txt")
    Copy-Item .\docs\MIGRATIONS.md (Join-Path $packageRoot "MIGRATIONS.txt")
    Copy-Item .\samples\profiles (Join-Path $packageRoot "profiles") -Recurse
    New-Item (Join-Path $packageRoot "portable.mode") -ItemType File | Out-Null

    Compress-Archive -Path "$packageRoot\*" -DestinationPath $zipPath -CompressionLevel Optimal
    $hash = (Get-FileHash $zipPath -Algorithm SHA256).Hash.ToLowerInvariant()
    $size = (Get-Item $zipPath).Length
    Set-Content $checksumPath "$hash  $assetName" -Encoding ascii

    $manifest = [ordered]@{
        schemaVersion = 1
        product = "SysDiff"
        version = $Version
        channel = "stable"
        runtime = $Runtime
        tag = "v$Version"
        assetName = $assetName
        assetUrl = "https://github.com/Onmaynec/SysDiff/releases/download/v$Version/$assetName"
        sha256 = $hash
        sizeBytes = $size
        minimumUpdaterVersion = "0.7.0"
        publishedAtUtc = [DateTimeOffset]::UtcNow.ToString("o")
        unsigned = $true
    }
    $manifest | ConvertTo-Json -Depth 5 | Set-Content $manifestPath -Encoding utf8NoBOM

    $roundTrip = Get-Content $manifestPath -Raw | ConvertFrom-Json
    if ($roundTrip.version -ne $Version -or
        $roundTrip.tag -ne "v$Version" -or
        $roundTrip.assetName -ne $assetName -or
        $roundTrip.sha256 -ne $hash -or
        [long]$roundTrip.sizeBytes -ne $size) {
        throw "Release manifest не прошёл round-trip validation"
    }

    Write-Host "✅ Архив: $zipPath" -ForegroundColor Green
    Write-Host "🔐 SHA-256: $hash" -ForegroundColor Green
    Write-Host "📜 Manifest: $manifestPath" -ForegroundColor Green
    Write-Host "⚠️  Authenticode: unsigned build (сертификат пока не настроен)" -ForegroundColor Yellow
}
finally {
    Pop-Location
}
