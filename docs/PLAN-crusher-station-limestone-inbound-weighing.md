# Plan: Cân trạm đập cho hàng nhập đá vôi

## 1. Mục tiêu

Xây dựng luồng cân hàng nhập cho trạm đập, chủ yếu phục vụ xe nội bộ chở đá vôi. Luồng này chạy trên cùng ứng dụng và cùng database hiện tại, nhưng dữ liệu phải được phân vùng theo trạm bằng `StationCode`.

Mục tiêu nghiệp vụ:

- Trạm đập chỉ nhìn thấy và thao tác dữ liệu của trạm đập.
- Trạm cân chính không nhìn thấy dữ liệu trạm đập trong các màn vận hành thường ngày.
- Xe tại trạm đập là xe nội bộ, định danh chính bằng `Số xe` như `01.386`, không dùng biển số xe làm khóa nghiệp vụ chính.
- Cho phép cân 1 lần nếu xe đã có trọng lượng xe chuẩn.
- Vẫn cho phép cân 2 lần như luồng nhập hàng hiện tại khi cần kiểm chứng hoặc xe chưa có trọng lượng chuẩn.
- Báo cáo, dashboard và sync central phải tính đúng theo từng trạm và từng chế độ cân.

## 2. Trạng thái code hiện tại sau phân trạm

Code hiện tại đã có các nền tảng cần dùng lại:

- Bảng/trạng thái trạm: `stations`.
- Gắn tài khoản với trạm: `user_station_assignments`.
- Cấu hình menu/KPI theo trạm: `station_feature_flags`.
- Service lấy trạm hiện tại: `IStationScope`.
- Service kiểm tra trạm được phép thao tác: `IStationAuthorizationService`.
- Service đọc feature theo trạm: `IStationFeatureService`.
- Header app cho phép chọn trạm trong danh sách trạm user được phân quyền.
- Các dữ liệu nghiệp vụ chính đã bắt đầu có `StationCode`.
- ERP contract đã chuyển sang hướng bắt buộc truyền `@StationCode`.

Vì vậy plan trạm đập không cần làm lại phần phân trạm. Plan này chỉ bổ sung nghiệp vụ riêng của trạm đập trên nền `StationCode` và user-station scope hiện có.

## 3. Nguyên tắc thiết kế

- `StationCode` là khóa phân vùng dữ liệu bắt buộc.
- Không dùng `station_type`.
- Không suy luận trạm theo sản phẩm, kho, loại hàng hoặc tên máy.
- Menu nào hiện ở trạm nào lấy từ `station_feature_flags`.
- Dữ liệu thao tác mới phải lấy `StationCode` từ `IStationScope`, không đọc trực tiếp từ `app_config.station_code`.
- Xe nội bộ vẫn lưu trong bảng `vehicles`; `VehiclePlate` là số xe/biển số dùng để nhận diện xe trên giao diện.
- Thêm cờ `IsInternalVehicle` để phân biệt xe nội bộ và xe ngoài.
- Với xe nội bộ, trường TTCP hiện có được hiểu là `Trọng lượng xe chuẩn`; với xe ngoài, TTCP vẫn giữ ý nghĩa tải trọng toàn bộ cho phép.
- Trong code hiện tại trường TTCP của `vehicles` là `TtcpWeight`; không tạo thêm trường trọng lượng chuẩn mới nếu `TtcpWeight` đã đáp ứng được.
- Trọng lượng xe chuẩn phải lưu snapshot vào lượt cân để báo cáo lịch sử không đổi khi danh mục xe chuẩn thay đổi.

## 4. Phạm vi chức năng

### 4.1 Trong phạm vi

- Mở rộng danh mục xe hiện có để quản lý xe nội bộ và trọng lượng xe chuẩn.
- Thêm chế độ cân nhập hàng:
  - `TWO_WEIGH`: cân 2 lần như luồng nhập hiện tại.
  - `SINGLE_WITH_STANDARD_TARE`: cân 1 lần, lấy trọng lượng xe chuẩn để tính KL hàng.
