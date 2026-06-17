# Plan: Gắn tài khoản với trạm và chọn trạm khi vận hành

## 1. Mục tiêu

Mở rộng cơ chế `StationCode` hiện tại từ mức cấu hình máy/app sang mức phiên đăng nhập, để cùng một app/DB có thể phục vụ nhiều trạm mà không bị thao tác nhầm dữ liệu.

Mục tiêu nghiệp vụ:

- Một user chỉ được xem và thao tác dữ liệu của các trạm được phân quyền.
- User thuộc một trạm thì vào app tự dùng trạm đó.
- Admin hoặc user được cấp nhiều trạm có thể chọn trạm làm việc sau đăng nhập.
- Mọi dữ liệu phát sinh phải ghi đúng `StationCode` của phiên làm việc hiện tại.
- ERP vẫn truyền `@StationCode` rõ ràng khi gọi stored procedure, không phụ thuộc user.

## 2. Nguyên tắc thiết kế

- `StationCode` vẫn là khóa phân vùng dữ liệu nghiệp vụ.
- `app_config.station_code` chỉ còn là trạm mặc định của máy hoặc fallback khi chưa bật user-station scope.
- Khi đã bật user-station scope, mọi query/use case phải lấy `StationCode` từ `CurrentUserContext` hoặc `CurrentStationContext`, không lấy trực tiếp từ `app_config`.
- Không chỉ ẩn menu theo UI. Phải enforce ở tầng repository/service.
- Không cho user thao tác nếu chưa chọn trạm hoặc không có quyền với trạm.
- Admin nhiều trạm khi đổi trạm phải refresh toàn bộ màn hình/query, không giữ selection cũ.

## 3. Phạm vi

### In scope

- Thêm danh mục trạm.
- Thêm bảng gắn user với trạm.
- Bổ sung context trạm hiện tại trong phiên đăng nhập.
- Thêm màn/quy trình chọn trạm cho user có nhiều trạm.
- Cập nhật repository/service để lấy station từ session context.
- Cập nhật tạo dữ liệu/sync/outbox/report/dashboard theo station của phiên hiện tại.
- Bổ sung kiểm tra phân quyền trạm khi mở màn và khi thao tác.

### Out of scope

- Không tách database theo trạm.
- Không thay đổi Central API thành hệ thống quản trị đa trạm hoàn chỉnh.
- Không thay thế `@StationCode` trong stored procedure ERP.
- Không tự động suy luận trạm theo sản phẩm/kho.

## 4. Schema đề xuất

### 4.1 Bảng `stations`

```sql
CREATE TABLE dbo.stations
(
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    StationCode nvarchar(50) NOT NULL,
    StationName nvarchar(255) NOT NULL,
    IsActive bit NOT NULL CONSTRAINT DF_stations_is_active DEFAULT (1),
    SortOrder int NOT NULL CONSTRAINT DF_stations_sort_order DEFAULT (0),
    CreatedAt datetime2(7) NOT NULL,
    CreatedBy nvarchar(100) NULL,
    UpdatedAt datetime2(7) NULL,
    UpdatedBy nvarchar(100) NULL
);

CREATE UNIQUE INDEX UX_stations_station_code
ON dbo.stations(StationCode);
```

Ví dụ:

| StationCode | StationName |
|---|---|
| `QN01` | Trạm cân chính |
| `DAP01` | Trạm đập |

### 4.2 Bảng `user_station_assignments`

```sql
CREATE TABLE dbo.user_station_assignments
(
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    UserId uniqueidentifier NOT NULL,
    StationCode nvarchar(50) NOT NULL,
    IsDefault bit NOT NULL CONSTRAINT DF_user_station_default DEFAULT (0),
    IsActive bit NOT NULL CONSTRAINT DF_user_station_active DEFAULT (1),
    CreatedAt datetime2(7) NOT NULL,
    CreatedBy nvarchar(100) NULL,
    UpdatedAt datetime2(7) NULL,
    UpdatedBy nvarchar(100) NULL,
    CONSTRAINT FK_user_station_assignments_users
        FOREIGN KEY (UserId) REFERENCES dbo.users(Id)
);

CREATE INDEX IX_user_station_assignments_user_active
ON dbo.user_station_assignments(UserId, IsActive);

CREATE UNIQUE INDEX UX_user_station_assignments_user_station
ON dbo.user_station_assignments(UserId, StationCode);
```

