# Kế hoạch chi tiết: Xác nhận số bao khi lưu cân lần 2 ở màn Cân xuất khẩu

## 1. Bối cảnh và vấn đề

Ở luồng Cân xuất khẩu, số bao là dữ liệu nghiệp vụ quan trọng vì đang được dùng để:

- Hiển thị trên grid `Danh sách chuyến xe xuất khẩu`, cột `BAO`.
- Hiển thị các trường bao trên form chi tiết cắt lệnh: `SL đặt (bao)`, `Lũy kế (bao)`, `Còn lại (bao)`.
- Ghi vào `weighing_session_lines`, `delivery_tickets`, `weigh_tickets`.
- In phiếu và xuất báo cáo.
- Đồng bộ dữ liệu lên central/ERP theo payload hiện có.

Hiện tại hệ thống có thể tính số bao từ trọng lượng hàng và `TL bao` thông qua `BagCountDisplayHelper.Resolve(weightKg, bagWeightKg, fallback)`. Việc tự tính rồi lưu/hiển thị mà không có bước người dùng xác nhận là rủi ro cao, vì số bao thực tế có thể khác số hệ thống suy ra từ cân.

Mục tiêu của plan này là thêm bước xác nhận bắt buộc trước khi lưu cân lần 2 cho chuyến xuất khẩu hàng bao.

## 2. Phạm vi

### Trong phạm vi

- Màn `Cân xuất khẩu`.
- Luồng lưu cân lần 2 của chuyến xe xuất khẩu.
- Modal xác nhận số bao có nút `-` / `+` để điều chỉnh.
- Lưu số bao đã xác nhận vào DB.
- Đảm bảo grid chuyến xe, form chi tiết cắt lệnh, phiếu, báo cáo và sync dùng số bao đã xác nhận.
- Test use case và repository/list item liên quan.

### Ngoài phạm vi

- Không thay đổi công thức cân khối lượng.
- Không thay đổi cách tính `NetWeight`.
- Không thay đổi luồng cân nội địa.
- Không sửa `CutOrder.BagCount` theo từng chuyến xe, vì đây là số bao kế hoạch/tổng của cắt lệnh, không phải số bao thực tế của từng chuyến.

## 3. Hiện trạng code liên quan

### UI màn Cân xuất khẩu

- `src/StationApp.UI/ViewModels/ExportWeighingViewModel.cs`
  - `SaveCapturedWeightAsync()` là điểm xử lý lưu cân lần 1/lần 2.
  - Khi `SelectedTrip.SessionStatus == PENDING_WEIGHT2`, ViewModel gọi `CaptureSessionWeight2UseCase`.

- `src/StationApp.UI/Views/ExportWeighingView.xaml`
  - Grid `Danh sách cắt lệnh xuất khẩu` đang hiển thị:
    - `PlannedBagCountDisplay`
    - `AccumulatedBagCountDisplay`
    - `RemainingBagCountDisplay`
    - `BagWeightKg`
  - Grid `Danh sách chuyến xe xuất khẩu` đang hiển thị cột `BAO` bind `ExportVehicleTripListItem.BagCountDisplay`.

### Application/use case

- `src/StationApp.Application/UseCases/WeighingSessionUseCases.cs`
  - `CaptureSessionWeight2UseCase.ExecuteAsync()`:
    - Tính `netWeight = Abs(weight1 - weight2)`.
    - Lấy line của session.
    - Hiện đang set:
      - `line.ActualAllocatedWeight`
      - `line.ActualAllocatedBagCount`
      - `line.BagCountDisplay`
      - `deliveryTicket.AllocatedBagCount`
    - Sau đó sync master ticket qua `_ticketSyncService.SyncMasterTicketFromSession(...)`.

- `src/StationApp.Application/Formatting/BagCountDisplayHelper.cs`
  - Công thức hiện tại:

```csharp
public static int? Resolve(decimal? weightKg, decimal? bagWeightKg, int? fallback = null)
{
    if (bagWeightKg.HasValue && bagWeightKg.Value > 0m && weightKg.HasValue)
    {
        return (int)decimal.Round(
            weightKg.Value / bagWeightKg.Value,
            0,
            MidpointRounding.AwayFromZero);
    }

    return fallback;
}
```

### DTO/DB hiện có

- `ExportVehicleTripListItem.BagCountDisplay`
- `WeighingSessionLine.PlannedBagCount`
- `WeighingSessionLine.ActualAllocatedBagCount`
- `WeighingSessionLine.BagCountDisplay`
- `DeliveryTicket.AllocatedBagCount`
- `WeighTicket.BagCount`
- `CutOrder.BagCount`
- `CutOrder.BagWeightKg`

## 4. Quyết định thiết kế

### 4.1. Chèn modal trước khi lưu cân lần 2

Điểm chèn đề xuất: trong `ExportWeighingViewModel.SaveCapturedWeightAsync()`, trước khi gọi `CaptureSessionWeight2UseCase` cho `PENDING_WEIGHT2`.

Lý do:

- Đây là thời điểm đã có đủ `Weight1`, `Weight2`, `NetWeight`, `SelectedTrip`, `SelectedCutOrder`.
- Nếu người dùng hủy modal thì không lưu cân lần 2, tránh sinh dữ liệu nửa vời.
- Không làm thay đổi luồng cân lần 1.

### 4.2. Số bao xác nhận là nguồn dữ liệu chính sau cân lần 2

Sau khi người dùng xác nhận, số bao này phải được ghi vào:

- `WeighingSessionLine.ActualAllocatedBagCount`
- `WeighingSessionLine.BagCountDisplay`
- `DeliveryTicket.AllocatedBagCount`

Nếu `WeighTicket.BagCount` đang được dùng cho phiếu hoặc sync theo chuyến, cần cập nhật đồng bộ để tránh lệch giữa phiếu cân và phiếu giao nhận.

### 4.3. Không tự động sửa `CutOrder.BagCount`

`CutOrder.BagCount` là số bao kế hoạch/tổng của cắt lệnh. Một cắt lệnh có thể có nhiều chuyến xe. Nếu mỗi chuyến xe lưu cân lần 2 lại sửa `CutOrder.BagCount`, các chuyến trước/sau và số còn lại có thể bị sai.

Số lũy kế/còn lại nên tính từ các trip đã cân:

```text
AccumulatedBagCountDisplay = tổng BagCountDisplay/ActualAllocatedBagCount của các line đã cân
RemainingBagCountDisplay = PlannedBagCountDisplay - AccumulatedBagCountDisplay
```

### 4.4. Nên lưu audit số bao hệ thống tính và người xác nhận

Khuyến nghị thêm audit fields vào `weighing_session_lines`:

```sql
SystemCalculatedBagCount int NULL
BagCountConfirmedAt datetime2 NULL
BagCountConfirmedBy nvarchar(100) NULL
BagCountConfirmationMode nvarchar(50) NULL
```

Ý nghĩa:

- `SystemCalculatedBagCount`: số bao hệ thống gợi ý theo `NetWeight / BagWeightKg`.
- `BagCountConfirmedAt`: thời điểm xác nhận.
- `BagCountConfirmedBy`: user xác nhận.
- `BagCountConfirmationMode`: `AcceptedSuggested` hoặc `AdjustedManual`.

Plan vẫn có thể triển khai tối thiểu mà không thêm audit fields, nhưng không khuyến nghị vì đây là dữ liệu nhạy cảm và dễ cần truy vết.

## 5. Luồng nghiệp vụ đề xuất

1. Người dùng chọn cắt lệnh xuất khẩu.
2. Người dùng chọn chuyến xe chưa cân lần 2.
3. Người dùng lấy/lưu cân lần 2.
4. App tính:
   - `NetWeight = Abs(Weight1 - Weight2)`
   - `SuggestedBagCount = Round(NetWeight / BagWeightKg, AwayFromZero)`
