# PLAN - Xóa lượt cân lần 2 cho xuất hàng nội địa và xuất khẩu

## 1. Phạm vi

Chức năng này **chỉ áp dụng cho luồng xuất hàng nội địa và xuất khẩu tại NMC/QN01**:

- Màn `Cân nội địa`.
- Màn `Cân xuất khẩu`.
- Màn `Danh sách xe ra`.

Không áp dụng cho:

- `Cân mỏ đá`.
- `Cân mỏ sét`.
- Báo cáo/luồng tàu mỏ sét.
- TL bì xe nội bộ của mỏ đá/mỏ sét.

## 2. Mục tiêu

Cho phép người dùng xóa lượt cân lần 2 của một lượt cân đã có cân lần 2, đưa lượt cân về trạng thái `PENDING_WEIGHT2` để người dùng cân lần 2 lại.

Không được sửa trực tiếp bằng SQL hoặc chỉ set `Weight2 = null`, vì cân lần 2 hiện đang kéo theo:

- `WeighingSession.Weight2`, `Weight2Time`, `NetWeight`, `SessionStatus`.
- Phiếu cân tổng `WeighTicket`.
- Dòng phân bổ `WeighingSessionLine`.
- Phiếu giao nhận `DeliveryTicket`.
- Phiếu cân con/derived nếu đã phân bổ nhiều cắt lệnh.
- Trạng thái cắt lệnh.
- Báo cáo, in phiếu, sync ERP/outbox, audit log.

## 3. Kết luận khả thi

Khả thi nếu làm bằng một use case nghiệp vụ riêng, chạy trong transaction và có guard rõ ràng.

Đề xuất tên use case:

- `DeleteSessionWeight2UseCase`

Đề xuất action audit:

- `DELETE_WEIGHT_2`
- Text hiển thị: `Xóa lượt cân lần 2`

## 4. Code hiện tại liên quan

### 4.1 Lưu cân lần 2

File:

- `src/StationApp.Application/UseCases/CaptureSessionWeight2UseCase.cs`

Hiện tại khi lưu cân lần 2:

- Validate session phải ở `PENDING_WEIGHT2`.
- Tính `NetWeight = Abs(Weight1 - Weight2)`.
- Set `Weight2`, `Weight2Time`, `NetWeight`.
- Chuyển session sang `ALLOCATION_PENDING`.
- Nếu session chỉ có một line:
  - Tự phân bổ line.
  - Tạo/cập nhật phiếu giao nhận.
  - Có thể xác nhận số bao xuất khẩu.
  - Có thể đánh dấu Hoàn.
  - Chuyển session sang `READY_TO_COMPLETE`.
- Sync phiếu cân tổng qua `WeighingSessionTicketSyncService`.

### 4.2 Phân bổ nhiều cắt lệnh

File:

- `src/StationApp.Application/UseCases/AllocateWeighingSessionUseCase.cs`

Sau cân lần 2, nếu có nhiều line:

- Cập nhật `ActualAllocatedWeight`, `ActualAllocatedBagCount`.
- Tạo phiếu giao nhận từng line.
- Có thể tạo phiếu giao nhận tổng.
- Có thể tạo phiếu cân con `CutOrderDerived`.
- Chuyển session sang `READY_TO_COMPLETE`.

### 4.3 Hoàn tất lượt cân ra

File:

- `src/StationApp.Application/UseCases/CompleteWeighingSessionUseCase.cs`

Khi hoàn tất:

- Session chuyển sang `COMPLETED`.
- Với xuất khẩu: cắt lệnh quay về `IN_SESSION`, `WEIGHING` để tiếp tục nhận chuyến khác.
- Với xuất nội địa: cắt lệnh có thể chuyển `COMPLETED`, `OUT_YARD`.

### 4.4 Chốt cắt lệnh xuất khẩu

File:

- `src/StationApp.Application/UseCases/FinalizeExportCutOrderUseCase.cs`

Nếu cắt lệnh xuất khẩu đã chốt tổng:

- `ExportFinalizedAt` có giá trị.
- `CutOrderStatus = COMPLETED`.
- `ExportFinalizedWeight` đã được tính.
- Có thể đã queue sync ERP.

Trường hợp này không nên cho xóa lượt cân lần 2 vì sẽ làm lệch số đã chốt/đẩy ERP.

### 4.5 Xóa chuyến xuất khẩu hiện tại

File:

- `src/StationApp.Application/UseCases/DeleteExportVehicleTripUseCase.cs`

Use case này đang có pattern hữu ích:

