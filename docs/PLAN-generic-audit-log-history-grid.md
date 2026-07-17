# PLAN - Nâng cấp grid Lịch sử chỉnh sửa thành lịch sử audit tổng quát

## 1. Mục tiêu

Màn **Lịch sử chỉnh sửa** hiện đang trình bày theo case **Đổi số xe** với các cột cố định như `Số xe cũ`, `Số xe mới`, `TL bì cũ`, `TL bì mới`, `TL hàng cũ`, `TL hàng mới`. Cách này không phù hợp khi audit log là:

- Sửa cắt lệnh tạm xuất khẩu.
- Sửa tàu mỏ sét.
- Đánh dấu/bỏ đánh dấu hàng hoàn.
- Chuyển chuyến xe.
- Sửa số mooc, số seal.
- Tạo/sửa tài khoản.
- Cấu hình trạm, cấu hình vận hành.
- Các audit log khác có payload không theo form đổi số xe.

Mục tiêu nâng cấp:

1. Grid phản ánh được **giá trị cũ / giá trị mới** cho mọi audit log có thông tin thay đổi.
2. Không còn ép mọi log vào các cột riêng của nghiệp vụ đổi số xe.
3. Vẫn xem dễ với các log không có cặp cũ/mới, bằng cách hiển thị payload/tóm tắt rõ ràng.
4. Không đổi schema DB nếu chưa cần; ưu tiên parse `DetailJson` hiện có.
5. Giữ lọc theo trạm, thời gian, số xe/lượt cân nếu có.

## 2. Rà soát hiện trạng code

### 2.1 Màn UI hiện tại

File:

- `src/StationApp.UI/Views/WeighingSessionEditHistoryView.xaml`
- `src/StationApp.UI/ViewModels/WeighingSessionEditHistoryViewModel.cs`

Grid hiện có các cột:

- `STT`
- `Thời gian sửa`
- `Người sửa`
- `Số lượt cân`
- `Số xe cũ`
- `Số xe mới`
- `TL tổng (kg)`
- `TL bì cũ (kg)`
- `TL bì mới (kg)`
- `TL hàng cũ (kg)`
- `TL hàng mới (kg)`
- `Ghi chú audit`
- `Lý do sửa đổi`

Nhận xét:

- Các cột này hợp với `EDIT_WEIGHING_SESSION`.
- Khi log là `UPDATE_TEMPORARY_EXPORT_CUT_ORDER`, `UPDATE_CLAY_VESSEL`, `TOGGLE_CRUSHER_RETURNED_BROKEN_TRIP`, ViewModel đang “nhét tạm” dữ liệu customer/vessel/net weight vào các cột số xe/TL hàng, gây hiểu nhầm.
- Nhiều chuỗi trong file đang bị mojibake, cần sửa luôn khi đụng vào.

### 2.2 Query audit log hiện tại

File:

- `src/StationApp.Application/Interfaces/IAuditLogRepository.cs`
- `src/StationApp.Infrastructure/Repositories/OtherRepositories.cs`

Method hiện tại:

```csharp
SearchEditLogsAsync(string? vehiclePlate, string? sessionNo, DateTime fromDate, DateTime toDate, string? stationCode, CancellationToken ct)
```

Logic hiện tại chỉ lấy action theo trạm:

- `QN01`: `TRANSFER_EXPORT_TRIP`, `UPDATE_TEMPORARY_EXPORT_CUT_ORDER`
- Trạm khác: `EDIT_WEIGHING_SESSION`, `TOGGLE_CRUSHER_RETURNED_BROKEN_TRIP`, `UPDATE_CLAY_VESSEL`

Nhận xét:

- Chưa phải lịch sử audit tổng quát.
- Các audit log như sửa mooc/seal, account, station settings, app/config, master data nếu có ghi audit sẽ không hiện.
- Nếu muốn màn này là audit log tổng quát cho Manager/Admin, cần query theo station/date và tùy chọn action/entity thay vì whitelist cứng theo trạm.

