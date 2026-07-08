# Kế hoạch: Chốt thực xuất xuất khẩu theo quy cách Đóng bao / Rời

## 1. Bối cảnh

Ở luồng Cân xuất khẩu, một cắt lệnh có thể thuộc 2 quy cách:

- **Đóng bao**: số lượng thực xuất trả ERP phải dựa trên số bao thực tế đã xác nhận.
- **Rời**: số lượng thực xuất trả ERP phải dựa trên lũy kế tấn/kg cân thực tế.

Yêu cầu mới là bổ sung trường **Loại** ở modal tạo/sửa cắt lệnh tạm và lưu xuống DB để làm căn cứ khi chốt tổng và khi ERP lấy số lượng thực xuất.

**Loại là trường bắt buộc.** Người dùng phải chọn `Đóng bao` hoặc `Rời` khi tạo/sửa cắt lệnh tạm; use case cũng phải validate bắt buộc để tránh dữ liệu thiếu quy cách.

## 2. Quy tắc nghiệp vụ

### 2.1 Loại = Đóng bao

Modal tạo/sửa cắt lệnh tạm:

- Dropdown `Loại` chọn `Đóng bao`.
- `Loại` bắt buộc chọn.
- Trường `TL vỏ` enable và bắt buộc nhập.
- Trường `TL bao` enable và bắt buộc nhập.
- `TL vỏ >= 0`.
- `TL bao > 0`.

Khi chốt cắt lệnh xuất khẩu:

```text
Số lượng qua cân (kg) = Lũy kế bao * TL bao
Số lượng thực xuất ERP (kg) = Số lượng qua cân + SL khác
```

Trong đó `Lũy kế bao` phải tính số bao ròng, có trừ chuyến Hoàn.

### 2.2 Loại = Rời

Modal tạo/sửa cắt lệnh tạm:

- Dropdown `Loại` chọn `Rời`.
- `Loại` bắt buộc chọn.
- Trường `TL vỏ` disable.
- Trường `TL bao` disable.
- Không bắt buộc nhập `TL vỏ`, `TL bao`.
- Khi lưu nên set `TareWeightKg = null/0` và `BagWeightKg = null/0` theo pattern hiện tại; khuyến nghị dùng `0` nếu các cột hiện đang non-null trong request/use case.

Khi chốt cắt lệnh xuất khẩu:

```text
Số lượng qua cân (kg) = Lũy kế kg/tấn cân thực tế
Số lượng thực xuất ERP (kg) = Số lượng qua cân + SL khác
```

Trong đó `Lũy kế kg/tấn` là tổng `ActualAllocatedWeight` ròng, có trừ chuyến Hoàn.

## 3. Thiết kế dữ liệu

### 3.1 Thêm trường DB phản ánh quy cách

Thêm cột mới vào `cut_orders`, ví dụ:

```sql
ExportPackageType nvarchar(30) NULL
```

Giá trị đề xuất:

```text
BAGGED = Đóng bao
BULK = Rời
```

Lý do dùng mã tiếng Anh trong DB:

- Tránh lỗi encoding.
- Dễ so sánh trong code/SQL.
- UI vẫn hiển thị tiếng Việt `Đóng bao`, `Rời`.

Ghi chú triển khai:

- Với DB hiện hữu, cột nên tạo `NULL` để migration an toàn với dữ liệu cũ.
- Với dữ liệu mới từ app, `ExportPackageType` là bắt buộc ở tầng UI/use case.
- Nếu sau khi backfill dữ liệu cũ ổn định, có thể cân nhắc migration sau để đổi cột sang `NOT NULL`.

### 3.2 Domain constants

Tạo constants, ví dụ:

```csharp
public static class ExportPackageTypes
{
    public const string Bagged = "BAGGED";
    public const string Bulk = "BULK";
}
```

Không nên dùng `ProductType` để thay thế trường này vì `ProductType` là phân loại sản phẩm, còn yêu cầu mới cần phản ánh **quy cách xuất khẩu của cắt lệnh**.

## 4. Các điểm code cần sửa

### 4.1 Entity / EF / schema bootstrap

File dự kiến:

- `src/StationApp.Domain/Entities/CutOrder.cs`
- `src/StationApp.Infrastructure/Persistence/Configurations/CutOrderEntityConfiguration.cs`
- `src/StationApp.Infrastructure/Persistence/SchemaCompatibilityBootstrapper.cs`
- Migration mới trong `src/StationApp.Infrastructure/Migrations/...`

Việc cần làm:

