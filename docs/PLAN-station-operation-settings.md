# Plan: Cấu hình vận hành trạm cân trên Giao diện Danh mục trạm

Hiện tại, cấu hình chế độ cân và các giá trị mặc định của màn hình cân cho các trạm cân (ví dụ: Cân trạm đập, Cân mỏ sét) đang được quản lý dưới cơ sở dữ liệu ở bảng `station_operation_settings`. Khi cần thay đổi cấu hình, người quản trị phải dùng lệnh SQL `UPDATE`.

Ngoài ra, qua rà soát:
- Trường `crusher_default_product_code` và `clay_default_product_code` đã được định nghĩa dưới cơ sở dữ liệu nhưng chưa được nạp động trong code, hiện tại giao diện các màn cân vẫn đang lấy sản phẩm mặc định bằng các hằng số C# hardcode (Đá vôi / Sét).
- Các trường cấu hình khách hàng mặc định (như `crusher_default_customer_code`, `clay_default_customer_code`) chưa tồn tại dưới DB và cũng đang được hardcode.

Tài liệu này đề xuất phương án và kế hoạch chi tiết để đưa toàn bộ các cấu hình vận hành và giá trị mặc định sản phẩm/khách hàng này lên giao diện quản trị của màn hình **Danh mục trạm** (Station Master), đồng thời chuyển đổi luồng xử lý màn cân sang nạp động từ DB (có fallback).

---

## User Review Required

> [!IMPORTANT]
> **Các cấu hình vận hành cần hỗ trợ hiển thị & chỉnh sửa:**
>
> 1. **Cấu hình Cân trạm đập (Crusher Weighing Settings):**
>    - Cho phép cân 1 lần (`crusher_single_weigh_enabled`): CheckBox
>    - Chế độ cân mặc định (`crusher_default_weigh_mode`): ComboBox (`TWO_WEIGH` hoặc `SINGLE_WITH_STANDARD_TARE`)
>    - Mã sản phẩm mặc định (`crusher_default_product_code`): TextBox
>    - Mã khách hàng mặc định (`crusher_default_customer_code`): TextBox [MỚI]
>
> 2. **Cấu hình Cân mỏ sét (Clay Weighing Settings):**
>    - Cho phép cân 1 lần (`clay_single_weigh_enabled`): CheckBox
>    - Chế độ cân mặc định (`clay_default_weigh_mode`): ComboBox (`TWO_WEIGH` hoặc `SINGLE_WITH_STANDARD_TARE`)
>    - Mã sản phẩm mặc định (`clay_default_product_code`): TextBox
>    - Mã khách hàng mặc định (`clay_default_customer_code`): TextBox [MỚI]
>
> *Lưu ý (Đã thống nhất lược bỏ sau rà soát thực tế):*
> - Lược bỏ trường `require_standard_tare_for_single_weigh` (do tính chất nghiệp vụ cân 1 lần luôn bắt buộc phải có trọng lượng xe chuẩn để trừ bì tính khối lượng tịnh).
> - Lược bỏ trường `standard_tare_tolerance_kg` (dung sai xe chuẩn) do hiện tại hệ thống chưa phát triển logic đối chiếu dung sai này khi cập nhật trọng lượng xe chuẩn.

---

## Open Questions

> [!IMPORTANT]
> Vui lòng phản hồi các câu hỏi làm rõ dưới đây để tối ưu hóa trải nghiệm và luồng xử lý:
> 
> 1. **Giao diện hiển thị (UI Layout):**
>    - Bạn muốn bố trí các trường cấu hình này ở đâu trong khu vực chỉnh sửa của màn **Danh mục trạm**?
>    - **Phương án A (Khuyên dùng):** Gom nhóm thành 2 khu vực `Cấu hình Cân trạm đập` và `Cấu hình Cân mỏ sét` hiển thị cạnh nhau hoặc chồng lên nhau bằng `GroupBox` hoặc `TabControl` ngay phía dưới phần CheckBox "Menu hiển thị".
>    - **Phương án B:** Chỉ hiển thị động các GroupBox cấu hình tương ứng khi tích chọn Checkbox "Cân trạm đập" hoặc "Cân mỏ sét" ở "Menu hiển thị".
> 
> 2. **Xử lý giá trị mặc định khi tạo mới trạm:**
>    - Khi tạo mới trạm, các cấu hình này có nên tự động sinh ra các giá trị mặc định dưới DB không? (Ví dụ: `single_weigh_enabled = true`, `default_weigh_mode = SINGLE_WITH_STANDARD_TARE`, `default_product = [Trống]`, `default_customer = [Trống]`).
=> Trả lời: Ok đồng ý
> 
> 3. **Ràng buộc mã sản phẩm/khách hàng mặc định:**
>    - Trường "Mã sản phẩm mặc định" và "Mã khách hàng mặc định" khi nhập ở Danh mục trạm có cần validate xem có tồn tại trong danh mục master tương ứng của trạm đó không, hay chỉ cần lưu text thuần túy? (Đề xuất: Chỉ cần lưu text và nạp động lên màn cân, nếu mã sai hoặc trống màn cân sẽ tự động không hiển thị tên tương ứng hoặc fallback về giá trị hardcode).
=> Trả lời: Cần hiển thị list danh sách sản phẩm/khách hàng thuộc trạm đó để người dùng chọn, điều này đảm bảo sản phẩm được chọn luôn tồn tại trong master data

