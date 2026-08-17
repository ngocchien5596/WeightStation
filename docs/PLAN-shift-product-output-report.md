# Kế hoạch: Báo cáo sản lượng xuất hàng theo ca theo từng sản phẩm

## 1. Bối cảnh

Người dùng cần một báo cáo Excel tổng hợp **sản lượng xuất hàng theo ca**, theo từng sản phẩm. File mẫu tham khảo: `Mau bao cao san luong theo ca.xlsx`.

Mẫu hiện có 6 cột chính:

- `STT`
- `NGÀY`
- `CA BÁO CÁO`
- `S.LƯỢNG/T`
- `LŨY KẾ LÔ/T`

Báo cáo mới cần liệt kê toàn bộ sản phẩm theo 3 nhóm đã chốt: `Rời`, `Bao`, `Xuất khẩu`.

## 2. Chốt nghiệp vụ

1. **Lũy kế hàng xuất khẩu:** tính lũy kế theo từng cắt lệnh XK. Khi hiển thị theo sản phẩm, cộng lũy kế của các cắt lệnh XK thuộc sản phẩm đó.
2. **Khung ca báo cáo:**
   - Ca 1: `06:00:00 - 13:59:59`
   - Ca 2: `14:00:00 - 21:59:59`
   - Ca 3: `22:00:00 - 05:59:59` ngày hôm sau
3. **Nhóm sản phẩm:** chỉ dùng 3 nhóm `Rời`, `Bao`, `Xuất khẩu`.
4. **Hàng hoàn:** xử lý theo logic các báo cáo xuất hiện tại, không cộng sai vào sản lượng xuất.

## 3. Mục tiêu

- Tạo màn báo cáo mới: `Báo cáo sản lượng theo ca`.
- Bộ lọc gồm: ngày, ca, từ giờ, đến giờ, sản phẩm.
- Có preview giống các màn báo cáo khác.
- Tải Excel theo bố cục gần file mẫu.
- Liệt kê cả sản phẩm không phát sinh trong ca, số lượng hiển thị `0`.
- Không ảnh hưởng báo cáo xuất - NĐ và báo cáo xuất - XK hiện có.

## 4. Thiết kế dữ liệu

### 4.1. Nguồn dữ liệu

Nguồn chính:

- `weighing_sessions`
- `weighing_session_lines`
- `cut_orders`
- `products`

Không phụ thuộc ERP để tính sản lượng, vì dữ liệu cân/phân bổ đã nằm trong phần mềm cân.

### 4.2. Thời điểm ghi nhận

Dùng `WeighingSession.Weight2Time` làm thời điểm xuất hàng.

Chỉ lấy dữ liệu:

- `TransactionType = OUTBOUND`
- Lượt cân có `Weight2Time`
- Không xóa, không hủy
- Không lấy hàng `Không lấy hàng`
- Dòng cân đã phân bổ hợp lệ, ưu tiên `ActualAllocatedWeight`

### 4.3. Phân nhóm

- `Xuất khẩu`: cắt lệnh có `IsExportScale = true` hoặc `IsTemporaryExport = true`.
- `Bao`: hàng nội địa có `ProductType = Bao`.
- `Rời`: hàng nội địa có `ProductType = Rời/Xá` hoặc `Clinker`.

Nếu dữ liệu cũ thiếu `ProductType`, fallback sang danh mục `products`. Nếu vẫn không xác định được, đưa vào nhóm `Rời` hoặc `Chưa phân loại` tùy khi code thấy dữ liệu thực tế.

## 5. Logic tính toán

### 5.1. Sản lượng trong ca

Sản lượng ca của từng sản phẩm:

- Lọc theo `FromTime <= Weight2Time <= ToTime`.
- Group theo nhóm + sản phẩm.
- Đơn vị: tấn.
- Làm tròn 3 chữ số thập phân.

### 5.2. Lũy kế nội địa

Với nhóm `Rời` và `Bao`:

- Lũy kế ngày = tổng sản lượng từ `00:00:00` của ngày báo cáo đến hết `ToTime`.
- Vẫn group theo từng sản phẩm.

