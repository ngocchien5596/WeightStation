param(
    [switch]$NoRun,
    [switch]$SkipClean
)

$ErrorActionPreference = "Stop"

$projectPath = Join-Path $PSScriptRoot "src\StationApp.UI\StationApp.UI.csproj"
$uiRoot = Split-Path $projectPath -Parent
$objPath = Join-Path $uiRoot "obj"
$binPath = Join-Path $uiRoot "bin"
$uiDllPath = Join-Path $binPath "Debug\net8.0-windows\StationApp.UI.dll"

Write-Host "Stopping running StationApp.UI instances..." -ForegroundColor Cyan
Get-Process StationApp.UI -ErrorAction SilentlyContinue | Stop-Process -Force

$uiProcessHints = @(
    $projectPath.ToLowerInvariant(),
    $uiDllPath.ToLowerInvariant(),
    "stationapp.ui.dll",
    "stationapp.ui.csproj"
)

Get-CimInstance Win32_Process |
    Where-Object {
        $process = $_
        $commandLine = $process.CommandLine
        $process.Name -eq "dotnet.exe" -and
        $null -ne $commandLine -and
        (($uiProcessHints | Where-Object { $commandLine.ToLowerInvariant().Contains($_) }).Count -gt 0)
    } |
    ForEach-Object {
        Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    }

Write-Host "Shutting down dotnet build servers..." -ForegroundColor Cyan
dotnet build-server shutdown | Out-Host

if (-not $SkipClean) {
    Write-Host "Cleaning StationApp.UI build outputs..." -ForegroundColor Cyan
    if (Test-Path $objPath) {
        Remove-Item -LiteralPath $objPath -Recurse -Force
    }

    if (Test-Path $binPath) {
        Remove-Item -LiteralPath $binPath -Recurse -Force
    }
}

$env:MSBUILDDISABLENODEREUSE = "1"
$env:UseSharedCompilation = "false"

$dotnetArgs = if ($NoRun) {
    @(
        "build"
        $projectPath
        "-p:UseSharedCompilation=false"
        "-nodeReuse:false"
    )
}
else {
    @(
        "run"
        "--project", $projectPath
        "-p:UseSharedCompilation=false"
        "-nodeReuse:false"
    )
}

Write-Host "Executing: dotnet $($dotnetArgs -join ' ')" -ForegroundColor Cyan
& dotnet @dotnetArgs
