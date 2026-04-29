# PHASE 3 – PILOT & HARDENING SPECIFICATION (FINAL LOCKED)

## 1. Mục tiêu Phase 3
Phase 3 là giai đoạn đưa hệ thống từ mức:
- đúng kiến trúc
- đúng schema
- đúng nghiệp vụ lõi
sang mức:
- chạy được ở trạm thật
- đủ ổn định để pilot nội bộ
- có thể support khi lỗi xảy ra
- có thể rollback nếu pilot gặp sự cố

Kết thúc Phase 3, hệ thống phải đạt 5 kết quả:
1. **Pilot-ready build**: Có bản build usable cho 1 trạm thật.
2. **Hardening luồng vận hành thật**: Bao gồm ERP insert trực tiếp, inbound processor, weigh flow, printing, sync, diagnostics.
3. **Hardening thiết bị cân**: Cho phép support/operator tự cấu hình parser substring trên giao diện để sửa lỗi đọc số cân sai.
4. **Hardening UI/UX**: WeightView và submenu Cấu hình hệ thống phải usable cho người dùng thật.
5. **Pilot governance**: Có checklist, SOP, rollback plan, acceptance report.

## 2. Kiến trúc kế thừa bắt buộc
Phase 3 không được thay đổi các quyết định đã khóa ở Phase 2 Re-architecture.

### 2.1 Aggregate root
- `vehicle_registrations` là aggregate root

### 2.2 ERP inbound
- ERP insert trực tiếp vào `vehicle_registrations`
- app xử lý qua `VehicleRegistrationInboundProcessor`

### 2.3 Child documents
- `weigh_tickets` và `delivery_tickets` đều là child docs của `vehicle_registrations`

### 2.4 Workflow status
- Business workflow chỉ nằm ở: `vehicle_registrations.registration_status`
- Bộ trạng thái giữ nguyên:
  - REGISTERED
  - LOADING_IN_PROGRESS
  - OVERWEIGHT_PENDING_ACTION
  - COMPLETED
  - CANCELLED

### 2.5 Master data
- Giữ: `vehicles`, `customers`, `products`

### 2.6 TTCP và đăng kiểm
- Chỉ nằm ở: `vehicles`
- Không lấy từ ERP payload.

## 3. Phạm vi Phase 3
### 3.1 In scope
**A. Inbound processor hardening**
- validate
- normalize
- logging
- audit
- duplicate protection
- cancel-at-insert
- processed flags
- polling configuration

**B. Master data hardening**
- master xe usable
- master khách hàng usable
- master sản phẩm usable
- suggest/autocomplete usable

**C. WeightView hardening**
- root binding đúng
- loading/disabled states đúng
- row đỏ quá tải
- warning đăng kiểm
- popup phiếu liên quan
- thao tác operator rõ ràng hơn

**D. Device hardening**
- COM thật
- reconnect
- timeout
- stale reading
- parser substring config
- preview parse trên UI

**E. Printing & sync hardening**
- in phiếu cân
- in phiếu giao nhận
- printed flag logic
- sync retry
- sync diagnostics

**F. Recovery & supportability**
- app restart
- pending inbound recovery
- pending sync recovery
- DB verification scripts
- diagnostics usable
- rollback plan

### 3.2 Out of scope
Phase 3 chưa bao gồm:
- rollout đa trạm diện rộng
- admin web đầy đủ
- BI/reporting nâng cao
- multi-trip engine phức tạp
- void/reissue workflow phức tạp
- mobile app

## 4. Luồng vận hành mục tiêu
ERP insert -> `vehicle_registrations`
-> `VehicleRegistrationInboundProcessor`
-> validate + normalize
-> upsert master data
-> `registration_status = REGISTERED`
-> operator mở `WeightView`
-> chọn registration
-> load TTCP + đăng kiểm từ `vehicles`
-> cân lần 1
-> sinh `weigh_ticket` + `delivery_ticket`
-> `registration_status = LOADING_IN_PROGRESS`
-> cân lần 2
-> `COMPLETED` hoặc `OVERWEIGHT_PENDING_ACTION`
-> in phiếu
-> sync

## 5. Schema và config của Phase 3
### 5.1 vehicle_registrations
Giữ schema Phase 2 Re-architecture, và nếu chưa có thì phải có:
- `is_inbound_processed`
- `inbound_processed_at`
- `inbound_error_code`
- `inbound_error_message`
- `last_inbound_attempt_at`
- `last_sync_attempt_at`
- `last_sync_error`

### 5.2 weigh_tickets
Có thể bổ sung nếu support cần:
- `last_printed_at`
- `last_print_error`

