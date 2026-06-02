param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "",
    [string]$ConfigPath = "src/StationApp.UI/appsettings.json",
    [string]$PublishDir = "",
    [string]$AppVersion = "",
    [string]$ReleaseNotes = "",
    [switch]$SelfContained,
    [switch]$SkipDatabaseSchemaUpdate,
    [switch]$PublishSingleFile,
    [switch]$TimestampedBuildFolder
)

$ErrorActionPreference = "Stop"

function Assert-StationAppSemVer {
    param(
        [Parameter(Mandatory = $true)]
        [string]$VersionText
    )

    if ($VersionText.Trim() -notmatch '^\d+\.\d+\.\d+$') {
        throw "AppVersion phai dung dinh dang semantic version x.y.z. Vi du: 1.0.0 hoac 1.0.1. Gia tri hien tai: $VersionText"
    }
}

function Invoke-DotnetCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments[0]) that bai voi exit code $LASTEXITCODE."
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$runId = Get-Date -Format "yyyyMMdd_HHmmss_fff"
$buildRoot = Join-Path $env:TEMP "StationAppBuild\$runId\"
New-Item -ItemType Directory -Path $buildRoot -Force | Out-Null
$projectPath = Join-Path $repoRoot "src\StationApp.UI\StationApp.UI.csproj"
$updaterProjectPath = Join-Path $repoRoot "src\StationApp.Updater\StationApp.Updater.csproj"
$migratorProjectPath = Join-Path $repoRoot "src\StationApp.DbMigrator\StationApp.DbMigrator.csproj"
$schemaUpdateScriptPath = Join-Path $PSScriptRoot "update-local-db-schema.ps1"

if (-not (Test-Path $projectPath)) {
    throw "UI project not found: $projectPath"
}

if (-not (Test-Path $schemaUpdateScriptPath)) {
    throw "Schema update script not found: $schemaUpdateScriptPath"
}

if ([string]::IsNullOrWhiteSpace($PublishDir)) {
    $now = Get-Date -Format "yyyyMMdd_HHmmss"
    $PublishDir = Join-Path $repoRoot "Builds\StationApp_$AppVersion`_$now"
}

if ([string]::IsNullOrWhiteSpace($AppVersion)) {
    throw "Bat buoc truyen -AppVersion theo dinh dang x.y.z khi phat hanh. Vi du: -AppVersion 1.0.1"
}

Assert-StationAppSemVer -VersionText $AppVersion

if (-not [string]::IsNullOrWhiteSpace($ReleaseNotes)) {
    Write-Host "ReleaseNotes is accepted but only used by shared-folder release manifest generation."
}

Write-Host "Step 1/3: Build StationApp.UI and StationApp.DbMigrator ($Configuration)..."

$buildArgs = @(
    "build"
    $projectPath
    "-m:1"
    "-c"
    $Configuration
    "-p:SkipDatabaseSchemaUpdate=true"
    "-p:StationAppBuildRoot=$buildRoot"
)

if (-not [string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
    $buildArgs += "-p:RuntimeIdentifier=$RuntimeIdentifier"
}

$buildArgs += "-p:StationAppVersion=$AppVersion"

Invoke-DotnetCommand -Arguments $buildArgs

if (-not $SkipDatabaseSchemaUpdate) {
    Write-Host "Step 2/3: Run StationApp.DbMigrator to update local database schema and SQL objects..."

    $schemaArgs = @{
        Configuration = $Configuration
        ConfigPath = $ConfigPath
    }

    if (-not [string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
        $schemaArgs["RuntimeIdentifier"] = $RuntimeIdentifier
    }

    & powershell -NoProfile -ExecutionPolicy Bypass -File $schemaUpdateScriptPath @schemaArgs
}
else {
    Write-Host "Step 2/3: Skip database schema update."
}

Write-Host "Step 3/3: Publish StationApp.UI..."

$publishArgs = @(
    "publish"
    $projectPath
    "-c"
    $Configuration
    "-p:SkipDatabaseSchemaUpdate=true"
    "-p:StationAppVersion=$AppVersion"
    "-p:StationAppBuildRoot=$buildRoot"
)

if (-not [string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
    $publishArgs += "-r"
    $publishArgs += $RuntimeIdentifier
}

if (-not [string]::IsNullOrWhiteSpace($PublishDir)) {
    $publishArgs += "-o"
    $publishArgs += $PublishDir
}

if ($SelfContained) {
    $publishArgs += "--self-contained"
    $publishArgs += "true"
}
elseif (-not [string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
    $publishArgs += "--no-self-contained"
}

if ($PublishSingleFile) {
    $publishArgs += "-p:PublishSingleFile=true"
}
else {
    $publishArgs += "-p:PublishSingleFile=false"
}

Invoke-DotnetCommand -Arguments $publishArgs

Write-Host "Publishing StationApp.Updater tool..."

$updaterPublishDir = Join-Path $PublishDir "Tools\Updater"
$updaterArgs = @(
    "publish"
    $updaterProjectPath
    "-c"
    $Configuration
    "-r"
    $(if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier)) { "win-x64" } else { $RuntimeIdentifier })
    "--self-contained"
    "true"
    "-p:PublishSingleFile=true"
    "-p:StationAppVersion=$AppVersion"
    "-p:StationAppBuildRoot=$buildRoot"
    "-o"
    $updaterPublishDir
)

Invoke-DotnetCommand -Arguments $updaterArgs

Write-Host "Publishing StationApp.DbMigrator tool..."

$migratorPublishDir = Join-Path $PublishDir "Tools\DbMigrator"
$migratorArgs = @(
    "publish"
    $migratorProjectPath
    "-c"
    $Configuration
    "-r"
    $(if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier)) { "win-x64" } else { $RuntimeIdentifier })
    "--self-contained"
    "true"
    "-p:PublishSingleFile=true"
    "-p:StationAppVersion=$AppVersion"
    "-p:StationAppBuildRoot=$buildRoot"
    "-o"
    $migratorPublishDir
)

Invoke-DotnetCommand -Arguments $migratorArgs

Write-Host "Release publish completed."
Write-Host "AppVersion: $AppVersion"
if (-not [string]::IsNullOrWhiteSpace($PublishDir)) {
    Write-Host "PublishDir: $PublishDir"
}
