# Plan tạo version phiếu giao nhận A5 mới chạy song song

## 1. Mục tiêu

Tạo thêm một version phiếu giao nhận mới dựa trên file `Mau_A5_Phieu_Giao_Nhan_EDITABLE_v4.docx`, dùng song song với version phiếu giao nhận hiện tại.

Mẫu mới phải bám đúng cách vừa làm cho phiếu cân A5 v2:

- Không thay thế hoặc xóa `PGN ver 1`.
- Không tự đặt mẫu mới làm mặc định.
- Người dùng chọn được version phiếu ngay ở modal in.
- Giá trị mặc định khi mở modal in lấy theo version mặc định đã set ở màn `Cấu hình in`.
- Chỉ in text động lên phôi A5 ngang đã in sẵn.
- Dùng GDI text Unicode, font `Times New Roman`, giữ tiếng Việt có dấu.
- Không chuyển tiếng Việt sang không dấu.
- Không rasterize cả phiếu thành ảnh để tránh chữ bị chấm/mờ trên máy in kim.

## 2. Nội dung mẫu Word hiện tại

Đã đọc lại file `Mau_A5_Phieu_Giao_Nhan_EDITABLE_v4.docx` sau khi người dùng sửa. Mẫu hiện tại là A5 ngang và trên phôi đã có sẵn toàn bộ text tĩnh/logo/đường kẻ.

Các nhãn/khối nội dung chính trên mẫu:

- `PHIẾU GIAO NHẬN`
- `Liên 1: Lưu P.CLKD`
- Dòng thời gian:
  - `Vào lúc ... giờ ... phút, Ngày .../.../...`
  - `Ra lúc ... giờ ... phút, Ngày .../.../...`
- `Số phiếu`
- `Mã đơn hàng`
- `Mã in vỏ bao`
- `Tên khách hàng`
- `Mã khách hàng`
- `Nơi tiêu thụ`
- `Số phương tiện vận chuyển`
- `Chủng loại hàng`
- Bảng số lượng:
  - `Số lượng đặt hàng`: `Bao`, `Tấn`
  - `Số lượng thực giao`: `Bao`, `Tấn`
- `Tên lái xe`
- `Ghi chú`
- `Niêm chì số`
- `Số lô`
- `Nơi xuất hàng`
- Dòng lưu ý tĩnh đã có trên phôi:
  - `Lưu ý: Người nhận hàng kiểm tra các thông tin trên phiếu giao nhận, ký xác nhận và chịu trách nhiệm về các số liệu trên phiếu.`
- Khu chữ ký:
  - `NGƯỜI GIAO HÀNG`
  - `NGƯỜI NHẬN HÀNG`
  - `(Ghi rõ Họ Tên)`

## 3. Các điểm đã chốt

- Mẫu mới là A5 ngang.
- Trên phôi có hết nội dung tĩnh, chỉ thực hiện in text động.
- Tọa độ field chỉ cần căn tương đối ban đầu; sau khi code xong người dùng sẽ chỉnh vị trí qua màn `Cấu hình in`.
- Không cần thêm field tĩnh như `Lưu ý`.
- Có field `Mã in vỏ bao`, lấy từ ERP `M_CommandLatching.PrinterName`.
- `Mã đơn hàng` lấy y như phiếu giao nhận version 1.
- `Số lượng đặt hàng` tấn/bao và `Số lượng thực giao` tấn/bao làm logic y như phiếu giao nhận version 1.
- `Số phương tiện vận chuyển` hiển thị: biển số xe + số mooc nếu có trong ngoặc.
  Ví dụ: `14R-3584 (14RM-3642)`.
- `Tên lái xe` cần hiển thị trên mẫu mới.
- `Người giao hàng` lấy tên hiển thị của người nhấn nút in PGN.
- Vẫn cho sửa nhanh `Niêm chì số` ở modal in như hiện tại.

## 4. Hiện trạng code đã rà soát

