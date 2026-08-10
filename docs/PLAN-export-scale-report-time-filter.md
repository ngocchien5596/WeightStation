# Kế hoạch: Thêm bộ lọc thời gian cho Báo cáo xuất - XK

## 1. Bối cảnh

Màn **Báo cáo xuất - XK** hiện dùng `ExportScaleReportViewModel` và `ExportScaleReportView`. Bộ lọc đang có:

- Cắt lệnh xuất khẩu.
- Ngày báo cáo ca (`TargetDate`).

Phần service `ExportSummaryReportService.BuildExportScaleReportAsync` hiện tự quy đổi `TargetDate` thành khoảng từ 00:00:00 đến trước 00:00:00 ngày hôm sau, và có điểm đặc biệt:

- Nếu không chọn cắt lệnh: lọc chuyến xe theo `session.Weight2Time` trong ngày đó.
- Nếu có chọn cắt lệnh: lấy toàn bộ chuyến xe của cắt lệnh, không bị giới hạn bởi ngày.

Yêu cầu mới: thêm bộ lọc thời gian giống màn **Báo cáo xuất - NĐ**, tức có đủ **Từ giờ** và **Đến giờ**, gồm giờ/phút/giây + ngày.

Lưu ý nghiệp vụ quan trọng: **Ngày báo cáo ca vẫn phải giữ lại** vì đang ảnh hưởng đến phần thống kê sản lượng theo ca trong nội dung báo cáo. Bộ lọc thời gian mới chỉ dùng để lọc danh sách chuyến xe trong báo cáo, không thay thế ý nghĩa của `Ngày báo cáo ca`.

## 2. Mục tiêu

- Màn Báo cáo xuất - XK có bộ lọc thời gian giống Báo cáo xuất - NĐ.
- Giữ nguyên trường **Ngày báo cáo ca** để tính/hiển thị thống kê sản lượng theo ca trong nội dung báo cáo.
- Mặc định mở màn sẽ tự set theo ca hiện tại:
  - Ca 1: 06:00:00 - 13:59:59.
  - Ca 2: 14:00:00 - 21:59:59.
  - Ca 3: 22:00:00 - 05:59:59 ngày hôm sau.
- Khi nhấn **Xem** hoặc **Tải**, dữ liệu chuyến xe XK được lọc theo khoảng `FromTime` - `ToTime`.
- Giữ nguyên khả năng không chọn cắt lệnh để xem tất cả cắt lệnh xuất khẩu trong khoảng thời gian đã chọn.

## 3. Quyết định nghiệp vụ cần áp dụng

### 3.1. Khi không chọn cắt lệnh

Hiển thị tất cả chuyến xe xuất khẩu có:

- `Weight2Time` nằm trong khoảng `FromTime <= Weight2Time <= ToTime`.
- Cắt lệnh là xuất khẩu: `TransactionType = OUTBOUND`, `IsExportScale = true` hoặc `IsTemporaryExport = true`.
- Không lấy chuyến không lấy hàng, bị hủy, bị xóa mềm.

### 3.2. Khi có chọn cắt lệnh

Đề xuất áp dụng thống nhất với yêu cầu "bộ lọc thời gian": vẫn lọc chuyến xe của cắt lệnh đó theo khoảng `FromTime` - `ToTime`.

Lý do:

- Người dùng đang thao tác với báo cáo có bộ lọc thời gian thì kết quả cần phản ánh đúng khoảng đã chọn.
- Nếu muốn xem toàn bộ cắt lệnh, người dùng có thể chọn khoảng thời gian rộng.
- Tránh khác biệt khó hiểu giữa "có chọn cắt lệnh" và "không chọn cắt lệnh".

## 4. Phạm vi file cần sửa

### Application

- `src/StationApp.Application/DTOs/ExportScaleSummaryReportDocument.cs`
  - Thêm `FromTime`, `ToTime`.
  - Giữ `TargetDateForShiftReport` vì trường này phục vụ thống kê sản lượng theo ca trong nội dung báo cáo.

- `src/StationApp.Application/UseCases/ExportScaleSummaryReportUseCases.cs`
  - Đổi `BuildExportScaleSummaryReportUseCase.ExecuteAsync(...)` nhận thêm `DateTime fromTime, DateTime toTime`, nhưng vẫn giữ `DateTime? targetDateForShiftReport`.
  - Validate `fromTime <= toTime`.

