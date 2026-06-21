# Kế hoạch chi tiết: Bổ sung cột cho grid Danh sách chuyến xe ở màn Cân xuất khẩu

## 1. Mục tiêu

Mở rộng grid `Danh sách chuyến xe` ở màn `Cân xuất khẩu` để hiển thị gần giống biểu mẫu người dùng gửi, nhưng toàn bộ phần khối lượng sẽ hiển thị theo đơn vị `kg` thay vì `tấn`.

Các cột mục tiêu cần làm rõ:

- `Stt`
- `Ca`
- `Ngày xuất`
- `Phương tiện`
- `Số lượng qua cân (kg)`
- `Số lượng qua cân (bao)`
- `Khối lượng vỏ bao (kg)`
- `Rách vỡ hồi về (kg)`
- `Rách vỡ hồi về (bao)`
- `SL thực xuất (kg, trừ vỏ)`
- `SL thực xuất (bao)`
- `KL lệch quy cách (kg) - theo chuyến`
- `KL lệch quy cách (kg) - theo bao`

Plan này tập trung vào:

- làm rõ nguồn dữ liệu cho từng cột
- làm rõ công thức tính
- chỉ ra cột nào đã có dữ liệu sẵn, cột nào cần bổ sung
- chốt những điểm nghiệp vụ còn cần duyệt trước khi code

## 2. Hiện trạng code đã rà soát

### 2.1 Grid hiện tại