- Phiếu giao nhận đang dùng `PrintDocumentKind.DeliveryTicket`.
- `DeliveryTicketPrintComposer` đã cấp dữ liệu cho `PGN ver 1`.
- `PrintTemplateProvider` đang seed `PGN ver 1` bằng `DeliveryTicketFields`.
- Modal in đã có dropdown `Version phiếu`, dùng chung cho phiếu cân và phiếu giao nhận.
- `WpfPrintService` đã có đường `DotMatrixGdiTextPrinter` cho template có `SupportsDotMatrixTextMode = true`.
- Phiếu giao nhận hiện tại đang dùng đường GDI text Unicode khi tích `In máy kim`.
- Composer hiện tại có đủ dữ liệu nguồn cho phương tiện và tài xế:
  - `cut_orders.VehiclePlate`: biển số xe.
  - `cut_orders.MoocNumber`: số mooc.
  - `cut_orders.ReceiverName`: tên tài xế/người nhận hàng theo cắt lệnh.
- Khi làm PGN ver 2, không thêm cột DB mới cho `Tên lái xe` và `Số phương tiện vận chuyển`; chỉ format lại từ các trường `cut_orders` hiện có.
- Composer hiện tại chưa có field riêng cho `PackagePrinterName`: mã in vỏ bao.

## 5. Nguyên tắc thiết kế

- `PGN ver 1` giữ nguyên cơ chế đang chạy ổn.
- Thêm profile mới: `PGN ver 2 - A5 mẫu mới`.
- `PGN ver 2 - A5 mẫu mới` chạy song song với `PGN ver 1`.
- Mặc định hệ thống vẫn dùng `PGN ver 1`, trừ khi người dùng vào `Cấu hình in` và đặt `PGN ver 2` làm mặc định.
- Khi chọn `PGN ver 2`, checkbox `In máy kim` phải hiện và mặc định được tích.
- `PGN ver 2` có danh sách field/tọa độ riêng, không dùng chung `DeliveryTicketFields` của `PGN ver 1`.
- Không in lại logo, watermark, tiêu đề, đường kẻ, nhãn tĩnh, dòng lưu ý hay khu chữ ký.
- Tất cả literal tiếng Việt trong code nên dùng Unicode escape hoặc bảo đảm UTF-8 sạch để tránh lỗi mojibake.
- Không thay đổi ý nghĩa field của `PGN ver 1`; nếu cần format khác cho mẫu mới thì thêm field mới.

## 6. Mapping dữ liệu PGN ver 2

| Nội dung trên mẫu mới | Field code | Nguồn/logic dữ liệu |
|---|---|---|
| Số phiếu | `DeliveryNo` | Số phiếu giao nhận hiện tại |
| Mã đơn hàng | `ReferenceCode` | Lấy y như phiếu giao nhận version 1 |
| Mã in vỏ bao | `PackagePrinterName` | ERP `M_CommandLatching.PrinterName`; lưu/đồng bộ về DB cân rồi composer đưa vào field |
| Tên khách hàng | `CustomerName` | `CutOrder.CustomerName` |
| Mã khách hàng | `CustomerCode` | `CutOrder.CustomerCode` |
| Nơi tiêu thụ | `ConsumptionPlace` | Logic y như PGN ver 1 |
| Số phương tiện vận chuyển | `VehicleLine` | `cut_orders.VehiclePlate` + `cut_orders.MoocNumber` nếu có trong ngoặc, ví dụ `14R-3584 (14RM-3642)` |
| Chủng loại hàng | `ProductName` | `CutOrder.ProductName` |
| Số lượng đặt hàng - Bao | `BagCount` | Logic y như PGN ver 1 |
| Số lượng đặt hàng - Tấn | `PlannedWeight` | Logic y như PGN ver 1 |
| Số lượng thực giao - Bao | `ActualBagCount` | Logic y như PGN ver 1 |
| Số lượng thực giao - Tấn | `ActualWeight` | Logic y như PGN ver 1 |
| Tên lái xe | `ReceiverName` | `cut_orders.ReceiverName` |
| Ghi chú | `Notes` | Ghi chú phiếu/cắt lệnh |
| Niêm chì số | `SealNo` | `CutOrder.SealNo`, cho phép sửa nhanh trong modal in |
| Số lô | `LotNo` | `CutOrder.LotNo` |
| Nơi xuất hàng | `LoadingPlace` | `CutOrder.LoadingPlace` |
| Giờ vào | `Weight1Hour`, `Weight1Minute`, `Weight1Date` | Logic y như PGN ver 1 |
| Giờ ra | `Weight2Hour`, `Weight2Minute`, `Weight2Date` | Logic y như PGN ver 1 |
| Người giao hàng | `PrintedBy` | Người nhấn nút in PGN |

