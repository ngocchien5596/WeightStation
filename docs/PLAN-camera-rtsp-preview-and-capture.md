# Kế Hoạch Chi Tiết: Tích Hợp Camera RTSP Cho Màn Lập Phiếu Cân

## 1. Mục tiêu

Bổ sung chức năng camera vào ứng dụng trạm cân để:

- cấu hình tối đa `2 camera RTSP` trong `Cấu hình hệ thống`
- chỉ `preview realtime 1 camera` tại một thời điểm
- cho phép người dùng chọn camera đang preview
- chụp ảnh từ `cả 2 camera` khi:
  - `Lưu cân lần 1`
  - `Lưu cân lần 2`
- lưu ảnh vào `DB local`
- không làm chậm luồng cân chính hoặc gây block UI đáng kể

## 2. Phạm vi Phase 1

Phase đầu chỉ bao gồm:

- cấu hình 2 camera RTSP
- preview realtime 1 camera ở màn `Lập phiếu cân`
- chụp và lưu ảnh `2 camera` tại `cân lần 1` và `cân lần 2`
- lưu ảnh vào DB local theo `WeighingSession`

Chưa bao gồm:

- xem lại ảnh lịch sử
- đồng bộ ảnh sang ERP
- OCR / AI / nhận diện biển số
- ghi hình video liên tục
- nhiều hơn 2 camera

## 3. Quy tắc nghiệp vụ đã chốt

### 3.1 Preview

- Hệ thống hỗ trợ tối đa `2 camera`
- Chỉ `1 camera` được preview realtime tại một thời điểm
- Người dùng chọn:
  - `Camera 1`
  - hoặc `Camera 2`
- Việc đổi camera preview không ảnh hưởng đến dữ liệu nghiệp vụ cân

### 3.2 Capture khi lưu cân

- Khi `Lưu cân lần 1`:
  - sau khi lưu nghiệp vụ thành công, hệ thống chụp ảnh từ `Camera 1` và `Camera 2`
- Khi `Lưu cân lần 2`:
  - sau khi lưu nghiệp vụ thành công, hệ thống chụp ảnh từ `Camera 1` và `Camera 2`

### 3.3 Xử lý lỗi

- Nếu chụp ảnh lỗi:
  - không rollback thao tác lưu cân
  - vẫn giữ kết quả cân thành công
  - chỉ ghi log/cảnh báo mềm

### 3.4 Giao thức

- Giao thức camera chốt dùng: `RTSP`

## 4. Bố cục UI mới

### 4.1 Vị trí hiển thị

Ở màn `Lập phiếu cân`, hàng trên cùng sẽ chia thành 3 khối:

1. `Thông tin chi tiết lượt cân`
2. `Preview camera`
3. `Khu vực hiển thị số cân`

### 4.2 Quy tắc bố cục

- Thu hẹp khối `thông tin chi tiết lượt cân`
- Thêm khung `preview camera` vào `bên trái` khối `số cân`
- Khối `số cân` vẫn giữ vai trò chính cho thao tác cân

### 4.3 Thành phần trong panel preview

- nhãn camera đang xem
- lựa chọn:
  - `Camera 1`
  - `Camera 2`
- vùng preview realtime
- trạng thái kết nối:
  - `Đang kết nối`
  - `Mất kết nối`
  - `Chưa cấu hình`
  - `Đã tắt`

## 5. Thiết kế cấu hình hệ thống

Thêm nhóm `Cấu hình camera` trong `Cấu hình hệ thống`.

### 5.1 Các key cấu hình đề xuất

- `camera_1_enabled`
- `camera_1_name`
- `camera_1_rtsp_url`
- `camera_2_enabled`
- `camera_2_name`
- `camera_2_rtsp_url`
- `camera_preview_default`
- `camera_capture_timeout_ms`
- `camera_capture_jpeg_quality`
- `camera_capture_warmup_frames`

### 5.2 Ý nghĩa

- `enabled`: bật/tắt camera khỏi hệ thống
- `name`: tên hiển thị thân thiện
- `rtsp_url`: địa chỉ RTSP
- `preview_default`: camera mở mặc định khi vào màn cân
- `timeout`: giới hạn thời gian chụp
- `jpeg_quality`: chất lượng file ảnh lưu DB
- `warmup_frames`: số frame bỏ qua trước khi chụp để ổn định ảnh

## 6. Thiết kế cơ sở dữ liệu

### 6.1 Bảng mới

Tạo bảng riêng để lưu ảnh, ví dụ:

`weighing_session_images`

### 6.2 Cột đề xuất

- `Id`
- `WeighingSessionId`
- `CaptureStage`
- `CameraCode`
- `CameraName`
- `RtspUrlSnapshot`
- `ImageFormat`
- `ImageBytes`
- `FileSizeBytes`
- `CapturedAt`
- `CapturedBy`
- `IsDeleted`
- `DeletedAt`
- `DeletedBy`

### 6.3 Giá trị nghiệp vụ

- `CaptureStage`:
  - `WEIGHT1`
  - `WEIGHT2`
- `CameraCode`:
  - `CAM1`
  - `CAM2`

### 6.4 Lý do lưu bảng riêng

