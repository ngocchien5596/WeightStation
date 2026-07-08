# Kế hoạch: Sửa cắt lệnh xuất khẩu tạm

## Mục tiêu

Thêm chức năng **Sửa cắt lệnh tạm** ở màn **Cân xuất khẩu**. Nút chỉ hiển thị khi người dùng đang chọn một cắt lệnh xuất khẩu tạm, và chỉ enable khi cắt lệnh tạm đó chưa chốt tổng, chưa map sang cắt lệnh thật, chưa hủy/xóa. Khi sửa, hệ thống phải cập nhật đồng bộ thông tin cắt lệnh và các dữ liệu snapshot đã phát sinh trên chuyến xe để tránh lệch phiếu, báo cáo, và dữ liệu khi map.

## Quyết định nghiệp vụ

- Cho phép sửa cắt lệnh tạm dù đã có chuyến xe gắn vào.
- Không cho sửa nếu cắt lệnh tạm đã chốt tổng, đã completed, đã map sang cắt lệnh thật, đã hủy hoặc đã xóa.
- Các trường được sửa nên giống form tạo cắt lệnh tạm:
  - Mã khách hàng, khách hàng
  - Mã sản phẩm, sản phẩm, loại sản phẩm
  - SL đặt
  - TL vỏ
  - TL bao
  - Ghi chú
- Sau khi sửa, cần cập nhật đồng bộ:
  - `cut_orders`
  - `weighing_session_lines` thuộc cắt lệnh tạm
  - `weigh_tickets` thuộc cắt lệnh tạm
  - `delivery_tickets` thuộc cắt lệnh tạm
- Cần ghi audit log để hiển thị ở màn **Lịch sử chỉnh sửa**.

## Ghi chú hiện trạng code

- Nút tạo cắt lệnh tạm nằm ở `ExportWeighingView.xaml`.
- Luồng tạo đang dùng:
  - `CreateTemporaryExportCutOrderDialogViewModel`
  - `CreateTemporaryExportCutOrderDialogWindow`
  - `CreateTemporaryExportCutOrderUseCase`
- Hiện chưa có use case update cắt lệnh tạm.
- Luồng map cắt lệnh tạm sang cắt lệnh thật đang cập nhật lại `weighing_session_lines`, `weigh_tickets`, `delivery_tickets`; có thể tham khảo `MapTemporaryExportCutOrderUseCase`.

## Task List

### Task 1: Chuẩn hóa lại dialog tạo cắt lệnh tạm để tái dùng cho sửa

**Mô tả:** Điều chỉnh dialog hiện có để hỗ trợ 2 mode: tạo mới và sửa. Khi sửa, dialog nhận dữ liệu cắt lệnh tạm hiện tại để prefill form.

**Acceptance criteria:**
- [ ] Dialog vẫn tạo mới cắt lệnh tạm như hiện tại.
- [ ] Dialog sửa hiển thị title phù hợp, ví dụ `Sửa cắt lệnh tạm`.
- [ ] Các field được prefill đúng từ `SelectedCutOrder`.
- [ ] Validation hiện có vẫn hoạt động.

**Files likely touched:**
- `src/StationApp.UI/ViewModels/Dialogs/CreateTemporaryExportCutOrderDialogViewModel.cs`
- `src/StationApp.UI/Views/Dialogs/CreateTemporaryExportCutOrderDialogWindow.xaml`

**Verification:**
- [ ] Mở dialog tạo mới vẫn trống dữ liệu.
- [ ] Mở dialog sửa có dữ liệu hiện tại.

### Task 2: Thêm DTO và use case cập nhật cắt lệnh tạm

**Mô tả:** Tạo request/use case riêng để cập nhật cắt lệnh xuất khẩu tạm. Use case phải validate trạng thái và cập nhật đồng bộ các snapshot đã phát sinh.

**Acceptance criteria:**
- [ ] Chỉ update khi `IsTemporaryExport = true` và `IsExportScale = true`.
- [ ] Không cho update nếu cắt lệnh đã chốt tổng, completed, mapped, canceled, deleted.
- [ ] Update `cut_orders` đúng các trường sửa.
- [ ] Update `weighing_session_lines`, `weigh_tickets`, `delivery_tickets` liên quan.
- [ ] Recalculate `BagCount` theo `PlannedWeight / BagWeightKg` giống logic tạo mới.
- [ ] Cập nhật `UpdatedAt`, `UpdatedBy`, `SyncStatus`.

**Files likely touched:**
- `src/StationApp.Application/DTOs/Dtos.cs`
- `src/StationApp.Application/UseCases/UpdateTemporaryExportCutOrderUseCase.cs`
- `src/StationApp.Application/Interfaces/ICutOrderRepository.cs` nếu cần query phụ
- `src/StationApp.Infrastructure/Repositories/CutOrderRepository.cs` nếu cần query phụ
- `src/StationApp.UI/App.xaml.cs`

**Verification:**
- [ ] Unit test update cắt lệnh tạm chưa có chuyến.
- [ ] Unit test update cắt lệnh tạm đã có chuyến.
- [ ] Unit test reject cắt lệnh đã map/chốt/hủy.

### Task 3: Ghi audit log cho thao tác sửa cắt lệnh tạm

**Mô tả:** Ghi nhận thay đổi dữ liệu để xem ở màn Lịch sử chỉnh sửa. Nội dung cần thể hiện mã cắt lệnh tạm, trường thay đổi, giá trị cũ, giá trị mới.

**Acceptance criteria:**
- [ ] Có audit log khi sửa thành công.
- [ ] Log thể hiện rõ thao tác `Sửa cắt lệnh tạm`.
- [ ] Các trường chính có old/new value.
- [ ] Màn Lịch sử chỉnh sửa đọc được log này theo pattern hiện tại.

