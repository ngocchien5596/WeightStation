# Plan: Thêm thông tin sản phẩm và khách hàng vào màn hình cân trạm đập

## 1. Overview
Bổ sung thông tin Sản phẩm (Mã SP, Tên SP) và Khách hàng (Mã KH, Tên KH) vào màn hình cân trạm đập (Crusher Weighing). Lưu trữ trực tiếp thông tin dưới dạng các cột trên bảng `weighing_sessions` của Local DB và Central DB (đối với giao dịch trạm đập). Đồng bộ các trường mới qua Central API và hiển thị, nhập liệu trên giao diện WPF với chức năng Autocomplete cùng các giá trị mặc định.

## 2. Project Type
- **Type**: Desktop Client (WPF, C#) + Central Backend (ASP.NET Core API).
- **Database**: SQLite/SQL Server (Local) và SQL Server (Central).

## 3. Success Criteria
- **Database**: 4 cột mới (`ProductCode`, `ProductName`, `CustomerCode`, `CustomerName`) được thêm vào bảng `weighing_sessions` ở local và central thông qua bootstrapper.
- **UI Input**: Các ô nhập Autocomplete Khách hàng và Sản phẩm hoạt động trơn tru.
- **Default Values**: Khi mở lượt cân mới, tự động điền:
  - Sản phẩm mặc định: Mã SP: `ĐV`, Tên SP: `Đá vôi`.
  - Khách hàng mặc định: Mã KH: `NCC1`, Tên KH: `Công ty CPXD và SXVLXD`.
- **Read-only Mode**: Khi xem lại các lượt cân đã hoàn tất (`COMPLETED`) hoặc đã hủy (`CANCELLED`), các ô nhập Sản phẩm và Khách hàng sẽ ở trạng thái chỉ đọc (Read-only).
- **DataGrid**: Hiển thị thêm 2 cột Khách hàng và Sản phẩm ở bảng danh sách lượt cân trạm đập.
- **Sync**: Đồng bộ dữ liệu đầy đủ từ Local lên Central thông qua `SyncOutbox` payload.

## 4. Tech Stack
- **Languages**: C# (.NET 8), XAML.
- **Frameworks**: WPF (CommunityToolkit.Mvvm), Entity Framework Core.
- **Engines**: SQL Server / SQLite.

## 5. File Structure
Các file chính cần sửa đổi:
- `src/StationApp.Domain/Entities/WeighingSession.cs` (Entity)
- `src/StationApp.Infrastructure/Persistence/Configurations/WeighingSessionEntityConfigurations.cs` (EF Configuration)
- `src/StationApp.Infrastructure/Persistence/SchemaCompatibilityBootstrapper.cs` (Local DB Migrator)
- `src/StationApp.CentralApi/Program.cs` (Central DB Migrator)
- `src/StationApp.Application/DTOs/Dtos.cs` (DTOs)
- `src/StationApp.Application/UseCases/CrusherWeighingUseCases.cs` (UseCases & Request DTOs)
- `src/StationApp.Infrastructure/Repositories/WeighingSessionRepository.cs` (Repository Query)
- `src/StationApp.UI/ViewModels/CrusherWeighingViewModel.cs` (WPF ViewModel)
- `src/StationApp.UI/Views/CrusherWeighingView.xaml` (WPF View)

## 6. Task Breakdown

### Phase 1: Database & Domain Models (P0)
- **Task 1.1**: Thêm thuộc tính mới vào `WeighingSession.cs`
  - **Input**: `WeighingSession.cs` hiện tại.
  - **Output**: 4 thuộc tính `ProductCode`, `ProductName`, `CustomerCode`, `CustomerName`.
  - **Verify**: Code compiles.
- **Task 1.2**: Cấu hình EF mapping trong `WeighingSessionEntityConfigurations.cs`
  - **Input**: `WeighingSessionEntityConfigurations.cs` hiện tại.
  - **Output**: Thiết lập độ dài tối đa cho 4 thuộc tính mới.
  - **Verify**: Code compiles.
- **Task 1.3**: Thêm cột tự động ở Local DB qua `SchemaCompatibilityBootstrapper.cs`
  - **Input**: `SchemaCompatibilityBootstrapper.cs` hiện tại.
  - **Output**: Bổ sung 4 cột mới vào `WeighingSessionColumnPatches`.
  - **Verify**: Khởi chạy ứng dụng trạm cân, kiểm tra cấu trúc bảng `weighing_sessions` ở local DB (SQLite hoặc Local SQL Server) đảm bảo đã được tự động ALTER TABLE thêm 4 cột mới.
- **Task 1.4**: Thêm cột tự động ở Central DB (Server) qua `Program.cs` của `CentralApi`
  - **Input**: `Program.cs` hiện tại ở CentralApi.
  - **Output**: Gọi `EnsureColumnAsync` cho 4 cột mới.
  - **Verify**: Khởi chạy Central API, kiểm tra cấu trúc bảng `weighing_sessions` trên Central database (Server SQL Server) đảm bảo đã được tự động ALTER TABLE thêm 4 cột mới.

### Phase 2: Application DTOs & UseCases (P1)
- **Task 2.1**: Cập nhật DTO `CrusherWeighingSessionListItem` trong `Dtos.cs`
  - **Input**: `Dtos.cs` hiện tại.
  - **Output**: Thêm 4 trường `ProductCode`, `ProductName`, `CustomerCode`, `CustomerName`.
  - **Verify**: Code compiles.
- **Task 2.2**: Cập nhật `CreateCrusherSessionRequest` và UseCase `CrusherWeighingUseCases.cs`
  - **Input**: `CrusherWeighingUseCases.cs` hiện tại.
  - **Output**:
    - `CreateCrusherSessionRequest` có 4 trường Product/Customer.
    - `CreateSessionAsync` gán 4 trường này vào `WeighingSession` thực thể.
    - `SearchSessionsAsync` (query repository) map 4 trường này vào kết quả trả về.
  - **Verify**: Code compiles.
- **Task 2.3**: Cập nhật repository query trong `WeighingSessionRepository.cs`
  - **Input**: `WeighingSessionRepository.cs` hiện tại.
  - **Output**: Đọc 4 trường Product/Customer mới trong `SearchCrusherSessionsAsync`.
  - **Verify**: Trả về đủ dữ liệu trong DTO khi query.

### Phase 3: WPF UI Implementation (P2)
- **Task 3.1**: Cập nhật ViewModel `CrusherWeighingViewModel.cs`
  - **Input**: `CrusherWeighingViewModel.cs` hiện tại.
  - **Output**:
    - Khởi tạo 4 Autocomplete field: `FormCustomerCodeInput`, `FormCustomerInput`, `FormProductCodeInput`, `FormProductNameInput`.
    - Thêm 4 property observable backing.
    - Wire text changes và viết hàm xử lý select.
    - Tự động điền giá trị mặc định của SP (`ĐV`/`Đá vôi`) và KH (`NCC1`/`Công ty CPXD và SXVLXD`) khi khởi tạo lượt cân mới hoặc deselect.
    - Truyền dữ liệu Product/Customer vào UseCase khi bấm lưu (`SaveCrusherWeighingAsync`).
    - Bind/clear trường dữ liệu khi dòng được chọn thay đổi. Khóa input khi xem dòng đã hoàn tất/hủy (`IsWeighingReadOnly`).
  - **Verify**: Code compiles.
- **Task 3.2**: Thiết kế UI XAML `CrusherWeighingView.xaml`
  - **Input**: `CrusherWeighingView.xaml` hiện tại.
  - **Output**:
    - Thêm 2 hàng cho grid và chỉnh dịch các row index của các phần tử phía sau.
    - Bổ sung 4 `controls:AutocompleteTextBox` tương ứng với Khách hàng và Sản phẩm.
    - Thêm 2 cột Khách hàng & Sản phẩm vào DataGrid hiển thị.
  - **Verify**: Giao diện hiển thị đúng layout và không bị vỡ.

---

## 7. Phase X: Verification

### Automated Checks
- [ ] Chạy Build toàn bộ solution, không có lỗi biên dịch.
- [ ] Chạy kiểm tra local DB schema được cập nhật thành công sau khi khởi chạy ứng dụng.

### Manual Checks
- [ ] Mở màn hình **Cân trạm đập**:
  - [ ] Chọn một xe nội bộ -> Các ô nhập Khách hàng và Sản phẩm tự động điền các giá trị mặc định (`NCC1`/`Công ty CPXD và SXVLXD`, `ĐV`/`Đá vôi`).
  - [ ] Thử gõ gợi ý tìm kiếm Khách hàng/Sản phẩm khác để kiểm tra autocomplete.
  - [ ] Tiến hành cân lần 1 và nhấn **LƯU**.
  - [ ] Xác nhận lượt cân mới được thêm vào danh sách phía dưới và hiển thị đúng thông tin Khách hàng, Sản phẩm trên 2 cột mới của DataGrid.
  - [ ] Click vào dòng lượt cân đã hoàn tất -> Các ô nhập hiển thị đúng dữ liệu và chuyển sang trạng thái chỉ đọc (Read-only).
- [ ] Kiểm tra Database:
  - [ ] Kiểm tra dòng vừa tạo trong local SQLite/SQL Server table `weighing_sessions` có các giá trị Khách hàng/Sản phẩm đúng như đã nhập.
  - [ ] Kiểm tra dòng vừa tạo trong Central SQL Server table `weighing_sessions` đã được sync đúng dữ liệu.
