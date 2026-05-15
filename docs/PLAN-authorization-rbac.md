# PLAN - Authorization RBAC (ADMIN & OPERATOR)

Bạn là Principal WPF Product Engineer + Workflow Architect + Authorization Architect của dự án StationApp.

Nhiệm vụ của bạn là triển khai hoàn chỉnh cơ chế phân quyền theo 2 role `ADMIN` và `OPERATOR` cho toàn bộ hệ thống hiện tại, bám đúng system design và nghiệp vụ đã chốt. Đây là source of truth cuối cùng, không hỏi lại các điểm đã chốt dưới đây.

==================================================
1. MỤC TIÊU
==================================================

Tôi cần hệ thống RBAC (role-based access control) với đúng 2 role:
- `ADMIN`
- `OPERATOR`

Mục tiêu:
1. Chặn đúng quyền theo màn hình, chức năng và hành động
2. UI hiển thị đúng theo role
3. Command/ViewModel chỉ enable đúng quyền
4. Service/UseCase chặn đúng quyền, không chỉ chặn ở UI
5. Đồng bộ hoàn toàn với luồng nghiệp vụ hiện tại của StationApp dùng `weighing_session`

==================================================
2. ROLE DUY NHẤT CỦA HỆ THỐNG
==================================================

Chỉ dùng 2 giá trị `RoleCode`:
- `ADMIN`
- `OPERATOR`

Không tạo role khác.

### Yêu cầu tại DB/UI quản lý tài khoản
- `RoleCode` không cho nhập text tự do
- phải là dropdown chỉ có:
  - `ADMIN`
  - `OPERATOR`

==================================================
3. ĐỊNH NGHĨA QUYỀN THEO ROLE
==================================================

## 3.1 ADMIN
Là tài khoản quản trị toàn quyền.

Được:
- dùng toàn bộ app
- vào tất cả menu
- quản lý tài khoản
- vào tham số hệ thống
- vào cấu hình hệ thống tổng quát
- vào cấu hình thiết bị / tham số kỹ thuật nếu có
- dùng tất cả màn vận hành
- cân tự động
- cân tay
- tạo inbound manual
- tạo weighing session
- phân bổ
- xử lý quá tải
- split quá tải
- không tách quá tải
- chuyển xe ra
- in / in lại
- master data
- cấu hình in trong flow in
- hủy session / hủy phiếu theo flow hiện tại

## 3.2 OPERATOR
Là tài khoản vận hành trạm cân.

Được:
- login / logout
- vào các màn vận hành:
  - Danh sách xe vào
  - Lập phiếu cân
  - Danh sách xe ra
- tạo inbound manual
- tạo weighing session
- cân tự động
- lưu dữ liệu cân
- phân bổ
- xử lý quá tải
- split quá tải
- không tách quá tải
- chuyển xe ra
- in phiếu cân
- in phiếu giao nhận
- in lại
- dùng master data
- dùng cấu hình in trong flow in
- hủy session / hủy phiếu theo flow hiện tại

Không được:
- cân tay
- vào quản lý tài khoản
- vào tham số hệ thống
- vào cấu hình hệ thống tổng quát
- sửa quyền người dùng
- chỉnh các tham số kỹ thuật nhạy cảm

==================================================
4. RULE CỐT LÕI PHẢI KHÓA CHẶT
==================================================

## 4.1 Manual weighing
- `ADMIN` được cân tay
- `OPERATOR` không được cân tay

Áp dụng ở cả:
- UI
- Command/ViewModel
- Service/UseCase

Nếu OPERATOR cố gọi logic cân tay theo bất kỳ đường nào thì phải bị từ chối.

## 4.2 Account management
Chỉ `ADMIN` được:
- tạo tài khoản
- sửa tài khoản
- đổi role
- reset mật khẩu
- khóa/mở tài khoản

`OPERATOR` không được nhìn thấy menu quản lý tài khoản.

## 4.3 System parameters / global config
Chỉ `ADMIN` được vào:
- Tham số hệ thống
- Cấu hình hệ thống tổng quát
- cấu hình kỹ thuật nhạy cảm

`OPERATOR` không được thấy các menu này.

## 4.4 Operational actions
Cả `ADMIN` và `OPERATOR` đều được:
- tạo session
- tạo inbound manual
- cân tự động
- phân bổ
- xử lý quá tải
- chuyển xe ra
- in / in lại
- hủy theo flow vận hành hiện tại

==================================================
5. PHÂN QUYỀN THEO MÀN HÌNH
==================================================

