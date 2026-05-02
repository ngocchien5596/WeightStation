# PLAN - ERP SEARCH CLEANUP

Mục tiêu: Tối giản giao diện tìm kiếm tại màn hình Xe nhập hàng (Incoming Vehicle List) bằng cách loại bỏ các trường lọc chi tiết không cần thiết, chỉ giữ lại các trường định danh chính và nút Tìm kiếm.

## 1. Hiện trạng
Màn hình `IncomingVehicleListView.xaml` hiện có 2 hàng tìm kiếm:
- **Hàng 0:** Mã ĐKPT, Số PTVC, Mooc, Nút TÌM KIẾM.
- **Hàng 1:** Tên tài xế, Khách hàng, Mã SP, Tên sản phẩm.

## 2. Thay đổi đề xuất

### 2.1 UI - IncomingVehicleListView.xaml
- [MODIFY] Loại bỏ `<StackPanel Grid.Row="1" ...>` chứa các trường: Tên tài xế, Khách hàng, Mã SP, Tên sản phẩm.
- [MODIFY] Cập nhật `Grid.RowDefinitions` của Grid bên trong `HeaderBorder` để xóa bỏ Row 1 (hoặc chuyển thành chỉ 1 Row).
- [KEEP] Giữ nguyên nút **TÌM KIẾM** tại Row 0.

### 2.2 ViewModel - IncomingVehicleListViewModel.cs
- [MODIFY] Xóa bỏ các thuộc tính và lệnh khởi tạo liên quan đến các trường tìm kiếm đã bị loại bỏ:
    - `SearchDriverInput`, `SearchCustomerInput`, `SearchProductCodeInput`, `SearchProductNameInput`.
    - Các lệnh `WireTextState` tương ứng.
- [MODIFY] Cập nhật phương thức `LoadVehiclesAsync` (phần khởi tạo `IncomingVehicleListFilter`) để truyền `null` hoặc chuỗi trống cho các tham số không còn dùng.
- [MODIFY] Cập nhật phương thức `HasSearchFilters` để không kiểm tra các trường đã xóa.

## 3. Các bước thực hiện

1. Sửa file XAML để cập nhật giao diện.
2. Sửa file ViewModel để dọn dẹp code behind và logic filter.
3. Kiểm tra biên dịch (Compile check).

## 4. Kế hoạch xác minh (Verification Plan)

### Kiểm tra thủ công
- Chạy ứng dụng, vào màn hình **Xe nhập hàng**.
- Xác nhận hàng tìm kiếm thứ 2 đã biến mất.
- Nhập **Biển số xe** hoặc **Mã ĐKPT** và nhấn nút **TÌM KIẾM**.
- Xác nhận danh sách vẫn được lọc đúng theo các tiêu chí còn lại.
- Thử nhấn **Enter** trong ô nhập liệu để kích hoạt tìm kiếm.

---
**Bạn có đồng ý với kế hoạch này không? Nếu có, hãy phản hồi "Proceed" để tôi bắt đầu thực hiện.**