- Thêm property `ExportPackageType`.
- Cấu hình max length 30.
- Thêm migration/schema bootstrap để DB local tự có cột mới.
- Tầng application validate bắt buộc, dù DB giai đoạn đầu để nullable cho tương thích dữ liệu cũ.

Acceptance criteria:

- [ ] App chạy trên DB cũ tự thêm được cột.
- [ ] Build không lỗi.
- [ ] Không ảnh hưởng cắt lệnh cũ chưa có giá trị `ExportPackageType`.

### 4.2 DTO request/result/list item

File dự kiến:

- `src/StationApp.Application/DTOs/Dtos.cs`
- Các DTO liên quan create/update cắt lệnh tạm.

Việc cần làm:

- Thêm `ExportPackageType` vào:
  - `CreateTemporaryExportCutOrderRequest`
  - `UpdateTemporaryExportCutOrderRequest`
  - `CreateTemporaryExportCutOrderDialogResult`
  - `ExportScaleCutOrderListItem`
- Thêm computed properties nếu cần:
  - `IsExportBagged`
  - `FinalizationWeighedWeight`
  - `FinalizationActualExportWeight`

Acceptance criteria:

- [ ] Danh sách cắt lệnh load được `ExportPackageType`.
- [ ] UI biết cắt lệnh là `Đóng bao` hay `Rời`.
- [ ] Request tạo/sửa cắt lệnh tạm không cho thiếu `ExportPackageType`.

### 4.3 Modal tạo/sửa cắt lệnh tạm

File dự kiến:

- `src/StationApp.UI/ViewModels/Dialogs/CreateTemporaryExportCutOrderDialogViewModel.cs`
- `src/StationApp.UI/Views/Dialogs/CreateTemporaryExportCutOrderDialogWindow.xaml`

Việc cần làm:

- Thêm dropdown `Loại`.
- Option:
  - `Đóng bao` -> value `BAGGED`
  - `Rời` -> value `BULK`
- Default nên là `Đóng bao` để giữ hành vi hiện tại nếu người dùng đang quen nhập TL vỏ/TL bao.
- Dù có default, `Loại` vẫn là trường bắt buộc: nếu vì lý do nào đó giá trị rỗng/null thì không cho lưu.
- Nếu chọn `Đóng bao`:
  - `TareWeightKgInput` enable.
  - `BagWeightKgInput` enable.
  - Validate bắt buộc.
  - Preview số bao hoạt động như hiện tại.
- Nếu chọn `Rời`:
  - Disable `TareWeightKgInput`, `BagWeightKgInput`.
  - Clear hoặc set 0 hai trường này.
  - Hide/clear preview số bao và cảnh báo chia lẻ.

Acceptance criteria:

- [ ] Không chọn `Loại` thì không lưu và hiển thị lỗi bắt buộc.
- [ ] Chọn `Đóng bao`, bỏ trống `TL vỏ` hoặc `TL bao` thì không lưu.
- [ ] Chọn `Rời`, không nhập `TL vỏ`/`TL bao` vẫn lưu được.
- [ ] Khi sửa cắt lệnh tạm, dropdown hiển thị đúng giá trị đã lưu.

### 4.4 Use case tạo/sửa cắt lệnh tạm

File dự kiến:

- `src/StationApp.Application/UseCases/CreateTemporaryExportCutOrderUseCase.cs`
- `src/StationApp.Application/UseCases/UpdateTemporaryExportCutOrderUseCase.cs`

Việc cần làm:

- Validate `ExportPackageType` bắt buộc, chỉ nhận `BAGGED` hoặc `BULK`.
- Với `BAGGED`:
  - `TareWeightKg >= 0`.
  - `BagWeightKg > 0`.
  - Tính `BagCount = PlannedWeight / BagWeightKg` như hiện tại.
- Với `BULK`:
  - Không bắt buộc `TareWeightKg`, `BagWeightKg`.
  - Set `BagCount = 0`.
  - Set `TareWeightKg = 0`, `BagWeightKg = 0` hoặc null theo schema hiện hữu. Khuyến nghị giữ `0` nếu đang dùng decimal không nullable trong request.
- Lưu `cutOrder.ExportPackageType`.

Acceptance criteria:

- [ ] Tạo cắt lệnh tạm `Đóng bao` có TL bao hợp lệ và sinh số bao.
- [ ] Tạo cắt lệnh tạm `Rời` không cần TL bao và không sinh số bao.
- [ ] Sửa từ `Đóng bao` sang `Rời` reset thông tin bao.
- [ ] Sửa từ `Rời` sang `Đóng bao` bắt nhập lại TL vỏ/TL bao.

