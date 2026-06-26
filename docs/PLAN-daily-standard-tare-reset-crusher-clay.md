# Plan: Daily Reset TL xe chuẩn cho Cân trạm đập và Cân mỏ sét

## 1. Mục tiêu

Điều chỉnh nghiệp vụ `TL xe chuẩn` cho hai chức năng:

- `Cân trạm đập`
- `Cân mỏ sét`

Theo yêu cầu đã chốt:

- `TL xe chuẩn` chỉ có hiệu lực trong đúng ngày được xác lập.
- Sang ngày mới, hệ thống ngầm hiểu xe đó chưa có `TL xe chuẩn`, tương đương giá trị hiệu lực là `0`.
- Lượt đầu tiên của xe trong ngày bắt buộc đi theo `Cân 2 lần`.
- Khi hoàn tất `Cân 2 lần`, `TL xe chuẩn mới = Cân lần 2`.
- Không cho phép nhập tay hoặc sửa tay `TL xe chuẩn` trong 2 chức năng này.
- Không cần xử lý case phiên cân kéo dài qua ngày vì nghiệp vụ xác nhận không xảy ra.

## 2. Hiện trạng code

Qua rà soát code hiện tại:

- `TL xe chuẩn` đang được lưu trực tiếp ở `Vehicle.TtcpWeight`.
- Thời điểm cập nhật gần nhất đang lưu tại `Vehicle.StandardTareUpdatedAt`.
- Điều kiện cho `Cân 1 lần` hiện chỉ là `TtcpWeight > 0`, chưa xét giá trị này có thuộc ngày hiện tại hay không.
- UI của 2 màn cân hiện vẫn cho phép nhập/sửa trực tiếp `TL xe chuẩn`, nên có thể bypass nghiệp vụ mới nếu không chặn lại.
- Khi hoàn tất `Cân 2 lần`, hệ thống hiện chưa cập nhật lại `TL xe chuẩn` của xe theo `Cân lần 2`.

Các điểm code chính:

