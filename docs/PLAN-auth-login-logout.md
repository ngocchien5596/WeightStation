# TRIỂN KHAI MÀN LOGIN VÀ NÚT LOGOUT CHO STATIONAPP

Bạn là Principal WPF Product Engineer + Authentication Flow Architect + System Analyst của dự án StationApp.

Nhiệm vụ của bạn là thiết kế và triển khai đầy đủ chức năng Login và Logout cho ứng dụng StationApp, theo đúng giao diện/system design hiện tại của app, và đảm bảo người dùng phải đăng nhập thì mới được sử dụng app.

## 1. MỤC TIÊU

Tôi cần hệ thống có đầy đủ:

- Màn Login
- Bắt buộc login trước khi vào app
- Nút Logout
- Session người dùng hiện tại
- Chặn toàn bộ màn chức năng nếu chưa đăng nhập
- Giao diện:
    - đồng bộ với system design của app
    - không dùng UI Windows mặc định xấu
    - không lạc phong cách với app hiện tại

## 2. PHẠM VI ĐỢT NÀY

Đợt này chỉ cần local authentication trong app, dùng tài khoản lưu trong DB nội bộ.

Bao gồm:
- nhập Username + Password để đăng nhập
- kiểm tra PasswordHash
- kiểm tra IsActive
- lưu session user hiện tại trong app
- update LastLoginAt khi đăng nhập thành công
- có nút Logout để quay về màn Login

Không làm ở đợt này:
- đăng nhập qua email
- SSO
- OTP / MFA
- remember me dài hạn
- refresh token / JWT
- đăng nhập qua server ngoài
- auto lock account do login sai
- forgot password qua email

## 3. BẢNG TÀI KHOẢN PHẢI BÁM

Hệ thống tài khoản phải dựa trên bảng account hiện có, với schema sau khi đã mở rộng tối thiểu:

Các cột sử dụng cho login/logout:
- Id
- Username
- DisplayName
- RoleCode
- IsActive
- PasswordHash
- LastLoginAt
- CreatedAt
- CreatedBy
- UpdatedAt
- UpdatedBy

Không cần dùng ở login flow hiện tại:
- không có MustChangePassword
- không có FailedLoginCount
- không có LockoutEndAt

## 4. NGUYÊN TẮC BẮT BUỘC KHI MỞ APP

### 4.1 App phải yêu cầu đăng nhập trước

Khi mở ứng dụng:
- không được vào thẳng MainWindow
- không được dùng được app nếu chưa login
- hệ thống phải mở Login window / Login shell trước

### 4.2 Chỉ khi login thành công mới được vào app chính

Nếu đăng nhập đúng:
- đóng/ẩn màn Login
- mở MainWindow
- nạp thông tin user hiện tại vào session

Nếu đăng nhập sai:
- vẫn ở màn Login
- hiện lỗi rõ ràng
- không mở app chính

## 5. LUỒNG ỨNG DỤNG KHI STARTUP

### 5.1 Luồng startup chuẩn
1. App khởi động
2. Khởi tạo DI/container/config/services như bình thường
3. Không mở ngay MainWindow
4. Mở LoginWindow
5. User nhập Username + Password
6. Nếu login thành công:
    - tạo session user hiện tại
    - cập nhật LastLoginAt
    - mở MainWindow
    - đóng LoginWindow
7. Nếu login thất bại:
    - giữ nguyên LoginWindow
    - hiện thông báo lỗi
8. Nếu user đóng màn Login mà chưa login:
    - app thoát luôn

## 6. MÀN LOGIN – YÊU CẦU UI/UX

Màn Login phải:
- đồng bộ với StationApp hiện tại
- không quá màu mè
- rõ ràng, nội bộ, chuyên nghiệp
- dùng đúng palette / style / typography của app

### 6.1 Thành phần UI bắt buộc

Màn Login tối thiểu gồm:
- Logo / tên app: Station App
- Username
- Password
- Nút Đăng nhập
- Thông báo lỗi / validation
- Có thể có version nhỏ ở góc nếu app đang có convention này