- Thêm màn chức năng riêng `Cân trạm đập`; giao diện kế thừa bố cục màn `Cân nội địa` nhưng flow nghiệp vụ tách riêng.
- Cập nhật danh sách xe vào/ra, dashboard, báo cáo nhập để hỗ trợ cân 1 lần và số xe nội bộ.
- Cập nhật payload sync để Central DB reconstruct được lượt cân trạm đập.
- Giữ filter dữ liệu theo `StationCode` ở repository/service, không chỉ lọc ở UI.

### 4.2 Ngoài phạm vi phase đầu

- Không tự động học trọng lượng xe chuẩn từ dữ liệu cũ.
- Không nhận diện xe bằng camera/AI.
- Không đồng bộ ngược danh mục xe chuẩn từ central về local.
- Không tách database riêng cho trạm đập.
- Không thay thế toàn bộ danh mục xe hiện hữu.

## 5. Cấu hình trạm đập

### 5.1 Cấu hình menu hiện có

Với trạm đập, admin cấu hình tại:

```text
Cấu hình hệ thống -> Danh mục trạm -> Menu hiển thị
```

Gợi ý bật/tắt:

| Chức năng | Trạm đập |
| --- | --- |
| Trang chủ | Bật |
| Danh sách xe vào | Bật |
| Cân nội địa | Tắt nếu trạm đập không dùng luồng nội địa thường |
| Cân trạm đập | Bật |
| Cân xuất khẩu | Tắt |
| Danh sách xe ra | Bật |
| Báo cáo xuất | Tắt |
| Báo cáo nhập | Bật |
| KPI nhập hàng | Bật |
| KPI xuất hàng | Tắt |
| Màn mặc định | `CrusherWeighing` hoặc `IncomingVehicles` |

### 5.2 Cấu hình nghiệp vụ trạm đập

Không nên nhét cấu hình nghiệp vụ trạm đập vào `app_config` dạng global, vì cùng app/DB có thể dùng nhiều trạm. Đề xuất thêm bảng cấu hình theo trạm:

```sql
CREATE TABLE dbo.station_operation_settings
(
    Id uniqueidentifier NOT NULL PRIMARY KEY,
    StationCode nvarchar(50) NOT NULL,
    SettingKey nvarchar(100) NOT NULL,
    SettingValue nvarchar(255) NOT NULL,
    CreatedAt datetime2(7) NOT NULL,
    CreatedBy nvarchar(100) NULL,
    UpdatedAt datetime2(7) NULL,
    UpdatedBy nvarchar(100) NULL
);

CREATE UNIQUE INDEX UX_station_operation_settings_station_key
ON dbo.station_operation_settings(StationCode, SettingKey);
```

Các key cần có:

| Key | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `crusher_single_weigh_enabled` | `false` | Cho phép cân 1 lần theo TL xe chuẩn. |
| `crusher_default_weigh_mode` | `TWO_WEIGH` | Chế độ cân mặc định. |
| `crusher_require_standard_tare_for_single_weigh` | `true` | Bắt buộc có TL xe chuẩn mới được cân 1 lần. |
| `crusher_standard_tare_tolerance_kg` | `0` | Dung sai cảnh báo khi đối chiếu TL xe chuẩn. |
| `crusher_default_product_code` | rỗng | Sản phẩm mặc định, ví dụ đá vôi. |

Quyết định triển khai:

- Tạo và dùng bảng riêng `station_operation_settings`.
- `station_feature_flags` chỉ dùng cho hiển thị menu/KPI/navigation.
- `station_operation_settings` dùng cho tham số nghiệp vụ theo trạm.
- Không lưu setting trạm đập vào `app_config` global.

## 6. Thiết kế dữ liệu

### 6.1 Mở rộng bảng `vehicles`

Không tạo bảng riêng cho trọng lượng xe chuẩn. Mở rộng danh mục xe hiện có để quản lý cả xe ngoài và xe nội bộ.

Nguyên tắc:

- `VehiclePlate` vẫn là trường nhận diện xe trên giao diện cho cả xe ngoài và xe nội bộ.
- Với xe nội bộ, `VehiclePlate` có thể là số xe nội bộ như `01.386`.
- Thêm cờ `IsInternalVehicle`.
- Với `IsInternalVehicle = true`, TTCP hiện có được hiểu và hiển thị là `Trọng lượng xe chuẩn (kg)`.
- Với `IsInternalVehicle = false`, TTCP vẫn là tải trọng toàn bộ cho phép như hiện tại.
- Khi cân trạm đập theo mode cân 1 lần, chỉ cho chọn xe có `IsInternalVehicle = true` và `TtcpWeight > 0`.
- Không dùng `TtcpWeight` của xe nội bộ để kiểm tra quá tải trong màn `Cân trạm đập`; giá trị này là trọng lượng xe chuẩn, không phải TTCP vận tải.

Các cột cần bổ sung nếu bảng `vehicles` hiện chưa có:

| Cột | Kiểu | Ghi chú |
| --- | --- | --- |
| `IsInternalVehicle` | `bit NOT NULL DEFAULT 0` | Đánh dấu xe nội bộ. |
| `StandardTareUpdatedAt` | `datetime2(7) NULL` | Thời điểm cập nhật trọng lượng xe chuẩn. |
| `StandardTareUpdatedBy` | `nvarchar(100) NULL` | Người cập nhật trọng lượng xe chuẩn. |
| `StandardTareSource` | `nvarchar(40) NULL` | `MANUAL`, `ERP`, `FROM_TWO_WEIGH`. |

Trường trọng lượng chuẩn:

- Dùng lại `vehicles.TtcpWeight`.
- Không thêm `StandardTareWeight` vào `vehicles` để tránh lưu trùng một giá trị với hai tên khác nhau.
- Khi `IsInternalVehicle = true`, label UI của `TtcpWeight` là `Trọng lượng xe chuẩn (kg)`.
- Khi `IsInternalVehicle = false`, label UI của `TtcpWeight` là `TTCP (kg)`.

Không thêm `StationCode` vào `vehicles` trong phase đầu nếu danh mục xe vẫn dùng chung. Nếu sau này cần một số xe giống nhau nhưng trọng lượng chuẩn khác nhau theo trạm, khi đó mới tách bảng phụ hoặc thêm scope theo trạm cho `vehicles`.

UI danh mục xe cần đổi label động:

- Xe ngoài: hiển thị `TTCP (kg)`.
- Xe nội bộ: hiển thị `Trọng lượng xe chuẩn (kg)`.

Index khuyến nghị:

```sql
CREATE INDEX IX_vehicles_internal_vehicle
ON dbo.vehicles(IsInternalVehicle, VehiclePlate, IsActive);
```

### 6.2 Bổ sung `weighing_sessions`

Thêm các cột snapshot:

| Cột | Kiểu | Ghi chú |
| --- | --- | --- |
| `WeighingMode` | `nvarchar(40)` | `TWO_WEIGH` hoặc `SINGLE_WITH_STANDARD_TARE` |
| `InternalVehicleNo` | `nvarchar(50)` nullable | Snapshot số xe nội bộ, lấy từ `vehicles.VehiclePlate` với xe nội bộ |
| `StandardTareWeightSnapshot` | `decimal(18,3)` nullable | Snapshot `vehicles.TtcpWeight` dùng làm TL xe chuẩn khi cân |
| `StandardTareSourceSnapshot` | `nvarchar(40)` nullable | Nguồn TL xe chuẩn |
| `StandardTareVehicleId` | `uniqueidentifier` nullable | Bản ghi `vehicles` đã dùng làm nguồn TL xe chuẩn |
| `NetWeightCalculationMode` | `nvarchar(40)` nullable | `WEIGHT2_DIFF` hoặc `WEIGHT1_MINUS_STANDARD_TARE` |

Backfill:

- Dữ liệu cũ: `WeighingMode = 'TWO_WEIGH'`.
- Dữ liệu cũ: `NetWeightCalculationMode = 'WEIGHT2_DIFF'` nếu có đủ cân 1/cân 2.

