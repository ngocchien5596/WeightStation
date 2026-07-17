param(
    [string]$InstallerPath = "",
    [switch]$CheckOnly
)

$ErrorActionPreference = "Stop"

$providers = @(
    "Microsoft.ACE.OLEDB.16.0",
    "Microsoft.ACE.OLEDB.12.0"
)

function Test-AceOleDbProvider {
    if (-not [Environment]::Is64BitProcess) {
        throw "Hay chay script bang PowerShell 64-bit de kiem tra/cai Microsoft Access Database Engine 64-bit."
    }

    foreach ($provider in $providers) {
        $keys = @(
            "Registry::HKEY_CLASSES_ROOT\$provider",
            "Registry::HKEY_LOCAL_MACHINE\SOFTWARE\Classes\$provider"
        )

        foreach ($key in $keys) {
            if (Test-Path $key) {
                return $provider
            }
        }
    }

    return $null
}

function Resolve-InstallerPath {
    param([string]$RequestedPath)

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        if (-not (Test-Path $RequestedPath)) {
            throw "Khong tim thay file cai dat Access Database Engine: $RequestedPath"
        }

        return (Resolve-Path $RequestedPath).Path
    }

    $scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
    $candidates = @(
        (Join-Path $scriptDir "AccessDatabaseEngine_X64.exe"),
        (Join-Path $scriptDir "accessdatabaseengine_X64.exe"),
        (Join-Path $scriptDir "AccessDatabaseEngine.exe")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path $candidate) {
            return (Resolve-Path $candidate).Path
        }
    }

    throw "Chua co file cai dat Microsoft Access Database Engine 64-bit trong thu muc prerequisites. Hay dat file AccessDatabaseEngine_X64.exe canh script nay."
}

$installedProvider = Test-AceOleDbProvider
if ($installedProvider) {
    Write-Host "ACE OLEDB da san sang: $installedProvider"
    exit 0
}

if ($CheckOnly) {
    Write-Host "Chua cai Microsoft Access Database Engine/ACE OLEDB 64-bit."
    exit 2
}

$resolvedInstallerPath = Resolve-InstallerPath -RequestedPath $InstallerPath
Write-Host "Dang cai Microsoft Access Database Engine 64-bit tu: $resolvedInstallerPath"

$process = Start-Process `
    -FilePath $resolvedInstallerPath `
    -ArgumentList "/quiet" `
    -Wait `
    -PassThru

if ($process.ExitCode -ne 0) {
    throw "Cai Microsoft Access Database Engine that bai. ExitCode=$($process.ExitCode)"
}

$installedProvider = Test-AceOleDbProvider
if (-not $installedProvider) {
    throw "Da chay bo cai nhung van chua phat hien ACE OLEDB provider. Vui long khoi dong lai may hoac kiem tra quyen admin."
}

Write-Host "Cai dat thanh cong. ACE OLEDB da san sang: $installedProvider"
exit 0
