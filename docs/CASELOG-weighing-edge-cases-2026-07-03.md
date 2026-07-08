# Case Log: Các tình huống nghiệp vụ cân đã chốt ngày 2026-07-03

Tài liệu này ghi nhận các case nghiệp vụ đã trao đổi để dễ tra cứu lại khi kiểm thử, bàn giao hoặc chỉnh tiếp.

## 1. Tách quá tải khi tổng tải quá lớn

### Bối cảnh

Lượt cân quá tải nhưng hệ thống báo:

> Lượt cân này không thể tách hợp lệ thành 2 phiếu với ngưỡng TTCP 10% hiện tại...

Case ví dụ: `LC26070052`.

### Ý nghĩa đúng của TTCP10%

`TTCP10%` là trọng lượng tối đa cho phép lưu thông của cả xe và hàng, tức là giá trị gross tối đa.

Với xe xuất hàng:

- `Cân lần 1` là trọng lượng xác xe.
- `Cân lần 2` là trọng lượng xe + hàng.
- Phiếu tách thứ nhất phải có `Cân lần 2 = TTCP10%`.
- Trọng lượng hàng phiếu 1 = `TTCP10% - Cân lần 1`.
- Phiếu 2 nhận phần hàng còn lại.

### Ví dụ

Nếu:

- Cân lần 1 = `10,000 kg`
- TTCP10% = `32,000 kg`
- Tổng hàng = `65,500 kg`

Kết quả mong muốn:

- Phiếu 1: cân lần 1 `10,000`, cân lần 2 `32,000`, hàng `22,000`
- Phiếu 2: hàng còn lại `43,500`

### Hướng xử lý

Cho phép tách dù phiếu 2 vẫn vượt ngưỡng, miễn phiếu 1 đã đạt đúng ngưỡng gross `TTCP10%`.

### Trạng thái

Đã xử lý và có test bảo vệ case phiếu 1 gross bằng `TTCP10%`.

## 2. Cân trạm đập chỉ được chọn xe trong Danh mục xe

### Bối cảnh

Ở màn Cân trạm đập, trước đây có thể nhập xe chưa có trong danh mục và hệ thống tự tạo hoặc chuyển xe ngoài thành xe nội bộ.

### Mong muốn

- Chỉ cho cân xe đã tồn tại trong Danh mục xe.
- Không cho tạo master xe mới từ màn Cân trạm đập.
- Việc tạo hoặc cập nhật xe chỉ thực hiện ở màn Danh mục xe.
- Autocomplete chỉ hiển thị xe nội bộ đang active.

### Hướng xử lý

- Nút cân lần 1 chỉ enabled khi đã chọn được xe nội bộ active.
- Khi gõ xe chưa có trong danh mục, hiển thị cảnh báo yêu cầu tạo ở Danh mục xe.
- Loại bỏ luồng tự tạo xe hoặc convert xe ngoài thành xe nội bộ từ màn cân.
- Tầng use case cũng chặn xe không phải nội bộ hoặc đã ngừng sử dụng.

### Trạng thái

Đã xử lý.

## 3. Lượt cân trạm đập cũ tự lấy TL bì hiệu lực phát sinh sau đó

### Bối cảnh

Đầu ngày, xe A chưa có TL bì hiệu lực trong ngày.

Luồng xảy ra:

1. Xe A cân lần 1, tạo lượt cân đầu tiên, chế độ cân 2 lần.
2. Xe A đổ đá xuống nhưng quên cân lần 2 để lấy bì.
3. Xe A vào nhận đá lần sau, tạo lượt cân thứ hai, vẫn cân 2 lần do chưa có TL bì hiệu lực.
4. Lượt cân thứ hai cân lần 2 xong, hệ thống mới lưu được TL bì hiệu lực trong ngày cho xe A.
5. Quay lại lượt cân thứ nhất, hệ thống vẫn bắt cân 2 lần nhưng không tự lấy TL bì mới phát sinh.

