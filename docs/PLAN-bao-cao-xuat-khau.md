# Kế hoạch chi tiết: Triển khai chức năng Báo cáo xuất - xuất khẩu

Kế hoạch này chi tiết hóa cách thức xây dựng tính năng "Báo cáo xuất - xuất khẩu" dựa trên các yêu cầu nghiệp vụ đã thống nhất từ file Excel mẫu `Xi măng CEM II A-L 42.5N OMANCO 12.09( Bản chuẩn).xlsx`.

---

## Các tham số và nghiệp vụ đã thống nhất

> [!NOTE]
> 1. **Tham số cấu hình**:
>    - **Trọng lượng vỏ (E6)**: Lấy từ `CutOrder.TareWeightKg`.
>    - **Trọng lượng xi măng Net (E7)**: Lấy từ `CutOrder.BagWeightKg`.
>    - **Trọng lượng có vỏ (E8)**: Tính bằng `E6 + E7`.
>    - **Xử lý Null/0**: Nếu `TareWeightKg` hoặc `BagWeightKg` null hoặc bằng `0`, ô số lượng đặt bao (C11), cột khối lượng vỏ bao (G), số bao thực xuất (K) và các ô tính toán liên quan đến bao sẽ được **gán trực tiếp bằng 0** để tránh lỗi tính toán hoặc lỗi chia cho 0 (`#DIV/0!`).
> 2. **Bộ lọc dữ liệu**:
>    - Bắt buộc người dùng phải chọn **1 Cắt lệnh** (`CutOrder`) cụ thể trên UI để xuất báo cáo.
> 3. **Phân chia ca làm việc (theo thời gian cân lần 2 - Weight2Time)**:
>    - **Ca A**: Từ `06:00` đến `13:59`.
>    - **Ca B**: Từ `14:00` đến `21:59`.
>    - **Ca C**: Từ `22:00` đến `05:59` ngày hôm sau.
> 4. **Ngày xuất (cột C trong bảng chi tiết)**:
>    - Hiển thị **đầy đủ định dạng ngày** (`dd/MM/yyyy`) dựa trên thời gian cân lần 2 (`Weight2Time.Value.Date`), không dịch chuyển ngày đối với ca C.
>    - Ô ngày lọc ca `R7` của bảng phụ bên phải cũng hiển thị đầy đủ định dạng ngày (`dd/MM/yyyy`) để so sánh khớp dữ liệu.
> 5. **Chuyến xe hồi trả (Cột H và I)**:
>    - Các cột Rách vỡ hồi về (Tấn - cột H và Bao - cột I) sẽ lấy dữ liệu từ các chuyến xe có `IsReturnedBrokenTrip = true` (tương ứng với `isReturnedBrokenTrip = 1` trong database).
> 6. **Cột Ghi chú trong báo cáo xuất Excel**:
>    - Nhận giá trị trực tiếp từ trường `Note` của chuyến xe (`weighing_session_lines.Note`).
>    - Không tự động sinh các chuỗi ghi chú khác (như "Hàng rách vỡ hoàn" hoặc ghi chú gộp).
> 7. **Hiển thị UI màn Cân xuất khẩu**:
>    - Thêm cột **Ghi chú** (hiển thị trường `Note`) vào grid danh sách chuyến xe trên giao diện `Cân xuất khẩu`.

---

## Phân tích công thức Excel mẫu

Báo cáo gồm 4 khu vực chính trên `Sheet1`:

### 1. Khu vực Thông số cấu hình (Dòng 6-8, Cột A-O)
- `E6` (Trọng lượng vỏ): `= CutOrder.TareWeightKg` (nếu null/0, ghi `0`).
- `E7` (Trọng lượng tịnh Net): `= CutOrder.BagWeightKg` (nếu null/0, ghi `0`).
- `E8` (Trọng lượng có vỏ): `=+E6+E7` (nếu vỏ hoặc net bằng 0, ghi `0`).
- `L6` (Số chuyến đã xuất): `=COUNTA(E15:E{last_row})` (Đếm tổng số dòng dữ liệu có khối lượng cân).
- `L7` (Số chuyến hồi trả): `=COUNTA(H15:H{last_row})` (Đếm số chuyến có ghi nhận rách vỡ hồi về > 0).

### 2. Khu vực Tổng hợp số liệu (Dòng 9-11, Cột A-O)
- **Đặt hàng**:
  - `A11` (Tấn): Số lượng kế hoạch của Cắt lệnh (`CutOrder.PlannedWeight` quy đổi ra Tấn).
  - `C11` (Bao): `=IF(OR(E7=0,ISBLANK(E7)),0,A11*1000/E7)` (Số bao sling đặt hàng kế hoạch. Gán bằng 0 nếu Trọng lượng Net null/0).