5. App mở modal `Xác nhận số bao`.
6. Modal hiển thị:
   - Số lượt cân.
   - Biển số xe.
   - Cắt lệnh.
   - Cân lần 1.
   - Cân lần 2.
   - Khối lượng hàng.
   - `TL bao`.
   - Số bao hệ thống tính.
   - Số bao xác nhận.
7. Người dùng:
   - Bấm `Đồng ý` nếu đúng.
   - Bấm `+` / `-` để chỉnh số bao rồi bấm `Đồng ý`.
   - Bấm `Hủy` thì không lưu cân lần 2.
8. App gọi use case lưu cân lần 2 kèm số bao xác nhận.
9. App reload grid chuyến xe và chi tiết cắt lệnh.
10. Grid/form hiển thị số bao đã xác nhận.

## 6. UI modal xác nhận số bao

### Tên đề xuất

- ViewModel: `ConfirmExportBagCountDialogViewModel`
- Result: `ConfirmExportBagCountDialogResult`
- Window: `ConfirmExportBagCountDialogWindow`

### Nội dung modal

Các field chỉ đọc:

- `Số lượt cân`
- `Biển số xe`
- `Cắt lệnh`
- `Cân lần 1`
- `Cân lần 2`
- `Khối lượng hàng`
- `TL bao`
- `Số bao hệ thống tính`

Field chỉnh sửa:

- `Số bao thực tế`

Control:

- Nút `-`
- TextBox số bao
- Nút `+`
- Nút `Đồng ý`
- Nút `Hủy`

### Rule UI

- `Số bao thực tế >= 0`.
- Nút `-` disabled khi số bao là `0`.
- Nút `Đồng ý` disabled nếu số bao không hợp lệ.
- Nếu user nhập trực tiếp, chỉ cho số nguyên không âm.
- Nếu `BagWeightKg` null hoặc `<= 0`, modal vẫn mở nhưng số bao hệ thống tính để trống và bắt buộc user nhập số bao thực tế.

### Copy đề xuất

Title:

```text
Xác nhận số bao
```

Message:

```text
Vui lòng xác nhận số bao thực tế trước khi lưu cân lần 2.
```

Warning nếu số bao chỉnh khác số gợi ý:

```text
Số bao thực tế khác số hệ thống tính. Hệ thống sẽ lưu theo số bao bạn xác nhận.
```

## 7. Thay đổi Application layer

### 7.1. Mở rộng request lưu cân lần 2

File: `src/StationApp.Application/DTOs/Dtos.cs`

Hiện tại:

```csharp
public sealed record CaptureSessionWeightRequest(
    Guid SessionId,
    decimal Weight,
    bool IsStable,
    WeightMode Mode,
    bool BypassTolerance = false
);
```

Đề xuất:

```csharp
public sealed record CaptureSessionWeightRequest(
    Guid SessionId,
    decimal Weight,
    bool IsStable,
    WeightMode Mode,
    bool BypassTolerance = false,
    int? ConfirmedBagCount = null,
    int? SystemCalculatedBagCount = null
);
```

Hoặc nếu muốn tránh ảnh hưởng luồng cân khác, tạo request riêng:

```csharp
public sealed record CaptureExportSessionWeight2Request(
    Guid SessionId,
    decimal Weight,
    bool IsStable,
    WeightMode Mode,
    bool BypassTolerance,
    int ConfirmedBagCount,
    int? SystemCalculatedBagCount
);
```

Khuyến nghị: mở rộng `CaptureSessionWeightRequest` để ít thay đổi wiring, nhưng chỉ validate bắt buộc trong điều kiện `OUTBOUND + ExportScale + hàng bao`.

### 7.2. Validate bắt buộc số bao xác nhận

Trong `CaptureSessionWeight2UseCase.ExecuteAsync()`:

- Xác định session là chuyến xuất khẩu hàng bao.
- Nếu đúng, yêu cầu `request.ConfirmedBagCount.HasValue`.
- Nếu thiếu thì throw `InvalidOperationException`.

Điều kiện nhận diện đề xuất:

```text
session.TransactionType == OUTBOUND
line count == 1
CutOrder.IsExportScale == true
ProductType normalize == ProductTypes.Bagged
```

Nếu chưa chắc `ProductType`, resolve qua `ProductCode` giống logic validate dung sai hiện có.

### 7.3. Ghi số bao xác nhận

Thay đoạn hiện tại:

```csharp
var actualAllocatedBagCount = WeighingSessionBagCountHelper.ResolveActualBagCount(
    registration.ProductType,
    registration.BagCount,
    lineToAutoAllocate.PlannedBagCount);

lineToAutoAllocate.ActualAllocatedBagCount = actualAllocatedBagCount;
lineToAutoAllocate.BagCountDisplay = BagCountDisplayHelper.Resolve(
    actualAllocatedWeight,
    registration.BagWeightKg,
    actualAllocatedBagCount);
```

Với export bagged flow:

```csharp
var actualAllocatedBagCount = request.ConfirmedBagCount;

lineToAutoAllocate.ActualAllocatedBagCount = actualAllocatedBagCount;
lineToAutoAllocate.BagCountDisplay = actualAllocatedBagCount;
```

Fallback cho các flow khác giữ nguyên như hiện tại.

### 7.4. Ghi vào phiếu giao nhận

Đảm bảo:

```csharp
deliveryTicket.AllocatedBagCount = actualAllocatedBagCount;
```

Với export bagged flow, giá trị này là số bao đã xác nhận.

### 7.5. Ghi audit nếu thêm field

Nếu có audit fields:

```csharp
lineToAutoAllocate.SystemCalculatedBagCount = request.SystemCalculatedBagCount;
lineToAutoAllocate.BagCountConfirmedAt = now;
lineToAutoAllocate.BagCountConfirmedBy = _userContext.Username;
lineToAutoAllocate.BagCountConfirmationMode =
    request.SystemCalculatedBagCount == request.ConfirmedBagCount
        ? "AcceptedSuggested"
        : "AdjustedManual";
```

## 8. Thay đổi DB

### 8.1. Migration mới

Nếu chọn có audit, tạo migration:

```text
AddExportBagCountConfirmationFields
```

Thêm cột vào `weighing_session_lines`:

```sql
SystemCalculatedBagCount int NULL
BagCountConfirmedAt datetime2 NULL
BagCountConfirmedBy nvarchar(100) NULL
BagCountConfirmationMode nvarchar(50) NULL
```

### 8.2. Entity/configuration

Files cần cập nhật:

- `src/StationApp.Domain/Entities/WeighingSessionLine.cs`
- `src/StationApp.Infrastructure/Persistence/Configurations/...`
- `src/StationApp.Infrastructure/Migrations/...`
- `src/StationApp.Infrastructure/Persistence/SchemaCompatibilityBootstrapper.cs` nếu đang có cơ chế auto add column cho local/central.

### 8.3. Sync/central

Nếu các cột mới cần đồng bộ central:

- Cập nhật schema central auto bootstrap nếu có.
- Cập nhật payload factory cho `WeighingSessionLine`.
- Cập nhật Central API DTO/entity nếu central nhận line payload có schema tường minh.

Nếu không muốn thay central ngay, audit fields có thể local-only trong phase 1, nhưng cần ghi rõ để tránh kỳ vọng sai.

## 9. Thay đổi Infrastructure/repository

### 9.1. Grid chuyến xe

File: `src/StationApp.Infrastructure/Repositories/CutOrderRepository.cs`

Method liên quan:

- `GetExportVehicleTripsAsync(cutOrderId, ct)`

Yêu cầu:

- `ExportVehicleTripListItem.BagCountDisplay` ưu tiên:

```text
line.BagCountDisplay
?? line.ActualAllocatedBagCount
?? BagCountDisplayHelper.Resolve(line.ActualAllocatedWeight, cutOrder.BagWeightKg, line.PlannedBagCount)
```

Với chuyến đã cân lần 2, `line.BagCountDisplay` phải là số bao đã xác nhận.

### 9.2. Form chi tiết cắt lệnh

Method liên quan:

- `GetActiveExportScaleCutOrdersAsync(...)`

Yêu cầu:

- `AccumulatedBagCountDisplay` tính từ số bao thực tế đã xác nhận của các line.
- `RemainingBagCountDisplay` = planned - accumulated.
- Không tính lại số bao theo weight nếu line đã có `BagCountDisplay`/`ActualAllocatedBagCount`.

## 10. Thay đổi UI ViewModel

File: `src/StationApp.UI/ViewModels/ExportWeighingViewModel.cs`

### 10.1. Thêm bước xác nhận trong `SaveCapturedWeightAsync()`

Pseudo flow:

```csharp
else if (SelectedTrip.SessionStatus == WeighingSessionStatus.PENDING_WEIGHT2)
{
    if (!await ConfirmNegativeRemainingWeightAsync())
    {
        return;
    }

    var confirmation = await ConfirmExportBagCountIfNeededAsync();
    if (confirmation.Cancelled)
    {
        return;
    }

    await uc.ExecuteAsync(
        new CaptureSessionWeightRequest(
            selectedTripId,
            _pendingCapturedWeight2!.Value,
            _pendingWeight2IsStable,
            _pendingWeight2Mode,
            BypassTolerance: false,
            ConfirmedBagCount: confirmation.ConfirmedBagCount,
            SystemCalculatedBagCount: confirmation.SystemCalculatedBagCount),
        CancellationToken.None);
}
```

### 10.2. Tính số bao gợi ý ở UI

Inputs:

- `SelectedTrip.Weight1`
- `_pendingCapturedWeight2`
- `SelectedCutOrder.BagWeightKg`

Formula:

```csharp
var netWeight = Math.Abs(SelectedTrip.Weight1.Value - _pendingCapturedWeight2.Value);
var suggested = BagCountDisplayHelper.Resolve(netWeight, SelectedCutOrder.BagWeightKg);
```

Nếu thiếu `Weight1` hoặc `_pendingCapturedWeight2`, không mở modal mà báo lỗi không đủ dữ liệu.

### 10.3. Chỉ áp dụng khi cần

Điều kiện mở modal:

```text
SelectedTrip.SessionStatus == PENDING_WEIGHT2
SelectedCutOrder != null
SelectedCutOrder.BagWeightKg.HasValue && SelectedCutOrder.BagWeightKg > 0
```

Nếu muốn bắt buộc cho mọi chuyến xuất khẩu hàng bao, kể cả thiếu `TL bao`, cần thêm thông tin product type vào DTO/UI hoặc resolve từ repository. Khi thiếu `TL bao`, modal bắt buộc nhập tay.

Khuyến nghị phase 1:

- Nếu `BagWeightKg > 0`: mở modal với số gợi ý.
- Nếu `BagWeightKg` thiếu nhưng cắt lệnh có `PlannedBagCountDisplay`: mở modal với fallback.
- Nếu cả hai thiếu: mở modal bắt nhập tay.

## 11. Thay đổi Dialog service/UI

Files dự kiến:

- `src/StationApp.UI/ViewModels/Dialogs/ConfirmExportBagCountDialogViewModel.cs`
- `src/StationApp.UI/Views/Dialogs/ConfirmExportBagCountDialogWindow.xaml`
- `src/StationApp.UI/Views/Dialogs/ConfirmExportBagCountDialogWindow.xaml.cs`
- Đăng ký DI trong `App.xaml.cs` nếu pattern dialog hiện tại yêu cầu.