### 2.3 Dạng payload audit đang tồn tại

#### Dạng `Changes`

Ví dụ `EDIT_WEIGHING_SESSION` ở:

- `CrusherWeighingUseCases.cs`
- `ClayWeighingUseCases.cs`

Payload:

```json
{
  "Reason": "...",
  "InvalidatedOldVehicleStandardTare": {...},
  "AppliedStandardTareToNewVehicle": {...},
  "Changes": {
    "VehiclePlate": { "Old": "...", "New": "..." },
    "StandardTareWeightSnapshot": { "Old": 20000, "New": 14000 },
    "Weight2": { "Old": 20000, "New": 14000 },
    "NetWeight": { "Old": 38000, "New": 44000 }
  }
}
```

Đây là dạng tốt nhất để hiển thị cũ/mới.

#### Dạng `Old` / `New`

Ví dụ:

- `UPDATE_TEMPORARY_EXPORT_CUT_ORDER`
- `UPDATE_CLAY_VESSEL`

Payload:

```json
{
  "DisplayCode": "...",
  "Old": { "CustomerCode": "...", "ProductName": "...", ... },
  "New": { "CustomerCode": "...", "ProductName": "...", ... },
  "UpdatedLineCount": 3
}
```

Cần diff từng field trong `Old` và `New`.

#### Dạng payload mô tả một chiều

Ví dụ:

- `TRANSFER_EXPORT_TRIP`
- `TOGGLE_CRUSHER_RETURNED_BROKEN_TRIP`
- `UPDATE_WEIGHING_SESSION_MOOC_NO`
- `UPDATE_WEIGHING_SESSION_SEAL_NO`
- `CREATE_USER_ACCOUNT`
- `RESET_USER_PASSWORD`

Payload không phải lúc nào cũng có old/new. Với dạng này cần hiển thị:

- Trường nghiệp vụ chính.
- Giá trị cũ nếu có.
- Giá trị mới nếu có.
- Nếu không có old/new thì hiển thị ở `Chi tiết` / `Ghi chú`.

## 3. Thiết kế UI đề xuất

### 3.1 Đổi grid chính sang layout tổng quát

Thay grid hiện tại bằng các cột:

| Cột | Binding đề xuất | Ghi chú |
|---|---|---|
| STT | `Index` | Giữ nguyên |
| Thời gian | `CreatedAt` | `dd/MM/yyyy HH:mm:ss` |
| Người thao tác | `Actor` | Giữ nguyên |
| Hành động | `ActionDisplay` | Text tiếng Việt từ `Action` |
| Đối tượng | `EntityDisplay` | Ví dụ `Lượt cân LC26070018`, `Cắt lệnh TEMP...`, `Tàu 04` |
| Giá trị cũ | `OldValueDisplay` | Hiển thị nhiều dòng dạng `Tên trường: giá trị cũ` |
| Giá trị mới | `NewValueDisplay` | Hiển thị nhiều dòng dạng `Tên trường: giá trị mới` |
| Lý do/Ghi chú | `Note` | Reason, note, hoặc summary |
| Chi tiết | `DetailSummary` | Số dòng/chuyến bị ảnh hưởng, cap hoàn, chuyển từ/sang... |

Mỗi audit log hiển thị **một dòng grid**. Nếu log có nhiều trường thay đổi, cột `Giá trị cũ` và `Giá trị mới` sẽ hiển thị nhiều dòng `key: value` tương ứng.

Ví dụ `EDIT_WEIGHING_SESSION` đổi xe:

| Hành động | Đối tượng | Trường thay đổi | Giá trị cũ | Giá trị mới |
|---|---|---|---|---|
| Sửa lượt cân | LC26070018 |  | Số xe: 08<br>TL bì: 20,000 kg<br>TL hàng: 38,000 kg | Số xe: 04<br>TL bì: --<br>TL hàng: -- |

