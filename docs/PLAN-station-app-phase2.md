# PHASE 2 — TECHNICAL SPECIFICATION & IMPLEMENTATION PLAN (FINAL LOCKED)

Bản kế hoạch này là **Source of Truth** tuyệt đối cho Phase 2. Mọi hoạt động triển khai phải tuân thủ nghiêm ngặt các định nghĩa dưới đây.

## 1. Mục tiêu của Phase 2
- Đưa hệ thống lên mức **usable** cho vận hành thực tế và sẵn sàng pilot.
- Kết nối cân Yaohua thật (COM/serial) và Central API thật (Sync).
- Hoàn thiện WeightView, Master Data, Delivery Tickets, Dashboard và Diagnostics.

## 2. Ràng buộc kế thừa (Locked)
- **Kiến trúc**: SQL Server Express local. Không thêm entity `ERPOrder` local.
- **Ticket No**: Format `QNyyMM0001`.
- **Đơn vị**: DB lưu `kg`, UI hiển thị `tấn`.
- **Status**: Giữ nguyên model status từ Phase 1.
- **Công thức**: OUTBOUND (W2 - W1), INBOUND (W1 - W2).
- **Sync Semantics**: Không gộp `sync_status` của ticket với `status` của outbox.

## 3. Quyết định kỹ thuật đã chốt (Phase 2 Update)
1. **Cơ chế Auth**: Sử dụng **Static API Key** (Header `X-Api-Key`) cho mọi kết nối với Central API.
2. **Ưu tiên Local**: Mọi chức năng Autocomplete/Suggest sẽ **ưu tiên tìm kiếm tại Local DB** (Offline-first) để đảm bảo tốc độ và vận hành khi mất mạng.
3. **Cảnh báo tự động**: Hệ thống **tự động tính toán** trọng lượng so với TTCP. Nếu quá tải (>110%), hệ thống sẽ **tự động hiển thị Modal cảnh báo** ngay lập tức.
4. **Kết nối Yaohua**: Thiết lập `FrameEndChar = \r` (CR - ASCII 13), phù hợp với chế độ Broadcast của các đầu cân Yaohua qua cổng COM.

## 4. Danh mục Schema Chi tiết

### 4.1 vehicles (Master Data)
- `id`: uniqueidentifier PK
- `vehicle_plate`: nvarchar(30) NOT NULL
- `mooc_number`: nvarchar(30) NOT NULL DEFAULT ''
- `driver_name`: nvarchar(100) NULL
- `transport_method`: nvarchar(20) NULL
- `ttcp_weight`: decimal(18,3) NULL
- `vehicle_registration_no`: nvarchar(50) NULL
- `vehicle_registration_expiry_date`: date NULL
- `mooc_registration_no`: nvarchar(50) NULL
- `mooc_registration_expiry_date`: date NULL
- `is_active`: bit (1), `created_at`, `created_by`, `updated_at`, `updated_by`.
- **Constraints**: UNIQUE(vehicle_plate, mooc_number), INDEX(vehicle_plate), INDEX(is_active).

### 4.2 customers & products
- Schema chuẩn: `id`, `code` (Unique), `name`, `is_active`, audit fields.

### 4.3 weigh_tickets Delta (Bổ sung)
- **Snapshots**: `ttcp_weight_snapshot`, `vehicle_registration_no_snapshot`, `vehicle_registration_expiry_snapshot`, `mooc_registration_no_snapshot`, `mooc_registration_expiry_snapshot`.
- **Flags/Logic**: `is_over_weight`, `is_primary_display` (1), `is_printed` (0), `split_group_id`, `split_sequence`, `source_ticket_id`, `delivery_ticket_id`, `record_role` (WORKING/SOURCE).

### 4.4 delivery_tickets
- `id`: uniqueidentifier PK
- `delivery_no`: nvarchar(30) UNIQUE NOT NULL
- `erp_vehicle_registration_id`: nvarchar(50) NOT NULL
- Schema bao gồm các cờ `is_over_weight`, `is_printed`, `split_group_id`, `record_role`.

## 5. Quy tắc Nghiệp vụ & Đồng bộ

