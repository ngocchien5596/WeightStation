param(
    [Parameter(Mandatory = $false)]
    [string]$Configuration = "Debug",

    [Parameter(Mandatory = $false)]
    [string]$ConfigPath = ".\src\StationApp.UI\appsettings.json"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$migratorDll = Join-Path $repoRoot "._repo_build\bin\StationApp.DbMigrator\$Configuration\net8.0\StationApp.DbMigrator.dll"
$resolvedConfigPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ConfigPath))

if (-not (Test-Path $migratorDll)) {
    throw "Db migrator not found: $migratorDll"
}

if (-not (Test-Path $resolvedConfigPath)) {
    throw "Config file not found: $resolvedConfigPath"
}

$output = & dotnet exec $migratorDll --config $resolvedConfigPath 2>&1
$exitCode = $LASTEXITCODE

foreach ($line in $output) {
    $text = [string]$line
    if ($text -like "Security Warning: The negotiated TLS 1.0 is an insecure protocol*") {
        continue
    }

    Write-Host $text
}

if ($exitCode -ne 0) {
    throw "Database schema update failed with exit code $exitCode."
}
