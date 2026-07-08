# Kế hoạch triển khai: Hoàn cho Cân trạm đập

## Tổng quan

Bổ sung nghiệp vụ `Hoàn` cho màn `Cân trạm đập`, tương tự luồng `Hoàn` ở màn `Cân xuất khẩu`: người dùng tick checkbox để đánh dấu một lượt cân là hàng hoàn, dòng hoàn hiển thị màu đỏ trên grid và báo cáo, KPI Trang chủ của trạm đập hiển thị 3 thông tin `Nhập`, `Hoàn`, `Thực nhập`. Tài liệu này chỉ mô tả kế hoạch triển khai, chưa thực hiện code.

## Hiện trạng đã rà soát

- Màn Cân xuất khẩu đang dùng `IsReturnedBrokenTrip` trên `weighing_session_lines`.
- Checkbox Hoàn ở Cân xuất khẩu không bind TwoWay. UI dùng `IsChecked="{Binding IsReturnedBrokenTrip, Mode=OneWay}"`, chặn click bằng `PreviewMouseLeftButtonDown`, rồi gọi command với item của checkbox. Cách này tránh lỗi `SelectedItem` bị đổi sai thời điểm.
- `ToggleExportReturnedBrokenTripUseCase` validate line/cắt lệnh/session hợp lệ, yêu cầu đã có `ActualAllocatedWeight`, cập nhật `IsReturnedBrokenTrip`, sync status và save trong transaction.
- Cân trạm đập hiện là session độc lập trên `weighing_sessions`, không tạo `weighing_session_lines` như luồng xuất khẩu.
- Grid Cân trạm đập hiện bind trực tiếp `Sessions` / `SelectedSession`; báo cáo trạm đập lấy dữ liệu từ `WeighingSessions`.

## Quyết định kiến trúc

- Lưu trạng thái `Hoàn` của Cân trạm đập ở cấp `WeighingSession`, thêm field `IsReturnedBrokenTrip` vào `weighing_sessions`.
- Không tạo dummy `WeighingSessionLine` cho trạm đập, vì luồng trạm đập không gắn cắt lệnh/line và việc chèn line giả sẽ làm tăng rủi ro cho sync, report và các use case hiện có.
- KPI trạm đập tính theo tấn:
  - `Nhập`: tổng TL hàng của các lượt hoàn thành trong ngày, chưa trừ hàng hoàn.
  - `Hoàn`: tổng TL hàng của các lượt được đánh dấu Hoàn.
  - `Thực nhập`: `Nhập - Hoàn`.
- Dòng Hoàn hiển thị màu đỏ nhưng vẫn giữ trong danh sách, không xóa, không cancel, không đổi trạng thái session.
- Toggle Hoàn chỉ cho phép khi lượt cân đã hoàn thành và có `NetWeight > 0`.
- Mẫu UI checkbox ở Cân trạm đập phải copy pattern của Cân xuất khẩu: OneWay checkbox + `PreviewMouseLeftButtonDown` + command nhận item, không để CheckBox tự toggle trực tiếp.

## Danh sách công việc

### Giai đoạn 1: Data model và repository

## Task 1: Thêm cờ Hoàn vào WeighingSession

**Mô tả:** Bổ sung field session-level để Cân trạm đập lưu trạng thái hàng hoàn.

**Tiêu chí nghiệm thu:**
- [ ] `WeighingSession` có property `IsReturnedBrokenTrip`.
- [ ] EF configuration map field này với default `false`.
- [ ] Schema bootstrap/migration thêm cột `IsReturnedBrokenTrip bit NOT NULL DEFAULT 0` vào `weighing_sessions`.
- [ ] Không ảnh hưởng field `WeighingSessionLine.IsReturnedBrokenTrip` đang dùng cho Cân xuất khẩu.

**Kiểm chứng:**
- [ ] Build Infrastructure/Application thành công.
- [ ] Chạy app trên DB cũ không lỗi bootstrap schema.

**Phụ thuộc:** Không có

**File dự kiến thay đổi:**
- `src/StationApp.Domain/Entities/WeighingSession.cs`
- `src/StationApp.Infrastructure/Persistence/Configurations/WeighingSessionEntityConfigurations.cs`
- `src/StationApp.Infrastructure/Persistence/SchemaCompatibilityBootstrapper.cs`
- Migration EF nếu project đang dùng migration cho thay đổi schema.

**Phạm vi ước tính:** M

## Task 2: Đưa cờ Hoàn vào DTO/list item của Cân trạm đập

**Mô tả:** Để grid và viewmodel biết session nào là Hoàn.

