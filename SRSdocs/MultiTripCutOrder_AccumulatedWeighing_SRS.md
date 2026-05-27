# SRS: Cắt Lệnh Xuất Khẩu Lớn Qua Nhiều Lượt Cân

## 1. Mục tiêu

Tài liệu này mô tả phương án xử lý nghiệp vụ khi một `cắt lệnh xuất khẩu` có khối lượng lớn, không thể giao hết bằng một xe, nên phải đi qua nhiều `lượt cân` khác nhau.

Mục tiêu của giải pháp:

- lưu được `khối lượng thực giao tích lũy` của một cắt lệnh sau từng lượt xe
- hiển thị được `tiến độ giao hàng` của cắt lệnh
- cho phép cùng một `cắt lệnh` tiếp tục được dùng ở các lượt cân sau cho đến khi giao đủ
- không phá vỡ các luồng hiện có như:
  - cân lần 1, cân lần 2
  - phân bổ thực giao
  - phiếu cân, phiếu giao nhận
  - luồng quá tải

## 2. Bối cảnh nghiệp vụ

### 2.1 Tình huống phát sinh

1. ERP đẩy xuống một `cắt lệnh xuất khẩu` có `PlannedWeight` lớn.
2. Một xe không thể chở hết toàn bộ khối lượng đó.
3. Nhiều xe lần lượt vào lấy hàng theo cùng cắt lệnh.
4. Mỗi xe phát sinh một `lượt cân` riêng.
5. Sau mỗi lượt cân, hệ thống cần cộng dồn số kg thực giao vào chính cắt lệnh đó.

### 2.2 Kỳ vọng nghiệp vụ

Sau mỗi lượt cân, người dùng cần nhìn được:

- `Khối lượng kế hoạch`
- `Đã giao lũy kế`
- `Khối lượng giao trong lượt này`
- `Còn lại phải giao`
- `Số lượt xe đã chạy`

Chỉ khi giao đủ thì cắt lệnh mới được coi là hoàn tất.

## 3. Hiện trạng hệ thống

### 3.1 Điểm đã có sẵn

Hệ thống hiện đã có nền dữ liệu khá phù hợp:

- `weighing_sessions`
  - lưu thông tin từng lượt cân
- `weighing_session_lines`
  - lưu từng dòng cắt lệnh nằm trong một lượt cân
  - có `ActualAllocatedWeight`
  - có `ActualAllocatedBagCount`
- `delivery_tickets`
  - đang phản ánh khối lượng thực giao của từng dòng sau phân bổ
- `fn_GetCutOrderNetWeight`
  - hiện đã có hướng cộng `SUM(ActualAllocatedWeight)` theo `ErpCutOrderId`

Kết luận:

- dữ liệu `phát sinh theo từng lượt xe` đã có
- dữ liệu `tích lũy theo cắt lệnh` chưa được tổ chức đầy đủ ở tầng nghiệp vụ và UI

### 3.2 Điểm chưa phù hợp

Hệ thống hiện đang thiên về mô hình:

- `1 lượt cân -> nhiều cắt lệnh`

Nhưng chưa xử lý tốt mô hình:

- `1 cắt lệnh -> nhiều lượt cân`

Mâu thuẫn lớn nhất hiện nay:

1. Sau khi `Chuyển xe ra`, code đang đóng luôn cắt lệnh:
   - `CutOrderStatus = COMPLETED`
   - `ProcessingStage = OUT_YARD`
2. Điều này khiến hệ thống đang hiểu:
   - một cắt lệnh kết thúc sau một lượt xe
3. Với đơn xuất khẩu lớn, hiểu như vậy là sai nghiệp vụ.

## 4. Nguyên tắc thiết kế đề xuất

### 4.1 Nguồn sự thật

Không nên tạo bảng tổng hợp mới ngay ở giai đoạn đầu.

Nguồn dữ liệu chuẩn vẫn nên là:

- `weighing_session_lines`

Lý do:

- đây là nơi ghi nhận từng lần cắt lệnh tham gia một lượt cân
- đã có sẵn `ActualAllocatedWeight`
- phù hợp để truy ngược lịch sử từng xe
- tránh phát sinh lệch dữ liệu giữa bảng chi tiết và bảng tổng

### 4.2 Tư duy nghiệp vụ mới

Không coi `cắt lệnh` là hoàn tất sau một lượt cân.

Thay vào đó:

- mỗi lượt cân chỉ ghi nhận `một phần thực giao`
- cắt lệnh chỉ hoàn tất khi tổng tích lũy đã đạt mức kế hoạch, hoặc người dùng chủ động xác nhận đóng

## 5. Mô hình dữ liệu nghiệp vụ đề xuất

### 5.1 Các chỉ số cần có cho một cắt lệnh

Đối với mỗi cắt lệnh, hệ thống cần tính được:

- `PlannedWeight`
  - khối lượng kế hoạch từ ERP
