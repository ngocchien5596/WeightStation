# Implementation Plan: Sửa báo cáo nhập (trạm đập, mỏ sét) theo format `Book1.xlsx`

## Overview

Kế hoạch này mô tả cách chỉnh lại cả hai báo cáo nhập `trạm đập` và `mỏ sét` để file Excel xuất ra bám theo format mẫu trong [Book1.xlsx](../Book1.xlsx). Mục tiêu là thay đổi phần `export Excel`, `preview` và `print` của hai báo cáo hiện tại từ layout 17 cột, header hành chính và footer ký tên kiểu cũ sang layout gọn 9 cột giống file mẫu, đồng thời giữ nguyên logic lọc dữ liệu theo `Số xe` vừa triển khai.

Phạm vi chính của thay đổi là:

- Chuẩn hóa lại `shape` dữ liệu cho đúng 9 cột của mẫu.
- Viết lại layout Excel trong `CrusherInboundReportExcelExporter` và `ClayInboundReportExcelExporter`.
- Đồng bộ `preview grid` theo layout mẫu.
- Đồng bộ nút `IN` để in đúng layout mẫu.
- Giữ nguyên nguồn dữ liệu từ các phiên cân nhập hoàn tất (`INBOUND`, `COMPLETED`, theo `Weight2Time`).
- Tận dụng tối đa phần dùng chung giữa hai báo cáo, chỉ khác các phần nhận diện báo cáo như tiêu đề, tên sheet/file và dữ liệu tên trạm.
- Thêm logo công ty vào header Excel, đặt ở bên phải khối thông tin `B1:B3`.

## Mẫu `Book1.xlsx` đang thể hiện gì

Workbook mẫu có 1 sheet `Sheet1`, vùng dữ liệu chính nằm trong `A1:I30`, với các đặc điểm quan trọng:

- Header trái:
  - `B1:D1`: `CÔNG TY CỔ PHẦN XI MĂNG CẨM PHẢ`
  - `B2`: địa chỉ
  - `B3`: điện thoại
- Logo công ty:
  - cần chèn ở bên phải khối thông tin `B1:B3`
  - nằm cùng vùng header trên, không chen vào bảng dữ liệu bên dưới
- Header phải:
  - `G1:H2`: `BÁO CÁO THỐNG KÊ CÂN HÀNG`
  - `G3`: `Thời gian: Từ ... đến ... ngày ...`
- Bảng dữ liệu:
  - Header ở dòng `5`
  - 9 cột từ `A:I`
  - Các cột lần lượt là:
    1. `STT`
    2. `Số phiếu`
    3. `Số xe`
    4. `Ngày cân`
    5. `Tổng (tấn)`
    6. `Bì (tấn)`
    7. `Hàng (tấn)`
    8. `Khách hàng`
    9. `Hàng hóa`
- Dòng cộng tổng:
  - `A22:D22` merge với nhãn `Cộng tổng:`
  - `G22` là công thức cộng cột `Hàng (tấn)`
- Khối ký cuối báo cáo:
  - `B24:D24`: `ĐẠI DIỆN ĐƠN VỊ KHAI THÁC`
  - `H24:I24` tương đương khu vực đại diện bên phải
  - Dòng cuối có nhãn khu vực vận hành, `Thời gian in`, `Trang: 1/1`
- Font mẫu dùng `Times New Roman`, tiêu đề đậm cỡ lớn, header bảng có nền xám nhạt và viền mảnh.

## Quy ước nghiệp vụ đã chốt

- `Số phiếu` lấy từ `SessionNo`, nhưng khi hiển thị phải bỏ tiền tố đầu:
  - `QN02-`
  - `QN03-`
- `Tổng (tấn)` lấy từ `Weight1 / 1000`
- `Bì (tấn)` lấy theo ưu tiên:
  - `StandardTareWeightKg / 1000` nếu có
  - fallback `Weight2 / 1000` nếu không có `StandardTareWeightKg`
- `Ngày cân` hiển thị đúng kiểu dữ liệu mẫu: `dd/MM/yyyy HH:mm`
- `Preview` trên màn hình phải giống layout mẫu
- Nút `IN` phải in đúng layout mẫu, không in theo `FlowDocument` dạng grid như hiện tại
- Footer trái dùng `Tên Trạm đang thao tác`, áp dụng cho cả trạm đập và mỏ sét
- Logo dùng file có sẵn trong `src/StationApp.UI/Assets`

