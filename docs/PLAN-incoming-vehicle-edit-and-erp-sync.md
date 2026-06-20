# Kế hoạch mở sửa Số PTVC/Mooc ở Danh sách xe vào và đồng bộ ngược ERP

## 1. Mục tiêu

Cho phép ở màn `Danh sách xe vào`:

- khi chọn một `cắt lệnh xuất hàng` trong grid, người dùng được sửa:
  - `Số PTVC`
  - `Mooc`
- sau khi bấm `Lưu thay đổi`:
  - dữ liệu phải được cập nhật vào DB cân local (`cut_orders`)
  - đồng thời ứng dụng phải kết nối sang DB ERP Oracle để cập nhật đúng bản ghi cắt lệnh tương ứng

## 2. Hiện trạng đã rà soát trong code

### 2.1. UI hiện đang khóa đúng 2 trường cần mở

- File: `src/StationApp.UI/Views/IncomingVehicleListView.xaml`
- `Số PTVC` và `Mooc` đang bind `IsReadOnly="{Binding IsOutboundDetailLockMode}"`
- `IsOutboundDetailLockMode` hiện được tính trong `IncomingVehicleListViewModel` là:
  - `!IsCreateMode && FormTransactionType == TransactionType.OUTBOUND`

Kết luận:

- với cắt lệnh `xuất hàng`, UI hiện đang khóa sửa `Số PTVC` và `Mooc`
- muốn làm theo yêu cầu thì cần thay đổi logic khóa này

### 2.2. Luồng lưu hiện tại mới chỉ cập nhật DB cân local

- File: `src/StationApp.UI/ViewModels/IncomingVehicleListViewModel.cs`
- Command lưu đang gọi `UpdateIncomingRegistrationUseCase`
- File: `src/StationApp.Application/UseCases/UpdateIncomingRegistrationUseCase.cs`

Hiện tại use case này:

- cập nhật entity `CutOrder`
- lưu local qua `ICutOrderRepository`
- đồng bộ master data nội bộ qua `EnsureInboundMasterDataUseCase`
- chưa có bước cập nhật ngược sang ERP Oracle

### 2.3. Hạ tầng Oracle/ERP write-back chưa có sẵn

- File: `src/StationApp.Infrastructure/StationApp.Infrastructure.csproj`
- hiện chưa có package Oracle như `Oracle.ManagedDataAccess.Core`
- repo hiện có các SQL object liên quan ERP inbound SQL Server local, nhưng chưa có service/repository chuyên ghi ngược Oracle ERP

Kết luận:

- cần bổ sung hạ tầng kết nối Oracle mới

## 3. Dữ liệu đầu vào hiện có

Anh đã cung cấp thông tin kết nối Oracle:

- `hostname`: `10.0.0.11`
- `port`: `1521`
- `sid`: `orcl`
- `user`: `test_congno`

Đã chốt thêm mapping bảng/cột ERP:

- bảng ERP: `M_CommandLatching`
- trường `Số PTVC`: `transportNo`
- trường `Mooc`: `MoocNo`

Lưu ý triển khai:

- không hardcode password vào source
- password chỉ dùng ở máy triển khai / cấu hình runtime
- nếu cần test local, ưu tiên đọc từ `app_config`, file cấu hình local ngoài repo, hoặc biến môi trường

## 4. Điểm còn thiếu để code phần ERP hoàn chỉnh

Đã chốt nguyên tắc định danh bản ghi ERP:

- dùng `ErpCutOrderId` từ local để `WHERE` sang ERP

Đã chốt cột khóa ERP:

- `M_CommandLatching.documentNo` map với `ErpCutOrderId`

## 5. Hướng triển khai đề xuất

### 5.1. Mở khóa đúng 2 trường ở màn Danh sách xe vào

Thay đổi UI/VM để:

- với bản ghi `OUTBOUND`, vẫn giữ khóa các trường không được sửa nếu cần
- riêng `Số PTVC` và `Mooc` được phép chỉnh sửa

Hướng làm an toàn:

- không bỏ hẳn `IsOutboundDetailLockMode`
- tách thành các cờ nhỏ hơn, ví dụ:
  - `CanEditVehiclePlateInDetail`
  - `CanEditMoocInDetail`
  - `CanEditNonRegistrationFields`

