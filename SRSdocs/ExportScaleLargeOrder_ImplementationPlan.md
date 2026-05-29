# Kế hoạch triển khai: Cân xuất khẩu đơn to qua nhiều chuyến xe

## 1. Mục tiêu

Tài liệu này chuyển hóa plan tại:

`C:\Users\Chienbn\.gemini\antigravity-ide\brain\3e0da733-2443-4ea0-a72e-d72a764b1380\implementation_plan.md`

thành kế hoạch triển khai chi tiết, bám sát cấu trúc hiện tại của dự án `StationApp`.

Mục tiêu nghiệp vụ:

- Một `cut_order` xuất khẩu có khối lượng lớn có thể đi qua nhiều chuyến xe.
- Mỗi chuyến xe có một `weighing_session` riêng.
- Mỗi chuyến xe lưu được cân lần 1, cân lần 2, net weight, ảnh camera và chứng từ riêng.
- `cut_order` cha lưu trạng thái tổng và khối lượng đã chốt cuối cùng.
- Màn `Danh sách xe ra` phải hiển thị từng xe con sau khi xe đó cân xong lần 2, kể cả khi `cut_order` cha chưa chốt.
- Khi operator bấm `Chốt số lượng`, hệ thống tính tổng khối lượng đã xuất và đưa `cut_order` vào hàng đợi sync lên server.

## 2. Quyết định thiết kế cần review

### 2.1 Mô hình chuyến xe

Phase đầu không tạo bảng `cut_order_trips`.

Lý do:

- `weighing_sessions` hiện đã có `VehiclePlate`, `MoocNumber`, `DriverName`, `Weight1`, `Weight2`, `NetWeight`, `SessionStatus`.
- `weighing_session_lines` hiện đã liên kết `WeighingSessionId` với `CutOrderId`.
- Một `weighing_session` có thể được xem là một chuyến xe con của `cut_order` xuất khẩu.

Quy tắc:

- `cut_orders` là đơn/cắt lệnh cha.
- `weighing_sessions` là từng chuyến xe con.
- `weighing_session_lines` là liên kết lịch sử chuẩn giữa chuyến xe và cắt lệnh.
- `cut_orders.WeighingSessionId` không được dùng làm lịch sử cho luồng xuất khẩu đơn to.

### 2.2 Màn hình riêng

Plan nguồn đề xuất tạo màn `ExportWeighingView`.

Chấp nhận về mặt UI, nhưng cần giới hạn rõ:

- Màn mới chỉ là workflow riêng cho operator để quản lý đơn to.
- Logic cân, camera, in ấn, phân bổ, tạo phiếu phải tái sử dụng service/use case hiện có nhiều nhất có thể.
- Không copy/paste toàn bộ `WeighingViewModel` thành một nhánh độc lập lớn.

### 2.3 Camera riêng cho xuất khẩu

Plan nguồn đề xuất thêm cấu hình `export_camera_*`.

Chấp nhận, nhưng nên là phase riêng:

- Phase 1 có thể fallback dùng camera mặc định hiện tại.
- Phase 2 mới thêm UI cấu hình camera xuất khẩu riêng nếu thực tế cần.

## 3. Mô hình dữ liệu

### 3.1 Cập nhật `CutOrder`

Thêm các field vào entity `CutOrder` và bảng `cut_orders`:

- `IsExportScale bit not null default 0`
  - Đánh dấu cut order thuộc luồng cân xuất khẩu đơn to.
- `ExportFinalizedWeight decimal(18,3) null`
  - Tổng khối lượng đã chốt khi operator bấm chốt.
- `ExportFinalizedAt datetime2 null`
  - Thời điểm chốt.
- `ExportFinalizedBy nvarchar(100) null`
  - User chốt.

Khuyến nghị thêm field audit tùy chọn:

- `ExportStartedAt datetime2 null`
- `ExportStartedBy nvarchar(100) null`

Nếu không thêm hai field này trong phase đầu, có thể dùng `UpdatedAt/UpdatedBy`.