Grid `Danh sách chuyến xe` ở [ExportWeighingView.xaml](/G:/Source-code/pmcan_C#/src/StationApp.UI/Views/ExportWeighingView.xaml:468) hiện đang có:

- `Số lượt cân`
- `Số PTVC`
- `Mooc`
- `Tài xế`
- `Cân lần 1`
- `Cân lần 2`
- `Net`
- `Bao`
- `Trạng thái`
- `PC`
- `PGN`

Hiện chưa có:

- `Stt`
- `Ca`
- `Ngày xuất`
- `Khối lượng vỏ bao`
- `Rách vỡ hồi về`
- `SL thực xuất (trừ vỏ)`
- `KL lệch quy cách`

### 2.2 DTO chuyến xe export hiện tại

DTO [ExportVehicleTripListItem](/G:/Source-code/pmcan_C#/src/StationApp.Application/DTOs/Dtos.cs:490) hiện có:

- `SessionNo`
- `VehiclePlate`
- `MoocNumber`
- `DriverName`
- `Weight1`
- `Weight2`
- `NetWeight`
- `ActualAllocatedWeight`
- `BagCountDisplay`
- `Weight1Time`
- `Weight2Time`
- `SessionStatus`
- `WeighTicketNo`
- `DeliveryNo`

Chưa có field riêng cho:

- `Stt`
- `Ca`
- `Ngày xuất`
- `BagTareWeightKg`
- `ReturnedBrokenWeightKg`
- `ReturnedBrokenBagCount`
- `ActualNetAfterTareKg`
- `StandardKgPerBag`
- `ActualKgPerBag`
- `DeviationKgPerTrip`
- `DeviationKgPerBag`

### 2.3 Nguồn dữ liệu hiện có có thể tái sử dụng

Ở repository [CutOrderRepository.cs](/G:/Source-code/pmcan_C#/src/StationApp.Infrastructure/Repositories/CutOrderRepository.cs:1277), dữ liệu grid chuyến xe đang lấy từ:

- `weighing_session_lines`
- `weighing_sessions`
- `cut_orders`
- `weigh_tickets`
- `delivery_tickets`

Ngoài ra:

- `cut_orders.BagWeightKg` đã có sẵn
- `line.ActualAllocatedWeight` đã có sẵn
- `deliveryTicket.AllocatedBagCount` và `line.ActualAllocatedBagCount` đã có sẵn
- logic tính số bao display hiện đã có helper `ResolveBagCountDisplay(weightKg, bagWeightKg, fallback)` trong [CutOrderRepository.cs](/G:/Source-code/pmcan_C#/src/StationApp.Infrastructure/Repositories/CutOrderRepository.cs:1101)
- logic `kg/bao chuẩn` và `kg/bao thực tế` đã có ở báo cáo xuất trong [ExportSummaryReportServices.cs](/G:/Source-code/pmcan_C#/src/StationApp.Infrastructure/Services/ExportSummaryReportServices.cs:432)

### 2.4 Logic ca hiện có

Ở [ExportSummaryReportViewModel.cs](/G:/Source-code/pmcan_C#/src/StationApp.UI/ViewModels/ExportSummaryReportViewModel.cs:226), app đang dùng khung ca:

- `Ca A`: `06:00:00` -> `13:59:59`
- `Ca B`: `14:00:00` -> `21:59:59`
- `Ca C`: `22:00:00` -> `05:59:59` ngày hôm sau

Plan này đề xuất tái dùng đúng quy ước ca đó để tránh mỗi nơi tính một kiểu.

## 3. Đề xuất danh sách cột và nguồn dữ liệu

### 3.1 Các cột có thể lấy ngay từ dữ liệu hiện tại

| Cột UI | Nguồn dữ liệu đề xuất |
|---|---|
| `Stt` | đánh số theo thứ tự dòng sau khi sort |
| `Ngày xuất` | `Weight2Time` nếu đã có, fallback `UpdatedAt/CreatedAt` của session |
| `Phương tiện` | `VehiclePlate` |
| `Số lượng qua cân (kg)` | `ActualAllocatedWeight` nếu có, fallback `NetWeight` |
| `Số lượng qua cân (bao)` | `BagCountDisplay` |
| `Khối lượng vỏ bao (kg)` | tính từ `Số lượng qua cân (bao) * BagWeightKg` |
| `SL thực xuất (kg, trừ vỏ)` | tính từ `Số lượng qua cân (kg) - Khối lượng vỏ bao (kg)` |
| `SL thực xuất (bao)` | ưu tiên `AllocatedBagCount` hoặc `ActualAllocatedBagCount`, fallback `BagCountDisplay` |
| `KL lệch quy cách (kg) - theo chuyến` | tính từ `SL thực xuất (bao) * KL lệch quy cách (kg) theo bao` hoặc `SL thực xuất (kg) - (SL thực xuất (bao) * kg/bao chuẩn)` |
| `KL lệch quy cách (kg) - theo bao` | tính từ `SL thực xuất (kg) / SL thực xuất (bao) - kg/bao chuẩn` |

### 3.2 Các cột cần bổ sung logic suy ra

| Cột UI | Ghi chú |
|---|---|
| `Ca` | cần hàm suy ra từ `Ngày xuất` theo khung giờ A/B/C |
| `Khối lượng vỏ bao (kg)` | hiện chưa có field riêng trong DTO, cần tính ở repository |
| `SL thực xuất (kg, trừ vỏ)` | hiện chưa có field riêng trong DTO, cần tính ở repository |
| `KL lệch quy cách (kg)` | cần bổ sung field tính toán riêng trong DTO và repository |

### 3.3 Các cột chưa có nguồn dữ liệu rõ ràng trong code hiện tại

| Cột UI | Trạng thái hiện tại |
|---|---|
| `Rách vỡ hồi về (kg)` | chưa thấy field hoặc source dữ liệu rõ ràng |
| `Rách vỡ hồi về (bao)` | chưa thấy field hoặc source dữ liệu rõ ràng |

Đây là điểm cần chốt trước khi code.

## 4. Công thức tính đề xuất

### 4.1 Quy ước đơn vị

- Trên grid mới:
  - tất cả cột khối lượng hiển thị theo `kg`
  - các cột số bao hiển thị theo `bao`
- Dữ liệu nội bộ vẫn lưu `decimal` theo `kg`
- Không đổi logic lưu database từ `kg` sang `tấn`

### 4.2 Công thức cột `Số lượng qua cân`

`Số lượng qua cân (kg)`:

```text
SoLuongQuaCanKg = ActualAllocatedWeight ?? NetWeight ?? 0
```

Lý do:

- với chuyến đã phân bổ xong, `ActualAllocatedWeight` phản ánh đúng phần hàng của chuyến
- nếu chưa phân bổ xong nhưng đã cân ra `NetWeight`, vẫn nên hiển thị có giá trị

`Số lượng qua cân (bao)`:

```text
SoLuongQuaCanBao =
    Round(SoLuongQuaCanKg / BagWeightKg, 0, AwayFromZero)
```

Fallback:

```text
Nếu BagWeightKg không có hoặc <= 0
=> dùng AllocatedBagCount hoặc ActualAllocatedBagCount nếu có
```

### 4.3 Công thức cột `Khối lượng vỏ bao (kg)`

```text
KhoiLuongVoBaoKg = SoLuongQuaCanBao * BagWeightKg
```

Lưu ý:

- đây là tổng khối lượng vỏ của cả chuyến
- không phải khối lượng vỏ của 1 bao

### 4.4 Công thức cột `SL thực xuất (kg, trừ vỏ)`

```text
SlThucXuatKg = SoLuongQuaCanKg - KhoiLuongVoBaoKg
```

Đề xuất này bám đúng tiêu đề người dùng cung cấp: `SL thực xuất (trừ vỏ)`.

### 4.5 Công thức cột `SL thực xuất (bao)`

```text
SlThucXuatBao =
    ưu tiên AllocatedBagCount hoặc ActualAllocatedBagCount
    nếu không có thì fallback SoLuongQuaCanBao
```

Lý do:

- nếu sau này có logic điều chỉnh bao thực tế riêng theo chuyến, cột này vẫn bám được dữ liệu nghiệp vụ thật
- không khóa cứng hoàn toàn vào phép chia `kg / trọng lượng bao`

### 4.6 Công thức cột `KL lệch quy cách (kg)`

`kg/bao chuẩn`:

```text
KgPerBagChuan = PlannedWeightKg / PlannedBagCount
```

Trong code hiện tại, logic tương tự đã có ở báo cáo xuất:

```text
standardKgPerBag = plannedWeightKg / plannedBagCount
```

`kg/bao thực tế`:

```text
KgPerBagThucTe = SlThucXuatKg / SlThucXuatBao
```

`KL lệch quy cách (kg) - theo bao`:

```text
KlLechTheoBaoKg = KgPerBagThucTe - KgPerBagChuan
```

`KL lệch quy cách (kg) - theo chuyến`:

```text
KlLechTheoChuyenKg = KlLechTheoBaoKg * SlThucXuatBao
```

Hoặc tương đương:

```text
KlLechTheoChuyenKg = SlThucXuatKg - (SlThucXuatBao * KgPerBagChuan)
```

Làm tròn đề xuất:

- `kg/bao chuẩn`: tối đa `2` chữ số thập phân
- `kg/bao thực tế`: tối đa `2` chữ số thập phân
- `KL lệch theo bao`: tối đa `2` chữ số thập phân
- `KL lệch theo chuyến`: tối đa `2` chữ số thập phân

Quy tắc:

```text
decimal.Round(value, 2, MidpointRounding.AwayFromZero)
```

### 4.7 Công thức cột `Ca`

Đề xuất mapping:

- `A`: `06:00:00` -> `13:59:59`
- `B`: `14:00:00` -> `21:59:59`
- `C`: `22:00:00` -> `05:59:59`

`Ngày xuất` nên đi cùng `Weight2Time` vì đây là thời điểm chuyến hoàn tất xuất.

## 5. Điểm cần chốt nghiệp vụ trước khi code

### 5.1 Cột `Rách vỡ hồi về`

Hiện code chưa có nguồn dữ liệu rõ ràng cho:

- `Rách vỡ hồi về (kg)`
- `Rách vỡ hồi về (bao)`

Cần chốt 1 trong 3 hướng:

1. Chỉ hiển thị cột nhưng để `0` hoặc rỗng vì chưa có dữ liệu
2. Bổ sung field nhập tay cho từng chuyến
3. Lấy từ nguồn khác đang tồn tại ngoài app nhưng chưa tích hợp

Khuyến nghị:

- phase 1 nên chốt `0` hoặc `rỗng` nếu chưa có dữ liệu thật
- không nên đoán công thức vì sẽ làm sai nghiệp vụ

### 5.2 `SL thực xuất` có trừ `Rách vỡ hồi về` hay không

Tiêu đề ảnh đang ghi:

- `SL thực xuất (trừ vỏ)`

Theo câu chữ này, plan đề xuất:

- `SL thực xuất` chỉ trừ `vỏ`
- `Rách vỡ hồi về` là cột thông tin riêng, chưa trừ vào `SL thực xuất`

Nếu nghiệp vụ thực tế muốn:

```text
SlThucXuatKg = SoLuongQuaCanKg - KhoiLuongVoBaoKg - RachVoHoiVeKg
```

thì cần chốt lại trước khi code.

### 5.3 Chuẩn `kg/bao` dùng để tính lệch quy cách

Cần chốt chính xác chuẩn dùng để so sánh:

1. `PlannedWeight / PlannedBagCount`
2. `BagWeightKg` của cắt lệnh tạm
3. một cấu hình quy cách chuẩn riêng theo sản phẩm

Khuyến nghị:

- dùng `PlannedWeight / PlannedBagCount`

Lý do:

- khớp nhất với ví dụ người dùng gửi
- cũng sát logic đang có trong báo cáo xuất hiện tại

## 6. Phạm vi thay đổi kỹ thuật đề xuất

### 6.1 DTO

Mở rộng `ExportVehicleTripListItem` thêm:

- `int SequenceNo`
- `string? ShiftCode`
- `DateTime? ExportedAt`
- `decimal? WeighedQuantityKg`
- `int? WeighedBagCount`
- `decimal? BagTareWeightKg`
- `decimal? ReturnedBrokenWeightKg`
- `int? ReturnedBrokenBagCount`
- `decimal? ActualExportWeightKg`
- `int? ActualExportBagCount`
- `decimal? StandardKgPerBag`
- `decimal? ActualKgPerBag`
- `decimal? DeviationKgPerTrip`
- `decimal? DeviationKgPerBag`

### 6.2 Repository

Trong `GetExportVehicleTripsAsync()`:

- nạp thêm `CutOrder`
- tính các giá trị display ở 1 chỗ duy nhất
- chuẩn hóa fallback cho dữ liệu cũ
- sort nhất quán để `Stt` ổn định

### 6.3 UI

Thay grid ở `ExportWeighingView.xaml`:

- đổi tên cột hiện có cho sát nghiệp vụ
- thêm các cột mới
- nhóm cột theo logic:
  - nhận diện chuyến
  - số lượng qua cân
  - vỏ hoặc rách vỡ
  - thực xuất
  - lệch quy cách

Khuyến nghị thứ tự cột:

1. `Stt`
2. `Ca`
3. `Ngày xuất`
4. `Số lượt cân`
5. `Phương tiện`
6. `Mooc`
7. `Tài xế`
8. `Số lượng qua cân (kg)`
9. `Số lượng qua cân (bao)`
10. `Khối lượng vỏ bao (kg)`
11. `Rách vỡ hồi về (kg)`
12. `Rách vỡ hồi về (bao)`
13. `SL thực xuất (kg, trừ vỏ)`
14. `SL thực xuất (bao)`
15. `KL lệch quy cách (kg) - theo chuyến`
16. `KL lệch quy cách (kg) - theo bao`
17. `Trạng thái`
18. `PC`
19. `PGN`

## 7. Kế hoạch triển khai đề xuất

### Bước 1

Chốt lại 3 điểm nghiệp vụ:

- `Rách vỡ hồi về` lấy từ đâu
- `SL thực xuất` có trừ `Rách vỡ hồi về` không
- `kg/bao chuẩn` lấy từ `PlannedWeight / PlannedBagCount` hay nguồn khác

### Bước 2

Mở rộng `ExportVehicleTripListItem` và repository `GetExportVehicleTripsAsync()`.

### Bước 3

Cập nhật grid `Danh sách chuyến xe` ở màn `Cân xuất khẩu`.

### Bước 4

Kiểm thử bằng dữ liệu có:

- chuyến đã có `BagWeightKg`
- chuyến cũ chưa có `BagWeightKg`
- chuyến đã có `AllocatedBagCount`
- chuyến chưa hoàn tất cân lần 2
- cắt lệnh tạm và cắt lệnh ERP thật

## 8. Test case nghiệp vụ cần cover

1. Chuyến có `ActualAllocatedWeight = 20_180`, `BagWeightKg = 2_000`, `PlannedBagCount = 10` thì:
   - `Số lượng qua cân (bao)` ra `10`
   - `Khối lượng vỏ bao` đúng theo công thức chốt
2. Chuyến có `Weight2Time = 2026-06-20 15:10:00` thì `Ca = B`
3. Chuyến không có `BagWeightKg`, nhưng có `ActualAllocatedBagCount` thì cột `Bao` và `SL thực xuất (bao)` vẫn hiển thị được
4. Chuyến có dữ liệu lẻ ở `kg/bao thực tế` thì `KL lệch quy cách` chỉ hiển thị tối đa `2` số thập phân
5. Chuyến chưa hoàn tất cân lần 2 thì `Ngày xuất`, `Ca`, `SL thực xuất`, `KL lệch quy cách` phải có rule fallback rõ ràng hoặc để trống

## 9. Khuyến nghị chốt trước khi code

Để tránh làm sai nghiệp vụ, mình đề xuất chốt theo bộ quy tắc sau:

- `Ngày xuất` lấy theo `Weight2Time`
- `Ca` dùng quy tắc `A/B/C` đang có ở báo cáo
- `Số lượng qua cân (kg)` lấy `ActualAllocatedWeight ?? NetWeight`
- `Số lượng qua cân (bao)` lấy `Round(kg / BagWeightKg)` với fallback về số bao đã lưu
- `Khối lượng vỏ bao (kg)` = `bao * BagWeightKg`
- `SL thực xuất (kg)` = `Số lượng qua cân - khối lượng vỏ`
- `SL thực xuất (bao)` = số bao thực tế của chuyến
- `KL lệch quy cách (kg/bao)` = `kg/bao thực tế - kg/bao chuẩn`
- `KL lệch quy cách (kg/chuyến)` = `kg lệch theo bao * số bao`
- `Rách vỡ hồi về` tạm để `0` hoặc `rỗng` nếu hiện chưa có nguồn dữ liệu rõ ràng

Nếu bạn duyệt đúng theo bộ quy tắc này, bước code sau sẽ khá thẳng và ít phải sửa đi sửa lại.