Ví dụ `UPDATE_CLAY_VESSEL`:

| Hành động | Đối tượng | Giá trị cũ | Giá trị mới |
|---|---|---|---|
| Sửa tàu mỏ sét | Tàu 04 | Khách hàng: vina<br>Sản phẩm: Sét | Khách hàng: Minh Long<br>Sản phẩm: Sỉ |

Ví dụ `TOGGLE_CRUSHER_RETURNED_BROKEN_TRIP` không có cặp old/new đầy đủ:

| Hành động | Đối tượng | Giá trị cũ | Giá trị mới |
|---|---|---|---|
| Đánh dấu hàng hoàn | LC26070055 | Trạng thái hàng hoàn: Không<br>TL hoàn thực cân: 49,500 kg | Trạng thái hàng hoàn: Có<br>TL hoàn ghi nhận: 49,000 kg |

### 3.2 Tuỳ chọn chi tiết payload

Có 2 hướng:

1. Giai đoạn 1: chỉ thêm cột `Chi tiết`, không làm modal.
2. Giai đoạn 2: thêm nút `Xem chi tiết` mở modal hiển thị JSON đã format.

Đề xuất làm theo giai đoạn 1 trước để nhanh, ít rủi ro.

### 3.3 Bộ lọc

Giữ:

- Từ ngày.
- Đến ngày.
- Biển số xe.
- Số lượt cân.

Thêm:

- Dropdown `Hành động`: mặc định `Tất cả`.
- Textbox `Từ khóa`: tìm trong `DetailJson`, `Action`, `EntityType`, `Actor`.

Nếu scope muốn tối giản cho bước đầu, có thể chưa thêm filter hành động mà chỉ sửa grid.

## 4. Thiết kế ViewModel/parser

### 4.0 Chuẩn hóa `DetailJson` cho audit log mới

Không thêm cột `OldValue` / `NewValue` vào DB ở bước này. Thay vào đó, cần chuẩn hóa cấu trúc `DetailJson` khi ghi audit log mới để màn hiển thị có thể parse ổn định.

Chuẩn đề xuất:

```json
{
  "Subject": {
    "Code": "LC26070018",
    "Name": "Lượt cân LC26070018",
    "VehiclePlate": "04"
  },
  "Reason": "Lý do người dùng nhập nếu có",
  "Changes": {
    "VehiclePlate": {
      "Old": "08",
      "New": "04"
    },
    "NetWeight": {
      "Old": 38000,
      "New": 34000,
      "Unit": "kg"
    }
  },
  "Summary": {
    "UpdatedSessionCount": 1,
    "UpdatedLineCount": 0
  },
  "Notes": [
    "Vô hiệu TL bì xe cũ 08: 20000 kg",
    "Áp TL bì cho xe mới 04: 20000 kg"
  ]
}
```

Quy tắc:

1. Mọi audit log chỉnh sửa dữ liệu nên có `Subject` để xác định đối tượng hiển thị.
2. Mọi thay đổi cũ/mới nên đưa vào `Changes`.
3. Mỗi field trong `Changes` dùng đúng cấu trúc `{ Old, New, Unit? }`.
4. Các số lượng phụ trợ như số dòng/số chuyến bị cập nhật đưa vào `Summary`.
5. Ghi chú nghiệp vụ đưa vào `Notes`.
6. Không đưa array Id dài lên grid; chỉ dùng cho debug nếu thật sự cần.

Parser vẫn phải tương thích ngược với dữ liệu cũ:

- Dữ liệu cũ dạng `Changes` vẫn đọc bình thường.
- Dữ liệu cũ dạng `Old` / `New` vẫn diff được.
- Dữ liệu cũ dạng `OldX` / `NewX` vẫn đọc được.
- Dữ liệu cũ không có cũ/mới thì hiển thị summary.

### 4.1 Row model mới

