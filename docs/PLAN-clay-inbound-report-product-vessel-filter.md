# PLAN - Báo cáo cân hàng mỏ sét: lọc Sản phẩm và Chuyến tàu theo khoảng thời gian

## 1. Mục tiêu

Nâng cấp màn `Báo cáo cân hàng mỏ sét` để người dùng tìm chuyến tàu dễ hơn khi cùng một tàu có nhiều lượt phát sinh trong tháng và khác sản phẩm.

Yêu cầu đã chốt:

- Bỏ ô tìm kiếm theo `Số xe`.
- Thêm trường tìm kiếm/chọn `Sản phẩm`, đặt bên trái trường `Chuyến tàu`.
- `Sản phẩm` lọc theo thông tin sản phẩm gắn trên chuyến tàu/cắt lệnh tàu.
- Danh sách `Chuyến tàu` phải phụ thuộc vào `Khoảng thời gian` và `Sản phẩm` đã chọn.
- Nếu không chọn `Chuyến tàu`, báo cáo vẫn hiển thị tất cả chuyến xe phù hợp với khoảng thời gian và sản phẩm.
- Nếu chọn `Chuyến tàu`, báo cáo chỉ hiển thị chuyến xe thuộc chuyến tàu đó trong khoảng thời gian đã chọn.

## 2. Hiện trạng code

Các file chính:

- `src/StationApp.UI/Views/ClayInboundReportView.xaml`
- `src/StationApp.UI/Views/ClayInboundReportView.xaml.cs`
- `src/StationApp.UI/ViewModels/ClayInboundReportViewModel.cs`
- `src/StationApp.Application/DTOs/ClayInboundReportDtos.cs`
- `src/StationApp.Application/Interfaces/IClayInboundReportService.cs`
- `src/StationApp.Application/UseCases/ClayInboundReportUseCases.cs`
- `src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs`

Hiện tại:

- `ClayInboundReportFilter` còn trường `VehicleKeyword`.
- UI có ô `Số xe`, binding vào `VehicleSearchText`.
- `Chuyến tàu` đang load từ `GetVesselOptionsAsync(CancellationToken)` và lấy 300 chuyến tàu mới nhất, chưa phụ thuộc khoảng thời gian/sản phẩm.
- Report data query đã join `WeighingSessionLines`, `WeighingSessions`, `CutOrders` và có filter `VesselCutOrderId`.
- Preview/Excel đã có cột `Hoàn`, `Thực nhập`, và dòng `Chuyến tàu` khi có chọn chuyến tàu.

## 3. Quyết định nghiệp vụ

- `Sản phẩm` dùng để lọc theo `CutOrders.ProductCode/ProductName` của chuyến tàu, không lọc theo từng dòng chuyến xe.
- Danh sách `Chuyến tàu` chỉ hiển thị các tàu có ít nhất một chuyến xe hoàn thành trong khoảng thời gian đã chọn.
- Nếu đã chọn `Sản phẩm`, danh sách `Chuyến tàu` chỉ hiển thị các tàu có sản phẩm tương ứng trên chuyến tàu.
- Khi khoảng thời gian hoặc sản phẩm thay đổi:
  - Làm mới danh sách chuyến tàu.
  - Nếu chuyến tàu đang chọn không còn phù hợp, clear lựa chọn chuyến tàu.
- Khi xuất/xem báo cáo:
  - Không còn lọc theo số xe.
  - Áp dụng filter sản phẩm trước.
  - Nếu chọn chuyến tàu thì lọc thêm theo chuyến tàu.

## 4. Thiết kế dữ liệu/API nội bộ

### 4.1. DTO filter báo cáo

Sửa `ClayInboundReportFilter`:

- Bỏ hoặc ngừng dùng `VehicleKeyword`.
- Thêm `ProductCode` hoặc `ProductKeyword`.

Đề xuất:

```csharp
public sealed record ClayInboundReportFilter(
    DateTime FromTime,
    DateTime ToTime,
    string? ProductCode,
    Guid? VesselCutOrderId = null);
```

