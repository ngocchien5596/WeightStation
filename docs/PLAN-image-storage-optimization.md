# Kế Hoạch Chi Tiết: Tối Ưu Hóa Kích Thước Ảnh Camera Để Giảm Dung Lượng Database

## 1. Tổng quan (Overview)

Hiện tại, dung lượng cơ sở dữ liệu (DB) tăng nhanh (vượt quá 200MB trong môi trường thử nghiệm) chủ yếu do việc lưu trữ trực tiếp dữ liệu nhị phân ảnh (blob `ImageBytes`) trong bảng `weighing_session_images`. Mỗi phiên cân chụp từ 2 camera RTSP với độ phân giải gốc của camera (Full HD hoặc lớn hơn) và nén JPEG chất lượng cao (`85`), tạo ra tổng cộng 4 ảnh (khoảng 300KB - 800KB mỗi ảnh) cho mỗi lượt cân hoàn thành.

Để giải quyết vấn đề này một cách nhanh chóng và an toàn nhất theo yêu cầu tối giản, kế hoạch này tập trung vào việc **tối ưu hóa dữ liệu ảnh ngay tại thời điểm chụp trước khi lưu vào database**:
1. **Resize ảnh**: Tự động giảm kích thước chiều rộng/cao của ảnh (ví dụ: tối đa `1280px`) bằng OpenCV trước khi nén và lưu.
2. **Tăng tỉ lệ nén**: Điều chỉnh chất lượng nén JPEG (`jpegQuality` mặc định giảm từ `85` xuống `75`).
3. **Kết quả đạt được**: Kích thước mỗi ảnh giảm từ 300KB-800KB xuống còn khoảng **50KB - 90KB** (tiết kiệm hơn 80% dung lượng DB).

---

