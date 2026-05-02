Bạn là Principal WPF Product Engineer + Workflow Architect + .NET Application Engineer của dự án StationApp.

Nhiệm vụ của bạn là triển khai lại logic “kết thúc lượt xe” trong mô hình `Weighing Session` theo đúng nghiệp vụ sau. Đây là source of truth cuối cùng. Không suy diễn theo logic cũ.

==================================================
1. MỤC TIÊU
==================================================

Tôi cần một nút riêng trên màn `Lập phiếu cân / WeighingView` để chuyển một lượt xe sang `Out Yard`.

Tên nút chốt:
- `Chuyển xe ra`

Ý nghĩa:
- lượt xe đã hoàn tất cân
- đã hoàn tất phân bổ
- user xác nhận xe rời khu vực cân
- session được chuyển sang màn `Danh sách xe ra`

Rất quan trọng:
- KHÔNG dùng việc in phiếu làm điều kiện để chuyển Out Yard
- KHÔNG auto chuyển Out Yard khi in đủ phiếu
- In chứng từ và chuyển Out Yard là 2 nghiệp vụ độc lập

==================================================
2. LOGIC CHỐT CUỐI CẦN ÁP DỤNG
==================================================

## 2.1 Bỏ logic cũ
Phải tìm và gỡ toàn bộ logic kiểu:
- in đủ phiếu cân + phiếu giao nhận thì tự chuyển session sang Out Yard
- hoặc bất kỳ auto move nào đang phụ thuộc vào `IsPrinted`, `HasPrintedMasterWeighTicket`, `HasPrintedDeliveryTicket`

Không được giữ bất kỳ dependency nào giữa printing và move to out yard.

## 2.2 Logic mới
Session chỉ được chuyển Out Yard khi:
- đã hoàn tất cân
- đã hoàn tất phân bổ

Điều kiện kỹ thuật chốt:
- `SessionStatus == READY_TO_COMPLETE`

Sau khi user bấm nút `Chuyển xe ra` và xác nhận:
- `weighing_sessions.SessionStatus = COMPLETED`
- tất cả `vehicle_registrations` thuộc session:
  - `RegistrationStatus = COMPLETED`
  - `ProcessingStage = OUT_YARD`

==================================================
3. STATUS MODEL CẦN CẬP NHẬT
==================================================

Phải review và cập nhật enum/constants/status model của `weighing_sessions`.

Bộ trạng thái chốt cuối:
- `PENDING_WEIGHT1`
- `PENDING_WEIGHT2`
- `ALLOCATION_PENDING`
- `READY_TO_COMPLETE`
- `COMPLETED`
- `CANCELLED`

### Bắt buộc
Nếu codebase hiện đang dùng `READY_TO_PRINT` để biểu diễn “đã xong cân + xong phân bổ”, hãy refactor đổi nghĩa hoặc đổi tên thành:
- `READY_TO_COMPLETE`

Khuyến nghị tốt nhất:
- đổi hẳn enum/value sang `READY_TO_COMPLETE`
- cập nhật toàn bộ code liên quan

Nếu việc đổi tên status làm ảnh hưởng lớn, bạn vẫn phải implement đúng nghĩa nghiệp vụ:
- trạng thái trước khi hoàn tất là trạng thái “đã cân xong + phân bổ xong”
- và nó KHÔNG còn được coi là trạng thái phụ thuộc in

Nhưng phương án ưu tiên vẫn là:
- refactor sang `READY_TO_COMPLETE`

==================================================
4. ĐIỀU KIỆN ENABLE NÚT `CHUYỂN XE RA`
==================================================

Nút này chỉ được enable khi tất cả điều kiện sau đúng:

1. Có session đang được chọn
2. Session chưa `COMPLETED`
3. Session chưa `CANCELLED`
4. Đã có `Weight1`
5. Đã có `Weight2`
6. Đã có `NetWeight`
7. Đã phân bổ xong cho các line
8. Tổng phân bổ hợp lệ

### Rule implementation tối ưu
Nút enable khi:
- `SessionStatus == READY_TO_COMPLETE`

Nút disable khi:
- session null
- `SessionStatus != READY_TO_COMPLETE`
- hoặc session đã cancelled/completed

==================================================
5. HÀNH VI KHI USER BẤM NÚT
==================================================

Khi user bấm `Chuyển xe ra`:

### Bước 1
Validate lại:
- session tồn tại
- session ở trạng thái cho phép
- session chưa bị hủy
- session chưa hoàn tất
- session đã có đủ dữ liệu cân
- session đã phân bổ xong

### Bước 2
Hiện modal xác nhận đồng bộ với system design của app

#### Tiêu đề
- `Xác nhận chuyển xe ra`

#### Nội dung
- `Bạn có chắc muốn chuyển lượt xe này sang Danh sách xe ra không?`

