# Kế hoạch nâng cấp chốt tổng xuất khẩu: số lượng không qua cân

## 1. Mục tiêu

Ở luồng Cân xuất khẩu, một cắt lệnh/tàu có thể có thêm sản lượng vận chuyển không đi qua cân. Khi chốt tổng cần cho phép nhập số lượng này, lưu lại trên cắt lệnh, hiển thị ở form thông tin cắt lệnh và cộng vào số lượng thực xuất gửi/trả ERP.

Quy ước đã chốt:
- Người dùng nhập số lượng không qua cân theo đơn vị tấn ở modal chốt tổng.
- Lưu trong DB theo đơn vị kg, cùng chuẩn với `PlannedWeight`, `AccumulatedWeight`, `ExportFinalizedWeight`.
- Khi nhận input từ UI cần quy đổi tấn sang kg trước khi gọi use case.
- Hiển thị số lượng không qua cân ở cả form thông tin cắt lệnh và grid danh sách cắt lệnh.
- Không cho sửa lại số lượng không qua cân sau khi đã chốt tổng.

## 2. Hiện trạng code đã rà soát

- `ExportWeighingViewModel.FinalizeAsync` đang dùng confirm đơn giản rồi gọi:
  `FinalizeExportCutOrderRequest(SelectedCutOrder.CutOrderId)`.
- `FinalizeExportCutOrderUseCase` đang tính:
  `ExportFinalizedWeight = tổng ActualAllocatedWeight của các chuyến hợp lệ`, có xử lý chuyến hoàn bằng `ExportReturnedBrokenTripHelper.ResolveSignedWeight`.
- `CutOrder` hiện có `ExportFinalizedWeight`, `ExportFinalizedAt`, `ExportFinalizedBy`, nhưng chưa có trường lưu riêng số lượng không qua cân.
- `CutOrderNetWeightHelper.ResolveDeliveryTicketActualWeightKg` đã ưu tiên `ExportFinalizedWeight` cho cắt lệnh xuất khẩu đã chốt. Đây là điểm thuận lợi: nếu `ExportFinalizedWeight` được set bằng `qua cân + không qua cân`, các luồng in/trả số liệu dùng helper này sẽ đi theo tổng đúng.
- `GetExportCutOrdersAsync` hiện tính `AccumulatedWeight` từ các chuyến qua cân và `RemainingWeight = PlannedWeight - AccumulatedWeight`.
- `ExportWeighingView.xaml` form thông tin cắt lệnh đang hiển thị `Lũy kế`, `Còn lại`, `Ghi chú`; chưa có dòng/ô cho số lượng không qua cân và thực xuất.
- `SchemaCompatibilityBootstrapper` và `CentralApi.Program` đang có pattern tự bổ sung cột `ExportFinalizedWeight`; cần bổ sung cột mới ở cả local station DB và central sync DB.

## 3. Thiết kế dữ liệu

Thêm trường mới trên `CutOrder`:

- `ExportUnweighedWeight decimal(18,3) NOT NULL DEFAULT 0`

Ý nghĩa:
- Số lượng xuất khẩu không qua cân, nhập tại thời điểm chốt tổng.
- Giá trị mặc định 0 cho dữ liệu cũ.
- Không thay thế `AccumulatedWeight`; `AccumulatedWeight` vẫn là tổng qua cân.
- `ExportFinalizedWeight` sau chốt sẽ là:
  `tổng qua cân hợp lệ + ExportUnweighedWeight`.

Các file cần sửa:
- `src/StationApp.Domain/Entities/CutOrder.cs`
- `src/StationApp.Infrastructure/Persistence/Configurations/CutOrderEntityConfiguration.cs`
- `src/StationApp.Infrastructure/Persistence/SchemaCompatibilityBootstrapper.cs`
- `src/StationApp.CentralApi/Program.cs`
- Migration mới trong `src/StationApp.Infrastructure/Migrations` nếu project đang dùng migration để triển khai schema.

## 4. DTO và repository

