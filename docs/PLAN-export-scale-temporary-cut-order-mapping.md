# Plan: Nâng cấp luồng cân xuất khẩu với cắt lệnh tạm và map sang cắt lệnh ERP

## 1. Mục tiêu

Luồng hiện tại yêu cầu có cắt lệnh ERP thật trước rồi mới bấm `Cân xuất khẩu` và tạo chuyến xe. Yêu cầu mới cần hỗ trợ thực tế vận hành:

- Trạm cân có thể tạo trước một cắt lệnh xuất khẩu tạm trên màn `Cân xuất khẩu`.
- Nhân viên tạo nhiều chuyến xe và cân lần 1/cân lần 2 vào cắt lệnh tạm đó.
- Sau khi ERP chốt lô, ERP tạo đăng ký phương tiện/cắt lệnh thật và truyền xuống cân.
- Tại màn `Danh sách xe vào`, khi chọn cắt lệnh thật và bấm `Cân xuất khẩu`, app hiển thị modal để map cắt lệnh thật với cắt lệnh tạm.
- Sau khi map, toàn bộ chuyến xe, line, phiếu cân, phiếu giao nhận sẽ gắn sang cắt lệnh thật để chốt tổng và ERP lấy số lượng thực xuất.
- Khi ERP `RA` cắt lệnh thật rồi `CO` lại, các chuyến xe đã cân không bị mất. App phải chuyển các chuyến xe đó về một cắt lệnh xuất khẩu tạm để sau đó map lại với cắt lệnh thật mới.

Ràng buộc tương thích bắt buộc:

- Luồng cân xuất khẩu hiện tại vẫn phải chạy đúng khi người dùng có cắt lệnh ERP thật trước rồi mới bấm `Cân xuất khẩu`.
- Nếu không chọn map với cắt lệnh tạm, hành vi `TransitionToExportScaleUseCase`, tạo chuyến, cân lần 1/lần 2, in phiếu và chốt tổng phải giữ như hiện tại.
- Khi ERP `RA` một cắt lệnh xuất khẩu đã có chuyến xe, hệ thống phải tạo mới hoặc tìm một cắt lệnh xuất khẩu tạm để gắn các chuyến xe đó vào.
- Các chuyến xe đã được chuyển về cắt lệnh tạm sau RA vẫn phải hiển thị ở màn `Cân xuất khẩu`.
- Các chuyến xe export-scale sau RA không được hiển thị ở màn `Cân nội địa`.

## 2. Hiện trạng code liên quan

### 2.1 Quan hệ dữ liệu hiện tại

Quan hệ đúng giữa cắt lệnh xuất khẩu và chuyến xe là:

```text
cut_orders.Id
  -> weighing_session_lines.CutOrderId
  -> weighing_session_lines.WeighingSessionId
  -> weighing_sessions.Id
```

`cut_orders.WeighingSessionId` chỉ là cột phụ trợ trong một số luồng cũ, không phải quan hệ chính cho luồng xuất khẩu nhiều chuyến.

### 2.2 Use case hiện tại

- `TransitionToExportScaleUseCase`: chuyển một `cut_orders` thật sang luồng xuất khẩu bằng cách set:
  - `IsExportScale = true`
  - `CutOrderStatus = IN_SESSION`
  - `ProcessingStage = WEIGHING`
  - `ExportStartedAt/ExportStartedBy`
- `CreateExportVehicleSessionUseCase`: tạo `weighing_sessions` và 1 `weighing_session_lines` trỏ tới `CutOrderId` của cắt lệnh xuất khẩu.
- `TransferExportVehicleTripUseCase`: đã có logic chuyển 1 chuyến xe từ cắt lệnh xuất khẩu này sang cắt lệnh xuất khẩu khác bằng cách cập nhật:
  - `weighing_session_lines.CutOrderId`
  - `weigh_tickets.CutOrderId`, `ErpCutOrderId`, customer/product/planned fields
  - `delivery_tickets.CutOrderId`, `ErpCutOrderId`, customer/product/notes
- `FinalizeExportCutOrderUseCase`: lấy các trip theo `GetExportVehicleTripsAsync(cutOrder.Id)`, cộng `ActualAllocatedWeight`, rồi set `ExportFinalizedWeight`, `ExportFinalizedAt`, `CutOrderStatus = COMPLETED`, `ProcessingStage = OUT_YARD`.