Lý do chọn `ProductCode`:

- Dropdown sản phẩm có `Code` ổn định.
- Tránh lọc mơ hồ theo text khi tên sản phẩm có dấu/ký tự dài.

### 4.2. DTO filter danh sách chuyến tàu

Thêm DTO mới:

```csharp
public sealed record ClayInboundVesselLookupFilter(
    DateTime FromTime,
    DateTime ToTime,
    string? ProductCode);
```

Mục đích: service lookup chuyến tàu nhận đủ điều kiện ngày và sản phẩm.

### 4.3. Interface service

Sửa:

```csharp
Task<IReadOnlyList<ReportLookupOptionDto>> GetVesselOptionsAsync(
    ClayInboundVesselLookupFilter filter,
    CancellationToken ct);
```

Giữ `GetProductOptionsAsync` để load dropdown sản phẩm.

## 5. Kế hoạch thực hiện

### Task 1 - Sửa contract DTO và service interface

Mô tả:

- Cập nhật `ClayInboundReportFilter`.
- Thêm `ClayInboundVesselLookupFilter`.
- Sửa chữ ký `GetVesselOptionsAsync`.
- Cập nhật use case `GetClayInboundReportLookupOptionsUseCase`.

Acceptance criteria:

- Không còn contract filter theo `VehicleKeyword` cho báo cáo mỏ sét.
- Có contract lookup chuyến tàu theo `FromTime/ToTime/ProductCode`.
- Build Application project không lỗi.

Files dự kiến:

- `src/StationApp.Application/DTOs/ClayInboundReportDtos.cs`
- `src/StationApp.Application/Interfaces/IClayInboundReportService.cs`
- `src/StationApp.Application/UseCases/ClayInboundReportUseCases.cs`

### Task 2 - Sửa query báo cáo và query lookup chuyến tàu

Mô tả:

- Trong `BuildAsync`, thêm điều kiện lọc sản phẩm theo `vessel.ProductCode`.
- Bỏ `MatchesFilter` theo số xe.
- Sửa `GetVesselOptionsAsync` để join `WeighingSessionLines`, `WeighingSessions`, `CutOrders`.
- Chỉ trả về chuyến tàu có chuyến xe phát sinh trong khoảng thời gian đã chọn.
- Nếu `ProductCode` có giá trị, lọc `CutOrders.ProductCode == ProductCode`.

Acceptance criteria:

- Không chọn sản phẩm/chuyến tàu: lấy tất cả chuyến xe trong khoảng thời gian.
- Chọn sản phẩm: chỉ lấy chuyến xe thuộc các chuyến tàu có sản phẩm đó.
- Chọn chuyến tàu: chỉ lấy chuyến xe thuộc tàu đó.
- Dropdown chuyến tàu không hiển thị tàu không có chuyến xe trong khoảng thời gian.

Files dự kiến:

- `src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs`

### Task 3 - Sửa ViewModel filter

Mô tả:

- Bỏ `VehicleSearchText`.
- Thêm `ProductOptions`, `ProductOptionsView`, `ProductSearchText`, `SelectedProduct`.
- Dùng `GetProductsAsync` load dropdown sản phẩm.
- Khi `FromDate/FromHour/FromMinute/FromSecond/ToDate/ToHour/ToMinute/ToSecond` hoặc `SelectedProduct` thay đổi, refresh lại `VesselOptions`.
- Khi refresh vessel, nếu `SelectedVessel` không còn trong danh sách mới thì clear `SelectedVessel` và `VesselSearchText`.
- Khi build report filter, truyền `ProductCode` và `VesselCutOrderId`.

Acceptance criteria:

- Vào màn báo cáo, sản phẩm trống và chuyến tàu trống.
- Đổi khoảng ngày làm danh sách chuyến tàu cập nhật theo khoảng mới.
- Chọn sản phẩm làm danh sách chuyến tàu cập nhật theo sản phẩm đó.
- Không còn binding/property `VehicleSearchText` dùng trên UI báo cáo mỏ sét.

