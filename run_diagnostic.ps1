# Diagnostic Script for StationApp
$ErrorActionPreference = "SilentlyContinue"
$date = Get-Date -Format "yyyyMMdd_HHmmss"
$outDir = "DiagnosticPack_$date"
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

Write-Host "==========================================" -ForegroundColor Green
Write-Host "STATIONAPP CLIENT-SIDE DIAGNOSTICS TOOL   " -ForegroundColor Green
Write-Host "==========================================" -ForegroundColor Green

# 1. Machine Info
Write-Host "[1/4] Dang lay thong tin may tinh..." -ForegroundColor Cyan
$machineInfo = @"
MachineName: $env:COMPUTERNAME
UserName: $env:USERNAME
DateTime: $((Get-Date).ToString())
"@
$machineInfo | Out-File "$outDir/machine-info.txt" -Encoding utf8

# 2. Network Diagnostics
Write-Host "[2/4] Dang kiem tra ket noi mang toi DB Server (10.0.0.152)..." -ForegroundColor Cyan
$ping = Test-Connection -ComputerName "10.0.0.152" -Count 4 -ErrorAction SilentlyContinue
if ($ping) {
    "Ping 10.0.0.152 OK. Avg RTT: $( ($ping | Measure-Object ResponseTime -Average).Average ) ms" | Out-File "$outDir/network-test.txt" -Encoding utf8
} else {
    "Ping 10.0.0.152 FAILED" | Out-File "$outDir/network-test.txt" -Encoding utf8
}

$portTest = Test-NetConnection -ComputerName "10.0.0.152" -Port 1433
"Port 1433 (SQL Server) Accessible: $($portTest.TcpTestSucceeded)" | Out-File "$outDir/network-test.txt" -Append -Encoding utf8

# 3. User Interaction - Interactive Test
Write-Host ""
Write-Host ">>> BUOC THUC HIEN QUAN TRONG <<<" -ForegroundColor Yellow
Write-Host "1. Vui long MO ung dung StationApp.UI len." -ForegroundColor White
Write-Host "2. Thuc hien cac thao tac can hang, tim kiem xe (khoang 1-2 phut)." -ForegroundColor White
Write-Host "3. Neu thay ung dung bi DO/LAG, hay ghi lai thoi gian." -ForegroundColor White
Write-Host "4. Sau khi test xong, DONG ung dung lai." -ForegroundColor White
Write-Host ""
Read-Host "Bam nut [ENTER] tai day SAU KHI DA DONG UNG DUNG de tiep tuc thu thap log..."

# 4. Log Collection
Write-Host "[3/4] Dang gom file logs moi nhat..." -ForegroundColor Cyan

$apps = Get-ChildItem -Path "." -Filter "StationApp.UI.exe" -File -Recurse -ErrorAction SilentlyContinue

if ($apps) {
    $count = 0
    foreach ($app in $apps) {
        $appDir = $app.DirectoryName
        $logDir = Join-Path $appDir "logs"
        if (Test-Path $logDir) {
            $destSub = Join-Path "$outDir/app-logs" $app.Directory.Name
            New-Item -ItemType Directory -Path $destSub -Force | Out-Null
            Copy-Item -Path $logDir -Destination $destSub -Recurse -Force
            $count++
        }
    }
    if ($count -eq 0) {
        "Tim thay app nhung khong thay thu muc logs ben canh." | Out-File "$outDir/app-logs-not-found.txt" -Encoding utf8
    }
} else {
    $anyLogs = Get-ChildItem -Path "." -Filter "logs" -Directory -Recurse -ErrorAction SilentlyContinue
    if ($anyLogs) {
        foreach ($l in $anyLogs) {
            $destSub = Join-Path "$outDir/app-logs" $l.Parent.Name
            New-Item -ItemType Directory -Path $destSub -Force | Out-Null
            Copy-Item -Path $l.FullName -Destination $destSub -Recurse -Force
        }
    } else {
        "Khong tim thay StationApp.UI.exe va cung khong thay bat ky thu muc logs nao." | Out-File "$outDir/app-logs-not-found.txt" -Encoding utf8
    }
}


# 5. Instructions Archive
@"
CHECKLIST THAO TAC TEST (Dành cho Kỹ thuật viên/Người vận hành)
--------------------------------------------------------------
1. Dong app StationApp neu dang mo.
2. Mo app StationApp.UI len.
3. Dung yen, cho 1 phut khong lam gi.
4. Bam vao tab Weighing (Can hang).
5. Go tim kiem bien so xe bat ky (Vi du: 30A).
6. Bam chon 1 dong trong Grid.
7. Neu thay hien tuong app bi Do/Lag, hay ghi lai gio phut hien tai.
8. Dong app.
9. Nen (Zip) thu muc [$outDir] nay gui lai cho doi ngu Dev.
"@ | Out-File "$outDir/instructions.txt" -Encoding utf8

Write-Host "==========================================" -ForegroundColor Green
Write-Host "HOAN THANH! Hay gui thu muc [$outDir] ve cho doi ho tro." -ForegroundColor Yellow
Write-Host "==========================================" -ForegroundColor Green