- `src/StationApp.Application/UseCases/CrusherWeighingUseCases.cs`
- `src/StationApp.Application/UseCases/ClayWeighingUseCases.cs`
- `src/StationApp.UI/ViewModels/CrusherWeighingViewModel.cs`
- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`
- `src/StationApp.Domain/Entities/Vehicle.cs`

## 3. Quy tắc nghiệp vụ sẽ áp dụng

### 3.1 Khái niệm `TL xe chuẩn hiệu lực`

Không reset vật lý dữ liệu trong DB vào lúc 0h. Thay vào đó dùng cơ chế `effective value`:

- Nếu `Vehicle.TtcpWeight <= 0` thì xem như chưa có `TL xe chuẩn`.
- Nếu `Vehicle.StandardTareUpdatedAt` khác ngày hiện tại thì xem như `TL xe chuẩn` đã hết hiệu lực.
- Chỉ khi `TtcpWeight > 0` và `StandardTareUpdatedAt.Date == TodayLocal.Date` thì `TL xe chuẩn` mới được coi là hợp lệ để dùng cho `Cân 1 lần`.

### 3.2 Chế độ cân

- Nếu `TL xe chuẩn hiệu lực = 0` thì mode phải là `Cân 2 lần`.
- Nếu `TL xe chuẩn hiệu lực > 0` thì mode phải là `Cân 1 lần`.

### 3.3 Cách xác lập `TL xe chuẩn` mới

- `TL xe chuẩn` mới chỉ được hình thành sau khi hoàn tất `Cân 2 lần`.
- Giá trị mới được lấy bằng `Cân lần 2`.

### 3.4 Cách nhập/sửa `TL xe chuẩn`

- Không cho phép nhập tay `TL xe chuẩn` trong màn cân trạm đập.
- Không cho phép nhập tay `TL xe chuẩn` trong màn cân mỏ sét.
- Không cho phép sửa tay `TL xe chuẩn` trong các flow chọn/tạo/cập nhật xe từ 2 màn cân này.

## 4. Hướng triển khai

## 4.1 Tạo rule dùng chung cho `TL xe chuẩn` theo ngày

Tạo một helper hoặc domain rule dùng chung để xác định `TL xe chuẩn hiệu lực` theo ngày hiện tại.

Yêu cầu:

- Dùng `_clock.TodayLocal` hoặc giá trị ngày local thống nhất trong hệ thống.
- Không dùng `DateTime.Now` rải rác để tự so sánh ngày ở nhiều nơi.
- Rule phải tái sử dụng được cho cả `Crusher` và `Clay`.

Acceptance criteria:

- [ ] Có một hàm/rule dùng chung để tính `effective standard tare`.
- [ ] Rule phân biệt đúng 3 case: không có tare, tare khác ngày, tare cùng ngày.

Files likely touched:

- `src/StationApp.Application/...` hoặc `src/StationApp.Domain/...`
- `tests/...`

## 4.2 Sửa use case tạo phiên cân

Điều chỉnh logic trong `CreateSessionAsync` của cả `Crusher` và `Clay`.

Mục tiêu:

- Không còn dùng raw `vehicle.TtcpWeight > 0` để quyết định có được `Cân 1 lần` hay không.
- Chỉ cho `Cân 1 lần` khi xe có `TL xe chuẩn hiệu lực` trong ngày.

Acceptance criteria:

- [ ] Xe có tare từ hôm trước không được tạo session `Cân 1 lần`.
- [ ] Xe có tare trong ngày được phép tạo session `Cân 1 lần`.
- [ ] Thông báo lỗi nghiệp vụ phản ánh đúng là chưa có `TL xe chuẩn` hợp lệ cho ngày hiện tại.

Files likely touched:

- `src/StationApp.Application/UseCases/CrusherWeighingUseCases.cs`
- `src/StationApp.Application/UseCases/ClayWeighingUseCases.cs`

## 4.3 Cập nhật `TL xe chuẩn` khi hoàn tất `Cân 2 lần`

Điều chỉnh logic `CaptureWeight2Async` của cả `Crusher` và `Clay`.

Mục tiêu:

- Sau khi hoàn tất `Cân 2 lần`, cập nhật:
  - `Vehicle.TtcpWeight = Weight2`
  - `Vehicle.StandardTareUpdatedAt = now`
  - `Vehicle.StandardTareUpdatedBy = current user / actor phù hợp`

Nếu hệ thống hiện có cơ chế sync vehicle thì cần tận dụng đúng pattern sẵn có để đồng bộ thay đổi này.

Acceptance criteria:

- [ ] Hoàn tất `Cân 2 lần` sẽ tạo ra `TL xe chuẩn` mới cho xe.
- [ ] `TL xe chuẩn` mới có hiệu lực ngay trong ngày vừa cập nhật.
- [ ] Không làm thay đổi dữ liệu lịch sử của các phiên cân đã lưu.

Files likely touched:

- `src/StationApp.Application/UseCases/CrusherWeighingUseCases.cs`
- `src/StationApp.Application/UseCases/ClayWeighingUseCases.cs`
- Có thể thêm logic sync vehicle nếu cần

## 4.4 Sửa UI để tự động chọn mode theo tare hiệu lực

Điều chỉnh `CrusherWeighingViewModel` và `ClayWeighingViewModel`.

Mục tiêu:

- Khi chọn xe:
  - nếu `TL xe chuẩn hiệu lực = 0` thì tự chuyển sang `Cân 2 lần`
  - nếu `TL xe chuẩn hiệu lực > 0` thì tự chuyển sang `Cân 1 lần`
- Khi refresh màn hình hoặc chọn lại xe, mode phải phản ánh đúng trạng thái hiện tại của xe chứ không chỉ bám vào default mode từ setting.

Acceptance criteria:

- [ ] Chọn xe chưa có tare hiệu lực thì UI tự về `Cân 2 lần`.
- [ ] Chọn xe đã có tare hiệu lực trong ngày thì UI tự về `Cân 1 lần`.
- [ ] Không còn tình trạng setting mặc định trạm ghi đè sai mode sau khi đã chọn xe.

Files likely touched:

- `src/StationApp.UI/ViewModels/CrusherWeighingViewModel.cs`
- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`

## 4.5 Chặn nhập tay và sửa tay `TL xe chuẩn` trong 2 màn cân

Điều chỉnh các flow:

- chọn/tạo xe nội bộ
- xác nhận xe nội bộ
- cập nhật master data xe từ màn cân

Mục tiêu:

- Không cho người dùng gõ `TL xe chuẩn` để được `Cân 1 lần`.
- Không cho người dùng sửa `TL xe chuẩn` thủ công trong hai màn cân này.
- Xe mới của ngày đầu tiên vẫn được phép cân, nhưng phải đi qua `Cân 2 lần`.

Acceptance criteria:

- [ ] Không thể tạo tare mới bằng nhập tay trên 2 màn cân.
- [ ] Không thể sửa tare cũ bằng nút cập nhật trên 2 màn cân.
- [ ] Flow xe mới vẫn hoạt động bình thường cho `Cân 2 lần`.

Files likely touched:

- `src/StationApp.UI/ViewModels/CrusherWeighingViewModel.cs`
- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`

## 4.6 Hiển thị `TL xe chuẩn` theo đúng hiệu lực ngày

Điều chỉnh dữ liệu hiển thị lên form xe trong 2 màn cân.

Mục tiêu:

- Nếu tare đã hết hiệu lực do khác ngày, UI không được làm người dùng hiểu rằng xe vẫn có thể `Cân 1 lần`.
- Sau khi hoàn tất `Cân 2 lần`, chọn lại xe phải thấy tare mới của hôm nay.

Acceptance criteria:

- [ ] Tare hôm qua không hiển thị như tare đang dùng được cho hôm nay.
- [ ] Tare mới hình thành từ `Cân lần 2` hiển thị đúng ngay sau khi cập nhật.

Files likely touched:

- `src/StationApp.UI/ViewModels/CrusherWeighingViewModel.cs`
- `src/StationApp.UI/ViewModels/ClayWeighingViewModel.cs`

## 4.7 Rà ảnh hưởng report và dữ liệu lịch sử

Kiểm tra các báo cáo và danh sách phiên cân liên quan:

- `CrusherInboundReportServices`
- `ClayInboundReportServices`

Mục tiêu:

- Xác nhận báo cáo lịch sử vẫn dùng snapshot của phiên cân, không dùng `Vehicle.TtcpWeight` hiện tại để tính lại quá khứ.

Acceptance criteria:

- [ ] Report lịch sử không bị sai sau khi áp dụng rule daily tare.
- [ ] Snapshot `StandardTareWeightSnapshot` của session vẫn là nguồn hiển thị lịch sử.

Files likely touched:

- Có thể không cần sửa code, nhưng phải verify:
  - `src/StationApp.Infrastructure/Services/CrusherInboundReportServices.cs`
  - `src/StationApp.Infrastructure/Services/ClayInboundReportServices.cs`

## 5. Kế hoạch test

## 5.1 Test nghiệp vụ cho cả Crusher và Clay

Các case cần verify:

1. Xe có `TL xe chuẩn` được cập nhật hôm nay:
   - Chọn xe
   - Hệ thống tự chuyển `Cân 1 lần`

2. Xe có `TL xe chuẩn` được cập nhật hôm qua:
   - Chọn xe
   - Hệ thống tự chuyển `Cân 2 lần`
   - Không cho tạo `Cân 1 lần`

3. Xe chưa từng có `TL xe chuẩn`:
   - Chọn xe
   - Hệ thống tự chuyển `Cân 2 lần`

4. Hoàn tất `Cân 2 lần`:
   - `Cân lần 2` được lưu vào `Vehicle.TtcpWeight`
   - `StandardTareUpdatedAt` được cập nhật ngày hiện tại

5. Ngay sau khi hoàn tất `Cân 2 lần`:
   - Chọn lại cùng xe
   - Hệ thống tự chuyển sang `Cân 1 lần`

6. Nhập tay `TL xe chuẩn` trên màn cân:
   - Không được dùng để mở `Cân 1 lần`
   - Không được ghi đè tare hiện có

## 5.2 Regression cần lưu ý

- `Cân 2 lần` hiện hữu vẫn phải tạo và hoàn tất phiên bình thường.
- `Cân 1 lần` của xe đã có tare hợp lệ trong ngày vẫn phải chạy đúng cách tính `KL hàng`.
- Flow refresh/reset form không bị vỡ.
- Flow chọn xe nội bộ mới không bị hỏng.

## 6. Rủi ro và lưu ý

| Rủi ro | Mức độ | Hướng xử lý |
|--------|--------|-------------|
| Logic Crusher và Clay bị lệch nhau do code clone | Cao | Dùng chung rule/helper và áp cùng pattern cho cả 2 bên |
| UI vẫn còn đường nhập tay tare ngoài một flow nào đó | Cao | Rà toàn bộ `EnsureInternalVehicle`, `ConfirmInternalVehicle`, `UpdateVehicleMasterData` |
| Dùng `DateTime.Now` không đồng nhất timezone | Trung bình | Chuẩn hóa so sánh ngày bằng `_clock.TodayLocal` |
| Tác động nhầm sang các màn hình khác dùng `Vehicle.TtcpWeight` | Trung bình | Giới hạn phạm vi áp rule ở 2 chức năng Crusher/Clay |

## 7. Thứ tự thực hiện đề xuất

1. Tạo helper/rule `effective standard tare`
2. Sửa `CreateSessionAsync` của `Crusher` và `Clay`
3. Sửa `CaptureWeight2Async` để cập nhật tare mới
4. Sửa UI auto-switch mode khi chọn xe
5. Chặn nhập tay/sửa tay tare trong 2 màn cân
6. Verify report/history
7. Bổ sung test và chạy smoke test

## 8. Tiêu chí hoàn tất

- Xe chạy lượt đầu tiên trong ngày bắt buộc `Cân 2 lần` nếu chưa có tare hiệu lực trong ngày.
- Hoàn tất `Cân 2 lần` sẽ tạo `TL xe chuẩn` mới bằng `Cân lần 2`.
- Sau khi đã có tare trong ngày, xe tự chuyển sang `Cân 1 lần`.
- Không còn đường nhập tay hoặc sửa tay `TL xe chuẩn` trong 2 màn cân này.
- Lịch sử và báo cáo không bị sai do thay đổi tare theo ngày.