## 5.1 Cả ADMIN và OPERATOR đều được vào
- Danh sách xe vào
- Lập phiếu cân
- Danh sách xe ra
- Master data
- flow in / cấu hình in trong flow in
- modal xử lý quá tải
- xem phiếu liên quan
- xem chi tiết session / detail view nếu có

## 5.2 Chỉ ADMIN được vào
- Quản lý tài khoản
- Tham số hệ thống
- Cấu hình hệ thống tổng quát
- cấu hình thiết bị / parser / tham số kỹ thuật nếu có
- các màn admin-only khác

==================================================
6. PHÂN QUYỀN THEO CHỨC NĂNG / NÚT
==================================================

## 6.1 Màn Danh sách xe vào
### ADMIN
- tìm kiếm
- autocomplete
- tạo inbound manual
- chọn nhiều dòng
- tạo weighing session
- xác nhận vào cân
- mở chi tiết nếu có

### OPERATOR
- tìm kiếm
- autocomplete
- tạo inbound manual
- chọn nhiều dòng
- tạo weighing session
- xác nhận vào cân
- mở chi tiết nếu có

## 6.2 Màn Lập phiếu cân
### ADMIN
- cân lần 1
- cân lần 2
- lưu
- cân tự động
- cân tay
- phân bổ
- xử lý quá tải
- tách quá tải
- không tách
- để sau
- in PC
- in PGN
- in lại
- xem phiếu liên quan
- hủy session / hủy phiếu
- chuyển xe ra

### OPERATOR
- cân lần 1
- cân lần 2
- lưu
- cân tự động
- phân bổ
- xử lý quá tải
- tách quá tải
- không tách
- để sau
- in PC
- in PGN
- in lại
- xem phiếu liên quan
- hủy session / hủy phiếu
- chuyển xe ra

### OPERATOR không được
- cân tay
- dùng bất kỳ toggle/manual input/path nào đi vào manual weighing

## 6.3 Màn Danh sách xe ra
### Cả ADMIN và OPERATOR
- tìm kiếm / lọc
- xem chi tiết
- in phiếu cân
- in phiếu giao nhận
- in lại
- tra cứu chứng từ

## 6.4 Master data
### Cả ADMIN và OPERATOR
- được vào và dùng theo system design hiện tại

Nếu module master data có chức năng cực kỳ nhạy cảm ngoài phạm vi vận hành, giữ nguyên nhưng phải ghi rõ trong review. Mặc định phase này: OPERATOR được dùng master data.

## 6.5 Quản lý tài khoản
### Chỉ ADMIN
- CRUD tài khoản
- đổi role
- reset password
- khóa/mở tài khoản

==================================================
7. UI / MENU RULES
==================================================

## 7.1 Với ADMIN
- thấy toàn bộ menu
- thấy toàn bộ nút hợp lệ theo trạng thái nghiệp vụ

## 7.2 Với OPERATOR
- thấy các menu vận hành
- thấy master data
- thấy in / flow in
- KHÔNG thấy:
  - Quản lý tài khoản
  - Tham số hệ thống
  - Cấu hình hệ thống tổng quát
  - control/manual mode của cân tay

### Quy tắc hiển thị
- menu không có quyền: ưu tiên ẩn hoàn toàn
- action không có quyền: ưu tiên ẩn hoặc disable tùy ngữ cảnh
- riêng manual weighing: ưu tiên ẩn hoàn toàn với OPERATOR nếu UI cho phép

==================================================
8. CHẶN QUYỀN Ở TẦNG CODE
==================================================

Phân quyền không được chỉ làm ở UI.

Phải có 3 lớp chặn:

## Lớp 1: UI/menu
- ẩn hoặc disable đúng role

## Lớp 2: Command/ViewModel
- command chỉ enable khi role hợp lệ
- ví dụ `ManualWeighCommand` phải false với OPERATOR

## Lớp 3: Service/UseCase
- nếu role không hợp lệ mà gọi logic admin-only thì từ chối
- đặc biệt phải chặn:
  - manual weighing use case
  - account management use case
  - system parameter update use case
  - global config update use case

==================================================
9. LOGIN / SESSION / CURRENT USER
==================================================

Sau khi login thành công, session hiện tại phải giữ tối thiểu:
- `UserId`
- `Username`
- `DisplayName`
- `RoleCode`
- `IsAuthenticated`

Toàn bộ menu, nút, command, service guard phải bám vào `RoleCode` này.

Nếu codebase chưa có abstraction rõ cho current user/session thì phải chuẩn hóa, ví dụ:
- `ICurrentUserContext`
- `IUserSession`
hoặc abstraction tương đương đang phù hợp với codebase hiện có

