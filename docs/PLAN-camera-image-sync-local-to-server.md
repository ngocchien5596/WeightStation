# Kế Hoạch Chi Tiết: Đồng Bộ Ảnh Camera Từ DB Local Lên DB Server

## 1. Mục tiêu

Thiết kế luồng đồng bộ ảnh camera từ trạm cân (`DB local`) lên hệ thống trung tâm (`DB server`) theo hướng:

- không làm chậm thao tác cân tại trạm
- không làm lỗi sync ảnh ảnh hưởng đến nghiệp vụ cân
- chống trùng khi retry
- hỗ trợ retry nền khi mất mạng hoặc server lỗi tạm thời
- lưu được đầy đủ metadata ảnh để tra cứu theo lượt cân

## 2. Bối cảnh

Phase camera local đã chốt:

- ảnh được chụp từ `2 camera RTSP`
- chụp tại:
  - `Lưu cân lần 1`
  - `Lưu cân lần 2`
- ảnh được lưu vào `DB local`

Nhu cầu tiếp theo:

- đồng bộ các ảnh này lên `DB server`

## 3. Nguyên tắc thiết kế

### 3.1 Không block thao tác cân

- `Lưu cân lần 1` và `Lưu cân lần 2` chỉ cần save local thành công
- sync ảnh lên server phải chạy nền
- lỗi sync ảnh không được rollback kết quả cân local

### 3.2 Idempotent

- gửi lại nhiều lần không được tạo ảnh trùng trên server
- server phải có khóa nghiệp vụ hoặc khóa kỹ thuật để upsert an toàn

### 3.3 Retry được

- mất mạng, timeout, lỗi server phải retry được
- cần lưu chi tiết lần retry cuối và lỗi cuối

### 3.4 Tách luồng ảnh khỏi luồng chứng từ nhẹ

- ảnh là dữ liệu nặng
- không nên dùng chung nhịp sync với dữ liệu text nhẹ nếu không có phân loại rõ

## 4. Dữ liệu local cần bổ sung

Trên bảng ảnh local, ví dụ `weighing_session_images`, nên bổ sung:

- `SyncStatus`
- `LastSyncAttemptAt`
- `LastSyncSuccessAt`
- `LastSyncError`
- `RetryCount`
- `ServerImageId` (nếu server cấp lại ID)
- `SyncBatchId` (optional)

### 4.1 Giá trị trạng thái đề xuất

- `PENDING`
- `SYNCING`
- `SYNCED`
- `FAILED`

### 4.2 Ý nghĩa

- `PENDING`: chờ đồng bộ
- `SYNCING`: worker đang xử lý
- `SYNCED`: đã đồng bộ thành công
- `FAILED`: lần gần nhất lỗi, sẽ retry tiếp

## 5. Thiết kế dữ liệu server

### 5.1 Bảng server

Tạo bảng tương ứng ở server, ví dụ:

- `weighing_session_images`

### 5.2 Cột chính

- `Id`
- `SourceLocalImageId`
- `SessionNo`
- `WeighingSessionId` (nếu có map)
- `ErpCutOrderId` (optional, nếu cần truy từ ERP)
- `CaptureStage`
- `CameraCode`
- `CameraName`
- `CapturedAt`
- `CapturedBy`
- `ImageFormat`
- `ImageBytes`
- `FileSizeBytes`
- `CreatedAt`
- `UpdatedAt`

### 5.3 Khóa chống trùng

Ưu tiên 1 trong 2 cách:

- dùng `SourceLocalImageId` làm khóa idempotent
- hoặc unique theo:
  - `SessionNo + CaptureStage + CameraCode`

Khuyến nghị:

- dùng `SourceLocalImageId` vì rõ ràng và ít mơ hồ hơn

## 6. Hình thức sync đề xuất

Có 2 hướng kỹ thuật:

### 6.1 Hướng A: DB to DB qua stored procedure

Worker local gọi trực tiếp proc ở DB server, ví dụ:

- `sp_UpsertWeighingSessionImageFromStation`

Ưu điểm:

- phù hợp nếu hệ thống hiện tại đã sync nghiệp vụ theo SQL/DB
- dễ triển khai trong hạ tầng SQL Server hiện có

Nhược điểm:

- ảnh BLOB đi trực tiếp vào SQL dễ nặng kết nối
- coupling cao với DB server

### 6.2 Hướng B: HTTP API upload ảnh

Worker local gọi API upload ảnh lên server.

Ưu điểm:

- dễ scale hơn
- dễ chuyển sang object storage/file storage sau này
- kiểm soát timeout/retry tốt hơn

Nhược điểm:

- cần thêm service/API layer

### 6.3 Khuyến nghị

Nếu mục tiêu là triển khai nhanh, gần với kiến trúc hiện tại:

- phase 2 gần: `DB to DB via stored procedure`

Nếu mục tiêu dài hạn:

- phase sau: chuyển sang `API upload`

## 7. Cơ chế queue sync

### 7.1 Khuyến nghị

Không sync trực tiếp từ thao tác cân.

Thay vào đó:

1. Ảnh lưu local
2. đánh `SyncStatus = PENDING`
3. background worker quét ảnh pending để sync

### 7.2 Có dùng lại sync_outbox không?

Có 2 lựa chọn:

- dùng lại `sync_outbox`
- tạo queue logic riêng ngay trên bảng ảnh

Khuyến nghị phase đầu:

- dùng trực tiếp cột trạng thái trên `weighing_session_images`
- chưa cần bọc thêm `sync_outbox`

Lý do:

- đơn giản hơn
- dễ query retry
- ảnh là payload nặng, không cần nhân đôi payload ở outbox

## 8. Worker sync nền

### 8.1 Cách chạy

Worker nền chạy theo chu kỳ, ví dụ:

- mỗi `30 giây` hoặc `60 giây`

### 8.2 Luồng xử lý

1. lấy top N ảnh `PENDING/FAILED`
2. đánh `SYNCING`
3. gửi lên server
4. nếu thành công:
   - `SYNCED`
   - set `LastSyncSuccessAt`
   - clear `LastSyncError`
5. nếu thất bại:
   - `FAILED`
   - tăng `RetryCount`
   - cập nhật `LastSyncError`

### 8.3 Batch size

Khuyến nghị:

- mỗi batch `5-20 ảnh`

Không nên sync batch quá lớn vì:

- ảnh nặng
- dễ timeout
- khó recovery

## 9. Retry policy

### 9.1 Nguyên tắc

- retry tự động cho lỗi tạm thời
- không retry vô hạn với lỗi dữ liệu không hợp lệ

### 9.2 Gợi ý

- retry tối đa `10 lần`
- backoff:
  - lần 1: ngay chu kỳ sau
  - lần 2-5: tăng dần
  - sau đó giữ nhịp chậm hơn

### 9.3 Phân loại lỗi

#### Lỗi tạm thời

- timeout
- mất mạng
- deadlock
- server bận

=> retry

#### Lỗi dữ liệu

- thiếu khóa nghiệp vụ
- image bytes rỗng
- server reject schema

=> đánh `FAILED`, cần can thiệp

## 10. Định danh nghiệp vụ ảnh

Để phía server tra cứu được ảnh theo nghiệp vụ, nên sync kèm:

- `SessionNo`
- `WeighingSessionId` local
- `CaptureStage`
- `CameraCode`
- `CapturedAt`
- `StationCode` (nếu hệ thống có mã trạm)
- `ErpCutOrderId` hoặc danh sách cut order liên quan nếu cần

Khuyến nghị tối thiểu:

- `SourceLocalImageId`
- `SessionNo`
- `CaptureStage`
- `CameraCode`

## 11. Dung lượng và tối ưu lưu trữ

### 11.1 Định dạng ảnh

- lưu `JPEG`

### 11.2 Giới hạn nên áp

- quality mặc định vừa phải
- resize hợp lý trước khi lưu nếu stream quá lớn

### 11.3 Vì sao cần tối ưu

Nếu mỗi lượt cân có:

- `2 camera`
- `2 thời điểm`

thì mỗi session có tối đa `4 ảnh`

Lượng dữ liệu sẽ tăng rất nhanh nếu:

- ảnh quá lớn
- lưu gốc full HD không nén hợp lý

## 12. Giao ước proc/API phía server

### 12.1 Nếu dùng stored procedure

Đề xuất proc:

- `dbo.sp_UpsertWeighingSessionImageFromStation`

Input:

- `@SourceLocalImageId`
- `@SessionNo`
- `@CaptureStage`
- `@CameraCode`
- `@CameraName`
- `@CapturedAt`
- `@CapturedBy`
- `@ImageFormat`
- `@ImageBytes`
- `@FileSizeBytes`

Quy tắc:

- nếu `SourceLocalImageId` đã tồn tại thì update/idempotent return success
- nếu chưa tồn tại thì insert mới

### 12.2 Nếu dùng API

Đề xuất endpoint:

- `POST /api/weighing-session-images/upsert`

Payload tương đương proc ở trên.

## 13. Tác động code dự kiến

### 13.1 Domain / Infrastructure local

- entity ảnh bổ sung metadata sync
- repository ảnh
- background sync worker
- config cho batch size / interval / retry

### 13.2 Application

- service đánh dấu pending sync sau khi lưu ảnh
- service điều phối sync nền

### 13.3 Server side

- bảng ảnh server
- proc/API upsert ảnh
- logging lỗi upload

## 14. Trình tự triển khai đề xuất

### Bước 1

- mở rộng bảng local `weighing_session_images` thêm metadata sync

### Bước 2

- tạo bảng ảnh phía server

### Bước 3

- tạo proc/API upsert ảnh trên server

### Bước 4

- viết background worker sync ảnh ở local

### Bước 5

- thêm logging và retry policy

### Bước 6

- test end-to-end với:
  - mạng tốt
  - mất mạng
  - retry
  - gửi trùng

## 15. Tiêu chí nghiệm thu

- lưu cân local vẫn nhanh, không bị block bởi sync ảnh
- ảnh mới chụp được đánh `PENDING`
- worker sync đẩy được ảnh lên server
- ảnh sync lại không sinh trùng
- mất mạng thì ảnh vẫn retry được sau
- lỗi sync ảnh không làm hỏng session cân local

## 16. Khuyến nghị chốt trước khi code

Cần chốt 3 điểm:

1. Sync ảnh lên `DB server` trực tiếp hay qua `API`
2. Ảnh server có lưu `BLOB` trong DB hay chỉ lưu metadata + file path
3. Có cần phase đầu làm luôn màn tra cứu trạng thái sync ảnh hay chưa

## 17. Hướng mình khuyến nghị

Nếu cần triển khai nhanh, ít thay đổi hạ tầng:

- local lưu `varbinary(max)`
- server cũng lưu `varbinary(max)`
- sync qua `stored procedure`
- worker nền retry theo batch nhỏ

Nếu sau này số lượng ảnh tăng lớn:

- chuyển dần sang `API + object storage`