### Mong muốn

Khi chọn lại lượt cân thứ nhất đang chờ cân lần 2:

- Hệ thống tự kiểm tra master xe A.
- Nếu xe A đã có TL bì hiệu lực trong ngày thì lấy TL bì đó.
- Người dùng bấm `Cân lần 2` thì không lấy số cân từ thiết bị.
- `Cân lần 2` được gán bằng TL bì hiệu lực trong ngày.
- Tính TL hàng từ `abs(Cân lần 1 - TL bì)`.
- Khi bấm Lưu, vẫn lưu qua luồng cân lần 2 bình thường.

### Hướng xử lý

- Khi chọn session `PENDING_WEIGHT2`, re-query xe nội bộ active theo biển số.
- Nếu có TL bì hiệu lực hôm nay, cache lại cho nút `Cân lần 2`.
- Nút `Cân lần 2` ưu tiên dùng TL bì hiệu lực thay vì `CurrentWeight`.
- Khi lưu cân lần 2, cập nhật snapshot TL bì trên session.

### Trạng thái

Đã xử lý và có test use case xác nhận session lưu lại snapshot TL bì.

## 4. Đổi số xe làm vô hiệu hóa TL bì sai của xe bị chọn nhầm

### Bối cảnh

Case xe A, xe B:

1. Xe A đầu ngày cân lần 1, lượt `0001`, chế độ cân 2 lần để xác định TL bì trong ngày.
2. Xe B cân lần 1, lượt `0002`, cũng chế độ cân 2 lần.
3. Nhân viên nhận ra thực tế lượt `0002` là xe A, nhưng lúc cân đã chọn nhầm xe B.
4. Vì lượt `0002` đã cân lần 2, hệ thống đã ghi TL bì hiệu lực trong ngày cho xe B.
5. Nhưng TL bì đó thực tế là của xe A, không phải xe B.
6. Xe B thực tế chưa vào cân lần nào để xác định bì hôm nay.
7. Khi chọn xe B cho lượt sau, hệ thống thấy master xe B có TL bì hiệu lực và tự chuyển sang cân 1 lần, đây là sai.

### Mong muốn

Khi dùng chức năng `Đổi số xe` để đổi lượt cân từ xe B sang xe A:

- Nếu TL bì hôm nay của xe B được sinh ra từ chính lượt cân đang bị đổi,
- Thì cần vô hiệu hóa TL bì hôm nay sai của xe B.
- Lần sau xe B vào cân phải quay lại chế độ cân 2 lần để xác định lại TL bì đúng.

### Điều kiện nên áp dụng để tránh xóa nhầm

Chỉ vô hiệu hóa TL bì của xe cũ khi đủ các điều kiện:

- Lượt đang đổi là lượt cân đã hoàn thành và có `Weight2` để dùng làm TL bì. Ưu tiên case cân 2 lần, nhưng cần cover cả dữ liệu thực tế đang lưu `SINGLE_WITH_STANDARD_TARE` nếu `Weight2` chính là TL bì cần chuyển sang xe mới.
- Xe cũ có TL bì hiệu lực trong ngày.
- TL bì hiện tại của xe cũ khớp với `Weight2` của lượt đang đổi.
- `StandardTareUpdatedAt` của xe cũ là ngày hiện tại.
- Xe cũ chỉ có đúng `1` lượt cân hoàn thành sinh ra TL bì trong ngày đó. Nếu xe cũ đã có nhiều lượt hoàn thành trong ngày, không tự clear TL bì để tránh xóa nhầm bì đúng từ lượt khác.

### Case không được xóa TL bì

- Xe B đã có TL bì đúng từ lượt khác trước đó.
- Lượt đang đổi chưa có `Weight2`, hoặc không đủ dấu hiệu cho thấy TL bì hiện tại của xe cũ khớp với lượt đang đổi.
- Lượt đang đổi chưa có cân lần 2.
- TL bì master của xe cũ không khớp `Weight2` của lượt bị đổi.
- Xe cũ có nhiều hơn `1` lượt cân hoàn thành trong ngày, vì không đủ chắc chắn TL bì hiện tại chỉ đến từ lượt đang đổi.