### 3.2 EF Core mapping

Sửa `CutOrderEntityConfiguration.cs`:

```csharp
builder.Property(e => e.IsExportScale)
    .IsRequired()
    .HasDefaultValue(false);

builder.Property(e => e.ExportFinalizedWeight)
    .HasColumnType("decimal(18,3)");

builder.Property(e => e.ExportFinalizedBy)
    .HasMaxLength(100);
```

### 3.3 Bootstrap DB tương thích máy trạm

Thêm patch cột vào `SchemaCompatibilityBootstrapper.cs`.

Yêu cầu:

- DDL phải idempotent.
- Không dùng cú pháp SQL Server mới như `CREATE OR ALTER` trong compatibility bootstrap.
- Dùng `IF COL_LENGTH(...) IS NULL ALTER TABLE ... ADD ...`.

### 3.4 Index khuyến nghị

Thêm index để truy vấn nhanh danh sách đơn xuất khẩu và lịch sử chuyến xe:

- `IX_cut_orders_is_export_scale_status`
  - columns: `IsExportScale`, `CutOrderStatus`, `ProcessingStage`, `IsDeleted`
- `IX_weighing_session_lines_cut_order_id`
  - hiện đã có `IX_weighing_session_lines_registration_id` trên `CutOrderId`; cần kiểm tra và tái sử dụng.
- `IX_weighing_sessions_status_updated`
  - columns: `SessionStatus`, `UpdatedAt`

## 4. Trạng thái nghiệp vụ

### 4.1 Trạng thái của `cut_order` xuất khẩu

Khi ERP đẩy xuống:

- `IsExportScale = false`
- `CutOrderStatus = REGISTERED`
- `ProcessingStage = IN_YARD`

Khi operator bấm `Cân xuất khẩu`:

- `IsExportScale = true`
- `CutOrderStatus = IN_SESSION`
- `ProcessingStage = WEIGHING`
- `WeighingSessionId = null`

Trong khi đang chạy nhiều xe:

- `CutOrderStatus = IN_SESSION`
- `ProcessingStage = WEIGHING`
- `WeighingSessionId = null`

Khi chốt:

- `CutOrderStatus = COMPLETED`
- `ProcessingStage = OUT_YARD`
- `ExportFinalizedWeight = tổng net weight hợp lệ`
- `ExportFinalizedAt = now`
- `ExportFinalizedBy = current user`
- `SyncStatus = SYNC_QUEUED`

### 4.2 Trạng thái của chuyến xe con

Mỗi chuyến xe con là một `weighing_session`:

- `PENDING_WEIGHT1`
- `PENDING_WEIGHT2`
- `ALLOCATION_PENDING`
- `READY_TO_COMPLETE`
- `COMPLETED`
- `CANCELLED`

Quy tắc hiển thị ở `Danh sách xe ra`:

- Chuyến xe con xuất khẩu được hiển thị khi session đã có `Weight2` và `NetWeight`.
- Trạng thái chấp nhận:
  - `READY_TO_COMPLETE`
  - `COMPLETED`

Cần chốt lại với nghiệp vụ:

- Nếu `READY_TO_COMPLETE` đã cho xe ra được thì hiển thị ngay.
- Nếu bắt buộc operator bấm `Chuyển xe ra` thì chỉ hiển thị `COMPLETED`.

## 5. Use cases cần thêm

### 5.1 `TransitionToExportScaleUseCase`

Input:

- `Guid cutOrderId`

Điều kiện:

- Cut order tồn tại, chưa xóa, chưa hủy.
- `TransactionType = OUTBOUND`.
- `CutOrderStatus = REGISTERED`.
- `ProcessingStage = IN_YARD`.
- Chưa có session active.

Xử lý:

- Set `IsExportScale = true`.
- Set `CutOrderStatus = IN_SESSION`.
- Set `ProcessingStage = WEIGHING`.
- Set `WeighingSessionId = null`.
- Cập nhật audit.

