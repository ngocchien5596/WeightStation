# Kế hoạch triển khai sync backup qua Internet cho QN02/QN03

## 1. Mục tiêu

QN02 (Mỏ đá) và QN03 (Mỏ sét) không thông mạng nội bộ về Central DB/API hiện tại, nhưng có Internet ngoài. Cần bổ sung cơ chế đồng bộ dữ liệu từ máy trạm QN02/QN03 về một máy tính backup có kết nối Internet để khi máy trạm hỏng đột xuất vẫn còn dữ liệu phục hồi.

Mục tiêu chính:

- Tận dụng cơ chế sync hiện tại: `SyncOutboxWorker` -> `CentralApiClient` -> `StationApp.CentralApi` -> SQL backup.
- QN02/QN03 sync được qua Internet, không phụ thuộc mạng nội bộ.
- Không ảnh hưởng luồng sync hiện tại của QN01.
- Dữ liệu backup đủ dùng để tra cứu và phục hồi sau sự cố.
- Có trạng thái, log, hướng dẫn kiểm tra và hướng dẫn phục hồi rõ ràng.

## 2. Hiện trạng code đã rà soát

### 2.1. Local app

Các thành phần đang có:

- `src/StationApp.Sync/Services/SyncOutboxWorker.cs`
  - Tự tìm các aggregate có `SyncStatus = SYNC_QUEUED`.
  - Tạo/cập nhật bản ghi `sync_outbox`.
  - POST payload lên API theo từng aggregate.
  - Khi thành công cập nhật `SyncStatus = SYNC_SUCCESS`.
  - Khi lỗi cập nhật `SyncStatus = SYNC_FAILED` và lưu lỗi.

- `src/StationApp.Sync/Services/CentralApiClient.cs`
  - Đọc `central_api_url` từ cấu hình app.
  - Gửi các aggregate:
    - `cut_orders`
    - `weigh_tickets`
    - `delivery_tickets`
    - `weighing_sessions`
    - `weighing_session_lines`
    - `vehicles`
    - `customers`
    - `products`
    - `incoming_seed_vehicles`
    - `stations`

- `src/StationApp.Sync/Services/WeighingSessionImageSyncWorker.cs`
  - Sync riêng ảnh cân trong `weighing_session_images`.

- `src/StationApp.Sync/Services/CentralApiImageSyncClient.cs`
  - Cũng đọc `central_api_url`.
  - POST ảnh lên `api/weighing-session-images`.

- `src/StationApp.Domain/Constants/AppConfigKeys.cs`
  - Đã có:
    - `central_api_url`
    - `central_api_key`
    - `sync_interval_seconds`

Kết luận: có thể dùng lại hầu như nguyên cơ chế sync hiện tại nếu máy backup chạy được `StationApp.CentralApi` trên endpoint Internet mà QN02/QN03 truy cập được.

### 2.2. Backup/Central API

Các thành phần đang có:

- `src/StationApp.CentralApi/Program.cs`
  - Có `/health`.
  - Có middleware API key.
  - Có các endpoint nhận sync aggregate.
  - Có bootstrap tự bổ sung schema còn thiếu trên DB nhận sync.

- `src/StationApp.CentralApi/Services/SyncEndpointHandler.cs`
  - Upsert theo `Id`.
  - Có `sync_ingestion_logs` để biết request đã nhận, thành công hay lỗi.

Kết luận: có thể deploy một instance `StationApp.CentralApi` độc lập trên máy backup Internet, trỏ vào database backup riêng, ví dụ `StationAppBackup_QN02_QN03`.

## 3. Quyết định kiến trúc đề xuất

### 3.1. Dùng lại `StationApp.CentralApi` làm Backup API

Không tạo project API mới ở giai đoạn đầu. Deploy lại chính `StationApp.CentralApi` trên máy backup có Internet, nhưng cấu hình DB và API key riêng.

Lý do:

- Cơ chế upsert/idempotency đã có.
- Schema bootstrap đã có.
- Local worker không cần biết đó là “central” hay “backup”, chỉ cần URL/API key đúng.
- Ít thay đổi code, giảm rủi ro.

Tên vận hành đề xuất:

- Dịch vụ: `StationApp.BackupApi`
- Project code dùng lại: `StationApp.CentralApi`
- Database: `StationAppBackup_QN02_QN03`
- Endpoint Internet: ví dụ `https://station-backup.company-domain/...`

### 3.2. QN02/QN03 trỏ `central_api_url` về Backup API Internet

Với QN02/QN03:

- `central_api_url` = URL Internet của Backup API.
- `central_api_key` = key riêng cho nhóm QN02/QN03.

Với QN01:

- Giữ nguyên cơ chế hiện tại nếu QN01 vẫn thông mạng nội bộ về Central API/DB.