- Không cho xóa chuyến đã cân lần 2.
- Soft-delete session/line/ticket/delivery ticket.
- Chọn lại `CurrentPrimaryWeighTicketId`, `CurrentPrimaryDeliveryTicketId` nếu chứng từ hiện tại bị xóa.

Chức năng xóa lượt cân lần 2 có thể tái sử dụng cùng hướng xử lý soft-delete chứng từ, nhưng không xóa session.

## 5. Nguyên tắc nghiệp vụ

1. Chỉ cho xóa lượt cân lần 2 thuộc luồng xuất hàng nội địa hoặc xuất khẩu.
2. Chỉ cho xóa khi session có `Weight2` hoặc `Weight2Time`.
3. Sau khi xóa lượt cân lần 2, session quay về `PENDING_WEIGHT2`.
4. Giữ nguyên:
   - Số lượt cân.
   - Biển số xe/mooc/tài xế.
   - `Weight1`, `Weight1Time`.
   - Các line/cắt lệnh đang gắn vào session.
5. Xóa/reset dữ liệu sinh từ cân lần 2:
   - `Weight2`, `Weight2Time`, `NetWeight`.
   - Phân bổ line.
   - Số bao thực tế/xác nhận số bao.
   - Đánh dấu Hoàn.
   - Trạng thái overweight.
   - Phiếu giao nhận và phiếu cân derived liên quan.
6. Không xóa ảnh đã chụp và audit log cũ.
7. Không hard-delete phiếu đã sinh; dùng soft-delete/cancel để giữ dấu vết số phiếu.
8. Bắt buộc nhập lý do xóa lượt cân lần 2.
9. Ghi audit log đầy đủ old/new.

## 6. Guard đề xuất

### 6.1 Cho phép xóa lượt cân lần 2 khi

- Session chưa bị hủy/xóa.
- Session có `Weight2` hoặc `Weight2Time`.
- Session thuộc `TransactionType.OUTBOUND`.
- Session có station `QN01` hoặc thuộc màn NMC đang thao tác.
- Các cắt lệnh liên quan chưa bị chốt khóa theo nghiệp vụ.

### 6.2 Chặn xóa lượt cân lần 2 khi

- Session không có cân lần 2.
- Session đã hủy/xóa.
- Cắt lệnh xuất khẩu đã chốt tổng:
  - `ExportFinalizedAt != null`, hoặc
  - `CutOrderStatus = COMPLETED`, hoặc
  - `ErpExportCompleted = true`.
- Chứng từ đã có trạng thái sync ERP thành công mà việc rollback sẽ tạo lệch dữ liệu. Nếu code hiện tại không có trạng thái sync thành công rõ ràng, cần ít nhất chặn theo trạng thái chốt tổng.
- Session thuộc mỏ đá/mỏ sét.

### 6.3 Quyền thao tác

Đề xuất chỉ cho:

- `Manager`
- `Admin`

Không cho `Operator` thực hiện vì đây là thao tác xóa dữ liệu đã ghi nhận và ảnh hưởng chứng từ.

## 7. Hành vi xóa lượt cân lần 2 chi tiết

### 7.1 Reset `WeighingSession`

Set:

- `Weight2 = null`
- `Weight2Time = null`
- `NetWeight = null`
- `SessionStatus = PENDING_WEIGHT2`
- `IsOverweight = false`
- `OverweightAmount = 0`
- `OverweightResolutionStatus = NOT_APPLICABLE`
- `OverweightResolvedAt = null`
- `OverweightResolvedBy = null`
- `SyncStatus = SYNC_QUEUED`
- `LastSyncAttemptAt = null`
- `LastSyncError = null`
- `UpdatedAt`, `UpdatedBy`

Không đổi:

- `Weight1`
- `Weight1Time`
- `SessionNo`
- `VehiclePlate`
- `MoocNumber`
- `DriverName`
- `TransactionType`

### 7.2 Reset phiếu cân tổng `WeighTicket` role `MasterSession`

Set:

- `Weight2 = null`
- `Weight2Time = null`
- `Weight2User = null`
- `Weight2Mode = null`
- `Weight2IsStable = null`
- `Weight2UpdatedAt = null`
- `NetWeight = null`
- `IsOverWeight = false`
- `Status = LOADING_STARTED`
- `SyncStatus = SYNC_QUEUED`
- `UpdatedAt`, `UpdatedBy`

Không đổi:

- `TicketNo`
- `Weight1`
- `Weight1Time`
- Snapshot xe/mooc.

### 7.3 Reset `WeighingSessionLine`

Với tất cả line active của session:

- `ActualAllocatedWeight = null`
- `ActualAllocatedBagCount = null`
- `BagCountDisplay = null`
- `SystemCalculatedBagCount = null`
- `BagCountConfirmedAt = null`
- `BagCountConfirmedBy = null`
- `BagCountConfirmationMode = null`
- `Note = null`
- `IsReturnedBrokenTrip = false`
- `LineStatus = PENDING`
- `DeliveryTicketId = null`
- `SyncStatus = SYNC_QUEUED`
- `LastSyncAttemptAt = null`
- `LastSyncError = null`
- `UpdatedAt`, `UpdatedBy`

### 7.4 Soft-delete phiếu giao nhận liên quan

Với các `DeliveryTicket` active có `WeighingSessionId = session.Id`:

- `IsDeleted = true`
- `DeletedAt`, `DeletedBy`
- `AllocatedWeight = 0` hoặc null theo convention đang dùng.
- `AllocatedBagCount = 0` hoặc null theo convention đang dùng.
- `IsOverWeight = false`
- `SyncStatus = SYNC_QUEUED`
- `UpdatedAt`, `UpdatedBy`

Nếu cắt lệnh đang trỏ `CurrentPrimaryDeliveryTicketId` đến phiếu bị xóa:

- Chọn lại phiếu giao nhận hợp lệ gần nhất của cắt lệnh.
- Nếu không còn phiếu hợp lệ thì set null.

### 7.5 Soft-delete phiếu cân con/derived

Với `WeighTicket` active có `WeighingSessionId = session.Id` và role:

- `CutOrderDerived`
- `SplitDerived`

Set:

- `IsDeleted = true`
- `IsCancelled = true`
- `Status = TICKET_CANCELLED`
- `NetWeight = 0`
- `DeletedAt`, `DeletedBy`
- `SyncStatus = SYNC_QUEUED`
- `UpdatedAt`, `UpdatedBy`

Nếu cắt lệnh đang trỏ `CurrentPrimaryWeighTicketId` đến phiếu bị xóa:

- Chọn lại phiếu cân hợp lệ gần nhất.
- Nếu không còn phiếu hợp lệ thì set về phiếu cân tổng hoặc null tùy quan hệ cắt lệnh.

### 7.6 Reset trạng thái cắt lệnh

Với cắt lệnh xuất nội địa:

- Nếu cắt lệnh đang `COMPLETED` do lượt cân này hoàn tất:
  - Chuyển về trạng thái phù hợp để cân lại: đề xuất `IN_SESSION`, `ProcessingStage = WEIGHING`.
  - `SyncStatus = SYNC_QUEUED`.

Với cắt lệnh xuất khẩu:

- Nếu chưa chốt tổng:
  - Giữ cắt lệnh ở trạng thái đang nhận chuyến.
  - Clear các primary ticket/delivery nếu đang trỏ đến chứng từ đã soft-delete.
- Nếu đã chốt tổng:
  - Chặn xóa lượt cân lần 2.

## 8. UI đề xuất

### 8.1 Vị trí nút

Thêm nút `Xóa lượt cân lần 2` tại:

- Màn `Cân nội địa`.
- Màn `Cân xuất khẩu`.
- Màn `Danh sách xe ra`.

Ba vị trí này dùng chung một nghiệp vụ `DeleteSessionWeight2UseCase`; không tạo logic riêng cho từng màn.

Không thêm nút ở:

- `Cân mỏ đá`.
- `Cân mỏ sét`.

### 8.2 Điều kiện enable

Nút enable khi:

- Có selected session/trip.
- Selected session có `Weight2` hoặc `Weight2Time`.
- Selected session không bị hủy/xóa.
- Role hiện tại là `Manager` hoặc `Admin`.
- Không bị khóa bởi chốt tổng xuất khẩu.

Riêng màn `Danh sách xe ra`:

- Chỉ enable với xe/lượt cân đã có cân lần 2.
- Chỉ áp dụng cho xe xuất nội địa hoặc xuất khẩu thuộc NMC/QN01.
- Sau khi xóa lượt cân lần 2, dòng xe không còn được xem là đã hoàn tất cân ra cho đến khi cân lần 2 lại.
- Lượt cân phải quay lại đúng màn cân nguồn để xử lý tiếp:
  - Xuất nội địa quay lại màn `Cân nội địa`.
  - Xuất khẩu quay lại màn `Cân xuất khẩu`.
  - Việc xác định màn nguồn dựa vào `TransactionType`, `CutOrder.IsExportScale` và các line/cắt lệnh đang gắn với session.

### 8.3 Modal xác nhận

Modal hiển thị:

- Số lượt cân.
- Biển số xe.
- Cắt lệnh/tàu nếu có.
- Cân lần 1.
- Cân lần 2 hiện tại.
- TL hàng hiện tại.
- Danh sách chứng từ sẽ bị ảnh hưởng:
  - Phiếu cân tổng.
  - Phiếu giao nhận.
  - Phiếu cân con nếu có.
- Cảnh báo nếu phiếu đã in.
- Ô `Lý do xóa lượt cân lần 2` bắt buộc.

Nút:

- `Hủy`
- `Đồng ý`

Sau khi thành công:

- Refresh lại grid.
- Giữ chọn lượt cân vừa xóa lượt cân lần 2 nếu còn trong danh sách.
- Toast: `Đã xóa lượt cân lần 2 cho lượt cân {SessionNo}.`

## 9. Audit log

Action:

- `DELETE_WEIGHT_2`

Text hiển thị:

- `Xóa lượt cân lần 2`

DetailJson old/new cần có:

- Subject:
  - `SessionNo`
  - `VehiclePlate`
  - `MoocNumber`
  - `StationCode`
  - `CutOrderCode`
- OldValue:
  - `Weight2`
  - `Weight2Time`
  - `NetWeight`
  - `SessionStatus`
  - `LineStatus`
  - `ActualAllocatedWeight`
  - `ActualAllocatedBagCount`
  - `DeliveryTicketNo`
  - `WeighTicketNo`
  - `IsReturnedBrokenTrip`
- NewValue:
  - Các trường tương ứng sau khi reset.
- Summary:
  - Lý do xóa lượt cân lần 2.
  - Danh sách phiếu giao nhận bị soft-delete.
  - Danh sách phiếu cân con bị cancel.
  - Có phiếu đã in hay chưa.

Màn `Lịch sử chỉnh sửa` cần hiển thị action này ở đúng trạm hiện tại.

## 10. Task triển khai

### Task 1: Tạo use case xóa lượt cân lần 2

**Mô tả:** Tạo `DeleteSessionWeight2UseCase` cho luồng outbound session.

**Acceptance criteria:**

- Request gồm `SessionId`, `Reason`.
- Validate quyền `Manager/Admin`.
- Validate đúng scope xuất nội địa/xuất khẩu, không áp dụng mỏ đá/mỏ sét.
- Validate session có cân lần 2.
- Validate cắt lệnh xuất khẩu chưa chốt tổng.

**Files dự kiến:**

- `src/StationApp.Application/UseCases/DeleteSessionWeight2UseCase.cs`
- `src/StationApp.Application/Security/StationAuthorization.cs`
- `tests/StationApp.Application.Tests/...`

### Task 2: Reset session, line và chứng từ

**Mô tả:** Hoàn tác dữ liệu sinh từ cân lần 2 theo mục 7.

**Acceptance criteria:**

- Session quay về `PENDING_WEIGHT2`.
- Phiếu cân tổng không còn cân lần 2/TL hàng.
- Line không còn phân bổ/số bao/Hoàn.
- Delivery ticket liên quan bị soft-delete.
- Phiếu cân derived liên quan bị cancel/soft-delete.
- Cắt lệnh không còn trỏ đến chứng từ đã bị soft-delete.

**Verification:**

- Test session một line tự phân bổ.
- Test session nhiều line đã phân bổ.
- Test session đã `COMPLETED` nhưng chưa chốt khóa.

### Task 3: Guard chốt tổng và chứng từ khóa

**Mô tả:** Chặn các case có rủi ro làm lệch dữ liệu ERP/chốt tổng.

**Acceptance criteria:**

- Chặn cắt lệnh xuất khẩu đã `ExportFinalizedAt`.
- Chặn cắt lệnh xuất khẩu `CutOrderStatus = COMPLETED`.
- Chặn `ErpExportCompleted = true`.
- Message lỗi tiếng Việt rõ ràng.

**Verification:**

- Unit test cho từng guard.

### Task 4: Audit log

**Mô tả:** Ghi audit log old/new chuẩn hóa.

**Acceptance criteria:**

- Có action `DELETE_WEIGHT_2`.
- Dropdown Hành động hiển thị `Xóa lượt cân lần 2`.
- Grid lịch sử hiển thị giá trị cũ/mới có nghĩa, không hiển thị GUID thô.
- Lý do xóa lượt cân lần 2 hiển thị được trên lịch sử.

**Verification:**

- Test `AuditLogDisplayMapper`.
- Manual check màn Lịch sử chỉnh sửa tại QN01.

### Task 5: UI Cân nội địa

**Mô tả:** Thêm nút `Xóa lượt cân lần 2` và modal ở màn `Cân nội địa`.

**Acceptance criteria:**