Mục tiêu:

- chỉ mở đúng phạm vi yêu cầu
- tránh vô tình mở thêm các trường outbound khác như KH, SP, SL đặt nếu không mong muốn

### 5.2. Mở rộng use case cập nhật local + ERP

Mở rộng `UpdateIncomingRegistrationUseCase` theo hướng:

- vẫn cập nhật local `cut_orders`
- nếu bản ghi là `CutOrderSource = ERP` hoặc có `ErpCutOrderId/ErpRegistrationCode`, thì gọi thêm service sync ngược ERP

Đề xuất thêm interface application:

- `IErpCutOrderWriteBackService`

Nhiệm vụ:

- nhận dữ liệu cần update:
  - `CutOrderId` local
  - `ErpCutOrderId`
  - `ErpRegistrationCode`
  - `VehiclePlate`
  - `MoocNumber`
  - `UpdatedBy`
  - `UpdatedAt`
- thực hiện update Oracle ERP

### 5.3. Tầng Infrastructure kết nối Oracle

Thêm mới:

- package Oracle:
  - ưu tiên `Oracle.ManagedDataAccess.Core`
- service triển khai:
  - `OracleErpCutOrderWriteBackService`

Service này sẽ:

- build connection string từ cấu hình runtime
- mở kết nối Oracle
- chạy câu `UPDATE` có tham số
- update đúng bảng/cột:
  - `M_CommandLatching.transportNo`
  - `M_CommandLatching.MoocNo`
- ghi log rõ:
  - khóa ERP dùng để update
  - giá trị `Số PTVC` cũ/mới nếu truy xuất được
  - giá trị `Mooc` cũ/mới nếu truy xuất được
  - số dòng affected

Câu lệnh dự kiến ở mức khái niệm:

```sql
UPDATE M_CommandLatching
SET
    transportNo = :transportNo,
    MoocNo = :moocNo
WHERE documentNo = :erpCutOrderId
```

### 5.4. Chính sách transaction / tính nhất quán

Do local DB là SQL Server còn ERP là Oracle, không nên dùng distributed transaction.

Đề xuất flow:

1. validate dữ liệu
2. update ERP trước
3. nếu ERP update thành công thì mới commit local
4. nếu ERP không update được thì báo lỗi và không lưu local

Lý do:

- tránh local đã đổi nhưng ERP chưa đổi
- đúng với yêu cầu “lưu ở DB cân và đồng thời update bên ERP”

Lưu ý:

- nếu nghiệp vụ muốn ưu tiên local luôn lưu trước dù ERP lỗi, cần đổi chiến lược
- mặc định plan này chọn `ERP phải thành công thì local mới commit`

### 5.5. Điều kiện chỉ update ERP khi phù hợp

Chỉ gọi sync ngược ERP khi:

- bản ghi là cắt lệnh từ ERP
- có đủ khóa định danh ERP
- đang ở `ProcessingStage = IN_YARD`

Không gọi ERP update khi:

- cắt lệnh tạo tay local
- cắt lệnh tạm
- cắt lệnh đã không còn ở `Danh sách xe vào`

### 5.6. Logging và chẩn đoán

Thêm log ở các mức:

- `Information`
  - bắt đầu update local
  - bắt đầu update ERP
  - ERP affected rows
  - hoàn tất thành công
- `Warning`
  - không tìm thấy khóa ERP để update
  - ERP update ra `0 row`
- `Error`
  - lỗi kết nối Oracle
  - lỗi SQL Oracle
  - local/ERP mismatch

Khuyến nghị thêm audit action:

- `UPDATE_INCOMING_REGISTRATION_ERP_WRITEBACK`

## 6. Rà soát schema ERP cần làm trước khi code phần Oracle

Trước khi code chính thức, cần xác nhận nốt:

1. tên schema Oracle nếu không phải schema mặc định của user `test_congno`
2. có cần update thêm cột audit như:
   - `UPDATED_BY`
   - `UPDATED_AT`
3. có trigger / constraint nào bên ERP ảnh hưởng khi update không

