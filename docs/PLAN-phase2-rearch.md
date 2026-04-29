# PHASE 2 RE-ARCHITECTURE – FINAL LOCKED SPECIFICATION

## 1. Mục tiêu của bản re-architecture
Mục tiêu của lần re-architecture này là sửa lại một sai lệch kiến trúc quan trọng:
- Trước đây hệ thống đang xoay quanh `weigh_tickets`
- Nhưng nghiệp vụ thực tế cho thấy thực thể gốc phải là **Đăng ký phương tiện** (`vehicle_registrations`).

Vì vậy, từ bản này trở đi:
- **Aggregate root chính thức**: `vehicle_registrations`
- Các thực thể `weigh_tickets` và `delivery_tickets` chỉ là child documents phát sinh từ `vehicle_registrations`.

## 2. Kết luận nghiệp vụ đã chốt
### 2.1 Root business
ERP gửi xuống phần mềm cân Đăng ký phương tiện, không phải phiếu cân.

### 2.2 Chứng từ phát sinh
Từ một `vehicle_registration` có thể phát sinh:
- 1 hoặc nhiều `weigh_tickets`
- 1 hoặc nhiều `delivery_tickets`

### 2.3 Workflow trung tâm
Workflow nghiệp vụ phải nằm ở `vehicle_registrations.registration_status`. Không đặt workflow nghiệp vụ chính ở `weigh_tickets` nữa.

### 2.4 Child documents
`weigh_tickets` và `delivery_tickets` không còn là root, không giữ full business status. Chỉ giữ:
- Dữ liệu chứng từ
- Cờ in (`is_printed`)
- Cờ quá tải (`is_over_weight`)
- Vai trò record (`record_role`)
- Nhóm liên quan (`split_group_id`)
- Technical sync status.

## 3. Những gì phải thay đổi so với các phase trước
- Bỏ mô hình `weigh_tickets` là trung tâm.
- Chuyển toàn bộ business workflow state sang `vehicle_registrations`.
- WeightView bind theo `vehicle_registration`. Grid hiển thị 1 dòng / 1 `vehicle_registration`.
- ERP inbound đi vào `vehicle_registrations`.

## 4. Quy định cập nhật tài liệu cũ
AI phải coi toàn bộ tài liệu Phase 0, Phase 1, Phase 2 cũ là lỗi thời nếu mâu thuẫn với bản này.
- Tạo revision/addendum cho Phase 0 & Phase 1.
- Đánh dấu Phase 2 cũ là `SUPERSEDED`.

## 5. Data model mới
### 5.1 Aggregate root: vehicle_registrations
- Lưu dữ liệu nghiệp vụ gốc nhận từ ERP hoặc tạo tay.
- Là cha của `weigh_tickets` và `delivery_tickets`.
- Là thực thể chính hiển thị trong grid của WeightView.

### 5.2 Child document: weigh_tickets
- Phải có `vehicle_registration_id`.
- Không giữ full business workflow status. Chỉ giữ dữ liệu chứng từ cân.

### 5.3 Child document: delivery_tickets
- Phải có `vehicle_registration_id`.
- Chủ yếu phục vụ in, liên kết PGN, grouping.

## 6. Schema chính thức
### 6.1 Bảng vehicle_registrations
```sql
vehicle_registrations
- id                                   uniqueidentifier PK
- erp_vehicle_registration_id          nvarchar(50)     NULL
- registration_source                  nvarchar(20)     NOT NULL
- registration_status                  nvarchar(30)     NOT NULL
- transaction_type                     nvarchar(20)     NOT NULL
- transport_method                     nvarchar(20)     NULL
- vehicle_plate                        nvarchar(30)     NOT NULL
- mooc_number                          nvarchar(30)     NULL
- receiver_name                        nvarchar(100)    NULL
- receiver_id_no                       nvarchar(50)     NULL
- customer_code                        nvarchar(50)     NULL
- customer_name                        nvarchar(255)    NULL
- product_code                         nvarchar(50)     NULL
- product_name                         nvarchar(255)    NULL
- planned_weight                       decimal(18,3)    NULL
- bag_count                            int              NULL
- notes                                nvarchar(500)    NULL
- is_cancelled                         bit              NOT NULL DEFAULT 0
- has_overweight_case                  bit              NOT NULL DEFAULT 0
- current_primary_weigh_ticket_id      uniqueidentifier NULL
- current_primary_delivery_ticket_id   uniqueidentifier NULL
- sync_status                          nvarchar(20)     NOT NULL
- idempotency_key                      uniqueidentifier NOT NULL
- app_version                          nvarchar(50)     NULL
- created_at                           datetime2        NOT NULL
- created_by                           nvarchar(100)    NOT NULL
- updated_at                           datetime2        NULL
- updated_by                           nvarchar(100)    NULL
```
**Constraints**:
- `UNIQUE(erp_vehicle_registration_id) WHERE erp_vehicle_registration_id IS NOT NULL`
- `INDEX(registration_status)`, `INDEX(sync_status)`, `INDEX(vehicle_plate)`, `INDEX(created_at)`

