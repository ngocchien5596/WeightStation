Bạn là Senior .NET Engineer + Solution Architect của dự án StationApp.

Nhiệm vụ của bạn là **chuẩn hóa toàn bộ xử lý thời gian trong hệ thống** theo đúng rule nghiệp vụ sau:

==================================================
1. BUSINESS RULE BẮT BUỘC
==================================================

Từ bây giờ, toàn bộ hệ thống phải thống nhất theo nguyên tắc:

1. Tất cả thời gian **lưu trong DB** đều là **giờ local của người dùng**
2. Tất cả thời gian **hiển thị trên UI** cũng là **giờ local của người dùng**
3. Timezone chuẩn của hệ thống hiện tại là:
   - `Asia/Ho_Chi_Minh`
   - UTC+7
4. Không dùng UTC để lưu rồi convert lại cho nghiệp vụ hiện tại
5. Không dùng giờ của SQL Server làm nguồn giờ nghiệp vụ
6. Không dùng `GETDATE()` / `SYSDATETIME()` / `CURRENT_TIMESTAMP` của DB cho nghiệp vụ thao tác người dùng
7. Không gọi rải rác:
   - `DateTime.Now`
   - `DateTime.UtcNow`
   - `DateTimeOffset.Now`
   - `DateTimeOffset.UtcNow`
   trong code business nếu không đi qua service thống nhất

Mục tiêu là:
- user thao tác lúc nào thì DB lưu đúng giờ local của user lúc đó
- UI hiển thị lại đúng giờ local đó, không cần convert timezone trên UI

==================================================
2. PHẠM VI ÁP DỤNG
==================================================

Áp dụng cho toàn bộ timestamp nghiệp vụ của hệ thống, tối thiểu gồm:

### vehicle_registrations
- `CreatedAt`
- `UpdatedAt`
- `InboundProcessedAt`
- `LastInboundAttemptAt`
- `LastSyncAttemptAt`

### weigh_tickets
- `CreatedAt`
- `UpdatedAt`
- `Weight1Time`
- `Weight1UpdatedAt`
- `Weight2Time`
- `Weight2UpdatedAt`
- `LastPrintedAt`

### delivery_tickets
- `CreatedAt`
- `UpdatedAt`
- `LastPrintedAt`

### các bảng / log / entity khác nếu có field datetime
- rà toàn bộ solution để tìm tất cả các field thời gian và chuẩn hóa cùng nguyên tắc nếu chúng là timestamp nghiệp vụ

Lưu ý:
- nếu là field kỹ thuật/log nội bộ không hiển thị cho user nhưng vẫn phục vụ vận hành tại trạm, vẫn nên theo rule local-time để nhất quán
- nếu có field thật sự cần UTC cho mục đích hạ tầng/system-level thì phải nêu rõ trong report và không được tự ý giữ lại mà không giải thích

==================================================
3. CÁCH HIỂU ĐÚNG YÊU CẦU
==================================================

### Yêu cầu đúng
- user bấm thao tác lúc 14:35 tại máy trạm
- DB lưu 14:35
- UI hiển thị 14:35

### Không được làm
- DB lưu UTC rồi UI convert ngược lại
- DB lưu theo giờ SQL Server nếu SQL Server ở máy khác
- chỗ này dùng `Now`, chỗ kia dùng `UtcNow`, chỗ khác dùng `GETDATE()` lung tung

==================================================
4. GIẢI PHÁP KỸ THUẬT BẮT BUỘC
==================================================

Bạn phải triển khai theo mô hình **time provider thống nhất**.

## 4.1 Tạo abstraction chung
Tạo service chuẩn, ví dụ:
- `IClockService`
hoặc
- `ITimeProvider`
hoặc tên tương đương

Service này phải có tối thiểu:
- `NowLocal`
- hoặc `GetCurrentLocalTime()`

Khuyến nghị thêm:
- `TodayLocal`
- `Format` helper nếu cần, nhưng không bắt buộc