Tạo model thay thế `EditHistoryItemRow`, ví dụ:

```csharp
public sealed class AuditHistoryRow
{
    public int Index { get; set; }
    public Guid AuditLogId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Actor { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string ActionDisplay { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string EntityDisplay { get; set; } = string.Empty;
    public string OldValueDisplay { get; set; } = string.Empty;
    public string NewValueDisplay { get; set; } = string.Empty;
    public string Note { get; set; } = string.Empty;
    public string DetailSummary { get; set; } = string.Empty;
}
```

### 4.2 Parser service/helper

Tạo helper trong UI hoặc Application:

- Ưu tiên UI layer nếu chỉ phục vụ hiển thị.
- Nếu muốn test kỹ hơn, đặt ở Application dạng service thuần không phụ thuộc WPF.

Đề xuất:

- `src/StationApp.UI/Services/AuditLogDisplayMapper.cs`

Hàm chính:

```csharp
AuditHistoryRow Map(AuditLog log, AuditDisplayContext context)
```

Quy tắc parse:

1. Nếu có object `Changes`: mỗi property con có `{ Old, New }` được thêm vào 2 cột `Giá trị cũ` / `Giá trị mới` theo dạng `Tên trường: giá trị`.
2. Nếu có object `Old` và `New`: diff cùng tên property, chỉ thêm field có thay đổi vào 2 cột cũ/mới.
3. Nếu có các cặp kiểu `OldX` / `NewX`: map thành field `X`, rồi thêm vào 2 cột cũ/mới.
4. Nếu không có old/new: tạo một dòng summary, để `Giá trị mới` hoặc `Chi tiết` là tóm tắt payload.
5. Bỏ qua field kỹ thuật quá nhiễu nếu đã có summary riêng: `UpdatedLineCount`, `UpdatedSessionCount`, `UpdatedCutOrderIds`, `UpdatedWeighTicketIds`, các Id array dài.

### 4.3 Mapping tên field tiếng Việt

Cần dictionary:

| Field | Text hiển thị |
|---|---|
| `VehiclePlate` | Số xe |
| `InternalVehicleNo` | Số xe nội bộ |
| `StandardTareWeightSnapshot` | TL bì |
| `Weight1` | Cân lần 1 |
| `Weight2` | Cân lần 2 |
| `GrossWeight` | TL tổng |
| `NetWeight` | TL hàng |
| `OldNetWeight` / `NewNetWeight` | TL hàng |
| `CustomerCode` | Mã khách hàng |
| `CustomerName` | Khách hàng |
| `ProductCode` | Mã hàng |
| `ProductName` | Hàng hóa |
| `ProductType` | Loại hàng |
| `PlannedWeight` | SL kế hoạch |
| `BagCount` | Số bao |
| `TareWeightKg` | TL vỏ |
| `BagWeightKg` | TL bao |
| `ExportPackageType` | Loại xuất khẩu |
| `Notes` | Ghi chú |
| `SealNo` | Số seal |
| `MoocNumber` | Số mooc |
| `IsReturnedBrokenTrip` | Hàng hoàn |
| `ReturnedRecognizedWeight` | TL hoàn ghi nhận |
| `ActualReturnedWeight` | TL hoàn thực cân |

### 4.4 Format giá trị

Quy tắc format:

- Weight field: `N0 kg`, riêng các trường báo cáo tấn nếu payload là tấn thì `N3 tấn`.
- DateTime: `dd/MM/yyyy HH:mm:ss`.
- Bool:
  - `true`/`false` với field hàng hoàn: `Có` / `Không`.
  - Active status: `Đang hoạt động` / `Ngừng hoạt động`.
- Null/empty: `--`.
- Array Id dài: không hiển thị full ở grid, đưa vào `DetailSummary` dạng `Đã cập nhật 3 dòng`.

### 4.5 Mapping action tiếng Việt