### 2.3 Repository/UI hiện tại

- `CutOrderRepository.GetActiveExportScaleCutOrdersAsync()`: chỉ lấy `cut_orders` có `IsExportScale = true`, `TransactionType = OUTBOUND`, `ProcessingStage = WEIGHING` hoặc đã chốt.
- `CutOrderRepository.GetExportVehicleTripsAsync(cutOrderId)`: lấy trip qua `weighing_session_lines.CutOrderId`.
- `IncomingVehicleListViewModel.TransitionToExportScaleAsync()`: hiện bấm `Cân xuất khẩu` là gọi thẳng `TransitionToExportScaleUseCase`, không có bước map.
- `ExportWeighingViewModel.CreateTripAsync()`: chỉ tạo chuyến khi đã chọn một cắt lệnh xuất khẩu có sẵn.

### 2.4 ERP RA/CO hiện tại

`sp_SoftDeleteCutOrderDocumentsForReissue.sql` đang xử lý RA bằng cách:

- tìm cắt lệnh theo `ErpCutOrderId`
- nếu có session/line liên quan thì soft-delete line, ticket, delivery, hoặc session tùy trạng thái
- không có cơ chế chuyển các chuyến xe xuất khẩu đã cân sang cắt lệnh tạm

Điểm này chưa phù hợp với yêu cầu mới, vì RA cắt lệnh thật có thể làm mất liên kết lịch sử cần map lại.

## 3. Thiết kế dữ liệu đề xuất

### 3.1 Mở rộng `cut_orders`

Thêm các cột:

```sql
IsTemporaryExport bit NOT NULL DEFAULT 0
MappedRealCutOrderId uniqueidentifier NULL
MappedTemporaryCutOrderId uniqueidentifier NULL
TemporaryExportCreatedReason nvarchar(50) NULL
TemporaryExportDisplayCode nvarchar(100) NULL
TemporaryExportSourceErpCutOrderId nvarchar(100) NULL
MappedAt datetime2 NULL
MappedBy nvarchar(100) NULL
```

Ý nghĩa:

- `IsTemporaryExport`: đánh dấu cắt lệnh tạm.
- `MappedRealCutOrderId`: trên cắt lệnh tạm, trỏ tới cắt lệnh thật đã map gần nhất.
- `MappedTemporaryCutOrderId`: trên cắt lệnh thật, trỏ ngược lại cắt lệnh tạm nguồn.
- `TemporaryExportCreatedReason`: ví dụ `MANUAL_PRELOAD`, `ERP_REISSUE_HOLDING`.
- `TemporaryExportDisplayCode`: mã hiển thị cho UI, ví dụ `CL-TAM-0001`.
- `TemporaryExportSourceErpCutOrderId`: chỉ dùng cho temp holding sinh ra khi ERP RA cắt lệnh thật; lưu mã cắt lệnh ERP cũ để gợi ý map lại khi ERP CO cắt lệnh mới.
- `MappedAt/MappedBy`: audit thao tác map.

Không dùng `ErpCutOrderId` giả để tránh đụng unique active ERP key. Với cắt lệnh tạm, `ErpCutOrderId` nên để `NULL`, hiển thị bằng `TemporaryExportDisplayCode`.

`TemporaryExportGroupId` không bắt buộc trong phase này và có thể bỏ. Lý do:

- Với temp tạo thủ công trước khi ERP có lệnh thật, app không có khóa ERP nào đủ tin cậy để sinh group đúng.
- Với RA/CO, chỉ cần biết temp holding đang giữ chuyến của `ErpCutOrderId` nào. Trường `TemporaryExportSourceErpCutOrderId` đủ rõ hơn `TemporaryExportGroupId`.
- Khi map xong, quan hệ audit đã có bằng `MappedRealCutOrderId` và `MappedTemporaryCutOrderId`.
- Nếu sau này cần hỗ trợ 1 lô tạm split sang nhiều cắt lệnh thật, lúc đó mới cân nhắc thêm group/batch entity riêng thay vì nhét GUID group vào `cut_orders`.

### 3.2 Index cần thêm

```sql
CREATE INDEX IX_cut_orders_temp_export
ON cut_orders(IsTemporaryExport, IsExportScale, ProcessingStage, IsDeleted);

CREATE INDEX IX_cut_orders_mapped_real
ON cut_orders(MappedRealCutOrderId, IsDeleted);

CREATE INDEX IX_cut_orders_temp_source_erp
ON cut_orders(TemporaryExportSourceErpCutOrderId, IsDeleted);
```