Acceptance criteria:

- Modal hiển thị số bao gợi ý.
- Bấm `+` tăng 1.
- Bấm `-` giảm 1, không cho nhỏ hơn 0.
- Nhập trực tiếp số nguyên không âm được.
- `Đồng ý` trả result.
- `Hủy` không lưu cân lần 2.

## 12. Phiếu in, báo cáo, sync

### 12.1. Phiếu in

Rà các điểm lấy số bao:

- `ExportWeighingViewModel.ResolvePrintActualBagCount(...)`
- `PrintContracts`
- Composer phiếu cân/phiếu giao nhận.

Yêu cầu:

- Ưu tiên `ActualAllocatedBagCount`.
- Fallback `BagCountDisplay`.
- Không tự tính lại nếu line đã có số xác nhận.

### 12.2. Báo cáo xuất

File liên quan:

- `src/StationApp.Infrastructure/Services/ExportSummaryReportServices.cs`

Yêu cầu:

- `ActualBagCount` lấy từ số bao đã xác nhận.
- `Kg/bao` và `Tỉ lệ` tính theo số bao xác nhận.
- Nếu số bao xác nhận khác số hệ thống tính, báo cáo vẫn theo số xác nhận.

### 12.3. Sync

Rà:

- `SyncPayloadFactory`
- Outbox cho `WeighingSessionLine`, `DeliveryTicket`, `WeighTicket`.

Yêu cầu:

- Payload line/delivery/ticket chứa số bao đã xác nhận.
- Nếu thêm audit fields và central cần nhận, payload phải bổ sung fields tương ứng.

## 13. Kế hoạch triển khai theo task

### Task 1: Chốt contract số bao xác nhận

Mô tả:

Xác định DTO/request và quy tắc validate cho số bao xác nhận.

Files dự kiến:

- `src/StationApp.Application/DTOs/Dtos.cs`
- `src/StationApp.Application/UseCases/WeighingSessionUseCases.cs`

Acceptance criteria:

- Request lưu cân lần 2 nhận được `ConfirmedBagCount`.
- Không ảnh hưởng compile các call site hiện tại.
- Luồng không export hoặc không hàng bao vẫn chạy như cũ.

Verification:

- `dotnet build src/StationApp.UI/StationApp.UI.csproj -c Debug --no-restore`

### Task 2: Cập nhật use case lưu cân lần 2

Mô tả:

Sửa `CaptureSessionWeight2UseCase` để dùng số bao xác nhận cho chuyến xuất khẩu hàng bao.

Files dự kiến:

- `src/StationApp.Application/UseCases/WeighingSessionUseCases.cs`

Acceptance criteria:

- Thiếu `ConfirmedBagCount` trong export bagged flow thì reject.
- Có `ConfirmedBagCount` thì ghi vào line và delivery ticket.
- Nếu có audit fields, ghi đủ audit.

Verification:

- Unit test use case.
- Build application.

### Task 3: Thêm migration/audit fields

Mô tả:

Thêm audit fields để truy vết số bao hệ thống tính và số bao người dùng xác nhận.

Files dự kiến:

- `src/StationApp.Domain/Entities/WeighingSessionLine.cs`
- `src/StationApp.Infrastructure/Persistence/Configurations/...`
- `src/StationApp.Infrastructure/Migrations/...`
- `src/StationApp.Infrastructure/Persistence/SchemaCompatibilityBootstrapper.cs`

Acceptance criteria:

- DB local tự có cột mới khi migrate/bootstrap.
- App đọc/ghi được audit fields.
- Không làm hỏng DB cũ chưa có dữ liệu.

Verification:

- Build infrastructure.
- Chạy migration hoặc schema bootstrap trên DB dev.

### Task 4: Tạo modal xác nhận số bao

Mô tả:

Tạo dialog ViewModel + XAML cho bước xác nhận số bao.

Files dự kiến:

- `src/StationApp.UI/ViewModels/Dialogs/ConfirmExportBagCountDialogViewModel.cs`
- `src/StationApp.UI/Views/Dialogs/ConfirmExportBagCountDialogWindow.xaml`
- `src/StationApp.UI/Views/Dialogs/ConfirmExportBagCountDialogWindow.xaml.cs`

Acceptance criteria:

- Modal hiển thị đủ thông tin cân.
- Có nút `-` / `+`.
- Cho nhập số bao nguyên không âm.
- `Đồng ý` trả số bao xác nhận.
- `Hủy` trả null/cancel.

Verification:

- Build UI.
- Manual test modal.

### Task 5: Tích hợp modal vào màn Cân xuất khẩu

Mô tả:

Chèn `ConfirmExportBagCountIfNeededAsync()` vào `ExportWeighingViewModel.SaveCapturedWeightAsync()`.

Files dự kiến:

- `src/StationApp.UI/ViewModels/ExportWeighingViewModel.cs`

Acceptance criteria:

- Khi lưu cân lần 2 chuyến xuất khẩu hàng bao, modal mở trước khi lưu.
- Bấm `Hủy` không lưu cân lần 2.
- Bấm `Đồng ý` lưu cân lần 2 kèm số bao xác nhận.
- Nếu lưu bị warning dung sai, khi user xác nhận “Vẫn lưu” vẫn dùng cùng số bao đã xác nhận, không bắt xác nhận lại vô lý.

Verification:

- Manual flow: cân lần 2, xác nhận số bao đúng.
- Manual flow: chỉnh số bao bằng `+/-`.
- Manual flow: hủy modal.
- Build UI.

### Task 6: Cập nhật repository/list item hiển thị số bao

Mô tả:

Đảm bảo grid chuyến xe và form chi tiết cắt lệnh ưu tiên số bao đã xác nhận.

Files dự kiến:

- `src/StationApp.Infrastructure/Repositories/CutOrderRepository.cs`
- `src/StationApp.Application/DTOs/Dtos.cs` nếu cần thêm field audit/display.

Acceptance criteria:

- Cột `BAO` ở grid chuyến xe hiển thị đúng số xác nhận.
- `Lũy kế (bao)` cộng đúng các chuyến đã xác nhận.
- `Còn lại (bao)` trừ đúng số đã xác nhận.
- Reload màn không làm số bao quay lại số tự tính.

Verification:

- Test repository nếu có.
- Manual reload màn sau cân lần 2.

### Task 7: Cập nhật phiếu, báo cáo, sync

Mô tả:

Rà tất cả nơi lấy số bao để tránh dùng lại số tự tính.

Files dự kiến:

- `src/StationApp.UI/ViewModels/ExportWeighingViewModel.cs`
- `src/StationApp.Application/Printing/...`
- `src/StationApp.Infrastructure/Services/ExportSummaryReportServices.cs`
- `src/StationApp.Infrastructure/Services/InfrastructureServices.cs` hoặc sync payload factory hiện có.

Acceptance criteria:

- Phiếu cân hiển thị số bao xác nhận.
- Phiếu giao nhận hiển thị số bao xác nhận.
- Báo cáo xuất dùng số bao xác nhận cho `Thực xuất (bao)`, `Kg/bao`, `Tỉ lệ`.
- Payload sync không mất số bao xác nhận.

Verification:

- In preview/print test nếu có.
- Xuất báo cáo test.
- Kiểm tra payload outbox.

### Task 8: Test regression

Mô tả:

Thêm test đảm bảo lỗi không tái phát.

Test case đề xuất:

- Lưu cân lần 2 export bagged với:
  - `NetWeight = 50_000`
  - `BagWeightKg = 50`
  - hệ thống gợi ý `1_000`
  - user xác nhận `998`
  - DB phải lưu `998`.
