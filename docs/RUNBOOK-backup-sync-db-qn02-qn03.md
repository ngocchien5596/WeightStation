# Runbook tạo DB BackupSync cho QN02/QN03

Tài liệu này dùng khi dựng máy backup có Internet ngoài để hứng dữ liệu sync từ trạm `QN02` và `QN03`.

Lưu ý về tên gọi:

- Tên nghiệp vụ/triển khai: **BackupSync API**.
- Ghi chú kỹ thuật: hiện tại BackupSync API đang tái sử dụng project/executable `StationApp.CentralApi`.
- Khi vận hành QN02/QN03, nên gọi là **BackupSync API** để tránh nhầm với Central API nội bộ của `QN01`.

## 1. Nguyên tắc

- `QN01` không dùng DB/API backup này, vẫn đồng bộ qua Central API nội bộ hiện tại.
- `QN02`, `QN03` chỉ đồng bộ qua BackupSync Internet.
- Máy backup cần có:
  - SQL Server.
  - Bản publish BackupSync API.
  - IP public/domain hoặc mạng Internet ngoài để QN02/QN03 gọi được.

## 1.1. Vì sao chắc chắn QN02/QN03 sync sang BackupSync

Việc dữ liệu đi sang BackupSync hay Central API nội bộ được quyết định ở máy trạm, không phụ thuộc vào tên executable trên server.

Code route hiện tại:

- `src/StationApp.Sync/Services/BackupSyncRouteResolver.cs`
- `src/StationApp.Sync/Services/CentralApiClient.cs`
- `src/StationApp.Sync/Services/CentralApiImageSyncClient.cs`

Logic:

- Payload có `StationCode = QN01` sẽ gửi tới `central_api_url`.
- Payload có `StationCode = QN02` hoặc `QN03` sẽ gửi tới `backup_sync_api_url`.
- Nếu `QN02/QN03` thiếu hoặc sai `backup_sync_api_url`, hệ thống báo lỗi cấu hình và không fallback sang `central_api_url`.

Các cấu hình cần kiểm tra trên máy trạm `QN02/QN03`:

```text
backup_sync_enabled = true
backup_sync_station_codes = QN02,QN03
backup_sync_api_url = http://<ip-hoac-domain-backup>:5005/
backup_sync_api_key = API_KEY_BACKUP_SYNC_RIENG
```

Test đã cover các case chính:

```powershell
dotnet test tests\StationApp.Sync.Tests\StationApp.Sync.Tests.csproj
```

Các test routing kiểm tra:

- `QN01` đi Central API.
- `QN02` đi BackupSync API.
- `QN03` thiếu BackupSync URL thì lỗi cấu hình, không gửi sang Central API.

## 2. Tạo database trên SQL Server máy backup

Ví dụ tạo database tên `StationAppBackupSync`:

```sql
CREATE DATABASE StationAppBackupSync;
GO
```

Tạo login riêng cho BackupSync API:

```sql
CREATE LOGIN station_backup_sync WITH PASSWORD = 'MatKhauManhCuaBan';
GO

USE StationAppBackupSync;
GO

CREATE USER station_backup_sync FOR LOGIN station_backup_sync;
GO

ALTER ROLE db_owner ADD MEMBER station_backup_sync;
GO
```

Gợi ý: khi triển khai thật nên đổi `MatKhauManhCuaBan` thành mật khẩu mạnh và lưu lại ở nơi quản trị nội bộ.

Chỉ cần tạo database rỗng. Không cần tự tạo chi tiết schema bằng tay.

Khi BackupSync API khởi động lần đầu, code sẽ tự tạo bảng và vá schema:

```csharp
db.Database.EnsureCreatedAsync();
EnsureCentralSchemaCompatibilityAsync(db);
```

Điều kiện là SQL user của BackupSync API phải có quyền tạo bảng/sửa schema trong DB này. Vì vậy giai đoạn đầu có thể cấp `db_owner` như script trên.

## 3. Tạo API key BackupSync

Tạo API key random bằng PowerShell:

```powershell
[Convert]::ToBase64String((1..32 | ForEach-Object { Get-Random -Maximum 256 }))
```

Ví dụ kết quả:

```text
FqYzPz8eYwJqZbWfM0p3Q3xK0m4x8b7LqvFv9s2aA1c=
```

Key này dùng đồng thời ở:

- `CentralApi:ApiKey` trong file cấu hình của BackupSync API trên máy backup.
- `BackupSync API Key` trên máy trạm `QN02`, `QN03`.

Không dùng chung key này với Central API nội bộ của `QN01`.

## 4. Cấu hình BackupSync API trên máy backup

Trước khi cấu hình, cần publish/export BackupSync API ra một thư mục riêng trên máy backup.