| Action | Text |
|---|---|
| `EDIT_WEIGHING_SESSION` | Sửa lượt cân |
| `TRANSFER_EXPORT_TRIP` | Chuyển chuyến xe xuất khẩu |
| `UPDATE_TEMPORARY_EXPORT_CUT_ORDER` | Sửa cắt lệnh tạm xuất khẩu |
| `UPDATE_CLAY_VESSEL` | Sửa tàu mỏ sét |
| `TOGGLE_CRUSHER_RETURNED_BROKEN_TRIP` | Cập nhật hàng hoàn |
| `UPDATE_WEIGHING_SESSION_MOOC_NO` | Cập nhật số mooc |
| `UPDATE_WEIGHING_SESSION_SEAL_NO` | Cập nhật số seal |
| `CREATE_USER_ACCOUNT` | Tạo tài khoản |
| `UPDATE_USER_ACCOUNT` | Sửa tài khoản |
| `SET_USER_ACTIVE_STATUS` | Cập nhật trạng thái tài khoản |
| `RESET_USER_PASSWORD` | Reset mật khẩu |
| action chưa map | Hiển thị nguyên mã action |

## 5. Thiết kế repository/query

### 5.1 Vấn đề hiện tại

`SearchEditLogsAsync` đang whitelist action theo trạm, nên không thể là audit log tổng quát.

### 5.2 Đề xuất nâng cấp

Tạo request model:

```csharp
public sealed record AuditLogSearchRequest(
    DateTime FromDate,
    DateTime ToDate,
    string? StationCode,
    string? VehiclePlate,
    string? SessionNo,
    string? Action,
    string? Keyword);
```

Thêm method:

```csharp
Task<IReadOnlyList<AuditLog>> SearchAsync(AuditLogSearchRequest request, CancellationToken ct);
```

Logic:

- Lọc theo `CreatedAt`.
- Lọc theo `StationCode`.
- Nếu có `Action`, lọc action.
- Nếu có `VehiclePlate`, `SessionNo`, `Keyword`, lọc trên `DetailJson`/`Actor`/`Action`/`EntityType`.
- Không whitelist action theo trạm nữa cho màn audit tổng quát.

Giữ `SearchEditLogsAsync` tạm thời nếu còn màn khác đang dùng, hoặc chuyển màn hiện tại sang method mới rồi đánh dấu method cũ là legacy.

## 6. Implementation plan

### Phase 1: Tách model hiển thị tổng quát

#### Task 1: Tạo row model audit tổng quát

**Mô tả:** Thay `EditHistoryItemRow` chuyên cho đổi số xe bằng row model tổng quát phản ánh một audit log trên một dòng.

**Acceptance criteria:**

- Có row model chứa `ActionDisplay`, `EntityDisplay`, `OldValueDisplay`, `NewValueDisplay`, `Note`, `DetailSummary`.
- `OldValueDisplay` và `NewValueDisplay` hỗ trợ hiển thị nhiều dòng `Tên trường: giá trị`.
- ViewModel không còn phụ thuộc các property `OldVehiclePlate`, `NewVehiclePlate`, `OldStandardTare`, `NewStandardTare`.

**Files likely touched:**

- `src/StationApp.UI/ViewModels/WeighingSessionEditHistoryViewModel.cs`

**Verification:**

- Build UI không lỗi.

### Phase 2: Parser audit payload

#### Task 2: Tạo mapper parse `DetailJson`

**Mô tả:** Viết mapper đọc `AuditLog.DetailJson` theo các dạng `Changes`, `Old/New`, `OldX/NewX`, và fallback summary.

**Acceptance criteria:**

- `EDIT_WEIGHING_SESSION` hiển thị nhiều dòng field cũ/mới đúng.
- `UPDATE_TEMPORARY_EXPORT_CUT_ORDER` diff được `Old` / `New`.
- `UPDATE_CLAY_VESSEL` diff được `Old` / `New`.
- `TOGGLE_CRUSHER_RETURNED_BROKEN_TRIP` hiển thị được trạng thái hoàn và TL hoàn ghi nhận.
- Payload không có old/new vẫn có ít nhất một dòng summary đọc được.