### 5.3 delivery_tickets
Có thể bổ sung nếu support cần:
- `last_printed_at`
- `last_print_error`

### 5.4 device_configs
Phase 3 giữ và mở rộng theo đúng quyết định mới:
*Cấu hình thiết bị cơ bản*:
- `com_port`
- `baudrate`
- `parity`
- `data_bits`
- `stop_bits`
- `frame_end_char`
- `parser_type`
- `stability_threshold`
- `stable_cycles`
- `read_timeout_seconds`
- `reconnect_interval_seconds`

*Cấu hình tách chuỗi số cân*:
- `weight_substring_start`
- `weight_substring_length`

### 5.5 app_config
Phải có tối thiểu:
- `station_code`
- `ticket_prefix`
- `delivery_prefix`
- `tolerance_kg`
- `sync_interval_seconds`
- `retry_base_seconds`
- `overweight_split_residual_ratio`
- `registration_inbound_poll_seconds`
- `pilot_mode_enabled`

## 6. Inbound processor hardening
### 6.1 Mục tiêu
`VehicleRegistrationInboundProcessor` phải đủ ổn định để chạy liên tục trên trạm.

### 6.2 Trách nhiệm
Processor phải:
- quét record mới `is_inbound_processed = 0`
- validate field tối thiểu
- normalize dữ liệu
- upsert master data
- set `registration_status`
- set `is_inbound_processed`
- set `inbound_processed_at`
- ghi `audit_logs`
- ghi `ILogger`

### 6.3 Validation tối thiểu
Record inbound hợp lệ nếu có:
- `vehicle_plate`
- `customer_code`
- `product_code`

### 6.4 Validation fail behavior
Nếu thiếu field tối thiểu:
- không mark processed
- không tạo child docs
- không upsert master data
- ghi `audit_logs`
- ghi `ILogger`
- lưu `inbound_error_code` và `inbound_error_message`

### 6.5 Cancel-at-insert behavior
Nếu:
- `is_cancelled = 1`
- và dữ liệu đủ hợp lệ
Thì:
- vẫn upsert master data
- set `registration_status = CANCELLED`
- set processed
- không tạo child docs

### 6.6 Polling
Polling interval mặc định: 5 giây. Nhưng phải cấu hình được qua: `registration_inbound_poll_seconds`

## 7. Master data hardening
### 7.1 vehicles
- *Business key*: (`vehicle_plate`, `mooc_number`)
- *Mục tiêu*: Master xe phải là nơi operator/admin hoàn thiện dần: tài xế, HTVC, TTCP, đăng kiểm xe, đăng kiểm mooc.
- *Các trường chính*: `vehicle_plate`, `mooc_number`, `driver_name`, `transport_method`, `ttcp_weight`, `vehicle_registration_no`, `vehicle_registration_expiry_date`, `mooc_registration_no`, `mooc_registration_expiry_date`, `is_active`

### 7.2 customers
Màn Master khách hàng phải hỗ trợ: search, add/edit, active/inactive.

### 7.3 products
Màn Master sản phẩm phải hỗ trợ: search, add/edit, active/inactive.

### 7.4 Suggest/autocomplete
- *Vehicle*: nhập `vehicle_plate` -> suggest; chọn biển số -> hiện danh sách mooc; chọn mooc -> fill TTCP, HTVC, tài xế, đăng kiểm xe/mooc.
- *Customer*: suggest theo code/tên.
- *Product*: suggest theo code/tên.

## 8. WeightView hardening
### 8.1 Root binding
- `WeightView` phải bind theo `vehicle_registration`

### 8.2 Form trái
Bind theo root + lookup vehicles:
- Số PTVC: `vehicle_registrations.vehicle_plate`
- Mooc: `vehicle_registrations.mooc_number`
- Tên tài xế: ưu tiên `vehicles.driver_name`, fallback `vehicle_registrations.receiver_name`
- TTCP: `vehicles.ttcp_weight`
- TTCP 10%: `vehicles.ttcp_weight * 1.10`
- Mã ĐKPT: `vehicle_registrations.erp_vehicle_registration_id`
- HTVC: ưu tiên `vehicles.transport_method`, fallback `vehicle_registrations.transport_method`
- Nhà phân phối: `vehicle_registrations.customer_name`
- Mã sản phẩm: `vehicle_registrations.product_code`
- SL đặt: `vehicle_registrations.planned_weight`
- Số bao: `vehicle_registrations.bag_count`
- Ghi chú: `vehicle_registrations.notes`
- Không lấy/nhập hàng: `vehicle_registrations.is_cancelled`
- ĐK xe: `vehicles.vehicle_registration_no`
- Hạn ĐK xe: `vehicles.vehicle_registration_expiry_date`
- ĐK mooc: `vehicles.mooc_registration_no`
- Hạn ĐK mooc: `vehicles.mooc_registration_expiry_date`