### 5.3. Lũy kế xuất khẩu

Với nhóm `Xuất khẩu`:

- Tính lũy kế ở grain `CutOrderId + ProductCode`.
- Với mỗi cắt lệnh XK, cộng các chuyến thuộc cắt lệnh đó có `Weight2Time <= ToTime`.
- Sau đó group về sản phẩm để hiển thị.

Ví dụ: sản phẩm A có 2 cắt lệnh XK phát sinh trong kỳ, dòng sản phẩm A hiển thị lũy kế = lũy kế cắt lệnh 1 + lũy kế cắt lệnh 2.

### 5.4. Hàng hoàn

Áp dụng lại logic báo cáo xuất hiện tại:

- Không cộng sai hàng hoàn vào sản lượng xuất.
- Nếu báo cáo hiện tại đang giảm trừ bằng trọng lượng âm thì báo cáo này cũng giảm trừ tương ứng.
- Nếu báo cáo hiện tại loại trừ hàng hoàn thì báo cáo này cũng loại trừ.

## 6. Bố cục Excel

Tạo Excel bằng ClosedXML, tham khảo `Mau bao cao san luong theo ca.xlsx`.

### Header

- Cột A: `STT`
- Cột B: `NGÀY`
- Cột C-D: `CA BÁO CÁO`
- Cột E: `S.LƯỢNG/T`
- Cột F: `LŨY KẾ LÔ/T`

### Body

Thứ tự nhóm:

1. `Rời`
2. `Bao`
3. `Xuất khẩu`

Mỗi nhóm:

- Dòng sản phẩm.
- Dòng tổng nhóm.

Cuối báo cáo:

- Dòng `TỔNG TOÀN BỘ`.

### Định dạng

- Header in đậm, căn giữa.
- Cột số lượng căn phải, format `#,##0.###`.
- Dòng tổng nhóm và tổng toàn bộ in đậm.
- Merge/căn ô theo tinh thần file mẫu.

## 7. UI

Tạo màn mới:

- `ShiftProductOutputReportView.xaml`
- `ShiftProductOutputReportViewModel.cs`

Bộ lọc:

- Ngày báo cáo.
- Ca: Ca 1, Ca 2, Ca 3, Tùy chỉnh.
- Từ giờ.
- Đến giờ.
- Sản phẩm.
- Nút `Xem`.
- Nút `Tải`.

Hành vi:

- Chọn ngày + ca sẽ tự set khoảng giờ.
- Chỉnh giờ thủ công thì chuyển ca sang `Tùy chỉnh`.
- Dropdown sản phẩm có option `Tất cả sản phẩm`.
- Sản phẩm dropdown chỉ lấy sản phẩm xuất hàng.

## 8. Preview

Làm giống các màn báo cáo hiện có:

- Build document.
- Export Excel tạm.
- Dùng `ReportPreviewHelper.GeneratePreviewAsync`.
- Hiển thị `IDocumentPaginatorSource`.
- Cleanup file tạm khi đổi màn/đóng màn.

## 9. Task triển khai

### Task 1: Tạo contract báo cáo

**Mô tả:** Thêm DTO và interface cho báo cáo sản lượng theo ca.

**Acceptance criteria:**

- Có filter chứa ngày báo cáo, ca, from/to, productCode.
- Có document chứa group, row, tổng nhóm, tổng toàn bộ.
- Build Application pass.

**Files likely touched:**

- `src/StationApp.Application/DTOs/ShiftProductOutputReportDtos.cs`
- `src/StationApp.Application/Interfaces/IShiftProductOutputReportService.cs`
- `src/StationApp.Application/Interfaces/IShiftProductOutputReportExporter.cs`

### Task 2: Tạo use case

**Mô tả:** Thêm use case build, export và lookup sản phẩm.

**Acceptance criteria:**

- Validate `FromTime <= ToTime`.
- Validate ngày báo cáo có giá trị.
- Lấy tên người lập báo cáo từ user context.

**Files likely touched:**

- `src/StationApp.Application/UseCases/ShiftProductOutputReportUseCases.cs`

### Task 3: Implement service query và tính toán