Không cần thêm `StationCode` trực tiếp vào `users` nếu muốn hỗ trợ một user nhiều trạm. Nếu muốn đơn giản hóa, có thể thêm `DefaultStationCode` vào `users`, nhưng vẫn nên giữ bảng assignment.

### 4.3 Bảng `station_feature_flags`

Dùng để cấu hình trạm nào được hiển thị chức năng nào. Không nên lưu dạng global trong `app_config` nếu cùng một app/DB phục vụ nhiều trạm.

```sql
CREATE TABLE dbo.station_feature_flags
(
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    StationCode nvarchar(50) NOT NULL,
    FeatureKey nvarchar(100) NOT NULL,
    FeatureValue nvarchar(50) NOT NULL,
    CreatedAt datetime2(7) NOT NULL,
    CreatedBy nvarchar(100) NULL,
    UpdatedAt datetime2(7) NULL,
    UpdatedBy nvarchar(100) NULL
);

CREATE UNIQUE INDEX UX_station_feature_flags_station_key
ON dbo.station_feature_flags(StationCode, FeatureKey);
```

Nhóm feature key đề xuất:

| FeatureKey | Mặc định | Ý nghĩa |
|---|---:|---|
| `show_menu_dashboard` | `true` | Hiển thị Trang chủ. |
| `show_menu_incoming_vehicle_list` | `true` | Hiển thị Danh sách xe vào. |
| `show_menu_weighing` | `true` | Hiển thị Cân nội địa. |
| `show_menu_export_weighing` | `true` | Hiển thị Cân xuất khẩu. |
| `show_menu_outgoing_vehicle_list` | `true` | Hiển thị Danh sách xe ra. |
| `show_menu_export_report` | `true` | Hiển thị Báo cáo xuất. |
| `show_menu_inbound_report` | `true` | Hiển thị Báo cáo nhập. |
| `show_dashboard_inbound_kpi` | `true` | Hiển thị KPI nhập hàng. |
| `show_dashboard_outbound_kpi` | `true` | Hiển thị KPI xuất hàng. |
| `default_navigation_target` | `Dashboard` | Màn mặc định sau khi chọn trạm. |

Ví dụ cho trạm đập:

```text
StationCode = DAP01
show_menu_dashboard = true
show_menu_incoming_vehicle_list = true
show_menu_weighing = true
show_menu_export_weighing = false
show_menu_outgoing_vehicle_list = true
show_menu_export_report = false
show_menu_inbound_report = true
show_dashboard_inbound_kpi = true
show_dashboard_outbound_kpi = false
default_navigation_target = Weighing
```

Lưu ý:

- Feature flag chỉ điều khiển hiển thị và điều hướng.
- Quyền dữ liệu vẫn phải enforce bằng `StationCode` và user-station assignment.
- Nếu một chức năng bị tắt cho trạm, user không được điều hướng trực tiếp vào màn đó bằng code/menu cũ.

## 5. Cấu hình app

Thêm key:

```text
enable_user_station_scope = true
default_station_code = QN01
```

Ý nghĩa:

- `enable_user_station_scope = false`: app dùng cơ chế hiện tại, lấy `station_code` từ `app_config`.
- `enable_user_station_scope = true`: app lấy station từ user session.
- `default_station_code`: fallback cho dữ liệu cũ, admin setup ban đầu, hoặc máy chỉ có một trạm.

Giữ tương thích:

- `station_code` vẫn giữ để không phá các máy đang chạy.
- Sau khi nâng cấp ổn định, có thể chuẩn hóa thành `default_station_code`.

## 6. Luồng đăng nhập và chọn trạm

### 6.1 User chỉ có một trạm

