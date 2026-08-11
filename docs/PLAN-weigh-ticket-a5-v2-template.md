# Plan tạo version phiếu cân A5 mới chạy song song

## 1. Mục tiêu

Tạo thêm một version phiếu cân mới dựa trên file `Mau_A5_Phieu_Can_EDITABLE_v4.docx`, dùng song song với version phiếu cân hiện tại. Người dùng vẫn in được phiếu cân cũ, đồng thời có thể chọn phiếu cân A5 mẫu mới trong màn in/cấu hình in.

## 2. Hiện trạng code đã rà soát

- Phiếu cân hiện tại dùng `PrintDocumentKind.WeighTicket`.
- Template phiếu in đang được quản lý bằng `PrintTemplateProvider`, có profile lưu trong bảng `PrintTemplateProfiles`.
- Hiện đang seed profile `PC ver 1` cho phiếu cân.
- `PC ver 1` hiện là kiểu in đầy đủ: in cả text tĩnh và text động theo template hiện tại.
- Mỗi template gồm kích thước trang, offset, danh sách field theo tọa độ mm.
- Màn in đã có dropdown chọn profile, lưu mặc định profile, preview, xuất Word/Excel theo template đang chọn.
- `WeighTicketPrintComposer` hiện cấp dữ liệu chính cho phiếu cân qua `Fields`.

## 3. Thông tin mẫu Word

File `Mau_A5_Phieu_Can_EDITABLE_v4.docx` là A5 ngang:

- Kích thước trang: khoảng `210 x 148 mm`.
- Lề: khoảng `5-6 mm`.
- Các trường nhìn thấy trên mẫu:
  - `BIỂN SỐ XE`
  - `Số phiếu`
  - `TEM XE`
  - `Ngày giờ cân lần 1`
  - `TEM MOOC`
  - `KHÁCH HÀNG`
  - `XUẤT/NHẬP`
  - `Ngày giờ cân lần 2`
  - `KHỐI LƯỢNG LẦN 1`
  - `SỐ BAO`
  - `KHỐI LƯỢNG LẦN 2`
  - `MÃ SỐ`
  - `KHỐI LƯỢNG HÀNG`
  - `SỐ LÔ`
  - `HÀNG HÓA`
  - checkbox `Người nhận`, `Người giao`
  - chữ ký `Nhân viên trạm cân`

## 4. Nguyên tắc thiết kế

- Không thay thế hoặc xóa `PC ver 1`.
- Thêm profile/version mới, đề xuất tên hiển thị: `PC ver 2 - A5 mẫu mới`.
- Nếu chưa chọn profile mới thì luồng in hiện tại giữ nguyên.
- Sau khi triển khai vẫn giữ mẫu cũ làm mặc định, chưa tự chuyển mặc định sang mẫu mới.
- Cơ chế in phải phân biệt theo version:
  - `PC ver 1`: giữ nguyên kiểu in hiện tại, in đầy đủ cả text tĩnh và text động.
  - `PC ver 2 - A5 mẫu mới`: chỉ in text động lên phôi in sẵn, không in text/line/logo tĩnh.
- Có thể cấu hình căn chỉnh field riêng cho version mới trong màn cấu hình in.
- Dữ liệu dùng chung composer hiện tại, chỉ bổ sung field còn thiếu.
- Không cần migration DB nếu tiếp tục dùng cơ chế `PrintTemplateProfiles` hiện có.
- Bố cục/căn chỉnh phải bám sát file Word mẫu để phiếu tạo ra giống phôi mới.
- Khi in thực tế chỉ in text động lên phôi in sẵn; không in lại logo, tiêu đề, đường kẻ, nhãn tĩnh hoặc các ký tự đã có sẵn trên phôi.
- Cách in cần đi theo cơ chế đang in ổn cho phiếu giao nhận: ưu tiên text Unicode thật, giữ tiếng Việt có dấu, không chuyển sang không dấu, không rasterize cả phiếu thành ảnh.

## 5. Mapping dữ liệu đề xuất

| Trường trên mẫu mới | Field code đề xuất | Nguồn dữ liệu |
|---|---|---|
| Số phiếu | `TicketNo` | Số phiếu cân hiện tại, format qua `BusinessNumberFormatter` |
| Biển số xe | `VehiclePlate` | Biển số xe của lượt cân, kèm số mooc trong ngoặc nếu có. Ví dụ: `14R-3584 (14RM-3642)` |
| Tem xe | `VehicleRegistrationNo` | Số đăng kiểm xe/tem xe hiện tại |
| Tem mooc | `MoocRegistrationNo` | Số đăng kiểm mooc/tem mooc hiện tại |
| Khách hàng | `CustomerName` | Khách hàng của cắt lệnh/lượt cân |
| Xuất/Nhập | `TransactionTypeDisplayShort` | `Inbound` => `Nhập`, `Outbound` => `Xuất` |
| Ngày giờ cân lần 1 | `Weight1DateTime` | `Weight1Time`, format `dd/MM/yyyy HH:mm:ss` |
| Ngày giờ cân lần 2 | `Weight2DateTime` | `Weight2Time`, format `dd/MM/yyyy HH:mm:ss` |
| Khối lượng lần 1 | `Weight1` | Giá trị cân lần 1, kg |
| Khối lượng lần 2 | `Weight2` | Giá trị cân lần 2, kg |
| Khối lượng hàng | `NetWeight` | Trọng lượng hàng, kg |
| Số bao | `BagCount` | Số bao thực tế/in được của phiếu |
| Mã số | `CutOrderCode` | Mã cắt lệnh |
| Số lô | `LotNo` | Số lô |
| Hàng hóa | `ProductName` | Tên sản phẩm/hàng hóa |
| Nhân viên trạm cân | `PrintedBy` hoặc `Weight2User` | Cần chốt |

