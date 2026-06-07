import os

srs_path = r'g:\Source-code\pmcan_C#\SRSdocs\StationApp_System_SRS.md'

with open(srs_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Locate Chapter 4 heading and Chapter 5 heading
start_target = "# 4. Đặc tả Chi tiết các Phân hệ và Thiết kế Giao diện (Module Specifications & UI Design)"
end_target = "# 5. Các Phụ lục (Appendices)"

start_index = content.find(start_target)
end_index = content.find(end_target)

if start_index == -1:
    print("Could not find Chapter 4 header.")
    exit(1)
if end_index == -1:
    print("Could not find Chapter 5 header.")
    exit(1)

prefix = content[:start_index]
suffix = content[end_index:]

chapter_4_content = """# 4. Đặc tả Chi tiết các Phân hệ và Thiết kế Giao diện (Module Specifications & UI Design)

Chương này đặc tả chi tiết giao diện người dùng (UI), các hình ảnh chụp thực tế từ ứng dụng trạm cân đang chạy, danh sách các phần tử giao diện (Control), bindings với ViewModel và các nghiệp vụ xử lý chi tiết (validation, event handler, truy vấn CSDL liên quan) cho từng phân hệ và màn hình con. Điều này giúp lập trình viên WPF và đội ngũ phát triển dễ dàng đối chiếu, triển khai mã nguồn và tái hiện ứng dụng chính xác 100%.

---

## 4.1 Phân hệ Đăng nhập và Trang chủ (Login & Dashboard Module)

### 4.1.1 Giao diện Đăng nhập (LoginWindow.xaml)

Màn hình xuất hiện khi khởi chạy ứng dụng trạm cân. Màn hình được thiết kế tối giản, tập trung vào ô nhập liệu chính, có màu sắc tương phản cao và hiển thị phiên bản ứng dụng rõ ràng ở góc dưới.

![Màn hình Đăng nhập](images/login.png)

#### Danh sách Phần tử Giao diện Chính (LoginWindow.xaml)
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-LGN-TXT-USER | `TextBox` | `Username` | Nhập tên đăng nhập của nhân viên/quản trị viên. |
| UI-LGN-TXT-PASS | `PasswordBox` | (Xử lý qua Code-behind / `Password`) | Nhập mật khẩu. |
| UI-LGN-BTN-SUBMIT | `Button` | `LoginCommand` | Nút đăng nhập, mặc định kích hoạt khi nhấn phím `Enter`. |
| UI-LGN-TXT-VER | `TextBlock` | `AppVersion` | Hiển thị phiên bản ứng dụng hiện tại (Ví dụ: `1.0.5`). |

#### Các Yêu cầu Chức năng Nghiệp vụ liên quan (FR-AUTH)
- **FR-AUTH-001 (Đăng nhập)**: Nhân viên cân nhập `Username` và `Password`. Hệ thống thực hiện băm mật khẩu và đối chiếu với bản ghi trong bảng `users` cục bộ.
- **FR-AUTH-002 (Phân quyền RBAC)**: Sau khi đăng nhập thành công, phiên làm việc sẽ lưu giữ vai trò người dùng trong `StationAuthorization`:
  - **ADMIN (Quản trị viên)**: Quyền truy cập đầy đủ, bao gồm cấu hình thiết bị cân cổng COM, cấu hình RTSP camera, cấu hình phôi in, sao lưu dữ liệu local, tạo và quản lý tài khoản người dùng, và thực hiện cân tay (MANUAL - tự nhập số cân).
  - **OPERATOR (Nhân viên cân)**: Chỉ được phép thực hiện cân tự động (AUTO - đọc từ cổng COM), gộp đơn, in phiếu cân/giao nhận và bypass dung sai hàng bao (được ghi lại nhật ký kiểm toán).

---

### 4.1.2 Giao diện Trang chủ/Dashboard (DashboardView.xaml)

Màn hình hiển thị ngay sau khi người dùng đăng nhập thành công. Đây là trung tâm giám sát nhanh tình trạng hoạt động của trạm cân, hiển thị sản lượng cân trong ngày và kết nối phần cứng.

![Màn hình Trang chủ - Dashboard](images/Trang_chu.png)

#### Danh sách Phần tử Giao diện Chính (DashboardView.xaml)
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-DSB-CARD-TOTAL | `Border/TextBlock` | `TotalVehiclesToday` | Số lượng lượt xe đã qua trạm cân trong ngày hiện tại. |
| UI-DSB-CARD-NET-IN | `Border/TextBlock` | `TotalInboundWeightToday` | Tổng sản lượng nhập hàng tịnh trong ngày (kg). |
| UI-DSB-CARD-NET-OUT | `Border/TextBlock` | `TotalOutboundWeightToday` | Tổng sản lượng xuất hàng tịnh trong ngày (kg). |
| UI-DSB-TXT-COM | `TextBlock` | `ScaleConnectionStatus` | Trạng thái kết nối đầu cân COM (Xanh: Connected / Đỏ: Disconnected). |
| UI-DSB-TXT-CAM1 | `TextBlock` | `Camera1ConnectionStatus` | Trạng thái hoạt động camera IP giám sát số 1. |
| UI-DSB-TXT-CAM2 | `TextBlock` | `Camera2ConnectionStatus` | Trạng thái hoạt động camera IP giám sát số 2. |
| UI-DSB-TXT-API | `TextBlock` | `CentralApiStatus` | Trạng thái kết nối API với Central Server trung tâm. |
| UI-DSB-TXT-BACKUP | `TextBlock` | `LastBackupTime` | Thời gian hoàn tất sao lưu cơ sở dữ liệu cục bộ gần nhất. |
| UI-DSB-TXT-SYNC | `TextBlock` | `OutboxPendingCount` | Số lượng bản ghi nghiệp vụ đang chờ đồng bộ trong Outbox. |

#### Các Yêu cầu Chức năng Nghiệp vụ liên quan (FR-DSB)
- **Tải số liệu thống kê**: DashboardViewModel truy vấn đếm số lượng bản ghi trong các bảng `weighing_sessions` có `CreatedAt` bằng ngày hiện tại.
- **Giám sát thiết bị thời gian thực**: Sử dụng dịch vụ `IScaleDevice` và `ICameraPreviewService` định kỳ ping/check trạng thái kết nối phần cứng. Cập nhật màu sắc chỉ báo (Xanh lá = OK, Cam = Đang kết nối, Đỏ = Lỗi).

---

## 4.2 Phân hệ Danh sách xe vào và Quản lý Đăng ký (Incoming Queue & Registration)

### 4.2.1 Giao diện Danh sách xe chờ (IncomingVehicleListView.xaml)

Hiển thị danh sách phương tiện đã được duyệt và đồng bộ từ ERP hoặc các đăng ký thủ công đang ở trong bãi trạm cân chờ thực hiện cân lần 1.

![Màn hình Danh sách xe vào](images/Danh_sach_xe_vao.png)

#### Danh sách Phần tử Giao diện Chính (IncomingVehicleListView.xaml)
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-INC-TXT-SEARCH | `TextBox` | `SearchQuery` | Ô tìm kiếm nhanh phương tiện theo biển số xe hoặc mã đơn hàng. |
| UI-INC-GRID-DATA | `DataGrid` | `ItemsSource={Binding PendingVehicles}` | Lưới hiển thị danh sách các phương tiện ở trạng thái chờ (`IN_YARD`). |
| UI-INC-BTN-MANUAL | `Button` | `CreateManualOrderCommand` | Nút mở hộp thoại tạo đơn cân lẻ/Inbound thủ công khi xe ngoài luồng ERP vào trạm. |
| UI-INC-BTN-START | `Button` | `StartDomesticWeighingCommand` | Chọn một dòng và nhấn nút để bắt đầu phiên cân nội địa tiêu chuẩn (chuyển sang màn hình `WeighingView`). |
| UI-INC-BTN-EXPORT | `Button` | `StartExportWeighingCommand` | Chọn đơn xuất khẩu lớn và nhấn để chuyển sang màn hình Cân xuất khẩu chuyên dụng (`ExportWeighingView`). |
| UI-INC-BTN-REFRESH | `Button` | `RefreshCommand` | Tải lại danh sách xe chờ từ database cục bộ. |

#### Các Yêu cầu Chức năng Nghiệp vụ liên quan (FR-WEIGH-001)
- **Tải danh sách xe chờ**: Đọc từ bảng `cut_orders` các bản ghi có `ProcessingStage = 'IN_YARD'` và `CutOrderStatus = 'REGISTERED'`.
- **Tạo đơn cân lẻ thủ công**: Nhân viên trạm cân bấm nút "Tạo Đơn Cân Lẻ" để nhập thông tin biển số xe, khách hàng, sản phẩm, trọng lượng dự kiến cho xe ngoài danh sách ERP (chủ yếu là xe nhập hàng Inbound). Dữ liệu này được lưu trực tiếp vào bảng `cut_orders` cục bộ với nguồn `CutOrderSource = 'MANUAL'`.
- **Bắt đầu phiên cân (Domestic/Export)**: Khi chọn xe và nhấn bắt đầu, hệ thống kiểm tra cờ `IsExportScale` để điều hướng chính xác sang màn hình cân tương ứng.

---

## 4.3 Phân hệ Quy trình Cân Nội địa Tiêu chuẩn (Standard Domestic Weighing Module)

### 4.3.1 Giao diện Vận hành Cân chính (WeighingView.xaml)

Màn hình làm việc chính của nhân viên cân khi thực hiện cân xe nội địa 2 lần (cân vào, cân ra), hiển thị video trực tiếp từ camera, số cân thời gian thực cỡ lớn và bảng lưới gộp/phân bổ đơn hàng.

![Màn hình Cân nội địa](images/Can_noi_dia.png)

#### Danh sách Phần tử Giao diện Chính (WeighingView.xaml)
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-WGH-TXT-LWEIGHT | `TextBox` | `CurrentWeight` | Hiển thị số cân từ thiết bị đọc. Chỉ Quản trị viên được nhập số thủ công khi chọn chế độ "Cân Tay". |
| UI-WGH-RDO-AUTO | `RadioButton` | `IsAutoMode` | Lựa chọn chế độ đọc cân tự động (AUTO) qua cổng COM của thiết bị cân. |
| UI-WGH-RDO-MANUAL | `RadioButton` | `IsManualMode` | Lựa chọn chế độ nhập số cân bằng tay (Chỉ khả dụng cho vai trò ADMIN). |
| UI-WGH-TXT-STABLE | `TextBlock` | `StabilityText` | Trạng thái ổn định số cân đọc về (`ỔN ĐỊNH` hoặc `DAO ĐỘNG`). |
| UI-WGH-IMG-CAM | `Image` | `CameraPreviewSource` | Hiển thị luồng RTSP thời gian thực để kiểm soát vị trí đỗ xe của xe. |
| UI-WGH-BTN-W1 | `Button` | `CaptureWeight1Command` | Lấy giá trị cân lần 1 khi xe cân vào trạm, cập nhật `Weight1`. |
| UI-WGH-BTN-W2 | `Button` | `CaptureWeight2Command` | Lấy giá trị cân lần 2 khi xe cân ra trạm, cập nhật `Weight2`. |
| UI-WGH-BTN-SAVE | `Button` | `SaveCapturedWeightCommand` | Ghi nhận số cân và chuyển trạng thái phiên cân. |
| UI-WGH-BTN-ALLOC | `Button` | `OpenAllocationCommand` | Mở màn hình/dialog phân bổ khối lượng tịnh cho các đơn hàng gộp trên xe. |
| UI-WGH-BTN-SPLIT | `Button` | `ShowOverweightHandlingCommand` | Mở hộp thoại xử lý quá tải tải trọng thiết kế của xe. |
| UI-WGH-BTN-INPC | `Button` | `PrintWeighTicketCommand` | In phiếu cân tổng hợp của phiên cân (chứng từ Master). |
| UI-WGH-BTN-INPGN | `Button` | `PrintDeliveryTicketCommand` | In các phiếu giao nhận chi tiết cho từng đơn hàng (chứng từ Line). |
| UI-WGH-BTN-OUT | `Button` | `MoveToOutYardCommand` | Hoàn tất phiên cân, đổi trạng thái đơn cắt lệnh thành `COMPLETED` và cho xe ra. |
| UI-WGH-GRID-LINES | `DataGrid` | `ItemsSource={Binding SessionLines}` | Lưới hiển thị chi tiết các đơn hàng được gộp trên chuyến xe hiện tại. |
| UI-WGH-GRID-SESS | `DataGrid` | `ItemsSource={Binding Sessions}` | Lưới danh sách các phiên cân dở dang đang hoạt động tại trạm. |
| UI-WGH-CHK-NOLOAD | `CheckBox` | `IsNoLoadMarked` | Đánh dấu phiên cân không tải (chỉ đi qua bàn cân để kiểm tra, không xuất/nhập hàng). |
| UI-WGH-CHK-ACTUAL | `CheckBox` | `UseActualWeightForBaggedCutOrders` | Cho phép sử dụng khối lượng tịnh thực tế cho đơn hàng bao thay vì quy đổi theo số lượng bao. |

#### Các Yêu cầu Chức năng Nghiệp vụ liên quan (FR-WEIGH-002, 003 & FR-ALLOC-001)
- **Cân lần 1 (Weight 1)**: Xe đỗ lên cân, hệ thống hiển thị camera trực tiếp để nhân viên cân kiểm tra đỗ xe đúng vị trí. Bấm "Lấy Cân 1" để ghi nhận khối lượng xe. Tự động chụp ảnh từ camera trạm `C2` (với đơn nội địa) và lưu vào bảng `weighing_session_images` cục bộ, đồng thời tạo phiên cân (`weighing_sessions`) và dòng phiếu cân Master (`weigh_tickets` với `RecordRole = MasterSession`) ở trạng thái `PENDING_WEIGHT2`.
- **Cân lần 2 & Tính khối lượng tịnh (Weight 2)**: Khi xe quay lại cân ra, nhân viên kiểm tra camera đỗ xe và bấm "Lấy Cân 2". Hệ thống tính toán: `NetWeight = |Weight1 - Weight2|`.
- **Đối chiếu Dung sai hàng bao**: Đối với sản phẩm hàng bao, hệ thống tự động kiểm tra xem khối lượng tịnh thực tế có vượt quá ngưỡng dung sai cho phép không (cấu hình trong `ToleranceKgPerBag` - ví dụ 0.5 kg/bao). Nếu vượt ngưỡng, hệ thống cảnh báo đỏ và khóa nút Lưu. Nhân viên cân phải trao đổi với bộ phận xuất hàng, nếu đồng ý thì nhấn nút bỏ qua trên hộp thoại (Bypass) mới được lưu cân lần 2. Hành động bypass này tự động ghi vào `audit_logs`.
- **Phân bổ Khối lượng**: Đối với xe cân gộp nhiều đơn hàng (nhiều dòng chi tiết):
  - Nhân viên cân bấm nút "Phân Bổ".
  - Hộp thoại hiện ra hiển thị các đơn hàng trên xe và cho phép nhập số lượng phân bổ (`ActualAllocatedWeight`).
  - **Ràng buộc**: Tổng khối lượng phân bổ của tất cả các đơn bắt buộc phải bằng chính xác khối lượng tịnh `NetWeight` của xe. Nếu là hàng bao, hệ thống tự động tính số bao quy đổi: `Round(ActualAllocatedWeight / 50)`.
  - Bấm "Xác nhận" để cập nhật bảng `weighing_session_lines` và sinh các phiếu cân con (`weigh_tickets` với `RecordRole = CutOrderDerived`) tương ứng.

---

### 4.3.2 Dialog Chọn đại diện xe (VehicleRepresentativeSelectionDialogWindow.xaml)

Hộp thoại hiển thị khi nhân viên cân thực hiện lưu số cân, yêu cầu lựa chọn hoặc nhập thông tin người đại diện/tài xế thực tế chịu trách nhiệm cho chuyến xe.

#### Danh sách Phần tử Giao diện Chính
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-VRS-GRID-DATA | `DataGrid` | `Representatives` | Hiển thị danh sách các tài xế/người đại diện của đơn vị vận tải. |
| UI-VRS-BTN-SELECT | `Button` | `SelectCommand` | Xác nhận chọn đại diện và đóng hộp thoại. |
| UI-VRS-BTN-CANCEL | `Button` | `CancelCommand` | Hủy bỏ và đóng hộp thoại. |

#### Nghiệp vụ xử lý liên quan
- Truy vấn danh sách đại diện từ bảng `vehicles` dựa trên biển số xe đang thực hiện cân. Ghi nhận người được chọn vào cột `DriverName` trong bảng `weighing_sessions` và `weigh_tickets`.

---

### 4.3.3 Dialog Cấu hình in phiếu (PrintOptionsDialogWindow.xaml)

Hộp thoại tùy chỉnh tham số trước khi in ấn phiếu cân tổng hợp hoặc phiếu giao nhận.

#### Danh sách Phần tử Giao diện Chính
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-PRN-COM-PRINTER | `ComboBox` | `SelectedPrinter` | Lựa chọn máy in vật lý kết nối với máy tính. |
| UI-PRN-COM-TEMPLATE | `ComboBox` | `SelectedTemplate` | Chọn mẫu phôi in ấn (Mẫu in mặc định hoặc mẫu in tùy chỉnh). |
| UI-PRN-TXT-COPIES | `TextBox` | `CopyCount` | Số lượng bản in cần kết xuất (mặc định: 3 bản). |
| UI-PRN-BTN-PRINT | `Button` | `PrintCommand` | Thực hiện in ấn ra máy in vật lý. |
| UI-PRN-BTN-CANCEL | `Button` | `CancelCommand` | Hủy in. |

#### Nghiệp vụ xử lý liên quan
- **Nạp cấu hình**: Truy vấn thông tin máy in và phôi in từ bảng `print_template_profiles`. Áp dụng các thông số dịch chuyển lề in `OffsetXmm` và `OffsetYmm` lên đối tượng in của WPF trước khi xuất lệnh in.

---

### 4.3.4 Dialog Lịch sử ảnh camera (CameraImageHistoryWindow.xaml)

Hộp thoại hiển thị các hình ảnh chụp nhanh phương tiện trên bàn cân từ camera giám sát trạm cân, làm bằng chứng chống gian lận tải trọng và kiểm soát đỗ xe.

#### Danh sách Phần tử Giao diện Chính
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-CAM-IMG-W1 | `Image` | `Weight1ImageSource` | Hiển thị hình ảnh xe chụp tại thời điểm lưu cân lần 1. |
| UI-CAM-IMG-W2 | `Image` | `Weight2ImageSource` | Hiển thị hình ảnh xe chụp tại thời điểm lưu cân lần 2. |
| UI-CAM-GRID-METADATA | `DataGrid` | `ImageMetadata` | Chi tiết thời gian chụp, mã camera, và tài khoản nhân viên lưu cân. |
| UI-CAM-BTN-CLOSE | `Button` | `CloseCommand` | Đóng hộp thoại. |

#### Nghiệp vụ xử lý liên quan
- **Truy vấn hình ảnh**: Đọc dữ liệu nhị phân (byte array) của ảnh từ bảng `weighing_session_images` liên kết theo `WeighingSessionId`. Chuyển đổi mảng byte thành `BitmapImage` hiển thị lên UI.

---

## 4.4 Phân hệ Quy trình Cân Xuất khẩu Đơn hàng Lớn (Export Scale Weighing Module)

### 4.4.1 Giao diện Cân Xuất khẩu (ExportWeighingView.xaml)

Màn hình chuyên dụng quản lý hợp đồng xuất khẩu lớn (ví dụ clinker hoặc xi măng rời hàng nghìn tấn), quản lý hàng chục lượt xe con ra vào lấy hàng cộng dồn lũy kế sản lượng.

![Màn hình Cân xuất khẩu](images/Can_xuat_khau.png)

#### Danh sách Phần tử Giao diện Chính (ExportWeighingView.xaml)
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-EXP-GRID-CO | `DataGrid` | `ItemsSource={Binding CutOrders}` | Bảng danh sách các đơn hàng xuất khẩu lớn đang hoạt động. |
| UI-EXP-GRID-TRIPS | `DataGrid` | `ItemsSource={Binding Trips}` | Lưới hiển thị danh sách toàn bộ các chuyến xe con đã và đang thực hiện của đơn hàng xuất khẩu được chọn. |
| UI-EXP-BTN-ADDTRIP | `Button` | `CreateTripCommand` | Khởi tạo một chuyến xe con mới cho đơn xuất khẩu đang chọn (gọi `CreateExportVehicleSessionUseCase`). |
| UI-EXP-BTN-TRANSFER | `Button` | `TransferTripCommand` | Mở dialog chuyển chuyến xe con từ đơn xuất khẩu nguồn sang đích (gọi `TransferExportVehicleTripUseCase`). |
| UI-EXP-BTN-FINALIZE | `Button` | `FinalizeCommand` | Nút bấm chốt sản lượng cho hợp đồng xuất khẩu lớn để kết thúc và giải phóng đơn hàng (`FinalizeExportCutOrderUseCase`). |
| UI-EXP-BTN-PRINT | `Button` | `PrintTicketCommand` | In chứng từ cho chuyến xe con đang chọn. |

#### Các Yêu cầu Chức năng Nghiệp vụ liên quan (FR-EXPORT-001)
- **Cân chuyến xe con**: Mỗi chuyến xe con thực hiện quy trình cân 2 lần y hệt như cân nội địa. Tuy nhiên, sau khi cân lần 2, hệ thống tự động cộng dồn `NetWeight` của chuyến xe con vào sản lượng lũy kế đã xuất của đơn hàng cha trong bảng `cut_orders`.
- **Chốt sản lượng**: Khi đơn xuất khẩu lớn đạt hoặc vượt khối lượng kế hoạch, hoặc khi tàu đã bốc xong hàng, cán bộ trạm cân nhấn nút "Chốt Tổng". Hệ thống khóa đơn cắt lệnh cha (`CutOrderStatus = 'COMPLETED'`), cập nhật tổng khối lượng chốt `ExportFinalizedWeight` và thời điểm chốt `ExportFinalizedAt`, sau đó đẩy bản ghi này vào Outbox để đồng bộ lên Central Server ERP.

---

### 4.4.2 Dialog Chuyển chuyến xe con (ExportTripTransferDialogWindow.xaml)

Sử dụng khi một chuyến xe con đã cân xong (hoặc đang cân) nhưng cần chuyển sản lượng sang một đơn cắt lệnh xuất khẩu lớn khác do thay đổi kế hoạch xuất hàng.

#### Danh sách Phần tử Giao diện Chính
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-TRF-TXT-SOURCE | `TextBlock` | `SourceSessionNo` | Hiển thị số chuyến xe con hiện tại cần chuyển. |
| UI-TRF-COM-TARGET | `ComboBox` | `TargetCutOrders` | Danh sách các đơn cắt lệnh xuất khẩu lớn đang hoạt động để chọn đơn đích. |
| UI-TRF-BTN-CONFIRM | `Button` | `ConfirmTransferCommand` | Xác nhận thực hiện chuyển chuyến xe và đóng hộp thoại. |
| UI-TRF-BTN-CANCEL | `Button` | `CancelCommand` | Hủy bỏ thao tác. |

#### Nghiệp vụ xử lý liên quan
- **Chuyển đổi dữ liệu**: Cập nhật cột `CutOrderId` của dòng chi tiết phiên cân (`weighing_session_lines`) và phiếu giao nhận (`delivery_tickets`) sang ID đơn cắt lệnh mới. Hệ thống tự động tính toán lại sản lượng lũy kế xuất khẩu cho cả 2 đơn cắt lệnh (đơn nguồn giảm, đơn đích tăng).

---

## 4.5 Phân hệ Danh sách xe ra và Lịch sử Phiếu cân (Outgoing Queue & Ticket History)

### 4.5.1 Giao diện Danh sách xe ra (OutgoingVehicleListView.xaml)

Quản lý danh sách các xe đã hoàn thành cân lần 2, cho phép in lại chứng từ phiếu cân/phiếu giao nhận và thực hiện xác nhận cho xe ra khỏi bãi cân.

![Màn hình Danh sách xe ra](images/Danh_sach_xe_ra.png)

#### Danh sách Phần tử Giao diện Chính (OutgoingVehicleListView.xaml)
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-OUT-GRID-DATA | `DataGrid` | `ItemsSource={Binding CompletedSessions}` | Lưới danh sách các phiên cân đã hoàn thành hoặc sẵn sàng hoàn thành. |
| UI-OUT-TXT-SEARCH | `TextBox` | `SearchQuery` | Ô nhập tìm kiếm nhanh biển số xe đã cân xong. |
| UI-OUT-BTN-PRINTWEIGH | `Button` | `ReprintWeighTicketCommand` | Thực hiện in lại Phiếu cân tổng hợp của phiên cân đang chọn. |
| UI-OUT-BTN-PRINTDELIV | `Button` | `ReprintDeliveryTicketCommand` | Thực hiện in lại Phiếu giao nhận chi tiết cho dòng đơn hàng đang chọn. |
| UI-OUT-BTN-OUT | `Button` | `MoveToOutYardCommand` | Hoàn tất phiên cân, đổi trạng thái và cho xe ra khỏi trạm. |

#### Các Yêu cầu Chức năng Nghiệp vụ liên quan (FR-PRINT-001)
- **Quy tắc In ấn Bắt buộc**: Hệ thống chỉ cho phép xe ra (`MoveToOutYardCommand`) khi và chỉ khi nhân viên cân đã thực hiện in thành công Phiếu cân tổng hợp chính (`HasPrintedMasterWeighTicket = true`) và toàn bộ các Phiếu giao nhận chi tiết của các dòng cắt lệnh liên kết (`HasPrintedDeliveryTicket = true`).
- **In lại chứng từ**: Vai trò nhân viên cân được phép in lại chứng từ nếu gặp sự cố kẹt giấy hoặc mờ mực. Tuy nhiên hành động này sẽ ghi lại sự kiện vào `audit_logs` để kiểm soát số lần in ấn và chống gian lận.

---

### 4.5.2 Giao diện Danh sách phiếu cân (TicketListView.xaml)

Giao diện tra cứu lịch sử toàn bộ các phiếu cân đã được phát hành tại trạm cân.

#### Danh sách Phần tử Giao diện Chính
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-TKT-TXT-SEARCH | `TextBox` | `SearchKeyword` | Tìm kiếm phiếu cân theo biển số xe hoặc số phiếu cân. |
| UI-TKT-GRID-DATA | `DataGrid` | `Tickets` | Bảng lưới danh sách tất cả các phiếu cân `weigh_tickets` cục bộ. |
| UI-TKT-BTN-SEARCH | `Button` | `SearchCommand` | Thực thi tìm kiếm theo bộ lọc. |
| UI-TKT-BTN-VIEW | `Button` | `ViewDetailsCommand` | Xem thông tin chi tiết và lịch sử in ấn của phiếu cân đang chọn. |

#### Nghiệp vụ xử lý liên quan
- Truy vấn từ bảng `weigh_tickets` cục bộ, lọc theo các trường `VehiclePlate`, `TicketNo` hoặc khoảng thời gian tạo phiếu. Hiển thị thông tin khối lượng cân vào/ra và trạng thái đồng bộ (`SyncStatus`).

---

## 4.6 Phân hệ Báo cáo Thống kê (Reporting Module)

### 4.6.1 Báo cáo tổng hợp cân nhập hàng (InboundSummaryReportView.xaml)

Báo cáo chi tiết sản lượng hàng hóa, nguyên vật liệu nhập vào nhà máy (Inbound) trong một khoảng thời gian được cấu hình.

![Báo cáo nhập hàng](images/Bao_cao_nhap.png)

#### Danh sách Phần tử Giao diện Chính (InboundSummaryReportView.xaml)
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-RPT-IN-DATE-FROM | `DatePicker` | `StartDate` | Lọc báo cáo từ ngày bắt đầu. |
| UI-RPT-IN-DATE-TO | `DatePicker` | `EndDate` | Lọc báo cáo đến ngày kết thúc. |
| UI-RPT-IN-GRID | `DataGrid` | `ItemsSource={Binding ReportData}` | Danh sách các phiếu cân nhập hàng và tổng cộng khối lượng tịnh. |
| UI-RPT-IN-BTN-EXCEL | `Button` | `ExportToExcelCommand` | Xuất dữ liệu báo cáo nhập hàng ra file Microsoft Excel (`.xlsx`). |

#### Nghiệp vụ xử lý liên quan
- **Truy vấn báo cáo**: Thực hiện câu lệnh SQL truy vấn bảng `weigh_tickets` có `TransactionType = 'INBOUND'` và `CreatedAt` nằm trong khoảng `[StartDate, EndDate]`, gom nhóm theo mã sản phẩm `ProductCode` để tính tổng sản lượng thực tế đã nhập.

---

### 4.6.2 Báo cáo tổng hợp cân xuất khẩu (ExportSummaryReportView.xaml)

Báo cáo tổng hợp sản lượng xuất khẩu xi măng, clinker (Outbound) chạy qua luồng cân xuất khẩu đơn hàng lớn.

![Báo cáo xuất khẩu](images/Bao_cao_xuat.png)

#### Danh sách Phần tử Giao diện Chính (ExportSummaryReportView.xaml)
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-RPT-EX-DATE-FROM | `DatePicker` | `StartDate` | Lọc báo cáo từ ngày. |
| UI-RPT-EX-DATE-TO | `DatePicker` | `EndDate` | Lọc báo cáo đến ngày. |
| UI-RPT-EX-GRID | `DataGrid` | `ItemsSource={Binding ReportData}` | Chi tiết sản lượng lũy kế đã chốt của từng đơn hàng xuất khẩu lớn. |
| UI-RPT-EX-BTN-EXCEL | `Button` | `ExportToExcelCommand` | Xuất dữ liệu báo cáo xuất khẩu ra file Excel. |

#### Nghiệp vụ xử lý liên quan
- **Truy vấn báo cáo**: Truy vấn từ bảng `cut_orders` có `IsExportScale = 1` và `ExportFinalizedAt` nằm trong khoảng thời gian được chọn, tổng hợp các chuyến xe con liên quan để kết xuất báo cáo sản lượng chi tiết.

---

## 4.7 Phân hệ Cấu hình Hệ thống (System Configuration Module - SettingsView.xaml)

Giao diện cấu hình toàn bộ hệ thống trạm cân, được tổ chức thành các Tab con để Quản trị viên (ADMIN) thiết lập các thông số.

### 4.7.1 Cấu hình thiết bị cân (ScaleDeviceConfigView.xaml)

Tab cấu hình cổng nối tiếp kết nối với đầu cân (Scale Indicator).

![Cấu hình cân](images/Cau_hinh_can.png)

#### Danh sách Phần tử Giao diện Chính
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-CFG-SCL-PORT | `ComboBox` | `SelectedPortName` | Danh sách cổng COM vật lý khả dụng trên máy tính (COM1, COM2...). |
| UI-CFG-SCL-BAUD | `ComboBox` | `SelectedBaudRate` | Tốc độ truyền dữ liệu của cổng COM (ví dụ: 9600, 4800). |
| UI-CFG-SCL-PARSER | `ComboBox` | `SelectedParserType` | Thuật toán giải mã khung dữ liệu cân truyền về (ví dụ: `YaohuaWeightFrameParser`). |
| UI-CFG-SCL-BTN-SAVE | `Button` | `SaveCommand` | Lưu cấu hình cổng COM và khởi động lại cổng kết nối. |

#### Nghiệp vụ xử lý liên quan
- Lưu trữ cấu hình vào bảng `app_config`. Khi lưu thành công, hệ thống tự động tắt và khởi tạo lại đối tượng `SerialPort` tương ứng để nhận số cân tức thì.

---

### 4.7.2 Cấu hình camera IP RTSP (CameraConfigView.xaml)

Tab thiết lập thông tin kết nối RTSP của camera IP dùng để giám sát vị trí đỗ xe trên bàn cân và chụp ảnh biển số.

![Cấu hình camera](images/Cau_hinh_camera.png)

#### Danh sách Phần tử Giao diện Chính
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-CFG-CAM-CODE | `ComboBox` | `SelectedCameraCode` | Chọn mã định danh camera (CAM1 - Đầu xe / CAM2 - Đuôi xe). |
| UI-CFG-CAM-URL | `TextBox` | `RtspUrl` | Nhập đường dẫn RTSP phát luồng video (ví dụ: `rtsp://admin:admin123@192.168.1.100:554/h264`). |
| UI-CFG-CAM-BTN-TEST | `Button` | `TestConnectionCommand` | Thử kết nối camera và hiển thị luồng video preview nhanh. |
| UI-CFG-CAM-BTN-SAVE | `Button` | `SaveCommand` | Lưu cấu hình camera. |

#### Nghiệp vụ xử lý liên quan
- Ghi nhận thông số kết nối vào bảng `app_config` cục bộ dưới các key `Camera1RtspUrl`, `Camera2RtspUrl`. Tự động khởi tạo kết nối RTSP và chụp ảnh kiểm tra.

---

### 4.7.3 Cấu hình phôi in ấn (PrintConfigView.xaml)

Tab thiết lập các mẫu phôi in ấn chứng từ (Phiếu cân, Phiếu giao nhận) và căn chỉnh độ lệch lề in.

![Cấu hình in](images/Cau_hinh_in.png)

Để tinh chỉnh vị trí in ấn chính xác cho từng loại biểu mẫu được thiết kế sẵn của nhà máy, Quản trị viên sử dụng giao diện căn chỉnh tọa độ in ấn:

![Canh chỉnh vị trí in](images/Cau_hinh_in(canh_chinh_vi_tri_in).png)

#### Danh sách Phần tử Giao diện Chính
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-CFG-PRN-TEMPLATE | `ComboBox` | `SelectedTemplateProfile` | Lựa chọn mẫu phôi in ấn cần căn chỉnh. |
| UI-CFG-PRN-OFFSETX | `TextBox` | `OffsetXmm` | Nhập độ lệch lề in theo chiều ngang (mm). |
| UI-CFG-PRN-OFFSETY | `TextBox` | `OffsetYmm` | Nhập độ lệch lề in theo chiều dọc (mm). |
| UI-CFG-PRN-BTN-SAVE | `Button` | `SaveCommand` | Lưu cấu hình phôi in và máy in mặc định. |

#### Nghiệp vụ xử lý liên quan
- Cập nhật trực tiếp vào bảng `print_template_profiles`. Các tham số offset này được lưu dưới dạng số thực (`decimal`). Khi in ấn, WPF Print Engine sẽ cộng thêm tọa độ offset này vào canvas trước khi vẽ nội dung phiếu.

---

### 4.7.4 Quản lý tài khoản người dùng (AccountManagementView.xaml)

Tab quản trị danh sách người dùng và cấp phát tài khoản vận hành trạm cân.

![Quản lý tài khoản](images/Quan_ly_tai_khoan.png)

#### Danh sách Phần tử Giao diện Chính
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-CFG-ACC-GRID | `DataGrid` | `ItemsSource={Binding Users}` | Lưới hiển thị danh sách người dùng trạm cân. |
| UI-CFG-ACC-BTN-ADD | `Button` | `AddUserCommand` | Mở hộp thoại thêm tài khoản người dùng mới. |
| UI-CFG-ACC-BTN-EDIT | `Button` | `EditUserCommand` | Chỉnh sửa thông tin tài khoản đang chọn. |
| UI-CFG-ACC-BTN-RESETPASS | `Button` | `ResetPasswordCommand` | Mở dialog reset mật khẩu người dùng (`ResetPasswordDialogWindow.xaml`). |

#### Nghiệp vụ xử lý liên quan
- **Reset mật khẩu**: Quản trị viên nhập mật khẩu mới. Hệ thống thực hiện băm chuỗi mật khẩu mới bằng BCrypt và cập nhật cột `PasswordHash` trong bảng `users` cục bộ.

---

### 4.7.5 Tham số hệ thống và Sao lưu dữ liệu (SystemSettingsView.xaml)

Tab cấu hình các tham số hệ thống chung và cấu hình thư mục sao lưu cơ sở dữ liệu cục bộ.

![Tham số hệ thống](images/Tham_so_he_thong.png)

#### Danh sách Phần tử Giao diện Chính
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-CFG-SYS-BKDIR | `TextBox` | `BackupDirectory` | Thư mục lưu trữ tệp tin sao lưu (ví dụ `D:\PMCAN_backup`). |
| UI-CFG-SYS-BTN-BKNOW | `Button` | `RunBackupNowCommand` | Thực hiện sao lưu cơ sở dữ liệu cục bộ SQL Server ngay lập tức. |
| UI-CFG-SYS-BTN-RESTORE | `Button` | `RestoreDatabaseCommand` | Phục hồi cơ sở dữ liệu cục bộ từ tệp tin `.bak` được chọn. |

#### Nghiệp vụ xử lý liên quan (FR-BACKUP-001)
- **Tác vụ Tự động Sao lưu**: Tác vụ chạy ngầm `LocalDatabaseBackupWorker` chạy định kỳ. Vào đúng **03:00 AM** mỗi ngày, worker gọi câu lệnh backup SQL Server cục bộ lưu vào thư mục cấu hình dưới định dạng tệp `yyyyMMdd_{DatabaseName}.bak`.
- **Dọn dẹp tệp tin cũ (Retention)**: Sau khi hoàn thành sao lưu, worker quét thư mục và tự động xóa các tệp `.bak` cũ hơn **10 ngày** để giải phóng dung lượng bộ nhớ.

---

### 4.7.6 Trạng thái đồng bộ hàng chờ Outbox (SyncInfoView.xaml)

Tab theo dõi hàng đợi đồng bộ dữ liệu cục bộ lên Central Server ERP.

![Cấu hình đồng bộ](images/Cau_hinh_dong_bo.png)

#### Danh sách Phần tử Giao diện Chính
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-CFG-SYN-GRID | `DataGrid` | `ItemsSource={Binding OutboxItems}` | Lưới danh sách các bản ghi thay đổi nghiệp vụ đang xếp hàng chờ đồng bộ. |
| UI-CFG-SYN-BTN-SYNC | `Button` | `ForceSyncCommand` | Buộc hệ thống kích hoạt gửi lại toàn bộ các bản ghi đang chờ hoặc bị lỗi đồng bộ ngay lập tức. |

#### Nghiệp vụ xử lý liên quan
- **Truy vấn Outbox**: Đọc trực tiếp từ bảng `sync_outbox`. Cho phép người dùng theo dõi số lượng bản ghi chưa đồng bộ và nội dung chi tiết của payload JSON gửi đi.

---

### 4.7.7 Danh mục sản phẩm (ProductMasterView.xaml)

Tab quản lý danh mục sản phẩm, vật tư bốc xếp tại trạm cân.

![Danh mục sản phẩm](images/Danh_muc_San_pham.png)

#### Danh sách Phần tử Giao diện Chính
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-MST-PRD-GRID | `DataGrid` | `ItemsSource={Binding Products}` | Lưới danh sách danh mục sản phẩm cục bộ. |
| UI-MST-PRD-TXT-CODE | `TextBox` | `ProductCode` | Mã sản phẩm vật tư. |
| UI-MST-PRD-TXT-NAME | `TextBox` | `ProductName` | Tên sản phẩm vật tư. |
| UI-MST-PRD-BTN-SAVE | `Button` | `SaveCommand` | Lưu thông tin sản phẩm vật tư. |

#### Nghiệp vụ xử lý liên quan
- Thêm hoặc cập nhật dữ liệu trực tiếp trong bảng `products` cục bộ. Thông thường danh mục này được đồng bộ tự động từ ERP.

---

### 4.7.8 Danh mục khách hàng (CustomerMasterView.xaml)

Tab tra cứu danh mục khách hàng, nhà phân phối.

![Danh mục khách hàng](images/Danh_muc_Khach_hang.png)

#### Danh sách Phần tử Giao diện Chính
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-MST-CST-GRID | `DataGrid` | `ItemsSource={Binding Customers}` | Lưới hiển thị danh mục khách hàng (`customers`). |

#### Nghiệp vụ xử lý liên quan
- Truy xuất thông tin khách hàng từ bảng `customers` cục bộ để làm dữ liệu gợi ý/autocomplete khi tạo đơn cân lẻ thủ công.

---

### 4.7.9 Danh mục phương tiện (VehicleMasterView.xaml)

Tab quản lý danh mục phương tiện vận chuyển, rơ mooc và tải trọng đăng kiểm cho phép (TTCP).

![Danh mục xe](images/Danh_muc_xe.png)

#### Danh sách Phần tử Giao diện Chính
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-MST-VEH-GRID | `DataGrid` | `ItemsSource={Binding Vehicles}` | Lưới danh sách xe, mooc, tải trọng cho phép (TTCP) và số đăng kiểm tương ứng. |
| UI-MST-VEH-BTN-SAVE | `Button` | `SaveCommand` | Lưu thông tin phương tiện. |

#### Nghiệp vụ xử lý liên quan
- Lưu trữ dữ liệu xe vào bảng `vehicles` cục bộ. Dữ liệu này được đối chiếu tự động khi cân lần 2 để kiểm soát và cảnh báo xe quá tải trọng đăng kiểm.

---

## 4.8 Phân hệ Chẩn đoán và Cập nhật (Diagnostics & Update Module)

### 4.8.1 Chẩn đoán phần cứng (DiagnosticsView.xaml)

Giao diện kỹ thuật phục vụ Admin kiểm tra tín hiệu thô nhận về từ các thiết bị ngoại vi của trạm cân.

#### Danh sách Phần tử Giao diện Chính
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-DIA-TXT-RAWLOG | `TextBox` | `RawSerialDataLog` | Hiển thị log chuỗi dữ liệu thô (raw string) nhận về từ cổng COM thiết bị cân. |
| UI-DIA-BTN-TESTCOM | `Button` | `TestComPortCommand` | Bắt đầu mở và kiểm tra dữ liệu truyền cổng COM. |
| UI-DIA-BTN-TESTCAM | `Button` | `TestCameraCommand` | Thực hiện kết nối và chụp ảnh thử từ camera. |

#### Nghiệp vụ xử lý liên quan
- Cho phép kỹ thuật viên phân tích cấu trúc khung truyền cân để xác định tính ổn định của thiết bị.

---

### 4.8.2 Cập nhật phiên bản ứng dụng (AppUpdateView.xaml)

Màn hình kiểm tra và cài đặt phiên bản nâng cấp của phần mềm trạm cân.

![Cập nhật ứng dụng](images/Cap_nhat_ung_dung.png)

#### Danh sách Phần tử Giao diện Chính
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-UPD-TXT-CURVER | `TextBlock` | `CurrentVersion` | Phiên bản hiện tại của ứng dụng đang cài trên máy trạm. |
| UI-UPD-TXT-NEWVER | `TextBlock` | `LatestVersion` | Phiên bản mới nhất khả dụng trên Central Server. |
| UI-UPD-BTN-UPGRADE | `Button` | `DownloadAndUpgradeCommand` | Bắt đầu tải tệp tin nâng cấp và tiến hành chạy bộ cài đặt tự động. |

#### Nghiệp vụ xử lý liên quan
- Ứng dụng kết nối tới API `/api/app/check-update` trên máy chủ trung tâm. Nếu có phiên bản mới, hệ thống tải xuống gói cài đặt ZIP, giải nén và kích hoạt file thực thi `StationApp.Updater.exe` để thay thế ứng dụng UI cũ.

"""

content_new = prefix + chapter_4_content + suffix

with open(srs_path, 'w', encoding='utf-8') as f:
    f.write(content_new)

print("Chapter 4 of SRS successfully updated with real screens and detailed descriptions!")
