# Plan: Màn Lịch sử cân (PM cũ)

## 1. Mục tiêu

Tạo chức năng **Lịch sử cân (PM cũ)** trong nhóm **Cấu hình hệ thống** để tài khoản Admin tra cứu dữ liệu cân lịch sử từ database ngoài `XIM_TRANSPORT`, bảng `dbo.Datacan`.

Chức năng chỉ đọc dữ liệu. Không ghi ngược vào database cũ, không đồng bộ dữ liệu cũ vào `StationAppLocal`, và không dùng dữ liệu này để tính KPI, báo cáo hoặc chứng từ vận hành hiện tại.

## 2. Phạm vi đã triển khai

- Thêm submenu **Cấu hình hệ thống > Lịch sử cân (PM cũ)** trên thanh điều hướng trái.
- Chỉ tài khoản có role `ADMIN` nhìn thấy và truy cập được chức năng này.
- Đọc dữ liệu từ connection string `ConnectionStrings:ExternalDatacanConnection`.
- Hiển thị dữ liệu từ `XIM_TRANSPORT.dbo.Datacan` trên `DataGrid` read-only.
- Sắp xếp dữ liệu theo `Ngayra DESC`, các dòng chưa có `Ngayra` được đưa xuống sau.
- Có bộ lọc tìm kiếm theo:
  - `Soxe` - Biển số xe.
  - `Hanghoa` - Sản phẩm/Hàng hóa.
  - `Khachhang` - Khách hàng.
- Có nút **TÌM KIẾM** và **TẢI LẠI**.
- Có phân trang cố định ở cuối màn hình với nút `<` và `>`.
- Mặc định mỗi trang tải 100 dòng, service giới hạn `pageSize` trong khoảng 20 đến 500 để tránh load quá nhiều dữ liệu.
- Bật virtualization cho grid để giảm tải UI khi dữ liệu lớn.

## 3. Mapping dữ liệu

| Cột trên UI | Cột nguồn | Ghi chú |
|---|---|---|
| Số phiếu | `dbo.Datacan.Sophieu` | Text |
| Biển số xe | `dbo.Datacan.Soxe` | Text, căn trái |
| Loại | `dbo.Datacan.Nhomhang` | Text |
| Khách hàng | `dbo.Datacan.Khachhang` | Text |
| Hàng hóa | `dbo.Datacan.Hanghoa` | Text |
| Giờ cân lần 1 | `dbo.Datacan.Ngayvao` | Format `dd/MM/yyyy HH:mm:ss` |
| Giờ cân lần 2 | `dbo.Datacan.Ngayra` | Format `dd/MM/yyyy HH:mm:ss` |
| Cân lần 1 | `dbo.Datacan.KLxe` | Numeric, format `N0` |
| Cân lần 2 | `dbo.Datacan.KLTong` | Numeric, format `N0` |
| Trọng lượng hàng | `dbo.Datacan.KLhang` | Numeric, format `N0` |
| Người cân | `dbo.Datacan.Nvc` | Text |

## 4. Cấu hình

Connection string được cấu hình trong `src/StationApp.UI/appsettings.json`:

```json
{
  "ConnectionStrings": {
    "ExternalDatacanConnection": "Data Source=10.0.0.1;Initial Catalog=XIM_TRANSPORT;User Id=sa;Password=***;TrustServerCertificate=True;Encrypt=False;"
  }
}
```

Khuyến nghị khi vận hành thật:

- Không dùng tài khoản `sa` dài hạn.
- Tạo một SQL login/user read-only riêng cho phần mềm cân.
- Chỉ cấp quyền `SELECT` trên `dbo.Datacan`.

Ví dụ:

```sql
USE [XIM_TRANSPORT];
CREATE USER [stationapp_readonly] FOR LOGIN [stationapp_readonly];
GRANT SELECT ON dbo.Datacan TO [stationapp_readonly];
```

## 5. Thành phần code

### 5.1 DTO

File: `src/StationApp.Application/DTOs/Dtos.cs`

- `ExternalDatacanRecordDto`
- `ExternalDatacanQueryResult`

### 5.2 Interface

File: `src/StationApp.Application/Interfaces/IExternalDatacanQueryService.cs`

Interface:

```csharp
Task<ExternalDatacanQueryResult> GetLatestAsync(
    string? vehiclePlateKeyword,
    string? productKeyword,
    string? customerKeyword,
    int pageIndex,
    int pageSize,
    CancellationToken cancellationToken);
```

### 5.3 Service đọc DB ngoài

File: `src/StationApp.Infrastructure/Services/ExternalDatacanQueryService.cs`

Service dùng `Microsoft.Data.SqlClient`, query có parameter, không nối chuỗi SQL động.

Query chính:

```sql
SELECT
    Sophieu AS TicketNo,
    Soxe AS VehiclePlate,
    Nhomhang AS GroupName,
    Khachhang AS CustomerName,
    Hanghoa AS ProductName,
    Ngayvao AS Weight1Time,
    Ngayra AS Weight2Time,
    KLxe AS Weight1,
    KLTong AS Weight2,
    KLhang AS NetWeight,
    Nvc AS OperatorName
FROM dbo.Datacan
WHERE (@VehiclePlateKeyword IS NULL OR Soxe LIKE N'%' + @VehiclePlateKeyword + N'%')
  AND (@ProductKeyword IS NULL OR Hanghoa LIKE N'%' + @ProductKeyword + N'%')
  AND (@CustomerKeyword IS NULL OR Khachhang LIKE N'%' + @CustomerKeyword + N'%')
ORDER BY CASE WHEN Ngayra IS NULL THEN 1 ELSE 0 END, Ngayra DESC
OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
```

### 5.4 ViewModel

File: `src/StationApp.UI/ViewModels/Settings/ExternalDatacanViewModel.cs`

State chính:

- `Records`
- `VehiclePlateKeyword`
- `ProductKeyword`
- `CustomerKeyword`
- `PageIndex`
- `PageSize`
- `HasNextPage`
- `PageSummary`
- `ErrorMessage`
- `IsLoading`

Command chính:

- `SearchCommand`
- `RefreshCommand`
- `PreviousPageCommand`
- `NextPageCommand`

### 5.5 View

File: `src/StationApp.UI/Views/Settings/ExternalDatacanView.xaml`

Giao diện hiện tại:

- Không hiển thị header phụ trong nội dung màn, vì tiêu đề chính đã nằm ở header trang.
- Vùng tìm kiếm có label rõ cho từng ô: **Biển số xe**, **Sản phẩm**, **Khách hàng**.
- Nút **TÌM KIẾM** và **TẢI LẠI** dùng style giống các màn danh mục.
- `DataGrid` chỉ đọc, có virtualization.
- Thanh phân trang cố định ở bottom.

### 5.6 Điều hướng và phân quyền

Các file liên quan:

- `src/StationApp.UI/ViewModels/MainViewModel.cs`
- `src/StationApp.UI/Views/MainWindow.xaml`
- `src/StationApp.UI/ViewModels/SettingsViewModel.cs`
- `src/StationApp.UI/Views/SettingsView.xaml`
- `src/StationApp.UI/Resources/WorkflowTextResources.xaml`

Quy tắc:

- Navigation key: `Settings_ExternalDatacan`.
- Tab index trong `SettingsViewModel`: `7`.
- Quyền hiển thị: `StationAuthorization.IsAdmin(...)`.
- Người dùng không phải Admin không thấy submenu và không truy cập được tab.

## 6. Test plan

### Functional

- Admin đăng nhập thấy menu **Cấu hình hệ thống > Lịch sử cân (PM cũ)**.
- User thường không thấy menu này.
- Mở màn lần đầu tự tải trang dữ liệu đầu tiên.
- Dữ liệu sắp xếp theo `Ngayra DESC`.
- Nhập biển số xe và bấm **TÌM KIẾM**: chỉ hiển thị dòng có `Soxe` chứa từ khóa.
- Nhập sản phẩm và bấm **TÌM KIẾM**: chỉ hiển thị dòng có `Hanghoa` chứa từ khóa.
- Nhập khách hàng và bấm **TÌM KIẾM**: chỉ hiển thị dòng có `Khachhang` chứa từ khóa.
- Nhập nhiều điều kiện: filter theo logic `AND`.
- Bấm **TẢI LẠI**: reset về trang đầu theo bộ lọc hiện tại.
- Nút `<` và `>` chuyển trang đúng, giữ nguyên bộ lọc hiện tại.

### Error handling

- Sai IP DB ngoài: hiển thị lỗi trên màn, app không crash.
- Sai user/password: hiển thị lỗi kết nối/quyền, app không crash.
- Thiếu connection string: hiển thị lỗi chưa cấu hình `ConnectionStrings:ExternalDatacanConnection`.
- Bảng `dbo.Datacan` không tồn tại: hiển thị lỗi SQL rõ ràng.
- Các cột ngày/số null hoặc kiểu dữ liệu không chuẩn: service parse an toàn, không crash toàn màn.

### Performance

- Không load toàn bộ `dbo.Datacan`.
- Mỗi lần query chỉ lấy `PageSize + 1` dòng để xác định còn trang sau hay không.
- Grid bật row/column virtualization.
- Nếu bảng `dbo.Datacan` rất lớn và query `LIKE %keyword%` chậm, cần tối ưu phía DB cũ bằng index phù hợp hoặc đổi chiến lược tìm kiếm sang prefix search.

## 7. Ghi chú vận hành

- Đây là chức năng tra cứu lịch sử PM cũ, không phải nguồn dữ liệu nghiệp vụ chính của phần mềm cân mới.
- Không dùng dữ liệu từ màn này để sửa số cân, tính báo cáo, tính dashboard hoặc sync lên Central API.
- Nếu cần mở rộng thành chức năng đối soát dữ liệu, cần lập plan riêng để định nghĩa quy tắc mapping giữa PM cũ và dữ liệu cân mới.
