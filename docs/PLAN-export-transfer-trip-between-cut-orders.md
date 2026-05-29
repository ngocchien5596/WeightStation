# Kế hoạch chi tiết: Chuyển chuyến xe giữa các cắt lệnh ở màn Cân xuất khẩu

## 1. Mục tiêu

Bổ sung chức năng cho phép nhân viên cân chuyển một `chuyến xe` đã gắn nhầm từ `cắt lệnh nguồn` sang `cắt lệnh đích` trong luồng `Cân xuất khẩu`.

Mục tiêu nghiệp vụ:

- Giảm thao tác sửa tay hoặc hủy làm lại khi nhân viên chọn nhầm cắt lệnh.
- Giữ nguyên dữ liệu cân thực tế của chuyến xe:
  - số cân lần 1
  - số cân lần 2
  - net
  - ảnh lịch sử
- Sau khi chuyển, các thông tin tổng hợp của 2 cắt lệnh phải tự cập nhật đúng:
  - Lũy kế
  - Còn lại
  - Tổng chuyến đã lấy
  - Chuyến cuối

Phạm vi của plan này chỉ áp dụng cho màn `Cân xuất khẩu`, không áp dụng cho màn `Lập phiếu cân` nội địa.

## 2. Đánh giá tính khả thi với dự án hiện tại

Chức năng này **khả thi** với kiến trúc hiện tại vì:

- Quan hệ giữa `chuyến xe export` và `cắt lệnh export` hiện đang được lưu qua `weighing_session_lines.CutOrderId`.
- Thông tin tổng hợp của cắt lệnh ở màn `Cân xuất khẩu` đang được tính động từ `weighing_session_lines`, không lưu cứng.
- Danh sách chuyến xe của một cắt lệnh export cũng đang được load từ `weighing_session_lines + weighing_sessions`.

Điều này có nghĩa là:

- Nếu chuyển đúng `weighing_session_line` từ cắt lệnh A sang cắt lệnh B,
- và đồng bộ lại các chứng từ liên quan,
- thì phần `Lũy kế`, `Còn lại`, `Chuyến cuối` sẽ tự tính lại đúng sau khi reload màn hình.

## 3. Ràng buộc nghiệp vụ cần chốt

Trước khi code, cần thống nhất rõ các điều kiện cho phép chuyển.

Đề xuất an toàn:

- Chỉ cho chuyển trong luồng `Cân xuất khẩu`.
- Chỉ chuyển `1 chuyến xe` tại một thời điểm.
- Chỉ cho chuyển khi chuyến xe đã thuộc đúng một `weighing_session_line`.
- Không cho chuyển nếu `cắt lệnh nguồn` đã `chốt tổng`.
- Không cho chuyển nếu `cắt lệnh đích` đã `chốt tổng`.
- Không cho chuyển nếu chuyến xe đã đồng bộ thành công lên server trung tâm.
- Không cho chuyển nếu chuyến xe đã bị hủy.
- Chỉ cho chuyển giữa 2 cắt lệnh export đang còn mở.

Đề xuất thêm điều kiện tương thích dữ liệu:

- `TransactionType` phải cùng là `OUTBOUND`
- `IsExportScale = true`
- `ProductCode` phải giống nhau
- nên bắt buộc `CustomerCode` giống nhau

Lý do:

- Nếu cho chuyển giữa 2 cắt lệnh khác sản phẩm hoặc khác khách hàng thì chứng từ, báo cáo và đồng bộ ERP rất dễ sai bản chất nghiệp vụ.

## 4. Rủi ro chính cần xử lý

### 4.1 Không chỉ đổi `CutOrderId` của line

Nếu chỉ cập nhật `weighing_session_lines.CutOrderId` mà không cập nhật dữ liệu liên quan thì sẽ phát sinh lỗi:

- phiếu cân vẫn trỏ về cắt lệnh cũ
- phiếu giao nhận vẫn trỏ về cắt lệnh cũ
- màn tra cứu phiếu liên quan hiển thị sai
- sync lên server trung tâm bị sai cắt lệnh

### 4.2 Chứng từ đã in

Nếu chuyến xe đã in:

- `IN PC`
- `IN PGN`

thì cần quyết định rõ:

- có cho chuyển nữa hay không
- nếu có thì chứng từ cũ xử lý thế nào

Đề xuất phase đầu:

- **không cho chuyển nếu đã in phiếu cân hoặc phiếu giao nhận**

Lý do:

- tránh phải bổ sung nghiệp vụ hủy/in lại/chỉnh lại dấu vết chứng từ trong cùng phase đầu.

### 4.3 Đồng bộ server trung tâm

Nếu chuyến xe hoặc chứng từ đã `SYNC_SUCCESS`, việc đổi sang cắt lệnh khác ở local sẽ gây lệch với server trung tâm.

Đề xuất phase đầu:

- **không cho chuyển nếu bất kỳ chứng từ liên quan nào đã sync thành công**

## 5. Dữ liệu bị ảnh hưởng