## Current State

Code hiện tại của hai báo cáo nhập đang ở trạng thái:

- UI đã lọc theo `Số xe` và có `Xem / In / Tải`:
  - [src/StationApp.UI/Views/CrusherInboundReportView.xaml](../src/StationApp.UI/Views/CrusherInboundReportView.xaml)
  - [src/StationApp.UI/ViewModels/CrusherInboundReportViewModel.cs](../src/StationApp.UI/ViewModels/CrusherInboundReportViewModel.cs)
  - [src/StationApp.UI/Views/ClayInboundReportView.xaml](../src/StationApp.UI/Views/ClayInboundReportView.xaml)
  - [src/StationApp.UI/ViewModels/ClayInboundReportViewModel.cs](../src/StationApp.UI/ViewModels/ClayInboundReportViewModel.cs)
- DTO/export shape hiện tại vẫn phục vụ layout chi tiết 17 cột:
  - [src/StationApp.Application/DTOs/CrusherInboundReportDtos.cs](../src/StationApp.Application/DTOs/CrusherInboundReportDtos.cs)
  - [src/StationApp.Application/DTOs/ClayInboundReportDtos.cs](../src/StationApp.Application/DTOs/ClayInboundReportDtos.cs)
- Service build dữ liệu vẫn trả đầy đủ các trường chi tiết như:
  - `SessionNo`
  - `InternalVehicleNo`
  - `DriverName`
  - `CustomerName`
  - `ProductName`
  - `Weight1Time`
  - `Weight2Time`
  - `Weight1`
  - `StandardTareWeightKg`
  - `Weight2`
  - `NetWeightKg`
  - `Notes`
  - `WeigherName`
- Exporter hiện tại đang dùng layout cũ:
  - [src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs](../src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs)
  - [src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs](../src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs)
  - Bảng từ cột `B:R`
  - 17 cột
  - Header hành chính 2 khối trái/phải
  - Footer `Tổ trưởng` / `Người lập`

Điểm khác nhau hiện có giữa hai báo cáo chủ yếu là:

- Tiêu đề báo cáo:
  - `BÁO CÁO NHẬP TRẠM ĐẬP`
  - `BÁO CÁO NHẬP MỎ SÉT`
- Tên sheet xuất:
  - `BaoCaoNhapTramDap`
  - `BaoCaoNhapMoSet`
- Rule cân 1 lần đang dùng constant khác nhau theo nghiệp vụ:
  - `CrusherWeighingModes.SingleWithStandardTare`
  - `ClayWeighingModes.SingleWithStandardTare`

## Gap giữa format hiện tại và format mẫu

Khác biệt chính cần xử lý:

- Mẫu mới chỉ có `9` cột, còn exporter hiện tại đang xuất `17` cột.
- Mẫu mới hiển thị `Số phiếu`, `Số xe`, `Ngày cân`, `Tổng/Bì/Hàng`, `Khách hàng`, `Hàng hóa`; không còn các cột:
  - tài xế
  - chế độ cân
  - ngày/giờ cân 1 riêng
  - ngày/giờ cân 2 riêng
  - TL xe chuẩn
  - ghi chú
  - người cân
- Mẫu dùng đơn vị `tấn`, còn dữ liệu hiện tại chủ yếu đang ở `kg`.
- Mẫu cộng tổng riêng cột `Hàng (tấn)` thay vì đang cộng `NetWeightKg` tại một dòng tổng theo layout cũ.
- Mẫu có dòng `Thời gian in` và `Trang: 1/1` ở footer dưới cùng.
- Mẫu có bố cục header doanh nghiệp đơn giản hơn layout hiện tại.
- Mẫu mới còn bổ sung thêm yêu cầu có `logo công ty` trong vùng header.
- Preview và print hiện tại chưa bám layout mẫu.

## Architecture Decisions