**Tiêu chí nghiệm thu:**
- [ ] `CrusherWeighingSessionListItem` có `IsReturnedBrokenTrip`.
- [ ] `SearchCrusherSessionsAsync` select giá trị từ `WeighingSession.IsReturnedBrokenTrip`.
- [ ] `SearchClaySessionsAsync` cân nhắc không hiển thị Hoàn nếu chỉ áp dụng trạm đập; nếu reuse DTO thì set field đúng từ session nhưng UI Cân mỏ sét không cần hiển thị.

**Kiểm chứng:**
- [ ] Test/query list session trạm đập có item Hoàn trả về `true`.
- [ ] Build UI thành công.

**Phụ thuộc:** Task 1

**File dự kiến thay đổi:**
- `src/StationApp.Application/DTOs/Dtos.cs`
- `src/StationApp.Infrastructure/Repositories/WeighingSessionRepository.cs`

**Phạm vi ước tính:** S

### Checkpoint: Nền tảng

- [ ] App build được sau khi thêm cột/DTO.
- [ ] DB cũ tự bootstrap được cột mới.
- [ ] Chưa thay đổi UI toggle ở checkpoint này.

### Giai đoạn 2: Toggle nghiệp vụ Hoàn cho Cân trạm đập

## Task 3: Tạo use case ToggleCrusherReturnedBrokenTrip

**Mô tả:** Tạo use case riêng cho trạm đập, gắn/bỏ gắn `IsReturnedBrokenTrip` trên session.

**Tiêu chí nghiệm thu:**
- [ ] Use case load session theo `SessionId`.
- [ ] Validate session không deleted/cancelled.
- [ ] Validate `InternalVehicleNo` có giá trị để đảm bảo là session trạm đập/xe nội bộ.
- [ ] Validate session đã `COMPLETED` và `NetWeight > 0`.
- [ ] Nếu state mới bằng state cũ thì return idempotent.
- [ ] Khi toggle, cập nhật `IsReturnedBrokenTrip`, `SyncStatus = SYNC_QUEUED`, clear retry fields, set `UpdatedAt/UpdatedBy`.
- [ ] Save trong transaction.

**Kiểm chứng:**
- [ ] Unit test toggle true thành công.
- [ ] Unit test toggle false thành công.
- [ ] Unit test reject session chưa hoàn thành.
- [ ] Unit test reject session bị xóa/hủy.

**Phụ thuộc:** Task 1

**File dự kiến thay đổi:**
- `src/StationApp.Application/UseCases/ToggleCrusherReturnedBrokenTripUseCase.cs`
- `src/StationApp.UI/App.xaml.cs` để đăng ký DI
- `tests/StationApp.Application.Tests/...`

**Phạm vi ước tính:** M

## Task 4: Gắn checkbox Hoàn vào grid Cân trạm đập theo pattern an toàn

**Mô tả:** Thêm cột checkbox Hoàn vào `CrusherWeighingView` và command toggle trong `CrusherWeighingViewModel`.

**Tiêu chí nghiệm thu:**
- [ ] Grid Cân trạm đập có cột `HOÀN`.
- [ ] Checkbox bind `IsChecked` OneWay, không TwoWay.
- [ ] Checkbox click đi qua event `PreviewMouseLeftButtonDown`, set `e.Handled = true`, lấy item từ `CheckBox.DataContext`, gọi command với item đó.
- [ ] Command không phụ thuộc vào `SelectedSession` hiện tại để tránh lỗi selected item.
- [ ] Command confirm trước khi toggle:
  - Tick: "Đánh dấu lượt cân ... là hàng hoàn?"
  - Untick: "Bỏ đánh dấu hàng hoàn cho lượt cân ...?"
- [ ] Sau khi toggle, reload list và giữ/clear selection có chủ đích, không để checkbox tự đổi visual trước khi save.
- [ ] Dòng Hoàn trong grid hiển thị text màu đỏ.

**Kiểm chứng:**
- [ ] Manual: click checkbox trên dòng khác dòng đang selected vẫn toggle đúng dòng được click.
- [ ] Manual: cancel confirm thì checkbox không đổi trạng thái.
- [ ] Manual: toggle xong reload grid, dòng đó màu đỏ.
- [ ] Build UI thành công.

**Phụ thuộc:** Task 2, Task 3

**File dự kiến thay đổi:**
- `src/StationApp.UI/ViewModels/CrusherWeighingViewModel.cs`
- `src/StationApp.UI/Views/CrusherWeighingView.xaml`
- `src/StationApp.UI/Views/CrusherWeighingView.xaml.cs`

**Phạm vi ước tính:** M

## Task 5: Truyền cờ Hoàn vào in phiếu trạm đập

**Mô tả:** Nếu phiếu cân trạm đập có ghi chú Hoàn như Cân xuất khẩu, cần truyền flag vào composer.

**Tiêu chí nghiệm thu:**
- [ ] `ExecutePrintFlowAsync` của Cân trạm đập truyền `SelectedSession.IsReturnedBrokenTrip` vào `composer.Compose`.
- [ ] Phiếu preview/in của lượt Hoàn hiển thị ghi chú theo template composer hiện có.
- [ ] Lượt bình thường không hiển thị ghi chú Hoàn.