- `src/StationApp.Application/Interfaces/IExportSummaryReportService.cs`
  - Đổi chữ ký `BuildExportScaleReportAsync(...)` để nhận `fromTime`, `toTime`.

### Infrastructure

- `src/StationApp.Infrastructure/Services/ExportSummaryReportServices.cs`
  - Đổi `BuildExportScaleReportAsync(...)`.
  - Không dùng `TargetDateForShiftReport` để lọc danh sách chuyến xe nữa.
  - Vẫn dùng `TargetDateForShiftReport` cho phần thống kê theo ca đang có trong báo cáo.
  - Lọc `session.Weight2Time.Value >= fromTime && session.Weight2Time.Value <= toTime` cho cả trường hợp có chọn cắt lệnh và không chọn cắt lệnh.
  - Gán `document.FromTime`, `document.ToTime` để Excel/preview hiển thị đúng khoảng lọc.
  - Rà lại header Excel:
    - Dòng/thông tin **Ngày báo cáo ca** vẫn giữ.
    - Bổ sung hoặc cập nhật dòng **Khoảng thời gian lọc** để hiển thị `Từ ... đến ...`.

### UI

- `src/StationApp.UI/ViewModels/ExportScaleReportViewModel.cs`
  - Thêm các property giống `ExportSummaryReportViewModel`:
    - `FromDate`, `ToDate`.
    - `FromHour`, `FromMinute`, `FromSecond`.
    - `ToHour`, `ToMinute`, `ToSecond`.
    - `HourOptions`, `MinuteOptions`, `SecondOptions`.
  - Thêm `ApplyCurrentShift()`.
  - Thêm `ResolveShiftRange(DateTime now)`.
  - Thêm `TryBuildDateRange(...)`.
  - Giữ property `TargetDate`.
  - Khi Preview/Export:
    - Validate thời gian.
    - Gọi use case bằng `fromTime`, `toTime`, và `TargetDate`.
  - File name tải về nên đổi từ `BaoCaoXuatXK_{cutOrderCode}_{TargetDate:yyyyMMdd}.xlsx` sang dạng có khoảng thời gian, ví dụ:
    - `BaoCaoXuatXK_{cutOrderCode}_{from:yyyyMMdd_HHmmss}_{to:yyyyMMdd_HHmmss}.xlsx`.

- `src/StationApp.UI/Views/ExportScaleReportView.xaml`
  - Giữ trường `Ngày báo cáo ca`.
  - Thêm cụm `Từ giờ` và `Đến giờ` giống `ExportSummaryReportView.xaml`.
  - Bố trí lại để gồm:
    - `Từ giờ`
    - `Đến giờ`
    - `Ngày báo cáo ca`
    - `Cắt lệnh xuất khẩu`
    - `Xem`, `Tải`
  - Đảm bảo dropdown cắt lệnh vẫn đủ rộng để đọc option.

## 5. Chi tiết triển khai theo task

### Task 1: Cập nhật contract báo cáo XK

**Mô tả:** Đổi use case/service để nhận khoảng thời gian rõ ràng, đồng thời vẫn giữ ngày báo cáo ca.

**Acceptance criteria:**

- `BuildExportScaleSummaryReportUseCase.ExecuteAsync` nhận `fromTime`, `toTime`, `targetDateForShiftReport`.
- Có validate `fromTime <= toTime`.
- Interface `IExportSummaryReportService` đồng bộ chữ ký mới.

**Verification:**

- Build project Application không lỗi.

### Task 2: Cập nhật query lấy dữ liệu chuyến xe XK

**Mô tả:** Sửa `BuildExportScaleReportAsync` để lọc bằng `Weight2Time` trong khoảng thời gian đã chọn.

**Acceptance criteria:**

- Không chọn cắt lệnh: lấy tất cả chuyến XK trong khoảng thời gian.
- Có chọn cắt lệnh: chỉ lấy chuyến của cắt lệnh đó trong khoảng thời gian.
- `TargetDateForShiftReport` không còn quyết định danh sách chuyến xe.
- `TargetDateForShiftReport` vẫn phục vụ phần thống kê sản lượng theo ca trong báo cáo.
- Không lấy dữ liệu xóa mềm, hủy, không lấy hàng.

**Verification:**

- Test tay bằng SQL hoặc app với một khoảng ngày/giờ hẹp và rộng.
- Kiểm tra số dòng preview khớp với dữ liệu `weighing_session_lines + weighing_sessions`.

### Task 3: Cập nhật ViewModel màn Báo cáo xuất - XK

