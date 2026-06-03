# DEPLOY: `StationApp.CentralApi` trên Windows Server

## 1. Mục tiêu

Tài liệu này dùng để deploy `StationApp.CentralApi` lên máy server Windows, để app trạm cân có thể sync dữ liệu lên DB trung tâm.

Kiến trúc cần nhớ:

- máy trạm cân chạy `StationApp.UI`
- máy server chạy `StationApp.CentralApi`
- `StationApp.CentralApi` ghi dữ liệu vào DB `StationAppCentral`

## 2. Trên server cần có gì

Phải có đủ:

1. SQL Server
2. database trung tâm, ví dụ `StationAppCentral`
3. bản publish của `StationApp.CentralApi`
4. port mạng mở để máy trạm gọi vào API

## 3. Publish từ máy dev

Chạy tại root solution:

```powershell
dotnet publish src/StationApp.CentralApi/StationApp.CentralApi.csproj -c Release -o .\publish\CentralApi
```

Sau khi xong, thư mục cần copy là:

- `publish\CentralApi`

## 4. Copy lên server

Ví dụ copy lên:

- `C:\Apps\StationApp.CentralApi`

Thư mục trên server sau khi copy sẽ chứa:

- `StationApp.CentralApi.exe`
- `StationApp.CentralApi.dll`
- `appsettings.json`
- các DLL dependency khác

## 5. Sửa cấu hình trên server

Mở file:

- `C:\Apps\StationApp.CentralApi\appsettings.json`

Ví dụ:

```json
{
  "ConnectionStrings": {
    "CentralConnection": "Server=10.0.0.1;Database=StationAppCentral;User Id=tramcan;Password=MatKhauMoiManh123!_2;Encrypt=False;TrustServerCertificate=True;"
  },
  "CentralApi": {
    "ApiKey": "05051996"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  }
}
```

Yêu cầu:

- `CentralConnection` phải connect được DB server
- `ApiKey` phải đúng với key nhập ở app trạm

## 6. Chạy API trên server

Mở PowerShell tại thư mục deploy:

```powershell
cd C:\Apps\StationApp.CentralApi
```

Chạy một trong hai cách:

### Cách 1. Chạy bằng exe

```powershell
.\StationApp.CentralApi.exe --urls "http://0.0.0.0:5000"
```

### Cách 2. Chạy bằng dotnet

```powershell
dotnet .\StationApp.CentralApi.dll --urls "http://0.0.0.0:5000"
```

Lưu ý:

- nếu cổng `5000` bị chiếm, đổi sang `5001`
- nếu đổi cổng thì app trạm cũng phải đổi URL theo

## 7. Mở firewall trên server

Nếu dùng port `5000`, mở inbound port:

```powershell
New-NetFirewallRule -DisplayName "StationApp Central API 5000" -Direction Inbound -Action Allow -Protocol TCP -LocalPort 5000
```

## 8. Test ngay trên server

Trên chính máy server, mở:

```text
http://localhost:5000/health
```

Kỳ vọng:

```json
{
  "success": true,
  "service": "StationApp.CentralApi",
  "database": "ok"
}
```

Nếu không ra `database: ok`, lỗi nằm ở DB connection hoặc quyền SQL.

## 9. Test từ máy trạm cân

Giả sử server có IP `10.0.0.1` và API chạy port `5000`.

Trên app trạm:

- `Central API URL` = `http://10.0.0.1:5000/`
- `Central API Key` = `05051996`

Sau đó:

1. bấm `Kiểm tra kết nối`
2. nếu thành công thì lưu cấu hình

## 10. Nếu kiểm tra kết nối lỗi

Kiểm tra theo thứ tự:

1. `CentralApi` có đang chạy không
2. server có mở được `http://localhost:5000/health` không
3. từ máy trạm có mở được `http://10.0.0.1:5000/health` không
4. firewall server đã mở port chưa
5. `ApiKey` ở app trạm có đúng không
6. DB login trong `CentralConnection` có dùng được không

## 11. Cách chạy nền lâu dài

Giai đoạn đầu có thể chạy tay bằng PowerShell để test.

Khi cần vận hành ổn định hơn, nên chuyển sang một trong các cách:

1. Task Scheduler
2. NSSM để chạy thành Windows Service
3. IIS hoặc reverse proxy nếu đội hạ tầng yêu cầu

## 12. Checklist hoàn tất

Được coi là deploy xong khi:

1. server mở được `/health`
2. máy trạm test kết nối thành công
3. tạo dữ liệu local và central DB nhận được
4. bảng `sync_ingestion_logs` có record `INSERTED` hoặc `UPDATED`
