param(
    [Parameter(Mandatory = $false)]
    [string]$Configuration = "Debug",

    [Parameter(Mandatory = $false)]
    [string]$RuntimeIdentifier = "",

    [Parameter(Mandatory = $false)]
    [string]$BuildRoot = "",

    [Parameter(Mandatory = $false)]
    [string]$ConfigPath = ".\src\StationApp.UI\appsettings.json"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($BuildRoot)) {
    $buildOutputRoot = Join-Path $repoRoot "._repo_build"
}
elseif ([System.IO.Path]::IsPathRooted($BuildRoot)) {
    $buildOutputRoot = [System.IO.Path]::GetFullPath($BuildRoot)
}
else {
    $buildOutputRoot = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $BuildRoot))
}

$migratorBaseDir = Join-Path $buildOutputRoot "bin\StationApp.DbMigrator\$Configuration\net8.0"
$resolvedConfigPath = [System.IO.Path]::GetFullPath((Join-Path $repoRoot $ConfigPath))

if ([string]::IsNullOrWhiteSpace($RuntimeIdentifier)) {
    $runtimeSpecificPath = $null
}
else {
    $runtimeSpecificPath = Join-Path $migratorBaseDir "$RuntimeIdentifier\StationApp.DbMigrator.dll"
}

$candidatePaths = @()
if ($runtimeSpecificPath) {
    $candidatePaths += $runtimeSpecificPath
}
$candidatePaths += (Join-Path $migratorBaseDir "StationApp.DbMigrator.dll")

$migratorDll = $candidatePaths | Where-Object { Test-Path $_ } | Select-Object -First 1

if (-not $migratorDll -and (Test-Path $migratorBaseDir)) {
    $migratorDll = Get-ChildItem -Path $migratorBaseDir -Filter "StationApp.DbMigrator.dll" -Recurse -File |
        Sort-Object FullName |
        Select-Object -ExpandProperty FullName -First 1
}

if ([string]::IsNullOrWhiteSpace($migratorDll) -or -not (Test-Path $migratorDll)) {
    $searchedPaths = ($candidatePaths | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }) -join ", "
    if ([string]::IsNullOrWhiteSpace($searchedPaths)) {
        $searchedPaths = $migratorBaseDir
    }

    throw "Db migrator not found. Searched: $searchedPaths"
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
