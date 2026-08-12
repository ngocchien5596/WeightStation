# Runbook BackupSync Internet cho QN02/QN03

## Nguyên tắc định tuyến

- Dữ liệu phát sinh từ `QN01` chỉ đồng bộ qua Central API nội bộ hiện tại.
- Dữ liệu phát sinh từ `QN02`, `QN03` chỉ đồng bộ qua BackupSync Internet.
- Nếu `QN02` hoặc `QN03` chưa cấu hình `backup_sync_api_url`, hệ thống báo lỗi cấu hình và không tự động đẩy sang Central API nội bộ.

## Cấu hình trên máy trạm QN02/QN03

Vào `Cấu hình hệ thống` và khai báo:

- `BackupSync API URL`: địa chỉ public/domain của máy backup.
- `BackupSync API Key`: khóa API của máy backup.
- `Trạm dùng BackupSync`: mặc định `QN02,QN03`.
- Bật `BackupSync cho QN02/QN03`.

Sau đó bấm `Test BackupSync` để kiểm tra kết nối.

## Kiểm tra dữ liệu backup

Ví dụ kiểm tra tổng quan dữ liệu đã backup cho `QN02`:

```powershell
Invoke-RestMethod `
  -Uri "https://backup-domain/api/backup-export/stations/QN02/summary" `
  -Headers @{ "X-Api-Key" = "<api-key>" }
```

## Restore dữ liệu về máy trạm

Tool restore nằm trong project `StationApp.BackupRestoreTool`.

Chạy dry-run trước để kiểm tra kết nối và số lượng dữ liệu:

```powershell
dotnet run --project src\StationApp.BackupRestoreTool\StationApp.BackupRestoreTool.csproj -- `
  --backup-api-url "https://backup-domain/" `
  --api-key "<api-key>" `
  --station-code QN02 `
  --local-connection "Server=.;Database=StationApp;Trusted_Connection=True;TrustServerCertificate=True"
```

Khi đã chắc chắn đúng máy, đúng trạm, thêm `--execute` để ghi dữ liệu vào DB local:

```powershell
dotnet run --project src\StationApp.BackupRestoreTool\StationApp.BackupRestoreTool.csproj -- `
  --backup-api-url "https://backup-domain/" `
  --api-key "<api-key>" `
  --station-code QN02 `
  --local-connection "Server=.;Database=StationApp;Trusted_Connection=True;TrustServerCertificate=True" `
  --execute
```

## Ghi chú an toàn

- Restore chỉ dùng dữ liệu của đúng `station-code` đã truyền.
- Cấu hình phần cứng local như cổng COM, máy in, camera không restore tự động để tránh ghi đè cấu hình riêng của máy mới.
- Nên backup DB local hiện tại trước khi chạy restore có `--execute`.