## 6. Các field cần bổ sung vào composer

Trong `WeighTicketPrintComposer`, bổ sung các field mới để template A5 mới dùng được:

- `TransactionTypeDisplayShort`: trả `Nhập` hoặc `Xuất`.
- `CutOrderCode`: mã cắt lệnh hiển thị ở trường `Mã số`.
- `BagCount`: số bao phù hợp với phiếu cân.
- `Weight1`: giá trị cân lần 1 theo kg.
- `Weight2`: giá trị cân lần 2 theo kg.
- Có thể giữ `GrossWeight`/`EmptyWeight` cho template cũ, nhưng template mới dùng trực tiếp `Weight1`/`Weight2`.

## 7. Task triển khai

### Task 1: Chốt mapping và kiểm tra dữ liệu còn thiếu

**Mô tả:** Rà soát `CutOrder`, `WeighTicket`, `WeighingSession` và các luồng in nội địa/xuất khẩu/mỏ để xác định chính xác field nào đã có, field nào cần fallback.

**Acceptance criteria:**
- Có danh sách mapping cuối cùng cho toàn bộ field mẫu A5 mới.
- Biết rõ `Mã số`, `Số bao`, `Nhân viên trạm cân`, checkbox `Người nhận/Người giao` lấy từ đâu.

**Verification:**
- So sánh 1 lượt cân nội địa và 1 lượt cân xuất khẩu trên preview.

**Files likely touched:** Chưa sửa code ở task này.

### Task 2: Bổ sung field dữ liệu phiếu cân

**Mô tả:** Cập nhật `WeighTicketPrintComposer` để cấp đủ field cho template mới mà không ảnh hưởng template cũ.

**Acceptance criteria:**
- `TransactionTypeDisplayShort` hiển thị `Nhập`/`Xuất`.
- `CutOrderCode` hiển thị mã cắt lệnh.
- `Weight1`, `Weight2`, `BagCount` có format thống nhất.
- Template cũ vẫn in bình thường vì các field cũ không đổi.

**Verification:**
- Unit test hoặc sample preview kiểm tra field values.
- `dotnet build` pass.

**Files likely touched:**
- `src/StationApp.Application/Printing/PrintContracts.cs`
- Có thể thêm test trong `tests/StationApp.Application.Tests`

### Task 3: Seed profile `PC ver 2 - A5 mẫu mới`

**Mô tả:** Thêm danh sách field động theo tọa độ mm dựa trên bố cục `Mau_A5_Phieu_Can_EDITABLE_v4.docx`. Chỉ đưa các field dữ liệu động vào profile in thực tế; không seed các text/line/logo tĩnh vì các phần đó đã in sẵn trên phôi.

**Acceptance criteria:**
- `GetProfilesAsync(PrintDocumentKind.WeighTicket)` trả thêm profile mới.
- Profile cũ `PC ver 1` vẫn tồn tại.
- Profile mới có kích thước A5 ngang và field đúng tên.
- Profile mới chỉ chứa các trường động cần in lên phôi.
- Khi app đã có dữ liệu profile cũ trong DB, bootstrap profile mới không làm mất profile cũ.

**Verification:**
- Mở màn in phiếu cân thấy chọn được `PC ver 2 - A5 mẫu mới`.
- Preview có đủ field và không vỡ bố cục.

**Files likely touched:**
- `src/StationApp.UI/Printing/PrintTemplateProvider.cs`

### Task 3.1: Phân biệt chế độ in theo profile phiếu cân

**Mô tả:** Bổ sung metadata hoặc quy ước profile để pipeline in biết `PC ver 1` là full template, còn `PC ver 2 - A5 mẫu mới` là overlay text động trên phôi in sẵn.

**Acceptance criteria:**
- Chọn `PC ver 1` vẫn in cả nội dung tĩnh và động như hiện tại.
- Chọn `PC ver 2 - A5 mẫu mới` chỉ in các field động.
- Không dùng chung danh sách field tĩnh của `PC ver 1` cho `PC ver 2`.
- Cấu hình offset và vị trí field của hai version độc lập nhau.

**Verification:**
- In thử `PC ver 1`: vẫn có logo/text tĩnh/nhãn như trước.
- In thử `PC ver 2`: chỉ thấy dữ liệu động trên giấy trắng/PDF; khi đặt lên phôi thì khớp vị trí.