- Thiếu `ConfirmedBagCount` thì throw.
- `DeliveryTicket.AllocatedBagCount = 998`.
- `GetExportVehicleTripsAsync()` trả `BagCountDisplay = 998`.
- `GetActiveExportScaleCutOrdersAsync()` tính lũy kế/còn lại theo `998`.

Verification:

- `dotnet test tests/StationApp.Application.Tests/StationApp.Application.Tests.csproj --no-restore -v:q`
- Nếu có integration tests:
  - `dotnet test tests/StationApp.IntegrationTests/StationApp.IntegrationTests.csproj --no-restore -v:q`
- `dotnet build src/StationApp.UI/StationApp.UI.csproj -c Debug --no-restore`

## 14. Checkpoint triển khai

### Checkpoint 1: Contract + use case

- Build pass.
- Test use case pass.
- Không có thay đổi UI.

### Checkpoint 2: Modal + ViewModel

- Build UI pass.
- Manual test modal:
  - đồng ý
  - hủy
  - chỉnh `+/-`
  - nhập tay

### Checkpoint 3: End-to-end

- Tạo chuyến xuất khẩu.
- Cân lần 1.
- Cân lần 2.
- Modal hiện.
- Chỉnh số bao.
- Lưu.
- Reload màn.
- Grid chuyến xe hiển thị số bao xác nhận.
- Form chi tiết cắt lệnh lũy kế/còn lại đúng.
- Phiếu/báo cáo đúng.

## 15. Rủi ro và giảm thiểu

| Rủi ro | Mức độ | Giảm thiểu |
|---|---:|---|
| Số bao hiển thị lại bị tính lại từ weight sau reload | Cao | Repository phải ưu tiên `line.BagCountDisplay`/`ActualAllocatedBagCount` trước khi fallback công thức |
| Người dùng hủy modal nhưng cân lần 2 vẫn bị lưu | Cao | Modal phải chạy trước use case; cancel thì return ngay |
| Lưu lần đầu fail dung sai, bấm “Vẫn lưu” làm mất số bao đã xác nhận | Cao | Giữ result modal trong biến local và truyền lại khi retry với `BypassTolerance = true` |
| Cắt lệnh nhiều chuyến bị sửa nhầm `CutOrder.BagCount` | Cao | Không update `CutOrder.BagCount` trong lưu cân lần 2 |
| Báo cáo xuất dùng số tự tính thay vì số xác nhận | Trung bình | Rà `ExportSummaryReportServices` và thêm test |
| Central DB thiếu cột audit mới | Trung bình | Cập nhật migration/bootstrap central hoặc giữ audit local-only có ghi chú |
| Người dùng phải bấm `+/-` quá nhiều nếu lệch lớn | Trung bình | Cho nhập trực tiếp số bao bên cạnh nút `+/-` |

## 16. Câu hỏi cần chốt

1. Modal xác nhận số bao áp dụng cho mọi chuyến xuất khẩu, hay chỉ khi cắt lệnh có `TL bao > 0`?
2. Có bắt buộc nhập số bao nếu thiếu `TL bao` không?
3. Có đồng ý thêm audit fields vào DB không?
4. Audit fields có cần đồng bộ lên central không, hay chỉ lưu local để truy vết?
5. Có cho nhập trực tiếp số bao ngoài nút `+/-` không? Khuyến nghị: có.

## 17. Đề xuất triển khai mặc định

Nếu không có yêu cầu khác, nên triển khai theo hướng:

- Bắt buộc xác nhận khi lưu cân lần 2 cho chuyến xuất khẩu hàng bao.
- Modal có cả nút `+/-` và ô nhập trực tiếp.
- Số bao xác nhận lưu vào `ActualAllocatedBagCount`, `BagCountDisplay`, `DeliveryTicket.AllocatedBagCount`.
- Thêm audit fields vào `weighing_session_lines`.
- Không sửa `CutOrder.BagCount`.
- Repository/báo cáo/phiếu ưu tiên số bao đã xác nhận.

