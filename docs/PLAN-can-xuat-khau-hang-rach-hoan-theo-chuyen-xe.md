# Kế hoạch chi tiết: Bổ sung nghiệp vụ cân hàng rách hoàn ở luồng Cân xuất khẩu

## 1. Mục tiêu

Bổ sung nghiệp vụ để một `chuyến xe` trong luồng `Cân xuất khẩu` có thể được đánh dấu là `hàng rách hoàn`.

Khi một chuyến được đánh dấu là hàng rách hoàn:

- vẫn dùng chính chuyến xe đó để cân
- vẫn hiển thị trong grid `Danh sách chuyến xe`
- có checkbox trên giao diện để bật hoặc tắt
- dòng đó hiển thị chữ màu đỏ
- các số liệu `Lũy kế kg`, `Lũy kế bao`, `Còn lại kg`, `Còn lại bao` phải tính theo giá trị ròng
- phiếu cân của chuyến đó phải hiển thị thêm text `Hàng rách vỡ` ở cuối `Ghi chú`

## 2. Quyết định nghiệp vụ đã chốt

1. Vẫn cho phép đổi checkbox `Hàng hoàn` sau khi đã `IN PC`
2. Với chuyến được tích checkbox, phiếu cân phải append thêm text `Hàng rách vỡ` vào cuối `Ghi chú`
3. `ExportFinalizedWeight` và các báo cáo xuất liên quan phải tính theo giá trị ròng sau khi trừ hàng hoàn

## 3. Hiện trạng code đã rà soát

### 3.1 Grid chuyến xe hiện tại

