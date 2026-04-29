# KẾ HOẠCH FIX TRIỆT ĐỂ BUG ĐƠ / GIẬT ỨNG DỤNG STATIONAPP (REVISED)

## 1. Mục tiêu

Loại bỏ triệt để các stall nhìn thấy rõ trên máy trạm client, đặc biệt:
* stall khoảng ~5 giây khi mở `WeightView`
* lag khi điều hướng các màn có grid/form
* nhiễu runtime do reconnect COM6 lỗi
* nhiễu runtime do background worker gọi sai Central API
* startup first-run quá chậm

Mục tiêu cuối:
1. `WeightView` không còn stall ~5 giây
2. lỗi COM6 không làm app giật thêm
3. worker master-data không còn spam lỗi `localhost:5000`
4. startup first-run được harden đúng cách
5. có before/after timing trên đúng máy client bị lag

---

## 2. Kết luận kỹ thuật làm nền tảng

### P0-A. WeightView load path đang bị stall theo kiểu deterministic
Evidence từ timing ~5.13s lặp lại cho thấy cần coi đây là **critical-path stall**, không chỉ là query chậm ngẫu nhiên.

### P0-B. COM6 reconnect failure đang là root-cause contributor
Evidence từ log:
* `Failed to open serial port COM6`
* `The semaphore timeout period has expired`
* reconnect attempts lặp lại

Kết luận:
* device reconnect path đang ảnh hưởng load path hoặc runtime responsiveness

### P0-C. Central API URL đang misconfigured
Evidence:
* worker đang gọi `http://localhost:5000/api/master-data`
* trên máy client điều này bị refused

Kết luận:
* background worker đang chạy lỗi theo chu kỳ
* tạo thêm overhead không cần thiết

### P1. Cold startup path còn nặng
Evidence:
* startup đầu tiên rất chậm
* nhưng lần sau giảm mạnh

Kết luận:
* cần harden
* nhưng không phải ưu tiên cao hơn WeightView stall và worker/device issues

---

## 3. Chiến lược sửa lỗi

Nguyên tắc lớn:
* **không để bất kỳ I/O nào (device, HTTP, DB) chặn critical UI path**
* **không swallow exception mù**
* **không fire-and-forget vô tổ chức**
* **mọi path lỗi phải degrade gracefully**
* **mọi fix phải đo before/after**

---

## 4. Workstream A — Tách device connect/reconnect khỏi critical UI path (P0)

### Mục tiêu
Không để việc mở `WeightView` hoặc mở app bị chờ bởi `COM6` connect/reconnect.

### Yêu cầu kỹ thuật
1. `SerialScaleDevice.ConnectAsync` không được nằm trên critical path của UI load
2. Lỗi mở COM không được làm:
   * stall màn hình
   * stall command UI
   * flood Dispatcher
3. Nhưng lỗi vẫn phải:
   * được log
   * được lưu state
   * hiển thị rõ trên Diagnostics/UI

### Cách triển khai đúng
* Không gọi `await _scaleDevice.ConnectAsync()` trực tiếp trong `WeighingViewModel.InitializeAsync()` nếu đây là path render màn
* Thay vào đó:
  * mở màn trước
  * attach device status bất đồng bộ sau
  * dùng state machine rõ

### Device state machine đề xuất
* `Disconnected`
* `Connecting`
* `Connected`
* `ReconnectWaiting`
* `Faulted`

### Reconnect policy bắt buộc
* exponential backoff
* cooldown
* max retry window
* cancel được khi user rời màn / app shutdown
* không queue reconnect chồng nhau

### Không được làm
* không nuốt exception hoàn toàn
* không fire-and-forget không quản lý
* không reconnect liên tục vô hạn với khoảng nghỉ ngắn

### File/module cần sửa
* `SerialScaleDevice.cs`
* device reconnect scheduler/policy
* `WeighingViewModel.cs`
* `DiagnosticsViewModel.cs`

### Acceptance
* COM fail vẫn hiển thị lỗi rõ
* nhưng mở `WeightView` không còn stall 5 giây
* reconnect fail không spam UI/log vô hạn

---

