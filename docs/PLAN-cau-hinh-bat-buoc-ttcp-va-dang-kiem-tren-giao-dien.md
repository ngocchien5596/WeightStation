# Plan cấu hình bắt buộc TTCP và đăng kiểm trên giao diện

## 1. Mục tiêu

Bổ sung chức năng cấu hình bằng giao diện để quản lý rule bắt buộc nhập `TTCP` và thông tin `đăng kiểm` ở màn `Danh sách xe vào`, với phạm vi đã chốt:

- Chỉ áp dụng cho trạm `QN01`.
- Chỉ áp dụng cho luồng `Danh sách xe vào`.
- Chỉ áp dụng cho phiếu `OUTBOUND`.
- Chỉ phân biệt 2 nhóm hàng:
  - `Bao`
  - `Rời/Xá`
- Không áp dụng cho `Hàng nhập`.
- Không áp dụng cho luồng `cân xuất khẩu`.

Mục tiêu nghiệp vụ:

- Người dùng tự bật/tắt rule trên giao diện, không cần sửa code.
- Rule được lưu theo trạm trong `station_operation_settings`.
- Khi bấm nút `Cân nội địa` ở màn `Danh sách xe vào`, hệ thống chặn thiếu dữ liệu đúng theo cấu hình.

## 2. Hiện trạng code đã rà soát lại

### 2.1 Hạ tầng cấu hình theo trạm đã có sẵn

Code hiện tại đã có đầy đủ nền để lưu cấu hình theo trạm:

- DTO:
  - [StationDtos.cs](g:/Source-code/pmcan_C#/src/StationApp.Application/DTOs/StationDtos.cs)
- Repository đọc/ghi `station_operation_settings`:
  - [MasterDataRepositories.cs](g:/Source-code/pmcan_C#/src/StationApp.Infrastructure/Repositories/MasterDataRepositories.cs)
- Service quản trị trạm:
  - [IStationScope.cs](g:/Source-code/pmcan_C#/src/StationApp.Application/Interfaces/IStationScope.cs)
  - [StationScopeServices.cs](g:/Source-code/pmcan_C#/src/StationApp.Infrastructure/Services/StationScopeServices.cs)
- Màn `Danh mục trạm`:
  - [StationMasterViewModel.cs](g:/Source-code/pmcan_C#/src/StationApp.UI/ViewModels/Settings/StationMasterViewModel.cs)
  - [StationMasterView.xaml](g:/Source-code/pmcan_C#/src/StationApp.UI/Views/Settings/StationMasterView.xaml)

Kết luận:

- Không cần tạo bảng cấu hình mới.
- Không cần tạo màn quản trị mới.
- Nên mở rộng tiếp `station_operation_settings` và màn `Danh mục trạm`.

### 2.2 Màn `Danh sách xe vào` hiện chưa có validate động cho TTCP/đăng kiểm ở luồng `Cân nội địa`

File chính:

- [IncomingVehicleListViewModel.cs](g:/Source-code/pmcan_C#/src/StationApp.UI/ViewModels/IncomingVehicleListViewModel.cs)

Hàm `ValidateIncomingDetailForm()` hiện chỉ chặn các trường cơ bản:

- `Số PTVC`
- `Tên tài xế` với phiếu xuất
- `Khách hàng`
- `Mã SP`
- `Tên SP`
- `SL đặt` với phiếu xuất

Hiện chưa có validate bắt buộc theo cấu hình cho:

- `TTCP`
- `Số ĐK xe`
- `Hạn ĐK xe`
- `Số ĐK mooc`
- `Hạn ĐK mooc`

Lưu ý thêm về flow:

- Nút `Lưu thay đổi` đi qua `SaveDetailAsync()`.
- Nút `Cân nội địa` đi qua `ConfirmEnterWeighingAsync()`.
- Hiện tại `ConfirmEnterWeighingAsync()` chỉ dùng `ValidateIncomingDetailForm()` ở nhánh `IsCreateMode`.
- Với bản ghi đã tồn tại, nhánh `ConfirmEnterWeighingAsync()` hiện chưa có lớp validate riêng cho `TTCP` và `đăng kiểm`, ngoài check hết hạn đang có.

### 2.3 Rule đăng kiểm hiện tại ở luồng `Cân nội địa` chỉ chặn trường hợp hết hạn

Trong `IncomingVehicleListViewModel` hiện có:

- `ValidateRegistrationExpiryForCreateSessionAsync(...)`
- `ValidateCurrentFormRegistrationExpiryForCreateSession(...)`

Hai nhánh này đang được gọi trong `ConfirmEnterWeighingAsync()` và chỉ xử lý:

- Nếu đã có ngày đăng kiểm và ngày đó nhỏ hơn hôm nay thì chặn.

Chưa xử lý:

- Bắt buộc phải nhập đầy đủ thông tin đăng kiểm trước khi chuyển xe vào `Cân nội địa`.

### 2.4 Rule TTCP hiện tại chủ yếu ảnh hưởng sau khi đã vào luồng cân

File liên quan:

- [WeighingSessionUseCases.cs](g:/Source-code/pmcan_C#/src/StationApp.Application/UseCases/WeighingSessionUseCases.cs)

Hiện tại `TTCP` đã được dùng trong luồng cân nội địa để tính toán và kiểm tra nghiệp vụ, nhưng chưa có lớp cấu hình giao diện để quyết định bắt buộc nhập ngay tại thời điểm bấm `Cân nội địa` ở màn `Danh sách xe vào`.

### 2.5 Cách xác định “trạm hiện tại” cần bám theo runtime context

Code hiện tại trong `StationScope`:

- Ưu tiên `StationRuntimeScope.StationCode` nếu người dùng đang thao tác trên một trạm đã chọn trong app.
- Nếu chưa có runtime station thì fallback `AppConfigKeys.DefaultStationCode`.
- Nếu vẫn chưa có thì fallback `AppConfigKeys.StationCode`.
- Cuối cùng mới fallback cứng `"QN01"`.

Kết luận quan trọng:

- Rule ở màn `Danh sách xe vào` phải dựa trên `IStationScope.GetCurrentStationCodeAsync(...)`.
- Không được lấy theo “dòng trạm đang chọn ở màn quản trị”.
- Không được hardcode `QN01` ở phần đọc setting hay validate, ngoài điều kiện so sánh phạm vi áp dụng.

### 2.6 Loại hàng `Rời/Xá` hiện đang normalize theo phạm vi hẹp

Trong [ProductTypes.cs](g:/Source-code/pmcan_C#/src/StationApp.Domain/Constants/ProductTypes.cs):

- `Bao`
- `Rời/Xá`
- alias thêm: `Roi/Xa`

Hiện chưa có bằng chứng code đang normalize thêm các biến thể khác như:

- `Rời`
- `Xá`
- `Bulk`

Kết luận:

- Plan không nên giả định đang cover nhiều alias hơn thực tế.
- Khi triển khai cần bám đúng `ProductTypes.Normalize(...)` hiện có, hoặc nếu mở rộng normalize thì phải coi đó là thay đổi có chủ đích.

### 2.7 Màn `Danh mục trạm` hiện có vùng setting gắn chặt với `CRUSHER/CLAY`

`StationMasterViewModel` đang dùng:

- `ActiveConfigMode`
- `IsOperationSettingsVisible`
- `OperationSettingsHeader`

Vùng `OperationSettings` hiện chỉ phục vụ:

- `Cân trạm đập`
- `Cân mỏ sét`

Kết luận:

- Không nên mô tả giải pháp theo hướng “cứ nhét thêm checkbox vào block hiện có”.
- Cấu hình TTCP/đăng kiểm cho `QN01` nên là một block độc lập trong màn `Danh mục trạm`, không phụ thuộc `CRUSHER/CLAY`.

## 3. Phạm vi triển khai chốt lại

Triển khai pha này chỉ gồm:

- Thêm setting mới trong `station_operation_settings`.
- Thêm UI quản trị cho riêng trạm `QN01` ở màn `Danh mục trạm`.
- Đọc rule theo `current runtime station`.
- Validate tại thời điểm bấm nút `Cân nội địa` ở màn `Danh sách xe vào`.

Chưa làm trong pha này:

- Không chặn ở nút `Lưu thay đổi`.
- Không áp dụng cho `cân xuất khẩu`.
- Không áp dụng cho `Hàng nhập`.
- Không thêm cảnh báo màu động hay ẩn/hiện field theo setting.

## 4. Thiết kế setting đề xuất

### 4.1 Key mới trong `station_operation_settings`

Đề xuất thêm 4 key:

- `incoming_require_ttcp_for_bagged_outbound`
- `incoming_require_registration_for_bagged_outbound`
- `incoming_require_ttcp_for_bulk_outbound`
- `incoming_require_registration_for_bulk_outbound`

Ý nghĩa:

- `...require_ttcp... = true`
  - Bắt buộc có `TTCP > 0` khi bấm `Cân nội địa`.
- `...require_registration... = true`
  - Bắt buộc có đủ:
    - `Số ĐK xe`
    - `Hạn ĐK xe`
    - `Số ĐK mooc` nếu xe có mooc
    - `Hạn ĐK mooc` nếu xe có mooc

### 4.2 Giá trị mặc định

Mặc định tất cả key mới = `false`.

Lý do:

- Không làm thay đổi hành vi hiện tại sau khi deploy.
- Chỉ khi admin bật trên giao diện thì rule mới có hiệu lực.

## 5. Thay đổi tầng Domain và Application

### 5.1 Constants cho key mới

Tạo constants riêng để tránh string literal rải rác.

Đề xuất file mới:

- `src/StationApp.Domain/Constants/IncomingVehicleOperationSettingKeys.cs`

### 5.2 Mở rộng `StationOperationSettingsDto`

File:

- [StationDtos.cs](g:/Source-code/pmcan_C#/src/StationApp.Application/DTOs/StationDtos.cs)

Cần thêm các property:

- `IncomingRequireTtcpForBaggedOutbound`
- `IncomingRequireRegistrationForBaggedOutbound`
- `IncomingRequireTtcpForBulkOutbound`
- `IncomingRequireRegistrationForBulkOutbound`

Yêu cầu:

- `Defaults` phải set rõ ràng `false`.
- Tên property theo nghĩa nghiệp vụ, không theo raw key DB.

### 5.3 Model rule nội bộ cho màn `Danh sách xe vào`

Đề xuất thêm model nội bộ, ví dụ:

- `IncomingVehicleComplianceRules`

Gồm 2 nhóm:

- `BaggedOutbound`
- `BulkOutbound`

Mỗi nhóm có:

- `RequireTtcpOnCreateSession`
- `RequireRegistrationOnCreateSession`

### 5.4 Provider đọc rule theo trạm hiện tại

Đề xuất thêm service dùng chung:

- `IIncomingVehicleComplianceSettingsProvider`

Trách nhiệm:

- Gọi `IStationScope.GetCurrentStationCodeAsync(...)`
- Nếu station hiện tại khác `QN01` thì trả về rule mặc định tắt toàn bộ
- Nếu station hiện tại là `QN01` thì đọc `station_operation_settings` và map sang model rule

Lợi ích:

- `IncomingVehicleListViewModel` không phải tự đọc dictionary thô
- Điều kiện `QN01 only` được gom ở một chỗ, dễ kiểm soát

## 6. Thay đổi ở service quản trị trạm

File chính:

- [StationScopeServices.cs](g:/Source-code/pmcan_C#/src/StationApp.Infrastructure/Services/StationScopeServices.cs)

Hiện service này đã:

- Load settings theo `StationOperationSettingsDto`
- Save settings vào dictionary rồi gọi `IStationOperationSettingsRepository.SaveSettingsAsync(...)`

Cần mở rộng:

- Load thêm 4 key mới ở `LoadOperationSettingsSetsAsync(...)`
- Save thêm 4 key mới ở `SaveStationAsync(...)`
- Giữ nguyên cơ chế sync payload `OperationSettings`

Kết luận:

- Không cần thay kiến trúc sync.
- Chỉ cần bảo đảm 4 key mới được đi vào payload như các setting khác.

## 7. Thay đổi UI quản trị

### 7.1 Nguyên tắc hiển thị

Màn `Danh mục trạm` cần thêm một block cấu hình độc lập, không dùng chung `ActiveConfigMode` của `CRUSHER/CLAY`.

Block này chỉ hiển thị khi:

- Đang sửa trạm có `EditStationCode = QN01`

Điều này giúp:

- Admin chỉ thấy đúng cấu hình cần quản lý cho `QN01`
- Không gây hiểu nhầm cho các trạm khác
- Không làm rối vùng cấu hình cân trạm đập và mỏ sét hiện hữu

### 7.2 Nội dung block cấu hình

Tên gợi ý:

- `Quy định bắt buộc hồ sơ xe vào`

Gồm 2 nhóm checkbox:

- `Xuất hàng - Bao`
  - `Bắt buộc TTCP khi bấm Cân nội địa`
  - `Bắt buộc đăng kiểm khi bấm Cân nội địa`
- `Xuất hàng - Rời/Xá`
  - `Bắt buộc TTCP khi bấm Cân nội địa`
  - `Bắt buộc đăng kiểm khi bấm Cân nội địa`

### 7.3 Lưu ý UX

Nên có chú thích ngắn:

- `Đăng kiểm` nghĩa là yêu cầu đủ số và hạn đăng kiểm xe.
- Nếu có `mooc` thì phải nhập đủ số và hạn đăng kiểm mooc.
- Rule này chỉ áp dụng khi bấm `Cân nội địa` ở màn `Danh sách xe vào` của trạm `QN01`.

## 8. Thay đổi màn `Danh sách xe vào`

### 8.1 Điểm gắn validate đúng theo code hiện tại

Luồng cần gắn rule mới là:

- `ConfirmEnterWeighingAsync()`

Không gắn vào:

- `SaveDetailAsync()`

Lý do:

- Đây là đúng hành vi người dùng vừa chốt lại.
- Nút `Cân nội địa` mới là thời điểm cần chặn để đảm bảo đủ hồ sơ trước khi tạo/chuyển vào lượt cân nội địa.

### 8.2 Cách đọc rule

Trong `IncomingVehicleListViewModel`:

- Load rule từ `IIncomingVehicleComplianceSettingsProvider`
- Có thể lazy-load trước khi validate hoặc load một lần rồi cache trong ViewModel

Đề xuất:

- Lazy-load trước lúc validate trong `ConfirmEnterWeighingAsync()` để giảm rủi ro stale config khi admin vừa đổi setting

### 8.3 Điều kiện áp dụng

Chỉ validate theo setting nếu đồng thời thỏa:

- `current station = QN01`
- `FormTransactionType = OUTBOUND`
- `ProductType` sau normalize là `Bao` hoặc `Rời/Xá`

Nếu không thỏa:

- Bỏ qua rule mới
- Giữ nguyên hành vi cũ

### 8.4 Quy tắc validate chi tiết

Nếu rule `RequireTtcpOnCreateSession = true`:

- `TtcpWeight` phải có giá trị
- `TtcpWeight > 0`

Nếu rule `RequireRegistrationOnCreateSession = true`:

- Luôn bắt buộc:
  - `VehicleRegistrationNo`
  - `VehicleRegistrationExpiry`
- Nếu có `FormMoocNumber`:
  - bắt buộc `MoocRegistrationNo`
  - bắt buộc `MoocRegistrationExpiry`

Ngoài ra:

- Nếu có ngày đăng kiểm nhưng đã hết hạn thì vẫn chặn như logic hiện có

### 8.5 Vị trí kiểm tra cụ thể trong `ConfirmEnterWeighingAsync()`

Plan cần bám đúng 2 nhánh hiện có:

- `IsCreateMode = true`
  - Hiện đang gọi `ValidateIncomingDetailForm()`
  - Cần bổ sung validate rule mới trước khi `CreateInboundRegistrationFromFormAsync(...)`
- `IsCreateMode = false`
  - Hiện lấy `selectedVehicles`, xác định `selectedIds`, rồi check hết hạn đăng kiểm
  - Cần bổ sung validate rule mới cho bản ghi đang chọn trước khi tạo hoặc gắn vào lượt cân

Điểm quan trọng:

- Rule mới phải chạy cho cả bản ghi mới nhập trên form và bản ghi đã tồn tại đang được chọn để `Cân nội địa`.
- Nhưng vẫn không được ảnh hưởng đến thao tác lưu nháp/cập nhật thông tin qua `SaveDetailAsync()`.

### 8.6 Message lỗi

Message cần cụ thể theo từng trường hợp, ví dụ:

- `Bắt buộc nhập TTCP trước khi chuyển xe vào Cân nội địa theo cấu hình trạm QN01.`
- `Bắt buộc nhập Số ĐK xe và Hạn ĐK xe trước khi chuyển xe vào Cân nội địa theo cấu hình trạm QN01.`
- `Xe có mooc nên bắt buộc nhập đủ Số ĐK mooc và Hạn ĐK mooc trước khi chuyển xe vào Cân nội địa theo cấu hình trạm QN01.`

## 9. Đồng bộ dữ liệu và tương thích

Không cần migration schema mới vì:

- Bảng `station_operation_settings` đã tồn tại
- `SaveSettingsAsync(...)` đã có cơ chế upsert theo `StationCode + SettingKey`

Lưu ý:

- Các key mới phải xuất hiện trong `SaveStationAsync(...)` để được tạo bản ghi khi admin lưu
- Các key mới phải được map trong `LoadOperationSettingsSetsAsync(...)` để UI đọc lại đúng trạng thái
- Clone/seed script nếu đang dùng `station_operation_settings` thì không cần đổi kiến trúc, chỉ cần chấp nhận thêm key mới

## 10. Danh sách file dự kiến sửa

- `src/StationApp.Domain/Constants/IncomingVehicleOperationSettingKeys.cs` `[new]`
- `src/StationApp.Application/DTOs/StationDtos.cs`
- `src/StationApp.Application/Interfaces/...` cho provider mới
- `src/StationApp.Infrastructure/Services/StationScopeServices.cs`
- `src/StationApp.UI/ViewModels/Settings/StationMasterViewModel.cs`
- `src/StationApp.UI/Views/Settings/StationMasterView.xaml`
- `src/StationApp.UI/ViewModels/IncomingVehicleListViewModel.cs`

Có thể phát sinh thêm:

- file đăng ký DI cho provider mới

## 11. Kế hoạch thực hiện

### Bước 1. Chuẩn hóa key và DTO

- Tạo constants cho 4 key mới
- Mở rộng `StationOperationSettingsDto`
- Bổ sung `Defaults`

### Bước 2. Mở rộng load/save station settings

- Map 4 key mới ở `LoadOperationSettingsSetsAsync(...)`
- Ghi 4 key mới ở `SaveStationAsync(...)`

### Bước 3. Thêm UI cấu hình cho `QN01`

- Thêm block độc lập trên màn `Danh mục trạm`
- Chỉ hiển thị khi `EditStationCode = QN01`
- Bind 4 checkbox mới vào ViewModel

### Bước 4. Tạo provider đọc rule theo runtime station

- Đọc `current station` qua `IStationScope`
- Nếu khác `QN01` thì trả rule mặc định tắt
- Nếu là `QN01` thì đọc settings và map sang rule model

### Bước 5. Gắn validate vào `ConfirmEnterWeighingAsync`

- Tách thêm hàm validate riêng cho luồng `Cân nội địa`, thay vì nhét hết vào `ValidateIncomingDetailForm()`
- Chỉ áp dụng cho `OUTBOUND + Bao/Rời/Xá`
- Chặn thiếu `TTCP`
- Chặn thiếu đăng kiểm
- Gọi ở cả nhánh `IsCreateMode = true` và nhánh chọn bản ghi sẵn có
- Giữ nguyên nhánh chặn đăng kiểm hết hạn hiện có

### Bước 6. Kiểm thử

- Test trạm `QN01` với setting bật/tắt
- Test trạm khác với cùng dữ liệu để xác nhận không bị ảnh hưởng
- Test `Bao`
- Test `Rời/Xá`
- Test có mooc và không mooc
- Test thiếu dữ liệu
- Test dữ liệu hết hạn
- Test riêng nút `Lưu thay đổi` để xác nhận không bị chặn bởi rule mới
- Test riêng nút `Cân nội địa` để xác nhận bị chặn đúng lúc

## 12. Rủi ro và lưu ý cần giữ trong lúc code

- `Rời/Xá` hiện chỉ chắc chắn normalize được `Rời/Xá` và `Roi/Xa`
- Không nên gắn block UI mới vào `ActiveConfigMode`, nếu không sẽ phụ thuộc sai vào menu trạm đập/mỏ sét
- Không nên dùng “trạm đang sửa ở màn admin” để validate ở `Danh sách xe vào`
- Không được vô tình gắn validate vào `SaveDetailAsync()`, vì user đã chốt chỉ chặn ở nút `Cân nội địa`

## 13. Kết luận

Hướng làm phù hợp nhất với code hiện tại là:

- Mở rộng `station_operation_settings`
- Mở rộng `StationOperationSettingsDto`
- Thêm block cấu hình riêng cho `QN01` ở màn `Danh mục trạm`
- Đọc rule theo `current runtime station`
- Áp rule khi bấm `Cân nội địa` ở `Danh sách xe vào`

Điểm chỉnh quan trọng sau khi rà code:

- `QN01 only` ở runtime phải dựa trên `IStationScope`, không dựa vào dòng đang sửa ở màn admin
- `Rời/Xá` hiện chưa nên giả định cover nhiều alias hơn `ProductTypes.Normalize(...)` đang có
- UI cấu hình mới phải tách khỏi block `CRUSHER/CLAY`, không dùng chung `ActiveConfigMode`
