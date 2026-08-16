# Kế hoạch: In biên bản kiểm tra khi vượt dung sai

## 1. Mục tiêu

Khi lưu cân lần 2 cho hàng bao và hệ thống phát hiện trọng lượng hàng vượt quá dung sai cho phép, modal cảnh báo cần có thêm nút **In biên bản**. Người dùng có thể in 2 bản biên bản theo mẫu `BIÊN BẢN KTRA SỐ LƯỢNG HÀNG TRÊN XE....docx`, trong đó hệ thống tự điền trước một số thông tin nghiệp vụ để nhân viên ký/xử lý ngoài giấy.

## 2. Hiện trạng code

- Cảnh báo dung sai đang phát sinh trong `CaptureSessionWeight2UseCase`.
- UI bắt exception `BaggedWeightToleranceExceededException` tại `WeighingViewModel.SaveCapturedWeightAsync`.
- Modal hiện tại dùng `IDialogService.ShowConfirmAsync`, chỉ có 2 lựa chọn: vẫn lưu hoặc hủy.
- Cơ chế in hiện có đang dùng `PrintTemplateDefinition`, `PrintPreviewPageModel`, `WpfPrintService`, `DotMatrixGdiTextPrinter` và overlay text Unicode/GDI cho các mẫu mới.
- Các file mẫu in hiện đã được đặt trong `src/StationApp.UI/Assets/PrintTemplates`; file biên bản hiện đang nằm ở root repo.

## 3. Phạm vi nghiệp vụ

- Chỉ áp dụng tại modal cảnh báo vượt dung sai khi cân lần 2 ở luồng cân xuất hàng nội địa.
- Không thay đổi logic kiểm tra dung sai hiện tại.
- Không tự động in khi vừa phát hiện lỗi; chỉ in khi người dùng bấm **In biên bản**.
- Sau khi in, người dùng vẫn có thể chọn **Vẫn lưu** hoặc **Hủy** như hiện tại.
- Số lượng in mặc định: **2 biên bản**.
- Khi bấm **In biên bản**, mở modal chọn máy in như luồng in phiếu hiện tại.
- Khổ giấy biên bản: **A4**.
- Nếu lượt cân có nhiều cắt lệnh/hàng hóa, dòng **Hàng hóa** hiển thị **tất cả** hàng hóa liên quan.

## 4. Dữ liệu cần tự fill

- Dòng **Ông/bà** thứ nhất:
  - Phần tên: dùng tên hiển thị đầy đủ của tài khoản đang thao tác, ưu tiên `ICurrentUserContext.DisplayName`, fallback `Username`.
  - Các dòng Ông/bà tiếp theo: để trống.
- Dòng **Hàng hóa**:
  - Lấy tên hàng hóa/sản phẩm của cắt lệnh đang bị vượt dung sai.
  - Nếu lượt cân có nhiều cắt lệnh, hiển thị tất cả tên hàng hóa, ưu tiên mỗi hàng hóa một dòng; nếu layout không đủ thì nối bằng dấu `;` và cho field wrap nhiều dòng.
- Phần chân ký **P.CLKD**:
  - Hiển thị tên đầy đủ của tài khoản đang thao tác, cùng nguồn với dòng Ông/bà thứ nhất.

## 5. Hướng thiết kế kỹ thuật

### 5.1. Template biên bản

- Di chuyển file `BIÊN BẢN KTRA SỐ LƯỢNG HÀNG TRÊN XE....docx` vào `src/StationApp.UI/Assets/PrintTemplates`.
- Đổi tên file nội bộ nếu cần cho dễ quản lý, ví dụ `BienBan_KiemTraSoLuongHangTrenXe.docx`.
- Cấu hình `.csproj` để file được copy khi build/publish.
- Giữ nguyên nội dung tĩnh của mẫu; chỉ overlay/fill phần text động.
- Khai báo template theo khổ A4.

### 5.2. Mô hình in riêng cho biên bản

- Mở rộng `PrintDocumentKind` thêm loại mới, ví dụ `OverToleranceInspectionReport`.
- Tạo `OverToleranceInspectionReportPrintModel` kế thừa `PrintPreviewPageModel`.
- Bổ sung template definition/profile riêng trong `PrintTemplateProvider`, gồm các field động tối thiểu:
  - `InspectorName1`
  - `ProductName`
  - `SalesDepartmentSignerName`
- Hỗ trợ chỉnh vị trí field như các mẫu in hiện tại nếu cần căn lại sau khi test.

### 5.3. Dialog cảnh báo dung sai