### 6.3 Bổ sung chứng từ liên quan

Tối thiểu phase đầu:

- `weigh_tickets`: thêm `WeighingMode`, `InternalVehicleNo`, `StandardTareWeightSnapshot`, `NetWeightCalculationMode`.
- `delivery_tickets`: có thể lấy qua `WeighingSessionId`; nếu cần sync/in độc lập thì thêm các trường snapshot tương tự.
- `sync_outbox`: payload phải có các field mới.
- Central DB: thêm field tương ứng cho `weighing_sessions`, `weigh_tickets`, nếu có thì `delivery_tickets`.

## 7. Công thức tính

### 7.1 Cân 1 lần theo TL xe chuẩn

Áp dụng cho xe nội bộ chở đá vôi vào cân:

```text
GrossWeight = Weight1
TareWeight = StandardTareWeightSnapshot
NetWeight = GrossWeight - TareWeight
```

Validation:

- Số xe nội bộ bắt buộc; lưu snapshot vào `InternalVehicleNo`.
- `Weight1 > 0`.
- `StandardTareWeightSnapshot > 0`.
- `Weight1 > StandardTareWeightSnapshot`.
- `NetWeight > 0`.
- Nếu xe chưa có TL xe chuẩn active thì không cho cân 1 lần.

Trạng thái sau khi lưu:

- `Weight1` có giá trị.
- `Weight2 = NULL`.
- `NetWeight = Weight1 - StandardTareWeightSnapshot`.
- `SessionStatus = COMPLETED` hoặc trạng thái hoàn tất hiện có của luồng nhập.
- Không đi qua logic quá tải/tách tải.
- Được coi là xe nhập đã xong.

### 7.2 Cân 2 lần

Giữ công thức nhập hàng hiện tại:

```text
NetWeight = ABS(Weight2 - Weight1)
```

Với trạm đập:

- Không bắt buộc tên tài xế.
- Không bắt buộc số lượng đặt.
- Không áp dụng logic quá tải nếu không có cấu hình riêng.
- Không dùng `TtcpWeight` của xe nội bộ để tính ngưỡng quá tải trong màn `Cân trạm đập`.
- Có thể lưu `InternalVehicleNo` và snapshot TL xe chuẩn để đối chiếu, nhưng kết quả chính vẫn theo cân 1/cân 2 thực tế.

## 8. Giao diện

### 8.1 Danh mục xe

Không tạo màn danh mục trọng lượng xe chuẩn riêng. Mở rộng màn `Danh mục xe` hiện có.

Trường cần bổ sung/điều chỉnh:

- Checkbox `Xe nội bộ`.
- Trường TTCP hiện có giữ nguyên dữ liệu, nhưng đổi label theo checkbox:
  - `Xe nội bộ = false`: `TTCP (kg)`.
  - `Xe nội bộ = true`: `Trọng lượng xe chuẩn (kg)`.
- Nguồn trọng lượng chuẩn.
- Thời điểm/người cập nhật trọng lượng chuẩn nếu cần hiển thị audit.

Chức năng:

- Tìm kiếm xe như hiện tại.
- Cho phép lọc xe nội bộ/xe ngoài.
- Khi lưu xe nội bộ, bắt buộc trọng lượng xe chuẩn > 0 nếu trạm đập dùng cân 1 lần.
- Ghi audit khi thay đổi trọng lượng xe chuẩn.

### 8.2 Màn `Cân trạm đập`

Tạo màn chức năng mới:

```text
Cân trạm đập
```

Giao diện:

- Bố cục giống màn `Cân nội địa` để người dùng quen thao tác.
- Tách View/ViewModel/use case riêng để không làm phức tạp thêm flow cân nội địa hiện tại.
- Tái sử dụng style, layout container, camera preview, khối hiển thị cân, nút thao tác và cách hiển thị trạng thái của màn `Cân nội địa` để hai màn đồng bộ trải nghiệm.
- Không copy UI rồi chỉnh rời rạc nếu có thể tách component/style dùng chung; mục tiêu là sau này sửa giao diện cân một nơi thì hai màn không lệch nhau.
- Menu trái hiển thị theo `station_feature_flags`.
- Feature key đề xuất: `show_menu_crusher_weighing`.
- Navigation target đề xuất: `CrusherWeighing`.