Mở rộng `ExportScaleCutOrderListItem`:
- Thêm `ExportUnweighedWeight`.
- Thêm computed property nếu cần:
  - `ActualExportWeight = AccumulatedWeight + ExportUnweighedWeight`
  - `ActualRemainingWeight = PlannedWeight - ActualExportWeight`

Điều chỉnh `CutOrderRepository.GetExportCutOrdersAsync`:
- Map `co.ExportUnweighedWeight`.
- Giữ `AccumulatedWeight` là số qua cân.
- Cân nhắc đổi `RemainingWeight` sang còn lại theo thực xuất dự kiến:
  `PlannedWeight - (AccumulatedWeight + ExportUnweighedWeight)`.

Khuyến nghị:
- Trên UI nên ghi nhãn rõ:
  - `Lũy kế qua cân`
  - `Không qua cân`
  - `Thực xuất`
  - `Còn lại`
- Như vậy không làm người dùng hiểu nhầm `Lũy kế` đã bao gồm phần nhập tay.

## 5. Modal chốt tổng

Thay confirm đơn giản bằng modal nhập liệu riêng.

Nội dung modal:
- Cắt lệnh/tàu.
- Sản phẩm/khách hàng nếu có.
- Tổng qua cân, hiển thị theo tấn.
- Ô nhập `Số lượng không qua cân (tấn)`.
- Tổng thực xuất dự kiến = `Tổng qua cân + Số lượng không qua cân`, hiển thị theo tấn.
- Nút `Hủy` và `Chốt tổng`.

Validation:
- Cho phép 0.
- Không cho âm.
- Không cho nhập sai định dạng số.
- Giá trị nhập ở modal là tấn; ViewModel quy đổi sang kg để truyền xuống application layer.
- Nếu tổng thực xuất <= 0 thì vẫn giữ rule hiện tại: không cho chốt khi không có chuyến hợp lệ.
- Nếu tổng thực xuất vượt số lượng đặt, không tự chặn nếu nghiệp vụ hiện tại vẫn cho phép lệch; chỉ cảnh báo nếu cần.

Các file dự kiến:
- Tạo `FinalizeExportCutOrderDialogViewModel`.
- Tạo view/modal tương ứng, theo style các modal hiện có.
- Sửa `ExportWeighingViewModel.FinalizeAsync` để mở modal và nhận kết quả.

## 6. Use case chốt tổng

Sửa request:

```csharp
public sealed record FinalizeExportCutOrderRequest(
    Guid CutOrderId,
    decimal ExportUnweighedWeight);
```

Sửa `FinalizeExportCutOrderUseCase`:
- `ExportUnweighedWeight` trong request là kg.
- Validate `ExportUnweighedWeight >= 0`.
- Tính `weighedWeight` từ các chuyến hợp lệ như hiện tại.
- Set:
  - `cutOrder.ExportUnweighedWeight = request.ExportUnweighedWeight`
  - `cutOrder.ExportFinalizedWeight = weighedWeight + request.ExportUnweighedWeight`
  - Các trường finalized/status/sync như hiện tại.
- Giữ logic loại chuyến hoàn bằng helper hiện tại.
- Nếu đã chốt rồi thì không ghi đè, giữ behavior idempotent hiện tại.

## 7. Hiển thị trên form thông tin cắt lệnh

Sửa `ExportWeighingView.xaml`:
- Thêm field `SL khác` để hiển thị số lượng không qua cân.
- Thêm field `Thực xuất`.
- Field `SL khác` đặt cùng dòng với `Ghi chú`, chia bố cục 50/50:
  - Bên trái: `SL khác`.
  - Bên phải: `Ghi chú`.
- Form hiển thị theo tấn để đồng bộ với các label hiện có.

Grid danh sách cắt lệnh:
- Thêm cột `KHÔNG QUA CÂN (KG)`.
- Thêm cột `THỰC XUẤT (KG)`.
- Cột `LŨY KẾ (KG)` vẫn là số qua cân, không cộng phần nhập tay.
- Cột `CÒN LẠI (KG)` nên tính theo `SL đặt - Thực xuất` để phản ánh đủ số lượng đã xuất thực tế.

## 8. ERP/sync

Mục tiêu là ERP nhận số lượng thực xuất đã cộng thêm không qua cân.