## 4.2 Rule của service
Service này phải trả về:
- thời gian local theo timezone hệ thống `Asia/Ho_Chi_Minh`

Không được để mỗi chỗ tự xử lý timezone riêng.

## 4.3 Toàn bộ code business phải dùng service này
Mọi chỗ gán thời gian cho entity phải đi qua service thống nhất.

Ví dụ các thao tác:
- tạo registration
- update registration
- cân lần 1
- cân lần 2
- in phiếu
- sync
- inbound processed
- cancel
- split overweight
- create/update master nếu có timestamp

==================================================
5. RÀ SOÁT TOÀN BỘ CODE HIỆN TẠI
==================================================

Bạn phải thực hiện code audit toàn solution để tìm các chỗ đang dùng sai.

## 5.1 Bắt buộc tìm kiếm toàn bộ project các pattern sau
- `DateTime.Now`
- `DateTime.UtcNow`
- `DateTimeOffset.Now`
- `DateTimeOffset.UtcNow`
- `GETDATE(`
- `SYSDATETIME(`
- `CURRENT_TIMESTAMP`
- `DateTime.Today`
- mọi helper thời gian tự phát nếu có

## 5.2 Phân loại từng chỗ tìm được
Với mỗi chỗ, phải phân loại:
1. business timestamp
2. hạ tầng/log nội bộ
3. test code
4. migration / SQL script
5. viewmodel/UI formatting

## 5.3 Cách xử lý
- business timestamp -> bắt buộc thay bằng `IClockService`
- DB default time bằng SQL -> bỏ dùng nếu là nghiệp vụ
- UI formatting -> chỉ format hiển thị, không convert timezone lung tung
- test code -> update mock/fake clock nếu cần

==================================================
6. DB / ENTITY / ORM RULES
==================================================

## 6.1 Kiểu dữ liệu
Nếu đang dùng `datetime2` thì tiếp tục dùng `datetime2` là được.

## 6.2 Không để DB tự sinh giờ nghiệp vụ
Nếu entity đang dựa vào:
- default constraint `GETDATE()`
- trigger gán giờ
- computed time
thì phải rà lại.

### Rule:
- timestamp nghiệp vụ phải do app set trước khi save
- không để DB server tự gán giờ nghiệp vụ

## 6.3 EF Core / Repository / Use Case
Phải rà:
- entity creation
- repository save/update
- use case handlers
- workers/background services
- print handlers
- sync handlers
- inbound processor
- overweight split logic
- cancel logic
- any audit update points

==================================================
7. UI RULES
==================================================

## 7.1 Hiển thị thời gian
UI chỉ được:
- lấy dữ liệu thời gian từ DB/entity
- format hiển thị cho dễ đọc

## 7.2 Không được convert timezone ở UI
Không được:
- giả định DB đang là UTC
- convert sang local ở XAML/ViewModel
- dùng logic timezone conversion lung tung trên màn hình

## 7.3 Áp dụng cho các màn có thời gian
Ví dụ:
- WeightView
- Danh sách phiếu
- Phiếu liên quan
- Diagnostics nếu có hiển thị timestamp
- grid có cột ngày giờ
- popup in/giao nhận nếu có

==================================================
8. TIMEZONE POLICY
==================================================

## 8.1 Chốt policy
Toàn hệ thống hiện tại dùng:
- `Asia/Ho_Chi_Minh`

## 8.2 Nếu cần config hóa
Nếu bạn thấy hợp lý, có thể tạo:
- `SystemTimeZoneId = "Asia/Ho_Chi_Minh"`
ở config hoặc constant tập trung

Nhưng không được làm phức tạp quá mức nếu chưa cần multi-timezone.

## 8.3 Nguồn thời gian
Ưu tiên:
- app-side time provider thống nhất

Không dùng:
- giờ SQL Server remote làm nguồn giờ nghiệp vụ

==================================================
9. CÁCH ĐIỂM CẦN CẨN THẬN
==================================================