Grid `Danh sách chuyến xe` ở [ExportWeighingView.xaml](/G:/Source-code/pmcan_C#/src/StationApp.UI/Views/ExportWeighingView.xaml:468) hiện có:

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

Hiện chưa có cột checkbox `Hàng hoàn`.

### 3.2 Dữ liệu line hiện tại

Entity [WeighingSessionLine](/G:/Source-code/pmcan_C#/src/StationApp.Domain/Entities/WeighingSessionLine.cs:1) hiện đã có:

- `ActualAllocatedWeight`
- `ActualAllocatedBagCount`
- `BagCountDisplay`
- `LineStatus`
- `HasPrintedDeliveryTicket`

Hiện chưa có field:

- `IsReturnedBrokenTrip`

### 3.3 Logic số bao hiện tại

Sau khi rà lại code, hiện có 2 lớp dữ liệu số bao:

- dữ liệu gốc:
  - `ActualAllocatedBagCount`
  - `BagCountDisplay`
- dữ liệu fallback:
  - tự quy đổi từ `kg / BagWeightKg` bằng helper dùng chung

Điểm quan trọng:

- plan phải bám theo thực tế mới này
- không nên mô tả số bao chỉ còn là `ActualAllocatedBagCount` như trước nữa

### 3.4 Logic progress hiện tại

Ở [CutOrderRepository.cs](/G:/Source-code/pmcan_C#/src/StationApp.Infrastructure/Repositories/CutOrderRepository.cs:1037), phần progress cắt lệnh xuất hiện đang tính theo kiểu cộng dương toàn bộ:

```text
AccumulatedWeight = SUM(ActualAllocatedWeight)
AccumulatedBagCountDisplay = quy đổi theo tổng kg
RemainingWeight = PlannedWeight - AccumulatedWeight
RemainingBagCountDisplay = PlannedBagCount - AccumulatedBagCountDisplay
```

Hiện chưa có khái niệm `trừ ngược` cho hàng hoàn.

### 3.5 Logic finalize export hiện tại

Ở [ExportScaleUseCases.cs](/G:/Source-code/pmcan_C#/src/StationApp.Application/UseCases/ExportScaleUseCases.cs:1291), `FinalizeExportCutOrderUseCase` hiện đang:

- lấy trips từ `GetExportVehicleTripsAsync`
- cộng dương `ActualAllocatedWeight`
- gán vào `ExportFinalizedWeight`

Tức là:

- code hiện tại chưa phản ánh đúng yêu cầu `giá trị ròng sau khi trừ hàng hoàn`

### 3.6 Logic phiếu cân hiện tại

Composer phiếu cân đang nằm ở [PrintContracts.cs](/G:/Source-code/pmcan_C#/src/StationApp.Application/Printing/PrintContracts.cs:188).

Hiện tại luồng export đã có logic dựng `Ghi chú` theo số bao, nên đây là điểm đúng để nối thêm hậu tố:

- `Hàng rách vỡ`

## 4. Điểm chưa rõ hoặc mâu thuẫn đã phát hiện và cách chốt lại

### 4.1 Text trên phiếu cân đang bị mô tả không thống nhất

Trong các bản plan trước đang bị lẫn giữa:

- `Hàng rách vỡ`
- `Hàng hoàn rách vỡ`
- `(Rách vỡ)`

Chốt lại một cách duy nhất:

- text đúng là `Hàng rách vỡ`

Format đề xuất:

- chuyến thường: `20 bao`
- chuyến hàng hoàn: `20 bao - Hàng rách vỡ`

Không dùng:

- `Hàng hoàn rách vỡ`
- `(Rách vỡ)`
- `(Hàng hoàn rách vỡ)`

### 4.2 Nguồn số bao trong plan cũ chưa bám theo code mới

Plan cũ mô tả số bao theo thứ tự:

1. `ActualAllocatedBagCount`
2. nếu không có thì tự quy đổi từ `kg / BagWeightKg`

Nhưng trạng thái code hiện tại đã có thêm:

- `weighing_session_lines.BagCountDisplay`

Chốt lại nguồn số bao theo thứ tự ưu tiên mới:

1. `ActualAllocatedBagCount`
2. `BagCountDisplay`
3. fallback tính từ `ActualAllocatedWeight / BagWeightKg` bằng helper dùng chung

Điều này áp dụng cho:

- cột `Bao` ở grid
- tính `Lũy kế bao`
- tính `Còn lại bao`
- `Ghi chú` trên phiếu cân export

### 4.3 Progress bao không nên mô tả là “quy đổi từ tổng kg” một cách cứng

Nếu plan chỉ ghi:

- `AccumulatedBagCountDisplay = quy đổi từ AccumulatedWeight`

thì sẽ sai với định hướng mới, vì từ giờ cần ưu tiên dữ liệu line đã lưu.

Chốt lại:

- `AccumulatedBagCountNet` phải cộng theo `signed bag count` của từng line
- mỗi line ưu tiên `ActualAllocatedBagCount`, sau đó `BagCountDisplay`, rồi mới fallback tính

### 4.4 Finalize export hiện tại đang mâu thuẫn với yêu cầu đã chốt

Yêu cầu đã chốt:

- `ExportFinalizedWeight` phải là giá trị ròng sau khi trừ hàng hoàn

Nhưng code hiện tại vẫn đang:

- cộng dương toàn bộ `ActualAllocatedWeight`

Chốt lại:

- plan phải coi đây là hạng mục bắt buộc sửa
- không để nó chỉ là mục “rà soát nếu cần”

### 4.5 Báo cáo xuất cũng phải coi là hạng mục bắt buộc sửa

Vì bạn đã chốt là:

- `Có` ảnh hưởng tới báo cáo

nên plan phải ghi rõ:

- phần báo cáo xuất là phạm vi bắt buộc
- không phải “nếu cần thì sửa”

## 5. Đề xuất mô hình dữ liệu

### 5.1 Thêm cờ ở cấp `WeighingSessionLine`

Đề xuất thêm cột mới vào bảng `weighing_session_lines`:

- `IsReturnedBrokenTrip bit not null default 0`

Tên property:

- `public bool IsReturnedBrokenTrip { get; set; }`

Lý do chọn lưu ở `WeighingSessionLine`:

- mọi số liệu chuyến export đang bám theo line
- số bao thực tế cũng đang được lưu ở line
- logic progress cắt lệnh export cũng group theo line

### 5.2 Dữ liệu cũ

Yêu cầu:

- tất cả line cũ mặc định `IsReturnedBrokenTrip = 0`

Không cần backfill theo suy luận.

## 6. Quy tắc nghiệp vụ sau khi chốt

### 6.1 Ý nghĩa checkbox

- `checked`: chuyến này là chuyến hàng rách hoàn
- `unchecked`: chuyến này là chuyến xuất bình thường

Checkbox này không đổi:

- số cân lần 1/lần 2
- net của chuyến
- số phiếu cân
- số phiếu giao nhận

Checkbox này làm đổi:

- cách hiển thị dòng
- cách tính progress
- nội dung `Ghi chú` trên phiếu cân
- số liệu finalize export
- số liệu báo cáo xuất

### 6.2 Công thức tính ròng

Ký hiệu:

- `W_i`: khối lượng thực của line i
- `B_i`: số bao thực của line i
- `R_i`: `IsReturnedBrokenTrip`

Công thức:

```text
SignedWeight_i = R_i ? -W_i : W_i
SignedBag_i = R_i ? -B_i : B_i
```

Tổng:

```text
AccumulatedWeightNet = SUM(SignedWeight_i)
AccumulatedBagCountNet = SUM(SignedBag_i)
RemainingWeight = PlannedWeight - AccumulatedWeightNet
RemainingBagCount = PlannedBagCount - AccumulatedBagCountNet
```

### 6.3 Quy tắc xác định `B_i`

Cho từng line, số bao thực phải lấy theo thứ tự:

1. `ActualAllocatedBagCount`
2. `BagCountDisplay`
3. `BagCountDisplayHelper.Resolve(ActualAllocatedWeight, BagWeightKg, null)`

### 6.4 Quy tắc đổi checkbox sau khi đã in PC

Đã chốt:

- vẫn cho phép đổi

Nhưng cần:

- confirm dialog rõ ràng
- refresh lại grid và summary ngay sau khi lưu
- các lần in phiếu cân sau phải phản ánh trạng thái mới

## 7. Đề xuất thay đổi UI

### 7.1 Grid Danh sách chuyến xe

Ở [ExportWeighingView.xaml](/G:/Source-code/pmcan_C#/src/StationApp.UI/Views/ExportWeighingView.xaml:468):

- thêm 1 cột checkbox bên phải `PGN`
- header: `HÀNG HOÀN`

Khuyến nghị dùng `DataGridTemplateColumn`.

### 7.2 Tô đỏ dòng

Nếu `IsReturnedBrokenTrip = true`:

- toàn bộ chữ trên dòng hiển thị màu đỏ

Khuyến nghị:

- không đổi background
- chỉ đổi foreground

## 8. Đề xuất thay đổi phiếu cân

### 8.1 Format ghi chú

Chốt format:

- chuyến thường: `20 bao`
- chuyến hàng hoàn: `20 bao - Hàng rách vỡ`

### 8.2 Phạm vi áp dụng

Chỉ áp dụng cho:

- phiếu cân của luồng `Cân xuất khẩu`
- chuyến có `IsReturnedBrokenTrip = true`

Không áp dụng cho:

- phiếu giao nhận
- luồng nội địa
- chuyến export không tích hàng hoàn

## 9. Phạm vi code bắt buộc

### 9.1 Domain + Persistence

Thêm:

- `WeighingSessionLine.IsReturnedBrokenTrip`

Cập nhật:

- entity configuration
- bootstrap schema
- migration snapshot nếu cần

### 9.2 DTO

Mở rộng `ExportVehicleTripListItem`:

- `bool IsReturnedBrokenTrip`

### 9.3 Repository

`GetExportVehicleTripsAsync()` phải trả thêm:

- `IsReturnedBrokenTrip`

Các API summary export phải đổi sang `signed progress`.

### 9.4 Use case toggle

Tạo use case riêng, ví dụ:

- `ToggleExportReturnedBrokenTripUseCase`

Input:

- `SessionLineId`
- `IsReturnedBrokenTrip`

Use case phải:

1. load line
2. validate line thuộc luồng export
3. update cờ
4. set `UpdatedAt`, `UpdatedBy`
5. set `SyncStatus = SYNC_QUEUED` nếu cần
6. save

### 9.5 ViewModel

Ở [ExportWeighingViewModel.cs](/G:/Source-code/pmcan_C#/src/StationApp.UI/ViewModels/ExportWeighingViewModel.cs:1):

- thêm command toggle checkbox
- confirm dialog
- reload `SelectedCutOrder`
- reload `Trips`
- giữ lại selected trip nếu còn

### 9.6 Phiếu cân

Ở [PrintContracts.cs](/G:/Source-code/pmcan_C#/src/StationApp.Application/Printing/PrintContracts.cs:188):

- composer phải nhận được trạng thái `IsReturnedBrokenTrip`
- nếu `true` thì append ` - Hàng rách vỡ` vào cuối `Notes`

### 9.7 Finalize export

Ở [ExportScaleUseCases.cs](/G:/Source-code/pmcan_C#/src/StationApp.Application/UseCases/ExportScaleUseCases.cs:1291):

- bắt buộc sửa `totalWeight`
- phải dùng tổng ròng, không được cộng dương toàn bộ như hiện tại

### 9.8 Báo cáo xuất

Ở [ExportSummaryReportServices.cs](/G:/Source-code/pmcan_C#/src/StationApp.Infrastructure/Services/ExportSummaryReportServices.cs:431):

- bắt buộc rà và sửa theo cùng quy tắc ròng

## 10. Kế hoạch triển khai đề xuất

### Bước 1

Thêm `IsReturnedBrokenTrip` vào `weighing_session_lines`.

### Bước 2

Mở rộng DTO và repository để grid trả được cờ này.

### Bước 3

Thêm use case toggle `Hàng hoàn`.

### Bước 4

Cập nhật UI grid:

- thêm checkbox
- tô đỏ dòng hàng hoàn

### Bước 5

Đổi logic `Lũy kế/Còn lại` sang `signed progress`.

### Bước 6

Cập nhật phiếu cân để append `Hàng rách vỡ`.

### Bước 7

Sửa `FinalizeExportCutOrderUseCase` sang tổng ròng.

### Bước 8

Sửa các báo cáo hoặc summary export liên quan sang tổng ròng.

### Bước 9

Test end-to-end với dữ liệu thật.

## 11. Test case cần cover

1. Chuyến export thường:
   - `IsReturnedBrokenTrip = false`
   - `Lũy kế` tăng đúng
   - `Còn lại` giảm đúng
   - phiếu cân không có `Hàng rách vỡ`

2. Chuyến hàng hoàn:
   - `IsReturnedBrokenTrip = true`
   - dòng hiển thị chữ đỏ
   - `Lũy kế` giảm đúng
   - `Còn lại` tăng đúng
   - phiếu cân có `Hàng rách vỡ`

3. Chuyến có `ActualAllocatedBagCount`:
   - `Lũy kế bao` dùng đúng số bao thực

4. Chuyến không có `ActualAllocatedBagCount` nhưng có `BagCountDisplay`:
   - vẫn tính đúng `Lũy kế bao`

5. Chuyến không có cả `ActualAllocatedBagCount` lẫn `BagCountDisplay` nhưng có `BagWeightKg`:
   - fallback tính từ helper dùng chung

6. Tắt checkbox sau khi đã in PC:
   - vẫn lưu được
   - lần in sau không còn `Hàng rách vỡ`

7. Finalize export:
   - `ExportFinalizedWeight` bằng tổng ròng

8. Báo cáo xuất:
   - phản ánh đúng việc có chuyến hàng hoàn

## 12. Kết luận rà soát

Sau khi rà lại, những điểm đã được làm rõ và chốt thống nhất là:

- text đúng trên phiếu cân là `Hàng rách vỡ`
- format đề xuất là `20 bao - Hàng rách vỡ`
- nguồn số bao phải ưu tiên `ActualAllocatedBagCount`, sau đó `BagCountDisplay`, rồi mới fallback tính
- `FinalizeExportWeight` là hạng mục bắt buộc sửa, không phải tùy chọn
- `Báo cáo xuất` cũng là hạng mục bắt buộc sửa, không phải tùy chọn

Với bản plan này, mình thấy không còn mâu thuẫn lớn nào nữa và có thể bám vào để code tương đối thẳng.
