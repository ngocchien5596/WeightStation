# TRIỂN KHAI CHỨC NĂNG QUẢN LÝ TÀI KHOẢN THUỘC NHÓM CẤU HÌNH CHO STATIONAPP

Bạn là Principal WPF Product Engineer + System Analyst + .NET CRUD Architect của dự án StationApp.

Nhiệm vụ của bạn là thiết kế và triển khai chức năng Quản lý tài khoản thuộc nhóm Cấu hình của hệ thống, theo đúng giao diện/system design hiện tại của app, và chỉ mở rộng schema ở mức đủ dùng, không làm thừa.

## 1. MỤC TIÊU

Tôi cần một chức năng Quản lý tài khoản để quản trị tài khoản người dùng nội bộ trong hệ thống.

**Yêu cầu:**
- Chức năng này phải nằm trong menu Cấu hình
- Giao diện phải dùng đúng style/system design hiện tại của app
- Phải hỗ trợ:
  - xem danh sách tài khoản
  - tìm kiếm / lọc
  - tạo mới tài khoản
  - sửa tài khoản
  - ngừng hoạt động / kích hoạt lại tài khoản
  - reset mật khẩu
- Không xóa cứng tài khoản
- Làm theo đúng thứ tự:
  1. Review hiện trạng
  2. Schema design
  3. System design
  4. UI/UX design
  5. Implementation
  6. Test

## 2. SCHEMA HIỆN TẠI

Hiện tại trong DB đã có sẵn các trường:
- `Id` -> `uniqueidentifier` -> NOT NULL
- `Username` -> `nvarchar(100)` -> NOT NULL
- `DisplayName` -> `nvarchar(150)` -> NOT NULL
- `RoleCode` -> `nvarchar(30)` -> NOT NULL
- `IsActive` -> `bit` -> NOT NULL
- `CreatedAt` -> `datetime2(7)` -> NOT NULL
- `UpdatedAt` -> `datetime2(7)` -> NULL

## 3. SCHEMA UPDATE BẮT BUỘC

Để chức năng Quản lý tài khoản usable thật, cần bổ sung đúng các cột tối thiểu sau vào bảng tài khoản hiện tại:

### 3.1 Các cột mới cần thêm
- `PasswordHash` `nvarchar(255)` NULL
  - dùng để lưu mật khẩu đã băm
  - không lưu password dạng plain text
- `LastLoginAt` `datetime2(7)` NULL
  - dùng để lưu lần đăng nhập gần nhất
- `CreatedBy` `nvarchar(100)` NULL
  - người tạo tài khoản
- `UpdatedBy` `nvarchar(100)` NULL
  - người cập nhật tài khoản gần nhất

### 3.2 DB rules bắt buộc
- tạo unique index cho `Username`
- không thêm cột `Password` dạng plain text
- **không thêm:**
  - `MustChangePassword`
  - `PasswordChangedAt`
  - `FailedLoginCount`
  - `LockoutEndAt`

### 3.3 Không thêm IsDeleted

Ở đợt này không thêm `IsDeleted`, vì:
- đã có `IsActive`
- nghiệp vụ ngừng sử dụng tài khoản chỉ cần `IsActive = false`

## 4. PHẠM VI NGHIỆP VỤ CỦA ĐỢT NÀY

Chức năng Quản lý tài khoản chỉ quản lý:

**A. Thông tin tài khoản**
- Username
- DisplayName
- RoleCode
- IsActive

**B. Mật khẩu**
- tạo mới tài khoản với mật khẩu khởi tạo
- reset mật khẩu

**C. Audit**
- CreatedAt
- CreatedBy
- UpdatedAt
- UpdatedBy
- LastLoginAt

**D. Những gì không làm ở đợt này**

Không làm:
- bắt buộc đổi mật khẩu lần đầu
- lịch sử đổi mật khẩu
- đếm đăng nhập sai
- khóa tài khoản tạm thời
- MFA / OTP
- tự phục hồi mật khẩu qua email
- xóa cứng tài khoản

