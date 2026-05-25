param(
    [string]$Configuration = "Debug",
    [switch]$CleanAfter,
    [switch]$IncludeProjectBuildDirs,
    [switch]$IncludeSharedBuildCache,
    [switch]$IncludeLogs
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src\StationApp.UI\StationApp.UI.csproj"
$cleanupScriptPath = Join-Path $PSScriptRoot "cleanup-build-artifacts.ps1"

Write-Host "Building StationApp.UI ($Configuration)..."

dotnet build $projectPath `
    -m:1 `
    -c $Configuration `
    -p:SkipDatabaseSchemaUpdate=true

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
