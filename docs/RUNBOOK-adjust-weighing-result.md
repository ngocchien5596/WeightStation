# Runbook: Điều Chỉnh Kết Quả Cân Bằng SQL

Tài liệu này hướng dẫn cách dùng stored procedure `dbo.sp_AdjustWeighingResult` để điều chỉnh kết quả cân khi người dùng thao tác sai, nhưng vẫn đảm bảo dữ liệu trong các bảng nghiệp vụ được cập nhật đồng bộ.

## 1. Mục Đích

Stored procedure `dbo.sp_AdjustWeighingResult` được dùng để sửa kết quả cân theo một luồng chuẩn, tránh sửa tay rời rạc nhiều bảng như:

- `weighing_sessions`
- `weighing_session_lines`
- `weigh_tickets`
- `delivery_tickets`
- `cut_orders`
- `audit_logs`

Procedure chạy trong một transaction. Nếu dữ liệu sau điều chỉnh không hợp lệ, toàn bộ thay đổi sẽ rollback.

## 2. File Script

File script nằm tại:

```text
scripts/sql/sp_AdjustWeighingResult.sql
```

## 3. Cài Đặt Stored Procedure

Chạy script trên database local `StationAppLocal` bằng account có quyền tạo/sửa procedure.

Nếu dùng SSMS, mở file sau và Execute:

```text
scripts/sql/sp_AdjustWeighingResult.sql
```

Nếu dùng SQLCMD:

```sql
:r scripts/sql/sp_AdjustWeighingResult.sql
```

Nếu runtime user chưa có quyền gọi procedure, cấp quyền:

```sql
GRANT EXECUTE ON dbo.sp_AdjustWeighingResult TO [stationapp_runtime];
```

Nếu database user thực tế khác `stationapp_runtime`, thay bằng đúng user đang chạy ứng dụng.

## 4. Cú Pháp

```sql
EXEC dbo.sp_AdjustWeighingResult
    @ErpCutOrderId = N'QN.CL.2606/0014',
    @Weight1 = NULL,
    @Weight2 = NULL,
    @ActualAllocatedWeight = NULL,
    @ActualAllocatedBagCount = NULL,
    @UpdateSessionMaster = NULL,
    @UpdateExportFinalizedWeight = 1,
    @AdjustedAt = NULL,
    @AdjustedBy = N'admin',
    @Reason = N'Lý do điều chỉnh';
```

## 5. Ý Nghĩa Tham Số

| Tham số | Bắt buộc | Ý nghĩa |
| --- | --- | --- |
| `@ErpCutOrderId` | Có | Mã cắt lệnh cần điều chỉnh. |
| `@Weight1` | Không | Giá trị cân lần 1 mới. |
| `@Weight2` | Không | Giá trị cân lần 2 mới. |
| `@ActualAllocatedWeight` | Không | Khối lượng thực tế phân bổ cho cắt lệnh, đơn vị kg. |
| `@ActualAllocatedBagCount` | Không | Số bao thực tế của cắt lệnh. |
| `@UpdateSessionMaster` | Không | `NULL`: tự quyết định. `1`: cập nhật cả session master. `0`: chỉ cập nhật line/chứng từ của cắt lệnh. |
| `@UpdateExportFinalizedWeight` | Không | Với cân xuất khẩu, `1` sẽ cập nhật lại `ExportFinalizedWeight`. |
| `@AdjustedAt` | Không | Thời điểm điều chỉnh. Nếu không truyền, dùng `SYSDATETIME()`. |
| `@AdjustedBy` | Không | Người thực hiện điều chỉnh. |
| `@Reason` | Không | Lý do điều chỉnh, dùng để audit. |

## 6. Nguyên Tắc Cập Nhật

Procedure áp dụng các nguyên tắc sau:

- Nếu lượt cân chỉ có 1 cắt lệnh, mặc định cập nhật cả `weighing_sessions`.
- Nếu lượt cân có nhiều cắt lệnh, mặc định chỉ cập nhật line và chứng từ của cắt lệnh được truyền vào.
- Nếu ép `@UpdateSessionMaster = 1`, tổng `ActualAllocatedWeight` của các line phải khớp `ABS(Weight1 - Weight2)`.
- Nếu không khớp, procedure báo lỗi và rollback.
- Các entity bị thay đổi sẽ được đưa về trạng thái chờ đồng bộ.
- Mỗi lần điều chỉnh sẽ ghi audit vào `audit_logs` nếu bảng tồn tại.

## 7. Tình Huống 1: Sửa Khối Lượng Thực Của Một Cắt Lệnh

Dùng khi chỉ cần sửa khối lượng thực tế của một mã cắt lệnh, không sửa số cân vật lý của cả lượt cân.

Ví dụ sửa cắt lệnh `QN.CL.2606/0014` về `10.000 kg` và `200 bao`:

```sql
EXEC dbo.sp_AdjustWeighingResult
    @ErpCutOrderId = N'QN.CL.2606/0014',
    @ActualAllocatedWeight = 10000,
    @ActualAllocatedBagCount = 200,
    @AdjustedBy = N'admin',
    @Reason = N'Sửa khối lượng thực xuất do thao tác sai';
```