Ghi chú quan trọng:

- `VehicleLine` lấy từ `cut_orders.VehiclePlate` và `cut_orders.MoocNumber`; nếu cần format khác giữa PGN ver 1 và PGN ver 2 thì xử lý ở lớp template/composer theo version, không thêm cột DB mới.
- `ReceiverName` lấy trực tiếp từ `cut_orders.ReceiverName`.
- `BagCount`, `PlannedWeight`, `ActualBagCount`, `ActualWeight` giữ nguyên logic tính như version 1, chỉ đổi vị trí hiển thị theo mẫu mới.

## 7. Ảnh hưởng dữ liệu ERP/DB

Field mới `Mã in vỏ bao` cần đi qua đủ chuỗi dữ liệu:

- ERP nguồn: `M_CommandLatching.PrinterName`.
- Procedure nhận dữ liệu cắt lệnh từ ERP cần nhận/lưu trường này nếu DB cân chưa có.
- Entity/domain `CutOrder` hoặc DTO tương ứng cần có thuộc tính phản ánh field này.
- Composer PGN cần xuất field `PackagePrinterName`.
- Template `PGN ver 2` dùng field `PackagePrinterName`.

Tên field trong DB cân cần rà soát trước khi code:

- Nếu đã có trường phù hợp thì dùng lại.
- Nếu chưa có, thêm cột mới trong bootstrap/migration theo convention hiện có.
- Nên thêm tham số optional ở cuối procedure ERP để giữ backward compatible nếu procedure đang được ERP gọi theo danh sách tham số cũ.
- Không đổi logic của `ReferenceCode`, `PlannedWeight`, `BagCount`, `ActualWeight`, `ActualBagCount` đang dùng cho PGN ver 1.

## 8. Task triển khai chi tiết

### Task 1: Chốt field list và tọa độ ban đầu theo mẫu Word mới

**Mô tả:** Dựa trên file Word vừa cập nhật, tạo danh sách field overlay cho `PGN ver 2` và ước lượng tọa độ ban đầu.

**Acceptance criteria:**
- Có danh sách field overlay đúng theo mục 6.
- Không đưa logo/đường kẻ/nhãn tĩnh/dòng lưu ý/khu chữ ký vào template mới.
- Tọa độ ban đầu đủ để preview/in thử, người dùng có thể tinh chỉnh sau.

**Verification:**
- So sánh field list với file Word mẫu.

**Files likely touched:**
- Chưa sửa code ở task này.

### Task 2: Bổ sung dữ liệu `Mã in vỏ bao`

**Mô tả:** Thêm đường dữ liệu cho `M_CommandLatching.PrinterName` từ ERP xuống DB cân và ra composer PGN.

**Acceptance criteria:**
- DB cân có nơi lưu `Mã in vỏ bao`.
- Procedure ERP upsert/update liên quan nhận và lưu được trường này.
- Composer PGN trả field `PackagePrinterName`.
- Không làm đổi kết quả các field của PGN ver 1.

**Verification:**
- Kiểm tra cắt lệnh có `PrinterName` từ ERP thì preview PGN ver 2 hiển thị đúng.
- Test composer nếu có sẵn test phù hợp.
- `dotnet build` pass.

**Files likely touched:**
- `src/StationApp.Domain/Entities/CutOrder.cs`
- `src/StationApp.Infrastructure/Persistence/StationDbContext.cs`
- `src/StationApp.Infrastructure/Persistence/Configurations/CutOrderEntityConfiguration.cs`
- `scripts/sql/sp_UpsertCutOrderFromErp.sql`
- Bootstrap SQL/schema compatibility nếu project đang dùng bootstrap tự cập nhật cột mới
- `src/StationApp.Application/Printing/PrintContracts.cs`

### Task 3: Bổ sung field composer cho PGN ver 2

**Mô tả:** Bổ sung các field mới không ảnh hưởng PGN ver 1.