- **Số lượng qua cân**:
  - `E11` (Tấn): `=SUM(E15:E{last_row})`
  - `F11` (Bao): `=SUM(F15:F{last_row})`
- **Khối lượng vỏ**:
  - `G11` (Tấn): `=SUM(G15:G{last_row})`
- **SL nhận hồi về**:
  - `H11` (Tấn): `=SUM(H15:H{last_row})`
  - `I11` (Bao): `=SUM(I15:I{last_row})`
- **Số lượng thực xuất**:
  - `J11` (Tấn): `=SUM(J15:J{last_row})`
  - `K11` (Bao): `=SUM(K15:K{last_row})`
- **Tổng chênh lệch**:
  - `L11` (Theo lô - Tấn): `=SUM(L15:L{last_row})/1000`
  - `M11` (BQ bao - kg/bao): `=AVERAGE(M15:M{last_row})`
- **Số lượng tồn**:
  - `N11` (Tấn): `=+A11-J11`
  - `O11` (Bao): `=+C11-K11`

### 3. Khu vực Bảng chi tiết chuyến xe (Dòng 15 trở đi, Cột A-P)
- `Cột A` (Stt): Số thứ tự dòng.
- `Cột B` (Ca): Ca làm việc ('A', 'B', 'C') tính theo giờ của `Weight2Time`.
- `Cột C` (Ngày xuất): Ngày xuất đầy đủ định dạng (`dd/MM/yyyy`) lấy từ `Weight2Time.Value.Date`.
- `Cột D` (Phương tiện): Biển số xe (`VehiclePlate`).
- `Cột E` (Số lượng qua cân - Tấn): Khối lượng tịnh của chuyến xe (`NetWeight / 1000`).
- `Cột F` (Số lượng qua cân - Bao): Số bao thực tế của chuyến xe (`BagCountDisplay`). Nếu vỏ hoặc net null/0, ghi `0`.
- `Cột G` (Khối lượng vỏ bao - Tấn): `=IF(OR($E$6=0,ISBLANK($E$6)),0,F15*$E$6/1000)` (Gán bằng 0 nếu Trọng lượng vỏ null/0).
- `Cột H` (Rách vỡ hồi về - Tấn): Nếu chuyến xe là hàng hoàn (`IsReturnedBrokenTrip = true`), điền giá trị bằng `= E15 - G15`. Nếu không, điền `0`.
- `Cột I` (Rách vỡ hồi về - Bao): Nếu chuyến xe là hàng hoàn (`IsReturnedBrokenTrip = true`), điền giá trị bằng `= F15`. Nếu không, điền `0`.
- `Cột J` (SL thực xuất - Tấn): `=+E15-G15-H15` (Sẽ tự động bằng 0 đối với chuyến hàng hoàn).
- `Cột K` (SL thực xuất - Bao): `=+F15-I15` (Sẽ tự động bằng 0 đối với chuyến hàng hoàn).
- `Cột L` (KL lệch quy cách - Kg theo chuyến): `=IF(OR($E$8=0,ISBLANK($E$8)),0,E15*1000-F15*$E$8)` (Gán bằng 0 nếu Trọng lượng có vỏ null/0).
- `Cột M` (KL lệch quy cách - Kg/bao): `=IF(F15=0,"-",L15/F15)`.
- `Cột N` (Ghi chú): Lấy giá trị trực tiếp từ trường `Note` của chuyến xe (`weighing_session_lines.Note`).

### 4. Khu vực Báo cáo xuất theo ca (Cột Q-U, Dòng 2-10)
Bảng phụ này thống kê sản lượng cho một **Ngày cụ thể** (chọn ở ô `R7` có định dạng `dd/MM/yyyy`):
- `Q7:Q9` (Ca): Ca A, B, C.
- `R7` (Ngày): Ngày lọc dữ liệu theo ca.
- `S7:S9` (KL - Tấn): `=SUMIFS($E$15:$E$402,$B$15:$B$402,Q7,$C$15:$C$402,$R$7)-SUMIFS($H$15:$H$402,$B$15:$B$402,Q7,$C$15:$C$402,$R$7)` (Tổng khối lượng qua cân trừ tổng hồi về của ca đó trong ngày).
- `T7:T9` (Số bao): `=SUMIFS($K$15:$K$402,$B$15:$B$402,Q7,$C$15:$C$402,$R$7)` (Tổng số bao thực xuất của ca đó trong ngày).
- `U7:U9` (Số chuyến): `=COUNTIFS($B$15:$B$402,Q7,$C$15:$C$402,$R$7)` (Số chuyến xe của ca đó trong ngày).
- `S10, T10, U10` (Dòng tổng cộng): `=SUM(S7:S9)`, `=SUM(T7:T9)`, `=SUM(U7:U9)`.