**Kiểm chứng:**
- [ ] Manual preview phiếu cân trạm đập cho lượt Hoàn.
- [ ] Manual preview phiếu cân trạm đập cho lượt không Hoàn.

**Phụ thuộc:** Task 2

**File dự kiến thay đổi:**
- `src/StationApp.UI/ViewModels/CrusherWeighingViewModel.cs`

**Phạm vi ước tính:** S

### Checkpoint: Toggle chính

- [ ] Toggle Hoàn trên grid trạm đập đúng dòng, không lỗi selected item.
- [ ] Data được lưu vào DB và reload đúng.
- [ ] Phiếu in nếu có preview phải thể hiện đúng flag Hoàn.

### Giai đoạn 3: Dashboard KPI trạm đập

## Task 6: Tính KPI Nhập/Hoàn/Thực nhập trên Dashboard

**Mô tả:** Thay KPI trạm đập hiện tại thành 3 thông tin tấn: nhập, hoàn, thực nhập.

**Tiêu chí nghiệm thu:**
- [ ] `DashboardViewModel` có property:
  - `CrusherInboundTonnage`
  - `CrusherReturnedTonnage`
  - `CrusherActualInboundTonnage`
- [ ] `Nhập` tính tổng `NetWeight` của session trạm đập completed trong ngày, bỏ qua `IsNoLoad`.
- [ ] `Hoàn` tính tổng `NetWeight` của session completed, `IsReturnedBrokenTrip = true`, bỏ qua `IsNoLoad`.
- [ ] `Thực nhập = Nhập - Hoàn`, không âm nếu dữ liệu lỗi.
- [ ] Nếu vẫn cần count `Đang cân`, có thể giữ một card riêng hoặc bỏ theo yêu cầu "KPI sẽ là 3 thông tin nhập, hoàn, thực nhập".

**Kiểm chứng:**
- [ ] Unit/integration-style test tính KPI với 2 lượt nhập, 1 lượt hoàn.
- [ ] Manual Dashboard trạm đập hiển thị 3 card đúng label và giá trị.

**Phụ thuộc:** Task 1

**File dự kiến thay đổi:**
- `src/StationApp.UI/ViewModels/DashboardViewModel.cs`
- `src/StationApp.UI/Views/DashboardView.xaml`

**Phạm vi ước tính:** M

### Giai đoạn 4: Báo cáo trạm đập

## Task 7: Đưa Hoàn vào data báo cáo trạm đập

**Mô tả:** Báo cáo trạm đập cần biết dòng nào là Hoàn, tính tổng theo thực nhập nếu cần.

**Tiêu chí nghiệm thu:**
- [ ] `CrusherInboundReportRow` có `IsReturnedBrokenTrip`.
- [ ] `CrusherInboundReportService.BuildRows` map flag từ session.
- [ ] Preview report trong app hiển thị dòng Hoàn màu đỏ.
- [ ] Excel exporter hiển thị dòng Hoàn màu đỏ.
- [ ] Có cột/ghi chú `Hoàn` trong report nếu cần để người dùng nhận biết khi in/xuất Excel.

**Kiểm chứng:**
- [ ] Manual preview Báo cáo trạm đập với 1 dòng Hoàn màu đỏ.
- [ ] Manual export Excel, dòng Hoàn màu đỏ.
- [ ] Test service/exporter nếu đã có pattern test report.

**Phụ thuộc:** Task 1

**File dự kiến thay đổi:**
- `src/StationApp.Application/DTOs/CrusherInboundReportDtos.cs`
- `src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs`
- `src/StationApp.UI/ViewModels/CrusherInboundReportViewModel.cs`
- `src/StationApp.UI/Views/CrusherInboundReportView.xaml`

**Phạm vi ước tính:** M

## Task 8: Điều chỉnh tổng báo cáo nếu nghiệp vụ yêu cầu trừ Hoàn

**Mô tả:** Xác định và áp dụng cách tính tổng trong báo cáo: tổng nhập gross hay thực nhập sau khi trừ Hoàn.

**Tiêu chí nghiệm thu:**
- [ ] Nếu báo cáo cần hiển thị `Nhập`, `Hoàn`, `Thực nhập`, document có đủ 3 tổng.
- [ ] `Hoàn` được tính bằng tổng `NetWeight` của dòng Hoàn.
- [ ] `Thực nhập = Nhập - Hoàn`.
- [ ] Nếu vẫn giữ `Cộng tổng` cũ, label phải rõ là `Thực nhập` hoặc `Tổng nhập`.

**Kiểm chứng:**
- [ ] So sánh report với dữ liệu mẫu: 100 tấn nhập, 15 tấn hoàn => thực nhập 85 tấn.

