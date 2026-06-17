# Plan: Tách dữ liệu theo trạm bằng Station Scope

## 1. Mục tiêu

Triển khai cơ chế phân vùng dữ liệu theo trạm để mỗi máy trạm chỉ nhìn thấy và thao tác dữ liệu thuộc trạm của mình.

Use case trước mắt:

- Trạm cân chính vẫn vận hành các luồng hiện tại.
- Trạm đập chủ yếu cân hàng nhập, ví dụ đá vôi.
- Dữ liệu của trạm đập không hiển thị lẫn sang trạm cân chính và ngược lại.
- Dữ liệu khi đồng bộ lên Central DB vẫn gom chung được nhưng phải phân biệt rõ theo `StationCode`.

## 2. Nguyên tắc thiết kế

- `StationCode` là khóa phân vùng dữ liệu nghiệp vụ.
- Mọi dữ liệu nghiệp vụ phát sinh mới phải có `StationCode`.
- Mọi màn hình vận hành, dashboard, báo cáo, sync và stored procedure phải filter theo `StationCode` hiện tại.
- Không chỉ ẩn bằng UI. Phải chặn ở tầng query/service để tránh lọt dữ liệu.
- Master data chung như xe, khách hàng, sản phẩm vẫn dùng chung trong phase đầu. Chỉ tách master data theo trạm nếu có yêu cầu nghiệp vụ riêng.
- ERP bắt buộc truyền `@StationCode` rõ ràng khi đẩy dữ liệu xuống cân hoặc gọi các stored procedure nghiệp vụ. Không suy luận trạm bằng sản phẩm/kho/loại hàng trong luồng chạy chính.

## 3. Phạm vi phase 1

In scope:

- Thêm `StationCode` vào các bảng nghiệp vụ local.
- Backfill dữ liệu hiện có theo `station_code` trong `app_config`, fallback `QN01`.
- App tự gán `StationCode` khi tạo dữ liệu mới.
- Màn hình/query chỉ hiển thị dữ liệu theo `StationCode` hiện tại.
- Stored procedure ERP nhận và xử lý `@StationCode` bắt buộc.
- Sync payload gửi kèm `StationCode`.
- Central API/DB lưu `StationCode`.
- Dashboard và báo cáo tính theo `StationCode`.

Out of scope phase 1:

- Chưa tách database riêng cho từng trạm.
- Chưa tách master data theo trạm.
- Chưa làm màn quản trị tổng hợp đa trạm trên Central.
- Chưa phân quyền user theo nhiều trạm. Phase đầu mỗi app/máy dùng một `station_code` cấu hình local.

## 4. Cấu hình trạm

App config hiện có:

```text
station_code = QN01
```

Ví dụ cho trạm đập:

```text
station_code = DAP01
```

Nếu cần tối ưu giao diện cho từng trạm, dùng feature flag rõ ràng trong `app_config`, ví dụ:

| Key | Mặc định | Ý nghĩa |
|---|---:|---|
| `show_menu_incoming_vehicle_list` | `true` | Hiển thị màn Danh sách xe vào. |
| `show_menu_weighing` | `true` | Hiển thị màn Cân nội địa. |
| `show_menu_export_weighing` | `true` | Hiển thị màn Cân xuất khẩu. |
| `show_menu_outgoing_vehicle_list` | `true` | Hiển thị màn Danh sách xe ra. |
| `show_menu_export_report` | `true` | Hiển thị Báo cáo xuất. |
| `show_menu_inbound_report` | `true` | Hiển thị Báo cáo nhập. |
| `show_dashboard_inbound_kpi` | `true` | Hiển thị nhóm KPI nhập hàng. |
| `show_dashboard_outbound_kpi` | `true` | Hiển thị nhóm KPI xuất hàng. |
| `default_navigation_target` | `Dashboard` | Màn mặc định sau đăng nhập. |

Feature flags chỉ quyết định hiển thị giao diện. Tách dữ liệu vẫn bắt buộc dùng `StationCode` ở schema/query/service.

## 5. Schema local

Thêm `StationCode nvarchar(50) NOT NULL` vào các bảng:

| Bảng | Lý do |
|---|---|
| `cut_orders` | Cắt lệnh/đăng ký phương tiện thuộc một trạm. |
| `weighing_sessions` | Lượt cân thuộc một trạm. |
| `weighing_session_lines` | Line phân bổ thuộc trạm theo lượt cân/cắt lệnh. |
| `weigh_tickets` | Phiếu cân thuộc trạm. |
| `delivery_tickets` | Phiếu giao nhận thuộc trạm. |
| `weighing_session_images` | Ảnh cân thuộc trạm. |
| `sync_outbox` | Hàng đợi sync phải biết bản ghi của trạm nào. |

Backfill khi deploy:

```sql
DECLARE @StationCode nvarchar(50);

SELECT @StationCode = NULLIF(LTRIM(RTRIM(ConfigValue)), N'')
FROM dbo.app_config
WHERE ConfigKey = N'station_code';

IF @StationCode IS NULL
    SET @StationCode = N'QN01';

UPDATE dbo.cut_orders
SET StationCode = @StationCode
WHERE StationCode IS NULL OR LTRIM(RTRIM(StationCode)) = N'';
```

Các bảng còn lại backfill tương tự. Với bảng con như `weighing_session_lines`, `weigh_tickets`, `delivery_tickets`, `weighing_session_images`, ưu tiên copy `StationCode` từ session/cut order liên quan, sau đó mới fallback theo app config.

## 6. Index khuyến nghị

