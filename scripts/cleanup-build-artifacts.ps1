param(
    [switch]$IncludeProjectBuildDirs,
    [switch]$IncludeSharedBuildCache,
    [switch]$IncludeLogs,
    [switch]$IncludeRootArtifacts
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

function Remove-FileSafe {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }

    $sizeBytes = 0L
    try {
        $item = Get-Item -LiteralPath $Path -Force -ErrorAction Stop
        if (-not $item.PSIsContainer) {
            $sizeBytes = $item.Length
        }

        Remove-Item -LiteralPath $Path -Force -ErrorAction Stop
        return [pscustomobject]@{
            Path = $Path
            SizeMB = [math]::Round(($sizeBytes / 1MB), 2)
            Removed = $true
            ItemType = "File"
        }
    }
    catch {
        return [pscustomobject]@{
            Path = $Path
            SizeMB = [math]::Round(($sizeBytes / 1MB), 2)
            Removed = $false
            ItemType = "File"
        }
    }
}

$targets = New-Object System.Collections.Generic.List[string]
$fileTargets = New-Object System.Collections.Generic.List[string]

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
            $_.Name -in @('bin', 'obj', '.codex_build', 'artifacts', 'obj_verify', 'bin_verify', 'logs') -or
            $_.Name -like '._tmp_*' -or
            $_.Name -eq '._repo_build' -or
            $_.Name -eq '._verify_build'
        } |
        ForEach-Object { $targets.Add($_.FullName) }

    Get-ChildItem -Path (Join-Path $repoRoot 'src') -Recurse -Force -File -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -like '*wpftmp.csproj' } |
        ForEach-Object { $fileTargets.Add($_.FullName) }
}

if ($IncludeSharedBuildCache) {
    @(
        '._repo_build',
        '._wpf_build_isolated',
        '._alt_build',
        '._verify_version',
        '.codex_build',
        '.script_build_cache',
        '.script_build_cache_test'
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

    @(
        (Join-Path $repoRoot 'logs'),
        (Join-Path $repoRoot 'infra-build.log'),
        (Join-Path $repoRoot 'db_connection_used.txt')
    ) | ForEach-Object {
        if (Test-Path -LiteralPath $_) {
            if ((Get-Item -LiteralPath $_).PSIsContainer) {
                $targets.Add($_)
            }
            else {
                $fileTargets.Add($_)
            }
        }
    }
}

if ($IncludeRootArtifacts) {
    @(
        'artifacts',
        'publish',
        'scratch',
        '.tmp_versioncheck.cs'
    ) | ForEach-Object {
        $path = Join-Path $repoRoot $_
        if (Test-Path -LiteralPath $path) {
            if ((Get-Item -LiteralPath $path).PSIsContainer) {
                $targets.Add($path)
            }
            else {
                $fileTargets.Add($path)
            }
        }
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

$uniqueFileTargets = $fileTargets | Sort-Object -Unique
foreach ($target in $uniqueFileTargets) {
    $result = Remove-FileSafe -Path $target
    if ($null -ne $result) {
        $results += $result
    }
}

$removed = $results | Where-Object Removed
$failed = $results | Where-Object { -not $_.Removed }
$freedMb = [math]::Round((($removed | Measure-Object -Property SizeMB -Sum).Sum), 2)

Write-Host ""
Write-Host "Cleanup summary" -ForegroundColor Cyan
Write-Host "Removed: $($removed.Count) item(s)"
Write-Host "Freed : $freedMb MB"

if ($removed.Count -gt 0) {
    Write-Host ""
    Write-Host "Removed items:" -ForegroundColor Green
    $removed | Sort-Object Path | Format-Table -AutoSize
}

if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "Failed to remove:" -ForegroundColor Yellow
    $failed | Sort-Object Path | Format-Table -AutoSize
}
