# PLAN - ERP DIRECT INSERT + POST-INSERT PROCESSOR (FINAL LOCKED)

## 1. Mục tiêu và bối cảnh
ERP sẽ không push ĐKPT qua API. Thay vào đó, ERP trực tiếp `INSERT` dữ liệu vào bảng `vehicle_registrations` tại máy trạm. 
Hệ thống sẽ chạy Service xử lý hậu kỳ ngầm để chuẩn hóa dữ liệu và đồng bộ Master Data cục bộ.

---

## 2. Các thay đổi Kiến trúc & Schema
### 2.1 Schema updates (Bảng `vehicle_registrations`)
Thêm các trường kỹ thuật kiểm soát trạng thái xử lý:
- `is_inbound_processed` (bit, default 0, NOT NULL)
- `inbound_processed_at` (datetime2, NULL)
- `inbound_error_code` (nvarchar(50), NULL)
- `inbound_error_message` (nvarchar(500), NULL)

### 2.2 Background Service mới
- Tên lớp: `VehicleRegistrationInboundProcessor`
- Chu kỳ hoạt động: Quét dữ liệu mỗi 5 giây (`registration_inbound_poll_seconds` trong `app_config`).

---

## 3. Quy tắc xử lý chi tiết (Processor Logic)

### 3.1 Fetch Rule
- Chỉ quét record thỏa mãn: `is_inbound_processed = 0` AND `registration_source = 'ERP'`.

### 3.2 Quy trình Validate & Phân luồng
Yêu cầu tối thiểu các trường: `vehicle_plate`, `customer_code`, `product_code`.

#### Luồng A: Thiếu dữ liệu tối thiểu (Validation Failed)
- **Hành động**: 
  - KHÔNG set `is_inbound_processed = 1`.
  - KHÔNG tạo child docs.
  - KHÔNG upsert master data.
- **Logging**:
  - Ghi bản ghi vào `audit_logs` (Action: `ERP_INBOUND_VALIDATION_FAILED`).
  - Ghi ILogger cảnh báo mức Error/Warning.
  - Lưu mã lỗi vào `inbound_error_code` và `inbound_error_message`.

#### Luồng B: Bản ghi hợp lệ (Happy Path)
- Thực hiện chuẩn hóa dữ liệu (Trim, Uppercase Plate).
- Upsert Master data: `Vehicles`, `Customers`, `Products`.
- Cập nhật thông số: 
  - `registration_status = REGISTERED` (nếu chưa có trạng thái).
  - `is_inbound_processed = 1`.
  - `inbound_processed_at = now()`.

#### Luồng C: Bản ghi bị hủy ngay từ đầu (`is_cancelled = 1`)
- Nếu đủ dữ liệu validate: 
  - VẪN thực hiện Upsert master data (`Vehicles`, `Customers`, `Products`).
  - Set `registration_status = CANCELLED`.
  - Set `is_inbound_processed = 1` và `inbound_processed_at = now()`.
- Nếu thiếu dữ liệu validate: Xử lý theo **Luồng A**.

---

## 4. Test Cases & Verification Plan
- **TC1**: ERP Insert dữ liệu chuẩn -> Mark processed thành công -> Masters được nạp.
- **TC2**: ERP Insert thiếu trường -> Không mark processed -> Lưu log & cờ báo lỗi.
- **TC3**: ERP Insert record hủy -> Vẫn map masters (nếu đủ điều kiện) -> RegistrationStatus = `CANCELLED`.
