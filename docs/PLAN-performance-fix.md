# KẾ HOẠCH TỐI ƯU HIỆU NĂNG & KHẮC PHỤC LỖI ĐƠ / GIẬT ỨNG DỤNG

## Phiên bản cập nhật – sẵn sàng triển khai

## 1. Mục tiêu

Khắc phục tình trạng ứng dụng StationApp bị đơ/giật 2–3 giây trên **máy trạm client** khi kết nối tới SQL Server remote, đồng thời giữ nguyên tính đúng đắn của nghiệp vụ cân.

Mục tiêu cuối cùng của đợt này là:

* giảm hoặc loại bỏ hiện tượng giật UI theo chu kỳ
* đảm bảo thao tác cân lần 1 / cân lần 2 không bị sai số do tối ưu UI
* giảm I/O log không cần thiết trên máy trạm
* giữ lại đủ performance logs để tiếp tục điều tra nếu lỗi chưa hết hoàn toàn
* có số liệu before/after rõ ràng trên máy trạm bị lag

---

## 2. Phạm vi xử lý

Đợt tối ưu này tập trung vào 4 nhóm nguyên nhân có xác suất cao nhất:

### 2.1 Nguyên nhân chính đã xác nhận mạnh

**WPF Dispatcher Thread Starvation**
Nguồn dữ liệu cân từ cổng COM đẩy về liên tục với tần suất cao, làm UI queue bị ngập nếu mỗi frame đều yêu cầu cập nhật giao diện.

### 2.2 Nguyên nhân góp phần cần harden thêm

**Verbose Logging gây I/O đĩa và tăng tải runtime**
Toàn bộ log framework/EF Core ở mức quá chi tiết làm tăng ghi file không cần thiết trên máy trạm.

### 2.3 Nguyên nhân cần tiếp tục đo và theo dõi

**Remote DB access cost**
Dù ping và port test đang tốt, app vẫn dùng SQL Server remote. Việc mở kết nối, gọi query, refresh grid và polling nền vẫn có thể tạo độ trễ đáng kể trên máy client.

### 2.4 Nguyên nhân cần kiểm soát

**Background polling / refresh contention**
Các background workers hoặc auto-refresh cycles có thể đụng nhau, cạnh tranh DB/CPU/Dispatcher, làm người dùng cảm giác app khựng theo nhịp.

---

## 3. Kết quả mong muốn sau khi triển khai

Sau khi hoàn thành đợt fix này, hệ thống phải đạt:

* UI hiển thị số cân mượt và ổn định hơn
* không còn đơ định kỳ rõ rệt khi app đang idle hoặc chỉ hiển thị cân
* capture cân lần 1 / lần 2 lấy đúng dữ liệu thô mới nhất
* giảm đáng kể log framework nặng
* vẫn giữ được custom performance logs để chẩn đoán
* có báo cáo timing before/after trên máy trạm thực tế

---

## 4. Thiết kế giải pháp chi tiết

## 4.1 UI Throttling cho dữ liệu cân

### Mục tiêu

Chỉ giảm tần suất **vẽ UI**, không làm giảm chất lượng dữ liệu gốc phục vụ nghiệp vụ.

### Cơ chế chuẩn

Không cập nhật trực tiếp UI mỗi khi nhận một frame cân mới.
Thay vào đó:

* Mỗi frame cân mới sẽ cập nhật ngay vào một **snapshot nội bộ mới nhất**
* UI chỉ được cập nhật tối đa theo chu kỳ throttle cấu hình

### Cấu trúc snapshot nội bộ

Không dùng 2 biến rời kiểu `_latestRawWeight`, `_latestRawIsStable`.
Phải dùng một snapshot thống nhất, ví dụ:

* `Weight`
* `IsStable`
* `ReceivedAt`

Lý do:

* tránh trường hợp capture đọc weight của frame A nhưng isStable của frame B
* đảm bảo dữ liệu dùng cho nghiệp vụ là nhất quán tại một thời điểm

