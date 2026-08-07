# PLAN: Tích hợp tính năng Zoom UI (Phóng to / Thu nhỏ) trên ứng dụng WPF

Tài liệu này phác thảo kế hoạch nghiên cứu khả thi, thiết kế kỹ thuật và các bước thực hiện tích hợp tính năng điều chỉnh tỷ lệ hiển thị (Zoom) giao diện ứng dụng trạm cân.

---

## 1. Phân tích & Đánh giá khả thi (Feasibility Analysis)

### A. Phương pháp Zoom tối ưu trong WPF
WPF hỗ trợ đồ họa vector và hệ thống layout động, giúp việc co dãn tỷ lệ giao diện rất mượt mà. Chúng ta sẽ sử dụng phương pháp **`LayoutTransform` kết hợp `ScaleTransform`** trên Grid gốc của Window:
- **Lý do**: Khác với `RenderTransform` (chỉ scale ảnh sau khi vẽ, gây vỡ chữ và lệch tọa độ click chuột), `LayoutTransform` hoạt động ở giai đoạn tính toán hình học (Measure/Arrange). Hệ thống sẽ bố cục lại các control (TextBox, Button, DataGrid) tương ứng với kích thước mới, đảm bảo độ sắc nét cao nhất của phông chữ và tính chính xác tuyệt đối của tọa độ trỏ chuột.

### B. Phạm vi Zoom & Dải Zoom
- **Phạm vi**: Zoom toàn bộ giao diện màn hình (bao gồm cả thanh Sidebar menu bên trái và topbar).
- **Mức tối thiểu**: `0.8` (80% kích thước gốc).
- **Mức tối đa**: `1.5` (150% kích thước gốc).
- **Bước nhảy mỗi lần zoom**: `0.05` (5%).

---

## 2. Lưu trữ cấu hình Local (Local Config Storage)

Do độ phân giải màn hình của từng máy trạm cân khác nhau, cấu hình `ZoomLevel` sẽ được lưu trữ cục bộ (Local) thay vì lưu trữ trên database tập trung.

- **Vị trí lưu trữ**: Lưu trữ trực tiếp trong tệp cấu hình `appsettings.json` tại thư mục cài đặt của ứng dụng bằng cách bổ sung key `"ZoomLevel": 1.0` ở mức root.
- **Cơ chế ghi**: Mỗi khi người dùng thay đổi mức zoom, ứng dụng sẽ thực hiện đọc tệp `appsettings.json` hiện tại từ `AppContext.BaseDirectory`, phân tích và cập nhật giá trị `"ZoomLevel"`, sau đó lưu đè (ghi đè) trở lại.
- **Phòng ngừa lỗi**: Để tránh lỗi phân quyền ghi (Access Denied) nếu ứng dụng cài ở các thư mục như `Program Files`, hệ thống sẽ thực hiện bắt lỗi ngoại lệ `UnauthorizedAccessException`. Nếu xảy ra lỗi phân quyền, ứng dụng sẽ chuyển hướng lưu cấu hình zoom này sang thư mục người dùng cục bộ `%LOCALAPPDATA%\StationApp\zoomsettings.json` làm phương án dự phòng.

---

## 3. Các thay đổi chi tiết (Proposed Changes)

### Component: WPF ViewModel (Application State)
#### [MODIFY] [MainViewModel.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/ViewModels/MainViewModel.cs)
- Khai báo thuộc tính để lưu trữ mức zoom:
  ```csharp
  [ObservableProperty] private double _zoomLevel = 1.0;
  ```
- Nạp giá trị `ZoomLevel` từ `appsettings.json` khi khởi tạo `MainViewModel`:
  - Đọc từ `appsettings.json` tại `BaseDirectory`. Nếu không tồn tại hoặc bị lỗi, đọc từ file dự phòng ở `%LOCALAPPDATA%`.