---

## Proposed Changes

### Component 1: Application Domain & Service Contract

#### [NEW] [ExportScaleSummaryReportDocument.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/DTOs/ExportScaleSummaryReportDocument.cs)
Định nghĩa DTO lưu trữ dữ liệu báo cáo xuất - xuất khẩu:
- Các thuộc tính thông số: `CutOrderId`, `CutOrderCode`, `CustomerName`, `ProductName`, `PlannedWeightTon`, `TareWeightKg`, `NetCementWeightKg`, `TargetDateForShiftReport` (Ngày lọc báo cáo ca).
- Danh sách các dòng chi tiết: `List<ExportScaleSummaryReportRow>`.

#### [NEW] [ExportScaleSummaryReportRow.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/DTOs/ExportScaleSummaryReportRow.cs)
Định nghĩa thông tin từng dòng chi tiết chuyến xe:
- `Stt`, `Shift` ('A'/'B'/'C'), `ExportDate` (Ngày xuất đầy đủ `DateTime`), `VehiclePlate`, `NetWeightTon`, `BagCount`, `IsReturnedBrokenTrip`, `Notes`.

#### [MODIFY] [IExportSummaryReportService.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/Interfaces/IExportSummaryReportService.cs)
Bổ sung phương thức:
- `Task<ExportScaleSummaryReportDocument> BuildExportScaleReportAsync(Guid cutOrderId, DateTime? targetDateForShiftReport, CancellationToken ct)`: Truy vấn dữ liệu từ DB và xây dựng tài liệu báo cáo cho Cắt lệnh.

---

### Component 2: Infrastructure Service Implementation

#### [MODIFY] [ExportSummaryReportService.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Infrastructure/Services/ExportSummaryReportServices.cs)
Implement phương thức `BuildExportScaleReportAsync`:
1. Lấy thông tin `CutOrder` theo `cutOrderId`.
2. Truy vấn danh sách `WeighingSession` và `WeighingSessionLine` thuộc `CutOrder` này (không bị xóa, không bị hủy, trạng thái `COMPLETED`).
3. Lấy thông tin `DriverName` và `VehiclePlate` từ session.
4. Với từng dòng (`WeighingSessionLine`):
   - Xác định Ca (`A`, `B`, `C`) dựa trên `Weight2Time` (Sử dụng logic phân ca: A: 6h-13h59, B: 14h-21h59, C: 22h-5h59 ngày hôm sau).
   - Xác định Ngày xuất bằng `Weight2Time.Value.Date`.
   - Xác định chuyến hàng hoàn bằng `IsReturnedBrokenTrip`.
   - Map các dữ liệu vào `ExportScaleSummaryReportRow` (gán thuộc tính `Notes` bằng trường `Line.Note` từ DB).
5. Đóng gói kết quả trả về `ExportScaleSummaryReportDocument`.

#### [NEW] [ExportScaleSummaryReportExcelExporter.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Infrastructure/Services/ExportScaleSummaryReportExcelExporter.cs)
Xây dựng lớp xuất Excel sử dụng `ClosedXML` để thiết kế đúng định dạng mẫu:
- **Header và Cấu hình**: Ghi các thông tin khách hàng ở hàng 4, sản phẩm ở hàng 5, và các thông số E6, E7, E8. Thiết lập công thức cho các ô L6, L7.
- **Summary Block**: Ghi nhận số lượng đặt hàng vào ô A11. Sử dụng công thức Excel tại các ô C11, E11:O11 để tự động tính toán tổng số liệu từ bảng chi tiết phía dưới.
- **Bảng chi tiết (dòng 15 trở đi)**:
  - Ghi các giá trị Stt, Ca, Ngày (định dạng `dd/MM/yyyy`), Biển số, Khối lượng qua cân, Số bao thực tế.
  - Cột N (Ghi chú): Điền trực tiếp giá trị `Notes` của từng dòng (nhận từ `Note` của DB), không tự động sinh chuỗi "Hàng rách vỡ hoàn".
  - Sử dụng công thức ClosedXML để tính các ô tính toán:
    - Cột G (Khối lượng vỏ bao): `=IF(OR($E$6=0,ISBLANK($E$6)),0,{F_row}*$E$6/1000)`
    - Cột H (Hồi về tấn): `=IF({IsReturnedBrokenTrip_cell}, {E_row}-{G_row}, 0)`
    - Cột I (Hồi về bao): `=IF({IsReturnedBrokenTrip_cell}, {F_row}, 0)`
    - Cột J (SL thực xuất tấn): `={E_row}-{G_row}-{H_row}`
    - Cột K (SL thực xuất bao): `={F_row}-{I_row}`
    - Cột L (Lệch quy cách chuyến): `=IF(OR($E$8=0,ISBLANK($E$8)),0,{E_row}*1000-{F_row}*$E$8)`
    - Cột M (Lệch quy cách bao): `=IF({F_row}=0,"-",{L_row}/{F_row})`