**Acceptance criteria:**
- Có field `PackagePrinterName`.
- `VehicleLine` của PGN ver 2 format từ `cut_orders.VehiclePlate` và `cut_orders.MoocNumber`: `Biển số xe (Số mooc)`.
- `ReceiverName` lấy từ `cut_orders.ReceiverName`.
- Không thêm cột DB mới cho tài xế hoặc số phương tiện vận chuyển.
- `ReferenceCode`, `BagCount`, `PlannedWeight`, `ActualBagCount`, `ActualWeight` giữ logic như PGN ver 1.

**Verification:**
- Unit test composer kiểm tra các field mới.
- Unit test hiện có của PGN ver 1 vẫn pass.

**Files likely touched:**
- `src/StationApp.Application/Printing/PrintContracts.cs`
- `tests/StationApp.Application.Tests/PrintComposerTests.cs`

### Task 4: Tạo field/template riêng cho `PGN ver 2 - A5 mẫu mới`

**Mô tả:** Thêm `DeliveryTicketA5V2Fields` trong `PrintTemplateProvider`, chỉ gồm các field động cần in lên phôi.

**Acceptance criteria:**
- `PGN ver 2` có danh sách field riêng.
- Field `PackagePrinterName`, `VehicleLine`, `ReceiverName` có trong template mới.
- Các field số lượng theo đúng thứ tự mẫu mới: đặt hàng Bao/Tấn, thực giao Bao/Tấn.
- Không có logo/text tĩnh/đường kẻ/dòng lưu ý/khu chữ ký trong field list.

**Verification:**
- Preview PGN ver 2 có đủ field động.
- In trắng/PDF chỉ thấy text động.

**Files likely touched:**
- `src/StationApp.UI/Printing/PrintTemplateProvider.cs`

### Task 5: Seed profile mới song song, không đổi mặc định

**Mô tả:** Seed thêm profile `PGN ver 2 - A5 mẫu mới` nếu chưa có, tương tự `PC ver 2 - A5 mẫu mới`.

**Acceptance criteria:**
- `GetProfilesAsync(PrintDocumentKind.DeliveryTicket)` trả thêm `PGN ver 2 - A5 mẫu mới`.
- Nếu DB đã có `PGN ver 1`, profile mới vẫn được thêm tự động.
- Default profile không bị đổi.
- Cấu hình layout cũ không bị mất.

**Verification:**
- Mở modal in PGN thấy chọn được `PGN ver 2 - A5 mẫu mới`.
- Mở `Cấu hình in` thấy cả PGN ver 1 và PGN ver 2.

**Files likely touched:**
- `src/StationApp.UI/Printing/PrintTemplateProvider.cs`

### Task 6: Nhận diện đúng PGN ver 2 và bật in kim text-only

**Mô tả:** Thêm helper nhận diện PGN ver 2 để chọn đúng field defaults và bật `SupportsDotMatrixTextMode`.

**Acceptance criteria:**
- `PGN ver 1` vẫn dùng `DeliveryTicketFields`.
- `PGN ver 2` dùng `DeliveryTicketA5V2Fields`.
- `PGN ver 2` mặc định hiện checkbox `In máy kim` và tích sẵn.
- In bằng `DotMatrixGdiTextPrinter`, giữ Unicode và font `Times New Roman`.

**Verification:**
- Chọn PGN ver 1: preview/in không đổi.
- Chọn PGN ver 2: preview/in theo field mới.
- Build pass.

**Files likely touched:**
- `src/StationApp.UI/Printing/PrintTemplateProvider.cs`
- `src/StationApp.UI/Printing/WpfPrintService.cs` nếu cần

### Task 7: Cập nhật tên field và sample preview

**Mô tả:** Bổ sung tên tiếng Việt và dữ liệu mẫu cho các field mới.

**Acceptance criteria:**
- `PackagePrinterName` hiển thị là `Mã in vỏ bao`.
- `VehicleLine` hiển thị là `Số phương tiện vận chuyển`.
- `ReceiverName` hiển thị là `Tên lái xe`.
- Preview sample có đủ số phiếu, mã đơn hàng, mã in vỏ bao, khách hàng, sản phẩm, số lượng, xe/mooc, lái xe, niêm chì, giờ vào/ra.
- Không phát sinh lỗi encoding.

**Verification:**
- Mở `Cấu hình in`, chọn PGN ver 2 và kiểm tra danh sách field + preview.

