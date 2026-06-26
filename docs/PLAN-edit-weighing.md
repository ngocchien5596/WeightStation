# PLAN-edit-weighing: Chức năng sửa số liệu cân và màn hình Lịch sử sửa số liệu cân (Phương án 2)

Kế hoạch này mô tả chi tiết giải pháp thiết kế cơ sở dữ liệu, nghiệp vụ xử lý tại tầng Application và giao diện người dùng (WPF UI) để thêm tính năng chỉnh sửa số xe (biển số xe) cho các lượt cân tại màn hình **Cân trạm đập** và **Cân mỏ sét**, đồng thời tự động cập nhật trọng lượng xe chuẩn, tính toán lại khối lượng tịnh và lưu trữ lịch sử chỉnh sửa thông qua bảng `AuditLogs`.

Lịch sử chỉnh sửa sẽ được quản lý tập trung ở một **Màn hình riêng (Menu chức năng riêng trong Báo cáo)**, đồng thời hỗ trợ chuyển hướng nhanh từ màn hình cân sang màn hình lịch sử khi bấm nút "Lịch sử sửa".

## Phân tích yêu cầu & Nghiệp vụ xử lý

1. **Phân quyền chỉnh sửa:** Cả tài khoản `ADMIN` và `OPERATOR` đều có quyền thực hiện chỉnh sửa và xem lịch sử.
2. **Nội dung sửa đổi:**
   - Người dùng được chọn một xe nội bộ khác thay thế cho xe hiện tại của lượt cân qua một dropdown hỗ trợ suggest và auto-complete.
   - Khi đổi số xe:
     - Tìm kiếm thông tin xe mới để cập nhật `VehiclePlate`, `InternalVehicleNo`, `DriverName`, `StandardTareVehicleId`.
     - Lấy trọng lượng xe chuẩn của xe mới tại ngày hiện tại (`effectiveStandardTare`).
     - Cập nhật lại `StandardTareWeightSnapshot` và `StandardTareSourceSnapshot` của lượt cân.
     - Nếu chế độ cân là Cân một lần bằng xe chuẩn (`SingleWithStandardTare`): Cập nhật `Weight2 = effectiveStandardTare` (vì ở chế độ này, Weight2 lưu trữ trọng lượng xe chuẩn).
     - Tính toán lại khối lượng hàng tịnh: `NetWeight = Weight2 - Weight1` hoặc `Weight1 - Weight2` tùy chế độ cân (chế độ 2 lần cân hoặc 1 lần cân).
3. **Lưu lịch sử sửa đổi:**
   - Sử dụng bảng `AuditLogs` có sẵn trong hệ thống.
   - Ghi nhận hành động với:
     - `Actor`: Tên tài khoản thực hiện sửa đổi (Admin/Operator).
     - `Action`: `"EDIT_WEIGHING_SESSION"`
     - `EntityType`: `"WeighingSession"`
     - `EntityId`: ID của phiên cân (`session.Id`).
     - `DetailJson`: Chuỗi JSON lưu trữ chi tiết lý do sửa đổi (`Reason`) cùng các thay đổi trước/sau (Old/New) đối với `VehiclePlate`, `StandardTareWeightSnapshot`, `Weight2` (nếu có), `NetWeight`.
4. **Đồng bộ dữ liệu (Sync):**
   - Đặt `SyncStatus = SyncStatus.SYNC_QUEUED` cho lượt cân sau khi sửa để `SyncOutboxWorker` tự động nhận diện và đẩy bản cập nhật lên Central API.
5. **Điều hướng lịch sử:**
   - Trên màn hình Cân trạm đập / Cân mỏ sét, dưới form nhập thông tin xe sẽ có nút **"SỬA SỐ LIỆU"** và nút **"LỊCH SỬ SỬA"**.
   - Khi bấm **"SỬA SỐ LIỆU"**: Mở Dialog Window để chỉnh sửa xe mới & lý do.
   - Khi bấm **"LỊCH SỬ SỬA"**: Điều hướng (Navigate) trực tiếp sang menu **"Lịch sử sửa số liệu"**, đồng thời tự động điền sẵn Biển số xe hoặc Số lượt cân của lượt cân đang chọn vào bộ lọc tìm kiếm và tải dữ liệu.

---

## Proposed Changes (Các thay đổi đề xuất)

### 1. Repository & Infrastructure Layer (Tầng Cơ sở dữ liệu)

#### [MODIFY] [IAuditLogRepository.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/Interfaces/IAuditLogRepository.cs)
- Thêm phương thức tìm kiếm lịch sử:
  ```csharp
  Task<IReadOnlyList<AuditLog>> SearchEditLogsAsync(string? vehiclePlate, string? sessionNo, DateTime fromDate, DateTime toDate, CancellationToken ct);
  ```

#### [MODIFY] [OtherRepositories.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Infrastructure/Repositories/OtherRepositories.cs)
- Triển khai `SearchEditLogsAsync` trong `AuditLogRepository`:
  - Lọc theo `Action == "EDIT_WEIGHING_SESSION"` và khoảng thời gian `CreatedAt`.
  - Lấy danh sách từ DB và thực hiện lọc thêm theo `vehiclePlate` hoặc `sessionNo` trong bộ nhớ (dựa vào `DetailJson`).