- Thay `ShowConfirmAsync` hiện tại bằng custom dialog riêng, ví dụ:
  - `OverToleranceWarningDialogViewModel`
  - `OverToleranceWarningDialogWindow`
  - `OverToleranceWarningDialogResult`
- Dialog có 3 hành động:
  - **In biên bản**
  - **Vẫn lưu**
  - **Hủy**
- Nút **In biên bản**:
  - Không đóng modal.
  - Mở modal chọn máy in như khi in phiếu cân/phiếu giao nhận.
  - Disable trong lúc đang in để tránh bấm lặp.
  - Nếu in thành công, hiển thị toast/thông báo gọn.
  - Nếu in lỗi, giữ modal để người dùng có thể thử lại hoặc quyết định tiếp.

### 5.4. Nguồn dữ liệu biên bản

- Không parse từ message lỗi dung sai.
- Khi bắt exception, dùng `SelectedSession.SessionId` để truy vấn lại context đầy đủ:
  - lượt cân;
  - các dòng cắt lệnh liên quan;
  - hàng hóa/sản phẩm;
  - người thao tác hiện tại.
- Tạo use case/service đọc context, ví dụ `BuildOverToleranceInspectionReportUseCase`.

### 5.5. In 2 biên bản

- Có thể thực hiện theo một trong hai cách:
  - Tạo một batch có 2 page giống nhau.
  - Hoặc dùng `PrintOptionsModel.CopyCount = 2`.
- Ưu tiên cách phù hợp với `WpfPrintService` hiện tại sau khi test thực tế để đảm bảo máy in nhận đúng 2 bản.

## 6. Task chi tiết

### Task 1: Chuẩn hóa template biên bản

**Mô tả:** Đưa file mẫu biên bản vào đúng thư mục assets in ấn và cấu hình copy khi build/publish.

**Acceptance criteria:**
- [ ] File mẫu nằm trong `src/StationApp.UI/Assets/PrintTemplates`.
- [ ] Build Debug/Release đều copy được file mẫu ra output.
- [ ] Publish không làm mất file mẫu.

**Verification:**
- [ ] `dotnet build StationApp.sln /p:SkipDatabaseSchemaUpdate=true`
- [ ] Kiểm tra file xuất hiện trong thư mục output/publish.

**Dependencies:** Không.

**Files likely touched:**
- `src/StationApp.UI/StationApp.UI.csproj`
- `src/StationApp.UI/Assets/PrintTemplates/...docx`

### Task 2: Thêm loại document in biên bản

**Mô tả:** Bổ sung document kind/model/template field cho biên bản kiểm tra.

**Acceptance criteria:**
- [ ] Có `PrintDocumentKind.OverToleranceInspectionReport`.
- [ ] Có print model riêng cho biên bản.
- [ ] Template provider trả về đúng layout, kích thước giấy và các field động.

**Verification:**
- [ ] Unit test hoặc test composer kiểm tra đủ field `InspectorName1`, `ProductName`, `SalesDepartmentSignerName`.
- [ ] Build pass.

**Dependencies:** Task 1.

**Files likely touched:**
- `src/StationApp.Application/Printing/PrintContracts.cs`
- `src/StationApp.UI/Printing/PrintTemplateProvider.cs`
- `tests/StationApp.Application.Tests` hoặc `tests/StationApp.UI.Tests` nếu có test phù hợp.

### Task 3: Tạo composer/context cho biên bản

**Mô tả:** Tạo service/use case lấy dữ liệu từ lượt cân đang vượt dung sai và build print model.

**Acceptance criteria:**
- [ ] Tên người thao tác lấy từ `DisplayName`, fallback `Username`.
- [ ] Hàng hóa lấy từ cắt lệnh liên quan đến lượt cân.
- [ ] Các dòng tên người còn lại không tự fill.
- [ ] P.CLKD dùng đúng tên người thao tác.

**Verification:**
- [ ] Unit test với 1 cắt lệnh.
- [ ] Unit test với nhiều cắt lệnh.
- [ ] Unit test fallback khi `DisplayName` trống.

**Dependencies:** Task 2.

**Files likely touched:**
- `src/StationApp.Application/UseCases/...`
- `src/StationApp.Application/Printing/...`
- `tests/StationApp.Application.Tests/...`

### Task 4: Thay modal cảnh báo dung sai bằng custom dialog

**Mô tả:** Tạo modal cảnh báo mới có thêm nút **In biên bản**, vẫn giữ hành vi **Vẫn lưu/Hủy** hiện tại.

