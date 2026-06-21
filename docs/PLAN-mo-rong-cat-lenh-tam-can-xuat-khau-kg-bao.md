# Plan: Mở rộng cắt lệnh tạm ở màn Cân xuất khẩu theo kg/bao

## 1. Mục tiêu

Nâng cấp màn `Cân xuất khẩu` để khi tạo và thao tác với `Cắt lệnh tạm` có thể nhập và hiển thị đầy đủ:

- Khách hàng
- Sản phẩm
- Số lượng đặt
- Trọng lượng vỏ (`kg`, trường mới)
- Trọng lượng bao (`kg`, trường mới)
- Ghi chú

Đồng thời thay cách hiển thị số lượng từ một giá trị đơn sang hai nhánh song song:

- `Số lượng đặt`: tách `kg` và `bao`
- `Lũy kế`: tách `kg` và `bao`
- `Còn lại`: tách `kg` và `bao`

Người dùng nhập số lượng đặt theo `kg`, sau đó hệ thống tự tính `số bao` theo công thức:

```text
Số bao = Số lượng đặt (kg) / Trọng lượng bao (kg)
```

Quy ước nghiệp vụ đã chốt cho phase này:

- `Số bao` luôn là `số nguyên tuyệt đối`
- trong modal `Tạo cắt lệnh tạm`, mọi trường đều bắt buộc ngoại trừ `Ghi chú`

Mục tiêu UI là tận dụng đúng khoảng không hiện tại của màn hình, không mở rộng layout tổng thể, ưu tiên bố cục gọn theo dạng `1 dòng / 3 cột field`.

## 2. Hiện trạng code đã rà soát

### 2.1 Luồng tạo cắt lệnh tạm hiện tại

