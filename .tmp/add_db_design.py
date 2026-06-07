import os

srs_path = r'g:\Source-code\pmcan_C#\SRSdocs\StationApp_System_SRS.md'

with open(srs_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Let's locate Appendix B in the document
# Appendix B starts at '## Phụ lục B: Lược đồ Cơ sở dữ liệu Cục bộ (Local SQL Server Schema)'
# And continues with B.1, B.2, B.3, B.4, B.5
# We will append B.6 to B.13 and the ERD before '## Phụ lục C' or '---'

target_index = content.find("## Phụ lục C: Ma trận Nghiệp vụ Máy Trạng thái (State Machines)")
if target_index == -1:
    # fallback to normal horizontal rule search
    target_index = content.find("---", content.find("### B.5 Bảng `delivery_tickets`"))

if target_index != -1:
    db_extension = """
### B.6 Bảng `vehicles` (Danh mục Phương tiện Vận tải)
Lưu trữ thông tin tải trọng cho phép (TTCP), số đăng kiểm và ngày hết hạn hiệu lực đăng kiểm của xe và mooc để thực hiện kiểm soát tải trọng và tính hợp pháp của phương tiện.
- `Id` (uniqueidentifier, PK, NOT NULL)
- `VehiclePlate` (nvarchar(30), NOT NULL) - Biển số xe (Unique kết hợp MoocNumber).
- `MoocNumber` (nvarchar(30), NOT NULL, DEFAULT "") - Biển số mooc xe kéo (Unique kết hợp VehiclePlate).
- `DriverName` (nvarchar(100), NULL) - Tên tài xế mặc định.
- `TransportMethod` (nvarchar(20), NULL) - Phương thức vận chuyển (ROAD/WATERWAY).
- `TtcpWeight` (decimal(18,3), NULL) - Tải trọng cho phép (TTCP) của phương tiện theo đăng kiểm.
- `VehicleRegistrationNo` (nvarchar(50), NULL) - Số đăng kiểm của đầu xe.
- `MoocRegistrationNo` (nvarchar(50), NULL) - Số đăng kiểm của mooc xe kéo.
- `IsActive` (bit, NOT NULL, DEFAULT 1) - Trạng thái hoạt động.
- `CreatedBy` (nvarchar(100), NOT NULL)
- `UpdatedBy` (nvarchar(100), NULL)
- `CreatedAt`, `UpdatedAt` (Audit fields)

### B.7 Bảng `customers` (Danh mục Khách hàng)
Lưu trữ danh sách khách hàng và nhà phân phối phục vụ nhập nhanh đơn hàng thủ công tại trạm cân.
- `Id` (uniqueidentifier, PK, NOT NULL)
- `CustomerCode` (nvarchar(50), NOT NULL, UNIQUE) - Mã khách hàng (dữ liệu gốc từ ERP).
- `CustomerName` (nvarchar(255), NOT NULL) - Tên khách hàng đầy đủ.
- `CreatedBy` (nvarchar(100), NOT NULL)
- `UpdatedBy` (nvarchar(100), NULL)
- `CreatedAt`, `UpdatedAt` (Audit fields)

### B.8 Bảng `products` (Danh mục Sản phẩm)
Lưu trữ danh sách vật tư, sản phẩm, hàng hóa của nhà máy.
- `Id` (uniqueidentifier, PK, NOT NULL)
- `ProductCode` (nvarchar(50), NOT NULL, UNIQUE) - Mã sản phẩm (dữ liệu gốc từ ERP).
- `ProductName` (nvarchar(255), NOT NULL) - Tên sản phẩm đầy đủ.
- `ProductType` (nvarchar(30), NULL) - Loại sản phẩm (Bao, Rời, Hàng nhập).
- `CreatedBy` (nvarchar(100), NOT NULL)
- `UpdatedBy` (nvarchar(100), NULL)
- `CreatedAt`, `UpdatedAt` (Audit fields)

### B.9 Bảng `users` (Tài khoản Người dùng)
Quản trị thông tin nhân sự vận hành trạm cân phục vụ kiểm soát đăng nhập và phân quyền theo vai trò (RBAC).
- `Id` (uniqueidentifier, PK, NOT NULL)
- `Username` (nvarchar(100), NOT NULL, UNIQUE) - Tên đăng nhập hệ thống.
- `DisplayName` (nvarchar(150), NOT NULL) - Tên hiển thị trên giao diện.
- `RoleCode` (nvarchar(30), NOT NULL) - Mã vai trò (ADMIN/OPERATOR).
- `PasswordHash` (nvarchar(255), NULL) - Chuỗi băm mật khẩu an toàn (BCrypt).
- `IsActive` (bit, NOT NULL, DEFAULT 1) - Trạng thái hoạt động.
- `LastLoginAt` (datetime2, NULL) - Thời điểm đăng nhập gần nhất.
- `CreatedBy` (nvarchar(100), NULL)
- `UpdatedBy` (nvarchar(100), NULL)
- `CreatedAt`, `UpdatedAt` (Audit fields)

### B.10 Bảng `app_config` (Cấu hình Hệ thống)
Lưu trữ các cài đặt tham số vận hành tại trạm cân bao gồm thiết bị cổng COM, địa chỉ RTSP camera, dung sai hàng bao, và cấu hình sao lưu.
- `ConfigKey` (nvarchar(100), PK, NOT NULL) - Khóa cấu hình (Unique).
- `ConfigValue` (nvarchar(1000), NULL) - Giá trị cấu hình.
- `CreatedBy` (nvarchar(100), NOT NULL, DEFAULT 'SYSTEM')
- `UpdatedBy` (nvarchar(100), NULL)
- `CreatedAt`, `UpdatedAt` (Audit fields)

### B.11 Bảng `audit_logs` (Nhật ký Kiểm toán)
Ghi nhận toàn bộ các thao tác nhạy cảm tại trạm cân phục vụ hậu kiểm chống gian lận.
- `Id` (uniqueidentifier, PK, NOT NULL)
- `Actor` (nvarchar(100), NOT NULL) - Tài khoản thực hiện hành động.
- `Action` (nvarchar(100), NOT NULL) - Loại hành động (Cân tay, Bỏ qua dung sai, Cấu hình, Xóa phiên...).
- `EntityType` (nvarchar(50), NOT NULL) - Tên bảng/thực thể bị tác động.
- `EntityId` (uniqueidentifier, NOT NULL) - ID thực thể bị tác động.
- `CreatedAt` (datetime2, NOT NULL) - Thời gian thực hiện hành động.

### B.12 Bảng `print_template_profiles` (Cấu hình Phôi in ấn)
Lưu trữ thông tin cấu hình máy in chỉ định và tọa độ lệch lề cho từng loại chứng từ.
- `Id` (uniqueidentifier, PK, NOT NULL)
- `TemplateKind` (nvarchar(30), NOT NULL) - Loại chứng từ (WeighTicket, DeliveryTicket).
- `ProfileKey` (nvarchar(100), NOT NULL) - Mã cấu hình (Unique kết hợp TemplateKind).
- `DisplayName` (nvarchar(150), NOT NULL) - Tên cấu hình phôi in.
- `IsDefault` (bit, NOT NULL) - Cờ thiết lập cấu hình mặc định.
- `OffsetXmm` (decimal(18,3), NULL) - Khoảng lệch lề ngang (mm).
- `OffsetYmm` (decimal(18,3), NULL) - Khoảng lệch lề dọc (mm).
- `TemplateVersion` (int, NOT NULL) - Phiên bản cấu trúc phôi.
- `LayoutJson` (nvarchar(max), NOT NULL) - Cấu trúc layout động thiết kế dạng JSON.
- `CreatedBy` (nvarchar(100), NOT NULL)
- `UpdatedBy` (nvarchar(100), NOT NULL)
- `CreatedAt`, `UpdatedAt` (Audit fields)

### B.13 Bảng `sync_outbox` (Hàng đợi Đồng bộ Ngoại tuyến)
Quản lý hàng đợi đồng bộ dữ liệu giao dịch từ trạm cân lên máy chủ trung tâm theo cơ chế offline-first.
- `Id` (uniqueidentifier, PK, NOT NULL)
- `AggregateId` (uniqueidentifier, NOT NULL) - ID thực thể giao dịch gốc.
- `AggregateType` (nvarchar(50), NOT NULL) - Loại thực thể giao dịch (ví dụ: WeighingSession, DeliveryTicket).
- `PayloadJson` (nvarchar(max), NOT NULL) - Chuỗi JSON chứa toàn bộ dữ liệu giao dịch cần đồng bộ.
- `IdempotencyKey` (uniqueidentifier, NOT NULL) - Khóa chống trùng lặp dữ liệu trên Server.
- `Status` (nvarchar(20), NOT NULL) - Trạng thái đồng bộ (SYNC_QUEUED, SYNC_SUCCESS, SYNC_FAILED).
- `RetryCount` (int, NOT NULL, DEFAULT 0) - Số lần thử lại.
- `LastError` (nvarchar(1000), NULL) - Nội dung lỗi cuối cùng gặp phải.
- `CreatedAt` (datetime2, NOT NULL) - Thời điểm tạo thông điệp đồng bộ.

---

### B.14 Sơ đồ Mối quan hệ Thực thể (ERD - Entity Relationship Diagram)

Sơ đồ dưới đây thể hiện mối quan hệ logic giữa các thực thể chính trong cơ sở dữ liệu trạm cân:

```mermaid
erDiagram
    cut_orders ||--o| weighing_sessions : "WeighingSessionId"
    weighing_sessions ||--o{ weighing_session_lines : "WeighingSessionId"
    cut_orders ||--o{ weighing_session_lines : "CutOrderId"
    weighing_sessions ||--o{ weigh_tickets : "WeighingSessionId"
    weighing_session_lines ||--o| delivery_tickets : "DeliveryTicketId"
    
    vehicles ||--o{ weighing_sessions : "VehiclePlate"
    customers ||--o{ cut_orders : "CustomerCode"
    products ||--o{ cut_orders : "ProductCode"
    
    users ||--o{ audit_logs : "Actor"
```

---

"""
    # Insert before the targeted section
    content_new = content[:target_index] + db_extension + content[target_index:]
    with open(srs_path, 'w', encoding='utf-8') as f:
        f.write(content_new)
    print("Database design successfully appended to SRS!")
else:
    print("Could not find insertion target in SRS.")