### 3.3. Bắt buộc lọc và nhận diện theo `StationCode`

Backup DB nhận cả QN02 và QN03 trong cùng database. Mọi dữ liệu phải có `StationCode` để phân biệt.

Yêu cầu:

- QN02 chỉ sync dữ liệu `StationCode = QN02`.
- QN03 chỉ sync dữ liệu `StationCode = QN03`.
- Màn trạng thái sync local vẫn filter theo trạm đang thao tác như hiện tại.
- Truy vấn/phục hồi phải luôn lọc theo `StationCode`.

### 3.4. Bổ sung coverage dữ liệu để backup khôi phục được

Sync hiện tại phù hợp để xem lại nghiệp vụ cân, nhưng để dùng như backup phục hồi máy thì cần bổ sung thêm các bảng chưa được mirror đầy đủ.

Nhóm đã sync:

- `cut_orders`
- `weigh_tickets`
- `delivery_tickets`
- `weighing_sessions`
- `weighing_session_lines`
- `weighing_session_images`
- `vehicles`
- `customers`
- `products`
- `incoming_seed_vehicles`
- `stations`
- `station_feature_flags`
- `station_operation_settings`

Nhóm cần cân nhắc bổ sung:

- `audit_logs`: để giữ lịch sử chỉnh sửa sau sự cố.
- `users` và phân quyền trạm: để phục hồi tài khoản/quyền nếu máy mất.
- `app_config`: chỉ nên backup các cấu hình nghiệp vụ chung, không backup cấu hình local như COM port, printer, camera local nếu dễ gây sai khi restore sang máy khác.
- `print_template_profiles`: nếu có profile/vị trí in chỉnh trên máy trạm cần phục hồi.
- Các bảng cấu hình phát sinh khác nếu trong DB local có dùng và có `StationCode`.

## 4. Luồng vận hành đề xuất

1. Máy trạm QN02/QN03 phát sinh hoặc sửa dữ liệu local.
2. Use case nghiệp vụ set `SyncStatus = SYNC_QUEUED`.
3. `SyncOutboxWorker` tạo/cập nhật `sync_outbox`.
4. Worker gửi payload qua Internet đến Backup API.
5. Backup API kiểm tra `X-Api-Key`.
6. Backup API upsert vào DB backup theo `Id`.
7. Backup API ghi `sync_ingestion_logs`.
8. Máy trạm cập nhật trạng thái sync thành công/thất bại.
9. Khi sự cố máy trạm:
   - Dùng DB backup để tra cứu hoặc restore dữ liệu cho trạm tương ứng.

## 5. Phạm vi triển khai

### Trong scope

- Cấu hình và hardening để QN02/QN03 sync qua Internet.
- Tài liệu deploy Backup API trên máy Internet.
- Tài liệu cấu hình máy trạm QN02/QN03.
- Bổ sung kiểm tra/trạng thái để người dùng biết đang sync tới endpoint nào.
- Bổ sung coverage dữ liệu còn thiếu nếu cần phục hồi đầy đủ.
- Tài liệu restore dữ liệu theo `StationCode`.

### Ngoài scope giai đoạn đầu

- Đồng bộ hai chiều từ backup về trạm.
- Realtime replication mức SQL Server.
- Public UI web để xem dữ liệu backup.
- Tự động restore hoàn toàn không cần người kỹ thuật.

## 6. Task triển khai chi tiết

### Task 1. Chốt mô hình Backup API qua Internet

**Mô tả:** Xác định cách expose API từ máy backup ra Internet để QN02/QN03 gọi được.

**Phương án triển khai chấp nhận được:**

- Máy backup có IP public/static domain và mở port HTTPS.
- Hoặc dùng tunnel/reverse proxy có HTTPS.
- Hoặc VPN overlay nếu hai trạm có thể join cùng mạng ảo qua Internet.

**Tiêu chí nghiệm thu:**

- [ ] Từ máy QN02 mở được `GET /health`.
- [ ] Từ máy QN03 mở được `GET /health`.
- [ ] Endpoint dùng HTTPS hoặc có phương án bảo vệ tương đương.
- [ ] API key không để mặc định trong bản publish.

**Files có thể chạm:**

- `docs/DEPLOY-backup-api-internet.md`
- `src/StationApp.CentralApi/appsettings.json`

**Độ lớn:** S

### Task 2. Tạo tài liệu deploy Backup API và DB backup

**Mô tả:** Viết runbook riêng cho backup qua Internet, tránh nhầm với Central API nội bộ.

**Tiêu chí nghiệm thu:**

- [ ] Có hướng dẫn publish `StationApp.CentralApi`.
- [ ] Có hướng dẫn cấu hình `ConnectionStrings:CentralConnection` trỏ DB backup.
- [ ] Có hướng dẫn cấu hình `CentralApi:ApiKey`.
- [ ] Có hướng dẫn chạy dạng Windows Service/Task Scheduler.
- [ ] Có checklist test `/health` và test từ QN02/QN03.