### 5.1 Bảng `weighing_session_lines`

Đây là nơi gắn chuyến xe vào cắt lệnh.

Cần cập nhật:

- `CutOrderId`
- snapshot line nếu đang copy từ cắt lệnh:
  - `CustomerCode`
  - `CustomerName`
  - `DistributorName`
  - `ProductCode`
  - `ProductName`
  - `PlannedWeight`
  - `PlannedBagCount`
- `UpdatedAt`
- `UpdatedBy`

### 5.2 Bảng `weigh_tickets`

Các phiếu cân của `weighing_session` đang mang thông tin cắt lệnh và snapshot hiển thị.

Cần rà và cập nhật các bản ghi thuộc session đó:

- `CutOrderId`
- `ErpCutOrderId`
- `CustomerCode`
- `CustomerName`
- `ProductCode`
- `ProductName`
- `PlannedWeight`
- `BagCount`
- `Notes`
- `UpdatedAt`
- `UpdatedBy`
- `SyncStatus = SYNC_QUEUED`

### 5.3 Bảng `delivery_tickets`

Các phiếu giao nhận đang gắn theo `WeighingSessionLineId` và `CutOrderId`.

Cần cập nhật:

- `CutOrderId`
- `ErpCutOrderId`
- `CustomerCode`
- `ProductCode`
- `Notes`
- `UpdatedAt`
- `UpdatedBy`
- `SyncStatus = SYNC_QUEUED`

### 5.4 Bảng `cut_orders`

Không cần cộng trừ lũy kế thủ công nếu tiếp tục giữ mô hình tính động như hiện tại.

Chỉ cần đảm bảo:

- trạng thái `CurrentPrimaryWeighTicketId`
- trạng thái `CurrentPrimaryDeliveryTicketId`

không bị lệch nếu có use case hoặc màn hình nào còn dựa vào chúng.

## 6. Thiết kế UI đề xuất

### 6.1 Vị trí nút

Ở màn `Cân xuất khẩu`:

- Khi chọn `SelectedTrip`, hiển thị thêm nút `Chuyển cắt lệnh`.
- Khi không chọn chuyến xe, nút bị ẩn hoặc disable.

### 6.2 Cách chọn cắt lệnh đích

Yêu cầu gốc của người dùng mô tả modal confirm đã biết sẵn cắt lệnh đích. Tuy nhiên UI hiện tại chưa có vùng chọn cắt lệnh đích khi đang đứng ở một chuyến xe cụ thể.

Giải pháp phù hợp hơn:

1. Chọn `cắt lệnh nguồn`
2. Chọn `chuyến xe`
3. Nhấn `Chuyển cắt lệnh`
4. Mở modal chọn `cắt lệnh đích` từ danh sách eligible
5. Sau khi chọn đích, mở confirm modal cuối cùng

### 6.3 Nội dung modal confirm

Đề xuất text:

`Bạn có chắc chắn muốn chuyển chuyến xe {Biển số xe} với trọng lượng hàng {NET} từ cắt lệnh {Mã nguồn} sang cắt lệnh {Mã đích} không?`

Action:

- `Hủy`
- `Đồng ý`

Yêu cầu UX:

- Nhấn dấu `X` phải đóng modal, không thực hiện action ngầm.

## 7. Use case mới cần bổ sung

Đề xuất tạo use case mới:

- `ReassignExportTripUseCase`

Input:

- `SourceCutOrderId`
- `TargetCutOrderId`
- `WeighingSessionId`
- `WeighingSessionLineId`

### 7.1 Validate đầu vào

Use case phải kiểm tra:

- cắt lệnh nguồn tồn tại
- cắt lệnh đích tồn tại
- line tồn tại
- session tồn tại
- line thực sự đang thuộc cắt lệnh nguồn
- line thực sự thuộc session đang chọn
- nguồn và đích đều là cắt lệnh export
- nguồn và đích chưa chốt tổng
- session không bị hủy
- line không bị xóa
- line đã có `ActualAllocatedWeight`
- không có phiếu hoặc session nào đã `SYNC_SUCCESS`
- chưa in phiếu cân / phiếu giao nhận
- sản phẩm nguồn và đích tương thích
- khách hàng nguồn và đích tương thích

### 7.2 Xử lý chính

Trong cùng một transaction:

1. Cập nhật `weighing_session_line` sang `TargetCutOrderId`
2. Làm mới snapshot line theo cắt lệnh đích
3. Cập nhật toàn bộ `weigh_tickets` của session liên quan
4. Cập nhật toàn bộ `delivery_tickets` của line liên quan
5. Sửa lại các pointer primary của nguồn/đích nếu cần
6. Đẩy các chứng từ liên quan về `SYNC_QUEUED`

### 7.3 Output

Trả về kết quả đủ để UI reload đúng:

- `SourceCutOrderId`
- `TargetCutOrderId`
- `SessionId`
- `LineId`
- `TargetErpCutOrderId`

## 8. Ảnh hưởng tới các màn hình hiện tại

