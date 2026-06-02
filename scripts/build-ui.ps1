param(
    [string]$Configuration = "Debug",
    [string]$RuntimeIdentifier = "",
    [string]$ConfigPath = "src/StationApp.UI/appsettings.json",
    [string]$AppVersion = "",
    [switch]$SkipDatabaseSchemaUpdate,
    [switch]$CleanAfter,
    [switch]$IncludeProjectBuildDirs,
    [switch]$IncludeSharedBuildCache,
    [switch]$IncludeLogs
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
$cleanupScriptPath = Join-Path $PSScriptRoot "cleanup-build-artifacts.ps1"

Write-Host "Building StationApp.UI ($Configuration)..."

$buildArgs = @(
    "build"
    $projectPath
    "-m:1"
    "-c"
    $Configuration
    "-p:StationAppBuildRoot=$buildRoot"
)

if (-not [string]::IsNullOrWhiteSpace($AppVersion)) {
    Assert-StationAppSemVer -VersionText $AppVersion
    $buildArgs += "-p:StationAppVersion=$AppVersion"
}

if (-not $SkipDatabaseSchemaUpdate) {
    $buildArgs += "-p:RunDatabaseSchemaUpdate=true"
    $buildArgs += "-p:DatabaseSchemaConfigPath=$ConfigPath"

    if (-not [string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
        $buildArgs += "-p:RuntimeIdentifier=$RuntimeIdentifier"
    }

    Write-Host "Database schema update is enabled. StationApp.DbMigrator will run after build."
    Write-Host "ConfigPath: $ConfigPath"
}
else {
    $buildArgs += "-p:SkipDatabaseSchemaUpdate=true"
    Write-Host "Database schema update is skipped for this build."
}

Invoke-DotnetCommand -Arguments $buildArgs

if (-not $CleanAfter) {
    return
}

if (-not (Test-Path $cleanupScriptPath)) {
    throw "Cleanup script not found: $cleanupScriptPath"
}

$cleanupArgs = @()

if ($IncludeProjectBuildDirs) {
    $cleanupArgs += "-IncludeProjectBuildDirs"
}

if ($IncludeSharedBuildCache) {
    $cleanupArgs += "-IncludeSharedBuildCache"
}

if ($IncludeLogs) {
    $cleanupArgs += "-IncludeLogs"
}

Write-Host "Cleaning generated build artifacts..."

& powershell -NoProfile -ExecutionPolicy Bypass -File $cleanupScriptPath @cleanupArgs