**Files có thể chạm:**

- `docs/DEPLOY-backup-api-internet.md`
- `docs/RUNBOOK-internet-backup-sync-qn02-qn03.md`

**Độ lớn:** M

### Task 3. Bổ sung nhãn cấu hình sync để người vận hành không nhầm đích

**Mô tả:** Trên màn cấu hình/thông tin sync, hiển thị rõ URL hiện tại đang dùng là endpoint nội bộ hay backup Internet.

**Tiêu chí nghiệm thu:**

- [ ] Màn sync hiển thị URL endpoint hiện tại.
- [ ] Test kết nối vẫn dùng `central_api_url`.
- [ ] Không đổi hành vi QN01.
- [ ] Lỗi cấu hình hiển thị dễ hiểu: URL sai, API key sai, timeout.

**Files có thể chạm:**

- `src/StationApp.UI/ViewModels/Settings/SyncInfoViewModel.cs`
- `src/StationApp.UI/Views/Settings/SyncInfoView.xaml`
- `src/StationApp.Sync/Services/CentralApiHealthChecker.cs`

**Độ lớn:** M

### Task 4. Rà soát và bổ sung sync coverage cho dữ liệu phục hồi

**Mô tả:** Xác định bảng nào cần có trên backup để phục hồi máy QN02/QN03 sau sự cố, sau đó bổ sung aggregate/outbox/API endpoint nếu còn thiếu.

**Bảng ưu tiên bổ sung:**

- `audit_logs`
- `users`
- bảng phân quyền trạm của user, nếu đang tách riêng
- `print_template_profiles`
- cấu hình nghiệp vụ chung trong `app_config`

**Nguyên tắc với `app_config`:**

- Không sync cấu hình thiết bị local có khả năng khác nhau theo máy:
  - COM port
  - printer mặc định
  - camera RTSP local nếu không dùng lại được ở máy mới
- Chỉ sync các cấu hình nghiệp vụ chung nếu thật sự cần phục hồi.

**Tiêu chí nghiệm thu:**

- [ ] Có danh sách bảng đã sync và chưa sync.
- [ ] Các bảng cần backup có endpoint/API hoặc cơ chế payload tương ứng.
- [ ] Backup DB tự bootstrap được schema mới.
- [ ] Dữ liệu có `StationCode` hoặc có cách gắn station khi restore.

**Files có thể chạm:**

- `src/StationApp.Domain/Constants/SyncAggregateTypes.cs`
- `src/StationApp.Sync/Services/SyncOutboxWorker.cs`
- `src/StationApp.Sync/Services/CentralApiClient.cs`
- `src/StationApp.CentralApi/Program.cs`
- `src/StationApp.Infrastructure/Services/InfrastructureServices.cs`
- repository/use case của từng bảng cần sync

**Độ lớn:** L, nên tách nhỏ theo từng nhóm bảng.

### Task 5. Bảo vệ không sync nhầm trạm hoặc thiếu `StationCode`

**Mô tả:** Gia cố phía local và API để dữ liệu QN02/QN03 luôn có `StationCode`, tránh backup lẫn hoặc mất khả năng restore.

**Tiêu chí nghiệm thu:**

- [ ] Payload nghiệp vụ bắt buộc có `StationCode`.
- [ ] Backup API reject payload nghiệp vụ thiếu `StationCode`.
- [ ] `sync_ingestion_logs` lưu đúng `StationCode`.
- [ ] Query kiểm tra backup theo `StationCode` trả đúng dữ liệu QN02/QN03.

**Files có thể chạm:**

- `src/StationApp.CentralApi/Services/SyncEndpointHandler.cs`
- `src/StationApp.Infrastructure/Persistence/SchemaCompatibilityBootstrapper.cs`
- `src/StationApp.CentralApi/Program.cs`

**Độ lớn:** S

### Task 6. Kiểm tra sync ảnh qua Internet

**Mô tả:** Ảnh cân có dung lượng lớn hơn payload thường, cần kiểm tra riêng để tránh nghẽn Internet hoặc fail timeout.

**Tiêu chí nghiệm thu:**

- [ ] QN02 sync được ảnh cân lên backup DB.
- [ ] QN03 sync được ảnh cân lên backup DB.
- [ ] Nếu ảnh fail, local vẫn lưu nghiệp vụ cân bình thường.
- [ ] `LastSyncError` ảnh có thông tin dễ hiểu.
- [ ] Có khuyến nghị cấu hình JPEG quality/max dimension để phù hợp đường truyền.

**Files có thể chạm:**

