# Kế hoạch triển khai: Luồng quản lý Tàu và Chuyến xe cho Cân mỏ sét

## 1. Tổng quan
Thay đổi luồng nghiệp vụ **Cân mỏ sét (Clay Weighing)** từ luồng cân độc lập từng xe sang luồng quản lý theo **Tàu (Vessel Allocation / CutOrder)** và **Danh sách chuyến xe** của tàu đó (tương tự như luồng Cân xuất khẩu hiện tại).

### Các yêu cầu nghiệp vụ chính:
1. **Tạo chuyến Tàu trước**: Người vận hành tạo Tàu (dạng CutOrder nội bộ) trước khi cân. Thông tin tàu gồm: Tên phương tiện (vessel/barge), Đơn vị vận tải, Sản phẩm.
2. **Cơ chế chốt tổng**: Hỗ trợ chốt tổng chuyến tàu (Finalize) để không cho phép thêm chuyến xe hay chỉnh sửa thông tin nhằm bảo đảm toàn vẹn dữ liệu.
3. **Cân chuyến xe cho Tàu**: Khi tàu đang hoạt động, người vận hành tạo chuyến xe cân hàng cho tàu đó.
4. **Kiểm soát xe nội bộ**:
   * Chỉ cho phép chọn các xe nội bộ đang hoạt động và đã tồn tại trong Danh mục xe (Vehicle Master).
   * Khóa/chặn hoàn toàn việc nhập biển số xe tự do (free-text) hoặc thêm mới xe trực tiếp từ form cân mỏ sét.
   * Để thêm xe mới, bắt buộc vào Danh mục xe để tạo (form này không bắt buộc nhập TTCP đối với xe nội bộ).

---

## 2. Thiết kế Cơ sở dữ liệu & Entity

### 2.1. Thực thể Tàu (Vessel CutOrder)
Chúng ta sẽ sử dụng thực thể `CutOrder` hiện tại để biểu diễn chuyến Tàu của mỏ sét:
* `TransactionType` = `TransactionType.INBOUND`
* `StationCode` = mã trạm mỏ sét (QN03)
* `CutOrderSource` = `CutOrderSource.MANUAL`
* `VehiclePlate` = Tên phương tiện (Tên tàu/sà lan)
* `CustomerName` / `CustomerCode` = Đơn vị vận tải (nhà thầu vận chuyển)
* `ProductName` / `ProductCode` = Sản phẩm (Sét / vật tư sét)
* `PlannedWeight` = `null` (không bắt buộc nhập)
* `IsExportScale` = `false`

### 2.2. Chuyến xe (Weighing Session & Line)
Các chuyến xe cân mỏ sét sẽ được liên kết qua bảng `WeighingSessionLine` tương tự luồng xuất khẩu:
* `WeighingSession` đại diện cho phiên cân (lần 1, lần 2) của xe.
* `WeighingSessionLine` liên kết `WeighingSession` với `CutOrder` (Tàu mỏ sét).
* Trường `IsReturnedBrokenTrip` trên `WeighingSession` sẽ được sử dụng nếu có nghiệp vụ hàng hoàn.

### 2.3. Quy tắc tự động chọn Chế độ cân (Weighing Mode Selection)
Kế thừa hoàn toàn logic tự động chọn chế độ cân từ Cân mỏ đá:
* Khi chọn xe nội bộ:
  * Nếu xe có **Trọng lượng bì chuẩn (Standard Tare)** còn hiệu lực trong ngày (tra cứu qua `StandardTarePolicy.GetEffectiveStandardTare`) -> Tự động chuyển Chế độ cân sang **Cân 1 lần (SingleWithStandardTare)**. Khi cân lần 1 thành công, hệ thống tự động gán `Weight2 = Standard Tare` và tính `Net = Weight1 - Weight2`.
  * Nếu xe **không có Trọng lượng bì chuẩn** hiệu lực -> Tự động chuyển Chế độ cân sang **Cân 2 lần (TwoWeigh)**. Hệ thống sẽ giữ lượt cân ở trạng thái `PENDING_WEIGHT2` và bắt buộc phải thực hiện cân lần 2 để hoàn thành lượt cân (`Net = |Weight1 - Weight2|`).