### Quy tắc vận hành

* Toàn bộ frame thô từ thiết bị vẫn được nhận đầy đủ
* Snapshot nội bộ luôn được cập nhật ngay
* UI chỉ render theo nhịp throttle, mặc định 200ms
* Lệnh `CaptureWeight1Async` và `CaptureWeight2Async` phải lấy trực tiếp từ **snapshot mới nhất hợp lệ**, không phụ thuộc tốc độ render UI

### Cấu hình

Thêm/đọc config:

* `device_ui_throttle_ms`

Giá trị mặc định:

* `200`

Ý nghĩa:

* 200ms ≈ 5Hz, đủ mượt mắt cho operator nhưng không gây flood Dispatcher

### Quy định bổ sung rất quan trọng

Không chỉ đổi `Dispatcher.Invoke()` thành `Dispatcher.BeginInvoke()` là đủ.
Phải triển khai thêm cơ chế **coalescing / anti-flood**:

* tại một thời điểm chỉ cho phép tối đa 1 UI update pending
* nếu frame mới về trong khi UI update trước chưa chạy xong, chỉ cập nhật snapshot nội bộ
* không queue vô hạn các `BeginInvoke` mới

Đây là điểm bắt buộc để tránh app vẫn giật dù đã dùng `BeginInvoke`.

---

## 4.2 Logging Strategy cho Production/Pilot

### Mục tiêu

Giảm I/O log nặng, nhưng không làm mất khả năng chẩn đoán hiệu năng.

### Quy tắc log framework

Đối với production/pilot:

* `MinimumLevel.Override("Microsoft", Warning)`

Điều này có nghĩa:

* ẩn SQL verbose logs của framework/EF Core
* giảm noise và giảm ghi file không cần thiết

### Quy tắc log custom

Phải giữ lại **performance logs do app tự viết**, tối thiểu cho các sự kiện:

* App Startup
* DB Connection Duration
* WeightView Load
* Search / Grid Load
* Search Autocomplete
* Inbound Processor Cycle
* Sync Cycle
* Main UI Navigation nếu có

### Định dạng log

Ưu tiên:

* JSONL hoặc structured text log
* dễ filter theo thời gian và operation

### Yêu cầu thêm

Custom performance logs phải được ghi theo kiểu:

* buffered
* asynchronous
* rolling file nếu hệ thống đang support

Không được thay verbose EF logs bằng một loại custom logging nặng tương tự.

### Diagnostic Mode

Phải có cờ:

* `DiagnosticMode = true/false`

Khi `DiagnosticMode = true`:

* được phép bật lại logging sâu hơn của framework/EF nếu cần điều tra

Khi `DiagnosticMode = false`:

* chỉ giữ mức log đủ dùng cho pilot/production

---

## 4.3 Instrumentation cho DB và UI

### Mục tiêu

Không chỉ sửa cảm tính. Phải đo được cụ thể chỗ nào đang gây lag.

### Phải đo timing ở các tầng sau

#### Startup

* app khởi động
* shell/main window ready

#### DB

* open connection
* create DbContext
* execute query ở repository chính
* save/update duration

#### UI paths

* mở WeightView
* search grid
* chọn dòng grid
* autocomplete
* mở Diagnostics
* mở Master xe
* mở Master khách hàng
* mở Master sản phẩm

#### Background jobs

* inbound processor cycle
* sync cycle
* dashboard refresh cycle nếu có
* diagnostics refresh cycle nếu có

### Dữ liệu log tối thiểu

Mỗi log timing phải có:

* Timestamp
* MachineName
* ThreadId
* OperationName
* DurationMs
* Success/Fail
* Exception nếu có

Nếu lấy được, nên thêm:

* CorrelationId cho action UI

---

## 4.4 Điều tra và harden background jobs

### Mục tiêu

Ngăn background jobs góp phần làm UI khựng theo chu kỳ.

