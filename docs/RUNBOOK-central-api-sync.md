# RUNBOOK: Triển khai và kiểm tra sync `Local DB -> Central API -> Central DB`

## 1. Mục tiêu

Tài liệu này dùng để:

- cấu hình `Central API`
- cấu hình app trạm
- kiểm tra kết nối
- chạy thử sync end-to-end
- khoanh vùng lỗi khi sync không chạy

Phạm vi hiện tại:

- sync generic:
  - `cut_orders`
  - `weigh_tickets`
  - `delivery_tickets`
  - `vehicles`
  - `customers`
  - `products`
  - `weighing_sessions`
  - `weighing_session_lines`
- sync ảnh riêng:
  - `weighing_session_images`

Chưa bao gồm:

- mirror read-only cho `audit_logs`
- mirror read-only cho `app_config`
- mirror read-only cho `device_configs`
- mirror read-only cho `print_template_profiles`
- mirror read-only cho `users`

## 2. Thành phần cần có

Để sync hoạt động, phải có đủ 3 thành phần:

1. App trạm đang chạy và ghi dữ liệu vào local DB
2. `StationApp.CentralApi` đang chạy và reachable từ máy trạm
3. SQL Server trung tâm cho `Central API` ghi dữ liệu vào

Nếu thiếu một trong ba, sync sẽ không hoàn tất.

## 3. Cấu hình Central API

File cấu hình:

- [appsettings.json](/abs/g:/Source-code/pmcan_C#/src/StationApp.CentralApi/appsettings.json)

Các giá trị tối thiểu cần sửa:

```json
{
  "ConnectionStrings": {
    "CentralConnection": "Server=10.0.0.20;Database=StationAppCentral;User Id=sa;Password=your_password;Encrypt=False;TrustServerCertificate=True;"
  },
  "CentralApi": {
    "ApiKey": "your-strong-api-key"
  }
}
```

Yêu cầu:

- `CentralConnection` phải trỏ đúng SQL Server trung tâm
- `CentralApi:ApiKey` phải trùng với API key cấu hình trong app trạm
- không để `changeme` khi chạy thật

## 4. Chạy Central API

Trong môi trường dev, có thể chạy:

```powershell
dotnet run --project src/StationApp.CentralApi/StationApp.CentralApi.csproj
```

Khi publish:

```powershell
dotnet publish src/StationApp.CentralApi/StationApp.CentralApi.csproj -c Release -o .\publish\CentralApi
```

Sau khi chạy, kiểm tra nhanh:

- `GET /health`

Ví dụ:

```text
http://server-name:5000/health
```

Response mong đợi:

```json
{
  "success": true,
  "service": "StationApp.CentralApi",
  "database": "ok"
}
```

Nếu `database` không phải `ok`, lỗi nằm ở connection string hoặc quyền DB.

## 5. Cấu hình app trạm

Trên app trạm, vào:

- `Tham số hệ thống`

Nhập:

- `Central API URL`
  - ví dụ: `http://10.0.0.20:5000/`
- `Central API Key`
  - đúng với `CentralApi:ApiKey` trên server

Sau đó:

1. bấm `Lưu`
2. bấm `Kiểm tra kết nối Central API`

Kết quả đúng:

- app báo kết nối thành công
- dashboard không còn báo `Chua cau hinh`

## 6. Checklist end-to-end

Chạy theo đúng thứ tự này:

1. Mở `Central API`
2. Xác nhận `/health` trả `200`
3. Mở app trạm
4. Lưu `Central API URL` và `Central API Key`
5. Bấm test kết nối
6. Tạo hoặc cập nhật dữ liệu nghiệp vụ trên app trạm
7. Chờ worker sync chạy
8. Kiểm tra dữ liệu đã có trong DB trung tâm

## 7. Dữ liệu cần kiểm tra trên DB trung tâm

Ít nhất phải thấy dữ liệu xuất hiện ở các bảng:

- `cut_orders`
- `weigh_tickets`
- `delivery_tickets`
- `vehicles`
- `customers`
- `products`
- `weighing_sessions`
- `weighing_session_lines`
- `weighing_session_images`
- `sync_ingestion_logs`

`sync_ingestion_logs` dùng để biết server đã nhận gì và failed ở đâu.

## 8. Cách test thực tế

### 8.1 Test master data

Mục tiêu:

- xác nhận `vehicles`, `customers`, `products` lên server

Thao tác:

1. tạo một `cut_order` mới từ flow inbound hoặc flow nghiệp vụ có phát sinh master data
2. chờ worker sync
3. kiểm tra các bảng `vehicles`, `customers`, `products` trên central DB

### 8.2 Test weighing session

Mục tiêu:

- xác nhận reconstruct được phiên cân ở server

Thao tác:

1. tạo `weighing_session`
2. thêm line vào session
3. cân W1
4. cân W2
5. hoàn tất session
6. kiểm tra:
   - `weighing_sessions`
   - `weighing_session_lines`
   - `cut_orders`
   - `weigh_tickets`
   - `delivery_tickets`

### 8.3 Test image sync

Mục tiêu:

- xác nhận ảnh cân sync riêng, không làm block nghiệp vụ cân

Thao tác:

1. chụp ảnh ở bước cân có camera
2. hoàn tất nghiệp vụ local
3. kiểm tra `weighing_session_images` trên central DB
4. nếu ảnh chưa lên ngay, kiểm tra lại sau một chu kỳ worker

## 9. Dấu hiệu hệ thống hoạt động đúng

Ở app trạm:

- `Diagnostics` có `Central API URL`
- `Central API Key` ở trạng thái đã cấu hình
- `Central API Health` báo kết nối thành công
- pending outbox giảm dần
- failed sync không tăng bất thường

Ở server:

- `sync_ingestion_logs` có bản ghi `INSERTED` hoặc `UPDATED`
- không có nhiều bản ghi `FAILED` lặp lại cùng một aggregate

Ở DB trung tâm:

- dữ liệu được insert/update đúng theo nghiệp vụ local
- gọi sync lặp lại không tạo duplicate rõ ràng theo cùng `Id`

## 10. Cách khoanh vùng lỗi

### 10.1 Lỗi `Central API URL chưa được cấu hình hợp lệ`

Kiểm tra:

- app trạm đã lưu `Central API URL` chưa
- URL có đúng dạng absolute URL không
- URL có dấu `/` cuối hay không
  - code đã normalize, nhưng vẫn nên nhập đúng dạng `http://host:port/`

### 10.2 Lỗi test kết nối thất bại

Kiểm tra theo thứ tự:

1. `Central API` có đang chạy không
2. máy trạm có ping hoặc mở được `http://host:port/health` không
3. firewall trên server có mở port không
4. `CentralConnection` của API có đúng không
5. `X-Api-Key` ở app trạm có khớp với `CentralApi:ApiKey` không

### 10.3 Có dữ liệu local nhưng server không nhận

Kiểm tra:

1. `Diagnostics` xem còn pending/failed sync không
2. bảng `sync_outbox` local có record `PENDING`, `FAILED_RETRYABLE`, `FAILED_FINAL` không
3. bảng `sync_ingestion_logs` ở central có record nhận request chưa

Diễn giải:

- không có record ở `sync_ingestion_logs`
  - lỗi ở app trạm, network, URL hoặc auth
- có record `FAILED` ở `sync_ingestion_logs`
  - lỗi ở server mapping hoặc DB ghi dữ liệu
- có record `INSERTED/UPDATED` nhưng bảng nghiệp vụ không có dữ liệu đúng
  - cần rà lại mapping server hoặc constraints DB

### 10.4 Ảnh không sync

Kiểm tra:

1. bảng local `weighing_session_images`
2. cột `SyncStatus`
3. `LastSyncError`
4. `RetryCount`

Diễn giải:

- `PENDING`
  - worker ảnh chưa tới chu kỳ
- `FAILED`
  - xem `LastSyncError`
- `SYNCED`
  - kiểm tra server DB hoặc query đúng bảng chưa

## 11. Query kiểm tra nhanh

### 11.1 Xem log ingest gần nhất ở server

```sql
SELECT TOP 50 *
FROM sync_ingestion_logs
ORDER BY ReceivedAt DESC;
```

### 11.2 Xem dữ liệu phiên cân mới nhất

```sql
SELECT TOP 20 *
FROM weighing_sessions
ORDER BY CreatedAt DESC;
```

### 11.3 Xem line phiên cân mới nhất

```sql
SELECT TOP 50 *
FROM weighing_session_lines
ORDER BY CreatedAt DESC;
```

### 11.4 Xem ảnh cân mới nhất

```sql
SELECT TOP 20 Id, WeighingSessionId, CaptureStage, CameraCode, CapturedAt, FileSizeBytes
FROM weighing_session_images
ORDER BY CapturedAt DESC;
```

## 12. Khi nào coi là đạt

Có thể coi chức năng sync lên server đang hoạt động tốt khi:

1. `Central API` health pass
2. app trạm test kết nối pass
3. tạo mới nghiệp vụ local thì central DB nhận được
4. update nghiệp vụ local thì central DB update đúng
5. retry không tạo duplicate rõ ràng
6. ảnh cân lên được central DB
7. `sync_ingestion_logs` không có lỗi lặp bất thường

## 13. Việc nên làm tiếp

Sau khi deploy và test ổn phase này, việc tiếp theo nên làm là:

1. thêm mirror read-only cho `audit_logs`
2. thêm mirror read-only cho `app_config`, `device_configs`, `print_template_profiles`, `users`
3. bổ sung integration test end-to-end có SQL Server test riêng
4. thêm cơ chế host service cho `Central API` trên Windows Server hoặc IIS/nginx reverse proxy
