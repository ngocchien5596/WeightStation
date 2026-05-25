# SRS: CO Lại Cắt Lệnh Và Kế Thừa Lượt Cân Cũ

## 1. Mục tiêu

Tài liệu này mô tả nghiệp vụ khi xe đã vào lấy hàng, đã có `cân lần 1`, nhưng khách yêu cầu đổi sản phẩm hoặc ERP phải `RA/CO lại cắt lệnh`.

Mục tiêu của giải pháp:

- không làm mất dữ liệu `cân lần 1` đã cân
- không để `cắt lệnh cũ` tiếp tục hiện trong luồng vận hành active
- cho phép `cắt lệnh mới` sau khi `CO lại` tự nhận diện đúng `lượt cân` cũ phù hợp
- hỗ trợ nhân viên cân thao tác ít nhất có thể

## 2. Bối cảnh nghiệp vụ

### 2.1 Tình huống phát sinh

1. Xe đã được tạo `lượt cân`.
2. Xe đã thực hiện `cân lần 1`.
3. Trong lúc xe đang lấy hàng, khách yêu cầu đổi sản phẩm hoặc thay đổi thông tin cắt lệnh.
4. Nhân viên ERP thực hiện:
   - `RA cắt lệnh` cũ
   - sửa thông tin
   - `CO lại` cắt lệnh mới xuống trạm cân

### 2.2 Vấn đề nếu không xử lý đúng

- mất liên kết giữa `cắt lệnh mới` và `lượt cân cũ`
- mất dữ liệu `cân lần 1`
- sinh thêm `lượt cân` mới không cần thiết
- session cũ còn line mồ côi, dẫn tới:
  - `STT` bị lệch
  - bị hiểu nhầm là nhiều cắt lệnh
  - không auto phân bổ khi thực tế chỉ còn 1 cắt lệnh

## 3. Khái niệm chính

### 3.1 Cắt lệnh

Lưu trong bảng `cut_orders`.

Các mã cần phân biệt:

- `ErpCutOrderId`
  - mã cắt lệnh hiện tại từ ERP
  - có thể thay đổi khi `RA/CO lại`
- `ErpRegistrationCode`
  - mã ĐKPT gốc do ERP cấp
  - không thay đổi khi `CO lại`
  - dùng để nối `cắt lệnh mới` với `cắt lệnh cũ đã soft delete`

### 3.2 Lượt cân

Lưu trong bảng `weighing_sessions`.

Một `lượt cân` có thể:

- chưa cân lần 1
- đã cân lần 1, chờ cân lần 2
- chờ phân bổ
- sẵn sàng hoàn tất
- hoàn tất
- hủy

## 4. Phạm vi nghiệp vụ cần lưu

Tài liệu này bao phủ các luồng:

- `soft delete` cắt lệnh cũ khi ERP `RA lại`
- lưu giữ `cân lần 1`
- `CO lại` cắt lệnh mới
- tự gợi ý `Lượt cân` phù hợp ở màn `Danh sách xe vào`
- gắn cắt lệnh mới vào `lượt cân` cũ
- dọn `orphan line`
- tự phân bổ nếu thực tế chỉ còn 1 cắt lệnh

## 5. Yêu cầu dữ liệu

### 5.1 Bổ sung vào `cut_orders`

Cần có thêm cột:

- `ErpRegistrationCode`

Ý nghĩa:

- lưu `mã ĐKPT` gốc từ ERP
- đây là khóa nghiệp vụ để xác định `cắt lệnh mới` có phải là bản phát sinh từ cùng một ĐKPT cũ hay không

### 5.2 Dữ liệu giữ lại ở bản ghi soft delete cũ

Khi `cắt lệnh cũ` bị soft delete, vẫn phải giữ:

- `ErpRegistrationCode`
- `CarryForwardWeight1`
- `CarryForwardWeight1Time`
- `WeighingSessionId` cũ nếu có
- `VehiclePlate`
- `MoocNumber`
- `TransactionType`

## 6. Luồng nghiệp vụ chuẩn

### 6.1 ERP RA cắt lệnh cũ

ERP gọi proc:

- `dbo.sp_SoftDeleteCutOrderDocumentsForReissue`

Proc phải thực hiện:

1. soft delete các chứng từ liên quan:
   - `weigh_tickets`
   - `delivery_tickets`
2. soft delete:
   - `cut_orders` cũ
   - `weighing_session_lines` cũ liên quan nếu cần archive
   - `weighing_sessions` trong phạm vi reissue nếu session thực sự không còn cắt lệnh active
3. snapshot `cân lần 1` vào bản ghi cũ:
   - `CarryForwardWeight1`
   - `CarryForwardWeight1Time`

### 6.2 ERP CO lại cắt lệnh mới

ERP insert hoặc sync xuống một `cut_orders` active mới, trong đó:

- `ErpCutOrderId` là mã cắt lệnh mới
- `ErpRegistrationCode` vẫn bằng `ĐKPT` cũ

### 6.3 Màn Danh sách xe vào