### 8.1 Màn `Cân xuất khẩu`

Cần sửa:

- thêm nút `Chuyển cắt lệnh`
- thêm command
- thêm modal chọn cắt lệnh đích
- thêm confirm modal
- reload lại:
  - grid cắt lệnh
  - grid chuyến của nguồn hoặc đích

### 8.2 Màn `Danh sách phiếu liên quan`

Nếu tra cứu theo `CutOrderId`, sau khi chuyển phải đảm bảo phiếu hiện đúng ở cắt lệnh mới.

### 8.3 Màn `Danh sách xe ra`

Luồng export ở màn này đang dựng theo `line + session`.

Sau khi chuyển:

- nếu line đã sang cắt lệnh đích,
- thì màn này phải hiển thị mã cắt lệnh đích.

Không cần xử lý đặc biệt nếu dữ liệu gốc đã cập nhật chuẩn.

### 8.4 In ấn

Do logic in export hiện đang dựa trên `SelectedTrip.SessionId`, sau khi đổi cắt lệnh:

- dữ liệu in phải lấy snapshot mới
- các header cắt lệnh trên phiếu phải phản ánh cắt lệnh đích

## 9. Phương án triển khai theo phase

### Phase 1: Chuyển chuyến an toàn, chưa hỗ trợ case phức tạp

Cho phép chuyển khi:

- chuyến đã có đủ cân
- chưa in phiếu
- chưa sync thành công
- nguồn/đích chưa chốt tổng
- cùng sản phẩm và cùng khách hàng

Mục tiêu:

- làm xong core flow an toàn
- hạn chế tối đa side effect

### Phase 2: Mở rộng nếu nghiệp vụ thật sự cần

Xem xét thêm:

- cho phép chuyển sau khi đã in nhưng chưa sync
- tự động đánh dấu/hủy phiếu cũ và sinh lại phiếu mới
- audit log lịch sử chuyển chuyến xe

## 10. Audit và log khuyến nghị

Nên bổ sung log nghiệp vụ ở mức info:

- session id
- line id
- source cut order id
- source ERP cut order id
- target cut order id
- target ERP cut order id
- net weight
- user thao tác
- thời điểm thao tác

Nếu có điều kiện, nên bổ sung bảng audit riêng trong phase sau:

- `export_trip_reassign_logs`

Phase đầu có thể tạm dùng application log.

## 11. Test cases cần có

### 11.1 Happy path

- Chuyển chuyến xe từ cắt lệnh A sang cắt lệnh B thành công
- Lũy kế A giảm đúng
- Lũy kế B tăng đúng
- Còn lại A/B cập nhật đúng
- Chuyến cuối A/B cập nhật đúng
- Grid chuyến của A không còn chuyến đó
- Grid chuyến của B xuất hiện chuyến đó

### 11.2 Validation

- Không cho chuyển nếu không chọn chuyến xe
- Không cho chuyển nếu chọn cùng một cắt lệnh nguồn và đích
- Không cho chuyển nếu cắt lệnh đích đã chốt
- Không cho chuyển nếu chuyến đã in phiếu
- Không cho chuyển nếu dữ liệu đã sync thành công
- Không cho chuyển nếu khác sản phẩm
- Không cho chuyển nếu khác khách hàng

### 11.3 Chứng từ

- Sau khi chuyển, `IN PC` hiển thị mã cắt lệnh mới
- Sau khi chuyển, `IN PGN` hiển thị mã cắt lệnh mới
- Màn phiếu liên quan không còn bám vào cắt lệnh cũ

### 11.4 Regression

- Không ảnh hưởng tới:
  - tạo chuyến xe export
  - cân lần 1
  - cân lần 2
  - chốt tổng
  - xem ảnh
  - in phiếu của chuyến bình thường

## 12. Đề xuất chốt trước khi code

Đề xuất chốt nghiệp vụ cho phase đầu như sau:

- Có làm chức năng `Chuyển cắt lệnh`
- Chỉ áp dụng cho `Cân xuất khẩu`
- Chỉ cho chuyển khi chuyến chưa in và chưa sync thành công
- Không cho chuyển nếu nguồn hoặc đích đã chốt tổng
- Chỉ cho chuyển giữa 2 cắt lệnh cùng `CustomerCode` và `ProductCode`
- Chỉ cập nhật dữ liệu local và đưa lại `SYNC_QUEUED`

## 13. Kết luận

Chức năng này phù hợp với dự án hiện tại và có thể triển khai mà không cần thay đổi lớn kiến trúc hoặc thêm bảng mới trong phase đầu.

Điểm mấu chốt để làm đúng là:

- cập nhật đồng bộ `weighing_session_line`
- cập nhật đồng bộ `weigh_tickets`
- cập nhật đồng bộ `delivery_tickets`
- khóa các case đã in hoặc đã sync thành công

Nếu triển khai theo đúng phạm vi đã chốt ở plan này, rủi ro nghiệp vụ nằm ở mức chấp nhận được.
