param()

$ErrorActionPreference = "Stop"

$scriptRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$cleanupScript = Join-Path $scriptRoot "cleanup-build-artifacts.ps1"

if (-not (Test-Path -LiteralPath $cleanupScript)) {
    throw "Khong tim thay script cleanup-build-artifacts.ps1 tai $cleanupScript"
}

& powershell -NoProfile -ExecutionPolicy Bypass -File $cleanupScript `
    -IncludeProjectBuildDirs `
    -IncludeSharedBuildCache `
    -IncludeLogs `
    -IncludeRootArtifacts