### Nhánh xe mới chưa có TL bì hiệu lực

Nếu đổi lượt cân hoàn thành từ xe B sang xe A, và xe A chưa có TL bì hiệu lực trong ngày:

- Không đưa lượt cân về trạng thái `PENDING_WEIGHT2`.
- Giữ nguyên lượt cân ở trạng thái `COMPLETED`.
- Giữ `Weight1` là trọng lượng tổng đã cân thực tế.
- Giữ `Weight2` là trọng lượng bì thực tế của xe A.
- Gán `Weight2` vào master xe A làm TL bì hiệu lực trong ngày.
- Tính/giữ TL hàng theo vai trò TL bì: `Trọng lượng tổng - TL bì`, tức `Weight1 - Weight2` khi `Weight2` được chuyển sang làm TL bì xe mới.
- Ghi audit `AppliedStandardTareToNewVehicle`.

Lý do: lượt cân thực tế đã có đủ dữ liệu tổng và bì. Vấn đề chỉ là chọn nhầm biển số xe ban đầu, nên `Weight2` không được bỏ đi mà phải chuyển sang đúng xe. Case thực tế `LC26070018` cho thấy bản ghi có thể đang lưu `SINGLE_WITH_STANDARD_TARE`, nhưng nghiệp vụ vẫn cần lấy `Weight2` hiện tại làm TL bì của xe mới khi xe mới chưa có TL bì hiệu lực trong ngày.

### Nhánh xe mới đã có TL bì hiệu lực

Nếu xe A đã có TL bì hiệu lực trong ngày khi đổi xe:

- Không ghi đè TL bì hiện có của xe A bằng `Weight2` cũ.
- Cân lần 2 của lượt sau sửa dùng TL bì hiệu lực của xe A.
- TL hàng sau sửa = `Trọng lượng tổng - TL bì xe A`.
- Vẫn có thể vô hiệu TL bì xe cũ B nếu B thỏa đủ điều kiện xóa nhầm ở trên.

### Hướng xử lý đề xuất

Trong `UpdateSessionVehicleAsync`:

1. Load xe cũ theo `session.StandardTareVehicleId` trước khi đổi.
2. Xác định xem TL bì hôm nay của xe cũ có khớp với `session.Weight2` không.
3. Đếm số lượt cân hoàn thành trong ngày của xe cũ theo `StandardTareVehicleId`.
4. Nếu chỉ có đúng `1` lượt hoàn thành và TL bì khớp `Weight2`, set TL bì xe cũ về null hoặc clear ngày hiệu lực.
5. Nếu xe mới chưa có TL bì hiệu lực, gán TL bì vừa cân (`Weight2`) sang xe mới vì đó mới là xe thực tế của lượt cân.
6. Nếu xe mới đã có TL bì hiệu lực, dùng TL bì hiện có của xe mới để tính lại `Weight2`/TL hàng, không ghi đè master xe mới.
7. Ghi audit log:
   - `InvalidatedOldVehicleStandardTare` khi vô hiệu TL bì xe cũ.
   - `AppliedStandardTareToNewVehicle` khi áp `Weight2` sang xe mới.
8. Hiển thị các ghi chú audit này trên màn Lịch sử chỉnh sửa.

### Trạng thái

Đã xử lý. Khi đổi xe cho lượt cân đã hoàn tất và có `Weight2`, hệ thống sẽ vô hiệu hóa TL bì hôm nay của xe cũ nếu TL bì đó khớp với `Weight2` của lượt đang đổi và xe cũ chỉ có đúng `1` lượt hoàn thành trong ngày. Nếu xe mới chưa có TL bì hiệu lực, hệ thống áp `Weight2` thành TL bì hiệu lực cho xe mới và tính TL hàng = `Weight1 - Weight2`; nếu xe mới đã có TL bì hiệu lực, hệ thống dùng TL bì hiện có của xe mới để tính lại số liệu sau sửa. Có test bảo vệ các nhánh chính, bao gồm cả case bản ghi đang là `SINGLE_WITH_STANDARD_TARE`.