Files dự kiến:

- `src/StationApp.UI/ViewModels/ClayInboundReportViewModel.cs`

### Task 4 - Sửa XAML filter bar

Mô tả:

- Xóa block `Số xe`.
- Thêm ComboBox `Sản phẩm` bên trái `Chuyến tàu`.
- Dùng style/handler lookup hiện có (`ReportLookupComboStyle`, `LookupComboBox_Loaded`, `LookupComboBox_SelectionChanged`).
- Giữ layout gọn, không làm cụm nút `XEM/IN/TẢI` bị tràn.

Acceptance criteria:

- Thứ tự filter: `Từ giờ`, `Đến giờ`, `Sản phẩm`, `Chuyến tàu`, nút thao tác.
- `Sản phẩm` gõ để lọc giống `Chuyến tàu`.
- Không còn ô `Số xe`.

Files dự kiến:

- `src/StationApp.UI/Views/ClayInboundReportView.xaml`
- `src/StationApp.UI/Views/ClayInboundReportView.xaml.cs` nếu cần tách handler theo nhiều combobox.

### Task 5 - Kiểm tra preview/export sau khi đổi filter

Mô tả:

- Đảm bảo `PreviewSummaryText` hiển thị đúng khi chọn sản phẩm/chuyến tàu.
- Excel vẫn có dòng `Chuyến tàu` khi chọn chuyến tàu.
- Các cột `Hoàn`, `Thực nhập` và tổng cộng không đổi logic.

Acceptance criteria:

- Xem trước không lỗi khi không chọn sản phẩm/chuyến tàu.
- Xem trước không lỗi khi chọn sản phẩm nhưng không chọn chuyến tàu.
- Xem trước không lỗi khi chọn cả sản phẩm và chuyến tàu.
- File Excel xuất được với cùng điều kiện filter.

Files dự kiến:

- `src/StationApp.UI/ViewModels/ClayInboundReportViewModel.cs`
- `src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs`

## 6. Verification

Chạy build:

```powershell
dotnet build src\StationApp.UI\StationApp.UI.csproj --no-restore
```

Kiểm tra thủ công:

- Mở màn `Báo cáo cân hàng mỏ sét`.
- Chọn khoảng ngày có nhiều chuyến tàu.
- Không chọn sản phẩm: dropdown chuyến tàu hiển thị các tàu có phát sinh trong khoảng ngày.
- Chọn một sản phẩm: dropdown chuyến tàu thu hẹp đúng theo sản phẩm.
- Chọn chuyến tàu: preview/export chỉ có chuyến xe của tàu đó.
- Clear chuyến tàu: preview/export hiển thị tất cả chuyến xe theo khoảng ngày và sản phẩm.

## 7. Rủi ro và lưu ý

| Rủi ro | Ảnh hưởng | Cách xử lý |
|---|---|---|
| Refresh vessel quá nhiều khi người dùng chỉnh từng giờ/phút/giây | Có thể query DB nhiều lần | Chỉ refresh khi date range hợp lệ; cân nhắc debounce nếu UI lag |
| Chuyến tàu đang chọn không còn phù hợp sau khi đổi sản phẩm/ngày | Report lọc sai hoặc dropdown hiển thị lựa chọn cũ | Sau mỗi lần reload vessel, clear selected nếu không còn trong danh sách |
| Tên sản phẩm trùng nhau | Chọn sai sản phẩm nếu lọc theo text | Dùng `ProductCode` làm giá trị filter |
| Tàu chưa có chuyến xe trong khoảng ngày | Không xuất hiện trong dropdown | Đây là hành vi mong muốn theo yêu cầu |

## 8. Open questions

Không còn câu hỏi mở. Đã chốt `Sản phẩm` lọc theo thông tin gắn vào chuyến tàu/cắt lệnh tàu.