Output:

- `cutOrderId`
- thông tin tổng quan cut order để UI điều hướng sang màn cân xuất khẩu.

### 5.2 `CreateExportVehicleSessionUseCase`

Input:

- `Guid cutOrderId`
- `string vehiclePlate`
- `string? moocNumber`
- `string? driverName`

Điều kiện:

- Cut order tồn tại.
- `IsExportScale = true`.
- `CutOrderStatus = IN_SESSION`.
- `ProcessingStage = WEIGHING`.
- Cut order chưa được chốt.
- Không tạo session nếu vehiclePlate rỗng.

Xử lý:

- Tạo `WeighingSession` mới:
  - `TransactionType = OUTBOUND`
  - `VehiclePlate = input.VehiclePlate`
  - `MoocNumber = input.MoocNumber`
  - `DriverName = input.DriverName`
  - `SessionStatus = PENDING_WEIGHT1`
- Tạo `WeighingSessionLine` liên kết:
  - `WeighingSessionId = session.Id`
  - `CutOrderId = cutOrder.Id`
  - copy customer/product/planned data từ cut order
- Không set `cutOrder.WeighingSessionId = session.Id`, vì một cut order có thể có nhiều chuyến xe.

Output:

- `SessionId`
- `SessionNo`

### 5.3 Capture cân lần 1 / cân lần 2

Khuyến nghị:

- Tái sử dụng `CaptureSessionWeight1UseCase` và `CaptureSessionWeight2UseCase` hiện có nếu đủ điều kiện.
- Nếu use case hiện có đang giả định `cut_orders.WeighingSessionId = session.Id`, cần refactor sang lấy registration qua `weighing_session_lines`.

Yêu cầu:

- Ảnh camera vẫn lưu theo `WeighingSessionId` của chuyến xe con.
- Phiếu cân/phiếu giao nhận của mỗi chuyến xe gắn với session con.

### 5.4 `CompleteExportVehicleSessionUseCase`

Input:

- `Guid sessionId`

Điều kiện:

- Session thuộc cut order `IsExportScale = true`.
- Đã có `Weight1`, `Weight2`, `NetWeight`.
- Đã auto allocate hoặc đã allocate xong.

Xử lý:

- Chuyển session con sang `COMPLETED`.
- Không đóng `cut_order` cha.
- Tạo/cập nhật phiếu cân và phiếu giao nhận của chuyến xe con theo logic hiện có.
- Cập nhật `SyncStatus` của chứng từ liên quan nếu cần.

### 5.5 `FinalizeExportCutOrderUseCase`

Input:

- `Guid cutOrderId`

Điều kiện:

- Cut order tồn tại.
- `IsExportScale = true`.
- Chưa `COMPLETED`.
- Không có session con đang dở dang:
  - `PENDING_WEIGHT1`
  - `PENDING_WEIGHT2`
  - `ALLOCATION_PENDING`
- Có ít nhất một session con đã có net weight hợp lệ.

Tổng hợp khối lượng:

- Lấy từ `weighing_session_lines.ActualAllocatedWeight`.
- Join `weighing_sessions`.
- Chỉ tính line:
  - không xóa
  - `LineStatus = ALLOCATED`
  - session không xóa, không hủy
  - session status `READY_TO_COMPLETE` hoặc `COMPLETED`, tùy quyết định nghiệp vụ.

Xử lý:

- Tính `totalWeight`.
- Set `ExportFinalizedWeight = totalWeight`.
- Set `ExportFinalizedAt`.
- Set `ExportFinalizedBy`.
- Set `CutOrderStatus = COMPLETED`.
- Set `ProcessingStage = OUT_YARD`.
- Set `SyncStatus = SYNC_QUEUED`.

## 6. Query và repository

### 6.1 Query danh sách cut order xuất khẩu active

Thêm method vào `ICutOrderRepository`:

- `GetActiveExportScaleCutOrdersAsync(filter, ct)`

Trả về DTO mới:

- `CutOrderId`
- `ErpCutOrderId`
- `CustomerName`
- `ProductCode`
- `ProductName`
- `PlannedWeight`
- `AccumulatedWeight`
- `RemainingWeight`
- `TripCount`
- `LastTripAt`
- `IsFinalized`

`AccumulatedWeight` tính từ `weighing_session_lines`.

### 6.2 Query danh sách chuyến xe của một cut order

Thêm method:

- `GetExportVehicleTripsAsync(Guid cutOrderId, CancellationToken ct)`

Trả về DTO:

- `SessionId`
- `SessionNo`
- `VehiclePlate`
- `MoocNumber`
- `DriverName`
- `Weight1`
- `Weight2`
- `NetWeight`
- `ActualAllocatedWeight`
- `Weight1Time`
- `Weight2Time`
- `SessionStatus`
- `WeighTicketNo`
- `DeliveryNo`
- `HasPrintedWeighTicket`
- `HasPrintedDeliveryTicket`

### 6.3 Cập nhật `GetOutgoingListAsync`

Hiện tại `GetOutgoingListAsync` đang lấy theo:

- `cut_orders.ProcessingStage = OUT_YARD`
- `cut_orders.CutOrderStatus = COMPLETED`

Cần bổ sung tập dữ liệu xuất khẩu:

- join `weighing_sessions`
- join `weighing_session_lines`
- join `cut_orders`
- điều kiện `cut_orders.IsExportScale = true`
- session đã có `Weight2` và `NetWeight`
- session status theo quy tắc đã chốt:
  - `READY_TO_COMPLETE`
  - `COMPLETED`

Mapping với `OutgoingVehicleListItem`:

- `CutOrderId = cut_order.Id`
- `WeighingSessionId = session.Id`
- `SessionNo = session.SessionNo`
- `VehiclePlate = session.VehiclePlate`
- `MoocNumber = session.MoocNumber`
- `DriverName = session.DriverName`
- `ActualWeightKg = line.ActualAllocatedWeight ?? session.NetWeight`
- `TotalWeightKg = session.NetWeight`
- product/customer lấy từ cut order

## 7. UI

### 7.1 Màn `Danh sách xe vào`

Thêm nút:

- `Cân xuất khẩu`

Vị trí:

- Cùng khu vực với nút `Tạo lượt cân`.

Command:

- `TransitionToExportScaleCommand`.

Điều kiện enable:

- Có dòng selected.
- `TransactionType = OUTBOUND`.
- `CutOrderStatus = REGISTERED`.
- `ProcessingStage = IN_YARD`.
- Chưa phải `IsExportScale`.

Sau khi thành công:

- Toast thông báo.
- Điều hướng sang màn `Cân xuất khẩu`.

### 7.2 Màn mới `ExportWeighingView`

Tạo file:

- `src/StationApp.UI/Views/ExportWeighingView.xaml`
- `src/StationApp.UI/ViewModels/ExportWeighingViewModel.cs`

Bố cục đề xuất:

- Header search:
  - `Mã cắt lệnh`
  - `Biển số`
  - nút làm mới
- Vùng giữa:
  - panel thông tin cut order và tiến độ
  - panel camera
  - panel cân xe con
- Vùng dưới:
  - grid cut order xuất khẩu active
  - grid danh sách chuyến xe của cut order đang chọn

Thông tin tiến độ:

- `KL kế hoạch`
- `Đã giao`
- `Còn lại`
- `Số chuyến`
- tỷ lệ tiến độ

Nút:

- `Tạo chuyến xe`
- `Cân lần 1`
- `Cân lần 2`
- `Lưu chuyến xe`
- `In PC`
- `In PGN`
- `Xem ảnh lịch sử`
- `Chốt số lượng`

### 7.3 Navigation

Sửa:

- `MainWindow.xaml`
- `MainViewModel.cs`
- `App.xaml.cs`

Thêm menu:

- `Cân xuất khẩu`

Đăng ký DI:

- `ExportWeighingViewModel`
- các use case mới

### 7.4 Camera

Phase 1:

- dùng `ICameraSettingsProvider.GetAsync()`.

Phase 2:

- thêm `GetExportAsync()`.
- thêm app config `export_camera_*`.
- bổ sung UI cấu hình trong màn `SystemSettingsView`.

## 8. In ấn và ảnh camera

### 8.1 Ảnh camera

Mỗi chuyến xe con lưu ảnh theo:

- `WeighingSessionId = session.Id`
- `CaptureStage = WEIGHT1 / WEIGHT2`

Không lưu ảnh theo cut order cha.

### 8.2 Phiếu cân

Mỗi chuyến xe con có phiếu cân riêng.

Nếu cần hiển thị lũy kế trên phiếu:

- thêm field in:
  - `ExportAccumulatedBefore`
  - `ExportTripWeight`
  - `ExportAccumulatedAfter`
  - `ExportRemainingAfter`

Phase 1 có thể chưa in các field này, chỉ cần đảm bảo phiếu của chuyến xe con in đúng.

### 8.3 Phiếu giao nhận

Mỗi chuyến xe con có phiếu giao nhận riêng.

Cut order cha không cần tạo phiếu giao nhận tổng trong phase 1, trừ khi nghiệp vụ yêu cầu.

## 9. Sync lên server / ERP

Khi chưa chốt:

- các session/phiếu con có thể sync theo cơ chế hiện tại nếu cần.
- cut order cha chưa `COMPLETED`.

Khi chốt:

- `cut_orders.ExportFinalizedWeight` là giá trị tổng chốt.
- `cut_orders.SyncStatus = SYNC_QUEUED`.
- sync local -> server đẩy bản ghi cut order với số chốt.

Cần rà soát:

- DTO sync cut order.
- `CentralApiClient`.
- stored function/procedure server đọc số thực xuất.

Nếu server hiện chỉ đọc `fn_GetCutOrderNetWeight`, cần cập nhật để ưu tiên:

- nếu `IsExportScale = 1` và `ExportFinalizedWeight IS NOT NULL` thì trả `ExportFinalizedWeight`.
- ngược lại tính theo logic hiện tại.

## 10. Validation và rule chặn lỗi

### 10.1 Không cho chốt nếu còn chuyến xe dở dang

Chặn nếu tồn tại session con:

- chưa có cân lần 1
- đã có cân lần 1 nhưng chưa có cân lần 2
- đã có cân lần 2 nhưng chưa allocate xong

### 10.2 Không cho RA cắt lệnh khi có session con đã có cân lần 2

Cần cập nhật proc RA nếu cần:

- nếu cut order export đã có bất kỳ session con `Weight2 IS NOT NULL` thì chặn RA.
- nếu chưa có cân lần 2 thì cho phép RA theo logic hiện có.

### 10.3 Không cho tạo chuyến xe sau khi đã chốt

Nếu:

- `CutOrderStatus = COMPLETED`
- hoặc `ExportFinalizedAt IS NOT NULL`

thì chặn tạo session con mới.

### 10.4 Không cho chốt số âm/rỗng

Tổng net weight hợp lệ phải:

- lớn hơn hoặc bằng 0
- có ít nhất một chuyến xe hợp lệ

## 11. Task breakdown

### Phase 1: Data và domain

1. Thêm field vào `CutOrder.cs`.
2. Thêm mapping vào `CutOrderEntityConfiguration.cs`.
3. Thêm bootstrap columns vào `SchemaCompatibilityBootstrapper.cs`.
4. Thêm DTO:
   - `ExportScaleCutOrderListItem`
   - `ExportVehicleTripListItem`
5. Build kiểm tra domain/application/infrastructure.

### Phase 2: Repository và query

1. Thêm query danh sách cut order xuất khẩu active.
2. Thêm query danh sách chuyến xe theo cut order.
3. Cập nhật `GetOutgoingListAsync`.
4. Cập nhật query phiếu liên quan nếu cần để lấy theo session con.