## 5. Danh mục xe nội bộ không bắt buộc nhập TL bì khi tạo mới

### Bối cảnh

Trước đây khi thêm xe ở màn Danh mục xe, nếu tích `Xe nội bộ` thì hệ thống bắt buộc nhập `TL xe chuẩn`.

Sau khi chốt lại nghiệp vụ, xe nội bộ có thể được tạo trước trong danh mục mà chưa có TL bì hiệu lực trong ngày. TL bì sẽ được xác định sau bằng lượt cân 2 lần.

### Mong muốn

- Cho phép tạo xe nội bộ mà chưa nhập TL bì.
- Nếu người dùng có nhập TL bì thì giá trị phải lớn hơn `0`.
- Nếu không nhập TL bì, không set ngày hiệu lực TL bì.
- Xe nội bộ chưa có TL bì hiệu lực khi vào cân sẽ đi luồng cân 2 lần.
- Đổi text hiển thị từ `TL xe chuẩn` / `trọng lượng xe chuẩn` sang `TL bì` / `trọng lượng bì`.

### Hướng xử lý

- Bỏ validate bắt buộc nhập TL bì khi `EditIsInternalVehicle = true`.
- Chỉ validate `EditTtcpWeight > 0` khi có nhập giá trị.
- Khi tạo/sửa xe, chỉ set `StandardTareUpdatedAt` và `StandardTareUpdatedBy` nếu xe nội bộ có TL bì `> 0`.
- Rà soát toast, validation, modal đổi biển số và message use case để thống nhất text `TL bì`.

### Trạng thái

Đã xử lý và build UI thành công.

## 6. Luồng Đổi số xe ở màn Cân mỏ đá và định hướng áp dụng cho Cân mỏ sét

### Bối cảnh

Màn Cân mỏ đá đã có nút `Đổi số xe` để sửa xe đã chọn nhầm trên một lượt cân. Cần rà soát lại toàn bộ các nhánh đã xử lý trước khi bổ sung nút tương tự cho màn Cân mỏ sét.

Các điểm code hiện tại:

- UI nút thao tác: `CrusherWeighingView.xaml`, command `EditSessionVehicleCommand`.
- ViewModel mở modal: `CrusherWeighingViewModel.EditSessionVehicleAsync`.
- Modal nhập xe mới và lý do: `EditWeighingSessionVehicleViewModel` / `EditWeighingSessionVehicleWindow.xaml`.
- Use case xử lý nghiệp vụ thật: `CrusherWeighingUseCases.UpdateSessionVehicleAsync`.
- Lịch sử chỉnh sửa đọc audit: `WeighingSessionEditHistoryViewModel`.

### Nguyên tắc chung

- Chỉ cho đổi sang xe nội bộ đang active.
- Bắt buộc nhập lý do sửa đổi.
- Biển số xe mới phải khác biển số xe cũ.
- Trọng lượng tổng (`Weight1`) của lượt cân không thay đổi.
- Trọng lượng bì và trọng lượng hàng sau sửa phụ thuộc TL bì hiệu lực của xe mới hoặc `Weight2` hiện có của lượt cân.
- Sau khi đổi xe phải reload danh sách, chọn lại lượt vừa sửa và ghi audit `EDIT_WEIGHING_SESSION`.

### Modal dự kiến sau sửa

Modal đang hiển thị 2 khối:

- `Thông tin lượt cân hiện tại`.
- `Số liệu dự kiến sau sửa`.

Các trường cần giữ thống nhất:

- Số lượt cân.
- Biển số xe.
- Trọng lượng tổng.
- Trọng lượng bì.
- Trọng lượng hàng.