### 2.4. Quy tắc nghiệp vụ Đổi số xe (Change Vehicle / Edit Session Vehicle)
Khi người dùng thực hiện đổi biển số xe mới cho một lượt cân mỏ sét (gọi `EditWeighingSessionVehicleWindow`):
1. **Kiểm tra hợp lệ**: Xe mới bắt buộc phải là xe nội bộ có hiệu lực và đã tồn tại trong Danh mục xe (Vehicle Master). Bắt buộc nhập lý do thay đổi.
2. **Xử lý thay đổi Chế độ cân & Khối lượng theo các trường hợp**:
   * **Trường hợp A: Lượt cân chưa hoàn thành (mới chỉ cân lần 1 - trạng thái PENDING_WEIGHT2)**:
     * *Nếu xe mới có bì chuẩn*: Chuyển chế độ cân của lượt cân sang **Cân 1 lần**, gán `Weight2 = Bì chuẩn mới`, tự động tính toán khối lượng tịnh `NetWeight = Weight1 - Weight2` và cập nhật trạng thái lượt cân thành **Hoàn thành (COMPLETED)**.
     * *Nếu xe mới không có bì chuẩn*: Giữ nguyên chế độ cân **Cân 2 lần**, xóa sạch các giá trị `Weight2` và `NetWeight` (nếu có), đưa trạng thái lượt cân về **Chờ cân lần 2 (PENDING_WEIGHT2)**.
   * **Trường hợp B: Lượt cân đã hoàn thành (COMPLETED)**:
     * *Nếu lượt cân gốc là Cân 1 lần*:
       * *Xe mới có bì chuẩn*: Giữ chế độ cân **Cân 1 lần**, cập nhật `Weight2 = Bì chuẩn mới` và tính lại `NetWeight = Weight1 - Weight2`.
       * *Xe mới không có bì chuẩn*:
         * Nếu lượt cân có lưu giá trị `Weight2` snapshot cũ -> Giữ nguyên chế độ cân **Cân 1 lần**, áp dụng khối lượng `Weight2` đó làm Bì chuẩn ngày hôm đó cho xe mới (cập nhật vào thực thể xe mới), giữ nguyên trạng thái hoàn thành.
         * Nếu không có `Weight2` snapshot cũ -> Chuyển chế độ sang **Cân 2 lần**, xóa `Weight2` và `Net`, đưa trạng thái về **Chờ cân lần 2**.
     * *Nếu lượt cân gốc là Cân 2 lần*:
       * *Xe mới có bì chuẩn*: Chuyển chế độ cân sang **Cân 1 lần**, gán `Weight2 = Bì chuẩn mới`, tính lại `NetWeight = Weight1 - Weight2`, giữ nguyên trạng thái hoàn thành.
       * *Xe mới không có bì chuẩn*: Giữ nguyên chế độ cân **Cân 2 lần**, tính lại `NetWeight` dựa trên hiệu số của `Weight1` và `Weight2` sẵn có.
3. **Thu hồi / Cập nhật Bì chuẩn (Standard Tare Invalidation & Update)**:
   * Khi đổi số xe từ Xe cũ sang Xe mới:
     * Nếu Xe cũ vừa được cập nhật Bì chuẩn ngày hôm nay từ lượt cân hiện tại (lượt cân này là lượt duy nhất tạo bì chuẩn ngày hôm đó cho Xe cũ) -> Thu hồi bì chuẩn cũ của xe (set `TtcpWeight = null` trên Xe cũ).
     * Nếu Xe mới chưa có bì chuẩn và lượt cân được sửa đổi có lưu `Weight2` hợp lệ -> Tự động cập nhật `Weight2` làm Bì chuẩn ngày hôm nay cho Xe mới (cập nhật `TtcpWeight` trên Xe mới).

---

## 3. Các thay đổi đề xuất

### 3.1. [NEW] Use Cases mới cho Mỏ sét
* **`CreateClayTemporaryCutOrderUseCase.cs`**: Tạo chuyến tàu mỏ sét (CutOrder thủ công với `TransactionType = INBOUND` và `StationCode = CLAY_STATION`).
* **`CreateClayVehicleSessionUseCase.cs`**: Tạo phiên cân chuyến xe nội bộ dưới Tàu được chọn. Thực hiện kiểm tra sự tồn tại của xe trong danh mục xe, gán thông tin xe vào session và line liên kết.
* **`FinalizeClayCutOrderUseCase.cs`**: Thực hiện chốt tổng chuyến tàu mỏ sét, đổi trạng thái sang `FINALIZED`.

### 3.2. [MODIFY] Repository & Service layer
* **`IWeighingSessionRepository.cs` / `WeighingSessionRepository.cs`**:
  * Thêm phương thức truy vấn danh sách Tàu mỏ sét (`GetClayCutOrdersAsync`) và danh sách chuyến xe tương ứng (`GetClayVehicleTripsAsync`).
* **`ClayWeighingUseCases.cs`**:
  * Chuyển đổi logic lưu trữ/cân từ cấp session trực tiếp sang cấp line + session liên kết với `CutOrder` mỏ sét.

### 3.3. [MODIFY] UI & ViewModels