---

## Proposed Changes

### 1. Data Access & Domain Layer

#### [MODIFY] [IStationOperationSettingsRepository.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/Interfaces/IStationOperationSettingsRepository.cs)
- Bổ sung các phương thức:
  - `Task<IReadOnlyDictionary<string, string>> GetSettingsByStationAsync(string stationCode, CancellationToken ct);`
  - `Task SaveSettingsAsync(string stationCode, IReadOnlyDictionary<string, string> settings, string actor, CancellationToken ct);`

#### [MODIFY] [MasterDataRepositories.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Infrastructure/Repositories/MasterDataRepositories.cs)
- Triển khai hai phương thức mới trong `StationOperationSettingsRepository`:
  - `GetSettingsByStationAsync`: Query bảng `station_operation_settings` theo `StationCode`, trả về Dictionary `<Key, Value>`.
  - `SaveSettingsAsync`: Thực hiện upsert (cập nhật nếu đã có, chèn mới nếu chưa có) các cặp Key-Value cấu hình tương ứng cho trạm.

---

### 2. Application & Services Layer

#### [MODIFY] [Dtos.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/DTOs/Dtos.cs) (hoặc file Dto liên quan)
- Định nghĩa DTO `StationOperationSettingsDto` chứa các thuộc tính cấu hình vận hành để truyền tải lên UI.
- Cập nhật `StationManagementDto` và `SaveStationRequest` để đính kèm `StationOperationSettingsDto`.

#### [MODIFY] [IStationScope.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Application/Interfaces/IStationScope.cs)
- Cập nhật định nghĩa trong `IStationAdministrationService` để các phương thức `SearchStationsAsync` và `SaveStationAsync` xử lý kèm theo cấu hình vận hành.

#### [MODIFY] [StationScopeServices.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.Infrastructure/Services/StationScopeServices.cs)
- Cập nhật `SearchStationsAsync` để tải kèm cấu hình từ `station_operation_settings` cho từng trạm cân.
- Cập nhật `SaveStationAsync` để lưu trữ/cập nhật thông tin cấu hình vận hành song song với thông tin trạm và các feature flags.

---

### 3. Presentation Layer (WPF UI)

#### [MODIFY] [StationMasterViewModel.cs](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/ViewModels/Settings/StationMasterViewModel.cs)
- Thêm các thuộc tính Observable đại diện cho cấu hình vận hành của Crusher và Clay (SingleWeighEnabled, DefaultWeighMode, DefaultProductCode, DefaultCustomerCode).
- Khi thay đổi trạm được chọn (`OnSelectedStationChanged`), gán dữ liệu cấu hình vận hành tương ứng vào các thuộc tính này.
- Khi nhấn `LƯU` (`SaveAsync`), đóng gói các thuộc tính này vào `SaveStationRequest` gửi xuống service.
- Khi nhấn `LÀM MỚI` (`ResetForm`), reset các thuộc tính cấu hình vận hành về trạng thái mặc định.
- Bổ sung logic khóa/mở ComboBox "Chế độ cân mặc định" tương ứng với trạng thái CheckBox "Cho phép cân 1 lần".

#### [MODIFY] [StationMasterView.xaml](file:///g:/Source-code/pmcan_C%23/src/StationApp.UI/Views/Settings/StationMasterView.xaml)
- Bổ sung layout chỉnh sửa cấu hình vận hành. 
- Thiết kế hai GroupBox:
  - **Cấu hình Cân trạm đập:** chứa các trường CheckBox, ComboBox và TextBox tương ứng (Cho phép cân 1 lần, Chế độ mặc định, Mã SP mặc định, Mã KH mặc định).
  - **Cấu hình Cân mỏ sét:** chứa các trường tương ứng.
- Đảm bảo giao diện cân đối, đáp ứng tốt thẩm mỹ hiện tại của dự án.

---

## Verification Plan

### Automated Tests
- Bổ sung unit tests / integration tests trong `tests/StationApp.IntegrationTests/SmokeTests.cs` (hoặc test class liên quan):
  - Kiểm tra việc load cấu hình vận hành của trạm lên.
  - Kiểm tra việc lưu/cập nhật cấu hình vận hành thay đổi giá trị dưới database bảng `station_operation_settings`.

### Manual Verification
1. Mở màn hình **Danh mục trạm** trong phân hệ quản trị.
2. Chọn một trạm cân từ danh sách (ví dụ: `QN01`).
3. Thay đổi các cấu hình vận hành (ví dụ: Bật chế độ cân 1 lần cho Cân mỏ sét, nhập mã sản phẩm mặc định).
4. Nhấn **LƯU** và kiểm tra thông báo toast thành công.
5. Kiểm tra trực tiếp dữ liệu dưới DB bảng `station_operation_settings` để đảm bảo đã lưu chính xác key và value.
6. Mở màn hình **Cân mỏ sét** và kiểm tra xem cấu hình cân 1 lần và mã sản phẩm mặc định mới đã được áp dụng tự động hay chưa.