**Phụ thuộc:** Task 7

**File dự kiến thay đổi:**
- `src/StationApp.Application/DTOs/CrusherInboundReportDtos.cs`
- `src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs`

**Phạm vi ước tính:** S

### Checkpoint: Reporting

- [ ] Dashboard và Báo cáo trạm đập cùng một công thức.
- [ ] Dòng Hoàn màu đỏ ở grid Cân trạm đập và Báo cáo trạm đập.
- [ ] Tổng số liệu không tính sai khi có Hoàn.

### Giai đoạn 5: Tests và regression

## Task 9: Bổ sung test bảo vệ nghiệp vụ

**Mô tả:** Thêm test ở các tầng có logic để tránh regression.

**Tiêu chí nghiệm thu:**
- [ ] Test use case toggle Hoàn.
- [ ] Test Dashboard KPI tính `Nhập/Hoàn/Thực nhập`.
- [ ] Test report row map `IsReturnedBrokenTrip`.
- [ ] Nếu Excel exporter có test, check font row Hoàn màu đỏ.

**Kiểm chứng:**
- [ ] `dotnet test tests/StationApp.Application.Tests/StationApp.Application.Tests.csproj`
- [ ] Test report/integration liên quan nếu có.

**Phụ thuộc:** Task 3, Task 6, Task 7

**File dự kiến thay đổi:**
- `tests/StationApp.Application.Tests/...`
- `tests/StationApp.IntegrationTests/...`

**Phạm vi ước tính:** M

## Task 10: Checklist kiểm thử thủ công

**Mô tả:** Kiểm tra end-to-end trên app.

**Tiêu chí nghiệm thu:**
- [ ] Tạo/cân xong 1 lượt trạm đập bình thường.
- [ ] Tick Hoàn, confirm, dòng chuyển màu đỏ.
- [ ] Untick Hoàn, confirm, dòng về màu bình thường.
- [ ] Click checkbox trên dòng không selected, hệ thống toggle đúng dòng đó.
- [ ] Cancel confirm, UI không bị đổi checkbox.
- [ ] Dashboard hiển thị `Nhập`, `Hoàn`, `Thực nhập` đúng.
- [ ] Báo cáo trạm đập preview và Excel hiển thị dòng Hoàn màu đỏ.
- [ ] In/preview phiếu cân trạm đập lượt Hoàn hiển thị ghi chú đúng nếu composer hỗ trợ.

**Kiểm chứng:**
- [ ] Build UI: `dotnet build src/StationApp.UI/StationApp.UI.csproj -v:minimal`
- [ ] Chạy app và test luồng thật.

**Phụ thuộc:** Tasks 1-9

**Phạm vi ước tính:** S

## Rủi ro và cách giảm thiểu

| Rủi ro | Ảnh hưởng | Cách giảm thiểu |
|---|---|---|
| Nhập nhằng cấp lưu Hoàn giữa session và line | High | Cân trạm đập dùng session-level; Cân xuất khẩu giữ line-level, không trộn 2 luồng. |
| Checkbox làm đổi `SelectedSession` sai | High | Bắt buộc dùng pattern OneWay + `PreviewMouseLeftButtonDown` + command item, không bind TwoWay. |
| KPI và Báo cáo tính khác công thức | Medium | Định nghĩa chung: `Nhập`, `Hoàn`, `Thực nhập = Nhập - Hoàn`; thêm test với dữ liệu mẫu. |
| Sync trung tâm chưa biết field mới | Medium | Nếu payload sync session đang serialize entity, cần đảm bảo central có cột/ignore field. Nếu central chưa có, cần phối hợp schema trước rollout. |
| Báo cáo trạm đập và mỏ sét dùng chung DTO | Low | Chỉ hiển thị UI Hoàn ở trạm đập; nếu DTO dùng chung thì field default false cho mỏ sét. |

## Câu hỏi cần chốt

- KPI `Nhập/Hoàn/Thực nhập` có xác nhận là tính theo tấn `NetWeight` không? Plan đang giả định là tấn.
- Báo cáo trạm đập cần thêm cột riêng `Hoàn`, hay chỉ cần dòng Hoàn màu đỏ? Plan khuyến nghị thêm cột/ghi chú để khi in đen trắng vẫn nhận biết.
- Phiếu cân trạm đập có cần hiển thị ghi chú "Hàng hoàn" như phiếu Cân xuất khẩu không? Plan đã đưa vào Task 5 vì đang có sẵn tham số composer.

## Thứ tự triển khai đề xuất

1. Task 1-2: data + DTO.
2. Task 3-4: toggle grid an toàn.
3. Task 6: dashboard KPI.
4. Task 7-8: báo cáo trạm đập.
5. Task 5: print preview nếu cần.
6. Task 9-10: test và kiểm thử thủ công.