### Các job phải rà soát

* `VehicleRegistrationInboundProcessor`
* `SyncOutboxWorker`
* mọi timer/refresh trong `DiagnosticsView`
* mọi timer/refresh trong `DashboardView`

### Với mỗi job phải xác định

* polling interval
* average duration
* max duration
* có overlap hay không
* có query DB gì
* có update UI trực tiếp hay không

### Yêu cầu triển khai

* không để cycle chồng lên nhau
* nếu cycle trước chưa xong thì cycle sau không được chạy song song vô tội vạ
* nếu có refresh UI từ background thì phải marshal đúng cách và hạn chế tần suất

---

## 4.5 Xử lý SQL / DB side

### Mục tiêu

Nếu lag còn đến từ query chậm hoặc blocking, phải có bằng chứng.

### Việc cần làm

* đo query duration của các repository chính
* tìm query có max duration cao bất thường
* rà index của các bảng:

  * `vehicle_registrations`
  * `vehicles`
  * `customers`
  * `products`
* kiểm tra có N+1 queries không
* kiểm tra có full scan không
* kiểm tra có blocking hoặc wait type bất thường không

### Quan điểm xử lý

Đợt này chưa cần đại tu SQL toàn hệ thống nếu chưa có bằng chứng.
Nhưng nếu instrumentation chỉ ra query nào là hotspot thì phải:

* tối ưu query
* hoặc thêm index hợp lý
* hoặc giảm số lần gọi query

---

## 4.6 Device/UI separation

### Mục tiêu

Tách hẳn dữ liệu gốc từ thiết bị và dữ liệu hiển thị UI.

### Quy tắc

* dữ liệu thiết bị = raw source of truth tạm thời cho thao tác capture
* UI = projection đã throttle
* business capture = lấy raw snapshot mới nhất hợp lệ

### Không được làm

* không dùng giá trị hiển thị đã throttle để lưu phiếu cân
* không để UI timer ảnh hưởng số cân nghiệp vụ

---

## 4.7 DiagnosticsView hardening

### Mục tiêu

Diagnostics phải hữu ích nhưng không gây thêm lag.

### Yêu cầu

* nếu hiển thị raw COM data thì cũng phải throttle
* nếu parser là stateless thì có thể singleton
* nếu parser có state thì không được ép singleton

### Quy định

Trước khi chuyển parser sang singleton, phải xác minh rõ:

* parser hoàn toàn stateless
* không giữ state giữa các frame

Nếu không chứng minh được thì:

* dùng transient/factory thay vì singleton

---

## 4.8 File I/O / deployment

### Đánh giá hiện tại

Kiểu bung file chạy trực tiếp **không được coi là nguyên nhân chính** nếu chưa có bằng chứng.

### Tuy nhiên phải kiểm tra

* logger có ghi file sync quá dày không
* folder log có bị scan nặng không
* startup có load DLL/plugin bất thường không

### Hướng xử lý

* giữ deployment hiện tại nếu không có bằng chứng nó là thủ phạm
* ưu tiên fix UI/DB/polling trước

---

## 5. Danh mục file cần can thiệp

### Bắt buộc sửa

* `App.xaml.cs`
* `WeighingViewModel.cs`
* `DiagnosticsViewModel.cs`

### Bắt buộc rà và có thể sửa

* DbContext factory / DB setup
* repository layer có query chính
* `VehicleRegistrationInboundProcessor.cs`
* `SyncOutboxWorker.cs`
* logging configuration module
* helper timing/interceptor nếu tạo mới

### Có thể thêm mới

* `PerformanceLogger.cs`
* `OperationTimingScope.cs`
* `UiThrottleHelper.cs`
* `LatestScaleReadingSnapshot.cs`

---

## 6. Cách triển khai kỹ thuật

## 6.1 `App.xaml.cs`

### Nhiệm vụ

* đọc `DiagnosticMode`
* cấu hình log level theo mode
* benchmark startup tổng quát