- `Book1.xlsx` được coi là nguồn chuẩn cho `format export`, `preview` và `print`.
- Ưu tiên thay đổi shape dữ liệu và layout dùng chung trước, sau đó áp vào cả `crusher` và `clay`.
- Dùng chính `Weight2Time` làm `Ngày cân` trong báo cáo.
- Thiết kế implementation theo hướng dùng chung tối đa giữa `crusher` và `clay`; không copy-paste hai khối layout lớn nếu có thể tách helper hoặc chiến lược cấu hình.
- Logo cần lấy từ một asset chuẩn, cố định tỉ lệ và vị trí neo trong header để tránh hai báo cáo hiển thị lệch nhau.
- Chuẩn hóa `SessionNo` ở một chỗ duy nhất để bỏ tiền tố `QN02-` và `QN03-`.
- Không tái sử dụng bản in grid cũ; cơ chế in phải bám đúng layout mẫu.

## Mapping dữ liệu đề xuất theo mẫu mới

| Cột mẫu | Nguồn dữ liệu đã chốt | Ghi chú |
|---|---|---|
| `STT` | số thứ tự dòng | tăng từ 1 |
| `Số phiếu` | `SessionNo` sau khi bỏ `QN02-`, `QN03-` | dùng chung cho cả 2 báo cáo |
| `Số xe` | `InternalVehicleNo` | fallback `VehiclePlate` nếu thiếu |
| `Ngày cân` | `Weight2Time` | format `dd/MM/yyyy HH:mm` |
| `Tổng (tấn)` | `Weight1 / 1000` | |
| `Bì (tấn)` | `StandardTareWeightKg / 1000`, fallback `Weight2 / 1000` | |
| `Hàng (tấn)` | `NetWeightKg / 1000` | |
| `Khách hàng` | `CustomerName` | |
| `Hàng hóa` | `ProductName` | |

## Task List

### Phase 1: Khóa mapping và phạm vi UI

#### Task 1: Đối chiếu cột mẫu với dữ liệu hiện có

**Description:** Rà toàn bộ field hiện đang có trong `CrusherInboundReportRow` và `ClayInboundReportRow`, xác định field nào map trực tiếp được sang 9 cột mới theo quy ước đã chốt.

**Acceptance criteria:**
- [ ] Có bảng mapping cuối cùng cho đủ 9 cột của `Book1.xlsx`
- [ ] Xác định rõ cột nào cần chuyển đổi `kg -> tấn`
- [ ] Xác định rõ logic fallback của cột `Bì (tấn)`
- [ ] Xác định rõ điểm normalize `SessionNo`

**Verification:**
- [ ] Review lại [src/StationApp.Application/DTOs/CrusherInboundReportDtos.cs](../src/StationApp.Application/DTOs/CrusherInboundReportDtos.cs)
- [ ] Review lại [src/StationApp.Application/DTOs/ClayInboundReportDtos.cs](../src/StationApp.Application/DTOs/ClayInboundReportDtos.cs)
- [ ] Review lại [src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs](../src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs)
- [ ] Review lại [src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs](../src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs)
- [ ] So khớp từng cột với `Book1.xlsx`

**Dependencies:** None

**Files likely touched:**
- Không sửa code ở task này

**Estimated scope:** XS

#### Task 2: Khóa phạm vi `preview` và `print`

**Description:** Chốt phạm vi UI theo yêu cầu đã xác nhận: cả `preview grid` và `print layout` đều phải đồng bộ theo format mẫu, không chỉ riêng file export.

**Acceptance criteria:**
- [ ] `Preview` được xác định là phải giống file mẫu
- [ ] `Print` được xác định là phải in đúng layout mẫu
- [ ] Plan phản ánh đúng phạm vi triển khai

**Verification:**
- [ ] So sánh [src/StationApp.UI/ViewModels/CrusherInboundReportViewModel.cs](../src/StationApp.UI/ViewModels/CrusherInboundReportViewModel.cs) với layout mẫu
- [ ] So sánh [src/StationApp.UI/ViewModels/ClayInboundReportViewModel.cs](../src/StationApp.UI/ViewModels/ClayInboundReportViewModel.cs) với layout mẫu

**Dependencies:** Task 1

**Files likely touched:**
- Không sửa code ở task này

**Estimated scope:** XS

### Checkpoint: Scope Locked

- [ ] Mapping 9 cột đã rõ
- [ ] Phạm vi `export + preview + print` đã được chốt
- [ ] Không còn mơ hồ ở cột `Tổng` và `Bì`