## 4.1. Publish/export BackupSync API từ máy dev

Chạy tại root solution:

```powershell
dotnet publish src\StationApp.CentralApi\StationApp.CentralApi.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o .\publish\BackupSync
```

Sau khi chạy xong, copy toàn bộ thư mục:

```text
.\publish\BackupSync
```

lên máy backup, ví dụ:

```text
C:\Apps\StationApp.BackupSync
```

Ghi chú kỹ thuật: lệnh publish vẫn dùng project `StationApp.CentralApi` vì hiện tại BackupSync API tái sử dụng executable này.

Lệnh này publish đúng ứng dụng hứng sync cho QN02/QN03 vì các endpoint nhận dữ liệu sync đang nằm trong project `StationApp.CentralApi`, gồm:

- `/api/vehicle-registrations`
- `/api/weighing-sessions`
- `/api/weighing-session-lines`
- `/api/weigh-tickets`
- `/api/delivery-tickets`
- `/api/weighing-session-images`
- `/api/audit-logs`

Vai trò vận hành là BackupSync API vì bản publish này được deploy vào thư mục riêng, dùng DB backup riêng và API key riêng.

Nếu muốn publish self-contained để máy backup không cần cài .NET Runtime, dùng:

```powershell
dotnet publish src\StationApp.CentralApi\StationApp.CentralApi.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -o .\publish\BackupSync
```

## 4.1.1. Publish BackupSync API cho Ubuntu/Linux

Không copy bản publish `win-x64` sang Ubuntu. Nếu máy backup chạy Ubuntu, cần publish lại `linux-x64`.

Nếu Ubuntu đã cài .NET 8 Runtime:

```powershell
dotnet publish src\StationApp.CentralApi\StationApp.CentralApi.csproj `
  -c Release `
  -r linux-x64 `
  --self-contained false `
  -o .\publish\BackupSync-linux
```

Copy thư mục `.\publish\BackupSync-linux` lên Ubuntu, ví dụ:

```text
/opt/stationapp/backupsync
```

Nếu muốn không cần cài .NET Runtime trên Ubuntu:

```powershell
dotnet publish src\StationApp.CentralApi\StationApp.CentralApi.csproj `
  -c Release `
  -r linux-x64 `
  --self-contained true `
  -o .\publish\BackupSync-linux
```

Khi dùng bản self-contained trên Ubuntu, file chạy là:

```text
StationApp.CentralApi
```

## 4.2. Cấu hình `appsettings.json` trên máy backup

Trong thư mục publish của BackupSync API:

```text
C:\Apps\StationApp.BackupSync
```

sửa file:

```text
C:\Apps\StationApp.BackupSync\appsettings.json
```

Nội dung mẫu:

```json
{
  "ConnectionStrings": {
    "CentralConnection": "Server=.;Database=StationAppBackupSync;User Id=station_backup_sync;Password=MatKhauManhCuaBan;Encrypt=False;TrustServerCertificate=True;"
  },
  "CentralApi": {
    "ApiKey": "API_KEY_BACKUP_SYNC_RIENG",
    "EnableFileLog": true,
    "LogDirectory": "C:\\ProgramData\\StationApp\\BackupSync\\logs"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "StationApp.CentralApi": "Information"
    }
  }
}
```

Thay:

- `MatKhauManhCuaBan` bằng mật khẩu SQL login đã tạo.
- `API_KEY_BACKUP_SYNC_RIENG` bằng API key đã tạo ở bước 3.
- `Server=.` nếu SQL Server không nằm cùng máy hoặc dùng instance khác.

Ghi chú: tên section cấu hình vẫn là `CentralApi` vì executable hiện tái sử dụng code `StationApp.CentralApi`. Đây chỉ là tên kỹ thuật trong file cấu hình, không làm thay đổi vai trò triển khai là BackupSync API.

Nếu không muốn ghi log ra file, ví dụ khi chạy trên Ubuntu và chỉ muốn log ra console/systemd journal, cấu hình:

```json
{
  "CentralApi": {
    "ApiKey": "API_KEY_BACKUP_SYNC_RIENG",
    "EnableFileLog": false
  }
}
```

Khi `EnableFileLog = false`, không cần cấu hình `LogDirectory`.

Nếu vẫn muốn ghi log file trên Ubuntu, dùng path Linux:

```json
{
  "CentralApi": {
    "ApiKey": "API_KEY_BACKUP_SYNC_RIENG",
    "EnableFileLog": true,
    "LogDirectory": "/var/log/stationapp/backupsync"
  }
}
```

## 5. Khởi động BackupSync API lần đầu bằng console

Mở PowerShell trên máy backup:

```powershell
cd C:\Apps\StationApp.BackupSync
```

Chạy API:

```powershell
.\StationApp.CentralApi.exe --urls "http://0.0.0.0:5005"
```

Nếu publish theo kiểu framework-dependent và muốn chạy bằng `dotnet`:

```powershell
dotnet .\StationApp.CentralApi.dll --urls "http://0.0.0.0:5005"
```

Nếu chạy trên Ubuntu với bản framework-dependent:

```bash
cd /opt/stationapp/backupsync
dotnet StationApp.CentralApi.dll --urls "http://0.0.0.0:5005"
```

Nếu chạy trên Ubuntu với bản self-contained:

```bash
cd /opt/stationapp/backupsync
chmod +x StationApp.CentralApi
./StationApp.CentralApi --urls "http://0.0.0.0:5005"
```

Khi API khởi động, code hiện tại sẽ tự tạo schema:

```csharp
db.Database.EnsureCreatedAsync();
EnsureCentralSchemaCompatibilityAsync(db);
```

Vì vậy chỉ cần tạo database rỗng trước, các bảng hứng dữ liệu sync sẽ được bootstrap tự động.

Gợi ý port:

- Dùng `5005` cho BackupSync API để tránh nhầm với Central API nội bộ.
- Nếu đổi port, nhớ cấu hình lại `BackupSync API URL` trên QN02/QN03.

## 6. Mở firewall trên máy backup

Nếu dùng port `5005`, chạy PowerShell bằng quyền Administrator:

```powershell
New-NetFirewallRule `
  -DisplayName "StationApp BackupSync API 5005" `
  -Direction Inbound `
  -Action Allow `
  -Protocol TCP `
  -LocalPort 5005
```

## 7. Test BackupSync API và DB

Chạy PowerShell:

```powershell
Invoke-RestMethod `
  -Uri "http://<ip-hoac-domain-backup>:5005/health" `
  -Headers @{ "X-Api-Key" = "API_KEY_BACKUP_SYNC_RIENG" }
```

Kết quả mong muốn:

```json
{
  "success": true,
  "service": "StationApp.CentralApi",
  "database": "ok"
}
```

Ghi chú: trường `service` hiện vẫn trả về `StationApp.CentralApi` do đang tái sử dụng executable kỹ thuật. Khi vận hành, vẫn hiểu đây là BackupSync API của QN02/QN03.

Nếu `database` không phải `ok`, kiểm tra lại connection string, quyền SQL login và firewall SQL Server.

## 8. Chạy BackupSync API nền lâu dài

Không nên vận hành lâu dài bằng cửa sổ PowerShell mở tay. Có 2 cách khuyến nghị:

### Cách 1. Dùng NSSM để chạy như Windows Service

Tải/copy `nssm.exe` lên máy backup, ví dụ:

```text
C:\Tools\nssm\nssm.exe
```

Cài service:

```powershell
C:\Tools\nssm\nssm.exe install StationApp.BackupSync `
  "C:\Apps\StationApp.BackupSync\StationApp.CentralApi.exe" `
  "--urls http://0.0.0.0:5005"
```

Cấu hình thư mục làm việc:

```powershell
C:\Tools\nssm\nssm.exe set StationApp.BackupSync AppDirectory "C:\Apps\StationApp.BackupSync"
```

Cấu hình tự khởi động cùng Windows:

```powershell
C:\Tools\nssm\nssm.exe set StationApp.BackupSync Start SERVICE_AUTO_START
```

Khởi động service:

```powershell
Start-Service StationApp.BackupSync
```

Kiểm tra trạng thái:

```powershell
Get-Service StationApp.BackupSync
```

Dừng service khi cần cập nhật bản mới:

```powershell
Stop-Service StationApp.BackupSync
```

Gỡ service nếu cần:

```powershell
C:\Tools\nssm\nssm.exe remove StationApp.BackupSync confirm
```

### Cách 2. Dùng Task Scheduler

Tạo task tự chạy khi máy khởi động:

```powershell
$action = New-ScheduledTaskAction `
  -Execute "C:\Apps\StationApp.BackupSync\StationApp.CentralApi.exe" `
  -Argument '--urls "http://0.0.0.0:5005"' `
  -WorkingDirectory "C:\Apps\StationApp.BackupSync"

$trigger = New-ScheduledTaskTrigger -AtStartup

Register-ScheduledTask `
  -TaskName "StationApp.BackupSync" `
  -Action $action `
  -Trigger $trigger `
  -RunLevel Highest `
  -Description "StationApp BackupSync API for QN02/QN03"