- `AccumulatedWeight`
  - tổng `ActualAllocatedWeight` của tất cả `weighing_session_lines` active thuộc cắt lệnh
- `RemainingWeight`
  - `PlannedWeight - AccumulatedWeight`
- `TripCount`
  - số lượt cân hoàn tất đã phát sinh cho cắt lệnh

### 5.2 Cách tính đề xuất

`AccumulatedWeight` được tính từ:

- `weighing_session_lines.ActualAllocatedWeight`
- join với `weighing_sessions`
- loại trừ:
  - session bị hủy
  - line bị xóa mềm
  - line chưa phân bổ xong

### 5.3 Không thêm snapshot tổng trong phase đầu

Ở phase đầu:

- không thêm ngay các cột như `AccumulatedWeight`, `RemainingWeight` vào `cut_orders`
- chỉ tính động qua query/service

Ở phase sau, nếu cần tối ưu hiệu năng:

- có thể bổ sung snapshot/cache
- nhưng vẫn phải coi `weighing_session_lines` là nguồn chuẩn

## 6. Vòng đời cắt lệnh đề xuất

### 6.1 Khi tạo lượt cân mới

Nếu cắt lệnh:

- chưa giao đủ
- chưa gắn vào session active khác

thì vẫn cho phép đưa vào lượt cân mới.

### 6.2 Khi hoàn tất một lượt cân

Sau khi `Chuyển xe ra`:

- tính lại `AccumulatedWeight`
- tính lại `RemainingWeight`

Nếu `RemainingWeight > 0`:

- không set `CutOrderStatus = COMPLETED`
- không coi cắt lệnh đã xong
- cắt lệnh phải quay lại trạng thái sẵn sàng cho xe tiếp theo

Nếu `RemainingWeight <= 0`:

- mới set `CutOrderStatus = COMPLETED`
- `ProcessingStage = OUT_YARD`

### 6.3 Trạng thái đề xuất

Nên bổ sung thêm một trạng thái nghiệp vụ mới cho cắt lệnh, ví dụ:

- `PARTIAL_OUTBOUND`
hoặc
- `PARTIALLY_COMPLETED`

Mục đích:

- phân biệt rõ giữa:
  - chưa xe nào giao
  - đang giao dở
  - đã giao đủ

## 7. Xử lý trường `CutOrder.WeighingSessionId`

### 7.1 Vai trò đúng của field này

`CutOrder.WeighingSessionId` không phù hợp để làm lịch sử nhiều lượt cân.

Field này chỉ nên được dùng như:

- con trỏ tới `session đang active`

### 7.2 Quy tắc sử dụng mới

- khi cắt lệnh đang nằm trong một lượt cân active:
  - `WeighingSessionId = session hiện tại`
- khi lượt cân kết thúc nhưng cắt lệnh chưa giao đủ:
  - `WeighingSessionId = NULL`
- lịch sử các lượt đã chạy:
  - lấy từ `weighing_session_lines`

Kết luận:

- `WeighingSessionId` không còn là nguồn lịch sử
- mọi query lịch sử phải đi qua `weighing_session_lines`

## 8. Mâu thuẫn hiện tại cần sửa

### 8.1 Đóng cắt lệnh quá sớm

Trong luồng `MoveToOutYard`, hệ thống đang set:

- `CutOrderStatus = COMPLETED`
- `ProcessingStage = OUT_YARD`

cho toàn bộ cắt lệnh của session.

Đây là điểm phải sửa đầu tiên.

### 8.2 Query lịch sử đang bám vào `cut_orders.WeighingSessionId`

Một số query danh sách/lịch sử hiện đang đọc cắt lệnh theo `WeighingSessionId` từ `cut_orders`.

Khi chuyển sang mô hình nhiều lượt cân cho một cắt lệnh, cách này sẽ sai.

Các query lịch sử phải chuyển sang:

- join qua `weighing_session_lines`

### 8.3 UI chưa có khái niệm tích lũy

Hiện UI chủ yếu hiển thị:

- số kg của một lượt cân
- số kg của một phiếu

Nhưng chưa hiển thị tốt:

- cắt lệnh này đã giao được bao nhiêu lũy kế
- còn thiếu bao nhiêu
- đã đi qua bao nhiêu lượt xe

## 9. Thay đổi nghiệp vụ đề xuất

### 9.1 Service/query mới

Cần có một query hoặc service tổng hợp tiến độ cắt lệnh, trả về tối thiểu:

- `CutOrderId`
- `ErpCutOrderId`
- `PlannedWeight`
- `AccumulatedWeight`
- `RemainingWeight`
- `TripCount`
- `LastCompletedSessionNo`
- `LastCompletedAt`

### 9.2 Quy tắc hoàn tất cắt lệnh

Khi session hoàn tất:

- nếu tổng tích lũy chưa đủ:
  - cắt lệnh quay lại hàng chờ để tạo lượt xe tiếp theo