## 5. Workstream B — Bóc tách và sửa WeightView load path (P0)

### Mục tiêu
Xác định chính xác step nào đang ăn ~5 giây và loại bỏ nó khỏi path render màn.

### Việc phải làm
Bóc timing riêng cho từng bước:
* create ViewModel
* init search/filter state
* load registrations projection
* load grid data
* bind UI
* attach device service
* attach diagnostics state
* attach sync state nếu có

### Quy tắc
Phải phân biệt rõ:
* step nào cần để **màn hiển thị lần đầu**
* step nào có thể **load async sau khi màn đã lên**

### Fix hướng kiến trúc
* render screen shell trước
* grid/data quan trọng load async có loading indicator
* device/diagnostics attach sau
* không chờ background state không cần thiết

### Nghi ngờ bắt buộc phải kiểm tra
* timeout/wait 5 giây
* semaphore wait
* reconnect wait
* task delay
* any synchronous wait
* DB query lặp không cần thiết
* grid projection quá nặng

### File/module cần sửa
* `WeighingViewModel.cs`
* `WeighingView.xaml(.cs)` nếu cần loading state
* repositories / projection query cho grid
* command load/search/select

### Acceptance
* không còn cụm timing ~5.13s
* mở màn mượt hơn rõ
* grid lên nhanh hơn, không “khung trước dữ liệu sau” một cách rõ rệt

---

## 6. Workstream C — Fix Central API misconfiguration + graceful degradation (P0/P1)

### Mục tiêu
Không để worker background gọi sai `localhost:5000` rồi fail theo chu kỳ trên máy client.

### Vấn đề gốc
Hiện trạng không nên fix chỉ bằng sửa “một IP đúng” trong `appsettings.json`, mà phải sửa theo hướng:
* config validation
* degrade gracefully
* worker state rõ ràng

### Yêu cầu kỹ thuật
1. Nếu Central API URL trống / sai / là localhost không hợp lệ trên client:
   * worker không được spam retry vô hạn
   * worker không được spam log nặng
   * Diagnostics phải báo “chưa cấu hình” hoặc “endpoint không reachable”
2. Nếu URL hợp lệ:
   * worker chạy bình thường
3. Phải có circuit breaker hoặc cooldown:
   * ví dụ tạm dừng 10 phút sau N lần fail liên tiếp

### Không được làm
* không hardcode mơ hồ “dải IP hợp lệ”
* không ép localhost trên client nếu API không chạy local

### File/module cần sửa
* `appsettings.json` / config binding
* `CentralApiClient.cs`
* `InboundMasterDataWorker.cs`
* `DiagnosticsViewModel.cs`
* sync/diagnostics config validation path

### Acceptance
* không còn request lỗi lặp vô ích vào `localhost:5000`
* worker degrade đúng khi config sai
* UI diagnostics phản ánh đúng mà không gây lag thêm

---

## 7. Workstream D — Hardening startup / first-run path (P1)

### Mục tiêu
Giảm startup first-run, nhưng không phá tính đúng đắn schema.

### Quy tắc
Trước khi cho migration/background:
* phải xác định migration có bắt buộc để UI chạy không

### Nếu migration là bắt buộc
* không được cho UI nghiệp vụ chạy trước khi DB sẵn sàng
* nhưng có thể:
  * dùng splash/progress screen
  * không block MainWindow bằng UI freeze cứng

### Nếu migration không bắt buộc ngay
* mới được deferred/background

### Bắt buộc bóc timing
* config load
* DI build
* DB migrate
* OpenAsync
* first query
* shell ready

### File/module cần sửa
* `App.xaml.cs`
* startup bootstrap
* DB migration/init service

### Acceptance
* first-run tốt hơn
* không còn startup 15–20s theo kiểu “đứng im không biết app làm gì”
* schema correctness vẫn đảm bảo

---

## 8. Workstream E — Logging và performance instrumentation (P0 support)

### Mục tiêu
Đảm bảo mọi fix đều đo được.

### Bắt buộc giữ
* performance logs custom
* timing breakdown cho:
  * startup
  * WeightView load
  * device reconnect
  * central API worker
  * sync cycle