1. User đăng nhập.
2. App load danh sách trạm active của user.
3. Nếu chỉ có một trạm, set `CurrentStationCode` bằng trạm đó.
4. Điều hướng vào màn mặc định.

### 6.2 User có nhiều trạm

1. User đăng nhập.
2. App load danh sách trạm active của user.
3. Hiển thị modal chọn trạm.
4. User chọn trạm.
5. App set `CurrentStationCode`.
6. Load dashboard/menu theo trạm đã chọn.

### 6.3 User không có trạm

1. User đăng nhập thành công.
2. App phát hiện không có station assignment.
3. Không cho vào màn nghiệp vụ.
4. Hiển thị thông báo: `Tài khoản chưa được phân quyền trạm cân. Vui lòng liên hệ quản trị viên.`

### 6.4 Đổi trạm khi đang đăng nhập

Nếu user có nhiều trạm:

- Header hiển thị trạm hiện tại.
- Cho phép đổi trạm từ dropdown hoặc menu user.
- Khi đổi trạm:
  - Clear selection/current screen state.
  - Reload dashboard và màn hiện tại.
  - Không giữ cắt lệnh/lượt cân đã chọn ở trạm cũ.

## 7. Application interfaces

### 7.1 `ICurrentStationContext`

```csharp
public interface ICurrentStationContext
{
    string? StationCode { get; }
    string? StationName { get; }
    bool HasStation { get; }
    void SetStation(string stationCode, string stationName);
    void Clear();
}
```

### 7.2 `IStationAuthorizationService`

```csharp
public interface IStationAuthorizationService
{
    Task<IReadOnlyList<StationOption>> GetAllowedStationsAsync(Guid userId, CancellationToken ct);
    Task<bool> CanAccessStationAsync(Guid userId, string stationCode, CancellationToken ct);
    Task EnsureCanAccessStationAsync(Guid userId, string stationCode, CancellationToken ct);
}
```

### 7.3 `IStationScope`

Chuẩn hóa service lấy station hiện tại:

```csharp
public interface IStationScope
{
    Task<string> GetCurrentStationCodeAsync(CancellationToken ct);
}
```

Thứ tự resolve:

1. Nếu `enable_user_station_scope = true`: lấy từ `ICurrentStationContext`.
2. Nếu chưa có context: throw lỗi nghiệp vụ.
3. Nếu `enable_user_station_scope = false`: lấy `app_config.station_code`.
4. Fallback cuối cùng: `QN01` chỉ dùng cho migration/backfill, không dùng cho thao tác nghiệp vụ mới khi đã bật user-station scope.

### 7.4 `IStationFeatureService`

```csharp
public interface IStationFeatureService
{
    Task<StationFeatureSet> GetFeaturesAsync(string stationCode, CancellationToken ct);
    Task<bool> IsEnabledAsync(string stationCode, string featureKey, CancellationToken ct);
}
```

`StationFeatureSet` chứa các cờ hiển thị menu/KPI và `DefaultNavigationTarget`.

Quy tắc:

1. Đọc từ `station_feature_flags` theo `StationCode`.
2. Nếu thiếu key thì dùng default an toàn.
3. Cache ngắn hạn trong session để tránh query liên tục.
4. Khi admin lưu cấu hình feature, màn hiện tại cần reload menu nếu đang ở đúng trạm đó.

## 8. Tác động tới code hiện tại

### 8.1 Repository

Hiện tại nhiều repository đã đọc current station từ `app_config`. Cần chuyển sang dùng `IStationScope`.

Các nhóm cần đổi:

- `CutOrderRepository`
- `WeighingSessionRepository`
- `TicketRepository`
- `DeliveryTicketRepository`
- `WeighingSessionImageRepository`
- `SyncOutboxRepository`
- Các service báo cáo/dashboard đang tự đọc `app_config.station_code`

Mục tiêu:

- Không repository nào tự đọc trực tiếp `app_config.station_code`.
- Repository chỉ hỏi `IStationScope.GetCurrentStationCodeAsync()`.

### 8.2 DbContext SaveChanges