Logic preview:

- `Trọng lượng tổng` sau sửa luôn bằng `Weight1` hiện tại.
- Nếu xe mới có TL bì hiệu lực hôm nay, preview `Trọng lượng bì` = TL bì hiệu lực của xe mới.
- Nếu xe mới chưa có TL bì hiệu lực nhưng lượt cân có `Weight2`, preview `Trọng lượng bì` = `Weight2` hiện tại vì có thể chuyển `Weight2` này sang xe mới.
- `Trọng lượng hàng` preview = `Weight1 - Trọng lượng bì`.

### 4 case nghiệp vụ cần cover khi Đổi số xe

Code hiện tại trong `CrusherWeighingUseCases.UpdateSessionVehicleAsync` và `ClayWeighingUseCases.UpdateSessionVehicleAsync` đang chia nhánh theo:

- `isCompleted = session.SessionStatus == COMPLETED`.
- TL bì hiệu lực hôm nay của xe mới.
- `session.Weight2` có/không có giá trị.

Khi diễn giải theo nghiệp vụ cân, cần cover 4 case chính dưới đây.

### Case 1: Lượt mới cân 1 lần, đổi sang xe mới đã có TL bì hiệu lực

Điều kiện:

- Lượt đang sửa mới có `Weight1` là TL tổng.
- Lượt chưa có `Weight2`.
- Trạng thái thường là `PENDING_WEIGHT2` hoặc chưa `COMPLETED`.
- Xe mới có TL bì hiệu lực trong ngày.

Kết quả:

- Chuyển lượt sang chế độ `SINGLE_WITH_STANDARD_TARE`.
- Set `Weight2` = TL bì hiệu lực của xe mới.
- Set `Weight2Time` = thời điểm sửa.
- Set `NetWeightCalculationMode = Weight1MinusStandardTare`.
- Set `SessionStatus = COMPLETED`.
- `NetWeight = Weight1 - TL bì xe mới`.
- `StandardTareVehicleId` chuyển sang xe mới.
- `StandardTareWeightSnapshot` = TL bì hiệu lực của xe mới.

Ý nghĩa: lượt đã có TL tổng, xe mới đã có bì hợp lệ trong ngày, nên có thể hoàn tất lượt cân mà không cần lấy cân lần 2 từ thiết bị.

Mapping code hiện tại:

- Nhánh `!isCompleted && effectiveStandardTare.HasValue`.

### Case 2: Lượt mới cân 1 lần, đổi sang xe mới chưa có TL bì hiệu lực

Điều kiện:

- Lượt đang sửa mới có `Weight1` là TL tổng.
- Lượt chưa có `Weight2`.
- Trạng thái thường là `PENDING_WEIGHT2` hoặc chưa `COMPLETED`.
- Xe mới chưa có TL bì hiệu lực trong ngày.

Kết quả:

- Chuyển/giữ chế độ `TWO_WEIGH`.
- Clear `Weight2`, `Weight2Time`.
- `NetWeightCalculationMode = Weight2Diff`.
- `SessionStatus = PENDING_WEIGHT2`.
- `NetWeight = null`.
- `StandardTareVehicleId` chuyển sang xe mới.
- `StandardTareWeightSnapshot = null`.

Ý nghĩa: xe mới chưa có bì, lượt vẫn phải chờ cân lần 2 để xác định TL bì đúng của xe mới.

Mapping code hiện tại:

- Nhánh `!isCompleted && !effectiveStandardTare.HasValue`.

### Case 3: Lượt đã cân 2 lần, đổi sang xe mới đã có TL bì hiệu lực

Điều kiện:

- Lượt đang sửa đã `COMPLETED`.
- Lượt đã có đủ `Weight1` và `Weight2`.
- Xe mới có TL bì hiệu lực hôm nay.

Kết quả:

- Không ghi đè master TL bì của xe mới.
- Set `Weight2` = TL bì hiệu lực của xe mới.
- Set `Weight2Time` = thời điểm sửa.
- `NetWeightCalculationMode = Weight1MinusStandardTare`.
- Giữ `SessionStatus = COMPLETED`.
- `NetWeight = Weight1 - TL bì xe mới`.
- `StandardTareWeightSnapshot` = TL bì hiệu lực của xe mới.
- `StandardTareVehicleId` chuyển sang xe mới.
- Có thể vô hiệu TL bì xe cũ nếu đủ điều kiện ở mục `Vô hiệu hóa TL bì sai của xe cũ`.

Ý nghĩa: dữ liệu sau sửa phải phản ánh đúng xe mới; ví dụ `Weight1 = 54,000`, xe mới có TL bì `20,000` thì `Weight2 = 20,000`, `NetWeight = 34,000`.

Mapping code hiện tại:

- Nhánh `isCompleted && effectiveStandardTare.HasValue`.

### Case 4: Lượt đã cân 2 lần, đổi sang xe mới chưa có TL bì hiệu lực

Điều kiện:

- Lượt đang sửa đã `COMPLETED`.
- Lượt đã có đủ `Weight1` và `Weight2`.
- Xe mới chưa có TL bì hiệu lực trong ngày.

Kết quả:

- Không đưa lượt về `PENDING_WEIGHT2`.
- Giữ `SessionStatus = COMPLETED`.
- Dùng `Weight2` hiện có làm TL bì thực tế của xe mới.
- `StandardTareWeightSnapshot = Weight2`.
- `StandardTareVehicleId` chuyển sang xe mới.
- Gán `Weight2` vào master xe mới làm TL bì hiệu lực hôm nay.
- `NetWeightCalculationMode = Weight1MinusStandardTare`.
- `NetWeight = Weight1 - Weight2`.
- Ghi audit `AppliedStandardTareToNewVehicle`.
- Có thể vô hiệu TL bì xe cũ nếu đủ điều kiện ở mục `Vô hiệu hóa TL bì sai của xe cũ`.

Ý nghĩa: lượt cân thực tế đã có đủ tổng và bì, lỗi chỉ là chọn nhầm xe. Vì vậy phải chuyển bì sang đúng xe thay vì bắt cân lại.

Mapping code hiện tại:

- Nhánh `isCompleted && session.Weight2.HasValue && !effectiveStandardTare.HasValue`.
- Cờ `shouldApplyExistingWeight2AsStandardTareToNewVehicle = true`.

### Ngoài 4 case chính: dữ liệu hoàn thành nhưng thiếu Weight2

Điều kiện:

- Lượt đang sửa đã `COMPLETED`.
- Xe mới chưa có TL bì hiệu lực.
- Lượt không có `Weight2`.

Kết quả:

- Chuyển về chế độ `TWO_WEIGH`.
- Clear `Weight2`, `Weight2Time`.
- `NetWeightCalculationMode = Weight2Diff`.
- `SessionStatus = PENDING_WEIGHT2`.
- `NetWeight = null`.

Ý nghĩa: đây không phải case nghiệp vụ chuẩn của lượt đã cân 2 lần, nhưng code vẫn có guard để tránh suy luận bì khi không có dữ liệu đủ tin cậy.

### Logic dùng chung: Vô hiệu hóa TL bì sai của xe cũ

Khi đổi xe, có thể cần clear TL bì hiện tại của xe cũ nếu TL bì đó được sinh ra từ chính lượt cân đang bị đổi nhầm.

Chỉ vô hiệu hóa khi đủ tất cả điều kiện:

- Xe cũ khác xe mới và load được theo `session.StandardTareVehicleId`.
- Lượt đang sửa đã `COMPLETED`.
- Lượt có `Weight2`.
- Xe cũ đang có `TtcpWeight`.
- Xe cũ có `StandardTareUpdatedAt` trong ngày hiện tại.
- `TtcpWeight` của xe cũ khớp `session.Weight2` sau khi round 3 chữ số.
- Xe cũ chỉ có đúng `1` lượt cân hoàn thành sinh ra TL bì trong ngày theo `StandardTareVehicleId`.