- **Bảng báo cáo theo ca (Q4:U10)**:
  - Sử dụng công thức `SUMIFS` và `COUNTIFS` để ClosedXML render công thức Excel động dựa trên Ngày ở ô `R7` và các ca ở cột `Q`.
- **Định dạng mỹ thuật**: Căn lề, đặt cỡ chữ, font chữ (Times New Roman), viền ô (Thin border), định dạng số thập phân (`#,##0.00` cho tấn, `#,##0` cho bao), đặt màu nền cho Header và hàng tổng cộng giống hệt file mẫu.

---

### Component 3: UI & ViewModel Integration

#### [NEW] [ExportScaleReportViewModel.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/ViewModels/ExportScaleReportViewModel.cs)
ViewModel mới quản lý UI báo cáo xuất - xuất khẩu:
- Cho phép tìm kiếm và chọn Cắt lệnh (`SelectedCutOrder`).
- Cho phép nhập Ngày lọc báo cáo ca (`TargetDate`).
- Lệnh `PreviewCommand` để tải dữ liệu xem trước lên Grid.
- Lệnh `ExportExcelCommand` để xuất file Excel.

#### [NEW] [ExportScaleReportView.xaml](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Views/ExportScaleReportView.xaml)
Giao diện XAML cho màn hình báo cáo mới:
- Các control chọn Cắt lệnh, chọn ngày báo cáo ca, các tham số.
- Grid xem trước dữ liệu chi tiết chuyến xe.
- Nút bấm "Xem trước" và "Xuất Excel".

#### [MODIFY] [ExportWeighingView.xaml](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Views/ExportWeighingView.xaml)
Thêm cột **Ghi chú** (Note) hiển thị trong DataGrid chuyến xe ở màn hình Cân xuất khẩu:
- Thêm `<DataGridTextColumn Header="GHI CHÚ" Binding="{Binding Note}" Width="150" ElementStyle="{StaticResource GridTextLeft}"/>` ngay trước cột HOÀN (`DataGridTemplateColumn` của `IsReturnedBrokenTrip`).

---

## Verification Plan

### Automated Tests
- Tạo unit test trong `ExportSummaryReportServicesTests.cs` để kiểm tra logic tính ca và ngày xuất của các chuyến xe:
  - Chuyến xe cân lúc 10:00 ngày 12/12 -> Ca A, Ngày 12/12.
  - Chuyến xe cân lúc 16:00 ngày 12/12 -> Ca B, Ngày 12/12.
  - Chuyến xe cân lúc 23:00 ngày 12/12 -> Ca C, Ngày 12/12.
  - Chuyến xe cân lúc 02:00 ngày 13/12 -> Ca C, Ngày 13/12.
- Kiểm tra logic tính chuyến hàng hoàn (`IsReturnedBrokenTrip = true`) có đưa đúng khối lượng vào cột hồi về và cột thực xuất bằng 0 hay không.

### Manual Verification
1. Mở màn hình Báo cáo xuất - xuất khẩu mới trên ứng dụng.
2. Chọn một Cắt lệnh xuất khẩu đã có các chuyến xe hoàn thành.
3. Bấm "Xuất Excel" và lưu file.
4. Mở file Excel, kiểm tra cột Ghi chú (`Cột N`) hiển thị đúng thông tin của trường `weighing_session_lines.Note` từ DB, không tự động sinh chuỗi "Hàng rách vỡ hoàn".
5. Trên màn hình Cân xuất khẩu, kiểm tra grid Danh sách chuyến xe đã hiển thị cột **GHI CHÚ** và dữ liệu khớp với DB.