Trong [ExportWeighingViewModel.cs](/G:/Source-code/pmcan_C#/src/StationApp.UI/ViewModels/ExportWeighingViewModel.cs), nút `Tạo cắt lệnh tạm` hiện chỉ:

- hỏi xác nhận
- gọi `CreateTemporaryExportCutOrderUseCase`
- tạo bản ghi tạm gần như rỗng

Request hiện tại là `CreateTemporaryExportCutOrderRequest` trong [Dtos.cs](/G:/Source-code/pmcan_C#/src/StationApp.Application/DTOs/Dtos.cs), mới có:

- `CustomerCode`, `CustomerName`
- `ProductCode`, `ProductName`, `ProductType`
- `PlannedWeight`
- `BagCount`
- `Notes`

Chưa có:

- `TareWeightKg` hoặc trường tương đương cho `Trọng lượng vỏ`
- `BagWeightKg` hoặc trường tương đương cho `Trọng lượng bao`
- phần nhập liệu/hiển thị riêng cho `kg` và `bao`
- modal nhập đủ thông tin trước khi tạo

### 2.2 Dữ liệu cut order hiện tại

Entity [CutOrder.cs](/G:/Source-code/pmcan_C#/src/StationApp.Domain/Entities/CutOrder.cs) đang có:

- `PlannedWeight`
- `BagCount`
- các thông tin khách hàng, sản phẩm, ghi chú

Chưa có cột để lưu:

- `Trọng lượng vỏ`
- `Trọng lượng bao`

### 2.3 Dữ liệu danh sách cắt lệnh xuất khẩu

`ExportScaleCutOrderListItem` trong [Dtos.cs](/G:/Source-code/pmcan_C#/src/StationApp.Application/DTOs/Dtos.cs) hiện chỉ trả:

- `PlannedWeight`
- `AccumulatedWeight`
- `RemainingWeight`
- `TripCount`, `LastTripAt`

Chưa có:

- `PlannedBagCount`
- `AccumulatedBagCount`
- `RemainingBagCount`
- `TareWeightKg`
- `BagWeightKg`

### 2.4 UI hiện tại

View [ExportWeighingView.xaml](/G:/Source-code/pmcan_C#/src/StationApp.UI/Views/ExportWeighingView.xaml) đang có:

- khối thông tin cắt lệnh ở góc trên trái với layout 2 cặp cột label/value
- `SL đặt (kg)`, `Lũy kế (kg)`, `Còn lại (kg)` chỉ có 1 giá trị/kg
- `Khách hàng`, `Sản phẩm`, `Ghi chú` đang là read-only

Điểm thuận lợi:

- app đã có autocomplete cho master data khách hàng/sản phẩm ở các màn khác qua `AutocompleteService`
- có thể tái sử dụng `AutocompleteTextBox` và `AutocompleteFieldType.Customer`, `ProductCode`, `ProductName`

## 3. Phạm vi thay đổi đề xuất

### 3.1 Tầng dữ liệu

Mở rộng bảng `cut_orders` thêm 2 cột mới:

- `TareWeightKg decimal(18,3) null`
- `BagWeightKg decimal(18,3) null`

Các chỗ cần cập nhật đồng bộ:

- `CutOrder` entity
- `CutOrderEntityConfiguration`
- `SchemaCompatibilityBootstrapper`
- `StationDbContextModelSnapshot`
- migration mới
- nếu central sync đang serialize full entity thì chỉ cần đảm bảo hai property mới được map và đồng bộ như các field cut order khác

### 3.2 DTO/use case/repository

Mở rộng `CreateTemporaryExportCutOrderRequest` thêm:

- `decimal PlannedWeightKg`
- `decimal TareWeightKg`
- `decimal BagWeightKg`

Khuyến nghị triển khai:

- UI nhập trực tiếp `PlannedWeightKg`
- request xuống application truyền `PlannedWeightKg`
- business chỉ làm việc với đơn vị chuẩn là `kg`

Mở rộng `ExportScaleCutOrderListItem` thêm:

- `int? PlannedBagCountDisplay`
- `int AccumulatedBagCountDisplay`
- `int RemainingBagCountDisplay`
- `decimal? TareWeightKg`
- `decimal? BagWeightKg`

Lưu ý:

- `BagCount` trong entity hiện đã là `int?`
- hướng này phù hợp trực tiếp với yêu cầu nghiệp vụ mới
- phần hiển thị `bao` ở màn export cũng sẽ dùng `int`, không dùng `decimal`

`CutOrderRepository.GetActiveExportScaleCutOrdersAsync()` cần bổ sung:

- lấy thêm `TareWeightKg`, `BagWeightKg`
- tính thêm `AccumulatedBagCountDisplay`, `RemainingBagCountDisplay`

### 3.3 UI/ViewModel màn Cân xuất khẩu

Thay đổi khối thông tin cắt lệnh ở panel trái trên theo hướng:

- label căn trái thay vì căn phải
- giảm độ rộng label
- chia thành lưới `1 hoặc 2 hoặc 3 hoặc 4 cột field / 1 dòng` khi đủ chỗ

Đề xuất bố cục:

Dòng 1:

- `Mã cắt lệnh`
- `Sản phẩm`

Dòng 2:

- `SL đặt (kg)` input/edit
- `SL đặt (bao)` readonly/tự tính
- `Lũy kế (kg)` readonly
- `Lũy kế (bao)` readonly

Dòng 3:

- `Còn lại (kg)` readonly
- `Còn lại (bao)` readonly
- `Trọng lượng vỏ (kg)` input/edit
- `Trọng lượng bao (kg)` input/edit

Dòng 4:

- `Khách hàng`

Dòng 5:

- `Ghi chú` full row nếu cần
- `Chuyến cuối` readonly nếu cần giữ

Với cắt lệnh ERP thật:

- vẫn hiển thị read-only như hiện tại

Với cắt lệnh tạm:

- sau khi tạo qua modal, form thông tin cắt lệnh sẽ được fill dữ liệu tương ứng để người dùng theo dõi
- cho phép chỉnh sửa lại trực tiếp các field nếu cần:
  - khách hàng
  - sản phẩm
  - số lượng đặt (kg)
  - trọng lượng vỏ
  - trọng lượng bao
  - ghi chú

Ngoài form chính, cần bổ sung 1 modal tạo cắt lệnh tạm:

- mở khi người dùng bấm `Tạo cắt lệnh tạm`
- chứa các field nhập liệu đúng theo nghiệp vụ:
  - khách hàng
  - sản phẩm
  - số lượng đặt (kg)
  - trọng lượng vỏ (kg)
  - trọng lượng bao (kg)
  - ghi chú
- có nút `Lưu` và `Hủy`
- mọi field trong modal đều bắt buộc, ngoại trừ `Ghi chú`
- sau khi lưu thành công:
  - tạo bản ghi cắt lệnh tạm
  - tự động chọn dòng vừa tạo trong grid
  - fill toàn bộ dữ liệu lên form thông tin cắt lệnh ở panel trái

### 3.4 Cách nhập khách hàng/sản phẩm

Ưu tiên dùng autocomplete có sẵn:

- `CustomerCode` + `CustomerName`
- `ProductCode` + `ProductName`

Đề xuất UX:

- hiển thị 2 ô chính là `Khách hàng` và `Sản phẩm`
- khi chọn item autocomplete thì tự fill cả mã và tên nền
- nếu người dùng nhập tay tên mà không chọn từ master, vẫn cho phép lưu cắt lệnh tạm

Lý do:

- cắt lệnh tạm có thể phát sinh trước khi ERP tạo bản ghi chuẩn
- không nên khóa người dùng chỉ vì chưa có master data đầy đủ

## 4. Quy tắc tính toán đề xuất

### 4.1 Nhập số lượng đặt

Người dùng nhập trực tiếp:

- `PlannedWeightKg`

Quy tắc format:

- `kg`: hiển thị số nguyên hoặc tối đa 2 chữ số thập phân nếu cần đồng nhất với phần export hiện tại

### 4.2 Tính số bao

```text
PlannedBagCountDisplay = PlannedWeightKg / BagWeightKg
```

Vì nghiệp vụ đã chốt `Số bao` luôn là số nguyên tuyệt đối, áp dụng thống nhất:

- nếu có `BagWeightKg` thì:
  - `PlannedBagCountDisplay = round(PlannedWeightKg / BagWeightKg, 0, MidpointRounding.AwayFromZero)`
  - `AccumulatedBagCountDisplay = round(AccumulatedWeightKg / BagWeightKg, 0, MidpointRounding.AwayFromZero)`
  - `RemainingBagCountDisplay = max(0, PlannedBagCountDisplay - AccumulatedBagCountDisplay)`
- `CutOrder.BagCount` lưu đúng bằng `PlannedBagCountDisplay`

Quy tắc làm tròn số bao:

- dùng `MidpointRounding.AwayFromZero`
- ví dụ:
  - `12490 / 50 = 249.8` -> `250 bao`
  - `12475 / 50 = 249.5` -> `250 bao`
  - `12424 / 50 = 248.48` -> `248 bao`

Quy tắc cảnh báo:

- nếu `PlannedWeightKg / BagWeightKg` ra số lẻ, hệ thống vẫn cho phép lưu
- nhưng phải hiển thị cảnh báo rõ cho người dùng ngay tại modal trước khi xác nhận lưu
- nội dung cảnh báo nên theo hướng:
  - `Số lượng đặt chia cho trọng lượng bao đang ra số lẻ, hệ thống sẽ làm tròn số bao theo quy tắc chuẩn.`

### 4.3 Lũy kế và còn lại theo bao

Hiện `Lũy kế` đang tính từ tổng `ActualAllocatedWeight`.

Theo hướng mới:

- `Lũy kế (kg)` giữ nguyên logic hiện tại
- `Lũy kế (bao)` tính lại từ `AccumulatedWeight / BagWeightKg`, làm tròn về số nguyên theo rule ở trên, không phụ thuộc `ActualAllocatedBagCount`
- `Còn lại (kg)` = `PlannedWeightKg - AccumulatedWeightKg`
- `Còn lại (bao)` = `PlannedBagCountDisplay - AccumulatedBagCountDisplay`

Lý do:

- luồng export tạm này cần bám theo nhập liệu mới của cắt lệnh tạm
- tránh bị lệch vì các line cũ chưa chắc đã có `ActualAllocatedBagCount` chuẩn theo bag weight mới

## 5. Luồng thao tác sau khi nâng cấp

### 5.1 Tạo cắt lệnh tạm

Khi bấm `Tạo cắt lệnh tạm`:

1. App mở modal `Tạo cắt lệnh tạm`.
2. Trong modal, người dùng bắt buộc nhập đầy đủ:
   - khách hàng
   - sản phẩm
   - số lượng đặt theo kg
   - trọng lượng vỏ
   - trọng lượng bao
3. `Ghi chú` là trường không bắt buộc.
4. Trong lúc nhập:
   - hệ thống tự tính preview `Số bao`
   - nếu `Số lượng đặt / Trọng lượng bao` ra số lẻ thì hiển thị cảnh báo ngay trên modal
5. Nếu thiếu bất kỳ trường bắt buộc nào thì không cho nhấn lưu thành công.
6. Nhấn `Lưu`.
7. Hệ thống tạo cut order tạm trong DB local.
8. Sau khi lưu thành công:
   - tự chọn cut order vừa tạo
   - fill dữ liệu từ modal lên form thông tin cắt lệnh ở panel trái
   - form chính chuyển sang trạng thái có thể theo dõi/chỉnh sửa tiếp nếu cần

Lưu ý:

- modal là nơi nhập liệu ban đầu để tránh làm form chính bị trống hoặc nhấp nháy khi mới tạo bản ghi
- form chính vẫn là nơi theo dõi và chỉnh sửa lại sau khi đã tạo xong

### 5.2 Tạo chuyến xe

Chỉ cho phép `Tạo chuyến xe` khi:

- đã chọn cắt lệnh
- nếu là cắt lệnh tạm thì cut order đó phải được tạo hợp lệ từ modal với đầy đủ dữ liệu bắt buộc

### 5.3 Map sang cắt lệnh thật

Khi map temp sang ERP cut order thật:

- giữ nguyên logic map hiện tại
- đồng thời bổ sung mapping cho hai field mới:
  - `TareWeightKg`
  - `BagWeightKg`

Quy tắc ưu tiên dữ liệu:

- nếu cut order thật từ ERP chưa có hai field này thì copy từ temp
- nếu sau này ERP cũng có dữ liệu tương ứng thì cần chốt rule overwrite ở phase sau

## 6. Danh sách hạng mục code cần làm

### 6.1 Domain/Infrastructure

- cập nhật [CutOrder.cs](/G:/Source-code/pmcan_C#/src/StationApp.Domain/Entities/CutOrder.cs)
- cập nhật [CutOrderEntityConfiguration.cs](/G:/Source-code/pmcan_C#/src/StationApp.Infrastructure/Persistence/Configurations/CutOrderEntityConfiguration.cs)
- bổ sung bootstrap cột trong [SchemaCompatibilityBootstrapper.cs](/G:/Source-code/pmcan_C#/src/StationApp.Infrastructure/Persistence/SchemaCompatibilityBootstrapper.cs)
- tạo migration
- cập nhật repository query ở [CutOrderRepository.cs](/G:/Source-code/pmcan_C#/src/StationApp.Infrastructure/Repositories/CutOrderRepository.cs)

### 6.2 Application

- mở rộng [Dtos.cs](/G:/Source-code/pmcan_C#/src/StationApp.Application/DTOs/Dtos.cs)
- cập nhật [ExportScaleUseCases.cs](/G:/Source-code/pmcan_C#/src/StationApp.Application/UseCases/ExportScaleUseCases.cs)
- thêm use case cập nhật thông tin cắt lệnh tạm, ví dụ:
  - `UpdateTemporaryExportCutOrderDetailsUseCase`

Có thể cần thêm DTO riêng cho modal:

- `CreateTemporaryExportCutOrderDialogRequest`
- hoặc tái sử dụng trực tiếp request hiện có nếu muốn giảm số lớp trung gian

Không nên nhét logic update vào `CreateTemporaryExportCutOrderUseCase` vì:

- tạo mới và cập nhật là hai hành vi khác nhau
- UI inline edit sẽ cần lưu nhiều lần sau khi cut order đã tồn tại

### 6.3 UI

- cập nhật [ExportWeighingViewModel.cs](/G:/Source-code/pmcan_C#/src/StationApp.UI/ViewModels/ExportWeighingViewModel.cs)
- cập nhật [ExportWeighingView.xaml](/G:/Source-code/pmcan_C#/src/StationApp.UI/Views/ExportWeighingView.xaml)
- thêm modal/viewmodel cho `Tạo cắt lệnh tạm`
- bổ sung autocomplete state cho:
  - khách hàng
  - sản phẩm
- bổ sung command:
  - `SaveTemporaryCutOrderDetailsCommand`
- bổ sung command mở modal:
  - `CreateTemporaryCutOrderCommand`
- bổ sung property tính toán:
  - `PlannedWeightKgInput`
  - `SelectedCutOrderPlannedWeightKg`
  - `SelectedCutOrderPlannedBagCountDisplay`
  - `SelectedCutOrderAccumulatedBagCountDisplay`
  - `SelectedCutOrderRemainingBagCountDisplay`
- bổ sung property cảnh báo ở modal:
  - `BagCountPreview`
  - `HasFractionalBagWarning`
  - `FractionalBagWarningMessage`

## 7. Validation đề xuất

Trong modal `Tạo cắt lệnh tạm`, tất cả field đều bắt buộc:

- `Khách hàng`: bắt buộc
- `Sản phẩm`: bắt buộc
- `Số lượng đặt (kg)`: bắt buộc, phải > 0
- `Trọng lượng bao (kg)`: bắt buộc, phải > 0
- `Trọng lượng vỏ (kg)`: bắt buộc, phải >= 0
- `Ghi chú`: không bắt buộc

Validation consistency:

- khi có `BagWeightKg` và `PlannedWeightKg`, hệ thống phải luôn tính được `BagCount` là số nguyên
- không cho người dùng nhập/sửa trực tiếp ô `bao`
- nếu phép chia ra số lẻ thì phải hiển thị cảnh báo trước khi lưu
- nếu thiếu bất kỳ trường bắt buộc nào thì không cho lưu

Validation UI nên hiển thị mềm:

- highlight field
- cảnh báo inline ngay trên modal tạo cắt lệnh tạm cho trường hợp chia ra số lẻ
- thông báo lỗi ngắn ngay cạnh field hoặc dưới modal cho trường hợp thiếu dữ liệu

Không nên dùng message box chặn liên tục khi user đang chỉnh form.

## 8. Test case cần verify

### 8.1 Tạo và lưu cắt lệnh tạm

- bấm `Tạo cắt lệnh tạm` thì modal hiển thị đúng đủ field
- thử bỏ trống từng trường bắt buộc trong modal thì không cho lưu
- để trống `Ghi chú` vẫn cho lưu
- nhập đủ khách hàng/sản phẩm/số lượng/trọng lượng
- lưu thành công, reload màn vẫn thấy dữ liệu
- sau khi lưu, dữ liệu được fill xuống form thông tin cắt lệnh
- chuyển sang dòng khác rồi quay lại, dữ liệu không mất

### 8.2 Quy đổi kg/bao

- nhập `12500 kg` và `50 kg/bao` thì:
  - `SL đặt (bao) = 250`
- nhập `12475 kg` và `50 kg/bao` thì:
  - `SL đặt (bao) = 250`
  - modal hiển thị cảnh báo chia ra số lẻ trước khi lưu
- sửa `Trọng lượng bao` thì số bao cập nhật ngay

### 8.3 Lũy kế và còn lại

- tạo 1 chuyến cân xong 1 phần
- `Lũy kế (kg)` tăng đúng theo `ActualAllocatedWeight`
- `Lũy kế (bao)` tính đúng theo `AccumulatedWeight / BagWeightKg`
- `Còn lại (kg)` và `Còn lại (bao)` giảm đúng

### 8.4 Không ảnh hưởng cắt lệnh thật

- chọn cut order ERP thật, panel chỉ read-only
- các luồng tạo chuyến/cân/chốt tổng cũ vẫn chạy
- map temp sang thật không làm mất dữ liệu cũ

### 8.5 Sync

- dữ liệu mới ở `cut_orders` sync được lên central
- máy khác kéo về vẫn đọc được `TareWeightKg`, `BagWeightKg`

## 9. Rủi ro và điểm cần chốt trước khi code

### 9.1 Điểm cần chốt

- `Trọng lượng vỏ` chỉ để lưu tham chiếu hay sẽ tham gia tính toán nghiệp vụ ở bước sau
- khi map sang ERP cut order thật, có copy `BagWeightKg/TareWeightKg` sang cut order thật hay chỉ giữ ở temp

### 9.2 Khuyến nghị thực hiện

Để an toàn và ít phá vỡ code hiện tại, nên làm theo hướng:

- thêm 2 cột mới vào `cut_orders`
- giữ `BagCount` cũ để tương thích
- thêm các giá trị `bao` dạng tính toán số nguyên riêng cho màn `Cân xuất khẩu`
- chỉnh panel trái trên thành form hỗn hợp `read-only/editable` theo loại cut order
- nhập liệu ban đầu qua modal, sau đó đổ dữ liệu xuống form chính

## 10. Kết quả kỳ vọng sau khi hoàn thành

Sau khi xong, người dùng có thể:

- tạo cắt lệnh tạm qua modal nhập liệu đầy đủ
- nhận cảnh báo ngay nếu `SL đặt / Trọng lượng bao` ra số lẻ
- nhập nhanh đủ dữ liệu khách hàng/sản phẩm/số lượng/trọng lượng/ghi chú
- theo dõi đồng thời số lượng theo `kg` và `bao`
- không cần rời màn hình chính để theo dõi các chuyến xe
- vẫn giữ nguyên các luồng export hiện có cho cắt lệnh ERP thật