### 3.3 Central DB và sync

Các cột mới phải được thêm cả local và central:

- `SchemaCompatibilityBootstrapper`
- `CutOrderEntityConfiguration`
- `StationApp.CentralApi` bootstrap columns
- `SyncPayloadFactory` không cần đổi nếu serialize full entity, nhưng phải đảm bảo entity có property mới.

## 4. Luồng nghiệp vụ mới

### 4.1 Tạo cắt lệnh tạm trên màn `Cân xuất khẩu`

Thêm nút `Tạo cắt lệnh tạm`.

Input tối thiểu:

- mã tạm hoặc app tự sinh
- khách hàng, sản phẩm, loại hàng nếu biết
- KL kế hoạch tạm nếu biết
- ghi chú

Khi lưu:

- tạo `cut_orders` mới:
  - `IsTemporaryExport = true`
  - `IsExportScale = true`
  - `TransactionType = OUTBOUND`
  - `CutOrderStatus = IN_SESSION`
  - `ProcessingStage = WEIGHING`
  - `CutOrderSource = MANUAL`
  - `ErpCutOrderId = NULL`
  - `TemporaryExportCreatedReason = MANUAL_PRELOAD`
  - `TemporaryExportDisplayCode = CL-TAM-0001`, tăng tuần tự theo số lớn nhất đang có
  - `TemporaryExportSourceErpCutOrderId = NULL`
  - `SyncStatus = SYNC_QUEUED`

Sau đó màn `Cân xuất khẩu` hiển thị cắt lệnh tạm trong danh sách giống cắt lệnh thật, có badge `TẠM`.

### 4.2 Tạo chuyến xe vào cắt lệnh tạm

Tái sử dụng `CreateExportVehicleSessionUseCase`.

Cần nới `ValidateOpenExportCutOrder()` để chấp nhận:

- `IsExportScale = true`
- `TransactionType = OUTBOUND`
- `ProcessingStage = WEIGHING`
- `CutOrderStatus = IN_SESSION`
- không phân biệt tạm/thật

`weighing_session_lines.CutOrderId` ban đầu sẽ trỏ tới `cut_orders.Id` của cắt lệnh tạm.

### 4.3 ERP truyền cắt lệnh thật xuống

Khi ERP truyền cắt lệnh thật, nó xuất hiện ở `Danh sách xe vào` như hiện tại:

- `IsExportScale = false`
- `TransactionType = OUTBOUND`
- `CutOrderStatus = REGISTERED`
- `ProcessingStage = IN_YARD`

Người dùng chọn cắt lệnh thật và bấm `Cân xuất khẩu`.

### 4.4 Modal map cắt lệnh thật với cắt lệnh tạm

Thay đổi `IncomingVehicleListViewModel.TransitionToExportScaleAsync()`:

- Nếu không có cắt lệnh tạm phù hợp: cho phép chuyển như hiện tại.
- Nếu có cắt lệnh tạm đang hoạt động: hiển thị modal chọn:
  - `Map với cắt lệnh tạm`
  - `Không map, chuyển cắt lệnh thật sang cân xuất khẩu như cũ`

Modal hiển thị danh sách cắt lệnh tạm:

- mã tạm
- khách hàng/sản phẩm tạm
- số chuyến
- tổng thực cân
- chuyến cuối
- ghi chú

Định nghĩa "cắt lệnh tạm đang hoạt động":

- `IsTemporaryExport = true`
- `IsExportScale = true`
- `ProcessingStage = WEIGHING`
- `CutOrderStatus = IN_SESSION`
- `IsDeleted = false`
- `IsCancelled = false`
- `ExportFinalizedAt IS NULL`
- có ít nhất 1 `weighing_session_lines` active trỏ tới temp, hoặc temp vừa tạo để chuẩn bị cân
- chưa map sang cắt lệnh thật active khác

Query gợi ý:

```sql
SELECT co.*
FROM dbo.cut_orders co
WHERE co.IsTemporaryExport = 1
  AND co.IsExportScale = 1
  AND co.TransactionType = N'OUTBOUND'
  AND co.ProcessingStage = N'WEIGHING'
  AND co.CutOrderStatus = N'IN_SESSION'
  AND ISNULL(co.IsDeleted, 0) = 0
  AND ISNULL(co.IsCancelled, 0) = 0
  AND co.ExportFinalizedAt IS NULL
  AND NOT EXISTS (
      SELECT 1
      FROM dbo.cut_orders active_real
      WHERE active_real.Id = co.MappedRealCutOrderId
        AND ISNULL(active_real.IsDeleted, 0) = 0
        AND ISNULL(active_real.IsCancelled, 0) = 0
        AND ISNULL(active_real.IsTemporaryExport, 0) = 0
  );
```

Khi người dùng đang chọn một cắt lệnh thật từ ERP, modal ưu tiên sắp xếp temp theo:

- `TemporaryExportSourceErpCutOrderId = realCutOrder.ErpCutOrderId`, dùng cho case ERP RA/CO.
- temp có cùng `CustomerCode/ProductCode`.
- temp có chuyến cân gần nhất.
- temp tạo mới chưa có chuyến.

### 4.5 Map cắt lệnh tạm sang cắt lệnh thật

Tạo use case mới: `MapTemporaryExportCutOrderUseCase`.

Input:

```csharp
public sealed record MapTemporaryExportCutOrderRequest(
    Guid TemporaryCutOrderId,
    Guid RealCutOrderId);
```

Luồng xử lý trong transaction:

1. Load `temporaryCutOrder`, `realCutOrder`.
2. Validate `temporaryCutOrder`:
   - `IsTemporaryExport = true`
   - `IsExportScale = true`
   - chưa chốt tổng
   - không bị xóa/hủy
3. Validate `realCutOrder`:
   - `TransactionType = OUTBOUND`
   - đang ở `REGISTERED/IN_YARD`
   - chưa có session nội địa
   - không bị xóa/hủy
4. Chuyển `realCutOrder` sang export:
   - `IsExportScale = true`
   - `CutOrderStatus = IN_SESSION`
   - `ProcessingStage = WEIGHING`
   - `ExportStartedAt/By`
   - `MappedTemporaryCutOrderId = temporaryCutOrder.Id`
5. Repoint toàn bộ dữ liệu từ tạm sang thật:
   - `weighing_session_lines.CutOrderId = realCutOrder.Id`
   - `weigh_tickets.CutOrderId = realCutOrder.Id`
   - `weigh_tickets.ErpCutOrderId = realCutOrder.ErpCutOrderId`
   - `delivery_tickets.CutOrderId = realCutOrder.Id`
   - `delivery_tickets.ErpCutOrderId = realCutOrder.ErpCutOrderId`
   - refresh customer/product/planned fields theo real cut order
6. Đánh dấu cắt lệnh tạm:
   - `MappedRealCutOrderId = realCutOrder.Id`
   - `MappedAt/MappedBy`
   - `ProcessingStage = OUT_YARD` hoặc trạng thái riêng nếu thêm enum
   - không hard delete
7. Set `SyncStatus = SYNC_QUEUED` cho:
   - temp cut order
   - real cut order
   - sessions liên quan
   - lines liên quan
   - weigh tickets
   - delivery tickets

Có thể tái sử dụng phần lớn logic cập nhật line/ticket/delivery từ `TransferExportVehicleTripUseCase`, nhưng nên tách ra helper/service chung để tránh copy-paste.

### 4.6 Chốt tổng sau khi map

`FinalizeExportCutOrderUseCase` vẫn chạy trên cắt lệnh thật.

Vì `GetExportVehicleTripsAsync(realCutOrder.Id)` dựa vào `weighing_session_lines.CutOrderId`, sau map nó sẽ lấy đúng toàn bộ chuyến xe đã cân trước đó.

`sp_GetCutOrderNetWeight` và `fn_GetCutOrderNetWeight` cần rà lại:

- với cắt lệnh thật đã map: trả `ExportFinalizedWeight` hoặc tổng line đã map.
- với cắt lệnh tạm: không phục vụ ERP, có thể trả 0 hoặc chặn nếu `ErpCutOrderId IS NULL`.

## 5. Xử lý RA/CO từ ERP

### 5.1 Vấn đề hiện tại

ERP `RA` gọi `sp_SoftDeleteCutOrderDocumentsForReissue`.

Với cắt lệnh xuất khẩu đã có chuyến:

- không được xóa line/session như luồng nội địa thông thường.
- cần giữ các chuyến xe ở một cắt lệnh tạm để sau khi ERP `CO` lại cắt lệnh thật mới, người dùng map lại.

