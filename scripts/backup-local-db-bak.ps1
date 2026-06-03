param(
    [string]$ConnectionString,
    [string]$DatabaseName = "StationAppLocal",
    [string]$OutputDirectory = "backupdatabase\\sql-bak",
    [string]$DateStamp = "",
    [switch]$CopyOnly
)

$ErrorActionPreference = "Stop"

function Resolve-RepoRoot {
    $scriptDir = $PSScriptRoot
    if ([string]::IsNullOrWhiteSpace($scriptDir)) {
        throw "Khong xac dinh duoc thu muc script hien tai."
    }

    return (Resolve-Path (Join-Path $scriptDir "..")).Path
}

function Get-DefaultConnectionString {
    param(
        [string]$RepoRoot
    )

    $appSettingsPath = Join-Path $RepoRoot "src\\StationApp.UI\\appsettings.json"
    if (-not (Test-Path $appSettingsPath)) {
        throw "Khong tim thay file appsettings tai: $appSettingsPath"
    }

    $json = Get-Content -Path $appSettingsPath -Raw | ConvertFrom-Json
    $value = $json.ConnectionStrings.DefaultConnection
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "Khong doc duoc ConnectionStrings:DefaultConnection tu $appSettingsPath"
    }

    return $value
}

function Normalize-ConnectionString {
    param(
        [string]$ConnString,
        [string]$TargetDatabase
    )

    $builder = New-Object System.Data.SqlClient.SqlConnectionStringBuilder $ConnString
    $builder["Initial Catalog"] = $TargetDatabase
    return $builder.ConnectionString
}

function Invoke-NonQuery {
    param(
        [string]$ConnString,
        [string]$Sql
    )

    Add-Type -AssemblyName "System.Data"

    $connection = New-Object System.Data.SqlClient.SqlConnection $ConnString
    try {
        $connection.Open()
        $command = $connection.CreateCommand()
        $command.CommandTimeout = 0
        $command.CommandText = $Sql
        $null = $command.ExecuteNonQuery()
    }
    finally {
        $connection.Dispose()
    }
}

$repoRoot = Resolve-RepoRoot
if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
    $ConnectionString = Get-DefaultConnectionString -RepoRoot $repoRoot
}

if ([string]::IsNullOrWhiteSpace($DateStamp)) {
    $DateStamp = Get-Date -Format "yyyyMMdd"
}

$resolvedOutputDirectory = if ([System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory
} else {
    Join-Path $repoRoot $OutputDirectory
}

if (-not (Test-Path $resolvedOutputDirectory)) {
    $null = New-Item -Path $resolvedOutputDirectory -ItemType Directory -Force
}

$backupFilePath = Join-Path $resolvedOutputDirectory ("{0}_{1}.bak" -f $DateStamp, $DatabaseName)
$copyOnlyClause = if ($CopyOnly.IsPresent) { ", COPY_ONLY" } else { "" }
$escapedBackupPath = $backupFilePath.Replace("'", "''")
$sql = @"
BACKUP DATABASE [$DatabaseName]
TO DISK = N'$escapedBackupPath'
WITH INIT, FORMAT, COMPRESSION, STATS = 10$copyOnlyClause;
"@

$targetConnectionString = Normalize-ConnectionString -ConnString $ConnectionString -TargetDatabase "master"
Invoke-NonQuery -ConnString $targetConnectionString -Sql $sql

Write-Host ("[OK] Backup .bak da tao: {0}" -f $backupFilePath)