### 4.5 Repository danh sách cắt lệnh xuất khẩu

File dự kiến:

- `src/StationApp.Infrastructure/Repositories/CutOrderRepository.cs`

Việc cần làm:

- Select thêm `ExportPackageType`.
- Tính thêm số lượng qua cân dùng cho chốt:

```text
Nếu BAGGED:
    FinalizationWeighedWeight = AccumulatedBagCountDisplay * BagWeightKg
Nếu BULK:
    FinalizationWeighedWeight = AccumulatedWeight
Nếu null:
    Tạm fallback theo BAGGED khi BagWeightKg > 0, ngược lại BULK
```

Fallback cho dữ liệu cũ:

- Nếu `ExportPackageType` null và `BagWeightKg > 0` -> coi như `BAGGED`.
- Nếu `ExportPackageType` null và `BagWeightKg <= 0/null` -> coi như `BULK`.

Acceptance criteria:

- [ ] Cắt lệnh cũ không crash.
- [ ] Form/grid có đủ dữ liệu để hiển thị `SL chốt tổng`.

### 4.6 Modal Chốt cắt lệnh xuất khẩu

File dự kiến:

- `src/StationApp.UI/ViewModels/ExportWeighingViewModel.cs`
- `src/StationApp.UI/ViewModels/Dialogs/FinalizeExportCutOrderDialogViewModel.cs`
- `src/StationApp.UI/Views/Dialogs/FinalizeExportCutOrderDialogWindow.xaml`

Việc cần làm:

- `FinalizeAsync()` truyền giá trị `Qua cân` theo quy cách:
  - `BAGGED`: `Lũy kế bao * TL bao`.
  - `BULK`: `AccumulatedWeight`.
- Modal nên hiển thị thêm `Loại` để người dùng biết hệ thống đang tính theo quy cách nào.
- Label/tooltip có thể ghi:
  - Đóng bao: `Qua cân = Lũy kế bao * TL bao`.
  - Rời: `Qua cân = Lũy kế tấn`.
- `Thực xuất = Qua cân + SL khác` giữ như hiện tại.

Acceptance criteria:

- [ ] Modal cắt lệnh `Đóng bao` hiển thị qua cân theo bao.
- [ ] Modal cắt lệnh `Rời` hiển thị qua cân theo lũy kế kg/tấn.
- [ ] Nhập `SL khác` cập nhật `Thực xuất` đúng.

### 4.7 Use case Chốt cắt lệnh xuất khẩu

File dự kiến:

- `src/StationApp.Application/UseCases/FinalizeExportCutOrderUseCase.cs`

Việc cần làm:

- Không tin giá trị UI; use case tự tính từ DB/trips.
- Resolve quy cách:
  - `cutOrder.ExportPackageType == BAGGED` -> theo bao.
  - `cutOrder.ExportPackageType == BULK` -> theo kg cân.
  - `null` -> fallback như mục 4.5.
- Với `BAGGED`:

```text
signedBagCount = sum(ResolveSignedBagCount(...))
weighedWeight = signedBagCount * BagWeightKg
```

- Với `BULK`:

```text
weighedWeight = sum(ResolveSignedWeight(ActualAllocatedWeight, IsReturnedBrokenTrip))
```

- `ExportFinalizedWeight = weighedWeight + ExportUnweighedWeight`.

Acceptance criteria:

- [ ] `BAGGED`: cân vật lý 50,020 kg, lũy kế 1,000 bao, TL bao 50 kg, SL khác 700 kg -> `ExportFinalizedWeight = 50,700 kg`.
- [ ] `BULK`: lũy kế cân 50,020 kg, SL khác 700 kg -> `ExportFinalizedWeight = 50,720 kg`.
- [ ] Chuyến Hoàn trừ đúng theo bao với `BAGGED`.
- [ ] Chuyến Hoàn trừ đúng theo kg với `BULK`.
- [ ] Thiếu TL bao ở `BAGGED` thì chặn chốt.

### 4.8 Hàm trả số lượng thực xuất cho ERP

File liên quan:

- `scripts/sql/fn_GetCutOrderNetWeight.sql`
- `scripts/sql/sp_GetCutOrderNetWeight.sql`

Hiện trạng:

- Với cắt lệnh xuất khẩu đã chốt, `fn_GetCutOrderNetWeight` trả `co.ExportFinalizedWeight / 1000.0`.

Kết luận:

- Nếu `FinalizeExportCutOrderUseCase` lưu đúng `ExportFinalizedWeight` theo quy cách, ERP sẽ nhận đúng.
- Không bắt buộc sửa SQL.
- Có thể bổ sung comment trong SQL hoặc test để ghi rõ `ExportFinalizedWeight` đã là số sau phân nhánh quy cách.

Acceptance criteria:

- [ ] ERP lấy cắt lệnh `BAGGED` nhận số theo `bao * TL bao + SL khác`.
- [ ] ERP lấy cắt lệnh `BULK` nhận số theo `lũy kế kg + SL khác`.

## 5. Test cần bổ sung/cập nhật

File dự kiến:

- `tests/StationApp.Application.Tests/ExportScaleUseCasesTests.cs`

Test cases:

1. `CreateTemporaryExportCutOrder_Bagged_RequiresTareAndBagWeight`
2. `CreateTemporaryExportCutOrder_Bulk_DoesNotRequireTareAndBagWeight`
3. `UpdateTemporaryExportCutOrder_ChangingToBulk_ClearsBagMetrics`
4. `FinalizeExportCutOrder_Bagged_UsesBagCountTimesBagWeight`
5. `FinalizeExportCutOrder_Bulk_UsesAccumulatedWeight`
6. `FinalizeExportCutOrder_Bagged_SubtractsReturnedBrokenBags`
7. `FinalizeExportCutOrder_Bulk_SubtractsReturnedBrokenWeight`

## 6. Rủi ro và lưu ý

| Rủi ro | Mức độ | Cách xử lý |
|---|---:|---|
| Dữ liệu cũ chưa có `ExportPackageType` | Cao | Fallback: có `BagWeightKg > 0` thì coi là `BAGGED`, ngược lại `BULK` |
| Nhầm `ProductType` với quy cách xuất khẩu | Cao | Tạo field DB riêng `ExportPackageType`, không dùng `ProductType` |
| Cắt lệnh Đóng bao thiếu TL bao | Cao | Chặn chốt tổng với lỗi rõ ràng |
| Grid/form đang dùng `AccumulatedWeight` để đối chiếu cân vật lý | Trung bình | Giữ nguyên nghĩa `AccumulatedWeight`; thêm property riêng cho số chốt tổng |
| Chuyến Hoàn bị trừ sai đơn vị | Cao | Test riêng cho `BAGGED` và `BULK` |

## 7. Checklist nghiệm thu

- [ ] DB có trường `ExportPackageType`.
- [ ] Modal tạo/sửa cắt lệnh tạm có dropdown `Loại`.
- [ ] `Đóng bao` bắt buộc `TL vỏ`, `TL bao`.
- [ ] `Rời` disable và không bắt buộc `TL vỏ`, `TL bao`.
- [ ] Modal Chốt tổng hiển thị đúng `Qua cân` theo quy cách.
- [ ] `ExportFinalizedWeight` lưu đúng theo quy cách.
- [ ] ERP lấy thực xuất đúng qua `sp_GetCutOrderNetWeight`.
- [ ] Unit tests pass.
- [ ] Build `StationApp.UI` pass.

## 8. Câu hỏi cần chốt

1. Tên field DB dùng `ExportPackageType` ổn không, hay bạn muốn tên Việt hóa hơn như `ExportPackingType` / `PackagingType`?
   - **Đã chốt:** dùng `ExportPackageType`.
2. Với dữ liệu cũ chưa có Loại, fallback `BagWeightKg > 0 => Đóng bao`, còn lại `Rời` có ổn không?
   - **Đã chốt:** OK.
3. Với Loại `Rời`, khi lưu DB nên set `TL vỏ`, `TL bao` là `0` thay vì `null` để ít ảnh hưởng code hiện tại, có ok không?
   - **Đã chốt:** OK, lưu `TareWeightKg = 0`, `BagWeightKg = 0`.

## 9. Quyết định đã chốt

- Field DB: `cut_orders.ExportPackageType`.
- Giá trị DB:
  - `BAGGED` = `Đóng bao`.
  - `BULK` = `Rời`.
- `Loại` là trường bắt buộc khi tạo/sửa cắt lệnh tạm.
- Dữ liệu cũ fallback:
  - `ExportPackageType == null` và `BagWeightKg > 0` => coi là `BAGGED`.
  - `ExportPackageType == null` và `BagWeightKg <= 0/null` => coi là `BULK`.
- Với `BULK`, lưu `TareWeightKg = 0`, `BagWeightKg = 0`, `BagCount = 0`.