### 5.2 Hành vi mới khi RA cắt lệnh thật export

Trong `sp_SoftDeleteCutOrderDocumentsForReissue`, nếu `cut_orders.IsExportScale = 1`:

1. Tìm hoặc tạo cắt lệnh tạm giữ chỗ:
   - ưu tiên tìm temp đang active có `TemporaryExportSourceErpCutOrderId = @ErpCutOrderId`
   - nếu chưa có thì tạo mới `cut_orders` tạm với `TemporaryExportCreatedReason = ERP_REISSUE_HOLDING`
   - set `TemporaryExportSourceErpCutOrderId = @ErpCutOrderId`
   - temp holding phải có `IsTemporaryExport = 1`, `IsExportScale = 1`, `TransactionType = OUTBOUND`, `CutOrderStatus = IN_SESSION`, `ProcessingStage = WEIGHING`
2. Repoint toàn bộ chuyến xe từ cắt lệnh thật sang cắt lệnh tạm:
   - `weighing_session_lines.CutOrderId = tempCutOrder.Id`
   - `weigh_tickets.CutOrderId = tempCutOrder.Id`
   - `delivery_tickets.CutOrderId = tempCutOrder.Id`
3. Không xóa `weighing_sessions` và không cancel session.
4. Soft-delete cắt lệnh thật theo cơ chế hiện tại.
5. Giữ `TemporaryExportSourceErpCutOrderId` để CO lại có thể gợi ý đúng temp holding.

Sau RA, các chuyến xe phải vẫn thuộc luồng export-scale vì line đã trỏ sang temp holding có `IsExportScale = 1`. Điều kiện load màn `Cân nội địa` phải tiếp tục loại mọi session có line gắn với `cut_orders.IsExportScale = 1`, kể cả khi cắt lệnh thật ban đầu đã bị soft-delete.

### 5.3 Khi CO lại cắt lệnh thật

Khi ERP tạo lại cắt lệnh thật:

- bản ghi mới vào `Danh sách xe vào`
- người dùng bấm `Cân xuất khẩu`
- modal gợi ý cắt lệnh tạm theo 2 nhóm rõ ràng:
  - Temp holding sinh ra do ERP RA: ưu tiên `TemporaryExportSourceErpCutOrderId = realCutOrder.ErpCutOrderId`.
  - Temp tạo thủ công trước khi ERP CO cắt lệnh thật: không thể so sánh bằng `ErpCutOrderId`; phải gợi ý theo thông tin nghiệp vụ.
- sau khi map, các chuyến xe quay lại cắt lệnh thật mới.

Với temp tạo thủ công trước ERP, tiêu chí gợi ý/sắp xếp là:

- `CustomerCode` trùng `realCutOrder.CustomerCode`.
- `ProductCode` trùng `realCutOrder.ProductCode`.
- nếu thiếu mã thì fallback so sánh `CustomerName` và `ProductName` sau khi trim/normalize.
- `Notes` hoặc `TemporaryExportDisplayCode` có chứa từ khóa người dùng nhập khi tìm.
- temp có chuyến cân gần nhất được ưu tiên hơn temp trống.

Điểm số gợi ý đề xuất:

```text
+50 nếu CustomerCode trùng
+30 nếu ProductCode trùng
+15 nếu CustomerName trùng khi thiếu CustomerCode
+15 nếu ProductName trùng khi thiếu ProductCode
+10 nếu Notes/TemporaryExportDisplayCode match keyword
+5 nếu temp đã có chuyến cân
```

Modal vẫn phải cho người dùng chọn thủ công, vì với temp tạo trước ERP không có khóa định danh tuyệt đối để tự map 100%.

## 6. Thay đổi code theo module

### 6.1 Domain

Sửa `CutOrder`:

- thêm các property mới ở mục 3.1.

Nếu muốn rõ nghĩa hơn, có thể thêm enum:

```csharp
public enum TemporaryExportCreatedReason
{
    MANUAL_PRELOAD,
    ERP_REISSUE_HOLDING
}
```

Để giảm thay đổi schema phức tạp ban đầu, phase 1 có thể dùng `nvarchar(50)`.

### 6.2 Infrastructure

Sửa:

- `CutOrderEntityConfiguration`
- `SchemaCompatibilityBootstrapper`
- `StationApp.CentralApi` bootstrap schema
- `CutOrderRepository`