### 6.2 Style
- nền / panel / button cùng tone với app
- button chính màu xanh đồng bộ
- input rõ ràng, dễ nhìn
- không dùng giao diện mặc định xấu của Windows Forms/WPF cổ
- không dùng message box hệ thống để báo lỗi login nếu có thể tránh

### 6.3 Hành vi UX
- mở app là focus vào ô Username hoặc Password hợp lý
- nhấn Enter ở Password phải có thể submit login
- nút Đăng nhập chỉ enable khi dữ liệu hợp lệ tối thiểu
- khi đang login có thể hiện loading/busy state ngắn
- nếu login sai phải hiện lỗi tại chỗ, không crash, không đóng app

## 7. MAPPING DỮ LIỆU CHO LOGIN

### Input từ user
- Username
- Password

### Logic kiểm tra
- tìm tài khoản theo Username
- nếu không tồn tại: báo lỗi login thất bại
- nếu tồn tại nhưng IsActive = false: báo lỗi tài khoản ngừng hoạt động
- nếu PasswordHash null/rỗng: báo lỗi tài khoản chưa được cấu hình mật khẩu
- verify password người dùng nhập với PasswordHash
    - nếu đúng: login thành công
    - nếu sai: báo lỗi login thất bại

### Khi login thành công
- cập nhật LastLoginAt = clockService.NowLocal
- có thể cập nhật UpdatedAt, UpdatedBy nếu convention hệ thống yêu cầu
- tạo CurrentUserSession

## 8. THÔNG BÁO BẮT BUỘC Ở LOGIN

### 8.1 Validation tại client
- “Vui lòng nhập Username.”
- “Vui lòng nhập Password.”

### 8.2 Lỗi nghiệp vụ
- “Sai tài khoản hoặc mật khẩu.”
- “Tài khoản đã ngừng hoạt động.”
- “Tài khoản chưa được cấu hình mật khẩu.”
- “Không thể đăng nhập. Vui lòng thử lại.”

### 8.3 Không tiết lộ quá nhiều
Không nên phân biệt quá chi tiết để tránh lộ thông tin hệ thống, nhưng vì đây là app nội bộ, có thể chấp nhận mức rõ ràng vừa phải như trên.

## 9. SESSION NGƯỜI DÙNG HIỆN TẠI

Phải có một cơ chế lưu user hiện tại đang đăng nhập trong runtime của app.

### 9.1 Cần có service/session abstraction
Ví dụ: ICurrentUserService, IUserSession hoặc service tương đương.

### 9.2 Thông tin tối thiểu phải giữ trong session
- UserId
- Username
- DisplayName
- RoleCode
- IsAuthenticated = true/false

### 9.3 Mục đích
Session này sẽ được dùng cho:
- hiển thị tên người dùng ở UI
- CreatedBy, UpdatedBy
- phân quyền sau này nếu cần
- logout

## 10. NÚT LOGOUT – YÊU CẦU NGHIỆP VỤ

### 10.1 Mục tiêu
Người dùng đang ở trong app có thể bấm Logout để:
- thoát khỏi session hiện tại
- quay về màn Login
- app không còn usable cho tới khi login lại

### 10.2 Hành vi chuẩn của Logout
Khi user bấm Logout:
- hiện confirm modal
- nếu xác nhận:
    - clear current user session
    - đóng MainWindow hiện tại
    - quay về LoginWindow
- nếu hủy confirm: giữ nguyên app

### 10.3 Sau khi logout
- toàn bộ app chính không còn usable
- phải quay về màn Login
- user phải đăng nhập lại mới vào tiếp được

## 11. VỊ TRÍ NÚT LOGOUT TRÊN UI

Nút Logout phải được đặt ở vị trí phù hợp với system design hiện tại.

Khuyến nghị:
- sidebar dưới cùng, gần thông tin user
- hoặc góc header / menu user

Nếu app hiện đang có vùng hiển thị user ở góc dưới trái/sidebar thì: đặt luôn Logout ở đó để đồng bộ với hiện trạng.