#### [Weighing View & ViewModel] [ClayWeighingView.xaml](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Views/ClayWeighingView.xaml) & [ClayWeighingViewModel.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs)
* **Tái cấu trúc UI**: Thay đổi cấu trúc Grid từ đơn sang Double-Grid (giống `ExportWeighingView.xaml`):
  * Grid phía trên: Danh sách các Chuyến tàu mỏ sét (đang hoạt động / đã chốt tổng). Có nút **TẠO TÀU** và **CHỐT TỔNG**.
  * Cụm nút hành động và thanh tìm kiếm/lọc (`TẠO TÀU`, `TẠO CHUYẾN XE`, `CHUYỂN CHUYẾN`, `XÓA CHUYẾN XE`, `XEM ẢNH`, `CHỐT TỔNG`, Tìm kiếm, Checkbox hiển thị đã chốt) được đặt trong một `StackPanel` nằm ngang và căn giữa (`HorizontalAlignment="Center"`).
  * Grid phía dưới: Danh sách các Chuyến xe cân hàng cho Tàu đang chọn. Để tối ưu hóa không gian hiển thị, các cột cụ thể bao gồm: Số xe, Chế độ cân, Cân lần 1, Cân lần 2, TL bì, TL hàng đã được điều chỉnh giảm độ rộng đi 40 đơn vị, cột Ghi chú đã giảm 50 đơn vị so với mặc định ban đầu.
* **Form nhập thông tin xe**:
  * Khóa trường nhập biển số xe tự do. Sử dụng `AutocompleteTextBox` chỉ cho chọn các xe nội bộ có sẵn (`IsInternalVehicle = true`).
  * Ẩn/loại bỏ các nút tự tạo xe mới tại đây.
* **Logic điều khiển**:
  * Khi bấm nút **CÂN/LƯU**: Gọi `CreateClayVehicleSessionUseCase` để tạo chuyến xe gắn với Tàu đang chọn.
  * Tích hợp kiểm tra tính ổn định của cân và chụp ảnh camera (CAM1, CAM2) tương tự mỏ đá.

#### [Dialog Tạo tàu] [CreateClayVesselDialogWindow.xaml](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Views/Dialogs/CreateClayVesselDialogWindow.xaml) & [CreateClayVesselDialogViewModel.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/ViewModels/Dialogs/CreateClayVesselDialogViewModel.cs) [NEW]
* Cửa sổ Dialog nhập thông tin tạo tàu mới gồm các trường bắt buộc nhập (có đánh dấu sao đỏ `*`):
  * Tên phương tiện (đổi text nhãn cũ là "Tàu/Sà lan" thành "Tên phương tiện").
  * Mã đơn vị vận chuyển.
  * Đơn vị vận chuyển.
  * Mã hàng.
  * Hàng hóa.
* Sử dụng Auto-complete cho Sản phẩm và Đơn vị vận tải (khách hàng/nhà cung cấp). Ghi nhận ValidationMessage khi có trường để trống.

---

## 4. Kế hoạch Kiểm thử & Xác minh

### 4.1. Kiểm thử tự động (Unit Tests)
* Viết test cho `CreateClayTemporaryCutOrderUseCase` đảm bảo tạo đúng `CutOrder` với `TransactionType = INBOUND` và mã trạm mỏ sét.
* Viết test cho `CreateClayVehicleSessionUseCase` kiểm tra validation xe nội bộ phải tồn tại trong danh mục và gán đúng liên kết line.
* Viết test cho `FinalizeClayCutOrderUseCase` kiểm tra thay đổi trạng thái chốt tổng.

### 4.2. Kiểm thử thủ công (Manual Verification)
1. Mở màn hình **Cân mỏ sét**.
2. Bấm **TẠO TÀU**, điền tên phương tiện (ví dụ: `Tàu Sông Lô 09`), chọn đơn vị vận chuyển và sản phẩm (Sét).
3. Chọn tàu vừa tạo trên Grid, kiểm tra thông tin hiển thị ở Panel chi tiết.
4. Nhập biển số xe tại ô xe nội bộ:
   * Thử gõ biển số xe tự do không có trong danh mục xe -> Hệ thống không cho chọn và không cho cân.
   * Gõ biển số xe nội bộ hợp lệ -> Chọn xe và tiến hành cân lần 1 / cân lần 2 thành công.
5. Kiểm tra danh sách chuyến xe bên dưới tàu cập nhật đầy đủ thông tin chuyến xe vừa cân và lũy kế tấn tăng lên.
6. Chọn tàu và bấm **CHỐT TỔNG**, kiểm tra trạng thái chuyển sang đã chốt và form cân chuyến xe bị khóa (Read-Only) để bảo vệ dữ liệu.