Luồng thao tác:

- Chỉ phục vụ `TransactionType = INBOUND`.
- Ưu tiên nhập/chọn `Số xe`.
- Autocomplete theo `vehicles.VehiclePlate` với `IsInternalVehicle = true`.
- Hiển thị `Trọng lượng xe chuẩn (kg)` sau khi chọn xe nội bộ.
- Hiển thị lựa chọn chế độ cân:
  - `Cân 1 lần theo TL xe chuẩn`
  - `Cân 2 lần`
- Nếu `crusher_single_weigh_enabled = false`, ẩn/disable cân 1 lần.
- Nếu xe chưa có trọng lượng chuẩn, disable cân 1 lần và hiển thị lý do.
- Khi lưu cân 1 lần, hiển thị rõ công thức:

```text
KL hàng = Cân lần 1 - TL xe chuẩn
```

Màn `Cân nội địa` hiện tại vẫn giữ cho luồng cân nội địa thông thường. Không đưa logic cân trạm đập vào màn này ngoài các phần dùng chung có thể tái sử dụng.

### 8.3 Cấu hình menu cho màn mới

Cần mở rộng các phần hiện có:

- `StationFeatureKeys`: thêm `ShowMenuCrusherWeighing = "show_menu_crusher_weighing"`.
- `StationFeatureSetDto`: thêm `ShowMenuCrusherWeighing`.
- `StationFeatureService` và `StationAdministrationService`: đọc/lưu feature key mới.
- Màn `Danh mục trạm`: thêm checkbox `Cân trạm đập`.
- `MainViewModel`: thêm `CanViewCrusherWeighing`, navigation target `CrusherWeighing`, và fallback default navigation có xét màn này.
- `MainWindow`: thêm menu trái `Cân trạm đập`.

### 8.4 Danh sách xe vào/ra

Với trạm đập:

- Ưu tiên hiển thị `Số xe`.
- Biển số xe chỉ là phụ nếu có.
- Không bắt buộc tài xế.
- Không bắt buộc số lượng đặt.
- Không hiển thị thông tin riêng của xuất khẩu.
- Lượt cân 1 lần đã lưu xong phải hiển thị như xe đã hoàn tất.

## 9. Dashboard và báo cáo

### 9.1 Dashboard

KPI nhập hàng phải lọc theo `StationCode` hiện tại:

- Xe nhập chờ cân: lượt chưa có cân theo mode tương ứng.
- Xe nhập đang xử lý: chỉ áp dụng cho mode `TWO_WEIGH` đã có cân lần 1 nhưng chưa có cân lần 2.
- Xe nhập đã xong: mode `SINGLE_WITH_STANDARD_TARE` đã lưu cân lần 1 hoặc mode `TWO_WEIGH` đã hoàn tất.
- Số tấn nhập hàng: tổng `NetWeight` của lượt hoàn tất theo ngày và trạm.

Với trạm bật `show_dashboard_outbound_kpi = false`, không hiển thị KPI xuất.

### 9.2 Báo cáo nhập

Báo cáo nhập phải lấy cả:

- Lượt cân nhập `TWO_WEIGH` (text tiếng việt hiển thị "Cân 1 lần").
- Lượt cân nhập `SINGLE_WITH_STANDARD_TARE` (text tiếng việt hiển thị "Cân 2 lần").

Cột nên bổ sung/hiển thị:

- Số xe
- Chế độ cân
- TL xe chuẩn
- KL cân 1
- KL cân 2
- KL hàng
- Người cân
- Ghi chú

Quy tắc:

- Cân 1 lần: `KL cân 2` để trống hoặc hiển thị theo mẫu đã thống nhất; `TL xe chuẩn` có giá trị.
- Cân 2 lần: `KL cân 1`, `KL cân 2` theo thực tế; `TL xe chuẩn` chỉ tham khảo nếu có.
- Tổng số lượng lấy theo `NetWeight`, không tự tính lại từ text hiển thị.