## 12. MODAL XÁC NHẬN LOGOUT

Khi bấm Logout, phải hiện modal confirm đồng bộ với design system của app.

### Nội dung modal
- Tiêu đề: Xác nhận đăng xuất
- Nội dung: Bạn có chắc muốn đăng xuất không?
- Nút: Đăng xuất, Không

Không dùng MessageBox mặc định nếu app đã có modal system chuẩn.

## 13. QUY TẮC BẢO MẬT TỐI THIỂU

### 13.1 Không lưu password thô
- password người dùng nhập chỉ dùng để verify
- không lưu lại plain text
- không log plain text

### 13.2 Chỉ dùng PasswordHash
Bắt buộc verify bằng hash service chuẩn, ví dụ: BCrypt hoặc cơ chế tương đương.

### 13.3 Không hardcode tài khoản admin trong code
Không được: hardcode username/password trong code, tạo backdoor login.
Nếu cần seed admin đầu tiên, phải: seed vào DB bằng PasswordHash.

## 14. TÍCH HỢP VỚI QUẢN LÝ TÀI KHOẢN

Chức năng Login/Logout phải dùng chung dữ liệu với chức năng Quản lý tài khoản.

### Kết quả mong muốn
- tài khoản tạo trong màn Quản lý tài khoản có thể dùng để login
- reset password trong màn Quản lý tài khoản ảnh hưởng trực tiếp tới login
- deactivate tài khoản trong màn Quản lý tài khoản sẽ làm tài khoản đó không login được

## 15. RULE THỜI GIAN

Phải bám đúng rule đã chốt của hệ thống:
- thời gian lưu trong DB là giờ local của user
- dùng IClockService
- không dùng GETDATE()
- không gọi DateTime.Now/UtcNow rải rác trong business logic
Áp dụng cho: LastLoginAt

## 16. UI / STATE RULES CHO LOGIN

- **16.1 Khi app mở lên:** chỉ hiện LoginWindow, MainWindow chưa mở.
- **16.2 Khi login đang xử lý:** disable input tạm thời hoặc disable nút Đăng nhập để tránh spam login nhiều lần.
- **16.3 Khi login thất bại:** không đóng màn Login, không clear bừa Username, Password có thể được clear tùy UX decision, hiện lỗi rõ ràng.
- **16.4 Khi login thành công:** đóng/ẩn LoginWindow, mở MainWindow, hiển thị user hiện tại ở nơi phù hợp trên UI.

## 17. FILE / CODE PHẠM VI CẦN RÀ

Tối thiểu phải rà và sửa/tạo:
- startup flow / App.xaml.cs
- MainWindow bootstrap flow
- LoginWindow.xaml
- LoginViewModel.cs
- auth service / login service
- password hash verify service
- current user session service
- logout command
- menu/sidebar/header nơi đặt nút Logout
- modal confirm logout
- repository đọc tài khoản
- update LastLoginAt
- integration với chức năng Quản lý tài khoản

## 18. THỨ TỰ TRIỂN KHAI

- **Bước 1 — Review hiện trạng:** xác định startup app hiện tại đang mở gì, login flow hiện có hay chưa, bảng account/entity hiện tại, PasswordHash đã có chưa, vị trí đặt nút Logout.
- **Bước 2 — Schema design:** Thêm PasswordHash, LastLoginAt, CreatedBy, UpdatedBy, unique index cho Username (nếu chưa có).
- **Bước 3 — Auth design:** thiết kế login flow, session flow, logout flow, hashing/verify strategy.
- **Bước 4 — UI/UX design:** màn Login, vị trí Logout, modal confirm logout, loading / error states.
- **Bước 5 — Implement:** file-by-file, không pseudo-code, bám đúng system design hiện tại.
- **Bước 6 — Test:** mở app bắt login, login đúng/sai, tài khoản inactive, tài khoản chưa có mật khẩu, logout quay về login, login lại sau logout, update LastLoginAt.

## 19. OUTPUT BẮT BUỘC