**Files likely touched:**

- `src/StationApp.UI/Services/AuditLogDisplayMapper.cs`
- `src/StationApp.UI/ViewModels/WeighingSessionEditHistoryViewModel.cs`

**Verification:**

- Unit test mapper bằng vài JSON mẫu.

#### Task 2.1: Chuẩn hóa helper tạo `DetailJson` cho audit log mới

**Mô tả:** Tạo helper/builder dùng chung khi ghi audit log mới, để các use case sau này không tự serialize mỗi nơi một kiểu.

**Acceptance criteria:**

- Có helper tạo payload chuẩn gồm `Subject`, `Reason`, `Changes`, `Summary`, `Notes`.
- Helper hỗ trợ thêm nhiều field thay đổi với `Old`, `New`, `Unit`.
- Không bắt buộc migrate dữ liệu audit cũ.
- Các use case đang ghi audit quan trọng có thể được chuyển dần sang helper này mà không làm vỡ parser cũ.

**Files likely touched:**

- `src/StationApp.Application` hoặc `src/StationApp.UI/Services` tùy vị trí chốt.
- Các use case audit quan trọng nếu triển khai luôn: đổi số xe, sửa cắt lệnh tạm, sửa tàu, hàng hoàn.

**Verification:**

- Unit test helper tạo JSON chuẩn.
- Unit test mapper đọc được JSON chuẩn.

### Phase 3: Query audit tổng quát

#### Task 3: Thêm search audit tổng quát

**Mô tả:** Bổ sung method repository tìm audit log theo ngày/trạm/action/keyword, không whitelist cứng theo trạm.

**Acceptance criteria:**

- Manager/Admin xem được mọi audit log thuộc trạm đang chọn trong khoảng ngày.
- Log `UPDATE_WEIGHING_SESSION_MOOC_NO`, `UPDATE_WEIGHING_SESSION_SEAL_NO`, account, station settings nếu có trong DB sẽ hiện.
- Không làm hỏng màn lịch sử chuyển chuyến xuất khẩu nếu màn đó vẫn dùng query cũ.

**Files likely touched:**

- `src/StationApp.Application/Interfaces/IAuditLogRepository.cs`
- `src/StationApp.Infrastructure/Repositories/OtherRepositories.cs`
- `src/StationApp.UI/ViewModels/WeighingSessionEditHistoryViewModel.cs`

**Verification:**

- Test repository với seed audit nhiều action.
- Manual check filter theo station.

### Phase 4: Sửa grid XAML

#### Task 4: Đổi cột grid sang layout cũ/mới tổng quát

**Mô tả:** Thay các cột chuyên biệt đổi số xe bằng cột `Hành động`, `Đối tượng`, `Giá trị cũ`, `Giá trị mới`, `Ghi chú`, `Chi tiết`.

**Acceptance criteria:**

- Không còn cột `Số xe cũ`, `Số xe mới`, `TL bì cũ`, `TL hàng mới`.
- Không có cột `Trường thay đổi`; tên trường được hiển thị bên trong `Giá trị cũ` và `Giá trị mới`.
- Các dòng audit có text tiếng Việt đúng encoding.
- Text dài ở `Giá trị cũ`, `Giá trị mới`, `Ghi chú`, `Chi tiết` không làm vỡ layout; có wrap hoặc width hợp lý.

**Files likely touched:**

- `src/StationApp.UI/Views/WeighingSessionEditHistoryView.xaml`

**Verification:**

- Build UI.
- Manual mở màn Lịch sử chỉnh sửa.

### Phase 5: Bộ lọc và UX polish

#### Task 5: Thêm filter hành động/từ khóa nếu cần