```sql
CREATE INDEX IX_cut_orders_station_stage_status
ON dbo.cut_orders(StationCode, ProcessingStage, CutOrderStatus, IsDeleted);

CREATE INDEX IX_weighing_sessions_station_status_time
ON dbo.weighing_sessions(StationCode, SessionStatus, Weight2Time, CreatedAt);

CREATE INDEX IX_weigh_tickets_station_ticket_no
ON dbo.weigh_tickets(StationCode, TicketNo);

CREATE INDEX IX_delivery_tickets_station_delivery_no
ON dbo.delivery_tickets(StationCode, DeliveryNo);

CREATE INDEX IX_sync_outbox_station_status_next_retry
ON dbo.sync_outbox(StationCode, Status, NextRetryAt);
```

Nếu một mã ERP có thể xuất hiện ở nhiều trạm, các unique/index theo mã ERP phải đổi sang dạng có `StationCode`, ví dụ `(StationCode, ErpCutOrderId, IsDeleted)`.

## 7. Gán StationCode khi tạo dữ liệu

Quy tắc:

| Luồng | Quy tắc |
|---|---|
| Tạo cắt lệnh thủ công | `StationCode = currentStationCode`. |
| ERP upsert cắt lệnh | `StationCode = @StationCode`; thiếu hoặc rỗng thì reject. |
| Tạo weighing session | Lấy theo current station hoặc theo cắt lệnh cùng trạm. |
| Tạo weighing session line | Copy từ session/cut order. |
| Tạo weigh ticket | Copy từ session/cut order. |
| Tạo delivery ticket | Copy từ session/cut order. |
| Chụp ảnh cân | Copy từ session. |
| Enqueue sync outbox | Copy từ aggregate; payload cũng phải có `stationCode`. |

## 8. Filter dữ liệu theo trạm

Tất cả màn hình/query sau phải filter `StationCode = currentStationCode`:

- Trang chủ/Dashboard.
- Danh sách xe vào.
- Cân nội địa.
- Cân xuất khẩu.
- Danh sách xe ra.
- Báo cáo xuất.
- Báo cáo nhập.
- Cấu hình đồng bộ.
- Lịch sử ảnh cân.
- Các modal chọn cắt lệnh/chuyến xe.

Repository/query cần filter ở tầng data access, không chỉ filter ở ViewModel.

## 9. ERP SQL contract

ERP phải truyền `@StationCode` khi gọi các procedure/function sau:

```sql
EXEC dbo.sp_UpsertCutOrderFromErp
    @StationCode = N'QN01',
    @ErpCutOrderId = ...,
    ...
```

```sql
EXEC dbo.sp_UpdateCutOrderErpExtras
    @StationCode = N'QN01',
    @LotNo = ...,
    @SealNo = ...,
    @LoadingPlace = ...,
    @UpdatedAt = ...,
    @UpdatedBy = ...;
```

```sql
EXEC dbo.sp_MarkCutOrderErpExportCompleted
    @StationCode = N'QN01',
    @ErpCutOrderId = ...,
    @IsCompleted = 1,
    @UpdatedAt = ...,
    @UpdatedBy = ...;
```

```sql
EXEC dbo.sp_SoftDeleteCutOrderDocumentsForReissue
    @ErpCutOrderId = ...,
    @StationCode = N'QN01';
```

```sql
EXEC dbo.sp_GetCutOrderNetWeight
    @ErpCutOrderId = ...,
    @StationCode = N'QN01';
```

```sql
SELECT *
FROM dbo.fn_GetCutOrderNetWeight(@ErpCutOrderId, @StationCode);
```

Nếu `@StationCode` rỗng hoặc không truyền, procedure phải throw lỗi để tránh cập nhật nhầm trạm.

## 10. Sync lên Central

Payload nghiệp vụ phải có `stationCode`:

- `CutOrder`
- `WeighTicket`
- `DeliveryTicket`
- `WeighingSession`
- `WeighingSessionLine`
- `WeighingSessionImage`

Central API:

- Validate `stationCode` bắt buộc với payload nghiệp vụ.
- Lưu `StationCode` vào bảng nghiệp vụ central.
- Lưu `StationCode` vào `sync_ingestion_logs`.
- Không dùng `sync_outbox` local như business table trên central.

## 11. Kiểm thử bắt buộc

1. Cấu hình app trạm chính `station_code = QN01`, tạo cắt lệnh/lượt cân và kiểm tra chỉ QN01 thấy dữ liệu.
2. Cấu hình app trạm đập `station_code = DAP01`, tạo lượt cân nhập và kiểm tra QN01 không thấy dữ liệu DAP01.
3. ERP gọi upsert với `@StationCode = DAP01`, app DAP01 thấy dữ liệu, app QN01 không thấy.
4. ERP gọi procedure thiếu `@StationCode`, procedure phải báo lỗi.
5. Dashboard, báo cáo xuất/nhập, màn sync chỉ tính dữ liệu của trạm hiện tại.
6. Sync lên Central thành công và central lưu đúng `StationCode`.
7. Dữ liệu cũ sau deploy được backfill đúng `StationCode` hiện tại.

## 12. Liên quan plan trạm đập

Plan này chỉ giải quyết phân vùng dữ liệu theo trạm. Sau khi hoàn tất, chức năng cân trạm đập sẽ dùng cùng `StationCode = DAP01` và triển khai riêng các nghiệp vụ:

- Quản lý xe nội bộ theo số xe, không phụ thuộc biển số đăng kiểm.
- Lưu trọng lượng xe chuẩn.
- Cho phép cân một lần để tính KL hàng = cân lần 1 - trọng lượng xe chuẩn.
- Vẫn hỗ trợ cân hai lần như luồng nhập hàng hiện tại khi cần.

Chi tiết nghiệp vụ trạm đập được tách sang `docs/PLAN-crusher-station-limestone-inbound-weighing.md`.