#### Nút
- `Chuyển xe ra`
- `Không`

### Bước 3
Nếu user xác nhận:
Cập nhật DB trong transaction:

#### `weighing_sessions`
- `SessionStatus = COMPLETED`
- `UpdatedAt = clockService.NowLocal`
- `UpdatedBy = currentUser`

#### Tất cả `vehicle_registrations` thuộc session
- `RegistrationStatus = COMPLETED`
- `ProcessingStage = OUT_YARD`
- `UpdatedAt = clockService.NowLocal`
- `UpdatedBy = currentUser`

### Bước 4
UI cập nhật:
- session biến mất khỏi màn `Lập phiếu cân`
- session xuất hiện ở màn `Danh sách xe ra`
- không crash
- không giữ selection lỗi
- refresh hợp lý

==================================================
6. KHÔNG ĐƯỢC CẬP NHẬT LOGIC IN ẤN KHI CHUYỂN OUT YARD
==================================================

Khi bấm `Chuyển xe ra`, KHÔNG được:

- check `HasPrintedMasterWeighTicket`
- check `HasPrintedDeliveryTicket`
- check `weigh_tickets.IsPrinted`
- check `delivery_tickets.IsPrinted`
- set printed flags giả
- ép user in trước khi ra

In ấn là logic độc lập.

==================================================
7. HỆ QUẢ UX BẮT BUỘC SAU KHI SESSION SANG OUT YARD
==================================================

Vì chuyển Out Yard không phụ thuộc vào in, nên sau khi session đã ở màn `Danh sách xe ra`, user vẫn phải có cách để:

- xem chi tiết session
- in phiếu cân
- in phiếu giao nhận

### Yêu cầu
Màn `Danh sách xe ra` hoặc màn detail mở từ nó phải hỗ trợ các action hậu kỳ:
- `In phiếu cân`
- `In phiếu giao nhận`
- `Xem chi tiết`

Không được để session sang `Out Yard` rồi mất luôn khả năng in.

==================================================
8. ẢNH HƯỞNG ĐẾN 3 MÀN
==================================================

## 8.1 Màn Danh sách xe vào
Không thay đổi logic trực tiếp

## 8.2 Màn Lập phiếu cân
Phải bổ sung:
- nút `Chuyển xe ra`

Và nút này phải:
- có style đồng bộ
- có enable/disable rõ ràng
- có confirm modal

## 8.3 Màn Danh sách xe ra
Phải hiển thị session theo rule mới:
- `SessionStatus = COMPLETED`
- `IsCancelled = false`

KHÔNG phụ thuộc vào in ấn

Ngoài ra cần có action:
- xem chi tiết
- in PC
- in PGN

==================================================
9. QUAN HỆ VỚI INBOUND / OUTBOUND
==================================================

Nút `Chuyển xe ra` áp dụng cho cả:
- `OUTBOUND`
- `INBOUND`

### Rule chung
Miễn là session:
- đã xong cân
- đã xong phân bổ
- ở trạng thái `READY_TO_COMPLETE`

thì đều được chuyển sang `COMPLETED`

Với `INBOUND`:
- vẫn dùng cùng cơ chế
- không tạo logic riêng cho nút này

==================================================
10. PHÂN QUYỀN
==================================================

Nút `Chuyển xe ra` phải dùng được cho cả:
- `ADMIN`
- `OPERATOR`

### Không giới hạn admin-only
Vì đây là hành động vận hành bình thường.

### Rule giữ nguyên
- `OPERATOR` chỉ được cân tự động
- `ADMIN` mới được cân tay

Nhưng khi đã đủ điều kiện hoàn tất session:
- cả ADMIN và OPERATOR đều được bấm `Chuyển xe ra`

==================================================
11. TOAST / FEEDBACK BẮT BUỘC
==================================================

### Khi chưa đủ điều kiện mà command bị gọi
- `Lượt xe chưa đủ điều kiện để chuyển ra.`
hoặc
- `Lượt xe chưa hoàn tất cân hoặc chưa phân bổ xong.`

### Khi thành công
- `Đã chuyển lượt xe sang Danh sách xe ra.`

### Khi thất bại
- `Không thể chuyển lượt xe ra. Vui lòng thử lại.`

==================================================
12. THIẾT KẾ KỸ THUẬT BẮT BUỘC
==================================================

Phải triển khai logic này thành flow rõ ràng, không nhét linh tinh vào UI.

Tối thiểu cần có:

- command:
  - `MoveToOutYardCommand`
- service / use case:
  - `CompleteWeighingSession`
  hoặc tên tương đương
- validator:
  - `CanMoveToOutYard(session)`