## 10. Sync và Central API

Payload cần có:

- `stationCode`
- `weighingMode`
- `internalVehicleNo` hoặc snapshot số xe nội bộ từ `VehiclePlate`
- `standardTareWeightSnapshot`
- `standardTareSourceSnapshot`
- `netWeightCalculationMode`

Central DB cần lưu các trường trên để reconstruct đúng:

- Trạm nào phát sinh lượt cân.
- Xe nội bộ nào được cân.
- Cân 1 lần hay cân 2 lần.
- TL xe chuẩn đã dùng tại thời điểm cân.
- Công thức tính net weight.

Hệ thống hiện đã có luồng sync master `Vehicle`, vì vậy khi mở rộng `vehicles` cần cập nhật cả payload/schema Central cho:

- `isInternalVehicle`
- `standardTareSource`
- `standardTareUpdatedAt`
- `standardTareUpdatedBy`

Tuy nhiên Central không được phụ thuộc vào master `Vehicle` để tính lại lịch sử cân. `weighing_sessions` vẫn phải lưu snapshot trọng lượng xe chuẩn để reconstruct đúng dù danh mục xe sau này thay đổi.

## 11. ERP và stored procedure

Nếu ERP có đẩy dữ liệu xuống trạm đập:

- ERP bắt buộc truyền `@StationCode`.
- Nếu có số xe nội bộ, ERP có thể truyền vào trường biển số/số xe hiện có. Không bắt buộc thêm `@InternalVehicleNo` nếu contract hiện tại đã có `@VehiclePlate`.
- Procedure phải reject nếu thiếu `@StationCode`.
- Không fallback sang trạm mặc định của máy.

Nếu ERP lấy kết quả cân:

- Lấy `NetWeight` đã lưu trong `weighing_sessions`.
- Trả thêm `Weight1Time`, `Weight2Time`, `WeighingMode`, `InternalVehicleNo` hoặc số xe snapshot nếu ERP cần đối chiếu.
- Không tự suy luận lại công thức từ `Weight1`, `Weight2` nếu đã có `NetWeight`.

## 12. Use case cần bổ sung

### 12.1 Quản lý xe nội bộ và TL xe chuẩn

- Mở rộng các use case danh mục xe hiện có để hỗ trợ `IsInternalVehicle`.
- Khi `IsInternalVehicle = true`, validate TTCP/trọng lượng chuẩn > 0 nếu cấu hình yêu cầu.
- Ghi audit khi thay đổi cờ xe nội bộ hoặc trọng lượng chuẩn.

### 12.2 Cân 1 lần

- `SearchInternalVehicles`
- `LoadStandardTareByVehicle`
- `StartCrusherInboundSession`
- `SaveSingleWeighWithStandardTare`
- `CompleteCrusherInboundSession`

### 12.3 Cân 2 lần trạm đập

Tận dụng luồng nhập hiện tại, nhưng cần đảm bảo:

- Lọc theo `StationCode`.
- Không validate bắt buộc tài xế và số lượng đặt.
- Không kích hoạt logic quá tải/tách tải.
- Lưu `InternalVehicleNo` nếu có.

## 13. Migration và thứ tự triển khai

1. Xác nhận phân trạm hiện tại hoạt động:
   - `stations`
   - `user_station_assignments`
   - `station_feature_flags`
   - `IStationScope`
   - `@StationCode` trong ERP procedures
2. Tạo `station_operation_settings` cho setting nghiệp vụ theo trạm.
3. Mở rộng bảng `vehicles` với `IsInternalVehicle`, `StandardTareSource`, `StandardTareUpdatedAt`, `StandardTareUpdatedBy` nếu chưa có.
4. Thêm feature key `show_menu_crusher_weighing` và navigation target `CrusherWeighing`.
5. Thêm các cột mode/snapshot vào `weighing_sessions`.
6. Thêm các cột snapshot cần thiết vào `weigh_tickets` và central schema.
7. Backfill dữ liệu cũ:
   - `WeighingMode = 'TWO_WEIGH'`
   - `NetWeightCalculationMode = 'WEIGHT2_DIFF'`