Thêm repository methods:

```csharp
Task<CutOrder> CreateTemporaryExportCutOrderAsync(...);
Task<IReadOnlyList<ExportScaleCutOrderListItem>> GetTemporaryExportCutOrdersAsync(...);
Task<IReadOnlyList<ExportVehicleTripListItem>> GetExportVehicleTripsAsync(Guid cutOrderId, ...);
Task<IReadOnlyList<WeighingSessionLine>> GetActiveLinesByCutOrderIdAsync(Guid cutOrderId, ...);
```

Sửa `GetActiveExportScaleCutOrdersAsync()`:

- vẫn hiển thị cắt lệnh thật đang cân.
- hiển thị thêm cắt lệnh tạm đang active.
- DTO cần có `IsTemporaryExport`, `TemporaryExportDisplayCode`, `MappedRealCutOrderId`.

### 6.3 Application

Thêm use cases:

- `CreateTemporaryExportCutOrderUseCase`
- `GetTemporaryExportCutOrdersForMappingUseCase`
- `MapTemporaryExportCutOrderUseCase`

Refactor:

- tách logic “move export trip data to target cut order” từ `TransferExportVehicleTripUseCase` sang service chung, ví dụ `ExportTripRelinkService`.
- `TransitionToExportScaleUseCase` thêm mode:
  - transition thường
  - transition + map temporary

DTOs cần thêm:

```csharp
public sealed record CreateTemporaryExportCutOrderRequest(...);
public sealed record MapTemporaryExportCutOrderRequest(Guid TemporaryCutOrderId, Guid RealCutOrderId);
public sealed record TemporaryExportCutOrderOption(...);
```

### 6.4 UI màn `Cân xuất khẩu`

Thêm:

- nút `Tạo cắt lệnh tạm`
- modal tạo cắt lệnh tạm
- badge `TẠM` trên danh sách cắt lệnh
- khóa nút `Chốt tổng` nếu đang chọn cắt lệnh tạm chưa map sang thật

Lý do khóa chốt tổng trên cắt lệnh tạm:

- ERP chỉ lấy số lượng theo cắt lệnh thật.
- Nếu cho chốt tạm, sẽ sinh số liệu không có `ErpCutOrderId` để ERP đối chiếu.

### 6.5 UI màn `Danh sách xe vào`

Sửa `TransitionToExportScaleAsync()`:

- load danh sách cắt lệnh tạm khả dụng.
- nếu có temp: mở modal map.
- nếu user chọn temp: gọi `MapTemporaryExportCutOrderUseCase`.
- nếu user bỏ qua temp: gọi `TransitionToExportScaleUseCase` như hiện tại.
- sau map: điều hướng sang `ExportWeighingView` với `RealCutOrderId`.

### 6.6 Stored procedures

Sửa:

- `sp_SoftDeleteCutOrderDocumentsForReissue.sql`
- `sp_GetCutOrderNetWeight.sql`
- `fn_GetCutOrderNetWeight.sql`

Thêm mới nếu cần:

- `sp_MapTemporaryExportCutOrder.sql` chỉ dùng cho admin/ERP support, còn app nên dùng use case C#.
- `sp_CreateTemporaryExportHoldingCutOrderForReissue.sql` nếu muốn tách logic RA trong SQL cho dễ test.

## 7. Quy tắc dữ liệu bắt buộc

- Chuyến xe xuất khẩu luôn thuộc cắt lệnh qua `weighing_session_lines.CutOrderId`.
- Không dùng `cut_orders.WeighingSessionId` để xác định chuyến xe của cắt lệnh xuất khẩu.
- Cắt lệnh tạm không được chốt tổng gửi ERP.
- Sau map, toàn bộ line/ticket/delivery phải trỏ sang cắt lệnh thật.
- Sau RA cắt lệnh thật, không xóa session/line export đã cân; phải chuyển sang temp holding.
- Cắt lệnh thật CO lại phải map được vào temp holding cũ.

## 8. Test plan

### 8.1 Tạo tạm và cân trước ERP

1. Mở `Cân xuất khẩu`.
2. Tạo cắt lệnh tạm.
3. Tạo 2 chuyến xe vào cắt lệnh tạm.
4. Cân lần 1/lần 2.
5. Kiểm tra:
   - `weighing_sessions` có 2 session outbound.
   - `weighing_session_lines.CutOrderId` trỏ tới temp cut order.
   - temp hiển thị đúng tổng chuyến/tổng KL.