### Rule chốt cho validator
`CanMoveToOutYard(session)` trả về true khi:
- session != null
- session chưa completed
- session chưa cancelled
- session có Weight1
- session có Weight2
- session có NetWeight
- session đã phân bổ xong
- session đang ở `READY_TO_COMPLETE`

==================================================
13. REVIEW CODEBASE BẮT BUỘC
==================================================

Trước khi code, phải review và báo rõ:

1. Logic hiện tại session đang được chuyển sang `Danh sách xe ra` bằng cách nào
2. Auto move đang nằm ở file/service/use case nào
3. `SessionStatus` hiện đang định nghĩa ở đâu
4. `READY_TO_PRINT` hiện đang được dùng ở đâu
5. Màn `Danh sách xe ra` hiện query dữ liệu theo điều kiện gì
6. Sau khi session completed, hiện còn in được ở đâu, nếu chưa có thì phải bổ sung

==================================================
14. FILE / CODE PHẠM VI CẦN RÀ
==================================================

Tối thiểu phải rà và sửa/tạo:

- `WeighingView.xaml`
- `WeighingViewModel.cs`
- enum/constants cho `SessionStatus`
- command `MoveToOutYard`
- modal confirm
- use case/service hoàn tất session
- validator `CanMoveToOutYard`
- query của màn `Danh sách xe ra`
- action in từ màn `Danh sách xe ra` hoặc detail view nếu chưa có
- refresh/navigation logic giữa `WeighingView` và `OutgoingVehicleListView`

==================================================
15. THỨ TỰ TRIỂN KHAI
==================================================

### Bước 1 — Review hiện trạng
- tìm logic auto move dựa trên in
- xác định session status hiện có
- xác định query màn `Danh sách xe ra`
- xác định khả năng in từ màn xe ra hiện tại

### Bước 2 — Workflow design
- bỏ dependency in -> out yard
- thêm action thủ công `Chuyển xe ra`
- chốt điều kiện enable
- chốt update DB
- chốt post-completion UX

### Bước 3 — UI/UX design
- thêm nút
- style nút
- confirm modal
- toast
- trạng thái enable/disable

### Bước 4 — Implement
- file-by-file
- không pseudo-code
- không để bất kỳ dependency nào giữa printed flags và completion flow

### Bước 5 — Test
- session chưa ready -> nút disable
- session ready -> nút enable
- confirm thành công -> sang `Danh sách xe ra`
- không in phiếu vẫn chuyển ra được
- sau khi chuyển ra vẫn in được từ màn `Danh sách xe ra` hoặc detail
- `ADMIN` dùng được
- `OPERATOR` dùng được

==================================================
16. OUTPUT BẮT BUỘC
==================================================

Trả kết quả theo format:

## A. REVIEW HIỆN TRẠNG
- logic auto move hiện nằm ở đâu
- `SessionStatus` hiện dùng thế nào
- màn `Danh sách xe ra` query ra sao
- printing sau completion hiện xử lý thế nào

## B. WORKFLOW DESIGN
- action `Chuyển xe ra`
- điều kiện enable
- confirm flow
- DB update flow
- cách tách hẳn completion khỏi printing

## C. STATUS DESIGN
- bộ `SessionStatus` cuối cùng
- cách refactor từ `READY_TO_PRINT` sang `READY_TO_COMPLETE` nếu có

## D. UI/UX DESIGN
- vị trí nút
- style nút
- confirm modal
- toast behavior
- post-completion printing UX

## E. IMPLEMENTATION
- file tree
- code file-by-file
- validator
- command
- service/use case
- query update
- outgoing printing access

## F. TEST NOTES
- not ready
- ready
- confirm success
- confirm cancel
- no printing dependency
- print after out yard
- role behavior

==================================================
17. QUALITY GATE
==================================================

Không được coi là xong nếu:
- vẫn còn rule auto chuyển Out Yard khi in đủ phiếu
- nút `Chuyển xe ra` còn phụ thuộc `IsPrinted`
- chưa có confirm modal
- session chưa phân bổ xong mà vẫn enable nút
- session đã completed/cancelled mà vẫn enable nút
- chuyển Out Yard xong lại không in tiếp được
- `OPERATOR` không dùng được nút này

==================================================
18. MỤC TIÊU CUỐI CÙNG
==================================================

Tôi cần mô hình vận hành như sau:

- In chứng từ là nghiệp vụ riêng
- Kết thúc lượt xe là nghiệp vụ riêng
- Màn `Lập phiếu cân` có nút `Chuyển xe ra`
- Nút này chỉ cần điều kiện:
  - đã hoàn tất cân
  - đã phân bổ xong
- Không phụ thuộc việc đã in hay chưa
- Sau khi bấm xác nhận:
  - session sang `COMPLETED`
  - registration sang `OUT_YARD`
  - session xuất hiện ở `Danh sách xe ra`
- Và session vẫn còn khả năng in sau đó nếu cần