- Nút chỉ enable đúng điều kiện.
- Modal hiển thị đủ thông tin và bắt buộc lý do.
- Sau khi xóa lượt cân lần 2, người dùng có thể cân lần 2 lại.
- Grid/báo cáo không còn tính lượt vừa xóa lượt cân lần 2 như lượt đã hoàn tất.

### Task 6: UI Cân xuất khẩu

**Mô tả:** Thêm nút `Xóa lượt cân lần 2` và modal ở màn `Cân xuất khẩu`.

**Acceptance criteria:**

- Nút chỉ enable đúng điều kiện.
- Chặn nếu cắt lệnh đã chốt tổng.
- Reset số bao xác nhận/Hoàn/phiếu giao nhận.
- Sau khi xóa lượt cân lần 2, chuyến xe quay về trạng thái chờ cân lần 2.

### Task 7: UI Danh sách xe ra

**Mô tả:** Thêm nút `Xóa lượt cân lần 2` ở màn `Danh sách xe ra` để Quản lý/Quản trị có thể xóa lượt cân lần 2 ngay tại nơi kiểm soát xe ra.

**Acceptance criteria:**

- Nút chỉ enable khi chọn dòng xe/lượt cân có cân lần 2.
- Nút không hiển thị/không enable cho mỏ đá/mỏ sét.
- Dùng chung modal xác nhận và use case với `Cân nội địa`/`Cân xuất khẩu`.
- Sau khi xóa lượt cân lần 2, refresh danh sách xe ra và dòng đó không còn ở trạng thái đã cân ra hoàn tất nếu không còn thỏa điều kiện của màn.
- Sau khi thao tác từ `Danh sách xe ra`, hệ thống điều hướng hoặc đưa người dùng về đúng màn cân nguồn của lượt cân:
  - `Cân nội địa` nếu là lượt xuất nội địa.
  - `Cân xuất khẩu` nếu là chuyến xe xuất khẩu.
- Lượt cân/chuyến xe vừa xóa lượt cân lần 2 được chọn lại ở màn cân nguồn nếu còn tìm thấy trong danh sách.
- Ghi audit log `DELETE_WEIGHT_2` với lý do người dùng nhập.

### Task 8: Kiểm thử hồi quy

**Mô tả:** Kiểm tra các luồng xuất hàng chính sau thay đổi.

**Acceptance criteria:**

- Cân lần 1 -> cân lần 2 -> xóa lượt cân lần 2 -> cân lần 2 lại hoạt động đúng.
- In phiếu sau khi cân lại lấy dữ liệu mới.
- Báo cáo xuất nội địa/XK không tính dữ liệu đã xóa lượt cân lần 2.
- Chốt tổng xuất khẩu tính theo dữ liệu sau khi cân lại.
- Thao tác `Xóa lượt cân lần 2` từ `Danh sách xe ra` đưa lượt cân về đúng màn `Cân nội địa` hoặc `Cân xuất khẩu`.
- Mỏ đá/mỏ sét không xuất hiện nút và không bị ảnh hưởng.

## 11. Rủi ro và giảm thiểu

| Rủi ro | Mức độ | Giảm thiểu |
|---|---:|---|
| Lệch session với phiếu cân/phiếu giao nhận | Cao | Một use case transaction reset đầy đủ |
| Xóa lượt cân lần 2 của dữ liệu đã chốt tổng XK | Cao | Guard chặn `ExportFinalizedAt`, `CutOrderStatus = COMPLETED`, `ErpExportCompleted` |
| Phiếu đã in khác dữ liệu sau khi xóa lượt cân lần 2 | Trung bình | Cảnh báo trong modal, giữ lịch sử in, ghi audit |
| Báo cáo vẫn tính lượt đã xóa lượt cân lần 2 | Trung bình | Reset `Weight2Time`, `NetWeight`, line allocation và chứng từ active |
| Người dùng thao tác nhầm | Trung bình | Chỉ Manager/Admin, bắt buộc lý do, modal hiển thị rõ dữ liệu bị ảnh hưởng |

## 12. Câu hỏi cần duyệt

1. Tên nút và audit log thống nhất dùng `Xóa lượt cân lần 2`. Có đồng ý cách đặt tên này không?
2. Có đồng ý chỉ `Manager/Admin` được thao tác không?
3. Có cho xóa lượt cân lần 2 nếu phiếu đã in không? Mình đề xuất cho phép nhưng cảnh báo và ghi audit.
4. Với xuất nội địa đã hoàn tất ra cổng, khi xóa lượt cân lần 2 nên đưa cắt lệnh về `IN_SESSION/WEIGHING` như đề xuất có đúng không?