### Phase 2: Chuẩn hóa dữ liệu dùng chung

#### Task 3: Tạo shape dữ liệu phù hợp layout 9 cột

**Description:** Điều chỉnh DTO/report row để exporter, preview và print mới của cả `crusher` và `clay` không phải phụ thuộc vào cấu trúc 17 cột cũ.

**Acceptance criteria:**
- [ ] Truy cập được đầy đủ field cần cho 9 cột mẫu
- [ ] Conversion `kg -> tấn` được đặt ở một chỗ rõ ràng, dễ kiểm tra
- [ ] Có chỗ chuẩn hóa `SessionNo` để bỏ tiền tố `QN02-` và `QN03-`
- [ ] Không làm gãy luồng dữ liệu hiện tại của hai màn hình

**Verification:**
- [ ] Build project `StationApp.Application`
- [ ] Build project `StationApp.Infrastructure`

**Dependencies:** Task 1, Task 2

**Files likely touched:**
- [src/StationApp.Application/DTOs/CrusherInboundReportDtos.cs](../src/StationApp.Application/DTOs/CrusherInboundReportDtos.cs)
- [src/StationApp.Application/DTOs/ClayInboundReportDtos.cs](../src/StationApp.Application/DTOs/ClayInboundReportDtos.cs)
- [src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs](../src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs)
- [src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs](../src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs)

**Estimated scope:** S

#### Task 4: Bổ sung helper mapping và format

**Description:** Chuẩn hóa helper cho các phần lặp lại trong exporter/preview/print mới của cả hai báo cáo như format ngày cân, format tấn, chuẩn hóa số phiếu và căn lề số.

**Acceptance criteria:**
- [ ] Có helper hoặc block code rõ ràng cho format ngày
- [ ] Có helper hoặc block code rõ ràng cho đơn vị tấn
- [ ] Có helper hoặc block code rõ ràng cho normalize `SessionNo`
- [ ] Không lặp lại logic format ở nhiều chỗ

**Verification:**
- [ ] Review code không có duplication lớn
- [ ] Build `StationApp.Infrastructure`

**Dependencies:** Task 3

**Files likely touched:**
- [src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs](../src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs)
- [src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs](../src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs)

**Estimated scope:** XS

### Checkpoint: Data Ready

- [ ] Dữ liệu đã sẵn sàng cho 9 cột mẫu
- [ ] Mapping ra tấn/ngày/số phiếu đã ổn định
- [ ] Có chiến lược dùng chung cho cả `crusher` và `clay`

### Phase 3: Viết lại layout xuất và in theo `Book1.xlsx`

#### Task 5: Viết lại phần header và subtitle

**Description:** Thay header hành chính cũ của cả hai exporter bằng header doanh nghiệp theo mẫu `Book1.xlsx`, gồm tên công ty, địa chỉ, điện thoại, logo công ty lấy từ `src/StationApp.UI/Assets`, tiêu đề báo cáo và thời gian lọc.

**Acceptance criteria:**
- [ ] Có merge cell tương đương các khối `B1:D1`, `G1:H2`
- [ ] Có logo công ty nằm bên phải khối `B1:B3`
- [ ] Font, cỡ chữ, căn giữa/căn trái gần đúng mẫu
- [ ] Dòng `Thời gian: Từ ... đến ... ngày ...` phản ánh đúng filter hiện tại
- [ ] Tiêu đề được thay đúng theo từng báo cáo: `TRẠM ĐẬP` và `MỎ SÉT`

**Verification:**
- [ ] Xuất thử file Excel
- [ ] Mở file và so sánh trực quan với `Book1.xlsx`

**Dependencies:** Task 4

**Files likely touched:**
- [src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs](../src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs)
- [src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs](../src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs)
- `src/StationApp.UI/Assets/...`

**Estimated scope:** S

#### Task 6: Viết lại bảng dữ liệu 9 cột

**Description:** Thay hoàn toàn `BuildTable(...)` hiện tại của cả hai exporter bằng layout mới từ `A5:I...`, gồm header, data rows, căn lề, định dạng số tấn và ngày cân.

**Acceptance criteria:**
- [ ] Header bảng đúng thứ tự 9 cột của mẫu
- [ ] Dữ liệu từng dòng map đúng vào 9 cột
- [ ] Các cột số có format nhất quán
- [ ] Các cột text không bị cắt quá hẹp khi mở Excel

