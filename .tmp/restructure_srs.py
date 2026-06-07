import os

srs_path = r'g:\Source-code\pmcan_C#\SRSdocs\StationApp_System_SRS.md'

with open(srs_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Locate the beginning of '## Phụ lục B: Lược đồ Cơ sở dữ liệu Cục bộ' or '# 4. Các Phụ lục (Appendices)'
target_index = content.find("# 4. Các Phụ lục (Appendices)")
if target_index == -1:
    target_index = content.find("## Phụ lục A: Thuật ngữ định nghĩa")

if target_index != -1:
    # Truncate old appendices and database section from the document to replace them
    content_base = content[:target_index]
    
    new_db_and_appendices = """# 4. Thiết kế Cơ sở dữ liệu Cục bộ (Local Database Design)

Để đảm bảo khả năng hoạt động độc lập ngoại tuyến (offline-first) trong thời gian dài (tối thiểu 30 ngày), trạm cân sử dụng cơ sở dữ liệu SQL Server Express cục bộ tại máy trạm. Thiết kế dữ liệu bao gồm các bảng nghiệp vụ giao dịch, danh mục dữ liệu gốc (Master Data) và các bảng quản trị hệ thống.

## 4.1 Sơ đồ Mối quan hệ Thực thể (ERD - Entity Relationship Diagram)

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

## 4.2 Chi tiết Lược đồ các Bảng Cơ sở dữ liệu

Dưới đây là đặc tả chi tiết cấu trúc các bảng dữ liệu cục bộ được trích xuất từ Entity Configurations và mã nguồn thực tế. Các enum được lưu dưới dạng chuỗi (`nvarchar`) theo cấu hình `.HasConversion<string>()` ngoại trừ các trường hợp được chú thích cụ thể.

### 4.2.1 Bảng `cut_orders` (Thông tin Đơn cắt lệnh / Đăng ký từ ERP hoặc Tạo tay)
Bảng này lưu trữ thông tin đăng ký phương tiện, sản phẩm xuất/nhập, tải trọng kế hoạch đồng bộ từ hệ thống ERP hoặc được tạo thủ công tại trạm cân.

| Tên cột (Column Name) | Kiểu dữ liệu (Data Type) | Khóa/Ràng buộc (Constraints) | Mô tả chức năng (Description) |
| :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | PK, NOT NULL | Khóa chính của bản ghi. |
| `ErpCutOrderId` | `nvarchar(50)` | UNIQUE, NULL | Mã cắt lệnh giao hàng từ ERP. Có ràng buộc duy nhất trên bản ghi hoạt động. |
| `ErpRegistrationCode` | `nvarchar(100)` | NULL | Mã đăng ký phương tiện gốc từ ERP. |
| `CutOrderSource` | `nvarchar(20)` | NOT NULL | Nguồn gốc đơn hàng (`ERP` hoặc `MANUAL`). |
| `CutOrderStatus` | `nvarchar(30)` | NOT NULL | Trạng thái đơn (`REGISTERED`, `IN_SESSION`, `LOADING_IN_PROGRESS`, `COMPLETED`, `CANCELLED`). |
| `TransactionType` | `nvarchar(20)` | NOT NULL | Loại giao dịch (`OUTBOUND` - Xuất hàng / `INBOUND` - Nhập hàng). |
| `TransportMethod` | `nvarchar(20)` | NULL | Phương thức vận chuyển (`ROAD` - Đường bộ / `WATERWAY` - Đường thủy). |
| `VehiclePlate` | `nvarchar(30)` | NOT NULL | Biển số xe kéo/đầu xe. |
| `MoocNumber` | `nvarchar(30)` | NULL | Biển số mooc xe kéo. |
| `ReceiverName` | `nvarchar(100)` | NULL | Tên tài xế hoặc người nhận hàng thực tế. |
| `ReceiverIdNo` | `nvarchar(50)` | NULL | Số giấy tờ tùy thân của tài xế. |
| `CustomerCode` | `nvarchar(50)` | NULL | Mã khách hàng/Nhà phân phối (NPP). |
| `CustomerName` | `nvarchar(255)` | NULL | Tên khách hàng/Nhà phân phối (NPP). |
| `ProductCode` | `nvarchar(50)` | NULL | Mã sản phẩm/vật tư. |
| `ProductName` | `nvarchar(255)` | NULL | Tên sản phẩm/vật tư đầy đủ. |
| `ProductType` | `nvarchar(30)` | NULL | Phân loại sản phẩm (`Bao` - Hàng đóng bao / `Rời` - Hàng xá, xi măng rời). |
| `OrderCode` | `nvarchar(100)` | NULL | Mã số đơn đặt hàng. |
| `LotNo` | `nvarchar(100)` | NULL | Số lô hàng sản xuất. |
| `PlannedWeight` | `decimal(18,3)` | NULL | Khối lượng hàng hóa kế hoạch đăng ký (kg). |
| `BagCount` | `int` | NULL | Số lượng bao dự kiến (chỉ đối với đơn hàng bao). |
| `ProcessingStage` | `nvarchar(30)` | NOT NULL | Giai đoạn vận hành (`IN_YARD` - Chờ cân / `WEIGHING` - Đang cân / `OUT_YARD` - Đã ra). |
| `WeighingSessionId` | `uniqueidentifier` | FK, NULL | ID liên kết tới phiên cân đang hoạt động (`weighing_sessions`). |
| `CurrentPrimaryWeighTicketId` | `uniqueidentifier` | FK, NULL | ID liên kết tới phiếu cân tổng hợp chính hiện tại. |
| `CurrentPrimaryDeliveryTicketId` | `uniqueidentifier` | FK, NULL | ID liên kết tới phiếu giao nhận chính hiện tại. |
| `IsExportScale` | `bit` | NOT NULL, DEFAULT 0 | Cờ xác định đơn thuộc luồng Cân xuất khẩu đơn hàng lớn. |
| `ExportFinalizedWeight` | `decimal(18,3)` | NULL | Tổng khối lượng tịnh đã chốt của đơn xuất khẩu lớn. |
| `ExportFinalizedAt` | `datetime2` | NULL | Thời điểm chốt sản lượng đơn xuất khẩu. |
| `ExportFinalizedBy` | `nvarchar(100)` | NULL | Tài khoản thực hiện chốt đơn xuất khẩu. |
| `SyncStatus` | `nvarchar(20)` | NOT NULL | Trạng thái đồng bộ (`SYNC_QUEUED`, `SYNC_SUCCESS`, `SYNC_FAILED`). |
| `IdempotencyKey` | `uniqueidentifier` | NOT NULL | Khóa chống trùng lặp dữ liệu khi gửi lên Server. |
| `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` | Nhiều kiểu | Audit Fields | Trường thông tin kiểm toán ghi nhận người tạo, thời gian tạo và cập nhật. |

### 4.2.2 Bảng `weighing_sessions` (Phiên cân thực tế)
Bảng này lưu trữ thông tin của một phiên cân vật lý đầy đủ của một xe gồm cân lần 1, cân lần 2 và khối lượng tịnh thực tế.

| Tên cột (Column Name) | Kiểu dữ liệu (Data Type) | Khóa/Ràng buộc (Constraints) | Mô tả chức năng (Description) |
| :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | PK, NOT NULL | Khóa chính của phiên cân. |
| `SessionNo` | `nvarchar(50)` | UNIQUE, NOT NULL | Số phiên cân duy nhất sinh tự động theo quy tắc định dạng. |
| `TransactionType` | `nvarchar(30)` | NOT NULL | Loại giao dịch (`OUTBOUND`/`INBOUND`). |
| `VehiclePlate` | `nvarchar(30)` | NOT NULL | Biển số xe thực tế lúc cân. |
| `MoocNumber` | `nvarchar(30)` | NULL | Biển số mooc thực tế lúc cân. |
| `DriverName` | `nvarchar(150)` | NULL | Tên tài xế thực tế khai báo tại trạm cân. |
| `SessionStatus` | `nvarchar(30)` | NOT NULL | Trạng thái phiên cân (`PENDING_WEIGHT1`, `PENDING_WEIGHT2`, `ALLOCATION_PENDING`, `READY_TO_COMPLETE`, `COMPLETED`, `CANCELLED`). |
| `Weight1` | `decimal(18,3)` | NULL | Giá trị khối lượng cân lần 1 (kg). |
| `Weight1Time` | `datetime2` | NULL | Thời điểm lưu cân lần 1. |
| `Weight2` | `decimal(18,3)` | NULL | Giá trị khối lượng cân lần 2 (kg). |
| `Weight2Time` | `datetime2` | NULL | Thời điểm lưu cân lần 2. |
| `NetWeight` | `decimal(18,3)` | NULL | Khối lượng tịnh tính toán: `NetWeight = |Weight1 - Weight2|` (kg). |
| `Ttcp10WeightSnapshot` | `decimal(18,3)` | NULL | Snapshot tải trọng cho phép (TTCP) + 10% của xe tại thời điểm cân. |
| `IsOverweight` | `bit` | NOT NULL, DEFAULT 0 | Cờ xác định phiên cân bị quá tải trọng đăng kiểm. |
| `OverweightAmount` | `decimal(18,3)` | NOT NULL, DEFAULT 0 | Khối lượng vượt quá tải trọng cho phép (kg). |
| `OverweightResolutionStatus` | `nvarchar(30)` | NOT NULL | Trạng thái giải quyết quá tải (`NOT_APPLICABLE`, `PENDING`, `SPLIT_CONFIRMED`, `NO_SPLIT_CONFIRMED`). |
| `OverweightResolvedAt` | `datetime2` | NULL | Thời điểm phê duyệt xử lý quá tải. |
| `OverweightResolvedBy` | `nvarchar(100)` | NULL | Quản trị viên thực hiện phê duyệt quá tải. |
| `HasPrintedMasterWeighTicket` | `bit` | NOT NULL, DEFAULT 0 | Cờ xác định đã thực hiện in phiếu cân tổng hợp chính. |
| `UseActualWeightForBaggedCutOrders` | `bit` | NOT NULL, DEFAULT 0 | Cờ xác định sử dụng khối lượng tịnh thực tế cho đơn hàng bao thay vì tính theo bao. |
| `SyncStatus` | `nvarchar(30)` | NOT NULL | Trạng thái đồng bộ lên Server. |
| `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` | Nhiều kiểu | Audit Fields | Trường thông tin kiểm toán ghi nhận người tạo, thời gian tạo và cập nhật. |

### 4.2.3 Bảng `weighing_session_lines` (Dòng chi tiết của Phiên cân)
Bảng này đóng vai trò trung gian liên kết gộp nhiều đơn cắt lệnh khác nhau vào cùng một phiên cân xe vật lý.

| Tên cột (Column Name) | Kiểu dữ liệu (Data Type) | Khóa/Ràng buộc (Constraints) | Mô tả chức năng (Description) |
| :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | PK, NOT NULL | Khóa chính của dòng chi tiết. |
| `WeighingSessionId` | `uniqueidentifier` | FK, NOT NULL | Khóa ngoại liên kết bảng `weighing_sessions`. |
| `CutOrderId` | `uniqueidentifier` | FK, NOT NULL | Khóa ngoại liên kết bảng `cut_orders`. |
| `SequenceNo` | `int` | NOT NULL | Thứ tự sắp xếp các dòng đơn trong một phiên cân. |
| `CustomerCode` | `nvarchar(50)` | NULL | Mã khách hàng/nhà phân phối. |
| `CustomerName` | `nvarchar(255)` | NULL | Tên khách hàng/nhà phân phối đầy đủ. |
| `ProductCode` | `nvarchar(50)` | NULL | Mã sản phẩm. |
| `ProductName` | `nvarchar(255)` | NULL | Tên sản phẩm. |
| `PlannedWeight` | `decimal(18,3)` | NULL | Khối lượng kế hoạch đăng ký ban đầu (kg). |
| `ActualAllocatedWeight` | `decimal(18,3)` | NULL | Khối lượng thực giao được phân bổ sau cân lần 2 (kg). |
| `ActualAllocatedBagCount` | `int` | NULL | Số lượng bao thực tế được phân bổ cho dòng này. |
| `LineStatus` | `nvarchar(30)` | NOT NULL | Trạng thái dòng đơn trong phiên cân (`PENDING`, `ALLOCATED`, `PRINTED`, `CANCELLED`). |
| `HasPrintedDeliveryTicket` | `bit` | NOT NULL, DEFAULT 0 | Cờ xác định đã in Phiếu giao nhận (PGN) cho dòng đơn này. |
| `DeliveryTicketId` | `uniqueidentifier` | FK, NULL | ID liên kết tới chứng từ Phiếu giao nhận tương ứng được sinh ra. |
| `SyncStatus` | `nvarchar(30)` | NOT NULL | Trạng thái đồng bộ lên Server. |
| `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` | Nhiều kiểu | Audit Fields | Trường thông tin kiểm toán ghi nhận người tạo, thời gian tạo và cập nhật. |

### 4.2.4 Bảng `weigh_tickets` (Chứng từ Phiếu cân tổng hợp cấp Phiên cân)
Bảng này lưu trữ thông tin chứng từ in ấn phiếu cân tổng hợp cấp phiên cân (bao gồm cả các phiếu cân con được tách/sinh ra).

| Tên cột (Column Name) | Kiểu dữ liệu (Data Type) | Khóa/Ràng buộc (Constraints) | Mô tả chức năng (Description) |
| :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | PK, NOT NULL | Khóa chính của phiếu cân. |
| `WeighingSessionId` | `uniqueidentifier` | FK, NULL | ID phiên cân liên quan (nếu được sinh ra từ một phiên cân). |
| `CutOrderId` | `uniqueidentifier` | FK, NOT NULL | ID đơn cắt lệnh liên quan. |
| `TicketNo` | `nvarchar(20)` | UNIQUE, NOT NULL | Số phiếu cân sinh tự động (Unique). |
| `VehiclePlate` | `nvarchar(30)` | NOT NULL | Biển số xe đầu/kéo. |
| `MoocNumber` | `nvarchar(30)` | NULL | Biển số mooc xe kéo. |
| `DriverName` | `nvarchar(100)` | NULL | Tên tài xế. |
| `CustomerCode` | `nvarchar(50)` | NULL | Mã khách hàng. |
| `CustomerName` | `nvarchar(255)` | NULL | Tên khách hàng đầy đủ. |
| `ProductCode` | `nvarchar(50)` | NULL | Mã sản phẩm. |
| `ProductName` | `nvarchar(255)` | NULL | Tên sản phẩm. |
| `PlannedWeight` | `decimal(18,3)` | NULL | Khối lượng kế hoạch đăng ký (kg). |
| `BagCount` | `int` | NULL | Số bao dự kiến. |
| `Weight1` | `decimal(18,3)` | NULL | Khối lượng cân lần 1 (kg). |
| `Weight1Time` | `datetime2` | NULL | Thời điểm cân lần 1. |
| `Weight1User` | `nvarchar(100)` | NULL | Người thực hiện lưu cân lần 1. |
| `Weight1IsStable` | `bit` | NOT NULL | Cờ xác định số cân lần 1 ổn định tại thời điểm lưu. |
| `Weight1Mode` | `nvarchar(20)` | NULL | Chế độ cân lần 1 (`AUTO` - Tự động từ cổng COM / `MANUAL` - Admin nhập tay). |
| `Weight2` | `decimal(18,3)` | NULL | Khối lượng cân lần 2 (kg). |
| `Weight2Time` | `datetime2` | NULL | Thời điểm cân lần 2. |
| `Weight2User` | `nvarchar(100)` | NULL | Người thực hiện lưu cân lần 2. |
| `Weight2IsStable` | `bit` | NOT NULL | Cờ xác định số cân lần 2 ổn định. |
| `Weight2Mode` | `nvarchar(20)` | NULL | Chế độ cân lần 2 (`AUTO` - Tự động / `MANUAL` - Admin nhập tay). |
| `NetWeight` | `decimal(18,3)` | NULL | Khối lượng tịnh thực xuất/nhập ghi nhận trên phiếu (kg). |
| `Ttcp10WeightSnapshot` | `decimal(18,3)` | NULL | Snapshot tải trọng cho phép (TTCP + 10%) tại thời điểm cân. |
| `VehicleRegistrationNoSnapshot` | `nvarchar(50)` | NULL | Số đăng kiểm xe ghi nhận trên phiếu. |
| `MoocRegistrationNoSnapshot` | `nvarchar(50)` | NULL | Số đăng kiểm mooc ghi nhận trên phiếu. |
| `RecordRole` | `nvarchar(20)` | NOT NULL | Vai trò bản ghi phiếu cân (`MASTER_SESSION` - Phiếu tổng phiên / `CUT_ORDER_DERIVED` - Phiếu con / `SPLIT_DERIVED` - Phiếu tách tải). |
| `IsPrimaryDisplay` | `bit` | NOT NULL | Xác định phiếu hiển thị chính trên danh sách. |
| `IsPrinted` | `bit` | NOT NULL, DEFAULT 0 | Trạng thái đã in ấn ra giấy. |
| `LastPrintedAt` | `datetime2` | NULL | Thời điểm in ấn gần nhất. |
| `SplitGroupId` | `uniqueidentifier` | NULL | ID nhóm tách tải (dùng chung cho các phiếu được tách ra từ một lượt cân quá tải). |
| `SplitSequence` | `tinyint` | NULL | Thứ tự phiếu trong nhóm tách tải. |
| `SyncStatus` | `nvarchar(20)` | NOT NULL | Trạng thái đồng bộ dữ liệu. |
| `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` | Nhiều kiểu | Audit Fields | Trường thông tin kiểm toán ghi nhận người tạo, thời gian tạo và cập nhật. |

### 4.2.5 Bảng `delivery_tickets` (Phiếu giao nhận cấp Dòng chi tiết)
Bảng này lưu trữ thông tin chứng từ in ấn Phiếu Giao Nhận (PGN) cấp dòng chi tiết trong phiên cân để tài xế cầm đi giao/nhận hàng.

| Tên cột (Column Name) | Kiểu dữ liệu (Data Type) | Khóa/Ràng buộc (Constraints) | Mô tả chức năng (Description) |
| :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | PK, NOT NULL | Khóa chính của phiếu giao nhận. |
| `CutOrderId` | `uniqueidentifier` | FK, NOT NULL | Khóa ngoại liên kết bảng `cut_orders`. |
| `WeighingSessionId` | `uniqueidentifier` | FK, NULL | Khóa ngoại liên kết bảng `weighing_sessions`. |
| `WeighingSessionLineId` | `uniqueidentifier` | FK, NULL | Khóa ngoại liên kết bảng `weighing_session_lines`. |
| `DeliveryNo` | `nvarchar(30)` | UNIQUE, NOT NULL | Số phiếu giao nhận sinh duy nhất tự động. |
| `ErpCutOrderId` | `nvarchar(50)` | NOT NULL | Mã đơn cắt lệnh từ ERP. |
| `CustomerCode` | `nvarchar(50)` | NULL | Mã khách hàng nhận. |
| `ProductCode` | `nvarchar(50)` | NULL | Mã sản phẩm/vật tư. |
| `AllocatedWeight` | `decimal(18,3)` | NULL | Khối lượng tịnh được phân bổ thực tế cho đơn hàng (kg). |
| `AllocatedBagCount` | `int` | NULL | Số lượng bao thực xuất phân bổ thực tế. |
| `RecordRole` | `nvarchar(20)` | NOT NULL | Vai trò chứng từ (`NORMAL` - Phiếu thường / `MASTER` - Phiếu gộp nhiều đơn / `SPLIT_DERIVED` - Phiếu tách quá tải). |
| `IsPrinted` | `bit` | NOT NULL, DEFAULT 0 | Trạng thái đã in ấn phiếu. |
| `LastPrintedAt` | `datetime2` | NULL | Thời điểm in ấn gần nhất. |
| `SplitGroupId` | `uniqueidentifier` | NULL | ID liên kết nhóm tách quá tải. |
| `SplitSequence` | `tinyint` | NULL | Thứ tự trong nhóm tách quá tải. |
| `SyncStatus` | `int` | NOT NULL | Trạng thái đồng bộ (lưu dưới dạng giá trị số `int`). |
| `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy` | Nhiều kiểu | Audit Fields | Trường thông tin kiểm toán ghi nhận người tạo, thời gian tạo và cập nhật. |

### 4.2.6 Bảng `vehicles` (Danh mục Phương tiện Vận tải)
Bảng danh mục dữ liệu gốc (Master Data) lưu trữ thông tin các xe, mooc vận tải đăng ký vào/ra nhà máy phục vụ kiểm soát đăng kiểm và tải trọng cho phép.

| Tên cột (Column Name) | Kiểu dữ liệu (Data Type) | Khóa/Ràng buộc (Constraints) | Mô tả chức năng (Description) |
| :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | PK, NOT NULL | Khóa chính của phương tiện. |
| `VehiclePlate` | `nvarchar(30)` | NOT NULL | Biển số đầu xe/xe đơn (Unique kết hợp MoocNumber). |
| `MoocNumber` | `nvarchar(30)` | NOT NULL, DEFAULT "" | Biển số mooc đi kèm (Unique kết hợp VehiclePlate). |
| `DriverName` | `nvarchar(100)` | NULL | Tên tài xế mặc định của phương tiện. |
| `TransportMethod` | `nvarchar(20)` | NULL | Phương thức vận chuyển (`ROAD`/`WATERWAY`). |
| `TtcpWeight` | `decimal(18,3)` | NULL | Tải trọng cho phép (TTCP) thiết kế theo đăng kiểm của xe (kg). |
| `VehicleRegistrationNo` | `nvarchar(50)` | NULL | Số chứng nhận kiểm định an toàn kỹ thuật (số đăng kiểm) đầu xe. |
| `MoocRegistrationNo` | `nvarchar(50)` | NULL | Số đăng kiểm mooc kéo. |
| `IsActive` | `bit` | NOT NULL, DEFAULT 1 | Cờ trạng thái hoạt động của phương tiện. |
| `CreatedBy` | `nvarchar(100)` | NOT NULL | Người tạo xe. |
| `UpdatedBy` | `nvarchar(100)` | NULL | Người cập nhật thông tin xe gần nhất. |
| `CreatedAt`, `UpdatedAt` | `datetime2` | NOT NULL | Thời gian tạo và cập nhật dữ liệu. |

### 4.2.7 Bảng `customers` (Danh mục Khách hàng)
Bảng danh mục dữ liệu gốc lưu trữ thông tin khách hàng và nhà phân phối phục vụ hiển thị và chọn nhanh khi cân.

| Tên cột (Column Name) | Kiểu dữ liệu (Data Type) | Khóa/Ràng buộc (Constraints) | Mô tả chức năng (Description) |
| :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | PK, NOT NULL | Khóa chính của khách hàng. |
| `CustomerCode` | `nvarchar(50)` | UNIQUE, NOT NULL | Mã khách hàng/NPP duy nhất. |
| `CustomerName` | `nvarchar(255)` | NOT NULL | Tên khách hàng/NPP đầy đủ. |
| `CreatedBy` | `nvarchar(100)` | NOT NULL | Người tạo. |
| `UpdatedBy` | `nvarchar(100)` | NULL | Người cập nhật gần nhất. |
| `CreatedAt`, `UpdatedAt` | `datetime2` | NOT NULL | Thời gian tạo và cập nhật dữ liệu. |

### 4.2.8 Bảng `products` (Danh mục Sản phẩm)
Bảng danh mục dữ liệu gốc lưu trữ thông tin các mặt hàng, sản phẩm của nhà máy.

| Tên cột (Column Name) | Kiểu dữ liệu (Data Type) | Khóa/Ràng buộc (Constraints) | Mô tả chức năng (Description) |
| :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | PK, NOT NULL | Khóa chính của sản phẩm. |
| `ProductCode` | `nvarchar(50)` | UNIQUE, NOT NULL | Mã sản phẩm duy nhất. |
| `ProductName` | `nvarchar(255)` | NOT NULL | Tên sản phẩm đầy đủ. |
| `ProductType` | `nvarchar(30)` | NULL | Loại sản phẩm (`Bao` / `Rời` / `Hàng nhập`). |
| `CreatedBy` | `nvarchar(100)` | NOT NULL | Người tạo. |
| `UpdatedBy` | `nvarchar(100)` | NULL | Người cập nhật gần nhất. |
| `CreatedAt`, `UpdatedAt` | `datetime2` | NOT NULL | Thời gian tạo và cập nhật dữ liệu. |

### 4.2.9 Bảng `users` (Tài khoản Người dùng)
Bảng lưu trữ thông tin tài khoản người dùng tại trạm cân để xác thực đăng nhập và phân quyền.

| Tên cột (Column Name) | Kiểu dữ liệu (Data Type) | Khóa/Ràng buộc (Constraints) | Mô tả chức năng (Description) |
| :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | PK, NOT NULL | Khóa chính người dùng. |
| `Username` | `nvarchar(100)` | UNIQUE, NOT NULL | Tên đăng nhập hệ thống. |
| `DisplayName` | `nvarchar(150)` | NOT NULL | Tên hiển thị của cán bộ trạm cân. |
| `RoleCode` | `nvarchar(30)` | NOT NULL | Vai trò người dùng (`ADMIN` - Quản trị viên / `OPERATOR` - Nhân viên cân). |
| `PasswordHash` | `nvarchar(255)` | NULL | Chuỗi băm mật khẩu bảo mật (băm bằng BCrypt). |
| `IsActive` | `bit` | NOT NULL, DEFAULT 1 | Trạng thái hoạt động của tài khoản. |
| `LastLoginAt` | `datetime2` | NULL | Thời gian đăng nhập cuối cùng vào hệ thống. |
| `CreatedBy` | `nvarchar(100)` | NULL | Người tạo tài khoản. |
| `UpdatedBy` | `nvarchar(100)` | NULL | Người cập nhật tài khoản gần nhất. |
| `CreatedAt`, `UpdatedAt` | `datetime2` | NOT NULL | Thời gian tạo và cập nhật tài khoản. |

### 4.2.10 Bảng `app_config` (Cấu hình Hệ thống)
Bảng lưu trữ các tham số cấu hình của ứng dụng máy trạm (cổng COM, RTSP Camera, đường dẫn backup...).

| Tên cột (Column Name) | Kiểu dữ liệu (Data Type) | Khóa/Ràng buộc (Constraints) | Mô tả chức năng (Description) |
| :--- | :--- | :--- | :--- |
| `ConfigKey` | `nvarchar(100)` | PK, NOT NULL | Từ khóa cấu hình duy nhất. |
| `ConfigValue` | `nvarchar(1000)` | NULL | Giá trị cấu hình tương ứng. |
| `CreatedBy` | `nvarchar(100)` | NOT NULL, DEFAULT 'SYSTEM' | Người tạo cấu hình. |
| `UpdatedBy` | `nvarchar(100)` | NULL | Người cập nhật cấu hình. |
| `CreatedAt`, `UpdatedAt` | `datetime2` | NOT NULL | Thời gian tạo và cập nhật cấu hình. |

### 4.2.11 Bảng `audit_logs` (Nhật ký Kiểm toán)
Bảng ghi nhận nhật ký vận hành đối với tất cả các thao tác nhạy cảm tại trạm cân để phục vụ hậu kiểm chống gian lận.

| Tên cột (Column Name) | Kiểu dữ liệu (Data Type) | Khóa/Ràng buộc (Constraints) | Mô tả chức năng (Description) |
| :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | PK, NOT NULL | Khóa chính của bản ghi nhật ký. |
| `Actor` | `nvarchar(100)` | NOT NULL | Tài khoản thực hiện hành động (Username). |
| `Action` | `nvarchar(100)` | NOT NULL | Loại hành động thực hiện (ví dụ: `ManualWeigh` - Cân tay, `BypassTolerance` - Bỏ qua dung sai, `UpdateConfig` - Sửa cấu hình, `DeleteSession` - Hủy phiên). |
| `EntityType` | `nvarchar(50)` | NOT NULL | Tên thực thể/bảng bị tác động. |
| `EntityId` | `uniqueidentifier` | NOT NULL | ID thực thể/bản ghi bị tác động. |
| `CreatedAt` | `datetime2` | NOT NULL | Thời gian ghi nhận nhật ký kiểm toán. |

### 4.2.12 Bảng `print_template_profiles` (Cấu hình Phôi in ấn)
Bảng lưu trữ các thiết lập mẫu phôi in ấn, máy in mặc định và căn chỉnh lề (offset) của từng chứng từ.

| Tên cột (Column Name) | Kiểu dữ liệu (Data Type) | Khóa/Ràng buộc (Constraints) | Mô tả chức năng (Description) |
| :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | PK, NOT NULL | Khóa chính của cấu hình phôi in. |
| `TemplateKind` | `nvarchar(30)` | NOT NULL | Loại mẫu in (`WeighTicket`/`DeliveryTicket`). |
| `ProfileKey` | `nvarchar(100)` | NOT NULL | Khóa định danh phôi (Unique kết hợp TemplateKind). |
| `DisplayName` | `nvarchar(150)` | NOT NULL | Tên phôi in hiển thị trên giao diện. |
| `IsDefault` | `bit` | NOT NULL | Xác định phôi in mặc định của hệ thống. |
| `OffsetXmm` | `decimal(18,3)` | NULL | Tọa độ dịch chuyển lề ngang khi in (mm). |
| `OffsetYmm` | `decimal(18,3)` | NULL | Tọa độ dịch chuyển lề dọc khi in (mm). |
| `TemplateVersion` | `int` | NOT NULL | Phiên bản cấu trúc phôi in. |
| `LayoutJson` | `nvarchar(max)` | NOT NULL | Chuỗi JSON mô tả chi tiết layout in ấn động. |
| `CreatedBy` | `nvarchar(100)` | NOT NULL | Người tạo. |
| `UpdatedBy` | `nvarchar(100)` | NOT NULL | Người cập nhật. |
| `CreatedAt`, `UpdatedAt` | `datetime2` | NOT NULL | Thời gian tạo và cập nhật. |

### 4.2.13 Bảng `sync_outbox` (Hàng đợi Đồng bộ Ngoại tuyến)
Bảng lưu trữ hàng đợi Outbox chứa các sự thay đổi dữ liệu nghiệp vụ cục bộ cần đồng bộ tuần tự lên Server trung tâm.

| Tên cột (Column Name) | Kiểu dữ liệu (Data Type) | Khóa/Ràng buộc (Constraints) | Mô tả chức năng (Description) |
| :--- | :--- | :--- | :--- |
| `Id` | `uniqueidentifier` | PK, NOT NULL | Khóa chính của thông điệp đồng bộ. |
| `AggregateId` | `uniqueidentifier` | NOT NULL | ID thực thể giao dịch bị thay đổi cần đồng bộ. |
| `AggregateType` | `nvarchar(50)` | NOT NULL | Loại thực thể bị thay đổi (`WeighingSession`, `DeliveryTicket`...). |
| `PayloadJson` | `nvarchar(max)` | NOT NULL | Chuỗi JSON chứa toàn bộ dữ liệu cần đẩy lên Server. |
| `IdempotencyKey` | `uniqueidentifier` | NOT NULL | Khóa Idempotency tránh trùng lặp dữ liệu trên Server. |
| `Status` | `nvarchar(20)` | NOT NULL | Trạng thái đồng bộ (`SYNC_QUEUED`, `SYNC_SUCCESS`, `SYNC_FAILED`). |
| `RetryCount` | `int` | NOT NULL, DEFAULT 0 | Số lần thử lại gửi dữ liệu khi lỗi kết nối mạng. |
| `LastError` | `nvarchar(1000)` | NULL | Chi tiết thông tin lỗi mạng hoặc API gặp phải gần nhất. |
| `CreatedAt` | `datetime2` | NOT NULL | Thời gian tạo thông điệp đồng bộ. |

---

# 5. Các Phụ lục (Appendices)

## Phụ lục A: Thuật ngữ và Định nghĩa (Glossary)
*(Xem chi tiết tại Mục 1.3 của tài liệu này)*

---

## Phụ lục B: Ma trận Máy Trạng thái (State Machines)

### B.1 Vòng đời của một Phiên cân (Weighing Session Lifecycle)

```mermaid
stateDiagram-v2
    [*] --> PENDING_WEIGHT1 : Tạo phiên cân từ Danh sách xe vào
    PENDING_WEIGHT1 --> PENDING_WEIGHT2 : Lưu cân lần 1 thành công (Weight 1)
    PENDING_WEIGHT2 --> ALLOCATION_PENDING : Lưu cân lần 2 thành công (Weight 2)
    ALLOCATION_PENDING --> READY_TO_COMPLETE : Xác nhận phân bổ thực tế thành công
    READY_TO_COMPLETE --> COMPLETED : In đủ Phiếu cân tổng hợp chính & tất cả phiếu giao nhận chi tiết
    
    PENDING_WEIGHT1 --> CANCELLED : Nhân viên cân (Operator) hủy phiên cân
    PENDING_WEIGHT2 --> CANCELLED : Nhân viên cân (Operator) hủy phiên cân
    ALLOCATION_PENDING --> CANCELLED : Nhân viên cân (Operator) hủy phiên cân
    
    COMPLETED --> [*]
    CANCELLED --> [*] : Trả các đơn cắt lệnh (Cut Orders) về lại trạng thái chờ (`IN_YARD`)
```

### B.2 Trạng thái của Đơn cắt lệnh gốc (Cut Order Lifecycle)

```mermaid
stateDiagram-v2
    [*] --> REGISTERED : ERP đồng bộ xuống hoặc Tạo thủ công (IN_YARD)
    REGISTERED --> IN_SESSION : Được gắn vào một phiên cân đang hoạt động (WEIGHING)
    
    state IN_SESSION {
        [*] --> OutboundNormal : Xe chạy 1 chuyến thông thường
        [*] --> OutboundExport : Chuyển luồng cân xuất khẩu đơn lớn
    }
    
    OutboundNormal --> COMPLETED : Phiên cân chứa đơn hoàn tất cân lần 2 + in ấn (OUT_YARD)
    OutboundExport --> COMPLETED : Bấm chốt sản lượng (Finalize) đơn cắt lệnh lớn
    
    REGISTERED --> CANCELLED : Hủy đơn hàng hoặc bị ERP soft delete
    IN_SESSION --> REGISTERED : Phiên cân bị hủy (trả về hàng đợi)
    
    COMPLETED --> [*]
    CANCELLED --> [*]
```

---

## Phụ lục C: Ma trận Truy xuất Yêu cầu (Requirements Traceability Matrix)

Dưới đây là bảng đối chiếu giữa các mục tiêu nghiệp vụ chính và các mã yêu cầu đặc tả tương ứng trong hệ thống.

| Mục tiêu Nghiệp vụ | Mã Yêu cầu Chức năng | Yêu cầu Phi chức năng liên quan | Trạng thái kiểm chứng |
|:---|:---|:---|:---|
| Kiểm soát an toàn đăng nhập | FR-AUTH-001, FR-AUTH-002 | NFR-SEC-001 | Đã kiểm chứng |
| Quản lý xe chờ cân cục bộ | FR-WEIGH-001 | NFR-PERF-001 | Đã kiểm chứng |
| Đo lường cân tự động | FR-WEIGH-002, FR-WEIGH-003 | NFR-REL-002, NFR-USA-002 | Đã kiểm chứng |
| Phân bổ khối lượng nhiều đơn | FR-ALLOC-001 | NFR-SEC-002 | Đã kiểm chứng |
| Đảm bảo in đủ chứng từ | FR-PRINT-001, FR-OUT-001 | NFR-USA-001 | Đã kiểm chứng |
| Vận hành ngoại tuyến khi mất mạng | FR-SYNC-001 | NFR-REL-001 | Đã kiểm chứng |
| Xử lý đơn cắt lệnh lớn xuất khẩu | FR-EXPORT-001 | NFR-PERF-003 | Đã kiểm chứng |
| Xử lý cấp lại đơn cắt lệnh của ERP | FR-REISSUE-001 | NFR-REL-002 | Đã kiểm chứng |
| Tự động sao lưu dữ liệu cục bộ | FR-BACKUP-001 | NFR-REL-001 | Đã kiểm chứng |

---

## Phụ lục D: Phê duyệt từ các bên (Stakeholder Sign-Off)

Tài liệu Đặc tả Yêu cầu Phần mềm này đại diện cho sự thống nhất về mặt nghiệp vụ và kỹ thuật giữa các bên liên quan của dự án WeightStation.

| Đại diện | Vai trò | Chữ ký | Ngày |
|:---|:---|:---|:---|
| Đại diện Ban Dự án | Project Owner | ___________________ | ___/___/2026 |
| Đại diện Vận hành Trạm | Station Manager / Admin Representative | ___________________ | ___/___/2026 |
| Đại diện Tích hợp ERP | ERP System Integrator | ___________________ | ___/___/2026 |
| Đại diện Kỹ thuật | Tech Lead / Architect | ___________________ | ___/___/2026 |
"""
    
    content_new = content_base + new_db_and_appendices
    
    with open(srs_path, 'w', encoding='utf-8') as f:
        f.write(content_new)
    print("SRS successfully restructured!")
else:
    print("Could not find the Horizontal Rule or Appendices start in SRS.")