## 5. NGUYÊN TẮC NGHIỆP VỤ

### 5.1 Tài khoản không bị hard delete
Ở đợt này:
- không xóa cứng record khỏi DB
- “xóa” trong UI nếu có thì phải hiểu là:
  - ngừng hoạt động
  - tức là `IsActive = false`

### 5.2 Username là định danh ổn định
Quy tắc bắt buộc:
- Username chỉ được nhập khi tạo mới
- khi sửa tài khoản thì Username là read-only

### 5.3 Mật khẩu không được lưu thô
Bắt buộc:
- chỉ lưu `PasswordHash`
- phải dùng hash chuẩn
- không được lưu Password plain text ở DB, config hay log

## 6. VỊ TRÍ CHỨC NĂNG TRONG ỨNG DỤNG

Chức năng này phải nằm dưới menu:
- Cấu hình -> Quản lý tài khoản

Không được làm thành màn lạc phong cách hoặc entry point riêng ngoài system menu của app.

## 7. HÀNH VI NGHIỆP VỤ CHI TIẾT

### 7.1 Xem danh sách tài khoản
Phải có màn danh sách tài khoản với:
- khu vực tìm kiếm / lọc
- form chi tiết
- grid danh sách
- nút hành động

Grid hiển thị tối thiểu: Username, DisplayName, RoleCode, Trạng thái hoạt động, LastLoginAt, CreatedAt, UpdatedAt.

### 7.2 Tạo mới tài khoản
Cho phép tạo mới với các field:
- Username, DisplayName, RoleCode, Password, ConfirmPassword, IsActive

**Khi tạo mới, hệ thống phải:**
- sinh Id mới bằng GUID
- `CreatedAt` = `clockService.NowLocal`
- `CreatedBy` = user hiện tại
- `UpdatedAt` = null hoặc = CreatedAt tùy convention của hệ thống, nhưng phải thống nhất
- `UpdatedBy` = null hoặc = CreatedBy tùy convention, nhưng phải thống nhất
- `LastLoginAt` = null
- `PasswordHash` = kết quả hash từ mật khẩu user nhập

**Validation tạo mới:**
- Username bắt buộc, Username không trùng
- DisplayName bắt buộc
- RoleCode bắt buộc
- Password bắt buộc, ConfirmPassword bắt buộc
- Password = ConfirmPassword

### 7.3 Sửa tài khoản
Cho phép sửa:
- DisplayName
- RoleCode
- IsActive

**Khi sửa, hệ thống phải:**
- không cho sửa Username
- update: `UpdatedAt` = `clockService.NowLocal`, `UpdatedBy` = user hiện tại

### 7.4 Ngừng hoạt động tài khoản
Phải có action riêng: Ngừng hoạt động
Khi user xác nhận:
- set `IsActive = false`
- update: `UpdatedAt`, `UpdatedBy`
- Không delete record.

### 7.5 Kích hoạt lại tài khoản
Phải có action riêng: Kích hoạt lại
Khi user xác nhận:
- set `IsActive = true`
- update: `UpdatedAt`, `UpdatedBy`

### 7.6 Reset mật khẩu
Phải có action riêng: Reset mật khẩu
**Luồng reset mật khẩu:**
1. User chọn 1 tài khoản trên grid
2. Bấm Reset mật khẩu
3. Mở modal reset password
4. User nhập: NewPassword, ConfirmPassword
5. Bấm xác nhận
6. Hệ thống: hash mật khẩu mới, update `PasswordHash`, update `UpdatedAt`, update `UpdatedBy`.

*Lưu ý: Ở đợt này không có MustChangePassword, không có PasswordChangedAt, reset xong chỉ cần update PasswordHash và audit fields.*

## 8. VALIDATION BẮT BUỘC

### 8.1 Username
- bắt buộc nhập
- trim khoảng trắng đầu/cuối
- không trùng
  - khi create: kiểm tra unique
  - khi edit: read-only

### 8.2 DisplayName
- bắt buộc nhập
- trim khoảng trắng đầu/cuối