- tách BLOB khỏi bảng nghiệp vụ chính
- tránh làm nặng `weighing_sessions`
- dễ mở rộng tra cứu ảnh sau này

## 7. Kiến trúc kỹ thuật

### 7.1 Tách preview và capture

Nên tách 2 service:

- `preview service`
- `capture service`

### 7.2 Preview service

Trách nhiệm:

- chỉ giữ `1 RTSP stream active`
- decode frame nền
- xuất frame đã resize cho UI
- hỗ trợ đổi camera preview

### 7.3 Capture service

Trách nhiệm:

- mở kết nối RTSP ngắn hạn để chụp
- chụp độc lập với preview hiện tại
- chụp lần lượt hoặc song song có kiểm soát cho `CAM1` và `CAM2`
- trả về JPEG bytes

## 8. Tích hợp vào luồng cân

### 8.1 Cân lần 1

Điểm chèn:

- sau khi `CaptureSessionWeight1UseCase` lưu thành công

Luồng:

1. save nghiệp vụ cân lần 1
2. trigger chụp `CAM1`
3. trigger chụp `CAM2`
4. lưu 2 ảnh vào `weighing_session_images`
5. nếu lỗi camera thì chỉ log/cảnh báo

### 8.2 Cân lần 2

Điểm chèn:

- sau khi `CaptureSessionWeight2UseCase` lưu thành công

Luồng:

1. save nghiệp vụ cân lần 2
2. trigger chụp `CAM1`
3. trigger chụp `CAM2`
4. lưu 2 ảnh vào `weighing_session_images`
5. nếu lỗi camera thì chỉ log/cảnh báo

## 9. Hiệu năng và nguyên tắc chống lag

### 9.1 Preview

- chỉ preview `1 camera`
- giới hạn `8-12 fps`
- resize frame preview nhỏ
- không decode/render trên UI thread

### 9.2 Capture

- chụp nền async
- encode JPEG nền
- insert DB sau khi nghiệp vụ cân đã commit
- timeout ngắn, ví dụ `2-3 giây / camera`

### 9.3 Không làm ảnh hưởng luồng cân

- save cân không phụ thuộc cứng vào camera
- camera lỗi không được làm fail thao tác nghiệp vụ chính

## 10. Phần code dự kiến tác động

### 10.1 UI

- `src/StationApp.UI/Views/WeighingView.xaml`
- `src/StationApp.UI/ViewModels/WeighingViewModel.cs`
- `src/StationApp.UI/Views/Settings/SystemSettingsView.xaml`
- `src/StationApp.UI/ViewModels/Settings/SystemSettingsViewModel.cs`

### 10.2 Application

- use case lưu cân lần 1 / lần 2
- interface camera preview/capture
- use case save config camera

### 10.3 Infrastructure

- service RTSP preview
- service RTSP capture
- repository lưu ảnh
- EF config bảng ảnh
- schema bootstrapper / migrator

### 10.4 Domain

- entity `WeighingSessionImage`
- constant/key cấu hình camera

## 11. Đề xuất thứ tự triển khai

### Bước 1. Schema và cấu hình

- thêm key cấu hình camera
- thêm bảng `weighing_session_images`
- thêm entity + EF config + migrator/bootstrapper

### Bước 2. Service camera

- tạo preview service RTSP
- tạo capture service RTSP
- xử lý timeout, reconnect, status

### Bước 3. UI cấu hình hệ thống

- thêm màn nhập `RTSP URL`
- bật/tắt camera
- lưu config

### Bước 4. UI màn cân

- chỉnh layout hàng trên
- thêm panel preview
- thêm chọn `Camera 1 / Camera 2`
- bind trạng thái preview

### Bước 5. Hook nghiệp vụ cân

- chụp khi lưu cân lần 1
- chụp khi lưu cân lần 2
- lưu ảnh vào DB

### Bước 6. Test

- test cấu hình camera
- test preview đổi camera
- test lưu cân sinh ảnh
- test camera lỗi không fail nghiệp vụ cân

## 12. Rủi ro chính

- RTSP từ một số camera không ổn định hoặc codec khó decode
- WPF render preview nếu xử lý sai sẽ lag UI
- lưu ảnh BLOB đồng bộ có thể gây khựng nếu không tách nền
- camera có thể hỗ trợ main stream nặng, cần dùng substream nếu có

## 13. Tiêu chí nghiệm thu

- cấu hình được 2 camera RTSP
- preview realtime được 1 camera do người dùng chọn
- đổi camera preview hoạt động ổn định
- lưu cân lần 1 tạo được ảnh `CAM1` và `CAM2`
- lưu cân lần 2 tạo được ảnh `CAM1` và `CAM2`
- ảnh lưu đúng `WeighingSessionId` và `CaptureStage`
- camera lỗi không làm fail thao tác lưu cân

## 14. Điểm chưa quyết định

Các điểm này có thể cần chốt thêm trước khi code:

- có cần lưu thêm `thumbnail` hay không
- có cần màn xem lại ảnh theo lượt cân ngay trong phase 1 hay không
- có chụp song song 2 camera hay chụp tuần tự
- camera có hỗ trợ substream riêng hay chỉ 1 RTSP URL