Khi người dùng chọn `cắt lệnh mới`, hệ thống phải:

1. tìm `cut_orders` đã soft delete gần nhất có cùng `ErpRegistrationCode`
2. lấy ra:
   - `CarryForwardWeight1`
   - `CarryForwardWeight1Time`
   - `WeighingSessionId` hoặc `SessionNo` phù hợp
3. tự điền ô `Lượt cân` nếu tìm được `session` hợp lệ

### 6.4 Điều kiện để tự điền `Lượt cân`

Chỉ tự điền khi session cũ:

- chưa `COMPLETED`
- chưa `CANCELLED`
- cùng `TransactionType`
- cùng `VehiclePlate`
- nếu có `Mooc` thì cùng `Mooc`
- ưu tiên:
  - `PENDING_WEIGHT2`
  - hoặc case recover `ALLOCATION_PENDING` nhưng thực tế chỉ còn 0 hoặc 1 cắt lệnh active

Nếu có nhiều session phù hợp:

- không tự điền bừa
- hoặc lấy session mới nhất theo rule đã chốt
- hoặc buộc người dùng xác nhận/chọn session

## 7. Luồng thao tác ở trạm cân

### 7.1 Nếu ô `Lượt cân` đã được tự điền hoặc người dùng nhập tay

Khi người dùng bấm `Tạo lượt cân` và ô `Lượt cân` có giá trị:

- hệ thống hiểu đây là luồng `gắn vào lượt cân cũ`
- hệ thống phải tìm đúng `weighing session` theo `SessionNo`
- hệ thống không được tự tạo session mới trước khi người dùng xác nhận xong

Áp dụng chung cho cả 2 trường hợp:

- `auto suggest`
- `manual attach`

### 7.2 Confirm dùng lại cân lần 1

Nếu `session` được chỉ định đã có `cân lần 1`, hệ thống phải hiển thị cùng một confirm modal:

- đã có số `cân lần 1`
- thời gian `cân lần 1`
- người dùng có đồng ý gắn `cắt lệnh` vào chính `lượt cân` này để dùng lại số cân lần 1 hay không

Nội dung confirm này chỉ có ý nghĩa khi:

- `có session cũ phù hợp`
- và session đó thật sự đang giữ `Weight1`

### 7.3 Rule xử lý khi người dùng chọn trong confirm

Nếu `Đồng ý`:

- phải gắn `cắt lệnh` vào đúng `lượt cân cũ`
- phải giữ nguyên `Weight1`
- phải giữ nguyên `Weight1Time`
- không được tạo `lượt cân mới`

Nếu `Không`:

- không gắn vào `lượt cân cũ`
- tạo `lượt cân mới`
- `Weight1 = null`
- `Weight1Time = null`
- người dùng cân lần 1 lại như bình thường

### 7.4 Rule backend bắt chặt nghiệp vụ

Use case `CreateWeighingSession` chỉ được dùng cho luồng:

- tạo `lượt cân mới` thực sự

Use case này không được phép:

- tự động dùng lại `CarryForwardWeight1`
- tự động reuse session cũ
- tự bơm `Weight1` của session cũ vào session mới

Nếu muốn dùng lại `cân lần 1`, hệ thống bắt buộc phải đi qua luồng:

- tìm `lượt cân cũ` phù hợp
- người dùng xác nhận
- gắn vào chính `lượt cân cũ` đó

## 8. Xử lý orphan line

### 8.1 Định nghĩa

`orphan line` là line trong `weighing_session_lines` còn active nhưng:

- `CutOrderId` không còn join được về `cut_orders active`
- hoặc line thuộc cắt lệnh cũ đã soft delete

### 8.2 Yêu cầu xử lý

Khi gắn `cắt lệnh mới` vào session cũ, hệ thống phải:

1. phát hiện các `orphan line`
2. soft delete các line này
3. đưa trạng thái line về `CANCELLED`
4. xóa phân bổ thực và liên kết ticket nếu cần

Mục tiêu:

- grid chi tiết không còn hiện `STT` lệch
- session phản ánh đúng số line active thực tế

## 9. Tự động phân bổ khi chỉ còn 1 cắt lệnh

### 9.1 Rule nghiệp vụ

Nếu sau khi dọn `orphan line`, session thực tế chỉ còn đúng `1 cắt lệnh active`:

- hệ thống phải tự phân bổ luôn
- không bắt người dùng mở modal `Phân bổ`

### 9.2 Cách phân bổ

- `ActualAllocatedWeight = NetWeight`
- nếu là hàng `Bao`:
  - `ActualAllocatedBagCount` tính theo rule hiện hành của hệ thống
- session chuyển sang:
  - `READY_TO_COMPLETE`

## 10. Ảnh hưởng tới function ERP lấy sản lượng thực xuất

`fn_GetCutOrderNetWeight` phải luôn bỏ qua:

- `cut_orders.IsDeleted = 1`
- `weighing_sessions.IsDeleted = 1`
- `weighing_session_lines.IsDeleted = 1`

Như vậy:

- line mồ côi cũ không còn ảnh hưởng tới `NetWeight`
- sản lượng trả về cho ERP chỉ phản ánh dữ liệu active hợp lệ

## 11. Quy tắc hiển thị và nhập liệu

### 11.1 Màn Danh sách xe vào

Thêm ô:

- `Lượt cân`

Vị trí:

- dưới dòng `Ghi chú`

Hành vi:

- có thể tự điền từ `ErpRegistrationCode`
- người dùng cũng có thể nhập tay nếu cần
- nếu ô này có giá trị thì `Tạo lượt cân` phải ưu tiên xử lý theo luồng `gắn vào lượt cân cũ`
- confirm modal dùng lại `cân lần 1` phải áp dụng chung cho cả:
  - auto suggest
  - manual attach

### 11.2 Màn Lập phiếu cân

Khi session đã được recover đúng:

- không được còn line mồ côi active
- nếu chỉ còn 1 cắt lệnh thì không được yêu cầu phân bổ thủ công

## 12. Yêu cầu backend

### 12.1 Repository

Cần có query kiểu:

- tìm `cut_orders` deleted gần nhất theo `ErpRegistrationCode`
- lấy session phù hợp từ bản ghi deleted đó

### 12.2 Inbound processor

`CutOrderInboundProcessor` cần nhận và lưu:

- `ErpRegistrationCode`

### 12.3 Validation

Không được gắn cắt lệnh mới vào session nếu:

- khác `TransactionType`
- khác `VehiclePlate`
- khác `Mooc` theo rule đã chốt
- session đã `COMPLETED`
- session đã `CANCELLED`

Không được phép tạo `lượt cân mới` mà vẫn mang theo `Weight1` cũ.

Không được phép hiểu:

- `Đồng ý dùng lại cân lần 1`

theo nghĩa:

- lấy `Weight1` cũ để gắn sang một `session mới`

Vì `Weight1` gắn chặt với `lượt cân`, không gắn chặt với `cắt lệnh`.

## 13. Trường hợp ngoại lệ

### 13.1 Có nhiều cắt lệnh cũ cùng một ĐKPT

Nếu nhiều bản ghi deleted cùng `ErpRegistrationCode`:

- ưu tiên bản mới nhất theo `UpdatedAt` hoặc `DeletedAt`
- hoặc chỉ lấy bản có session còn hợp lệ nhất

### 13.2 Có nhiều session phù hợp

Nếu cùng `ĐKPT` nhưng có nhiều session mở:

- không tự điền bừa
- phải dùng rule ưu tiên rõ ràng hoặc yêu cầu user xác nhận

### 13.3 Session cũ đang ở trạng thái không phù hợp

Nếu session cũ:

- đã hoàn tất
- đã hủy
- hoặc dữ liệu line quá bẩn

thì:

- không tự gắn
- cho phép tạo session mới bình thường

## 14. Tác động hệ thống

### 14.1 DB

Phải cập nhật:

- `cut_orders`
  - thêm `ErpRegistrationCode`

### 14.2 Sync ERP

ERP cần gửi thêm:

- `ErpRegistrationCode`

### 14.3 UI

Phải cập nhật:

- màn `Danh sách xe vào`
  - thêm field `Lượt cân`
  - tự điền theo `ErpRegistrationCode`

## 15. Tiêu chí nghiệm thu

### 15.1 Case chuẩn

1. Cắt lệnh cũ `A` có `ĐKPT = X`, đã cân lần 1.
2. ERP `RA` cắt lệnh `A`, soft delete thành công.
3. ERP `CO` cắt lệnh mới `B`, vẫn có `ĐKPT = X`.
4. Màn `Danh sách xe vào` chọn `B`:
   - tự điền đúng `Lượt cân` cũ
   - hiện đúng confirm dùng lại `cân lần 1`
5. Nếu bấm `Đồng ý`:
   - không tạo session mới
   - gắn vào session cũ
   - giữ nguyên `Weight1/Weight1Time`
6. Nếu bấm `Không`:
   - không gắn vào session cũ
   - tạo `lượt cân mới`
   - `Weight1 = null`
7. Nếu session thực còn 1 line:
   - tự phân bổ
   - không cần mở modal phân bổ

### 15.2 Case dữ liệu bẩn

Nếu session cũ có line mồ côi:

- hệ thống phải tự soft delete line mồ côi
- grid không còn hiện `STT` lệch

### 15.3 Case ERP lấy sản lượng

`fn_GetCutOrderNetWeight` không được trả số bị cộng dồn từ line mồ côi hoặc line deleted.

## 16. Ghi chú triển khai

Ưu tiên kỹ thuật:

1. thêm `ErpRegistrationCode` vào `cut_orders`
2. cập nhật inbound ERP
3. thêm query truy dấu theo `ErpRegistrationCode`
4. auto-fill `Lượt cân` ở `Danh sách xe vào`
5. giữ cơ chế cleanup `orphan line`
6. giữ auto-phân bổ khi chỉ còn 1 cắt lệnh