### 8.3 RoleCode
- bắt buộc nhập
- Về RoleCode: Phải review xem app hiện có danh mục role cố định không.
  - nếu đã có role master/config -> dùng dropdown
  - nếu chưa có -> cho nhập text có validate cơ bản, nhưng phải ghi rõ trong report đây là điểm nên chuẩn hóa tiếp nếu hiện tại chưa có role master.

### 8.4 Password
Chỉ hiển thị trong: tạo mới tài khoản, reset mật khẩu.
Validation tối thiểu:
- bắt buộc nhập
- ConfirmPassword bắt buộc
- Password = ConfirmPassword
- nên có policy tối thiểu, ví dụ: tối thiểu 8 ký tự. Nếu app đã có policy mật khẩu chung thì bám theo policy đó.

## 9. RULE THỜI GIAN
Phải bám đúng rule hệ thống đã chốt:
- toàn bộ thời gian lưu trong DB là giờ local của user
- dùng `IClockService`
- không dùng `GETDATE()`
- không gọi `DateTime.Now` / `UtcNow` rải rác trong code business
- Áp dụng cho: `CreatedAt`, `UpdatedAt`, `LastLoginAt`

## 10. UI / UX DESIGN
Phải dùng đúng phong cách giao diện hiện tại của app:
- desktop nội bộ
- form trên, grid dưới
- button style đồng bộ
- màu sắc cùng system design
- không lòe loẹt
- không kiểu web consumer

## 11. BỐ CỤC MÀN HÌNH

### 11.1 Khu vực tìm kiếm / lọc
Tối thiểu gồm: Username, DisplayName, RoleCode, Trạng thái hoạt động

### 11.2 Khu vực form chi tiết
Tối thiểu gồm:
- Username, DisplayName, RoleCode, IsActive
- CreatedAt (read-only), CreatedBy (read-only), UpdatedAt (read-only), UpdatedBy (read-only), LastLoginAt (read-only)

### 11.3 Khu vực nút hành động
Tối thiểu gồm: Tìm kiếm, Làm mới, Tạo mới, Lưu, Reset mật khẩu, Ngừng hoạt động, Kích hoạt lại

### 11.4 Grid dữ liệu
Hiển thị danh sách tài khoản

## 12. GRID / FILTER / SEARCH

### 12.1 Grid columns
Tối thiểu: Username, Tên hiển thị, Vai trò, Trạng thái, LastLoginAt, Ngày tạo, Ngày cập nhật

### 12.2 Trạng thái hiển thị tiếng Việt
- IsActive = true -> Đang hoạt động
- IsActive = false -> Ngừng hoạt động

### 12.3 Search/filter
Phải hỗ trợ:
- nhấn Enter để tìm hoặc bấm nút Tìm kiếm
- có thể thêm debounce nếu phù hợp, nhưng không bắt buộc

### 12.4 Làm mới
- clear filter
- reload grid
- reset form nếu phù hợp

## 13. BUTTON STATE / SCREEN STATE

### 13.1 Khi chưa chọn bản ghi
- form có thể ở mode tạo mới hoặc rỗng
- các nút: Reset mật khẩu = disable, Ngừng hoạt động = disable, Kích hoạt lại = disable

### 13.2 Khi chọn tài khoản đang active
- Ngừng hoạt động = enable
- Kích hoạt lại = disable
- Reset mật khẩu = enable

### 13.3 Khi chọn tài khoản inactive
- Ngừng hoạt động = disable
- Kích hoạt lại = enable
- Reset mật khẩu = enable

### 13.4 Khi bấm Tạo mới
- clear form
- chuyển sang create mode
- Username editable
- form password hiện ra cho create flow

### 13.5 Khi chọn bản ghi để sửa
- Username read-only
- password field không hiện trực tiếp trên form edit
- chỉ đổi mật khẩu qua modal reset

## 14. MODAL / TOAST BẮT BUỘC
Phải dùng hệ modal/toast đồng bộ với app.