- `src/StationApp.Sync/Services/WeighingSessionImageSyncWorker.cs`
- `src/StationApp.Sync/Services/CentralApiImageSyncClient.cs`
- `docs/RUNBOOK-internet-backup-sync-qn02-qn03.md`

**Độ lớn:** S

### Task 7. Thêm công cụ kiểm tra trạng thái backup trên máy trạm

**Mô tả:** Mở rộng màn sync hiện có để người vận hành biết backup đang chạy tốt hay không.

**Tiêu chí nghiệm thu:**

- [ ] Hiển thị số pending/failed/success theo trạm hiện tại.
- [ ] Hiển thị lỗi gần nhất.
- [ ] Có nút test kết nối.
- [ ] Có nút đồng bộ lại chứng từ lỗi như hiện tại.
- [ ] Tên hiển thị tránh gây nhầm: “Đồng bộ backup” hoặc “Đồng bộ máy chủ”.

**Files có thể chạm:**

- `src/StationApp.UI/Views/Settings/SyncInfoView.xaml`
- `src/StationApp.UI/ViewModels/Settings/SyncInfoViewModel.cs`
- resource text nếu có.

**Độ lớn:** M

### Task 8. Viết script/query kiểm tra dữ liệu backup

**Mô tả:** Chuẩn bị query kiểm tra nhanh trên DB backup theo từng trạm.

**Tiêu chí nghiệm thu:**

- [ ] Có query xem session mới nhất theo QN02/QN03.
- [ ] Có query xem line, phiếu cân, phiếu giao nhận, ảnh.
- [ ] Có query xem lỗi ingest.
- [ ] Có query so sánh số lượng record local và backup trong một khoảng ngày.

**Files có thể chạm:**

- `scripts/sql/check-backup-sync-qn02-qn03.sql`
- `docs/RUNBOOK-internet-backup-sync-qn02-qn03.md`

**Độ lớn:** S

### Task 9. Tạo tài liệu phục hồi sau sự cố

**Mô tả:** Viết hướng dẫn khi máy QN02/QN03 hỏng: dựng máy mới, cài app, restore hoặc import dữ liệu từ backup.

**Tiêu chí nghiệm thu:**

- [ ] Có checklist dựng máy mới.
- [ ] Có cách lấy dữ liệu backup theo `StationCode`.
- [ ] Có cảnh báo không restore nhầm dữ liệu QN02 sang QN03.
- [ ] Có hướng dẫn xử lý cấu hình local như COM port, máy in, camera.
- [ ] Có hướng dẫn kiểm tra sau restore.

**Files có thể chạm:**

- `docs/RUNBOOK-restore-station-from-internet-backup.md`

**Độ lớn:** M

### Task 10. Kiểm thử end-to-end

**Mô tả:** Kiểm thử bằng môi trường dev hoặc staging trước khi triển khai thật.

**Kịch bản test:**

- QN02 tạo/sửa lượt cân mỏ đá.
- QN02 đánh dấu Hoàn.
- QN03 tạo tàu/chuyến xe/cân mỏ sét.
- QN03 chuyển chuyến.
- Chụp ảnh cân.
- Sửa danh mục xe/khách hàng/sản phẩm.
- Tắt Internet tạm thời rồi bật lại để kiểm tra retry.

**Tiêu chí nghiệm thu:**

- [ ] Khi online, dữ liệu lên backup DB.
- [ ] Khi offline, outbox chuyển failed/retryable nhưng nghiệp vụ local vẫn chạy.
- [ ] Khi online lại, dữ liệu pending sync thành công.
- [ ] Retry không tạo duplicate.
- [ ] DB backup có đủ dữ liệu để tra cứu theo `StationCode`.

**Files có thể chạm:**

- `tests/StationApp.Sync.Tests/*`
- `tests/StationApp.IntegrationTests/*`
- tài liệu runbook.

**Độ lớn:** M

## 7. Thứ tự triển khai đề xuất

1. Task 1: Chốt mô hình expose Backup API.
2. Task 2: Deploy thử Backup API + DB backup.
3. Task 8: Chuẩn bị query kiểm tra.
4. Task 10: Test sync hiện có với QN02/QN03 trên endpoint backup.
5. Task 3 + Task 7: Cải thiện UI/trạng thái để vận hành dễ.
6. Task 4: Bổ sung coverage dữ liệu phục hồi còn thiếu.
7. Task 5: Gia cố station scope.
8. Task 6: Test và tối ưu sync ảnh.
9. Task 9: Hoàn thiện tài liệu restore.

## 8. Rủi ro và cách xử lý