### Phase 3: Use cases

1. `TransitionToExportScaleUseCase`.
2. `CreateExportVehicleSessionUseCase`.
3. Refactor/tái sử dụng capture weight use cases cho session con.
4. `CompleteExportVehicleSessionUseCase` nếu use case hiện có không phù hợp.
5. `FinalizeExportCutOrderUseCase`.

### Phase 4: UI

1. Thêm nút `Cân xuất khẩu` vào `IncomingVehicleListView`.
2. Tạo `ExportWeighingView`.
3. Tạo `ExportWeighingViewModel`.
4. Thêm navigation/menu.
5. Gắn camera preview/capture.
6. Gắn print/image history commands.

### Phase 5: Sync và SQL server

1. Cập nhật DTO sync nếu thiếu field export.
2. Cập nhật server SQL function/procedure lấy số thực xuất.
3. Test sync local -> server.

### Phase 6: Verification

1. Build app.
2. Chạy schema update trên DB local cũ.
3. Test manual với cut order 100 tấn và 2 chuyến xe.
4. Test `Danh sách xe ra` hiện từng xe con.
5. Test chốt số lượng.
6. Test không chốt được khi còn xe dở dang.

## 12. Acceptance criteria

1. Operator chuyển được một cut order outbound sang luồng `Cân xuất khẩu`.
2. Một cut order xuất khẩu tạo được nhiều chuyến xe với biển số/mooc/tài xế khác nhau.
3. Mỗi chuyến xe có session riêng, cân lần 1/lần 2 riêng, ảnh camera riêng.
4. Mỗi chuyến xe sau khi cân xong lần 2 hiện trên `Danh sách xe ra`.
5. Màn xuất khẩu hiển thị đúng:
   - tổng kế hoạch
   - đã giao
   - còn lại
   - số chuyến xe
6. Không chốt được cut order nếu còn chuyến xe dở dang.
7. Khi chốt, `ExportFinalizedWeight` bằng tổng khối lượng hợp lệ của các chuyến xe.
8. Sau khi chốt, cut order thành `COMPLETED` và `OUT_YARD`.
9. Sau khi chốt, cut order được đưa vào sync queue.
10. Không làm hỏng luồng cân hiện tại:
   - cân outbound thường
   - cân inbound
   - quá tải
   - in PC/PGN
   - ảnh camera lịch sử

## 13. Rủi ro và điểm cần chốt trước khi code

### 13.1 Có cho phép nhiều xe con active cùng lúc không?

Nếu có:

- không được dùng `cut_orders.WeighingSessionId` làm active session pointer.
- màn export phải hiển thị danh sách session con đang dở dang.

Nếu không:

- có thể chặn tạo chuyến xe mới khi đang có session con chưa `READY_TO_COMPLETE`/`COMPLETED`.

### 13.2 Session `READY_TO_COMPLETE` đã được coi là xe ra chưa?

Cần chốt:

- hiện ở `Danh sách xe ra` ngay sau cân lần 2
- hay chỉ hiện sau khi bấm `Chuyển xe ra`

Plan nguồn yêu cầu hiện ngay sau cân lần 2.

### 13.3 Có cần phiếu giao nhận tổng không?

Phase 1 đề xuất:

- mỗi chuyến xe có PGN riêng.
- chưa tạo PGN tổng cho cut order cha.

Nếu nghiệp vụ cần PGN tổng khi chốt, cần thêm task riêng.

### 13.4 Camera export riêng có bắt buộc ngay phase 1 không?

Khuyến nghị:

- phase 1 fallback camera hiện tại.
- phase 2 thêm config riêng.

### 13.5 ERP lấy số thực xuất từ đâu?

Cần xác nhận server đang đọc:

- `ExportFinalizedWeight`
- hay `fn_GetCutOrderNetWeight`
- hay procedure sync riêng

Phần sync/SQL server phải chốt trước khi code phase 5.