Kết quả nếu đủ điều kiện:

- Set `oldVehicle.TtcpWeight = null`.
- Set `oldVehicle.StandardTareUpdatedAt = null`.
- Cập nhật `UpdatedAt`, `UpdatedBy`.
- Queue sync master xe cũ.
- Ghi audit `InvalidatedOldVehicleStandardTare`.

Không được vô hiệu hóa nếu xe cũ có nhiều hơn 1 lượt hoàn thành trong ngày, vì có thể TL bì hiện tại đến từ lượt đúng khác.

### Logic dùng chung: Audit log và lịch sử chỉnh sửa

Use case ghi audit `EDIT_WEIGHING_SESSION` với các phần chính:

- `Reason`.
- `Changes.VehiclePlate`.
- `Changes.StandardTareWeightSnapshot`.
- `Changes.Weight2`.
- `Changes.NetWeight`.
- `InvalidatedOldVehicleStandardTare` nếu có clear bì xe cũ.
- `AppliedStandardTareToNewVehicle` nếu có áp `Weight2` sang xe mới.

Màn `Lịch sử chỉnh sửa` hiện parse:

- Biển số cũ/mới.
- TL bì cũ/mới.
- TL hàng cũ/mới.
- Ghi chú vô hiệu TL bì xe cũ.

Khi áp dụng cho Cân mỏ sét cần dùng chung cấu trúc audit để màn lịch sử hiển thị được ngay.

### Điểm cần áp dụng cho Cân mỏ sét

Mỏ sét hiện đã có `ClayWeighingUseCases.UpdateSessionVehicleAsync` với logic nghiệp vụ tương tự Mỏ đá. Khi chuẩn bị code nút UI, cần bảo đảm đủ test cho cả 4 case nghiệp vụ ở trên.

Các test Mỏ sét hiện đã có và đang cover nhánh lượt đã cân 2 lần:

- `ClayUpdateSessionVehicleAsync_InvalidatesOldVehicleStandardTare_AndAppliesWeight2_WhenNewVehicleHasNoEffectiveTare`.
- `ClayUpdateSessionVehicleAsync_UsesNewVehicleStandardTare_WhenCompletedSessionChangesVehicle`.

Cần bổ sung/đối chiếu thêm test cho nhánh lượt mới cân 1 lần:

- Case 1: mới có `Weight1`, đổi sang xe mới đã có TL bì hiệu lực thì hoàn tất lượt và tính `NetWeight = Weight1 - TL bì xe mới`.
- Case 2: mới có `Weight1`, đổi sang xe mới chưa có TL bì hiệu lực thì giữ/chuyển `PENDING_WEIGHT2`, clear `Weight2`, chưa tính `NetWeight`.

Khi code nút `Đổi số xe` ở màn Cân mỏ sét cần:

- Thêm nút trên toolbar chuyến xe, command tương tự `EditSessionVehicleCommand`.
- Chỉ enable khi có `SelectedSession` hoặc `SelectedTrip` tương ứng.
- Mở lại `EditWeighingSessionVehicleViewModel` để dùng chung modal preview.
- Gọi `ClayWeighingUseCases.UpdateSessionVehicleAsync`, không gọi use case Mỏ đá.
- Sau khi lưu, reload tàu/chuyến/lượt cân và chọn lại chuyến vừa sửa.
- Bảo toàn quan hệ chuyến xe với tàu/cắt lệnh hiện tại, chỉ đổi xe và số liệu cân trên session.
- Sau khi đổi xe, các KPI/tổng tàu phải được lấy lại từ repository thay vì tự cộng từ state cũ.

### Trạng thái

Đã rà soát và ghi nhận để chuẩn bị code nút `Đổi số xe` cho màn Cân mỏ sét. Chưa thêm nút ở Mỏ sét trong mục này.