**Mô tả:** Query dữ liệu cân xuất và tính sản lượng theo nhóm/sản phẩm.

**Acceptance criteria:**

- Lọc đúng ca/khoảng giờ.
- Tính đúng sản lượng ca.
- Tính đúng lũy kế nội địa theo ngày.
- Tính đúng lũy kế XK theo cắt lệnh, rồi cộng về dòng sản phẩm.
- Sản phẩm không phát sinh vẫn hiển thị 0.

**Files likely touched:**

- `src/StationApp.Infrastructure/Services/ShiftProductOutputReportServices.cs`

### Task 4: Implement Excel exporter

**Mô tả:** Xuất Excel theo mẫu tham khảo.

**Acceptance criteria:**

- Có đủ 3 nhóm `Rời`, `Bao`, `Xuất khẩu`.
- Có dòng tổng nhóm và tổng toàn bộ.
- File mở được bằng Excel.

**Files likely touched:**

- `src/StationApp.Infrastructure/Services/ShiftProductOutputReportServices.cs`

### Checkpoint 1

- Build solution pass.
- Có thể gọi service/use case để sinh document.

### Task 5: Thêm ViewModel và filter UI

**Mô tả:** Tạo ViewModel cho màn báo cáo mới.

**Acceptance criteria:**

- Có dropdown ca, ngày, giờ, sản phẩm.
- Chọn ca tự set giờ.
- Preview và tải dùng cùng filter.
- Tên file tải về dạng `BaoCaoSanLuongTheoCa_{ngay}_{ca}_{from}_{to}.xlsx`.

**Files likely touched:**

- `src/StationApp.UI/ViewModels/ShiftProductOutputReportViewModel.cs`

### Task 6: Thêm XAML view

**Mô tả:** Tạo giao diện báo cáo.

**Acceptance criteria:**

- Filter hiển thị gọn, không overlap.
- Có vùng preview giống các báo cáo khác.
- Nút `Xem`, `Tải` hoạt động.

**Files likely touched:**

- `src/StationApp.UI/Views/ShiftProductOutputReportView.xaml`

### Task 7: Đăng ký DI và menu

**Mô tả:** Đăng ký service/use case/viewmodel và thêm menu.

**Acceptance criteria:**

- Mở được màn báo cáo từ menu.
- Role có quyền xem báo cáo hiện tại xem được.
- Không ảnh hưởng các menu báo cáo cũ.

**Files likely touched:**

- File đăng ký DI hiện có.
- `src/StationApp.UI/ViewModels/MainViewModel.cs`
- View sidebar/menu liên quan.

### Checkpoint 2

- `dotnet build StationApp.sln /p:SkipDatabaseSchemaUpdate=true` pass.
- Mở app được.
- Preview báo cáo có dữ liệu hoặc thông báo không dữ liệu đúng.

### Task 8: Kiểm thử nghiệp vụ và regression

**Acceptance criteria:**

- Sản phẩm có phát sinh trong ca hiển thị đúng sản lượng.
- Sản phẩm không phát sinh vẫn hiển thị 0.
- Lũy kế nội địa đúng từ đầu ngày đến cuối ca.
- Lũy kế XK đúng theo từng cắt lệnh.
- Báo cáo xuất - NĐ và báo cáo xuất - XK hiện có vẫn hoạt động.

## 10. Rủi ro và cách xử lý

| Rủi ro | Mức độ | Cách xử lý |
|---|---:|---|
| Một sản phẩm XK có nhiều cắt lệnh trong kỳ | Cao | Tính lũy kế ở grain cắt lệnh trước, sau đó group về sản phẩm |
| Dữ liệu cũ thiếu ProductType | Trung bình | Fallback sang danh mục sản phẩm, nếu vẫn thiếu thì đưa vào nhóm dự phòng |
| Công thức sản lượng XK khác Bao/Rời | Cao | Tái sử dụng logic báo cáo XK/finalize XK hiện có |
| Text tiếng Việt lỗi encoding khi sửa XAML/C# | Cao | Sửa file bằng UTF-8, ưu tiên Unicode escaped nếu file đang có lịch sử lỗi encoding |