## 9.1 Máy user sai giờ hệ thống
Do yêu cầu nghiệp vụ là dùng giờ local của user, nên cần chấp nhận:
- nếu máy user sai giờ, dữ liệu cũng sai giờ

Vì vậy bạn phải:
- nêu rõ trong report phần rủi ro
- khuyến nghị SOP: máy trạm phải sync giờ Windows/NTP chuẩn

## 9.2 Không làm phát sinh bug mới
- không để một số chỗ local time, một số chỗ UTC
- không để time provider dùng ở create mà update vẫn dùng DateTime.Now
- không để migration/seed/test chạy lỗi vì thay time source

## 9.3 Overweight split / weigh timestamps
Đặc biệt phải rà kỹ:
- `Weight1Time`
- `Weight2Time`
- `CreatedAt`
- `UpdatedAt`
trong logic tách phiếu quá tải, tạo phiếu mới, update phiếu cũ

==================================================
10. IMPLEMENTATION BẮT BUỘC
==================================================

Bạn phải làm theo thứ tự:

### Bước 1 — Audit
- tìm tất cả chỗ đang dùng thời gian trong solution
- lập danh sách đầy đủ

### Bước 2 — Design
- đề xuất `IClockService`
- xác định nơi đăng ký DI
- xác định timezone policy
- xác định field nào bị ảnh hưởng

### Bước 3 — Refactor
- thay toàn bộ business timestamp sang time provider
- bỏ phụ thuộc DB time defaults nếu đang dùng cho nghiệp vụ
- sửa ViewModel/UI nếu đang convert timezone sai

### Bước 4 — Test
- build
- unit test/integration test nếu có
- thêm/sửa test cho time provider nếu cần
- xác minh create/update/save/inbound/print/split/cancel dùng đúng local time

==================================================
11. OUTPUT BẮT BUỘC
==================================================

Trả kết quả theo đúng format:

## A. AUDIT REPORT
- liệt kê tất cả nơi đang dùng thời gian trong code
- phân loại từng chỗ:
  - business timestamp
  - DB default time
  - UI format
  - test
  - hạ tầng/log

## B. DESIGN
- interface/service nào được tạo
- timezone policy là gì
- source of truth cho thời gian là gì

## C. IMPACTED FIELDS
- danh sách field ở các bảng/entity bị ảnh hưởng
- field nào hiện đang lấy giờ sai
- field nào đã được chuẩn hóa

## D. IMPLEMENTATION
- file tree các file tạo/sửa
- code file-by-file
- DI registration
- entity/repository/use case changes
- UI/ViewModel formatting changes nếu có

## E. DB RULE REVIEW
- chỗ nào đang dùng `GETDATE()` / default constraint / DB-generated time
- đã xử lý ra sao
- chỗ nào vẫn giữ lại và vì sao

## F. TEST NOTES
- case nào đã test
- timestamp nào đã xác minh
- create/update/weight1/weight2/print/inbound/sync/cancel/split đã dùng đúng local time chưa

## G. RISKS / SOP NOTE
- lưu ý về việc máy trạm phải có giờ hệ thống đúng
- đề xuất sync giờ máy nếu cần

==================================================
12. QUALITY GATE
==================================================

Không được coi là xong nếu:
- còn chỗ business code dùng `DateTime.Now` / `DateTime.UtcNow` trực tiếp
- còn DB default time sinh giờ nghiệp vụ
- UI vẫn convert timezone lung tung
- chưa rà toàn bộ flow weigh1/weigh2/print/sync/inbound/split
- chưa có report audit đầy đủ

==================================================
13. MỤC TIÊU CUỐI CÙNG
==================================================

Sau khi bạn làm xong:
- user thao tác lúc nào thì DB lưu đúng giờ local của user lúc đó
- UI hiển thị lại đúng giờ local đó
- toàn hệ thống dùng một chuẩn thời gian duy nhất, không lẫn UTC / SQL time / local time
- dev về sau chỉ cần đọc rule là làm đúng ngay