```

Chạy task ngay:

```powershell
Start-ScheduledTask -TaskName "StationApp.BackupSync"
```

Kiểm tra task:

```powershell
Get-ScheduledTask -TaskName "StationApp.BackupSync"
```

### Cách 3. Dùng systemd trên Ubuntu

Tạo file service:

```bash
sudo nano /etc/systemd/system/stationapp-backupsync.service
```

Nếu dùng bản framework-dependent, nội dung mẫu:

```ini
[Unit]
Description=StationApp BackupSync API for QN02/QN03
After=network.target

[Service]
WorkingDirectory=/opt/stationapp/backupsync
ExecStart=/usr/bin/dotnet /opt/stationapp/backupsync/StationApp.CentralApi.dll --urls http://0.0.0.0:5005
Restart=always
RestartSec=10
Environment=ASPNETCORE_ENVIRONMENT=Production

[Install]
WantedBy=multi-user.target
```

Nếu dùng bản self-contained, đổi `ExecStart` thành:

```ini
ExecStart=/opt/stationapp/backupsync/StationApp.CentralApi --urls http://0.0.0.0:5005
```

Reload và chạy service:

```bash
sudo systemctl daemon-reload
sudo systemctl enable stationapp-backupsync
sudo systemctl start stationapp-backupsync
sudo systemctl status stationapp-backupsync
```

Xem log nếu `EnableFileLog = false`:

```bash
journalctl -u stationapp-backupsync -f
```

## 9. Cấu hình trên máy trạm QN02/QN03

Vào phần mềm cân, màn `Cấu hình hệ thống`, khai báo:

- `BackupSync API URL`: `http://<ip-hoac-domain-backup>:5005/`
- `BackupSync API Key`: `API_KEY_BACKUP_SYNC_RIENG`
- `Trạm dùng BackupSync`: `QN02,QN03`
- Bật `BackupSync cho QN02/QN03`

Sau đó bấm `KIỂM TRA BACKUP`.

## 10. Kiểm tra dữ liệu đã sync lên backup

Kiểm tra tổng quan dữ liệu của `QN02`:

```powershell
Invoke-RestMethod `
  -Uri "http://<ip-hoac-domain-backup>:5005/api/backup-export/stations/QN02/summary" `
  -Headers @{ "X-Api-Key" = "API_KEY_BACKUP_SYNC_RIENG" }
```

Kiểm tra `QN03`:

```powershell
Invoke-RestMethod `
  -Uri "http://<ip-hoac-domain-backup>:5005/api/backup-export/stations/QN03/summary" `
  -Headers @{ "X-Api-Key" = "API_KEY_BACKUP_SYNC_RIENG" }
```

## 11. Cập nhật bản BackupSync API mới

Nếu chạy bằng NSSM:

```powershell
Stop-Service StationApp.BackupSync
```

Copy đè bản publish mới vào:

```text
C:\Apps\StationApp.BackupSync
```

Sau đó chạy lại:

```powershell
Start-Service StationApp.BackupSync
```

Nếu chạy bằng Task Scheduler:

```powershell
Stop-ScheduledTask -TaskName "StationApp.BackupSync"
```

Copy đè bản publish mới, rồi chạy:

```powershell
Start-ScheduledTask -TaskName "StationApp.BackupSync"
```

Sau khi cập nhật, test lại:

```powershell
Invoke-RestMethod `
  -Uri "http://<ip-hoac-domain-backup>:5005/health" `
  -Headers @{ "X-Api-Key" = "API_KEY_BACKUP_SYNC_RIENG" }
```

## 12. Lưu ý vận hành

- Nếu QN02/QN03 chưa cấu hình `BackupSync API URL`, hệ thống sẽ báo lỗi cấu hình và không tự đẩy sang Central API nội bộ.
- Cấu hình phần cứng local như cổng COM, máy in, camera không restore tự động từ backup để tránh ghi đè cấu hình riêng của từng máy.
- Nên dùng HTTPS khi mở API ra Internet.
- Nên cấu hình firewall chỉ mở đúng port API cần dùng.
- Nên backup định kỳ DB `StationAppBackupSync` trên máy backup.

Các trường hợp được hiểu là QN02/QN03 chưa cấu hình `BackupSync API URL`:

- Trường `BackupSync API URL` để trống.
- URL sai format, ví dụ `10.0.0.5:5005` thay vì `http://10.0.0.5:5005/`.
- Máy mới restore/cài mới, DB local chưa có key `backup_sync_api_url`.
- Chỉ cấu hình `Central API URL` nhưng quên cấu hình `BackupSync API URL`.
- `backup_sync_station_codes` có `QN02,QN03`, nhưng `backup_sync_api_url` vẫn rỗng hoặc không parse được thành URL hợp lệ.

Khi xảy ra các case trên, dữ liệu QN02/QN03 sẽ ở trạng thái lỗi sync cấu hình để người vận hành sửa URL BackupSync, thay vì gửi nhầm sang Central API nội bộ.