| Rủi ro | Mức độ | Cách xử lý |
|---|---:|---|
| Máy backup không có IP public, QN02/QN03 không gọi được API | Cao | Dùng domain/tunnel/VPN overlay; bắt buộc test `/health` từ hai trạm |
| Dữ liệu backup chưa đủ để phục hồi toàn bộ app | Cao | Bổ sung coverage cho audit/users/config/template profile theo Task 4 |
| Sync ảnh làm chậm đường truyền Internet | Trung bình | Giữ worker ảnh riêng, giới hạn batch, giảm JPEG quality/max dimension nếu cần |
| API key lộ hoặc endpoint public bị gọi trái phép | Cao | Dùng HTTPS, API key mạnh, firewall/IP allowlist nếu có thể, log request |
| QN02/QN03 sync nhầm station | Cao | Bắt buộc `StationCode`, query và màn trạng thái luôn lọc theo trạm |
| Outbox local đánh `SYNC_SUCCESS` nhưng backup DB thiếu dữ liệu do lỗi server không bắt được | Trung bình | Dựa vào response API và `sync_ingestion_logs`; bổ sung test đối soát định kỳ |
| Restore ghi đè cấu hình local sai máy | Trung bình | Tách cấu hình local và cấu hình nghiệp vụ; restore có checklist riêng |

## 9. Điểm cần chốt trước khi code

1. Máy backup Internet sẽ expose API bằng cách nào: IP public/domain, tunnel, hay VPN overlay?
2. Backup DB dùng SQL Server Express/local SQL trên máy backup hay SQL Server riêng?
3. Có cần backup cả `audit_logs`, `users`, `print_template_profiles`, `app_config` ngay phase đầu không, hay phase đầu chỉ cần dữ liệu nghiệp vụ cân?
4. Có yêu cầu mã hóa HTTPS bắt buộc ngay từ đầu không?
5. Có cần QN01 cũng sync thêm sang backup Internet như bản dự phòng thứ hai không, hay chỉ QN02/QN03?

## 10. Đề xuất chốt kỹ thuật ban đầu

Để làm nhanh và ít rủi ro nhất:

1. Deploy `StationApp.CentralApi` lên máy backup Internet, đặt tên vận hành là `StationApp.BackupApi`.
2. Tạo DB `StationAppBackup_QN02_QN03`.
3. QN02/QN03 cấu hình `central_api_url` trỏ tới Backup API Internet.
4. Giữ QN01 như hiện tại.
5. Phase đầu test dữ liệu nghiệp vụ và ảnh theo cơ chế hiện có.
6. Phase sau bổ sung các bảng còn thiếu để phục hồi đầy đủ.

## 11. Chốt yêu cầu sau phản hồi

Các điểm đã chốt:

1. Máy backup Internet sẽ expose API bằng **IP public/domain**.
2. Dữ liệu backup lưu trong **SQL Server**.
3. Backup phase đầu phải bao gồm cả:
   - dữ liệu nghiệp vụ cân,
   - ảnh cân,
   - `audit_logs`,
   - `users` và phân quyền trạm,
   - cấu hình cần thiết để phục hồi,
   - `print_template_profiles` nếu có chỉnh profile in trong DB.
4. Bắt buộc dùng **HTTPS** khi public API ra Internet.
5. Không yêu cầu QN01 sync sang backup Internet. QN01 giữ nguyên luồng sync nội bộ hiện tại.
6. Cần làm thêm cơ chế **restore ngược từ máy backup về máy trạm** khi máy trạm QN02/QN03 bị hỏng.
7. Kiên quyết không làm ảnh hưởng đến luồng sync nội bộ đang ổn định. Nếu cần tách code/folder/config riêng thì tách riêng.

## 12. Nguyên tắc tách biệt để không ảnh hưởng sync nội bộ hiện tại

### 12.0. Chốt routing sync theo trạm

Luồng sync dữ liệu được chốt như sau:

- `QN01`:
  - Chỉ dùng cơ chế sync hiện tại qua `Central API` nội bộ.
  - Dùng mạng nội bộ như đang chạy ổn định.
  - Không dùng `BackupSync` Internet.

- `QN02` và `QN03`:
  - Chỉ dùng cơ chế `BackupSync` qua Internet ngoài.
  - Không dùng `Central API` nội bộ.
  - Dữ liệu backup lưu về SQL Server của máy/server backup Internet.

Vì vậy khi code không được để một dữ liệu phát sinh bị đẩy đồng thời lên cả hai kênh, trừ khi sau này có yêu cầu mới. Worker phải route theo `StationCode`:

| StationCode | Kênh sync được phép | Kênh sync không dùng |
|---|---|---|
| `QN01` | `Central API` nội bộ | `BackupSync` Internet |
| `QN02` | `BackupSync` Internet | `Central API` nội bộ |
| `QN03` | `BackupSync` Internet | `Central API` nội bộ |

### 12.1. Không đổi hành vi mặc định của QN01