Nếu không có tài liệu schema, cần làm một script kiểm tra read-only trước:

- tìm bản ghi trong `M_CommandLatching`
- đối chiếu theo `ErpCutOrderId`
- đối chiếu dữ liệu PTVC/Mooc thực tế

## 7. Phạm vi code dự kiến

### 7.1. UI

- `src/StationApp.UI/Views/IncomingVehicleListView.xaml`
- `src/StationApp.UI/ViewModels/IncomingVehicleListViewModel.cs`

### 7.2. Application

- `src/StationApp.Application/UseCases/UpdateIncomingRegistrationUseCase.cs`
- thêm interface mới trong `StationApp.Application/Interfaces`
- có thể mở rộng DTO request/result nếu cần mang thêm metadata ERP

### 7.3. Infrastructure

- `src/StationApp.Infrastructure/StationApp.Infrastructure.csproj`
- service Oracle write-back mới
- đăng ký DI ở `src/StationApp.UI/App.xaml.cs`
- có thể thêm lớp options/config nếu chọn hướng cấu hình hóa connection Oracle

## 8. Chiến lược cấu hình kết nối ERP

Đề xuất không commit thông tin nhạy cảm trực tiếp vào repo.

Đã chốt hướng cấu hình:

- lưu chuỗi kết nối ERP Oracle trong `appsettings.json`

Đề xuất:

- thêm section riêng, ví dụ:

```json
{
  "ErpOracle": {
    "ConnectionString": "User Id=test_congno;Password=...;Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=10.0.0.11)(PORT=1521))(CONNECT_DATA=(SID=orcl)))"
  }
}
```

Lưu ý:

- nếu repo có nguy cơ lộ mật khẩu, có thể commit placeholder trong `appsettings.json`, còn máy triển khai ghi đè bằng file cấu hình môi trường

## 9. Kịch bản kiểm thử cần bao phủ

### 9.1. Positive

- chọn 1 cắt lệnh `OUTBOUND` ở `Danh sách xe vào`
- sửa `Số PTVC`
- sửa `Mooc`
- bấm lưu
- local DB cập nhật đúng
- ERP Oracle cập nhật đúng ở:
  - `M_CommandLatching.transportNo`
  - `M_CommandLatching.MoocNo`
- reload grid vẫn thấy dữ liệu mới

### 9.2. Local only cases

- cắt lệnh tạo tay local không có khóa ERP
- cho phép lưu local
- không gọi Oracle update

### 9.3. ERP failure cases

- sai password Oracle
- Oracle unreachable
- update Oracle trả `0 row`

Kỳ vọng:

- không commit local nếu bản ghi thuộc ERP mà update Oracle thất bại
- hiển thị message rõ cho người dùng
- log đủ để debug

### 9.4. Guard cases

- bản ghi không còn ở `IN_YARD`
- không cho lưu
- dữ liệu `Số PTVC` trống
- validate chặn như hiện tại

## 10. Trình tự thực hiện đề xuất

1. Thêm chuỗi kết nối Oracle vào `appsettings.json`.
2. Triển khai câu update ERP theo khóa `M_CommandLatching.documentNo = ErpCutOrderId`.
3. Thêm package và service `OracleErpCutOrderWriteBackService`.
4. Mở khóa riêng 2 trường `Số PTVC` và `Mooc` trên màn `Danh sách xe vào`.
5. Mở rộng `UpdateIncomingRegistrationUseCase` để update ERP + local theo đúng thứ tự an toàn.
6. Bổ sung log/audit.
7. Test manual với 3 nhóm case:
   - ERP update thành công
   - ERP update `0 row`
   - Oracle connection lỗi

## 11. Kết luận duyệt plan

Phần này mình đánh giá:

- đã đủ thông tin để chốt:
  - bảng ERP `M_CommandLatching`
  - cột khóa `documentNo`
  - cột update `transportNo`, `MoocNo`
  - khóa nghiệp vụ local dùng để đối chiếu là `ErpCutOrderId`
  - chuỗi kết nối sẽ đặt trong `appsettings.json`
- phần thông tin đầu vào đã đủ để triển khai code theo plan này.
