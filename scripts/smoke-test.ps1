[CmdletBinding()]
param(
    [string]$Executable = ".\artifacts\publish\win-x64\sysdiff.exe",
    [string]$ExpectedVersion = "0.11.0"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot

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
if ($LASTEXITCODE -ne 0 -or $helpOutput -notmatch "LEGACY BRIDGE 0.11") {
    throw "Команда --help не содержит Legacy Bridge 0.11"
}
if ($helpOutput -notmatch "legacy convert" -or
    $helpOutput -notmatch "SCHEMA CONTRACT CENTER 0.10" -or
    $helpOutput -notmatch "schema validate" -or
    $helpOutput -notmatch "MIGRATION LAB 0.9" -or
    $helpOutput -notmatch "migration apply --yes" -or
    $helpOutput -notmatch "compatibility inspect" -or
    $helpOutput -notmatch "update install --yes" -or
    $helpOutput -notmatch "DRIFT OPERATIONS 0.6") {
    throw "Команда --help не содержит legacy, schema, migration, compatibility, updater и Drift Operations"
}

$legacyMatrix = (& $Executable legacy matrix --json | Out-String)
if ($LASTEXITCODE -ne 0 -or
    $legacyMatrix -notmatch '"productVersion": "0.11.0"' -or
    $legacyMatrix -notmatch '"targetSchemaVersion": 1' -or
    $legacyMatrix -notmatch '"sourceRange": "0.3.0-0.9.x"' -or
    $legacyMatrix -notmatch '"automaticBackup": true' -or
    $legacyMatrix -notmatch '"postConversionValidation": true') {
    throw "Команда legacy matrix --json не прошла smoke-проверку"
}

$legacySmoke = Join-Path $root "artifacts\smoke-legacy"
Remove-Item $legacySmoke -Recurse -Force -ErrorAction SilentlyContinue
New-Item $legacySmoke -ItemType Directory -Force | Out-Null
try {
    $legacyInput = Join-Path $legacySmoke "comparison-legacy.json"
    $legacyOutput = Join-Path $legacySmoke "comparison-v1.json"
    Copy-Item (Join-Path $root "tests\fixtures\legacy\v0.9\comparison-report.legacy.json") $legacyInput

    $legacyPlan = (& $Executable legacy plan comparison $legacyInput --json | Out-String)
    if ($LASTEXITCODE -ne 0 -or
        $legacyPlan -notmatch '"status": "UpgradeAvailable"' -or
        $legacyPlan -notmatch '"requiresBackup": true' -or
        $legacyPlan -notmatch '0.11.0-comparison-contract-v1') {
        throw "Legacy comparison plan не прошёл smoke-проверку"
    }

    $legacyConvert = (& $Executable legacy convert comparison $legacyInput --output $legacyOutput --yes --json | Out-String)
    if ($LASTEXITCODE -ne 0 -or
        $legacyConvert -notmatch '"success": true' -or
        $legacyConvert -notmatch '"changed": true' -or
        $legacyConvert -notmatch '"statusAfter": "Current"' -or
        $legacyConvert -notmatch '"backupPath":') {
        throw "Legacy comparison conversion не прошёл smoke-проверку"
    }
    if (-not (Test-Path $legacyOutput)) {
        throw "Legacy Bridge не создал output"
    }
    $backups = @(Get-ChildItem $legacySmoke -Filter "*.legacy-backup-*.json")
    if ($backups.Count -ne 1) {
        throw "Legacy Bridge должен создать ровно один backup"
    }

    $legacyVerify = (& $Executable legacy verify comparison $legacyOutput --json | Out-String)
    if ($LASTEXITCODE -ne 0 -or
        $legacyVerify -notmatch '"status": "Current"' -or
        $legacyVerify -notmatch '"sourceShape": "comparison-report-v1"') {
        throw "Преобразованный comparison report не прошёл legacy verify"
    }
}
finally {
    Remove-Item $legacySmoke -Recurse -Force -ErrorAction SilentlyContinue
}

$schemaCatalog = (& $Executable schema list --json | Out-String)
if ($LASTEXITCODE -ne 0 -or
    $schemaCatalog -notmatch '"productVersion": "0.11.0"' -or
    $schemaCatalog -notmatch '"contractVersion": 1' -or
    $schemaCatalog -notmatch '"jsonSchemaDraft": "2020-12"' -or
    $schemaCatalog -notmatch '"key": "snapshot"' -or
    $schemaCatalog -notmatch '"key": "comparison"' -or
    $schemaCatalog -notmatch '"key": "bundle"') {
    throw "Команда schema list --json не прошла smoke-проверку"
}

$schemaDocument = (& $Executable schema show snapshot | Out-String)
if ($LASTEXITCODE -ne 0 -or
    $schemaDocument -notmatch 'https://json-schema.org/draft/2020-12/schema' -or
    $schemaDocument -notmatch 'snapshot.schema.json' -or
    $schemaDocument -notmatch '"stability": "stable"') {
    throw "Команда schema show snapshot не вернула embedded public schema"
}

$schemaFixtures = @(
    @{ Kind = "snapshot"; Path = Join-Path $root "tests\fixtures\schema\v1\snapshot.valid.json" },
    @{ Kind = "comparison"; Path = Join-Path $root "tests\fixtures\schema\v1\comparison-report.valid.json" },
    @{ Kind = "bundle"; Path = Join-Path $root "tests\fixtures\schema\v1\investigation-bundle-manifest.valid.json" }
)
foreach ($fixture in $schemaFixtures) {
    $validation = (& $Executable schema validate $fixture.Kind $fixture.Path --json | Out-String)
    if ($LASTEXITCODE -ne 0 -or
        $validation -notmatch '"status": "Valid"' -or
        $validation -notmatch '"documentSchemaVersion": 1' -or
        $validation -notmatch '"isValid": true') {
        throw "Golden fixture $($fixture.Kind) не прошёл Schema Contract validation"
    }
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
    $compatibilityStatus -notmatch '"productVersion": "0.11.0"' -or
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
    $updateStatus -notmatch '"currentVersion": "0.11.0"' -or
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
if ($LASTEXITCODE -ne 0 -or $tuiOutput -notmatch "SYSDIFF CYBER CONSOLE 0.11.0") {
    throw "Cyber Console smoke frame не сформирован"
}
if ($tuiOutput -notmatch "LEGACY BRIDGE" -or
    $tuiOutput -notmatch "SOURCE: 0.3-0.9" -or
    $tuiOutput -notmatch "OUTPUT: ATOMIC" -or
    $tuiOutput -notmatch "SNAPSHOTS: PRESERVE") {
    throw "Smoke frame не содержит ключевые блоки Legacy Bridge"
}

Write-Host "✅ Smoke-тест SysDiff $ExpectedVersion и Legacy Bridge пройден." -ForegroundColor Green