---

### 2. Application Layer (Tầng Nghiệp vụ)

#### [MODIFY] [CrusherWeighingUseCases.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/UseCases/CrusherWeighingUseCases.cs)
- Thêm phương thức `UpdateSessionVehicleAsync(Guid sessionId, Guid newVehicleId, string reason, CancellationToken ct)`:
  - Tải lượt cân `WeighingSession` theo `sessionId`.
  - Tải xe nội bộ mới theo `newVehicleId`.
  - Lấy trọng lượng xe chuẩn ngày hiện tại bằng `StandardTarePolicy.GetEffectiveStandardTare(vehicle, _clock.TodayLocal)`.
  - Nếu là cân 1 lần (`SingleWithStandardTare`), kiểm tra xem xe mới có cấu hình xe chuẩn chưa. Nếu chưa có, ném lỗi yêu cầu xe phải có trọng lượng xe chuẩn.
  - Lưu lại trạng thái cũ của các trường (`VehiclePlate`, `StandardTareWeightSnapshot`, `Weight2`, `NetWeight`) để làm log.
  - Cập nhật thông tin xe mới vào lượt cân.
  - Tính toán lại `NetWeight`.
  - Tạo một đối tượng `AuditLog` với `Action = "EDIT_WEIGHING_SESSION"` và lưu chi tiết thay đổi vào `DetailJson`. Thêm log này vào `IAuditLogRepository`.
  - Đặt `SyncStatus = SyncStatus.SYNC_QUEUED` và `UpdatedAt = _clock.NowLocal`, `UpdatedBy = CurrentUsername()`.
  - Lưu thay đổi qua `IUnitOfWork`.

#### [MODIFY] [ClayWeighingUseCases.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/UseCases/ClayWeighingUseCases.cs)
- Thêm phương thức tương tự `UpdateSessionVehicleAsync` phục vụ cho trạm cân mỏ sét.

---

### 3. UI ViewModels (Tầng ViewModel)

#### [NEW] [EditWeighingSessionVehicleViewModel.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/ViewModels/Dialogs/EditWeighingSessionVehicleViewModel.cs)
- ViewModel cho dialog chỉnh sửa xe của lượt cân.
- Thuộc tính:
  - `Title` ("Chỉnh sửa biển số xe lượt cân")
  - `SessionNo`, `WeighingMode`, `Weight1`, `Weight2`, `NetWeight` (Thông tin lượt cân hiện tại).
  - `VehiclePlateInput` (`AutocompleteInputViewModel`) để hỗ trợ nhập gợi ý và gợi ý biển số xe mới.
  - `SelectedVehicle` (`Vehicle?` lưu xe mới được chọn).
  - `NewStandardTareWeight` (Trọng lượng xe chuẩn dự kiến của xe mới).
  - `NewNetWeight` (Khối lượng tịnh dự kiến tính toán lại).
  - `Reason` (Lý do sửa, bắt buộc nhập).
  - `DialogResultValue` (`bool?`).
- Các lệnh (Commands):
  - `SaveCommand`: Kiểm tra tính hợp lệ, gọi UseCases để cập nhật dữ liệu, đóng dialog với kết quả `true`.
  - `CancelCommand`: Đóng dialog với kết quả `false`.

#### [NEW] [WeighingSessionEditHistoryViewModel.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/ViewModels/WeighingSessionEditHistoryViewModel.cs)
- ViewModel cho màn hình lớn quản lý lịch sử sửa số liệu.
- Thuộc tính:
  - `Title` ("Lịch sử sửa số liệu cân")
  - `SearchVehiclePlate` (string), `SearchSessionNo` (string).
  - `FromDate` (DateTime), `ToDate` (DateTime).
  - `HistoryItems` (`ObservableCollection<EditHistoryItemRow>` danh sách lịch sử hiển thị).
- Phương thức:
  - `InitializeAsync()`: Mặc định lấy thời gian trong vòng 7 ngày qua và tải danh sách.
  - `SearchAsync()`: Gọi `IAuditLogRepository.SearchEditLogsAsync` để lấy log và cập nhật danh sách `HistoryItems` (phân tích chuỗi JSON từ `DetailJson` ra các cột thông tin cũ/mới).
  - `SetFilter(string? vehiclePlate, string? sessionNo)`: Cho phép màn hình ngoài truyền thông tin để lọc nhanh.

#### [MODIFY] [CrusherWeighingViewModel.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/ViewModels/CrusherWeighingViewModel.cs)
- Thêm sự kiện điều hướng: `public event Action<string?, string?>? NavigateToEditHistoryRequested;`
- Thêm lệnh `EditSessionVehicleCommand` (chỉ chạy được khi `SelectedSession != null`).
  - Mở dialog `EditWeighingSessionVehicleWindow` truyền vào session đang chọn.
  - Nếu kết quả trả về là `true`, tải lại danh sách (`await LoadSessionsAsync()`).