- nếu đủ:
  - cắt lệnh mới hoàn tất hẳn

### 9.3 Quy tắc tạo lượt cân mới cho cắt lệnh lớn

Khi tạo lượt cân mới:

- cho phép chọn lại cắt lệnh đã giao một phần
- nhưng không cho chọn nếu:
  - đang có session active khác
  - đã hoàn tất đủ khối lượng
  - đã bị hủy

## 10. Đề xuất hiển thị UI

### 10.1 Màn Danh sách xe vào

Thêm các cột:

- `KL kế hoạch`
- `Đã giao`
- `Còn lại`
- `Số lượt xe`
- `Tiến độ`

Tiến độ có thể hiển thị:

- `Chưa giao`
- `Đang giao dở`
- `Đã giao đủ`

### 10.2 Màn Lập phiếu cân

Ở grid chi tiết cắt lệnh trong một lượt cân, thêm:

- `Đã giao trước đó`
- `Phân bổ lượt này`
- `Còn lại sau lượt này`

Mục tiêu:

- nhân viên cân nhìn rõ lần này đang giao tiếp phần nào của cắt lệnh lớn

### 10.3 Lịch sử cắt lệnh

Cần có view hoặc modal hiển thị lịch sử giao theo cắt lệnh:

- `Số lượt cân`
- `Biển số xe`
- `Ngày cân`
- `KL phân bổ`
- `Lũy kế sau lượt này`

## 11. Tác động kỹ thuật chính

### 11.1 Use case cần sửa

Nhóm xử lý `weighing session`:

- logic hoàn tất session
- logic chuyển xe ra
- logic cho phép tạo session mới với cắt lệnh giao dở

### 11.2 Repository/query cần sửa

- các query đang lấy lịch sử theo `cut_orders.WeighingSessionId`
- các query danh sách xe ra / phiếu liên quan / chi tiết session
- bổ sung query tiến độ cắt lệnh

### 11.3 SQL cần sửa

- hàm tổng hợp net/actual theo cắt lệnh
- query báo cáo cắt lệnh
- query danh sách phục vụ UI

## 12. Lộ trình triển khai đề xuất

### Phase 1: Chuẩn hóa nghiệp vụ backend

1. Bổ sung query tính:
   - `AccumulatedWeight`
   - `RemainingWeight`
   - `TripCount`
2. Sửa logic hoàn tất session:
   - chưa đủ thì không đóng cắt lệnh
   - đủ mới đóng
3. Cho phép cắt lệnh đã giao một phần tiếp tục được tạo session mới

### Phase 2: Chuẩn hóa query lịch sử

1. Chuyển các query lịch sử từ `cut_orders.WeighingSessionId`
2. Dùng `weighing_session_lines` làm nguồn lịch sử chính
3. Rà toàn bộ màn có dữ liệu liên quan:
   - Danh sách xe vào
   - Lập phiếu cân
   - Danh sách xe ra
   - Danh sách phiếu liên quan

### Phase 3: Bổ sung UI tiến độ

1. Thêm các cột tiến độ ở `Danh sách xe vào`
2. Thêm thông tin lũy kế ở `Lập phiếu cân`
3. Thêm lịch sử giao theo cắt lệnh

### Phase 4: Tối ưu và báo cáo

1. Đánh giá hiệu năng query
2. Nếu cần thì bổ sung snapshot/cache
3. Đồng bộ logic báo cáo và in ấn

## 13. Acceptance Criteria

Giải pháp được coi là đạt khi:

1. Một cắt lệnh có thể đi qua nhiều lượt cân khác nhau.
2. Sau mỗi lượt cân, hệ thống lưu được số kg thực giao của chính lượt đó.
3. Hệ thống tính được `Đã giao lũy kế` của cắt lệnh.
4. Hệ thống tính được `Còn lại phải giao`.
5. Cắt lệnh chưa bị đóng nếu tổng giao chưa đủ.
6. Cắt lệnh chỉ `COMPLETED` khi đã giao đủ hoặc được đóng theo nghiệp vụ cho phép.
7. UI hiển thị được tiến độ giao của cắt lệnh.
8. Lịch sử từng lượt xe gắn với cắt lệnh được xem lại đầy đủ.
9. Không làm hỏng các luồng:
   - quá tải
   - phân bổ thực giao
   - phiếu cân
   - phiếu giao nhận

## 14. Khuyến nghị chốt phương án

Phương án nên chọn là:

- giữ `weighing_session_lines` làm nguồn dữ liệu chi tiết chuẩn
- không tạo bảng tổng mới ngay
- sửa vòng đời `cut order` để hỗ trợ `giao nhiều đợt`
- bổ sung query tiến độ và UI hiển thị lũy kế

Đây là hướng ít phá hệ thống nhất, tận dụng được nhiều cấu trúc hiện có, đồng thời mở rộng đúng bản chất nghiệp vụ của đơn xuất khẩu lớn.