**Mô tả:** Cho người dùng lọc nhanh theo action hoặc từ khóa khi log nhiều.

**Acceptance criteria:**

- Dropdown `Hành động` có `Tất cả` và các action có log trong khoảng ngày/trạm hoặc danh sách action phổ biến.
- Textbox từ khóa lọc trong `DetailJson`, actor, action, entity.
- Nút `Xóa lọc` reset đầy đủ.

**Files likely touched:**

- `src/StationApp.UI/ViewModels/WeighingSessionEditHistoryViewModel.cs`
- `src/StationApp.UI/Views/WeighingSessionEditHistoryView.xaml`
- `src/StationApp.Infrastructure/Repositories/OtherRepositories.cs`

**Verification:**

- Manual filter action và keyword.

### Phase 6: Test và rà encoding

#### Task 6: Test mapper và build

**Mô tả:** Bổ sung test cho parser để tránh sau này thêm action mới lại làm vỡ grid.

**Acceptance criteria:**

- Có test cho `Changes`.
- Có test cho `Old/New`.
- Có test cho fallback payload một chiều.
- Không còn mojibake trong màn Lịch sử chỉnh sửa.

**Files likely touched:**

- `tests/StationApp.Application.Tests` hoặc `tests/StationApp.UI.Tests` nếu có project phù hợp.
- Nếu chưa có UI test project, đặt mapper ở Application để test dễ hơn.

**Verification:**

- `dotnet test` project chứa test mapper.
- `dotnet build src\StationApp.UI\StationApp.UI.csproj --no-restore`.

## 7. Rủi ro và cách xử lý

| Rủi ro | Mức độ | Cách xử lý |
|---|---|---|
| Payload audit cũ không đồng nhất | Cao | Parser nhiều tầng: `Changes` -> `Old/New` -> `OldX/NewX` -> fallback summary |
| Một dòng audit có quá nhiều field thay đổi | Trung bình | Cột cũ/mới dùng wrap nhiều dòng dạng `Tên trường: giá trị`; có thể thêm modal chi tiết sau |
| Query tổng quát kéo quá nhiều log | Trung bình | Giữ filter ngày mặc định 7 ngày, lọc station, thêm action/keyword |
| Field kỹ thuật làm nhiễu người dùng | Trung bình | Dictionary field hiển thị + blacklist field kỹ thuật |
| Encoding tiếng Việt tiếp tục lỗi | Trung bình | Sửa file XAML/ViewModel liên quan bằng UTF-8 và dùng text có dấu chuẩn |
| Màn export transfer history dùng chung query bị ảnh hưởng | Thấp | Không sửa logic riêng của `ExportTripTransferHistoryViewModel` trong task này, hoặc giữ method cũ |

## 8. Câu hỏi cần chốt trước khi code

1. Màn **Lịch sử chỉnh sửa** có cần hiển thị **tất cả audit log** của trạm không, bao gồm tạo tài khoản/cấu hình/cập nhật app, hay chỉ các audit log ảnh hưởng nghiệp vụ cân?
2. Có muốn thêm cột/nút **Xem JSON chi tiết** ngay trong phase đầu không, hay chỉ cần grid tổng quát?
3. Với audit log có nhiều field thay đổi, plan đã chốt hướng **một audit log là một dòng**, cột `Giá trị cũ` và `Giá trị mới` gộp nhiều field dạng `Tên trường: giá trị`.

## 9. Đề xuất chốt của Codex

Đề xuất làm theo hướng:

1. Hiển thị **mỗi audit log là một dòng** để tránh grid bị nhân nhiều dòng.
2. Query **mọi audit log của trạm** trong khoảng ngày, nhưng có dropdown action để lọc.
3. Chưa làm modal JSON ở bước đầu; chỉ thêm `Chi tiết` dạng summary. Nếu sau khi dùng thấy cần soi sâu thì làm modal riêng.
