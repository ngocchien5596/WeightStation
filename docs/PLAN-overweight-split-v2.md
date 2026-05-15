# KẾ HOẠCH NÂNG CẤP OVERWEIGHT SPLITTING (V2) - CHI TIẾT UX & LOGIC

## 1. MỤC TIÊU
Nâng cấp thuật toán gợi ý tách quá tải sang hệ số ngẫu nhiên và bổ sung chế độ "Tùy chỉnh tay" (Manual Override) với bộ quy tắc validation chặt chẽ.

## 2. QUY TẮC NGHIỆP VỤ & UX (CHỐT)

### 2.1 Thuật toán Random
- Hệ số: `RandomSplitFactor` thuộc `[0.0001; OverweightSplitStepWeight]`.
- Retry: Tối đa 50 lần.
- Fallback: Chọn ngẫu nhiên số nguyên hợp lệ trong miền `[LowerBound; UpperBound]`.

### 2.2 Chế độ Manual Override
- **Kích hoạt:** Khi người dùng chỉnh sửa trực tiếp giá trị Phiếu 1 hoặc Phiếu 2.
- **Trạng thái:** Chuyển từ "Đề xuất hệ thống" sang "Tùy chỉnh tay".
- **Hiển thị Factor:** Ẩn giá trị % random, thay bằng `--` hoặc `Tùy chỉnh tay`.
- **Validation:** Re-validate ngay lập tức khi thay đổi giá trị. Khóa nút xác nhận nếu vi phạm quy tắc tổng hoặc ngưỡng TTCP.

### 2.3 Thông báo lỗi
- **Miền khả thi rỗng:** "Lượt cân này không thể tách hợp lệ thành 2 phiếu với ngưỡng TTCP 10% hiện tại. Vui lòng chọn Không tách hoặc kiểm tra lại tham số tách tải."

## 3. CÁC BƯỚC THỰC HIỆN CHI TIẾT

### Bước 1: Core Service Upgrade (`WeighingSessionOverweightService.cs`)
- Implement hàm tiền kiểm tra khả thi.
- Refactor `BuildSplitPlan` để hỗ trợ vòng lặp random và retry.
- Đảm bảo làm tròn đơn vị dùng `MidpointRounding.AwayFromZero`.

### Bước 2: UI/ViewModel Upgrade (`WeighingViewModel.cs`)
- Thêm cờ `IsManualSplitOverride`.
- Implement logic đồng bộ hóa khi sửa tay (sửa phiếu 1 tự tính phiếu 2 và ngược lại để hỗ trợ user, nhưng vẫn phải validate tổng).
- Quản lý trạng thái Enable/Disable nút xác nhận dựa trên tính hợp lệ của phương án split.

### Bước 3: UI Layout Upgrade (`WeighingView.xaml`)
- Chuyển Label hiển thị kết quả split sang TextBox.
- Thêm thông tin trạng thái Mode Split (System vs Manual).
- Thêm nút **[ĐỀ XUẤT LẠI]**.

### Bước 4: System Settings
- Cập nhật màn hình tham số hệ thống để validate `OverweightSplitStepWeight > 0.0001`.

## 4. DANH SÁCH FILE THAY ĐỔI
- `StationApp.Domain/Constants/AppConfigKeys.cs`
- `StationApp.Application/Services/WeighingSessionOverweightService.cs`
- `StationApp.Application/UseCases/WeighingSessionOverweightUseCases.cs`
- `StationApp.UI/ViewModels/WeighingViewModel.cs`
- `StationApp.UI/Views/WeighingView.xaml`
- `StationApp.UI/ViewModels/Settings/SystemSettingsViewModel.cs`
- `StationApp.UI/Converters/SplitModeDisplayConverter.cs` (New)

## 5. KẾ HOẠCH KIỂM THỬ
1. Kiểm tra tính ngẫu nhiên khi bấm "Đề xuất lại".
2. Kiểm tra việc ẩn hệ số random khi user gõ phím sửa cân.
3. Kiểm tra thông báo lỗi khi không thể chia làm 2 phiếu (case NetWeight quá lớn).
4. Kiểm tra logic phân line và bag count khi split line.