### 8.3 Cảnh báo đăng kiểm
Nếu hạn đăng kiểm xe < Today hoặc hạn đăng kiểm mooc < Today thì: field hạn hiển thị đỏ, warning hiển thị rõ.

### 8.4 Panel phải
- Bind theo `current_primary_weigh_ticket`

### 8.5 Grid chính
- Grid hiển thị: 1 dòng = 1 `vehicle_registration`
- *Row đỏ*: Nếu `has_overweight_case = true` thì row đỏ.

### 8.6 Popup related docs
Nút Xem phiếu liên quan phải hiển thị: toàn bộ `weigh_tickets` và toàn bộ `delivery_tickets` của registration đang chọn.

### 8.7 Disabled states
Phải khóa/mở nút đúng:
- chưa chọn registration -> không cân
- chưa cân lần 1 -> không cân lần 2
- registration canceled -> không thao tác cân tiếp
- inbound lỗi -> warning rõ

## 9. Device & parser hardening
Đây là phần cần bám rất sát yêu cầu mới của bạn.

### 9.1 Mục tiêu
Cho phép support/operator tự chỉnh cách lấy số cân từ raw frame ngay trên giao diện, bằng:
- vị trí bắt đầu
- độ dài chuỗi lấy
để không cần sửa code khi frame cân thay đổi.

### 9.2 Logic parse chuẩn
Khi nhận raw frame, hệ thống xử lý theo thứ tự:
- nhận raw frame
- trim/control-char cleanup cơ bản
- lấy chuỗi từ: `weight_substring_start` và `weight_substring_length`
- parse trực tiếp chuỗi đó thành số
- hiển thị preview

### 9.3 Không hỗ trợ ở Phase 3
Không làm trong Phase 3: prefix strip, suffix strip, regex pattern, divisor, decimal places config.

### 9.4 Màn “Thiết bị cân” – yêu cầu bắt buộc
*Nhóm cấu hình kết nối*: `com_port`, `baudrate`, `parity`, `data_bits`, `stop_bits`, `frame_end_char`.
*Nhóm cấu hình parser*: `parser_type`, `stability_threshold`, `stable_cycles`, `read_timeout_seconds`, `reconnect_interval_seconds`.
*Nhóm cấu hình tách chuỗi số cân*: vị trí bắt đầu (`weight_substring_start`), độ dài chuỗi lấy (`weight_substring_length`).

*Preview parse bắt buộc*:
- Raw frame: `ST,GS,+001234kg`
- Sau xử lý: `001234`
- Kết quả parse: `1234`

*Nút bắt buộc*: Test kết nối, Test đọc frame, Xem preview parse, Lưu cấu hình.

### 9.5 Validation parser config
Bắt buộc validate:
- `weight_substring_start >= 0`
- `weight_substring_length > 0`
- `start + length` không vượt quá độ dài frame mẫu khi preview/test.

### 9.6 Device diagnostics
Màn Diagnostics hoặc Thiết bị cân phải hiển thị: COM hiện tại, parser type, raw frame gần nhất, chuỗi sau xử lý gần nhất, kết quả parse gần nhất, stable/unstable, last valid reading, last parser error, reconnect attempts.

## 10. Printing hardening
### 10.1 Mục tiêu
In phiếu cân và phiếu giao nhận đủ dùng cho pilot.

### 10.2 Rule
- chỉ mark `is_printed = 1` khi in thành công
- nếu in lỗi: không mark printed, ghi log lỗi, hiển thị warning.

### 10.3 Cần test
- printer offline
- không có default printer
- in lặp
- in phiếu quá tải
- in phiếu liên quan

## 11. Sync hardening
### 11.1 Scope
Sync tối thiểu: `vehicle_registrations`, `weigh_tickets`, `delivery_tickets`.

### 11.2 Yêu cầu
- retry hợp lý
- không tạo trùng
- queue quan sát được
- lỗi nhìn thấy được trên diagnostics.

### 11.3 Diagnostics sync
Phải hiển thị: pending registrations/weigh tickets/delivery tickets, failed sync count, last success, last failure, error message gần nhất.

## 12. Recovery & resilience
### 12.1 App restart
Khi app restart: inbound processor resume, pending sync resume, registrations không mất, child docs không mất, primary refs vẫn đúng.