Trả kết quả theo format:
- A. REVIEW HIỆN TRẠNG
- B. SCHEMA UPDATE DESIGN
- C. AUTH FLOW DESIGN
- D. UI/UX DESIGN
- E. IMPLEMENTATION
- F. TEST NOTES

## 20. QUALITY GATE

Không được coi là xong nếu:
- mở app vẫn vào được MainWindow khi chưa login
- không có Login screen
- không có Logout
- logout mà không quay lại Login
- login không dùng PasswordHash
- vẫn lưu/log password thô
- không update LastLoginAt
- giao diện Login/Logout lệch system design của app

## 21. MỤC TIÊU CUỐI CÙNG

Tôi cần StationApp có:
- màn Login
- nút Logout
- bắt buộc login mới dùng được app
- session user hiện tại rõ ràng
- giao diện đồng bộ với system design
- hoạt động thực tế được với dữ liệu tài khoản trong DB hiện tại

# 22 . Không cần Socratic Gate thêm cho 4 điểm này. Hãy tự audit codebase và bám đúng thứ đang có trong repo, không introduce framework mới nếu chưa thật sự cần.

Chốt nguyên tắc xử lý như sau:

1. Dependency Injection (DI)
- Không thêm DI framework mới.
- Hãy rà codebase để xác định app đang dùng gì và reuse đúng cái đó.
- Nếu repo đang dùng Microsoft.Extensions.DependencyInjection / HostBuilder / ServiceCollection thì tiếp tục dùng đúng stack đó.
- Nếu đang dùng container khác thì bám theo container hiện hữu.
- Mục tiêu: thêm các service mới như IAuthService, IUserSession, IClockService vào đúng DI hiện có, không đổi kiến trúc DI của dự án.

2. ORM / Data Access
- Không đổi ORM/data access stack.
- Hãy tự rà codebase để xác định đang dùng EF Core, Dapper hay ADO.NET thuần.
- Nếu repo đã có DbContext, migration, entity config, repository pattern kiểu EF Core thì bám EF Core.
- Nếu đang dùng Dapper/ADO.NET cho module tương ứng thì giữ nguyên.
- Mục tiêu: đọc bảng account và update LastLoginAt bằng đúng data access pattern hiện có trong dự án.

3. UI Design Resources
- Hãy tự rà App.xaml, ResourceDictionary, MergedDictionaries, Styles.xaml, Colors.xaml hoặc các file resource tương đương.
- Phải reuse các StaticResource/DynamicResource hiện có để màn Login và modal Logout đồng bộ với app.
- Nếu codebase chưa có bộ resource chuẩn đủ dùng thì mới tạo bổ sung một shared resource dictionary tối thiểu, nhưng vẫn phải bám palette/style đang có của StationApp, không thiết kế lệch tông.

4. Startup Logic
- Hãy tự rà App.xaml và App.xaml.cs để xác định hiện app đang startup bằng StartupUri hay OnStartup/bootstrap code.
- Nếu đang dùng StartupUri trỏ thẳng MainWindow thì phải đổi flow để chặn vào app chính khi chưa login.
- Nếu đang dùng OnStartup/AppHost thì xử lý login gate tại đó.
- Mục tiêu cuối cùng:
  - mở app => hiện LoginWindow trước
  - login thành công => mới mở MainWindow
  - đóng LoginWindow khi chưa login => app thoát
  - logout từ MainWindow => clear session và quay về LoginWindow

Nguyên tắc tổng quát:
- Không hỏi thêm các câu trên nữa.
- Hãy tự review codebase, ghi rõ phát hiện trong phần Review hiện trạng, rồi implement theo framework đang có.
- Không introduce thư viện mới chỉ để làm login/logout nếu repo đã có stack đủ dùng.
- Chỉ escalate lại nếu thật sự trong repo không tồn tại đủ thông tin để xác định.

Output của bạn phải bổ sung rõ:
A. DI stack hiện tại là gì
B. ORM/data access stack hiện tại là gì
C. Shared UI resources hiện tại là gì
D. Startup flow hiện tại là gì
E. Bạn đã tích hợp Login/Logout vào stack hiện hữu như thế nào