==================================================
10. ACCOUNT MANAGEMENT RULES
==================================================

## 10.1 Role dropdown
Ở màn quản lý tài khoản:
- RoleCode là dropdown chỉ có `ADMIN`, `OPERATOR`

## 10.2 Safety rules
Bắt buộc:
- luôn còn ít nhất 1 tài khoản `ADMIN` active
- không cho disable/xóa admin cuối cùng
- OPERATOR không được tự nâng quyền
- OPERATOR không được sửa RoleCode của chính mình hoặc người khác

==================================================
11. REVIEW HIỆN TRẠNG BẮT BUỘC
==================================================

Trước khi code, phải review codebase và báo rõ:

1. Cơ chế login/session hiện tại là gì
2. Current user / role đang được lưu thế nào
3. Menu/sidebar đang render theo cách nào
4. Những màn nào hiện đã tồn tại
5. Các command/nút chính ở từng màn là gì
6. Logic manual weighing hiện nằm ở đâu
7. Logic account management hiện nằm ở đâu
8. Logic system parameters/config hiện nằm ở đâu
9. Đâu là điểm đã chặn bằng UI nhưng chưa chặn bằng service/use case
10. Đâu là điểm cần refactor để RBAC sạch hơn

==================================================
12. IMPLEMENTATION STRATEGY
==================================================

Làm theo đúng thứ tự:

### Bước 1 — Review hiện trạng
- audit login/session/current user
- audit menu/sidebar
- audit từng màn và từng command
- audit use cases nhạy cảm

### Bước 2 — Permission model design
- định nghĩa role matrix
- mapping role -> menu
- mapping role -> screen
- mapping role -> action/button
- mapping role -> service/use case

### Bước 3 — UI update
- menu visibility
- button visibility
- manual mode visibility
- account/system menus hidden for OPERATOR

### Bước 4 — Command/ViewModel update
- enable/disable command theo role
- manual commands chặn với OPERATOR
- admin-only commands chặn với OPERATOR

### Bước 5 — Service/UseCase guard
- thêm guard/check role ở các use case nhạy cảm
- bảo đảm không bypass được bằng cách gọi logic trực tiếp

### Bước 6 — Test
- test theo ADMIN
- test theo OPERATOR
- test menu
- test buttons
- test command enable
- test service guard
- test manual weighing blocked for OPERATOR
- test account management blocked for OPERATOR

==================================================
13. OUTPUT BẮT BUỘC
==================================================

Trả kết quả theo format:

## A. REVIEW HIỆN TRẠNG
- login/session hiện tại
- current user context hiện tại
- menu/screens hiện tại
- command chính hiện tại
- các điểm thiếu guard

## B. PERMISSION DESIGN
- role definitions
- menu permission matrix
- screen permission matrix
- action/button permission matrix
- service/use case permission matrix

## C. UI/UX DESIGN
- menu visibility by role
- button visibility by role
- manual mode visibility by role
- account/system menu hiding rules

## D. IMPLEMENTATION
- file tree các file tạo/sửa
- code file-by-file
- không pseudo-code
- guard logic rõ ràng

## E. TEST NOTES
- ADMIN flow
- OPERATOR flow
- manual weighing blocked for OPERATOR
- account management blocked for OPERATOR
- system parameter blocked for OPERATOR
- operational flow still works for OPERATOR

==================================================
14. QUALITY GATE
==================================================

Không được coi là xong nếu:
- RoleCode còn cho nhập tự do
- OPERATOR vẫn dùng được cân tay
- OPERATOR vẫn vào được quản lý tài khoản
- OPERATOR vẫn vào được tham số hệ thống / cấu hình tổng quát
- phân quyền chỉ chặn ở UI mà không chặn ở service/use case
- menu admin-only vẫn hiện với OPERATOR
- admin cuối cùng vẫn có thể bị disable/xóa

==================================================
15. MỤC TIÊU CUỐI CÙNG
==================================================

Tôi cần hệ thống chỉ có 2 role:

### ADMIN
- toàn quyền
- được cân tay
- được quản trị tài khoản
- được cấu hình hệ thống
- được dùng toàn bộ app

### OPERATOR
- dùng cho vận hành trạm cân
- được dùng các màn vận hành
- được xử lý toàn bộ flow vận hành thông thường
- được master data và in
- KHÔNG được cân tay
- KHÔNG được quản lý tài khoản
- KHÔNG được vào tham số hệ thống / cấu hình tổng quát