### Không nên làm quá nhiều ở đây

Không nhồi toàn bộ logic đo DB vào `App.xaml.cs`.
Startup benchmark có thể để đây, nhưng DB timings chi tiết phải đặt ở DB layer / repository / factory.

---

## 6.2 `WeighingViewModel.cs`

### Nhiệm vụ

* triển khai snapshot raw mới nhất
* triển khai UI throttling
* anti-flood Dispatcher
* capture lấy raw snapshot mới nhất
* log timing cho:

  * load WeightView
  * search/select nếu có
  * update cycle chính nếu cần

### Quy tắc bắt buộc

* không flood `Dispatcher.BeginInvoke`
* không capture từ giá trị UI display

---

## 6.3 `DiagnosticsViewModel.cs`

### Nhiệm vụ

* throttle hiển thị raw COM info
* tránh update UI quá dày
* kiểm tra parser lifetime phù hợp
* log timing cho diagnostics refresh

---

## 6.4 DB layer / repositories

### Nhiệm vụ

* đo connection open duration
* đo query duration
* đánh dấu hotspot
* tránh query sync trên UI path nếu còn

---

## 6.5 Background workers

### Nhiệm vụ

* log cycle duration
* ngăn overlap
* giảm contention với UI/DB nếu cần

---

## 7. Kế hoạch kiểm thử chấp nhận (UAT + validation)

## 7.1 Bài test bắt buộc

### Test 1 — Idle lag test

* mở app
* không thao tác 2 phút
* xác nhận không còn khựng theo chu kỳ rõ rệt

### Test 2 — WeightView interaction test

* mở WeightView
* search
* chọn dòng grid
* quan sát độ mượt

### Test 3 — Master screen test

* mở Master xe
* gõ tìm kiếm
* chọn dòng
* mở Diagnostics
* xác nhận không lag bất thường

### Test 4 — Capture correctness test

* kiểm tra rằng sau khi throttle UI, cân lần 1 / lần 2 vẫn lấy đúng trọng lượng mới nhất
* không bị lệch trọng lượng do render chậm

### Test 5 — Before/After benchmark

Phải có bảng trước/sau cho các action:

* app startup
* WeightView load
* search
* diagnostics open
* idle background cycles

### Test 6 — Client machine validation

Bắt buộc đo trên:

* **máy trạm bị lag**
  không chỉ trên máy DB server

---

## 7.2 Tiêu chí pass

Đợt fix được coi là pass khi:

* app không còn giật rõ rệt theo chu kỳ trên máy client
* thao tác UI chính mượt hơn trước
* capture trọng lượng vẫn chính xác
* log giảm ồn rõ rệt
* có số liệu before/after xác nhận cải thiện

---

## 8. Deliverables của đợt này

Sau khi triển khai xong, team phải bàn giao:

* build đã fix
* custom performance logs hoạt động
* config `device_ui_throttle_ms`
* config `DiagnosticMode`
* báo cáo timing before/after
* danh sách hotspot còn lại nếu có
* kết luận trạng thái:

  * FIXED
  * PARTIALLY FIXED
  * hoặc NEEDS MORE WORK

---

## 9. Rủi ro cần lưu ý

* nếu chỉ đổi `Invoke -> BeginInvoke` mà không coalesce queue, lag có thể vẫn còn
* nếu chỉ giảm log mà query/DB path vẫn sync, lag vẫn còn
* nếu snapshot raw không nhất quán, capture có thể sai
* nếu parser singleton mà có state, có thể sinh bug khó đoán

---

## 10. Kết luận triển khai

Đợt fix này **không chỉ là đổi vài dòng code**, mà là một gói tối ưu gồm:

* UI throttling đúng cách
* anti-flood Dispatcher
* logging strategy đúng cho production/pilot
* instrumentation đủ để đo
* kiểm soát background cycles
* xác minh trên đúng máy client bị lag