- Triển khai các Command điều hướng zoom:
  - `ZoomInCommand`: Tăng `ZoomLevel` thêm 0.05 (giới hạn tối đa 1.5).
  - `ZoomOutCommand`: Giảm `ZoomLevel` đi 0.05 (giới hạn tối thiểu 0.8).
  - `ResetZoomCommand`: Reset `ZoomLevel` về lại 1.0.
- Thêm phương thức ghi nhận và lưu giá trị `ZoomLevel` xuống file `appsettings.json` (hoặc file dự phòng ở `%LOCALAPPDATA%` nếu lỗi ghi).

---

### Component: WPF View (XAML Layout)
#### [MODIFY] [MainWindow.xaml](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Views/MainWindow.xaml)
- Áp dụng `LayoutTransform` lên Grid gốc của `MainWindow`:
  ```xml
  <Grid>
      <Grid.LayoutTransform>
          <ScaleTransform ScaleX="{Binding ZoomLevel}" ScaleY="{Binding ZoomLevel}"/>
      </Grid.LayoutTransform>
      ...
  </Grid>
  ```

---

### Component: Tương tác Phím tắt & Lăn chuột
#### [MODIFY] [MainWindow.xaml.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Views/MainWindow.xaml.cs)
- Lắng nghe sự kiện lăn chuột `PreviewMouseWheel` trên Window:
  - Nếu phím `Ctrl` đang được nhấn giữ (`Keyboard.Modifiers == ModifierKeys.Control`):
    - Lăn lên $\rightarrow$ Gọi `ZoomInCommand`.
    - Lăn xuống $\rightarrow$ Gọi `ZoomOutCommand`.
    - Đặt `e.Handled = true` để tránh cuộn trang scrollbar của view hiện tại.
- Lắng nghe sự kiện nhấn phím `PreviewKeyDown` trên Window để hỗ trợ phím tắt:
  - `Ctrl` + `Plus` / `Ctrl` + `Equal` $\rightarrow$ Tăng zoom (`ZoomInCommand`).
  - `Ctrl` + `Minus` $\rightarrow$ Giảm zoom (`ZoomOutCommand`).
  - `Ctrl` + `D0` (số 0) $\rightarrow$ Reset zoom về 100% (`ResetZoomCommand`).

---

## 4. Kịch bản xác minh (Verification Plan)

### Kiểm thử Thủ công (Manual Test Cases)
1. **Kiểm tra Phím tắt**:
   - Nhấn giữ `Ctrl` + Lăn chuột lên/xuống $\rightarrow$ Đảm bảo toàn bộ ứng dụng phóng to/thu nhỏ tương ứng (Sidebar menu và vùng hiển thị chính cùng scale đồng bộ).
   - Nhấn `Ctrl + Plus` và `Ctrl + Minus` $\rightarrow$ Đảm bảo thay đổi theo bước 5% mượt mà.
   - Nhấn `Ctrl + 0` $\rightarrow$ Đảm bảo quay về tỷ lệ gốc `1.0x`.
2. **Kiểm tra độ chính xác của tương tác chuột**:
   - Đặt zoom ở mức tối đa `1.5x` và tối thiểu `0.8x`, di chuột click chọn các dòng DataGrid, checkbox. Đảm bảo click chuẩn xác tại vị trí con trỏ (không bị lệch điểm chạm).
3. **Kiểm tra hiệu năng**:
   - Mở camera RTSP (Camera Preview) và thực hiện lăn chuột zoom. Đảm bảo camera preview không bị lag, giật hoặc đứng hình.
4. **Kiểm tra khả năng lưu trữ cấu hình**:
   - Đổi zoom lên `1.25x`, tắt ứng dụng.
   - Mở file `appsettings.json` tại thư mục bin/debug để kiểm tra xem key `"ZoomLevel": 1.25` được ghi nhận đúng chưa.
   - Khởi động lại ứng dụng và kiểm tra xem giao diện có mở ra ở kích thước `1.25x` hay không.