- Thêm lệnh `ViewSessionHistoryCommand` (chỉ chạy được khi `SelectedSession != null`).
  - Bắn sự kiện `NavigateToEditHistoryRequested` truyền vào `SelectedSession.VehiclePlate` và `SelectedSession.SessionNo` để kích hoạt chuyển trang.

#### [MODIFY] [ClayWeighingViewModel.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs)
- Bổ sung sự kiện `NavigateToEditHistoryRequested`, lệnh `EditSessionVehicleCommand` và `ViewSessionHistoryCommand` tương tự Crusher.

#### [MODIFY] [MainViewModel.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/ViewModels/MainViewModel.cs)
- Bổ sung menu item:
  `public bool CanViewEditHistoryReport => StationAuthorization.CanViewOperationalScreens(_currentUserContext.RoleCode);`
- Cập nhật `CanViewReportsMenu` để bao gồm `CanViewEditHistoryReport`.
- Cập nhật lệnh điều hướng `NavigateAsync` cho đích `"Reports_EditHistory"`:
  - Khởi tạo `WeighingSessionEditHistoryViewModel`.
  - Nếu chuyển hướng từ màn cân trạm đập/mỏ sét, lấy thông tin bộ lọc nhận được để điền sẵn vào bộ lọc và thực hiện tìm kiếm tự động.
  - Đăng ký lắng nghe sự kiện `NavigateToEditHistoryRequested` từ `CrusherWeighingViewModel` và `ClayWeighingViewModel` để chuyển hướng sang màn lịch sử.

---

### 4. UI Views (Tầng Giao diện)

#### [NEW] [EditWeighingSessionVehicleWindow.xaml](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Views/Dialogs/EditWeighingSessionVehicleWindow.xaml)
- Window giao diện popup cho phép chỉnh sửa xe (tương tự như style các popup khác, cho phép chọn xe auto-complete và bắt buộc nhập lý do).

#### [NEW] [EditWeighingSessionVehicleWindow.xaml.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Views/Dialogs/EditWeighingSessionVehicleWindow.xaml.cs)
- Code-behind quản lý đóng/mở Window.

#### [NEW] [WeighingSessionEditHistoryView.xaml](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Views/WeighingSessionEditHistoryView.xaml)
- UserControl giao diện màn hình lịch sử sửa đổi lớn:
  - Phía trên là bộ lọc: Ô nhập Biển số xe, ô nhập Số lượt cân, Chọn Từ ngày - Đến ngày, nút "Tìm kiếm".
  - Phía dưới là một `DataGrid` hiển thị toàn bộ lịch sử. Các cột: STT, Thời gian sửa, Người sửa, Số lượt cân, Biển số xe cũ -> mới, TL xe chuẩn cũ -> mới, Tịnh cũ -> mới, Lý do sửa.

#### [NEW] [WeighingSessionEditHistoryView.xaml.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Views/WeighingSessionEditHistoryView.xaml.cs)
- Code-behind đăng ký DataContext.

#### [MODIFY] [CrusherWeighingView.xaml](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Views/CrusherWeighingView.xaml)
- Bổ sung nút **"SỬA SỐ LIỆU"** và **"LỊCH SỬ SỬA"** ở phần StackPanel phía dưới form nhập xe (cạnh nút "IN PC").

#### [MODIFY] [ClayWeighingView.xaml](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Views/ClayWeighingView.xaml)
- Bổ sung 2 nút bấm tương tự như màn hình Cân trạm đập.

#### [MODIFY] [MainWindow.xaml](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Views/MainWindow.xaml)
- Bổ sung nút menu con trong menu Báo cáo:
  ```xml
  <Button Style="{StaticResource SidebarMenuItemStyle}" Padding="30,0,12,0"
          Visibility="{Binding CanViewEditHistoryReport, Converter={StaticResource BoolToVis}}"
          Command="{Binding NavigateCommand}" CommandParameter="Reports_EditHistory">
      <TextBlock Text="Lịch sử sửa số liệu" VerticalAlignment="Center"/>
  </Button>
  ```

---

## Verification Plan (Kế hoạch xác minh)

### 1. Kiểm tra biên dịch
- Build dự án WPF UI: `dotnet build src\StationApp.UI\StationApp.UI.csproj`

### 2. Xác minh thủ công
- Chọn lượt cân -> click "SỬA SỐ LIỆU" -> Đổi xe mới -> Bắt buộc nhập lý do -> Lưu.
- Kiểm tra danh sách đã tải lại với biển số xe và khối lượng mới.
- Click "LÌCH SỬ SỬA" dưới form -> Màn hình tự động chuyển sang menu "Lịch sử sửa số liệu" bên trái, tự động điền sẵn bộ lọc và hiển thị thông tin thay đổi.
- Bấm trực tiếp vào menu "Lịch sử sửa số liệu" trên Sidebar trái -> Tự lọc tìm kiếm lịch sử của tất cả các xe, hoạt động chính xác.