**Files likely touched:**
- `src/StationApp.Application/UseCases/UpdateTemporaryExportCutOrderUseCase.cs`
- Các service/repository audit hiện có nếu cần bổ sung action type.

**Verification:**
- [ ] Sửa một cắt lệnh tạm rồi mở Lịch sử chỉnh sửa thấy record.

### Task 4: Thêm nút Sửa cắt lệnh tạm ở màn Cân xuất khẩu

**Mô tả:** Thêm nút ở cụm action của màn Cân xuất khẩu. Nút chỉ visible khi đang chọn cắt lệnh tạm, và chỉ enable khi cắt lệnh tạm chưa chốt/mapped/canceled/deleted.

**Acceptance criteria:**
- [ ] Không chọn cắt lệnh: không thấy nút.
- [ ] Chọn cắt lệnh thật: không thấy nút.
- [ ] Chọn cắt lệnh tạm hợp lệ: thấy và enable nút.
- [ ] Chọn cắt lệnh tạm đã chốt/map/hủy: thấy nhưng disabled hoặc không hiển thị theo quyết định UI cuối.
- [ ] Nhấn nút mở dialog sửa với dữ liệu hiện tại.

**Files likely touched:**
- `src/StationApp.UI/Views/ExportWeighingView.xaml`
- `src/StationApp.UI/ViewModels/ExportWeighingViewModel.cs`

**Verification:**
- [ ] Manual check visibility/enable theo từng trạng thái.

### Task 5: Thực hiện flow update từ UI

**Mô tả:** Khi người dùng lưu dialog sửa, gọi use case update, reload lại danh sách cắt lệnh và danh sách chuyến xe của cắt lệnh đang chọn.

**Acceptance criteria:**
- [ ] Lưu thành công hiển thị toast thành công.
- [ ] Grid cắt lệnh cập nhật dữ liệu mới.
- [ ] Form chi tiết cắt lệnh cập nhật dữ liệu mới.
- [ ] Grid chuyến xe và dữ liệu phiếu liên quan phản ánh customer/product/SL đặt/TL vỏ/TL bao mới.
- [ ] Nếu update lỗi, toast hiển thị thông báo lỗi và không mất selection.

**Files likely touched:**
- `src/StationApp.UI/ViewModels/ExportWeighingViewModel.cs`

**Verification:**
- [ ] Sửa cắt lệnh tạm đã có chuyến, reload màn vẫn thấy dữ liệu mới.
- [ ] In/xem phiếu sau sửa dùng thông tin mới.

### Task 6: Kiểm tra regression các luồng liên quan

**Mô tả:** Đảm bảo sửa cắt lệnh tạm không phá luồng tạo chuyến, chuyển chuyến, xóa chuyến, chốt tổng, map sang cắt lệnh thật.

**Acceptance criteria:**
- [ ] Tạo cắt lệnh tạm mới vẫn hoạt động.
- [ ] Tạo chuyến xe cho cắt lệnh tạm sau sửa vẫn hoạt động.
- [ ] Map cắt lệnh tạm sang cắt lệnh thật vẫn hoạt động.
- [ ] Không cho chốt tổng cắt lệnh tạm như logic hiện tại.
- [ ] Không phát sinh lỗi selected item sau reload.

**Verification:**
- [ ] `dotnet build src\StationApp.UI\StationApp.UI.csproj --no-restore`
- [ ] Chạy các test application liên quan nếu có.
- [ ] Manual smoke test màn Cân xuất khẩu.

## Rủi ro và giảm thiểu

| Rủi ro | Mức độ | Giảm thiểu |
| --- | --- | --- |
| Dữ liệu snapshot chuyến xe lệch với cut order sau sửa | Cao | Update đồng bộ `weighing_session_lines`, `weigh_tickets`, `delivery_tickets` trong cùng transaction |
| Sửa cắt lệnh đã map/chốt gây sai ERP | Cao | Validate chặn trạng thái đã map/chốt/completed/canceled/deleted |
| Lỗi selected item sau reload | Trung bình | Preserve `SelectedCutOrder.CutOrderId`, reload trips sau khi update |
| Người dùng sửa sau khi đã in phiếu | Trung bình | Ghi audit log; nếu cần có thể thêm cảnh báo trong dialog |
| Form tạo bị ảnh hưởng khi tái dùng cho sửa | Trung bình | Giữ constructor tạo mới như cũ, thêm factory/constructor riêng cho edit mode |

## Câu hỏi cần chốt

- Khi cắt lệnh tạm đã có phiếu đã in, có cần hiện cảnh báo trước khi cho lưu không?
- Với cắt lệnh tạm đã có chuyến cân đủ 2 lần, có cho sửa `TL vỏ` và `TL bao` không? Đề xuất: vẫn cho sửa, nhưng phải update snapshot và audit đầy đủ.
- Nút trạng thái không hợp lệ nên **ẩn** hay **hiện disabled**? Theo yêu cầu hiện tại: chỉ hiển thị khi chọn cắt lệnh tạm, và enable nếu chưa chốt tổng. Đề xuất: cắt lệnh tạm đã chốt/map thì vẫn hiện disabled để người dùng hiểu có chức năng nhưng không sửa được.

## Checkpoint hoàn thành

- [ ] Build pass.
- [ ] Không còn text lỗi encoding ở dialog/nút mới.
- [ ] Sửa cắt lệnh tạm chưa có chuyến pass.
- [ ] Sửa cắt lệnh tạm đã có chuyến pass.
- [ ] Audit log hiển thị ở Lịch sử chỉnh sửa.
- [ ] Map sang cắt lệnh thật sau khi sửa vẫn đúng dữ liệu.