### 5.1 Master Data Sync (Inbound Flow)
1. **Step 1 — Upsert Vehicle**: Lookup theo (plate, mooc). Cập nhật mô tả nếu payload không rỗng.
2. **Step 2 — Upsert Customer**: Lookup theo `customer_code`.
3. **Step 3 — Upsert Product**: Lookup theo `product_code`.
4. **Step 4 — Snapshot**: Ghi toàn bộ thông tin master vào ticket tại thời điểm phát sinh.

### 5.2 Autocomplete & Suggest
- **Số PTVC**: Suggest theo `vehicles.vehicle_plate` (Tìm kiếm Local).
- **Mooc**: Sau khi chọn biển, dropdown Mooc hiện các option tương ứng.
- **Auto-fill**: Sau khi chọn Mooc, tự fill tài xế, HTVC, TTCP, và toàn bộ thông tin đăng kiểm.
- **Warning**: Nếu hạn đăng kiểm < Today -> Hiển thị đỏ và Warning Text "Xe/Mooc đã hết hạn đăng kiểm".

### 5.3 WeightView Mapping Chính thức
- **HTVC**: dùng `transport_method`.
- **Mã ĐKPT**: `erp_vehicle_registration_id`.
- **SL đặt**: `planned_weight` (hiển thị tấn).
- **Panel phải**: Live weight LED, `weight_1`, `weight_2`, `net_weight`.
- **Grid dưới**: Chỉ hiện `record_role = WORKING` & `is_primary_display = 1`. Row quá tải hiển thị đỏ. Thêm nút "Xem phiếu liên quan".

## 6. Hợp đồng Repository (Contracts)

### 6.1 IVehicleRepository
- `GetByPlateAndMoocAsync(plate, mooc)`
- `GetByPlateAsync(plate)`
- `SearchAsync(keyword)`

### 6.2 ICustomer/IProductRepository
- `GetByCodeAsync(code)`
- `SearchAsync(keyword)`

### 6.3 IDeliveryTicketRepository
- `GetByErpVehicleRegistrationIdAsync(id)`
- `GetBySplitGroupIdAsync(groupId)`

## 7. Kế hoạch triển khai (Sprints)

### Sprint 1 — Schema & Master Data Foundation ✅
- [x] Implement Entities: `Vehicle`, `Customer`, `Product`, `DeliveryTicket`.
- [x] Update `WeighTicket` với các trường snapshot và flags.
- [x] Migrations + Repositories + Tests.

### Sprint 2 — UI & Suggest Flow ✅
- [x] Hoàn thiện `WeightView` đúng mapping mục 11.
- [x] Suggest/Autocomplete cho Xe/Mooc (Local Search).
- [x] Hiển thị đăng kiểm đỏ/warning & Grid rule (phiếu chính, row đỏ quá tải).
- [x] Tự động trigger `OverweightWarningModal` khi quá tải.
- [x] Related tickets dialog.

### Sprint 3 — Device & Sync Real Integration ✅
- [x] `SerialScaleDevice`: Connect/Disconnect, Yaohua Parser, Reconnect, Diagnostics. Cấu hình `FrameEndChar = \r`.
- [x] Sync Inbound (Master upsert) & Outbound (Push ticket với X-Api-Key).
- [x] `DiagnosticsView` usable.

### Sprint 4 — System Config & Hardening ✅
- [x] Submenu "Cấu hình hệ thống" (6 màn hình con).
- [x] Master data management screens (Xe, Khách, Sản phẩm).
- [x] DashboardView, Startup checks, Bug fixes.

## 8. Definition of Done
- [x] Có đủ 3 bảng master và bảng delivery_tickets.
- [x] Snapshot đúng TTCP và đăng kiểm vào ticket.
- [x] WeightView đúng mapping, Autocomplete chạy đúng logic Mooc.
- [x] Cảnh báo quá tải (Modal tự động/Red row) và Đăng kiểm hết hạn hoạt động.
- [x] App kết nối cân Yaohua và Sync thật (Auth API Key) thành công.
- [x] 6 màn hình cấu hình hệ thống đầy đủ.