Cần rà và sửa các điểm:
- Nơi tạo payload CutOrder: `SyncPayloadFactory.CreatePayload(CutOrder)` đang serialize nguyên entity, nên khi thêm `ExportUnweighedWeight` vào entity payload sẽ tự có trường mới.
- Nơi tính số lượng thực xuất cho phiếu/giao nhận đang dùng `CutOrderNetWeightHelper`, helper đã ưu tiên `ExportFinalizedWeight`. Khi use case set `ExportFinalizedWeight = qua cân + không qua cân`, luồng này sẽ đúng.
- Rà các service outbound ERP nếu có điểm nào tự sum lại từ `WeighingSessionLine.ActualAllocatedWeight`; các điểm đó phải đổi sang ưu tiên `CutOrder.ExportFinalizedWeight` khi đã chốt.
- Bổ sung schema central để sync nhận được trường `ExportUnweighedWeight`.

## 9. Audit/log

Nếu hiện tại chốt tổng chưa ghi audit log, nên bổ sung tối thiểu:
- Cắt lệnh.
- Tổng qua cân.
- Số lượng không qua cân.
- Tổng thực xuất cuối.
- Người thao tác và thời điểm.

Nếu không muốn mở rộng scope ngay, có thể ghi log ứng dụng trước và để audit log thành bước sau. Tuy nhiên nghiệp vụ nhập tay ảnh hưởng số liệu ERP nên khuyến nghị ghi audit trong cùng đợt.

## 10. Test cần bổ sung

Unit test trong `tests/StationApp.Application.Tests/ExportScaleUseCasesTests.cs`:
- Chốt tổng với qua cân 2.500 kg và không qua cân 700 kg thì:
  - `ExportUnweighedWeight = 700`
  - `ExportFinalizedWeight = 3.200`
  - `SyncStatus = SYNC_QUEUED`
- Chốt tổng với không qua cân = 0 giữ behavior hiện tại.
- Không cho nhập không qua cân âm.
- Chuyến hoàn vẫn bị trừ trước khi cộng không qua cân.

Test helper/payload nếu có sẵn:
- Cắt lệnh export đã chốt thì `CutOrderNetWeightHelper` trả `ExportFinalizedWeight`.
- Payload CutOrder có trường `exportUnweighedWeight`.

## 11. Thứ tự triển khai đề xuất

1. Thêm field DB/entity/config/bootstrap/local migration/central schema.
2. Sửa DTO + repository để load và hiển thị số lượng không qua cân.
3. Tạo modal chốt tổng có ô nhập số lượng không qua cân.
4. Sửa `FinalizeExportCutOrderUseCase` để lưu `ExportUnweighedWeight` và set `ExportFinalizedWeight` bằng tổng thực xuất.
5. Sửa UI form thông tin cắt lệnh.
6. Rà payload ERP/sync, chỉnh các điểm còn tự sum qua cân nếu có.
7. Bổ sung audit log.
8. Bổ sung test.
9. Build/test:
   - `dotnet test tests/StationApp.Application.Tests/StationApp.Application.Tests.csproj`
   - `dotnet build src/StationApp.UI/StationApp.UI.csproj`

## 12. Câu hỏi cần chốt trước khi code

Đã chốt:
1. Ô nhập trên modal chốt tổng nhập theo tấn.
2. Không cho sửa lại số lượng không qua cân sau khi đã chốt tổng.
3. Hiển thị số lượng không qua cân và thực xuất ở cả form thông tin cắt lệnh và grid danh sách cắt lệnh.

## 13. Tiêu chí nghiệm thu

- Khi bấm `CHỐT TỔNG`, modal cho nhập số lượng không qua cân.
- Chốt thành công lưu được số lượng không qua cân trên cắt lệnh.
- Form thông tin cắt lệnh hiển thị được số lượng không qua cân và tổng thực xuất.
- `ExportFinalizedWeight` bằng `tổng qua cân hợp lệ + không qua cân`.
- Dữ liệu trả ERP/sync dùng tổng thực xuất đã cộng phần không qua cân.
- Dữ liệu cũ không lỗi vì cột mới có default 0.
