param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$ConfigPath = "src/StationApp.UI/appsettings.json",
    [string]$SharedReleaseRoot = "\\10.0.0.3\17. data dung chung\Chienbn\Phan_mem_can",
    [string]$AppVersion = "",
    [string]$ReleaseNotes = "",
    [switch]$DbMigratorRequired
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

function Invoke-PowerShellScript {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ScriptPath,
        [Parameter(Mandatory = $true)]
        [object[]]$Arguments
    )

    & powershell -NoProfile -ExecutionPolicy Bypass -File $ScriptPath @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "powershell script that bai: $ScriptPath (exit code $LASTEXITCODE)."
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishScriptPath = Join-Path $PSScriptRoot "publish-ui-release.ps1"

if ([string]::IsNullOrWhiteSpace($AppVersion)) {
    throw "Bat buoc truyen -AppVersion theo dinh dang x.y.z khi phat hanh len shared folder. Vi du: -AppVersion 1.0.1"
}

Assert-StationAppSemVer -VersionText $AppVersion

if ([string]::IsNullOrWhiteSpace($ReleaseNotes)) {
    $ReleaseNotes = "Cap nhat phat hanh tu shared folder."
}

$releaseStamp = Get-Date -Format "yyyyMMdd_HHmmss"
$localPublishDir = Join-Path $repoRoot "Builds\StationApp_$AppVersion`_$releaseStamp"
$packageName = "StationApp_$AppVersion`_$releaseStamp.zip"
$localZipPath = Join-Path $repoRoot "Builds\$packageName"
$sharedZipPath = Join-Path $SharedReleaseRoot $packageName
$latestManifestPath = Join-Path $SharedReleaseRoot "latest.json"
$versionManifestPath = Join-Path $SharedReleaseRoot "StationApp_$AppVersion`_$releaseStamp.json"

Invoke-PowerShellScript -ScriptPath $publishScriptPath -Arguments @(
    "-Configuration", $Configuration,
    "-RuntimeIdentifier", $RuntimeIdentifier,
    "-ConfigPath", $ConfigPath,
    "-PublishDir", $localPublishDir,
    "-AppVersion", $AppVersion,
    "-SelfContained"
)

if (Test-Path $localZipPath) {
    Remove-Item $localZipPath -Force
}

Compress-Archive -Path (Join-Path $localPublishDir "*") -DestinationPath $localZipPath -Force

$sha256 = (Get-FileHash $localZipPath -Algorithm SHA256).Hash.ToUpperInvariant()
$publishedAt = [DateTimeOffset]::Now

$latestManifest = [ordered]@{
    version = $AppVersion
    packageName = $packageName
    packagePath = $sharedZipPath
    sha256 = $sha256
    publishedAt = $publishedAt.ToString("o")
    releaseNotes = $ReleaseNotes
    dbMigratorRequired = $DbMigratorRequired.IsPresent
    minSupportedVersion = "1.0.0"
}

$manifestJson = $latestManifest | ConvertTo-Json -Depth 5

if (-not (Test-Path $SharedReleaseRoot)) {
    New-Item -ItemType Directory -Path $SharedReleaseRoot -Force | Out-Null
}

Copy-Item $localZipPath $sharedZipPath -Force
Set-Content -Path $latestManifestPath -Value $manifestJson -Encoding UTF8
Set-Content -Path $versionManifestPath -Value $manifestJson -Encoding UTF8

Write-Host "Shared folder release published successfully."
Write-Host "Version: $AppVersion"
Write-Host "Package: $sharedZipPath"
Write-Host "Manifest: $latestManifestPath"