**Verification:**
- [ ] Xuất file với dữ liệu thật
- [ ] So đối chiếu 3-5 dòng đầu với dữ liệu DB/session gốc
- [ ] Kiểm tra số liệu `Hàng (tấn)` khớp `NetWeightKg / 1000`

**Dependencies:** Task 5

**Files likely touched:**
- [src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs](../src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs)
- [src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs](../src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs)

**Estimated scope:** M

#### Task 7: Thêm dòng cộng tổng và footer động

**Description:** Bổ sung dòng `Cộng tổng:` và footer cuối trang cho cả hai báo cáo, trong đó footer trái dùng `Tên Trạm đang thao tác` thay vì nhãn cứng.

**Acceptance criteria:**
- [ ] Có dòng `Cộng tổng:` merge đúng vùng tương ứng
- [ ] Tổng cột `Hàng (tấn)` được tính đúng
- [ ] Footer có đủ 3 phần: trái, giữa, phải
- [ ] Footer trái hiển thị đúng `Tên Trạm đang thao tác`
- [ ] Kẻ đường trên footer tương tự mẫu nếu cần

**Verification:**
- [ ] Mở file export và so trực quan với `Book1.xlsx`
- [ ] Kiểm tra tổng cuối báo cáo bằng tay với sum các dòng chi tiết

**Dependencies:** Task 6

**Files likely touched:**
- [src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs](../src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs)
- [src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs](../src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs)

**Estimated scope:** S

#### Task 8: Chỉnh page setup và print layout

**Description:** Tối ưu khổ in, độ rộng cột, row height, font và vùng in cho cả hai báo cáo để file mở ra gần mẫu nhất và nút `IN` cũng in đúng layout mẫu.

**Acceptance criteria:**
- [ ] Cột `A:I` có width hợp lý gần mẫu
- [ ] Font `Times New Roman` áp dụng toàn sheet báo cáo
- [ ] Layout không bị tràn ngang khi in
- [ ] Freeze pane và print area được cân nhắc lại theo layout mới
- [ ] Có cơ sở để tái sử dụng layout export cho in

**Verification:**
- [ ] Print Preview trong Excel không vỡ layout
- [ ] Kiểm tra file xuất trên máy dev

**Dependencies:** Task 7

**Files likely touched:**
- [src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs](../src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs)
- [src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs](../src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs)

**Estimated scope:** S

### Checkpoint: Export Layout Complete

- [ ] File export mới bám được `Book1.xlsx`
- [ ] 9 cột lên đúng dữ liệu
- [ ] Dòng tổng, logo và footer đã hoàn chỉnh

### Phase 4: Đồng bộ UI theo mẫu

#### Task 9: Đồng bộ grid preview với format mới

**Description:** Đồng bộ `DataGrid` hiện tại của cả hai màn hình xuống các cột tương ứng file mẫu để người dùng nhìn trên màn hình giống file xuất.

**Acceptance criteria:**
- [ ] Grid chỉ còn các cột cần thiết theo format mới
- [ ] Summary text vẫn hoạt động đúng
- [ ] Không làm hỏng nút `Xem`, `In`, `Tải`

**Verification:**
- [ ] Build `StationApp.UI`
- [ ] Mở màn hình báo cáo và preview bằng dữ liệu thật

**Dependencies:** Task 2, Task 8

**Files likely touched:**
- [src/StationApp.UI/Views/CrusherInboundReportView.xaml](../src/StationApp.UI/Views/CrusherInboundReportView.xaml)
- [src/StationApp.UI/ViewModels/CrusherInboundReportViewModel.cs](../src/StationApp.UI/ViewModels/CrusherInboundReportViewModel.cs)
- [src/StationApp.UI/Views/ClayInboundReportView.xaml](../src/StationApp.UI/Views/ClayInboundReportView.xaml)
- [src/StationApp.UI/ViewModels/ClayInboundReportViewModel.cs](../src/StationApp.UI/ViewModels/ClayInboundReportViewModel.cs)

**Estimated scope:** S

#### Task 10: Đồng bộ nút `IN` theo layout mẫu

**Description:** Thay cơ chế in tạm hiện tại của cả hai báo cáo bằng layout in đúng mẫu, thống nhất với file export và preview.