### 14.1 Modal confirm
Bắt buộc cho: Ngừng hoạt động, Kích hoạt lại

### 14.2 Modal reset password
Phải có modal riêng với: NewPassword, ConfirmPassword

### 14.3 Toast bắt buộc
- “Đã tạo tài khoản thành công.”
- “Đã cập nhật tài khoản thành công.”
- “Username đã tồn tại.”
- “Đã ngừng hoạt động tài khoản.”
- “Đã kích hoạt lại tài khoản.”
- “Đã reset mật khẩu thành công.”
- “Không thể lưu tài khoản. Vui lòng thử lại.”

## 15. THIẾT KẾ KỸ THUẬT BẮT BUỘC
Phải thiết kế đầy đủ:
- screen/view, viewmodel
- repository/service/use case
- validation logic
- password hashing strategy
- mapping DB -> UI -> DB

**Password hashing:**
Bắt buộc dùng hash chuẩn, ví dụ: BCrypt hoặc cơ chế hash chuẩn tương đương. Không được lưu password thô.

## 16. PHẠM VI FILE / CODE CẦN RÀ
Tối thiểu phải rà và sửa/tạo:
- menu/sidebar config
- AccountManagementView.xaml, AccountManagementViewModel.cs
- account entity/model
- migration thêm các cột mới
- repository/service/use case
- create/update/deactivate/reactivate/reset commands
- validation helpers
- password hashing service
- modal/toast integration

## 17. THỨ TỰ TRIỂN KHAI

- **Bước 1 — Review hiện trạng:** xác định bảng/entity hiện tại, xác định menu Cấu hình, xác định role source, xác định có auth flow hiện tại hay chưa.
- **Bước 2 — Schema design:** migration thêm: PasswordHash, LastLoginAt, CreatedBy, UpdatedBy, unique index cho Username.
- **Bước 3 — System design:** luồng create/edit/deactivate/reactivate/reset, screen states, validation, password policy tối thiểu.
- **Bước 4 — UI/UX design:** layout màn hình, grid columns, form fields, button states, modal/toast.
- **Bước 5 — Implement:** file-by-file, không pseudo-code, bám đúng schema cập nhật.
- **Bước 6 — Test:** create account, edit account, deactivate/reactivate, reset password, unique username, search/filter, created/updated timestamps theo local time.

## 18. OUTPUT BẮT BUỘC
Trả kết quả theo format:

**A. REVIEW HIỆN TRẠNG**
- bảng/entity hiện tại, schema hiện có, giới hạn hiện tại

**B. SCHEMA UPDATE DESIGN**
- danh sách cột mới, kiểu dữ liệu, nullability, unique index, lý do thêm từng cột

**C. SYSTEM DESIGN**
- menu placement, flow create/edit/deactivate/reactivate/reset, screen states, validation rules, password policy

**D. UI/UX DESIGN**
- layout, form fields, grid columns, button states, modal/toast behavior

**E. IMPLEMENTATION**
- file tree, migration, entity/config update, code file-by-file, hashing service, modal/toast integration

**F. TEST NOTES**
- create, edit, deactivate, reactivate, reset password, unique username, filter/search

## 19. QUALITY GATE
Không được coi là xong nếu:
- vẫn lưu password thô
- chưa có PasswordHash
- chưa có unique username
- chưa có deactivate/reactivate
- chưa có reset password
- username vẫn sửa bừa khi edit
- hard delete tài khoản
- UI không bám system design của app

## 20. MỤC TIÊU CUỐI CÙNG
Tôi cần một chức năng Quản lý tài khoản nằm trong nhóm Cấu hình, CRUD được tài khoản theo đúng nhu cầu tối giản hiện tại:
- có quản lý Username / DisplayName / RoleCode / IsActive
- có PasswordHash
- có reset mật khẩu
- có LastLoginAt
- có CreatedBy, UpdatedBy
- không có các field thừa như: MustChangePassword, PasswordChangedAt, FailedLoginCount, LockoutEndAt
- giao diện đồng bộ với system design của app
- usable thật trong vận hành nội bộ