### 12.2 DB safety
Phải có: backup procedure, restore dry run, orphan check scripts, duplicate ERP ID check, null FK check cho child docs.

### 12.3 Processor safety
Nếu một record inbound lỗi: không crash toàn processor, record khác vẫn xử lý được.

## 13. Submenu “Cấu hình hệ thống” – Phase 3 final
Dưới menu Cấu hình hệ thống, phải có:

### 13.1 Tham số hệ thống
Quản lý: `station_code`, `ticket_prefix`, `delivery_prefix`, `tolerance_kg`, `sync_interval_seconds`, `retry_base_seconds`, `overweight_split_residual_ratio`, `registration_inbound_poll_seconds`, `pilot_mode_enabled`.

### 13.2 Thiết bị cân
Quản lý: COM / parser / stability, timeout / reconnect, `weight_substring_start`, `weight_substring_length`, preview parse.

### 13.3 Master xe
CRUD usable.

### 13.4 Master khách hàng
CRUD usable.

### 13.5 Master sản phẩm
CRUD usable.

### 13.6 Thông tin đồng bộ
Usable cho support: inbound pending, inbound failed, sync pending, sync failed, last success, last error.

## 14. Test plan chi tiết Phase 3
### 14.1 Inbound tests
- valid ERP insert
- invalid ERP insert
- canceled valid insert
- canceled invalid insert
- duplicate ERP ID
- processor restart
- batch inserts

### 14.2 Master tests
- same vehicle plate different mooc
- mooc dropdown
- TTCP update
- expired registration warning
- customer duplicate prevention
- product duplicate prevention

### 14.3 Device/parser tests
- connect success/fail
- parser success/fail
- substring parse đúng
- preview đúng
- reconnect
- timeout
- stale frame

### 14.4 Weight flow tests
- registration -> weigh 1 -> child docs
- weigh 2 -> complete
- overweight -> pending action
- cancel flow
- related docs popup

### 14.5 Printing tests
- print success/fail
- printed flag logic

### 14.6 Sync tests
- root sync
- child docs sync
- retry
- duplicate prevention

### 14.7 Recovery tests
- app restart with unprocessed inbound
- app restart with pending sync
- backup/restore dry run
- orphan check

## 15. Pilot execution plan
- *Stage A*: Technical dry run
- *Stage B*: Internal pilot
- *Stage C*: Stabilization

## 16. Deliverables Phase 3
- Pilot build
- Hardening patch set
- Updated schema/config
- Updated tests + smoke scripts
- Operator SOP
- Support SOP
- Pilot checklist
- Rollback plan
- Phase 3 acceptance report

## 17. Definition of Done Phase 3
Phase 3 được coi là done khi: ERP direct insert flow ổn, inbound processor ổn, master data flow ổn, WeightView usable, device thật ổn, cấu hình substring parse dùng được trên UI, preview parse hiển thị đúng, in phiếu usable, sync usable, recovery cơ bản pass, pilot nội bộ pass, SOP/checklist/rollback đủ.

## 18. Chỉ dẫn bắt buộc về tài liệu cũ
AI phải rà soát lại các tài liệu: Phase 0, Phase 1, Phase 2, Phase 2 Re-architecture và chỉ update nếu có chỗ mâu thuẫn với Phase 3. Không được sửa lại kiến trúc đã khóa (root, workflow status, ERP direct insert, master TTCP/đăng kiểm).

## 19. Prompt giao AI code/test cho Phase 3 (FINAL)
Đây là Phase 3 – Pilot & Hardening Specification (Final Locked) của StationApp. Hãy coi đây là source of truth cho Phase 3.
Bối cảnh kiến trúc đã khóa: `vehicle_registrations` là aggregate root, ERP insert trực tiếp vào `vehicle_registrations`, app xử lý hậu kỳ bằng `VehicleRegistrationInboundProcessor`, `weigh_tickets` và `delivery_tickets` là child docs, workflow status nằm ở `vehicle_registrations`, TTCP và đăng kiểm chỉ nằm ở `vehicles`.

*Nhiệm vụ Phase 3*:
1. Hardening `VehicleRegistrationInboundProcessor`
2. Hardening master data flow
3. Hardening `WeightView`
4. Hardening device integration
5. Nâng cấp màn Thiết bị cân để support cấu hình parse số cân trên UI: `weight_substring_start`, `weight_substring_length`, preview parse.
6. Hardening printing
7. Hardening sync
8. Hardening recovery
9. Tạo/cập nhật tests và smoke scripts cho Phase 3
10. Tạo/cập nhật pilot SOP, support SOP, rollback notes.