### 8.2 ERP truyền cắt lệnh thật và map

1. ERP truyền cắt lệnh thật xuống.
2. Mở `Danh sách xe vào`.
3. Chọn cắt lệnh thật, bấm `Cân xuất khẩu`.
4. Chọn temp trong modal map.
5. Kiểm tra:
   - tất cả `weighing_session_lines.CutOrderId` chuyển sang real cut order.
   - `weigh_tickets.CutOrderId/ErpCutOrderId` chuyển sang real.
   - `delivery_tickets.CutOrderId/ErpCutOrderId` chuyển sang real.
   - real cut order hiển thị trong `Cân xuất khẩu`.
   - temp không còn là cắt lệnh active để chốt.

### 8.3 Chốt tổng sau map

1. Chọn cắt lệnh thật đã map.
2. Bấm chốt tổng.
3. Kiểm tra:
   - `ExportFinalizedWeight` bằng tổng `ActualAllocatedWeight`.
   - ERP gọi `sp_GetCutOrderNetWeight` trả đúng thực xuất.
   - sync lên central có đủ cut order/session/line/ticket/delivery.

### 8.4 RA/CO cắt lệnh thật

1. Với cắt lệnh thật đã map và có chuyến, ERP gọi RA.
2. Kiểm tra:
   - real cut order bị soft-delete.
   - session không bị cancel/delete.
   - line/ticket/delivery được chuyển về temp holding.
3. ERP CO lại cắt lệnh thật mới.
4. User map lại.
5. Kiểm tra:
   - chuyến xe quay về cắt lệnh thật mới.
   - chốt tổng và ERP lấy số lượng đúng.

### 8.5 Case chặn

- Không cho map temp đã chốt.
- Không cho map temp không có chuyến nếu user không xác nhận.
- Không cho map real cut order đã thuộc session nội địa.
- Không cho chốt tổng trên temp.
- Không cho một temp map đồng thời vào nhiều real active cut order.

## 9. Thứ tự triển khai đề xuất

### Phase 1: Schema và query

- Thêm cột temp/mapping vào `cut_orders`.
- Cập nhật entity/config/bootstrap/central.
- Cập nhật DTO `ExportScaleCutOrderListItem`.
- Cập nhật `GetActiveExportScaleCutOrdersAsync()` để hiển thị temp.

### Phase 2: Tạo cắt lệnh tạm

- Thêm use case tạo temp.
- Thêm UI nút/modal tạo temp ở màn `Cân xuất khẩu`.
- Cho phép tạo chuyến vào temp.

### Phase 3: Map temp sang real

- Thêm modal map ở `Danh sách xe vào`.
- Thêm `MapTemporaryExportCutOrderUseCase`.
- Refactor logic relink line/ticket/delivery.
- Chặn chốt tổng trên temp.

### Phase 4: RA/CO safe holding

- Sửa `sp_SoftDeleteCutOrderDocumentsForReissue`.
- Đảm bảo RA real export chuyển chuyến về temp holding.
- Đảm bảo CO lại có thể map.

### Phase 5: Báo cáo/sync/ERP hardening

- Rà báo cáo xuất theo real cut order sau map.
- Rà dashboard/KPI không đếm temp sai.
- Rà central API schema.
- Rà outbox retry sau map.

## 10. Rủi ro và quyết định cần chốt

- Có cho phép chốt tổng trên cắt lệnh tạm không: đề xuất không cho.
- Khi map temp sang real, planned weight/bag count của line nên lấy theo real cut order: đề xuất có, để báo cáo/phiếu đúng ERP.
- Temp sau map có soft-delete không: đề xuất không xóa, chỉ đánh dấu đã map để audit và xử lý RA/CO.
- ERP RA cắt lệnh đã chốt tổng: cần quyết định nghiệp vụ có cho phép không. Nếu có, phải chuyển cả finalized weight về temp holding và khi CO lại thì chốt lại trên real mới.
- Nếu một lô tạm sau đó ERP tách thành nhiều cắt lệnh thật: phase đầu chưa hỗ trợ split temp sang nhiều real; chỉ hỗ trợ 1 temp -> 1 real. Nếu cần split, mở rộng bằng cách chọn từng chuyến khi map.