**Files likely touched:**
- `src/StationApp.UI/ViewModels/Dialogs/PrintOptionsDialogViewModel.cs`
- `src/StationApp.UI/ViewModels/Settings/PrintConfigViewModel.cs`

### Task 8: Rà các màn gọi in PGN

**Mô tả:** Đảm bảo mọi màn in PGN đều dùng `dialogVm.CurrentTemplate`, tức version người dùng đang chọn.

**Acceptance criteria:**
- Màn cân nội địa in PGN theo version đang chọn.
- Màn cân xuất khẩu in PGN theo version đang chọn.
- Màn danh sách xe ra in PGN theo version đang chọn nếu có.
- Không bị quay về default sau khi người dùng đã chọn PGN ver 2 trong modal in.

**Verification:**
- Rà code call `printService.PrintAsync(dialogVm.CurrentTemplate, ...)`.
- Build pass.

**Files likely touched:**
- `src/StationApp.UI/ViewModels/WeighingViewModel.cs`
- `src/StationApp.UI/ViewModels/ExportWeighingViewModel.cs`
- `src/StationApp.UI/ViewModels/OutgoingVehicleListViewModel.cs`

### Task 9: Kiểm tra export Word/Excel

**Mô tả:** Đảm bảo nút `Tải` trong modal in vẫn dùng được với PGN ver 2.

**Acceptance criteria:**
- Tải Excel/Word theo PGN ver 2 không lỗi.
- Nội dung file dùng field của version đang chọn.
- Không lỗi encoding tiếng Việt.

**Verification:**
- Tải thử Excel/Word từ modal PGN ver 2.

**Files likely touched:**
- Có thể không cần sửa nếu exporter đã đọc template động.

## 9. Rủi ro và cách xử lý

| Rủi ro | Ảnh hưởng | Cách xử lý |
|---|---|---|
| Chưa có cột lưu `Mã in vỏ bao` trong DB cân | PGN ver 2 thiếu field mới | Thêm cột/bootstrap và cập nhật procedure nhận dữ liệu ERP |
| Procedure ERP thay đổi tham số có thể ảnh hưởng hệ thống ERP đang gọi | ERP gọi lỗi nếu thứ tự/tham số không tương thích | Thêm tham số optional cuối procedure nếu có thể, giữ backward compatible |
| Format `VehicleLine` của PGN ver 2 khác PGN ver 1 | Mẫu cũ có thể bị ảnh hưởng nếu đổi global | Xử lý format theo version/template trong composer, không thêm cột DB mới |
| Mẫu mới chỉ in text động nhưng seed nhầm text tĩnh | In đè lên phôi | Kiểm tra field list, chỉ seed field động |
| Tọa độ ban đầu chưa khớp phôi | Phiếu in lệch | Căn tương đối trước, sau đó chỉnh qua `Cấu hình in` |
| Chữ tiếng Việt lỗi font trên máy in kim | Phiếu khó đọc | Dùng GDI Unicode/Times New Roman, không raw text không dấu, không raster toàn trang |
| Người dùng đổi version rồi bấm in quá nhanh | Có thể in nhầm version cũ | Giữ logic `IsLoadingProfile` để disable nút in khi đang tải profile |
| Field dài như khách hàng/hàng hóa/xe mooc | Tràn ô trên phôi | Cấu hình `MaxLines`, `WrapMode`, width phù hợp và test bằng dữ liệu dài |

## 10. Checkpoint triển khai

Sau khi được duyệt, triển khai theo từng lát:

1. Bổ sung dữ liệu `Mã in vỏ bao` từ ERP xuống DB/composer.
2. Bổ sung composer `PackagePrinterName`; `VehicleLine` lấy từ `cut_orders.VehiclePlate`/`MoocNumber`, `ReceiverName` lấy từ `cut_orders.ReceiverName`.
3. Seed profile và field riêng cho `PGN ver 2 - A5 mẫu mới`.
4. Kiểm tra chọn version, preview, in GDI text Unicode.
5. Kiểm tra build và test composer/export nếu có thay đổi logic dữ liệu.
6. Căn tọa độ ban đầu theo mẫu Word, sau đó tinh chỉnh thực tế qua màn `Cấu hình in`.