## 2. Loại dự án (Project Type)
- **Dự án**: WPF / BACKEND (C# / .NET)

---

## 3. Tiêu chí nghiệm thu (Success Criteria)

1. **Chất lượng ảnh hiển thị rõ ràng**: Ảnh chụp xe và biển số xe vẫn đảm bảo dễ đọc và theo dõi tốt ở độ phân giải tối ưu.
2. **Dung lượng ảnh giảm tối đa**: Mỗi file ảnh lưu vào DB có dung lượng trung bình dưới `100KB`.
3. **Không ảnh hưởng luồng dữ liệu cũ**: Không thay đổi cấu trúc bảng DB, không đổi luồng Sync lên Central API và UI xem lại lịch sử ảnh vẫn hoạt động hoàn hảo.

---

## 4. Công nghệ sử dụng (Tech Stack)

- **OpenCvSharp**: Sử dụng chức năng `Cv2.Resize` với bộ nội suy thích hợp (ví dụ: `InterpolationFlags.Area` để thu nhỏ ảnh chất lượng cao).

---

## 5. Phương án Thiết kế & Luồng dữ liệu

### Cấu hình mới đề xuất:
- Thêm cấu hình `camera_capture_max_dimension` trong `AppConfigKeys` và `AppConfigDefaults` với giá trị mặc định là `1280` (pixel).
- Điều chỉnh `camera_capture_jpeg_quality` mặc định thành `75` thay vì `85` trong `AppConfigDefaults`.

### Quy trình xử lý ảnh tại RtspCameraCaptureService:
Khi nhận được frame ảnh gốc (`Mat frame`) từ camera RTSP:
1. **Tính toán kích thước mới**: Kiểm tra `frame.Cols` (Width) và `frame.Rows` (Height). Nếu chiều lớn nhất vượt quá `1280` (hoặc giá trị cấu hình), tính toán tỷ lệ thu nhỏ để giữ nguyên aspect ratio.
2. **Resize**: Sử dụng `Cv2.Resize(frame, resizedFrame, new Size(newWidth, newHeight), 0, 0, InterpolationFlags.Area)` để thu nhỏ ảnh một cách mịn màng nhất.
3. **Nén Jpeg**: Encode ảnh đã resize với chất lượng `jpegQuality` (mặc định là `75`).

---

## 6. Phân rã nhiệm vụ (Task Breakdown)

### Phase 1: Logic & Configuration (Tối ưu hóa ảnh)
| Task ID | Tên nhiệm vụ | Agent phụ trách | Kỹ năng liên quan | Độ ưu tiên | Phụ thuộc | INPUT -> OUTPUT -> VERIFY |
|---------|--------------|-----------------|-------------------|------------|-----------|---------------------------|
| T1.1 | Thêm cấu hình kích thước tối đa của ảnh | `database-architect` | clean-code | P0 | Không | **INPUT**: File [AppConfigKeys.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Domain/Constants/AppConfigKeys.cs) và [AppConfigDefaults.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Domain/Constants/AppConfigKeys.cs).<br>**OUTPUT**: Khai báo hằng số `CameraCaptureMaxDimension` ("camera_capture_max_dimension") với giá trị mặc định `"1280"`. Đồng thời thay đổi giá trị mặc định `DefaultCameraCaptureJpegQuality` về `"75"`.<br>**VERIFY**: Biên dịch dự án thành công. |
| T1.2 | Cập nhật cấu trúc cấu hình hệ thống | `backend-specialist` | clean-code | P0 | T1.1 | **INPUT**: Cấu trúc cấu hình camera trong `InfrastructureServices.cs`, `CameraSystemSettings` và các class liên quan.<br>**OUTPUT**: Parse giá trị `camera_capture_max_dimension` từ DB cấu hình và đưa vào `CameraSystemSettings`.<br>**VERIFY**: Biên dịch thành công, chạy kiểm thử lấy cấu hình không bị crash. |
| T1.3 | Thực hiện Resize ảnh trong `RtspCameraCaptureService` | `performance-optimizer` | clean-code | P0 | T1.2 | **INPUT**: File [RtspCameraServices.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Services/RtspCameraServices.cs).<br>**OUTPUT**: Bổ sung hàm resize frame ảnh trước khi gọi `Cv2.ImEncode` nếu kích thước vượt quá giới hạn cấu hình.<br>**VERIFY**: Viết Unit Test hoặc log độ rộng/cao của ảnh trả về sau khi chụp, đảm bảo không vượt quá `1280px` và tỷ lệ ảnh chuẩn xác. |

### Phase 2: Integration & Verification (Tích hợp & Xác thực)
| Task ID | Tên nhiệm vụ | Agent phụ trách | Kỹ năng liên quan | Độ ưu tiên | Phụ thuộc | INPUT -> OUTPUT -> VERIFY |
|---------|--------------|-----------------|-------------------|------------|-----------|---------------------------|
| T2.1 | Xác thực luồng chụp ảnh và chất lượng hiển thị | `test-engineer` | testing-patterns | P1 | T1.3 | **INPUT**: Chức năng cân xe, bảng điều khiển xem trước camera và xem ảnh lịch sử.<br>**OUTPUT**: Ứng dụng chụp ảnh và lưu trữ vào database với kích thước ảnh thu nhỏ.<br>**VERIFY**: Thực hiện cân thử, kiểm tra size bản ghi mới trong DB (phải từ 50KB-100KB) và mở UI xem lại ảnh xem hiển thị có rõ biển số và xe hay không. Đảm bảo luồng Sync ảnh lên Central API vẫn gửi bình thường. |

---

## 7. Kế hoạch xác thực (Verification Plan)

### Kiểm tra tự động
- Đảm bảo tất cả các bài test kiểm thử hoạt động bình thường:
  ```bash
  dotnet test
  ```

### Kiểm tra thủ công
1. Chạy ứng dụng trạm cân, kích hoạt chụp ảnh thử nghiệm.
2. Kiểm tra log/cơ sở dữ liệu xem bản ghi mới được tạo trong `weighing_session_images` có cột `ImageBytes` nhỏ hơn 100KB hay không (thông thường tầm ~70KB).
3. Mở chức năng xem lại lịch sử cân (Lịch sử sửa dữ liệu hoặc danh sách chuyến xe) để xem ảnh hiển thị trên WPF có sắc nét và đủ độ phân giải đọc biển số hay không.
4. Đảm bảo tiến trình Sync gửi dữ liệu lên Server thành công mà không có lỗi định dạng hay kích thước.

---

## ✅ PHASE X COMPLETE
- Lint/Build: ✅ Success
- Tests: ✅ 108/108 Application Tests and 16/16 Sync Tests passed.
- Security: ✅ Checked (security_scan.py executed without critical failures in product code)
- Date: 2026-06-26