- Không đổi key `central_api_url`, `central_api_key` đang dùng cho QN01 nếu QN01 vẫn sync nội bộ.
- Không đổi endpoint hiện tại của `StationApp.CentralApi` nội bộ.
- Không đổi schema/logic nào có thể làm QN01 không sync được.
- Mọi thay đổi cho backup Internet phải có cấu hình bật/tắt rõ ràng và chỉ áp dụng cho `QN02`, `QN03`.

### 12.2. Tách cấu hình backup Internet

Thêm nhóm cấu hình riêng, không dùng lẫn với cấu hình sync nội bộ:

```json
{
  "BackupSync": {
    "Enabled": true,
    "ApiUrl": "https://backup-domain-or-public-ip/",
    "ApiKey": "...",
    "StationCodes": [ "QN02", "QN03" ],
    "IntervalSeconds": 30,
    "IncludeImages": true
  }
}
```

Không dùng `central_api_url` cho QN02/QN03 nữa nếu đã triển khai `BackupSync`, để tránh nhầm lẫn với kênh Central API nội bộ của QN01.

### 12.3. Tách module/folder code

Khi code, ưu tiên tạo folder/project riêng để cô lập:

- `src/StationApp.BackupSync/`
  - client backup,
  - worker backup nếu cần chạy song song với worker nội bộ,
  - restore client,
  - DTO restore.

- `src/StationApp.BackupApi/` nếu cần API riêng.
  - Có thể reuse logic từ `StationApp.CentralApi`.
  - Nếu chưa cần project mới, vẫn deploy `StationApp.CentralApi` nhưng đặt tên vận hành là `StationApp.BackupApi`.

- `docs/backup-sync/`
  - tài liệu deploy,
  - tài liệu vận hành,
  - tài liệu restore,
  - checklist test.

### 12.4. Trạng thái sync phải phản ánh đúng kênh theo trạm

Vì mỗi trạm chỉ dùng một kênh sync, có thể giữ cách đánh `SyncStatus` trên aggregate theo kênh được phép của trạm:

- `QN01`: `SyncStatus` phản ánh trạng thái sync lên Central API nội bộ.
- `QN02`, `QN03`: `SyncStatus` phản ánh trạng thái sync lên BackupSync Internet.

Tuy nhiên để code dễ đọc và tránh nhầm, phần BackupSync nên có outbox/worker/client riêng:

- hoặc bảng `backup_sync_outbox` riêng.

Phương án ưu tiên: tạo `backup_sync_outbox` riêng cho QN02/QN03 để không ảnh hưởng `sync_outbox` và worker nội bộ đang ổn định cho QN01. Nếu cần reuse `sync_outbox` để giảm scope, phải có điều kiện route rõ theo `StationCode` và test không làm QN01 đổi hành vi.

## 13. Cơ chế restore ngược từ backup về máy trạm

### 13.1. Mục tiêu restore

Khi máy trạm QN02/QN03 hỏng, có thể dựng máy mới và kéo dữ liệu từ máy backup về local DB để tiếp tục vận hành.

Restore phải đảm bảo:

- Chỉ restore đúng `StationCode` của trạm cần phục hồi.
- Không restore nhầm QN02 sang QN03 hoặc ngược lại.
- Không ghi đè cấu hình local đặc thù máy mới nếu không được xác nhận.
- Dữ liệu sau restore đủ để mở app, xem lịch sử cân, báo cáo, audit log và tiếp tục cân mới.

### 13.2. Phương án restore đề xuất

Tạo cơ chế restore dạng tool/luồng riêng, không chạy tự động trong app vận hành hằng ngày.

Các lựa chọn:

1. **Restore bằng command-line tool riêng**
   - Đề xuất ưu tiên.
   - Ví dụ project/folder: `src/StationApp.BackupRestoreTool/`.
   - Người kỹ thuật chạy khi dựng máy mới.
   - Ít rủi ro vì không lẫn vào UI vận hành.

2. **Restore bằng màn hình Admin trong app**
   - Chỉ Admin được dùng.
   - Cần khóa thao tác cân trong lúc restore.
   - Cần confirm nhiều bước để tránh ghi đè nhầm.

3. **Restore bằng script SQL xuất từ backup**
   - Có thể dùng làm phương án dự phòng.
   - Khó kiểm soát mapping/version schema hơn.

Đề xuất chốt: làm command-line restore tool trước, sau đó nếu cần mới đưa vào UI Admin.

### 13.3. Luồng restore command-line

Ví dụ:

```powershell
StationApp.BackupRestoreTool.exe `
  --backup-api-url "https://backup-domain/" `
  --api-key "..." `
  --station-code "QN02" `
  --local-connection "Server=.;Database=StationAppLocal;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;" `
  --mode "FullStationRestore"