8. Cập nhật màn danh mục xe để quản lý xe nội bộ/trọng lượng chuẩn.
9. Tạo màn riêng `Cân trạm đập`.
10. Cập nhật dashboard và báo cáo nhập.
11. Cập nhật sync payload và Central API.
12. Test với dữ liệu thực tế trạm đập.

## 14. Test plan

### 14.1 Phân trạm

- User chỉ có trạm `DAP01` chỉ thấy dữ liệu `DAP01`.
- User `QN01` không thấy dữ liệu trạm đập.
- Admin đổi trạm từ header thì dữ liệu reload đúng.
- Menu trạm đập ẩn Cân xuất khẩu/Báo cáo xuất nếu cấu hình tắt.

### 14.2 Danh mục xe nội bộ và TL xe chuẩn

- Tạo xe `01.386` trong danh mục xe.
- Tích `Xe nội bộ`.
- Nhập `Trọng lượng xe chuẩn = 15,000 kg`.
- Xe nội bộ hiển thị label `Trọng lượng xe chuẩn (kg)`.
- Xe ngoài vẫn hiển thị label `TTCP (kg)`.
- Sửa trọng lượng chuẩn ghi audit.

### 14.3 Cân 1 lần

- Số xe `01.386`, TL xe chuẩn `15,000 kg`.
- Cân lần 1 `50,000 kg`.
- Hệ thống lưu `NetWeight = 35,000 kg`.
- Lượt cân hoàn tất ngay sau lưu cân lần 1.
- Dashboard và báo cáo nhập tính `35,000 kg`.
- Sync lên Central có đủ mode/snapshot.

### 14.4 Cân 1 lần thiếu TL chuẩn

- Chọn số xe chưa có TL chuẩn.
- Cân 1 lần bị disable hoặc báo lỗi rõ.
- Vẫn có thể chuyển sang cân 2 lần.

### 14.5 Cân 2 lần

- Cân lần 1 và cân lần 2 chạy theo luồng nhập hiện tại.
- Không bắt buộc tài xế và số lượng đặt.
- Không hiển thị/không yêu cầu tách tải.
- Báo cáo/dashboard lấy `NetWeight` đúng.

### 14.6 Snapshot

- Cân 1 lần với TL chuẩn `15,000 kg`.
- Sau đó sửa danh mục thành `15,500 kg`.
- Báo cáo lượt cân cũ vẫn dùng snapshot `15,000 kg`.

## 15. Rủi ro và kiểm soát

| Rủi ro | Kiểm soát |
| --- | --- |
| Sai TL xe chuẩn làm sai KL hàng | Chỉ user có quyền được sửa, ghi audit, hiển thị TL chuẩn rõ trước khi lưu cân |
| Người vận hành nhập nhầm số xe | Autocomplete theo số xe, hiển thị mô tả/biển số phụ để đối chiếu |
| Dữ liệu trạm đập lẫn trạm khác | Bắt buộc filter `StationCode` ở repository/service |
| Cân 1 lần cho xe chưa có TL chuẩn | Disable/chặn lưu |
| Sửa TL chuẩn làm đổi báo cáo cũ | Lưu snapshot vào `weighing_sessions` |
| Sync thiếu field mode/snapshot | Cập nhật payload và Central DB, test reconstruct |

## 16. Điều kiện hoàn thành

Chức năng được coi là hoàn thành khi:

- Trạm đập cân được hàng nhập đá vôi bằng số xe nội bộ.
- Cân 1 lần theo TL xe chuẩn tính đúng KL hàng.
- Cân 2 lần vẫn hoạt động cho trường hợp cần kiểm chứng.
- Dữ liệu không hiển thị lẫn giữa các trạm.
- Dashboard và báo cáo nhập tính đúng theo cả hai mode.
- Sync central có đủ dữ liệu reconstruct.
- Thay đổi TL xe chuẩn không làm sai dữ liệu lịch sử.
