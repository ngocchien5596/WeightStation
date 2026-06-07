import os
import re

srs_path = r'g:\Source-code\pmcan_C#\SRSdocs\StationApp_System_SRS.md'

with open(srs_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Locate Chapter 4: Specific Requirements
start_target = "# 4. Yêu cầu Chi tiết (Specific Requirements)"
end_target = "# 5. Các Phụ lục (Appendices)"

start_index = content.find(start_target)
end_index = content.find(end_target)

if start_index != -1 and end_index != -1:
    content_base_before = content[:start_index]
    content_base_after = content[end_index:]
    
    new_chapter_4 = """# 4. Đặc tả Chi tiết các Phân hệ và Thiết kế Giao diện (Module Specifications & UI Design)

Chương này đặc tả chi tiết giao diện người dùng (UI), các phần tử UI tương ứng và các kịch bản xử lý nghiệp vụ cho từng phân hệ (Module) của ứng dụng trạm cân. Điều này giúp lập trình viên WPF và đội ngũ phát triển dễ dàng liên kết từ bản vẽ giao diện đến logic code của ViewModel và các bảng Cơ sở dữ liệu tương ứng.

---

## 4.1 Phân hệ 1: Đăng nhập và Quản lý Vai trò (Login & RBAC Module)

### 4.1.1 Bố cục Giao diện Đăng nhập (LoginWindow.xaml)
Màn hình xuất hiện khi khởi chạy ứng dụng trạm cân. Màn hình được thiết kế tối giản, tập trung vào ô nhập liệu chính, có màu sắc tương phản cao và hiển thị phiên bản ứng dụng rõ ràng ở góc dưới.

```
+-------------------------------------------------------+
|                HỆ THỐNG TRẠM CÂN                      |
|                  WEIGHTSTATION                        |
|                                                       |
|   Tài khoản: [____________________________________]   |
|   Mật khẩu:  [************************************]   |
|                                                       |
|                  [  Đăng Nhập  ]                      |
|                                                       |
| Phiên bản: 1.0.5                   Trạng thái: Local  |
+-------------------------------------------------------+
```

### 4.1.2 Danh sách Phần tử Giao diện Chính (LoginWindow.xaml)
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-LGN-TXT-USER | `TextBox` | `Username` | Nhập tên đăng nhập của nhân viên/quản trị viên. |
| UI-LGN-TXT-PASS | `PasswordBox` | (Xử lý qua mã code-behind) | Nhập mật khẩu. |
| UI-LGN-BTN-SUBMIT | `Button` | `LoginCommand` | Nút đăng nhập, mặc định kích hoạt khi nhấn phím `Enter`. |
| UI-LGN-TXT-VER | `TextBlock` | `AppVersion` | Hiển thị phiên bản ứng dụng hiện tại (Ví dụ: `1.0.5`). |

### 4.1.3 Các Yêu cầu Chức năng Nghiệp vụ liên quan (FR-AUTH)
- **FR-AUTH-001 (Đăng nhập)**: Nhân viên cân nhập `Username` và `Password`. Hệ thống thực hiện băm mật khẩu và đối chiếu với bản ghi trong bảng `users` cục bộ.
- **FR-AUTH-002 (Phân quyền RBAC)**: Sau khi đăng nhập thành công, phiên làm việc sẽ lưu giữ vai trò người dùng trong `StationAuthorization`:
  - **ADMIN (Quản trị viên)**: Quyền truy cập đầy đủ, bao gồm cấu hình thiết bị cân cổng COM, cấu hình RTSP camera, cấu hình phôi in, sao lưu dữ liệu local, tạo và quản lý tài khoản người dùng, và thực hiện cân tay (MANUAL - tự nhập số cân).
  - **OPERATOR (Nhân viên cân)**: Chỉ được phép thực hiện cân tự động (AUTO - đọc từ cổng COM), gộp đơn, in phiếu cân/giao nhận và bypass dung sai hàng bao (được ghi lại nhật ký kiểm toán).

---

## 4.2 Phân hệ 2: Danh sách Xe vào và Quản lý Đăng ký (Incoming Queue & Registration)

### 4.2.1 Bố cục Giao diện Danh sách xe chờ (IncomingVehicleListView.xaml)
Hiển thị danh sách phương tiện đã được duyệt và đồng bộ từ ERP hoặc các đăng ký thủ công đang ở trong bãi trạm cân chờ thực hiện cân lần 1.

```
+--------------------------------------------------------------------------------------------------+
| DANH SÁCH PHƯƠNG TIỆN CHỜ CÂN                                                    [Tạo Đơn Cân Lẻ] |
| Tìm kiếm: [ Biển số xe / Mã đơn...         ]                                      [Làm Mới Danh Sách] |
+--------------------------------------------------------------------------------------------------+
| STT | Mã Cắt Lệnh | Biển Số Xe | Số Mooc  | Tài Xế     | Khách Hàng | Sản Phẩm     | Khối Lượng Kế Hoạch |
|-----|-------------|------------|----------|------------|------------|--------------|---------------------|
| 1   | CO2600123   | 29C-123.45 | 29R-0012 | Nguyễn Văn | Xi măng A  | Xi măng bao  | 40,000 kg           |
| 2   | CO2600124   | 30E-987.65 |          | Trần Văn B | Công ty B  | Xi măng rời  | 60,000 kg           |
+--------------------------------------------------------------------------------------------------+
| [ Thực Hiện Phiên Cân ]                                               [ Cân Xuất Khẩu Đơn Lớn ] |
+--------------------------------------------------------------------------------------------------+
```

### 4.2.2 Danh sách Phần tử Giao diện Chính (IncomingVehicleListView.xaml)
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-INC-TXT-SEARCH | `TextBox` | `SearchQuery` | Ô tìm kiếm nhanh phương tiện theo biển số xe hoặc mã đơn hàng. |
| UI-INC-GRID-DATA | `DataGrid` | `ItemsSource={Binding PendingVehicles}` | Lưới hiển thị danh sách các phương tiện ở trạng thái chờ (`IN_YARD`). |
| UI-INC-BTN-MANUAL | `Button` | `CreateManualOrderCommand` | Nút mở hộp thoại tạo đơn cân lẻ/Inbound thủ công khi xe ngoài luồng ERP vào trạm. |
| UI-INC-BTN-START | `Button` | `StartDomesticWeighingCommand` | Chọn một dòng và nhấn nút để bắt đầu phiên cân nội địa tiêu chuẩn (chuyển sang màn hình `WeighingView`). |
| UI-INC-BTN-EXPORT | `Button` | `StartExportWeighingCommand` | Chọn đơn xuất khẩu lớn và nhấn để chuyển sang màn hình Cân xuất khẩu chuyên dụng (`ExportWeighingView`). |

### 4.2.3 Các Yêu cầu Chức năng Nghiệp vụ liên quan (FR-WEIGH-001)
- **Tạo Phiên cân mới**: Khi chọn một hoặc nhiều đăng ký đơn cắt lệnh cùng biển số xe trong hàng đợi và bấm "Thực hiện phiên cân", hệ thống sẽ khởi tạo một phiên cân (`weighing_session`) mới ở trạng thái `PENDING_WEIGHT1`, gán mã phiên `SessionNo` và liên kết biển số xe.
- **Tạo Đơn thủ công**: Nhân viên trạm cân bấm nút "Tạo Đơn Cân Lẻ" để nhập thông tin biển số xe, khách hàng, sản phẩm, trọng lượng dự kiến cho xe ngoài danh sách ERP (chủ yếu là xe nhập hàng Inbound). Dữ liệu này được lưu trực tiếp vào bảng `cut_orders` cục bộ với nguồn `CutOrderSource = 'MANUAL'`.

---

## 4.3 Phân hệ 3: Quy trình Cân Nội địa Tiêu chuẩn (Standard Domestic Weighing Module)

### 4.3.1 Bố cục Giao diện Vận hành Cân (WeighingView.xaml)
Màn hình làm việc chính của nhân viên cân khi thực hiện cân xe nội địa 2 lần (cân vào, cân ra), hiển thị video trực tiếp từ camera, số cân thời gian thực cỡ lớn và bảng lưới gộp/phân bổ đơn hàng.

```
+--------------------------------------------------------------------------------------------------+
| Biển số xe: [ 29C-123.45   ]   Mã cắt lệnh: [ CO2600123    ]   Mã phiên cân: [ W260607-001 ] [Làm Mới] |
+--------------------------------------------------------------------------------------------------+
| THÔNG TIN PHIÊN CÂN               | CAMERA TRỰC TIẾP               | SỐ CÂN THỜI GIAN THỰC (kg)   |
| Số xe:  29C-123.45                |                                | +--------------------------+ |
| Rơmooc: 29R-0012                  | +----------------------------+ | |         45,280           | |
| Tài xế: Nguyễn Văn A              | |                            | | +--------------------------+ |
| TTCP:   44,000 kg                 | |   [ Luồng Camera RTSP C2 ] | | Chế độ: (o) Tự động ( ) Tay| |
| Khách:  Công ty Xi măng A         | |   Giám sát đỗ xe trên bàn   | | Trạng thái: [  ỔN ĐỊNH  ]  | |
| Mã SP:  XM_BAO_50                 | |   cân trước khi ghi số     | | Cân lần 1: [ 15,120 kg   ] | |
| Tên SP: Xi măng bao 50kg          | |                            | | Cân lần 2: [ 45,280 kg   ] | |
| [ ] Không lấy đủ số lượng bao     | +----------------------------+ | Net Weight: [ 30,160 kg  ]  |
| [ ] Đơn cân không tải (No Load)   | Cấu hình Preview: [Camera C2 ] | [Lấy Cân 1] [Lấy Cân 2] [LƯU] |
+--------------------------------------------------------------------------------------------------+
| [Gán Cắt Lệnh] [Phân Bổ Trọng Lượng] [Xử Lý Quá Tải] [IN PHIẾU CÂN] [IN PHIẾU GIAO NHẬN] [CHO XE RA] |
+--------------------------------------------------------------------------------------------------+
| CHI TIẾT ĐƠN CẮT LỆNH GỘP TRÊN XE (Weighing Session Lines)                                       |
| STT | Mã Cắt Lệnh | Khách Hàng | Sản Phẩm     | SL Đặt (kg) | SL Bao Đặt | SL Cân Thực | SL Bao Thực |
|-----|-------------|------------|--------------|-------------|------------|-------------|-------------|
| 1   | CO2600123   | Xi măng A  | Xi măng bao  | 15,000      | 300        | 15,080      | 302         |
| 2   | CO2600128   | Nhà PP B   | Xi măng bao  | 15,000      | 300        | 15,080      | 302         |
+--------------------------------------------------------------------------------------------------+
| DANH SÁCH CÁC PHIÊN CÂN ĐANG HOẠT ĐỘNG (Active Sessions)                                         |
| Số Phiên    | Biển Số Xe | Số Mooc  | Trạng Thái        | Cân Lần 1 | Cân Lần 2 | Khối Lượng Tịnh (Net) |
|-------------|------------|----------|-------------------|-----------|-----------|-----------------------|
| W260607-001 | 29C-123.45 | 29R-0012 | ALLOCATION_PENDING| 15,120    | 45,280    | 30,160 kg             |
+--------------------------------------------------------------------------------------------------+
```

### 4.3.2 Danh sách Phần tử Giao diện Chính (WeighingView.xaml)
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-WGH-TXT-LWEIGHT | `TextBox` | `CurrentWeight` | Hiển thị số cân từ thiết bị đọc. Cỡ chữ 42, font Consolas. Chỉ Quản trị viên được nhập số thủ công khi chọn chế độ "Cân Tay". |
| UI-WGH-RDO-AUTO | `RadioButton` | `IsAutoMode` | Lựa chọn chế độ đọc cân tự động (AUTO) qua cổng COM của thiết bị cân. |
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

### 4.3.3 Các Yêu cầu Chức năng Nghiệp vụ liên quan (FR-WEIGH-002, 003 & FR-ALLOC-001)
- **Cân lần 1 (Weight 1)**: Xe đỗ lên cân, hệ thống hiển thị camera trực tiếp để nhân viên cân kiểm tra đỗ xe đúng vị trí. Bấm "Lấy Cân 1" để ghi nhận khối lượng xe. Tự động chụp ảnh từ camera trạm `C2` (với đơn nội địa) và lưu trữ cục bộ, đồng thời sinh dòng phiếu cân Master (`weigh_tickets` có `RecordRole = MasterSession`) ở trạng thái `PENDING_WEIGHT2`.
- **Cân lần 2 & Tính khối lượng tịnh (Weight 2)**: Khi xe quay lại cân ra, nhân viên kiểm tra camera đỗ xe và bấm "Lấy Cân 2". Hệ thống tính toán: `NetWeight = |Weight1 - Weight2|`.
- **Đối chiếu Dung sai hàng bao**: Đối với sản phẩm hàng bao, hệ thống tự động kiểm tra xem khối lượng tịnh thực tế có vượt quá ngưỡng dung sai cho phép không (cấu hình trong `ToleranceKgPerBag` - ví dụ 0.5 kg/bao). Nếu vượt ngưỡng, hệ thống cảnh báo đỏ và khóa nút Lưu. Nhân viên cân phải trao đổi với bộ phận xuất hàng, nếu đồng ý thì nhấn nút bỏ qua trên hộp thoại (Bypass) mới được lưu cân lần 2. Hành động bypass này tự động ghi vào `audit_logs`.
- **Phân bổ Khối lượng**: Đối với xe cân gộp nhiều đơn hàng (nhiều dòng chi tiết):
  - Nhân viên cân bấm nút "Phân Bổ".
  - Một hộp thoại hiện ra, hiển thị các đơn hàng trên xe và cho phép nhập số lượng phân bổ (`ActualAllocatedWeight`).
  - **Ràng buộc**: Tổng khối lượng phân bổ của tất cả các đơn bắt buộc phải bằng chính xác khối lượng tịnh `NetWeight` của xe. Nếu là hàng bao, hệ thống tự động tính số bao quy đổi: `Round(ActualAllocatedWeight / 50)`.
  - Bấm "Xác nhận" để sinh các chứng từ con tương ứng để phục vụ in ấn.

---

## 4.4 Phân hệ 4: Quy trình Cân Xuất khẩu Đơn hàng Lớn (Export Scale Weighing Module)

### 4.4.1 Bố cục Giao diện Cân Xuất khẩu (ExportWeighingView.xaml)
Màn hình chuyên dụng quản lý hợp đồng xuất khẩu lớn (ví dụ clinker hoặc xi măng rời hàng nghìn tấn), quản lý hàng chục lượt xe con ra vào lấy hàng cộng dồn lũy kế sản lượng.

```
+--------------------------------------------------------------------------------------------------+
| Đơn hàng xuất khẩu: [ CO2600888   ]  Tìm xe: [ 29C-555.55    ]                             [Làm Mới] |
+--------------------------------------------------------------------------------------------------+
| THÔNG TIN ĐƠN XUẤT KHẨU CHA       | CAMERA TRỰC TIẾP               | PHIÊN CÂN CHUYẾN XE CON      |
| Mã đơn:  CO2600888                |                                | Biển số xe:  [ 29C-555.55 ]  |
| Hàng hóa: Clinker rời             | +----------------------------+ | Số Mooc:     [ 29R-0099   ]  |
| Khách:   Cảng quốc tế B           | |                            | | Tài xế:      [ Vũ Văn C   ]  |
| Đặt hàng: 5,000,000 kg            | |   [ Luồng Camera RTSP C6 ] | | TTCP xe:     40,000 kg       |
| Lũy kế:   1,200,000 kg            | |   Giám sát vị trí đỗ xe    | | Số cân:      [ 41,200 kg  ]  |
| Còn lại:  3,800,000 kg            | |                            | |                              |
| Số chuyến: 35 chuyến đã cân       | +----------------------------+ | Cân lần 1:   15,200 kg       |
| Ghi chú:  Hợp đồng tàu biển       | Cấu hình Preview: [Camera C6 ] | Net Weight:  26,000 kg       |
|                                   |                                | [Lấy Cân 1] [Lấy Cân 2] [LƯU] |
+--------------------------------------------------------------------------------------------------+
| [ Tạo Chuyến Xe ]     [ Chuyển Chuyến Xe ]     [ In Phiếu Cân ]     [ In Phiếu Giao Nhận ] [CHỐT TỔNG] |
+--------------------------------------------------------------------------------------------------+
| DANH SÁCH ĐƠN HÀNG XUẤT KHẨU HOẠT ĐỘNG                                                           |
| Mã Đơn     | Biển Số Xe Gốc | Khách Hàng     | Sản Phẩm | SL Đặt (kg) | Lũy Kế (kg)  | Còn Lại (kg)  |
|------------|----------------|----------------|----------|-------------|--------------|---------------|
| CO2600888  | 29C-999.99     | Cảng quốc tế B | Clinker  | 5,000,000   | 1,200,000    | 3,800,000     |
+--------------------------------------------------------------------------------------------------+
| CHI TIẾT CÁC CHUYẾN XE CON ĐÃ THỰC HIỆN                                                          |
| Mã Phiên Cân| Biển Số Xe | Số Mooc  | Cân Lần 1 | Cân Lần 2 | Net Weight | Phân Bổ (kg) | Trạng Thái    |
|-------------|------------|----------|-----------|-----------|------------|--------------|---------------|
| WEXP0012-01 | 29C-555.55 | 29R-0099 | 15,200    | 41,200    | 26,000     | 26,000       | COMPLETED     |
| WEXP0012-02 | 30E-666.66 |          | 16,100    | 42,300    | 26,200     | 26,200       | COMPLETED     |
+--------------------------------------------------------------------------------------------------+
```

### 4.4.2 Danh sách Phần tử Giao diện Chính (ExportWeighingView.xaml)
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-EXP-BTN-ADDTRIP | `Button` | `CreateTripCommand` | Khởi tạo một chuyến xe con mới cho đơn xuất khẩu đang chọn (gọi `CreateExportVehicleSessionUseCase`). |
| UI-EXP-BTN-TRANSFER | `Button` | `TransferTripCommand` | Mở dialog chuyển chuyến xe con từ đơn xuất khẩu nguồn sang đích (gọi `TransferExportVehicleTripUseCase`). |
| UI-EXP-BTN-FINALIZE | `Button` | `FinalizeCommand` | Nút bấm chốt sản lượng cho hợp đồng xuất khẩu lớn để kết thúc và cho xe ra (`FinalizeExportCutOrderUseCase`). |
| UI-EXP-GRID-CO | `DataGrid` | `ItemsSource={Binding CutOrders}` | Bảng danh sách các đơn hàng xuất khẩu lớn đang hoạt động. |
| UI-EXP-GRID-TRIPS | `DataGrid` | `ItemsSource={Binding Trips}` | Lưới hiển thị danh sách toàn bộ các chuyến xe con đã và đang thực hiện của đơn hàng xuất khẩu được chọn. |

### 4.4.3 Các Yêu cầu Chức năng Nghiệp vụ liên quan (FR-EXPORT-001)
- **Tạo chuyến xe con**: Khi nhân viên trạm cân nhấn nút "Tạo Chuyến Xe", hệ thống tạo một phiên cân con độc lập ở trạng thái `PENDING_WEIGHT1`. Khối lượng kế hoạch còn lại của chuyến con được tính tự động bằng cách lấy khối lượng gốc của đơn trừ đi tổng khối lượng tịnh thực tế của các chuyến xe con trước đó đã hoàn tất.
- **Chuyển chuyến xe con**: Trường hợp xe con vào cân lần 1 dưới đơn hàng A nhưng khi ra lại bốc hàng và xuất hóa đơn dưới đơn hàng B, nhân viên bấm nút "Chuyển Chuyến". Hộp thoại hiện ra cho phép chuyển chuyến xe con đó sang đơn hàng xuất khẩu B. Hệ thống tự động cập nhật lại toàn bộ liên kết của dòng chi tiết phiên cân, phiếu cân và phiếu giao nhận liên quan sang đơn cắt lệnh đích.
- **Chốt sản lượng**: Khi toàn bộ các chuyến xe con của đơn hàng xuất khẩu lớn đã thực hiện xong và không còn chuyến nào dở dang, nhân viên cân hoặc quản trị viên nhấn "Chốt Tổng". Hệ thống cộng dồn tổng khối lượng tịnh của các chuyến xe con hợp lệ gán vào `ExportFinalizedWeight` trên đơn hàng cha và chuyển trạng thái đơn hàng cha sang `COMPLETED`, giải phóng trạng thái vận hành.

---

## 4.5 Phân hệ 5: Cấu hình Hệ thống và Thiết bị (System & Hardware Configurations)

### 4.5.1 Bố cục Giao diện Cấu hình (SettingsView.xaml)
Giao diện quản trị hệ thống dành cho vai trò ADMIN để cài đặt các thiết bị phần cứng, căn chỉnh phôi in ấn, quản trị tài khoản và sao lưu dữ liệu cục bộ.

```
+--------------------------------------------------------------------------------------------------+
| CẤU HÌNH HỆ THỐNG                                                                               |
| [ Thiết Bị Cân ]  [ Camera RTSP ]  [ Cân Chỉnh Phôi In ]  [ Người Dùng ]  [ Sao Lưu & Đồng Bộ ]   |
+--------------------------------------------------------------------------------------------------+
| CẤU HÌNH SAO LƯU DỰ PHÒNG CƠ SỞ DỮ LIỆU CỤC BỘ (LOCAL DATABASE BACKUP)                           |
|                                                                                                  |
|   Đường dẫn thư mục sao lưu:  [ C:\ProgramData\StationApp\SqlBackups                     ] [Chọn]|
|   Thời gian tự động sao lưu:   Hàng ngày lúc 03:00 AM                                            |
|   Thời gian lưu trữ tệp tin:  10 ngày (Hệ thống tự động xóa tệp tin cũ hơn)                      |
|                                                                                                  |
|   [ SAO LƯU DỮ LIỆU NGAY ]                         [ KHÔI PHỤC DỮ LIỆU CỤ CỤC BỘ ]                |
|                                                                                                  |
|   Nhật ký sao lưu:                                                                               |
|   - 07/06/2026 03:00:02 AM - Sao lưu tự động thành công -> 20260607_StationDb.bak (120MB)       |
|   - 06/06/2026 03:00:01 AM - Sao lưu tự động thành công -> 20260606_StationDb.bak (118MB)       |
+--------------------------------------------------------------------------------------------------+
```

### 4.5.2 Danh sách Phần tử Giao diện Chính (ScaleDeviceConfigView.xaml, SystemSettingsView.xaml)
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-SET-TXT-BKDIR | `TextBox` | `BackupDirectory` | Thiết lập đường dẫn thư mục lưu trữ file backup cơ sở dữ liệu local. |
| UI-SET-BTN-BKNOW | `Button` | `RunBackupNowCommand` | Thực hiện sao lưu cơ sở dữ liệu cục bộ SQL Server ngay lập tức (gọi `ILocalDatabaseBackupService.RunBackupNowAsync`). |
| UI-SET-COM-PORT | `ComboBox` | `SelectedPortName` | Chọn tên cổng COM vật lý kết nối với đầu cân (ví dụ: `COM1`, `COM2`). |
| UI-SET-TXT-RTSP1 | `TextBox` | `Camera1RtspUrl` | Nhập đường dẫn luồng RTSP cho camera số 1 giám sát đầu xe. |
| UI-SET-TXT-OFFX | `TextBox` | `OffsetXmm` | Nhập độ lệch lề in theo phương ngang (mm) cho mẫu in chứng từ. |

### 4.5.3 Các Yêu cầu Chức năng Nghiệp vụ liên quan (FR-BACKUP-001 & HW-COM/CAM)
- **Cấu hình Cổng COM**: Cho phép Admin thiết lập các tham số Baudrate (4800, 9600), DataBits, StopBits để mở kết nối SerialPort đọc dữ liệu thô từ đầu cân.
- **Tác vụ Tự động Sao lưu**: Tác vụ chạy ngầm `LocalDatabaseBackupWorker` chạy định kỳ kiểm tra. Vào đúng **03:00 AM** mỗi ngày, worker gọi câu lệnh backup SQL Server cục bộ lưu vào thư mục cấu hình dưới định dạng tệp `yyyyMMdd_{DatabaseName}.bak`.
- **Retention (Dọn dẹp)**: Sau khi hoàn thành sao lưu, worker quét thư mục và xóa các tệp `.bak` cũ hơn **10 ngày** để giải phóng bộ nhớ đĩa cứng cho máy trạm.

---

## 4.6 Phân hệ 6: Quản lý Phiên cân hoàn tất và Chứng từ (Outgoing & Tickets Module)

### 4.6.1 Bố cục Giao diện Danh sách xe ra (OutgoingVehicleListView.xaml)
Quản lý các phiên cân đã hoàn tất hoặc sẵn sàng hoàn tất. Nhân viên cân có thể in lại chứng từ hoặc kiểm tra lịch sử đồng bộ.

```
+--------------------------------------------------------------------------------------------------+
| PHIÊN CÂN ĐÃ HOÀN TẤT & IN LẠI PHIẾU                                                             |
| Tìm xe: [ Biển số xe...                ]                                           [Tìm Kiếm]    |
+--------------------------------------------------------------------------------------------------+
| Số Phiên    | Biển Số Xe | Khách Hàng     | Sản Phẩm | Khối Lượng Tịnh | Trạng Thái | Đồng Bộ    |
|-------------|------------|----------------|----------|-----------------|------------|------------|
| W260607-001 | 29C-123.45 | Công ty A      | Bao 50kg | 30,160 kg       | COMPLETED  | Đã Đồng Bộ |
| W260607-002 | 30E-987.65 | Công ty B      | Clinker  | 26,000 kg       | COMPLETED  | Chờ Đồng Bộ|
+--------------------------------------------------------------------------------------------------+
| [ In Lại Phiếu Cân Tổng ]       [ In Lại Phiếu Giao Nhận ]             [ Xem Nhật Ký Kiểm Toán ] |
+--------------------------------------------------------------------------------------------------+
```

### 4.6.2 Danh sách Phần tử Giao diện Chính (OutgoingVehicleListView.xaml)
| Mã phần tử | Loại Control | Tên / Binding | Mô tả chức năng |
| :--- | :--- | :--- | :--- |
| UI-OUT-GRID-DATA | `DataGrid` | `ItemsSource={Binding CompletedSessions}` | Lưới danh sách các phiên cân đã hoàn thành hoặc sẵn sàng hoàn thành. |
| UI-OUT-BTN-PRINTWEIGH | `Button` | `ReprintWeighTicketCommand` | Thực hiện in lại Phiếu cân tổng hợp của phiên cân đang chọn. |
| UI-OUT-BTN-PRINTDELIV | `Button` | `ReprintDeliveryTicketCommand` | Thực hiện in lại Phiếu giao nhận chi tiết cho dòng đơn hàng đang chọn. |

### 4.6.3 Các Yêu cầu Chức năng Nghiệp vụ liên quan (FR-PRINT-001)
- **Quy tắc In ấn Bắt buộc**: Hệ thống chỉ cho phép xe ra (`MoveToOutYardCommand`) khi và chỉ khi nhân viên cân đã thực hiện in thành công Phiếu cân tổng hợp chính (`HasPrintedMasterWeighTicket = true`) và toàn bộ các Phiếu giao nhận chi tiết của các dòng cắt lệnh liên kết (`HasPrintedDeliveryTicket = true`).
- **In lại chứng từ**: Vai trò nhân viên cân được phép in lại chứng từ nếu gặp sự cố kẹt giấy hoặc mờ mực. Tuy nhiên hành động này sẽ ghi lại sự kiện vào `audit_logs` để kiểm soát số lần in ấn.
"""
    
    content_new = content_base_before + new_chapter_4 + content_base_after
    
    with open(srs_path, 'w', encoding='utf-8') as f:
        f.write(content_new)
    print("SRS successfully restructured with detail modules and UI layouts!")
else:
    print("Could not find start/end targets in SRS.")