```

Các bước tool thực hiện:

1. Kiểm tra Backup API `/health`.
2. Kiểm tra API key.
3. Kiểm tra `StationCode` tồn tại trên backup.
4. Hiển thị thống kê dữ liệu sẽ restore:
   - số lượt cân,
   - số dòng lượt cân,
   - số phiếu cân,
   - số phiếu giao nhận,
   - số ảnh,
   - số audit log,
   - số danh mục.
5. Yêu cầu xác nhận trước khi ghi DB local.
6. Bootstrap schema local trước khi restore.
7. Restore theo thứ tự phụ thuộc.
8. Ghi log restore vào file và vào bảng audit/system log nếu có.
9. Sau restore, chạy kiểm tra đối soát.

### 13.4. Thứ tự restore dữ liệu

Thứ tự đề xuất:

1. `stations`
2. phân quyền/user liên quan đến station
3. danh mục:
   - `vehicles`
   - `customers`
   - `products`
   - `incoming_seed_vehicles`
4. cấu hình nghiệp vụ chung:
   - `station_feature_flags`
   - `station_operation_settings`
   - `print_template_profiles`
5. nghiệp vụ:
   - `cut_orders`
   - `weighing_sessions`
   - `weighing_session_lines`
   - `weigh_tickets`
   - `delivery_tickets`
6. ảnh:
   - `weighing_session_images`
7. audit:
   - `audit_logs`
8. `sync_outbox`:
   - Không restore nguyên trạng thái cũ.
   - Sau restore, các bản ghi local nên ở trạng thái đã sync backup hoặc tạo lại outbox theo nhu cầu.

### 13.5. Cấu hình không nên restore tự động

Không restore tự động các thông số gắn với phần cứng/máy cụ thể:

- cổng COM cân,
- baudrate/parity nếu máy mới khác thiết bị,
- máy in mặc định,
- cấu hình camera local nếu IP/camera thay đổi,
- đường dẫn file local,
- cấu hình update app theo shared folder nội bộ.

Các cấu hình này nên giữ theo `appsettings.json` local của máy mới hoặc để người kỹ thuật cấu hình lại.

### 13.6. API restore cần bổ sung

Backup API hiện tại chủ yếu nhận POST sync. Để restore ngược cần thêm nhóm endpoint read-only:

- `GET /api/backup-export/stations/{stationCode}/summary`
- `GET /api/backup-export/stations/{stationCode}/vehicles`
- `GET /api/backup-export/stations/{stationCode}/customers`
- `GET /api/backup-export/stations/{stationCode}/products`
- `GET /api/backup-export/stations/{stationCode}/cut-orders`
- `GET /api/backup-export/stations/{stationCode}/weighing-sessions`
- `GET /api/backup-export/stations/{stationCode}/weighing-session-lines`
- `GET /api/backup-export/stations/{stationCode}/weigh-tickets`
- `GET /api/backup-export/stations/{stationCode}/delivery-tickets`
- `GET /api/backup-export/stations/{stationCode}/weighing-session-images`
- `GET /api/backup-export/stations/{stationCode}/audit-logs`

Với bảng lớn như ảnh, cần phân trang:

- `pageSize`
- `cursor` hoặc `updatedFrom/idFrom`
- checksum/tổng số bản ghi để đối soát.

### 13.7. Bảo mật restore

Restore API nguy hiểm hơn sync inbound vì cho đọc dữ liệu. Cần:

- HTTPS bắt buộc.
- API key restore riêng, không dùng chung key sync nếu có thể.
- Chỉ allow `StationCode` được phép theo key.
- Log đầy đủ request restore.
- Có thể giới hạn IP public của trạm/máy kỹ thuật nếu hạ tầng hỗ trợ.

## 14. Task bổ sung cho restore ngược

### Task 11. Tách cấu hình backup/restore khỏi sync nội bộ

**Mô tả:** Thêm nhóm cấu hình `BackupSync`/`BackupRestore` riêng để không làm QN01 hoặc sync nội bộ bị ảnh hưởng.

**Tiêu chí nghiệm thu:**

- [ ] QN01 chạy với cấu hình cũ không thay đổi hành vi.
- [ ] QN02/QN03 có thể bật backup sync riêng.
- [ ] Không bắt buộc nhập cấu hình backup ở QN01.
- [ ] Dữ liệu `QN01` không được gửi sang BackupSync.
- [ ] Dữ liệu `QN02`, `QN03` không được gửi sang Central API nội bộ.
- [ ] Test build và test kết nối sync nội bộ hiện tại vẫn pass.

**Files có thể chạm:**

- `src/StationApp.Domain/Constants/AppConfigKeys.cs`
- `src/StationApp.UI/appsettings.json`
- `src/StationApp.UI/App.xaml.cs`
- module/folder backup sync mới nếu tách.

**Độ lớn:** M

### Task 12. Bổ sung Backup API read-only export endpoints

**Mô tả:** Thêm endpoint đọc dữ liệu backup theo `StationCode` để restore về máy trạm.

**Tiêu chí nghiệm thu:**

- [ ] Mọi endpoint bắt buộc có `StationCode`.
- [ ] Không endpoint nào trả dữ liệu của station khác.
- [ ] Có phân trang cho bảng lớn.
- [ ] Có summary/count để đối soát trước và sau restore.
- [ ] API sync POST hiện tại vẫn hoạt động như cũ.

**Files có thể chạm:**

- `src/StationApp.CentralApi/Program.cs` hoặc project `src/StationApp.BackupApi/`
- `src/StationApp.CentralApi/Services/*`
- `src/StationApp.Contracts/Sync/*` hoặc contracts restore mới.

**Độ lớn:** L, nên chia nhỏ theo nhóm bảng.

### Task 13. Tạo restore tool riêng

**Mô tả:** Tạo command-line tool để kéo dữ liệu từ Backup API về local DB khi dựng lại máy trạm.

**Tiêu chí nghiệm thu:**

- [ ] Chạy được theo tham số `backup-api-url`, `api-key`, `station-code`, `local-connection`.
- [ ] Có chế độ dry-run hiển thị số lượng dữ liệu sẽ restore.
- [ ] Có confirm trước khi ghi DB.
- [ ] Restore đúng thứ tự phụ thuộc.
- [ ] Ghi log file restore.

**Files có thể chạm:**

- `src/StationApp.BackupRestoreTool/`
- `StationApp.sln`
- `docs/backup-sync/RUNBOOK-restore-station-from-internet-backup.md`

**Độ lớn:** L

### Task 14. Bootstrap local schema trước restore

**Mô tả:** Đảm bảo máy mới có DB local đủ schema trước khi import dữ liệu từ backup.

**Tiêu chí nghiệm thu:**

- [ ] Restore tool gọi được schema bootstrap/migration local.
- [ ] Không lỗi khi DB local mới hoàn toàn.
- [ ] Không lỗi khi DB local đã có một phần dữ liệu.
- [ ] Có chính sách xử lý trùng `Id`: upsert theo `Id`.

**Files có thể chạm:**

- `src/StationApp.Infrastructure/Persistence/SchemaCompatibilityBootstrapper.cs`
- `src/StationApp.BackupRestoreTool/`

**Độ lớn:** M

### Task 15. Đối soát sau restore

**Mô tả:** Sau restore, so sánh số lượng dữ liệu local với backup theo từng bảng quan trọng.

**Tiêu chí nghiệm thu:**

- [ ] Có báo cáo count local vs backup.
- [ ] Có danh sách bảng lệch nếu có.
- [ ] Có log lỗi chi tiết theo bảng.
- [ ] Người kỹ thuật biết restore đạt/chưa đạt.

**Files có thể chạm:**

- `src/StationApp.BackupRestoreTool/`
- `scripts/sql/check-backup-sync-qn02-qn03.sql`

**Độ lớn:** M

### Task 16. Tài liệu vận hành restore

**Mô tả:** Viết hướng dẫn đầy đủ cho tình huống máy trạm QN02/QN03 bị hỏng.

**Tiêu chí nghiệm thu:**

- [ ] Có checklist dựng máy mới.
- [ ] Có lệnh restore mẫu cho QN02 và QN03.
- [ ] Có cảnh báo cấu hình local không restore tự động.
- [ ] Có bước kiểm tra app sau restore.
- [ ] Có bước test cân mới sau restore.

**Files có thể chạm:**

- `docs/backup-sync/RUNBOOK-restore-station-from-internet-backup.md`

**Độ lớn:** S

## 15. Thứ tự triển khai cập nhật

1. Tạo folder tài liệu riêng `docs/backup-sync/` và chuyển/copy runbook backup vào đó.
2. Chốt endpoint IP public/domain + HTTPS cho Backup API.
3. Deploy thử Backup API vào SQL Server backup.
4. Tách cấu hình/module BackupSync trước, không dùng lẫn `central_api_url`.
5. Route worker theo trạm:
   - QN01 -> Central API nội bộ.
   - QN02/QN03 -> BackupSync Internet.
6. Test regression để chứng minh QN01 vẫn sync nội bộ như cũ.
7. Cấu hình QN02/QN03 test sync qua Internet bằng BackupSync.
8. Bổ sung sync coverage: audit, users, phân quyền, config nghiệp vụ, print template profiles.
9. Bổ sung read-only restore endpoints trên Backup API.
10. Tạo restore command-line tool riêng.
11. Test restore vào DB local sạch theo QN02/QN03.
12. Hoàn thiện tài liệu deploy, vận hành, đối soát và restore.