### 6.2 Bảng weigh_tickets
```sql
weigh_tickets
- id                          uniqueidentifier PK
- vehicle_registration_id     uniqueidentifier NOT NULL
- ticket_no                   nvarchar(20)     UNIQUE NOT NULL
- weight_1                    decimal(18,3)    NULL
- weight_1_user               nvarchar(100)    NULL
- weight_1_time               datetime2        NULL
- weight_1_updated_at         datetime2        NULL
- weight_1_mode               nvarchar(20)     NULL
- weight_1_is_stable          bit              NULL
- weight_2                    decimal(18,3)    NULL
- weight_2_user               nvarchar(100)    NULL
- weight_2_time               datetime2        NULL
- weight_2_updated_at         datetime2        NULL
- weight_2_mode               nvarchar(20)     NULL
- weight_2_is_stable          bit              NULL
- net_weight                  decimal(18,3)    NULL
- is_over_weight              bit              NOT NULL DEFAULT 0
- is_primary_display          bit              NOT NULL DEFAULT 1
- is_printed                  bit              NOT NULL DEFAULT 0
- split_group_id              uniqueidentifier NULL
- split_sequence              tinyint          NULL
- source_ticket_id            uniqueidentifier NULL
- record_role                 nvarchar(20)     NOT NULL DEFAULT 'WORKING'
- sync_status                 nvarchar(20)     NOT NULL
- app_version                 nvarchar(50)     NULL
- created_at                  datetime2        NOT NULL
- created_by                  nvarchar(100)    NOT NULL
- updated_at                  datetime2        NULL
- updated_by                  nvarchar(100)    NULL
```

### 6.3 Bảng delivery_tickets
```sql
delivery_tickets
- id                          uniqueidentifier PK
- vehicle_registration_id     uniqueidentifier NOT NULL
- delivery_no                 nvarchar(30)     UNIQUE NOT NULL
- notes                       nvarchar(500)    NULL
- is_over_weight              bit              NOT NULL DEFAULT 0
- is_printed                  bit              NOT NULL DEFAULT 0
- split_group_id              uniqueidentifier NULL
- split_sequence              tinyint          NULL
- source_delivery_ticket_id   uniqueidentifier NULL
- record_role                 nvarchar(20)     NOT NULL DEFAULT 'WORKING'
- sync_status                 nvarchar(20)     NOT NULL
- created_at                  datetime2        NOT NULL
- created_by                  nvarchar(100)    NOT NULL
- updated_at                  datetime2        NULL
- updated_by                  nvarchar(100)    NULL
```

## 7. Trạng thái chính thức của vehicle_registrations
- `REGISTERED`: Đã nhận đăng ký phương tiện từ ERP hoặc tạo tay.
- `LOADING_IN_PROGRESS`: Đã có cân lần 1, đang lấy hàng.
- `OVERWEIGHT_PENDING_ACTION`: Đã cân lần 2 phát hiện quá tải.
- `COMPLETED`: Đã hoàn tất nghiệp vụ.
- `CANCELLED`: Đăng ký bị hủy.

### Luồng xử lý Quá tải (Chốt):
`REGISTERED` -> `LOADING_IN_PROGRESS` -> `OVERWEIGHT_PENDING_ACTION`
- Khi xử lý xong case quá tải hợp lệ, có thể quay lại `LOADING_IN_PROGRESS` (nếu cần cân lại/thao tác tiếp) hoặc `COMPLETED`. Không nhảy tắt sang `COMPLETED` nếu chưa xử lý xong.

## 8. Bảng master data
### 8.1 vehicles
- **Business key**: `(vehicle_plate, mooc_number)`
- Chứa: TTCP (`ttcp_weight`), Số & Hạn đăng kiểm xe/mooc. Không lưu ở ERP payload.

### 8.2 customers (customer_code) & 8.3 products (product_code)

## 9. ERP inbound payload tối thiểu
Chỉ gửi các trường nghiệp vụ gốc, không bao gồm TTCP, đăng kiểm.

## 10. Master sync / upsert flow mới
- Step 1: Upsert vehicles (Key: `vehicle_plate`, `mooc_number`)
- Step 2: Upsert customers
- Step 3: Upsert products
- Step 4: Upsert vehicle_registrations (Không tạo weigh_ticket ngay ở inbound).

## 11. Rule phát sinh child documents (1-N)
- Bình thường: 1 registration = 1 weigh ticket + 1 delivery ticket (sinh ra khi cân lần 1).
- Nhiều child docs chỉ phát sinh khi có nghiệp vụ đặc thù (như case quá tải hoặc tách phiếu).

## 12. WeightView binding & 13. Grid chính
- UI trái bind theo `vehicle_registration` (TTCP/Đăng kiểm lấy từ `vehicles`).
- UI phải bind theo `current_primary_weigh_ticket`.
- Grid: 1 dòng = 1 `vehicle_registration`. Tô đỏ nếu `has_overweight_case = true`.

## 14. Submenu Cấu hình hệ thống & 15. Child status rules (Technical only)

## 16. Sync strategy & 17. Repository contracts
- `IVehicleRegistrationRepository`
- `IWeighTicketRepository`
- `IDeliveryTicketRepository`

## 18. Use cases bắt buộc

## 19. Migration & Backfill strategy (Chốt)
Thứ tự ưu tiên backfill từ `weigh_tickets` cũ:
1. Có `erp_vehicle_registration_id` -> Gom theo `erp_vehicle_registration_id`.
2. Có quan hệ nội bộ (`split_group_id` / `source_ticket_id`) -> Gom vào cùng 1 Registration.
3. Còn lại -> **1 phiếu cân cũ = 1 vehicle_registration**. Không dùng heuristic mềm (24h).

## 20. Test plan & 21. Definition of Done
Tài liệu Phase 0, 1, 2 cũ phải nhất quán hoàn toàn.