**Mô tả:** Thêm bộ property giờ/ngày và validate giống Báo cáo xuất - NĐ.

**Acceptance criteria:**

- Khi mở màn, bộ lọc tự set theo ca hiện tại.
- `Ngày báo cáo ca` mặc định vẫn là hôm nay hoặc theo logic hiện tại.
- Nhập giờ/phút/giây không hợp lệ thì hiện toast cảnh báo.
- `Từ giờ > Đến giờ` thì không cho xem/tải.
- Preview và Export dùng đúng cùng một bộ lọc.

**Verification:**

- Chạy app, mở màn Báo cáo xuất - XK, kiểm tra giá trị mặc định theo giờ hiện tại.
- Thử nhập giờ lỗi và kiểm tra cảnh báo.

### Task 4: Cập nhật giao diện XAML

**Mô tả:** Thêm bộ lọc thời gian đầy đủ và giữ trường ngày báo cáo ca.

**Acceptance criteria:**

- Giao diện có `Từ giờ` và `Đến giờ`.
- Giao diện vẫn có `Ngày báo cáo ca`.
- Các control thời gian hiển thị cùng style với Báo cáo xuất - NĐ.
- Nút `XEM`, `TẢI` vẫn nằm cùng hàng, không vỡ layout.

**Verification:**

- Build WPF không lỗi XAML.
- Mở màn ở độ phân giải phổ biến để kiểm tra không overlap.

### Task 5: Cập nhật Excel/preview header nếu cần

**Mô tả:** Đảm bảo file preview/export thể hiện đúng khoảng thời gian lọc và vẫn giữ ngày báo cáo ca.

**Acceptance criteria:**

- Header báo cáo có thông tin `Ngày báo cáo ca`.
- Header báo cáo có thêm thông tin khoảng thời gian lọc `Từ ... đến ...`.
- Phần thống kê sản lượng theo ca vẫn dùng `Ngày báo cáo ca`.

**Verification:**

- Nhấn Xem, kiểm tra preview hiển thị đúng khoảng thời gian.
- Tải Excel, mở file và kiểm tra header.

### Task 6: Kiểm thử build và regression

**Mô tả:** Kiểm tra không ảnh hưởng Báo cáo xuất - NĐ và các báo cáo khác.

**Acceptance criteria:**

- Build `StationApp.UI` thành công.
- Báo cáo xuất - NĐ vẫn build và hoạt động như cũ.
- Báo cáo xuất - XK preview/export được với:
  - Không chọn cắt lệnh.
  - Có chọn cắt lệnh.
  - Khoảng giờ trong ngày.
  - Khoảng giờ qua ngày.

**Verification command:**

```powershell
dotnet build src\StationApp.UI\StationApp.UI.csproj -p:SkipDatabaseSchemaUpdate=true -p:StationAppBuildRoot=G:\Source-code\pmcan_C#\._verify_build\
```

## 6. Rủi ro và cách xử lý

| Rủi ro | Mức độ | Cách xử lý |
|---|---:|---|
| Đổi chữ ký service làm lỗi nơi gọi khác | Trung bình | Dùng `rg BuildExportScaleReportAsync/ExecuteAsync` để cập nhật hết call site |
| Người dùng đang quen chọn cắt lệnh là ra toàn bộ chuyến | Trung bình | Ghi rõ hành vi mới: có chọn cắt lệnh vẫn lọc theo thời gian; nếu cần toàn bộ thì chọn khoảng rộng |
| Header Excel đang phụ thuộc `TargetDateForShiftReport` | Trung bình | Giữ `TargetDateForShiftReport`, bổ sung `FromTime/ToTime` để không phá phần thống kê ca |
| Text tiếng Việt bị lỗi encoding khi sửa XAML/C# | Cao | Chỉ sửa file bằng UTF-8, ưu tiên text Unicode escaped nếu file đang có lịch sử lỗi encoding |

## 7. Câu hỏi cần chốt

1. Khi chọn một cắt lệnh XK, bộ lọc thời gian có áp dụng không?
   - Đề xuất: **Có áp dụng**, để đúng ý "thêm bộ lọc thời gian".
2. Header báo cáo XK sau sửa nên hiển thị đồng thời:
   - `Ngày báo cáo ca: dd/MM/yyyy`
   - `Khoảng thời gian lọc: Từ HH:mm:ss dd/MM/yyyy đến HH:mm:ss dd/MM/yyyy`