Trường hợp này phù hợp khi một lượt cân có nhiều cắt lệnh, nhưng chỉ một cắt lệnh bị sai khối lượng phân bổ.

## 8. Tình Huống 2: Sửa Cân Lần 1 Và Cân Lần 2 Cho Lượt Cân Một Cắt Lệnh

Dùng khi số cân vật lý bị sai và lượt cân chỉ có một cắt lệnh.

Ví dụ:

```sql
EXEC dbo.sp_AdjustWeighingResult
    @ErpCutOrderId = N'QN.CL.2606/0014',
    @Weight1 = 25000,
    @Weight2 = 15000,
    @AdjustedBy = N'admin',
    @Reason = N'Sửa số cân lần 1 và cân lần 2';
```

Procedure sẽ tự tính:

```text
NetWeight = ABS(Weight1 - Weight2)
```

Sau đó cập nhật đồng bộ session, line, phiếu cân, phiếu giao nhận và cắt lệnh.

## 9. Tình Huống 3: Sửa Cả Session Master Của Lượt Cân Nhiều Cắt Lệnh

Chỉ dùng khi cần sửa số cân tổng của cả lượt cân và đã biết chắc tổng phân bổ sau sửa phải khớp với số cân vật lý mới.

Ví dụ:

```sql
EXEC dbo.sp_AdjustWeighingResult
    @ErpCutOrderId = N'QN.CL.2606/0014',
    @Weight1 = 100000,
    @Weight2 = 80000,
    @ActualAllocatedWeight = 10000,
    @ActualAllocatedBagCount = 200,
    @UpdateSessionMaster = 1,
    @AdjustedBy = N'admin',
    @Reason = N'Sửa lại tổng cân và phân bổ cắt lệnh';
```

Nếu tổng các line sau điều chỉnh không bằng `ABS(Weight1 - Weight2)`, procedure sẽ rollback.

## 10. Kiểm Tra Sau Khi Điều Chỉnh

Kiểm tra số lượng thực xuất ERP lấy được:

```sql
EXEC dbo.sp_GetCutOrderNetWeight
    @ErpCutOrderId = N'QN.CL.2606/0014';
```

Kiểm tra session và line:

```sql
SELECT
    co.ErpCutOrderId,
    ws.SessionNo,
    ws.Weight1,
    ws.Weight2,
    ws.NetWeight,
    wsl.ActualAllocatedWeight,
    wsl.ActualAllocatedBagCount
FROM dbo.cut_orders co
JOIN dbo.weighing_session_lines wsl
    ON wsl.CutOrderId = co.Id
JOIN dbo.weighing_sessions ws
    ON ws.Id = wsl.WeighingSessionId
WHERE co.ErpCutOrderId = N'QN.CL.2606/0014'
  AND ISNULL(co.IsDeleted, 0) = 0
  AND ISNULL(wsl.IsDeleted, 0) = 0;
```

Kiểm tra phiếu cân và phiếu giao nhận:

```sql
SELECT
    TicketNo,
    ErpCutOrderId,
    Weight1,
    Weight2,
    NetWeight,
    RecordRole,
    UpdatedAt,
    UpdatedBy
FROM dbo.weigh_tickets
WHERE ErpCutOrderId = N'QN.CL.2606/0014'
  AND ISNULL(IsDeleted, 0) = 0;

SELECT
    DeliveryNo,
    ErpCutOrderId,
    AllocatedWeight,
    AllocatedBagCount,
    RecordRole,
    UpdatedAt,
    UpdatedBy
FROM dbo.delivery_tickets
WHERE ErpCutOrderId = N'QN.CL.2606/0014'
  AND ISNULL(IsDeleted, 0) = 0;
```

Kiểm tra audit:

```sql
SELECT TOP (20)
    CreatedAt,
    Actor,
    Action,
    EntityType,
    EntityId,
    DetailJson
FROM dbo.audit_logs
WHERE Action = N'ADJUST_WEIGHING_RESULT'
ORDER BY CreatedAt DESC;
```

## 11. Đồng Bộ Lên Central

Procedure sẽ đưa các entity bị sửa về trạng thái chờ đồng bộ.

Sau khi điều chỉnh, nếu cần đồng bộ ngay:

1. Mở màn `Cấu hình đồng bộ`.
2. Nhấn `Đồng bộ ngay`.
3. Kiểm tra các dòng liên quan không còn trạng thái lỗi như `FAILED_RETRYABLE`.

Nếu central chưa cập nhật, chạy thêm luồng đồng bộ lại chứng từ/cắt lệnh trong ứng dụng nếu đã có sẵn, vì một số payload outbox có thể cần được tạo lại theo dữ liệu mới.

## 12. Lưu Ý Vận Hành

- Không sửa tay trực tiếp từng bảng nếu có thể dùng procedure này.
- Luôn truyền `@Reason` để audit có ý nghĩa.
- Nên backup database trước khi sửa dữ liệu lớn.
- Nên thực hiện khi ứng dụng ít người dùng thao tác.
- Sau khi sửa, luôn kiểm tra lại dashboard, báo cáo xuất/nhập và kết quả `sp_GetCutOrderNetWeight`.