**Files likely touched:**
- `src/StationApp.Application/Printing/PrintContracts.cs`
- `src/StationApp.UI/Printing/PrintTemplateProvider.cs`
- `src/StationApp.UI/Printing/WpfPrintService.cs`
- `src/StationApp.UI/Printing/PrintOverlayRenderer.cs`

### Task 4: Cập nhật tên field trong màn cấu hình in

**Mô tả:** Bổ sung label tiếng Việt cho các field mới để màn căn chỉnh in dễ dùng.

**Acceptance criteria:**
- Các field mới không hiển thị tên code khó hiểu.
- Người dùng thấy các tên như `Xuất/Nhập`, `Mã số`, `Số bao`, `Cân lần 1`, `Cân lần 2`.

**Verification:**
- Mở cấu hình phiếu cân và kiểm tra danh sách field.

**Files likely touched:**
- `src/StationApp.UI/ViewModels/Dialogs/PrintOptionsDialogViewModel.cs`

### Task 5: Kiểm tra preview, in, xuất Word/Excel

**Mô tả:** Đảm bảo template mới đi qua toàn bộ pipeline hiện có: preview, in thực tế, xuất Word, xuất Excel.

**Acceptance criteria:**
- Preview/cấu hình dùng được để căn field theo mẫu Word/phôi mới.
- In ra PDF/physical printer chỉ có text động, đúng vị trí trên phôi.
- Text tiếng Việt còn dấu, không lỗi font, không bị in dạng ký tự mojibake.
- Không dùng cách chuyển toàn bộ phiếu thành bitmap/ảnh khiến chữ trên máy in kim bị chấm, mờ.
- Xuất Word/Excel không mất field.
- Chọn lại `PC ver 1` vẫn hoạt động như cũ.

**Verification:**
- `dotnet build`
- In thử 1 phiếu nội địa và 1 phiếu xuất khẩu bằng `Microsoft Print to PDF`.
- Nếu có máy in thật, in thử 1 bản để kiểm tra vị trí.

**Files likely touched:**
- Có thể không cần sửa thêm nếu pipeline hiện tại đã dùng template động.

### Task 6: Cập nhật tài liệu vận hành

**Mô tả:** Ghi chú cách chọn version phiếu cân mới và cách đặt mặc định.

**Acceptance criteria:**
- Người dùng biết vào màn cấu hình in để chọn/căn chỉnh `PC ver 2 - A5 mẫu mới`.
- Có ghi chú không xóa `PC ver 1`.

**Verification:**
- Review tài liệu cùng người dùng.

**Files likely touched:**
- `docs/` hoặc `SRSdocs/` tùy vị trí tài liệu vận hành hiện có.

## 8. Rủi ro và cách xử lý

| Rủi ro | Ảnh hưởng | Cách xử lý |
|---|---|---|
| Mẫu Word có bố cục phức tạp, nhiều logo/watermark/table | Preview/căn chỉnh lệch so với phôi | Dùng Word làm mẫu đo tọa độ, nhưng profile in chỉ chứa field động để in lên phôi sẵn |
| Field `Số bao` khác nhau giữa xuất bao, xuất rời, nhập hàng | Hiển thị sai số bao | Chốt quy tắc trước khi code |
| Một lượt cân gắn nhiều cắt lệnh | `Mã số` không rõ hiển thị mã nào | Chốt hiển thị tất cả mã hay mã chính |
| App đã lưu profile cũ trong DB | Seed profile mới không xuất hiện nếu logic chỉ seed khi rỗng | Sửa seed để đảm bảo thêm profile mới nếu chưa có key |
| Hai version phiếu cân có cơ chế in khác nhau | Nếu xử lý chung sẽ làm mẫu cũ mất text tĩnh hoặc mẫu mới in thừa text tĩnh lên phôi | Thêm metadata/quy ước rõ cho profile: full template với `PC ver 1`, dynamic overlay với `PC ver 2` |
| Dữ liệu tiếng Việt trên máy in kim | Có thể lệch font, mất dấu hoặc chữ bị chấm nếu in sai pipeline | Bám theo cách in phiếu giao nhận hiện đang ổn: in text Unicode động, không chuyển không dấu, không render cả phiếu thành ảnh |

## 9. Câu hỏi cần chốt trước khi code

Đã chốt:

1. `Số phiếu` trên mẫu mới là số phiếu cân `PC...`.
2. `Mã số` lấy mã cắt lệnh. Nếu 1 lượt cân có nhiều cắt lệnh thì hiển thị tất cả mã cắt lệnh.
3. `Số bao` lấy theo số bao thực tế.
4. Checkbox `Người nhận`/`Người giao` không cần tự tích.
5. `Nhân viên trạm cân` lấy tên hiển thị của người nhấn nút `IN PC`.
6. File Word/phôi mới có đủ logo và nội dung tĩnh, nhưng khi in thực tế phần mềm chỉ in các text động lên phôi in sẵn.
7. Version mới chỉ thêm để dùng song song, chưa đặt làm mặc định; mặc định vẫn dùng mẫu cũ.
8. `PC ver 1` giữ cơ chế in đầy đủ cả text tĩnh và text động; `PC ver 2 - A5 mẫu mới` dùng cơ chế chỉ in text động.