**Acceptance criteria:**
- [ ] Modal hiển thị đúng message vượt dung sai hiện tại.
- [ ] Nút **In biên bản** xuất hiện cùng modal.
- [ ] Bấm **In biên bản** không tự động lưu cân lần 2.
- [ ] Bấm **Vẫn lưu** vẫn gọi lại use case với `BypassTolerance = true`.
- [ ] Bấm **Hủy** không lưu cân lần 2.

**Verification:**
- [ ] Manual test case vượt dung sai: in, hủy, vẫn lưu.
- [ ] Không ảnh hưởng case cân không vượt dung sai.

**Dependencies:** Task 3.

**Files likely touched:**
- `src/StationApp.UI/ViewModels/WeighingViewModel.cs`
- `src/StationApp.UI/ViewModels/Dialogs/OverToleranceWarningDialogViewModel.cs`
- `src/StationApp.UI/Views/Dialogs/OverToleranceWarningDialogWindow.xaml`
- `src/StationApp.UI/Views/Dialogs/OverToleranceWarningDialogWindow.xaml.cs`
- `src/StationApp.UI/Services/WpfDialogService.cs` nếu cần map custom dialog.

### Task 5: Wire in thực tế 2 bản

**Mô tả:** Gắn nút **In biên bản** vào service in hiện tại và đảm bảo in đúng 2 bản.

**Acceptance criteria:**
- [ ] In đúng 2 biên bản cho một lần bấm.
- [ ] Khi bấm in, hệ thống mở modal chọn máy in như khi in phiếu.
- [ ] Dùng đúng máy in người dùng chọn trong modal.
- [ ] Template in theo khổ A4.
- [ ] Text tiếng Việt in Unicode, không chuyển sang không dấu.
- [ ] Không làm ảnh hưởng phiếu cân/phiếu giao nhận version hiện tại.

**Verification:**
- [ ] Test với `Microsoft Print to PDF`.
- [ ] Test in thử trên máy in kim nếu có điều kiện.
- [ ] Kiểm tra log khi in lỗi có đủ printer, document kind, session no.

**Dependencies:** Task 4.

**Files likely touched:**
- `src/StationApp.UI/Printing/WpfPrintService.cs`
- `src/StationApp.UI/Printing/DotMatrixGdiTextPrinter.cs`
- `src/StationApp.UI/ViewModels/Dialogs/OverToleranceWarningDialogViewModel.cs`

### Task 6: Kiểm thử hồi quy

**Mô tả:** Chạy test/build và kiểm tra các luồng liên quan.

**Acceptance criteria:**
- [ ] Cân lần 2 không vượt dung sai vẫn lưu bình thường.
- [ ] Cân lần 2 vượt dung sai có modal mới.
- [ ] In biên bản không làm thay đổi trạng thái lượt cân.
- [ ] Vẫn lưu sau khi in vẫn lưu đúng cân lần 2.
- [ ] Hủy sau khi in vẫn không lưu cân lần 2.

**Verification:**
- [ ] `dotnet build StationApp.sln /p:SkipDatabaseSchemaUpdate=true`
- [ ] Chạy test liên quan tới `CaptureSessionWeight2UseCase`.
- [ ] Manual smoke test trên màn Cân nội địa.

**Dependencies:** Task 5.

## 7. Rủi ro và cách xử lý

| Rủi ro | Ảnh hưởng | Cách xử lý |
|---|---|---|
| Mẫu Word không có placeholder rõ ràng | Khó fill đúng vị trí | Dùng cơ chế overlay field theo tọa độ như phiếu cân/phiếu giao nhận version mới |
| Dialog hiện tại chỉ hỗ trợ 2 nút | Không thêm được nút in | Tạo custom dialog riêng cho cảnh báo dung sai |
| In 2 bản bằng `CopyCount` không ổn với một số driver | In thiếu/thừa bản | Test cả `CopyCount = 2` và batch 2 page, chọn cách ổn định hơn |
| Lượt cân có nhiều cắt lệnh/hàng hóa | Dòng Hàng hóa có thể dài | Cho field wrap nhiều dòng, nếu quá dài thì nối gọn theo layout |
| Người dùng chọn sai máy in trong modal | In nhầm máy | Giữ preview/thông tin máy in rõ như modal in phiếu hiện tại |

## 8. Các điểm đã chốt

1. Nút **In biên bản** mở modal chọn máy in như khi in phiếu.
2. Biên bản dùng khổ giấy A4.
3. Nếu một lượt cân có nhiều cắt lệnh/hàng hóa, dòng **Hàng hóa** hiển thị tất cả.