**Acceptance criteria:**
- [ ] Bản in từ nút `In` phản ánh đúng layout mẫu
- [ ] Không in các cột cũ đã bị loại khỏi format mới
- [ ] Bản in hiển thị đúng `Tên Trạm đang thao tác` ở footer trái

**Verification:**
- [ ] Test `Print Preview` qua máy in ảo
- [ ] Kiểm tra văn bản tiếng Việt không lỗi font

**Dependencies:** Task 2, Task 9

**Files likely touched:**
- [src/StationApp.UI/ViewModels/CrusherInboundReportViewModel.cs](../src/StationApp.UI/ViewModels/CrusherInboundReportViewModel.cs)
- [src/StationApp.UI/ViewModels/ClayInboundReportViewModel.cs](../src/StationApp.UI/ViewModels/ClayInboundReportViewModel.cs)

**Estimated scope:** S

### Phase 5: Verification và chốt release

#### Task 11: Kiểm thử số liệu đầu-cuối

**Description:** So đối chiếu file export của cả `trạm đập` và `mỏ sét` với dữ liệu thật từ DB hoặc preview để chắc các cột `Tổng/Bì/Hàng` không bị đảo hoặc sai đơn vị.

**Acceptance criteria:**
- [ ] Chọn ít nhất 3 phiên cân để đối chiếu tay
- [ ] Tổng `Hàng (tấn)` khớp tổng cộng cuối báo cáo ở cả hai loại báo cáo
- [ ] `Số phiếu` hiển thị đúng sau khi bỏ tiền tố

**Verification:**
- [ ] Xuất file test
- [ ] So với dữ liệu nguồn trong DB hoặc grid

**Dependencies:** Task 8

**Files likely touched:**
- Không bắt buộc sửa code

**Estimated scope:** XS

#### Task 12: Build sạch và review chênh lệch cuối

**Description:** Build lại project liên quan, rà diff cuối cùng, và ghi nhận các điểm chưa làm nếu có chủ đích.

**Acceptance criteria:**
- [ ] `StationApp.Infrastructure` build sạch
- [ ] `StationApp.UI` build sạch
- [ ] Diff cuối cùng chỉ chứa thay đổi phục vụ format báo cáo nhập trạm đập và mỏ sét

**Verification:**
- [ ] `dotnet build src/StationApp.UI/StationApp.UI.csproj -p:StationAppBuildRoot=... -p:SkipDatabaseSchemaUpdate=true`
- [ ] Review `git diff`

**Dependencies:** Task 11

**Files likely touched:**
- Không bắt buộc sửa code

**Estimated scope:** XS

## Risks and Mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Hiểu sai cột `Tổng (tấn)` và `Bì (tấn)` | High | Đã chốt mapping trước khi code |
| Layout mẫu nhìn đơn giản nhưng có nhiều chi tiết merge/border | Medium | Tách riêng `BuildHeader`, `BuildTable`, `BuildFooter`, `ApplySheetLayout` và cân nhắc helper dùng chung cho hai báo cáo |
| Chèn logo trong Excel dễ lệch vị trí hoặc scale không đẹp | Medium | Chốt 1 file logo chuẩn và cố định anchor/size theo vùng header ngay từ đầu |
| Preview/grid và export đi theo 2 shape khác nhau | Medium | Đồng bộ từ cùng một shape/layout chuẩn |
| Print hiện tại không khớp export mới | Medium | Thay hẳn cách dựng bản in để bám layout mẫu, không tái dùng bản in grid cũ |
| Hai báo cáo bị sửa lệch nhau theo thời gian | Medium | Thiết kế task và implementation theo hướng song song, review diff của cả `crusher` và `clay` trong cùng một nhịp |

## Verification Checklist trước khi bắt đầu code

- [x] Đã chốt cột `Số phiếu`
- [x] Đã chốt mapping `Tổng` và `Bì`
- [x] Đã chốt có đổi `Preview`
- [x] Đã chốt có đổi `Print`
- [x] Đã chốt footer trái dùng `Tên Trạm đang thao tác`
- [x] Đã chốt file logo công ty dùng để chèn Excel
- [ ] Đồng ý lấy `Book1.xlsx` làm format chuẩn cho cả hai exporter mới

