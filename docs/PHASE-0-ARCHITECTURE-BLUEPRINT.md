# PHASE 0 — PRODUCT ARCHITECTURE & BLUEPRINT FINAL LOCKED

## 1. Mục tiêu hệ thống
Xây dựng một ứng dụng trạm cân chuẩn product bằng C# .NET + WPF, chạy trên Windows, dùng SQL Server Express local tại máy trạm, phục vụ quy trình:
- ERP hoặc người dùng tạo phiếu cân
- Nhân viên chọn phiếu cân
- Cân lần 1
- Xe vào làm hàng
- Cân lần 2
- Hệ thống tính `net_weight`
- Dữ liệu được đồng bộ từ DB local lên DB server
- Khi mất mạng vẫn thao tác được
- Khi có mạng lại thì tự sync an toàn

Mục tiêu thiết kế là:
- vận hành ổn định tại hiện trường
- không mất dữ liệu khi mất mạng hoặc app tắt đột ngột
- dễ audit, dễ debug
- đủ đơn giản để triển khai nhanh giai đoạn đầu
- mở đường cho phase sau nếu cần mở rộng

## 2. Quyết định chốt của Phase 0
- Chỉ dùng 1 aggregate chính là `weigh_tickets`
- Local DB dùng SQL Server Express
- Không tạo entity ERPOrder riêng trong local DB
- `ticket_no` format là `QNyyMM0001`
- Tất cả số cân và trọng lượng lưu theo kg
- Có lưu: `weight_1_is_stable`, `weight_2_is_stable`
- Giữ: `updated_at`, `updated_by`, `app_version`
- `status` có 4 trạng thái, `sync_status` có 3 trạng thái
- Tolerance ở Phase 1 chỉ cảnh báo vượt kế hoạch

## 3. Phạm vi domain
### 3.1 Aggregate trung tâm
Phase 0 chỉ dùng một aggregate: `WeighTicket`. Không có entity ERPOrder riêng trong local DB.

### 3.2 Ý nghĩa của WeighTicket
`WeighTicket` đồng thời là:
- snapshot dữ liệu nguồn từ ERP
- bản ghi vận hành tại trạm
- đối tượng đồng bộ lên server

## 4. Entity definition final
### 4.1 weigh_tickets — field final
- `id`: PK nội bộ, GUID
- `ticket_no`: format QNyyMM0001
- `erp_vehicle_registration_id`: mã chứng từ đăng ký phương tiện từ ERP
- `vehicle_plate`: Số PTVC
- `mooc_number`: số mooc
- `driver_name`: tên tài xế
- `customer_code/name`: mã/tên khách hàng
- `product_code/name`: mã/tên sản phẩm
- `planned_weight`: số lượng kế hoạch, đơn vị kg
- `bag_count`: số lượng bao
- `notes`: ghi chú
- `transaction_type`: OUTBOUND / INBOUND
- `transport_method`: ROAD / WATERWAY
- `is_cancelled`: cờ không lấy hàng
- `status`: trạng thái phiếu cân
- `idempotency_key`: UUID để sync đúng 1 lần
- `sync_status`: trạng thái đồng bộ local -> server
- **Cân 1**: `weight_1`, `weight_1_user`, `weight_1_time`, `weight_1_updated_at`, `weight_1_mode`, `weight_1_is_stable`
- **Cân 2**: `weight_2`, `weight_2_user`, `weight_2_time`, `weight_2_updated_at`, `weight_2_mode`, `weight_2_is_stable`
- `net_weight`: trọng lượng hàng thực tế, kg
- `app_version`: phiên bản ứng dụng tại thời điểm bản ghi được tạo/cập nhật
- `created_at/by`, `updated_at/by`

## 5. Enum và trạng thái final
- **status**: `TICKET_CREATED`, `LOADING_STARTED`, `TICKET_COMPLETED`, `TICKET_CANCELLED`
- **sync_status**: `SYNC_QUEUED`, `SYNC_SUCCESS`, `SYNC_FAILED`
- **transaction_type**: `OUTBOUND` (Xuất), `INBOUND` (Nhập)
- **transport_method**: `ROAD` (Đường bộ), `WATERWAY` (Đường thủy)
- **weight_mode**: `AUTO`, `MANUAL`

## 6. Business rules final
### 6.1 Rule chung về ticket
- Ticket có status = TICKET_CANCELLED thì không được cân tiếp.
- Ticket có status = TICKET_COMPLETED thì không được cân lại trong Phase 0.
- Ticket do ERP đẩy xuống thì created_by = ERP_SYSTEM.

### 6.2 Rule về cân lần 1 / lần 2
- Được phép ghi `weight_1`, `weight_2` kể cả khi chưa stable.
- Bắt buộc phải snapshot trạng thái ổn định: `weight_1_is_stable`, `weight_2_is_stable`.

### 6.3 Rule tính net_weight
- OUTBOUND: `net_weight` = `weight_2 - weight_1`
- INBOUND: `net_weight` = `weight_1 - weight_2`
- Validate: nếu `net_weight` < 0 thì không được complete ticket.

### 6.4 Rule tolerance
- Nếu `net_weight` > `planned_weight` + tolerance => cảnh báo (không block).

### 6.5 Rule cancel
- Nếu is_cancelled = 1 thì status phải là TICKET_CANCELLED.

### 6.6 Rule sync
- Mọi insert/update nghiệp vụ lên weigh_tickets phải sinh message vào outbox.
- idempotency_key tạo 1 lần và không đổi trong vòng đời ticket.
- Lỗi sync không làm thay đổi hay mất dữ liệu local nghiệp vụ.

## 7. State machine final
- TICKET_CREATED -> LOADING_STARTED -> TICKET_COMPLETED
- Nhánh hủy: CREATED/STARTED -> TICKET_CANCELLED

## 8. Logical architecture final
- **Station App**: Vận hành trực tiếp, đọc số cân, lưu local, cho phép offline, queue sync lên server.
- **Central Server**: Nguồn dữ liệu tập trung, chống sync trùng, phục vụ admin web.

## 9. Local SQL Server schema final
- `weigh_tickets`
- `sync_outbox`
- `audit_logs`
- `app_config`
- `device_configs`
- `users`

## 10. Central SQL Server schema final
- Bảng chính: `weigh_tickets`, `weigh_ticket_history`, `idempotency_records`, `stations`, `users`, `roles`, `audit_logs`.

## 11. Sync flow final
- Inbound: Station app pull theo chu kỳ.
- Outbound: Cập nhật ticket -> Save local SQL -> Enqueue sync_outbox -> Background worker gửi lên API -> Success (SYNC_SUCCESS) / Fail (SYNC_FAILED).
- Retry theo lịch: 30s, 2m, 10m, 30m, 2h.

## 12. API contract final
- Nhận Pull (Inbound): `GET /api/stations/{stationCode}/weigh-tickets/changes?since={cursor}`
- Đẩy Push (Outbound): `POST /api/weigh-tickets` với Header `Idempotency-Key`

## 13. Security & roles final
- `OPERATOR`: xem ticket, cân lần 1/2, complete/cancel ticket, in phiếu.
- `SUPERVISOR`: mọi quyền operator + retry sync, override warning tolerance, xử lý lỗi sync.
- `ADMIN`: cấu hình hệ thống, cấu hình thiết bị, quản lý user/role, export log.