### Logging strategy
* `Microsoft/EF = Warning` cho pilot/prod
* custom performance logs giữ lại
* DiagnosticMode bật sâu khi cần

### Không được làm
* không tắt hết log rồi “cảm giác app mượt hơn”
* không để logging mới quá nặng

---

## 9. Workstream F — Validation / acceptance (P0)

### Automated
* build pass
* existing tests pass
* add tests nếu cần cho:
  * reconnect policy
  * worker cooldown/circuit breaker
  * overlap guard
  * load path decomposition helpers

### Manual on client machine
Bắt buộc test trên đúng máy client bị lag:
1. startup
2. idle 2 phút
3. mở WeightView
4. search/select
5. mở Diagnostics
6. mở Master xe
7. thử trong tình huống COM6 lỗi
8. thử trong tình huống Central API chưa cấu hình đúng

### Before/After timing table bắt buộc
Phải có cho:
* App Startup
* DB Connection Duration
* WeightView Load
* Diagnostics Open
* Master screen open
* device reconnect cycle
* central API worker cycle

---

## 10. File/module checklist thực thi
Tối thiểu phải rà các file này:
* `SerialScaleDevice.cs`
* `WeighingViewModel.cs`
* `DiagnosticsViewModel.cs`
* `CentralApiClient.cs`
* `InboundMasterDataWorker.cs`
* `App.xaml.cs`
* startup/bootstrap service
* performance logger / timing helper
* config binding / settings access
* repositories/projection query của WeightView

---

## 11. Điều kiện được phép kết luận FIXED
Chỉ được chốt `FIXED` nếu:
1. WeightView không còn stall ~5 giây
2. COM reconnect fail không làm app giật thêm
3. Central API misconfig không gây retry noise/runtime lag
4. startup hợp lý hơn
5. user test trên máy client xác nhận app mượt hơn rõ rệt
6. có before/after timing chứng minh

Nếu chưa đạt hết, phải chốt:
* `PARTIALLY FIXED`

---

## 12. Prompt giao AI/dev triển khai

```text id="s2yq2z"
Đây là revised plan để fix triệt để bug lag/đơ StationApp. Hãy coi đây là source of truth cho đợt sửa lỗi này.

Mục tiêu:
- loại bỏ WeightView stall ~5 giây
- cô lập lỗi COM6 reconnect khỏi UI path
- sửa Central API misconfiguration + graceful degradation
- harden startup first-run
- đo before/after trên máy client bị lag

Workstreams:

A. Device reconnect isolation (P0)
- không để ConnectAsync/reconnect nằm trên critical UI load path
- dùng state machine + backoff + cooldown + cancellation
- không swallow exception mù
- không fire-and-forget vô tổ chức
- Diagnostics vẫn phải thấy lỗi nhưng UI không được stall

B. WeightView load path fix (P0)
- breakdown từng step load màn
- tìm chính xác step nào ăn ~5 giây
- render shell trước, attach device/diagnostics sau nếu phù hợp
- loại bỏ mọi wait/timeout/semaphore stall khỏi critical path
- tối ưu query/path nếu có evidence

C. Central API graceful degradation (P0/P1)
- không để worker gọi localhost sai trên client
- validate config
- nếu misconfigured thì cooldown/circuit breaker
- Diagnostics báo đúng nhưng không spam

D. Startup hardening (P1)
- bóc tách startup timings
- chỉ deferred/background migration nếu không phá schema readiness
- không để UI freeze cứng

E. Performance instrumentation (P0 support)
- giữ custom performance logs
- log breakdown cho startup, WeightView, device reconnect, central API worker, sync cycle

F. Validation
- build/test pass
- before/after timings trên máy client bị lag
- final status chỉ được là FIXED nếu loại bỏ được WeightView 5s stall và reconnect/config noise

Yêu cầu:
- File-by-file
- Không pseudo-code
- Không phá kiến trúc Phase 2 re-architecture
- Báo cáo:
  1. files changed
  2. root cause breakdown updated
  3. policy changes (reconnect / worker degradation)
  4. before/after timing table
  5. final status = FIXED / PARTIALLY FIXED / NOT FIXED
```