Hiện tại `StationDbContext` tự gán `StationCode` khi thêm entity mới. Khi bật user-station scope:

- Không nên để DbContext tự resolve từ DB config nữa.
- Nên chuyển logic gán station sang repository/use case hoặc dùng service scope rõ ràng.
- Nếu vẫn giữ trong DbContext, cần inject/access `IStationScope` cẩn thận để tránh dependency vòng.

Khuyến nghị:

- Phase đầu giữ DbContext fallback để chống null.
- Sau đó chuyển dần sang repository/use case set rõ `StationCode`.

### 8.3 UI

Cần cập nhật:

- Login flow.
- Header hiển thị trạm hiện tại.
- Modal chọn trạm.
- Menu đọc `IStationFeatureService` theo trạm hiện tại để ẩn/hiện chức năng.
- Dashboard đọc feature để ẩn/hiện nhóm KPI nhập/xuất.
- Màn quản lý tài khoản: gán trạm cho user.
- Màn quản lý trạm: cấu hình các feature được bật cho từng trạm.
- Nếu user đổi trạm, toàn bộ màn hình reload theo trạm mới.

## 9. Màn quản trị trạm và phân quyền trạm

### 9.1 Màn danh mục trạm

Chỉ admin xem/sửa.

Cột:

- Mã trạm
- Tên trạm
- Trạng thái
- Thứ tự hiển thị

Chức năng:

- Thêm/sửa/khóa trạm.
- Không cho xóa trạm đã có dữ liệu nghiệp vụ.

### 9.2 Màn gán trạm cho tài khoản

Tích hợp vào màn Quản lý tài khoản.

Thông tin:

- Danh sách trạm được phép thao tác.
- Trạm mặc định.
- Trạng thái active/inactive.

Validation:

- Một user có tối đa một trạm mặc định.
- User vận hành cân phải có ít nhất một trạm.
- Không cho bỏ hết trạm của chính admin đang đăng nhập nếu đó là admin cuối cùng.

### 9.3 Màn cấu hình chức năng theo trạm

Chỉ admin xem/sửa.

Vị trí đề xuất:

- `Cấu hình hệ thống` -> `Danh mục trạm`
- Trong chi tiết trạm có tab `Chức năng hiển thị`

Trường hiển thị:

- Mã trạm
- Tên trạm
- Danh sách checkbox feature:
  - Trang chủ
  - Danh sách xe vào
  - Cân nội địa
  - Cân xuất khẩu
  - Danh sách xe ra
  - Báo cáo xuất
  - Báo cáo nhập
  - KPI nhập hàng
  - KPI xuất hàng
- Màn mặc định sau đăng nhập/chọn trạm

Validation:

- Mỗi trạm phải có ít nhất một màn vận hành được bật.
- `default_navigation_target` phải là một màn đang được bật.
- Không cho tắt toàn bộ menu admin nếu tài khoản hiện tại cần quản trị hệ thống.

## 10. Stored procedure ERP

Không thay đổi hướng đã chốt:

- ERP bắt buộc truyền `@StationCode`.
- Procedure reject nếu thiếu `@StationCode`.
- User-station scope chỉ áp dụng cho thao tác trong app, không áp dụng cho ERP vì ERP không đăng nhập qua app.

Các procedure đã/đang cần contract station:

- `sp_UpsertCutOrderFromErp`
- `sp_UpdateCutOrderErpExtras`
- `sp_MarkCutOrderErpExportCompleted`
- `sp_SoftDeleteCutOrderDocumentsForReissue`
- `sp_GetCutOrderNetWeight`
- `fn_GetCutOrderNetWeight`

## 11. Sync và Central API

Không thay đổi cơ chế sync chính:

- Local outbox vẫn lưu `StationCode`.
- Payload nghiệp vụ vẫn có `stationCode`.
- Central API vẫn validate và lưu `StationCode`.

Cần bổ sung:

- Nếu admin đổi trạm, SyncInfo chỉ hiển thị outbox của trạm đang chọn.
- Worker sync có thể chạy theo `app_config.station_code` nếu mỗi app/máy phục vụ một trạm.
- Nếu cùng app/DB phục vụ nhiều trạm và muốn sync toàn bộ, worker không nên filter theo station hiện tại của user vì worker không phụ thuộc phiên đăng nhập.

Khuyến nghị thực tế:

- Outbox worker nên sync tất cả trạm trong DB local, hoặc có cấu hình `sync_station_scope = current_machine/all`.
- Màn Cấu hình đồng bộ vẫn filter theo trạm đang chọn để user dễ theo dõi.

## 12. Rủi ro cần xử lý

| Rủi ro | Cách xử lý |
|---|---|
| User đổi trạm nhưng màn còn giữ dữ liệu trạm cũ | Clear selection và reload toàn bộ screen khi đổi trạm. |
| Background worker dùng station của user đăng nhập cuối cùng | Worker không được phụ thuộc `CurrentUserContext`; dùng cấu hình riêng hoặc sync all station. |
| ERP quên truyền `@StationCode` | Procedure throw lỗi bắt buộc. |
| Dữ liệu cũ thiếu `StationCode` | Backfill trước khi bật user-station scope. |
| Admin chưa gán trạm cho user | Login không vào nghiệp vụ và báo rõ lỗi. |

## 13. Thứ tự triển khai

### Phase A: Schema và seed

1. Thêm entity/table `stations`.
2. Thêm entity/table `user_station_assignments`.
3. Thêm entity/table `station_feature_flags`.
4. Seed trạm từ `app_config.station_code` hiện tại.
5. Seed feature mặc định cho trạm hiện tại.
6. Gán user admin hiện có vào trạm hiện tại.

### Phase B: Station context

1. Thêm `ICurrentStationContext`.
2. Thêm `IStationScope`.
3. Thêm `IStationAuthorizationService`.
4. Cập nhật login flow để set station.

### Phase C: UI

1. Thêm modal chọn trạm.
2. Header hiển thị trạm hiện tại.
3. Cho phép đổi trạm nếu user có nhiều trạm.
4. Thêm quản lý trạm và gán trạm trong màn tài khoản.
5. Thêm cấu hình chức năng hiển thị theo trạm.
6. Menu và dashboard đọc feature theo trạm hiện tại.

### Phase D: Data access

1. Chuyển repository/service từ đọc `app_config.station_code` sang `IStationScope`.
2. Đảm bảo tạo dữ liệu mới lấy đúng station của phiên hiện tại.
3. Đảm bảo dashboard/report/sync info filter đúng.

### Phase E: Worker và sync

1. Quyết định worker sync all station hoặc theo machine station.
2. Không để worker phụ thuộc user session.
3. Test sync dữ liệu nhiều trạm lên Central.

### Phase F: Kiểm thử nghiệp vụ

1. User QN01 chỉ thấy QN01.
2. User DAP01 chỉ thấy DAP01.
3. Admin nhiều trạm đổi qua lại, dữ liệu hiển thị đúng.
4. Tạo cắt lệnh/lượt cân ở từng trạm, DB ghi đúng `StationCode`.
5. ERP truyền `@StationCode`, app đúng trạm nhận dữ liệu.
6. Report/dashboard/sync info theo đúng trạm đang chọn.
7. Feature trạm `DAP01` tắt Cân xuất khẩu/Báo cáo xuất thì menu không hiển thị và không điều hướng được vào màn đó.
8. Đổi trạm thì menu, dashboard và màn mặc định thay đổi đúng theo feature của trạm mới.

## 14. Điều kiện hoàn thành

Chức năng được coi là hoàn thành khi:

- Không có thao tác nghiệp vụ nào tạo dữ liệu với `StationCode` rỗng.
- User không thể xem/sửa dữ liệu của trạm chưa được phân quyền.
- Admin có thể chọn/đổi trạm và dữ liệu reload đúng.
- ERP contract bắt buộc `@StationCode` hoạt động.
- Sync và Central DB vẫn lưu đúng `StationCode`.
- Build pass và có test tối thiểu cho phân quyền station scope.
