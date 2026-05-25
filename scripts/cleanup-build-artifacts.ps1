param(
    [switch]$IncludeProjectBuildDirs,
    [switch]$IncludeSharedBuildCache,
    [switch]$IncludeLogs
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $repoRoot

function Get-DirectorySizeBytes {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return 0L
    }

    return (Get-ChildItem -LiteralPath $Path -Recurse -Force -File -ErrorAction SilentlyContinue |
        Measure-Object -Property Length -Sum).Sum
}

function Remove-DirectorySafe {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    $sizeBytes = Get-DirectorySizeBytes -Path $Path
    try {
        Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
        return [pscustomobject]@{
            Path = $Path
            SizeMB = [math]::Round(($sizeBytes / 1MB), 2)
            Removed = $true
        }
    }
    catch {
        return [pscustomobject]@{
            Path = $Path
            SizeMB = [math]::Round(($sizeBytes / 1MB), 2)
            Removed = $false
        }
    }
}

$targets = New-Object System.Collections.Generic.List[string]

Get-ChildItem -LiteralPath $repoRoot -Directory -Force -ErrorAction SilentlyContinue |
    Where-Object {
        $_.Name -like '._tmp_*' -or
        $_.Name -like '._append_*' -or
        $_.Name -eq '._tmp_ui_msbuildext'
    } |
    ForEach-Object { $targets.Add($_.FullName) }

if ($IncludeProjectBuildDirs) {
    Get-ChildItem -Path (Join-Path $repoRoot 'src'), (Join-Path $repoRoot 'tests') -Directory -Recurse -Force -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Name -in @('bin', 'obj', '.codex_build', 'artifacts') -or
            $_.Name -like '._tmp_*' -or
            $_.Name -eq '._repo_build'
        } |
        ForEach-Object { $targets.Add($_.FullName) }
}

if ($IncludeSharedBuildCache) {
    @(
        '._repo_build',
        '._wpf_build_isolated',
        'Builds'
    ) | ForEach-Object {
        $path = Join-Path $repoRoot $_
        if (Test-Path -LiteralPath $path) {
            $targets.Add($path)
        }
    }
}

if ($IncludeLogs) {
    $localLogDir = Join-Path $env:LOCALAPPDATA 'StationApp\logs'
    if (Test-Path -LiteralPath $localLogDir) {
        $targets.Add($localLogDir)
    }
}

$results = @()
$uniqueTargets = $targets | Sort-Object -Unique
foreach ($target in $uniqueTargets) {
    $result = Remove-DirectorySafe -Path $target
    if ($null -ne $result) {
        $results += $result
    }
}

$removed = $results | Where-Object Removed
$failed = $results | Where-Object { -not $_.Removed }
$freedMb = [math]::Round((($removed | Measure-Object -Property SizeMB -Sum).Sum), 2)

Write-Host ""
Write-Host "Cleanup summary" -ForegroundColor Cyan
Write-Host "Removed: $($removed.Count) folder(s)"
Write-Host "Freed : $freedMb MB"

if ($removed.Count -gt 0) {
    Write-Host ""
    Write-Host "Removed folders:" -ForegroundColor Green
    $removed | Sort-Object Path | Format-Table -AutoSize
}

if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "Failed to remove:" -ForegroundColor Yellow
    $failed | Sort-Object Path | Format-Table -AutoSize
}
