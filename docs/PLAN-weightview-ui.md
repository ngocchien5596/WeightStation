# KẾ HOẠCH: HOÀN THIỆN CHỨC NĂNG VÀ TRẠNG THÁI UI CHO MÀN LẬP PHIẾU CÂN / WEIGHTVIEW

Bạn là Principal WPF Product Engineer + Business Analyst của dự án StationApp.

Nhiệm vụ của bạn là **hoàn thiện toàn bộ chức năng và trạng thái UI cho màn “Lập phiếu cân / WeightView”**, bám sát:
- Phase 2 Re-architecture đã chốt
- Phase 3 Pilot & Hardening đã chốt
- UI hiện tại của màn hình
- schema và nghiệp vụ hiện có của dự án
- các quy tắc vận hành thực tế của trạm cân

Rất quan trọng:
- Không được tự ý thêm field ngoài schema hiện có
- Không được phá kiến trúc đã khóa:
  - `vehicle_registrations` là aggregate root
  - `weigh_tickets` và `delivery_tickets` là child docs
  - workflow status nằm ở `vehicle_registrations.registration_status`
- Không được để thao tác trên nút làm mất dòng đang chọn trong grid
- Không được tạo UX khó dùng cho operator

==================================================
1. MỤC TIÊU
==================================================

Hoàn thiện các chức năng và trạng thái UI cho màn WeightView, bao gồm:
- chọn bản ghi trong grid
- tìm kiếm header
- hiển thị detail theo bản ghi chọn
- enable/disable hợp lý cho các nút:
  - Giao nhận
  - In phiếu
  - Lưu
  - Cân lần 1
  - Cân lần 2
  - Hủy
  - Phiếu liên quan
- xử lý khác nhau giữa:
  - chế độ cân tự động
  - chế độ cân tay
- đảm bảo khi chưa chọn bản ghi thì detail phải clear
- đảm bảo khi thao tác xong vẫn giữ selected record / focus logic đúng
- hoàn thiện giao diện grid theo đúng yêu cầu hiển thị:
  - có kẻ ô hàng/cột rõ ràng
  - text bold
  - màu text theo nghiệp vụ:
    - xuất hàng = xanh lá
    - nhập hàng = đen
    - quá tải = đỏ

==================================================
2. BỐI CẢNH NGHIỆP VỤ BẮT BUỘC PHẢI BÁM
==================================================

Aggregate root:
- `vehicle_registrations`

Trạng thái root:
- `REGISTERED`
- `LOADING_IN_PROGRESS`
- `OVERWEIGHT_PENDING_ACTION`
- `COMPLETED`
- `CANCELLED`

Quy tắc nghiệp vụ đã chốt:
- ERP insert trực tiếp vào `vehicle_registrations`
- Sau cân lần 1 mới phát sinh:
  - `weigh_ticket`
  - `delivery_ticket`
- WeightView bind theo `vehicle_registration`
- Panel phải bind theo `current_primary_weigh_ticket`
- Grid là 1 dòng = 1 registration
- Khi có case quá tải, row hiển thị đỏ
- Nút “Phiếu liên quan” mở các child docs của registration
- TTCP và đăng kiểm lấy từ `vehicles`, không phải từ ERP

==================================================
3. QUY TẮC QUAN TRỌNG NHẤT VỀ SELECTION / FOCUS
==================================================

Đây là yêu cầu bắt buộc:

Khi người dùng chọn 1 bản ghi ở grid để thao tác, sau khi nhấn các nút:
- Giao nhận
- In phiếu
- Lưu
- Cân lần 1
- Cân lần 2
- Hủy
- Phiếu liên quan

thì:
1. Không được làm mất selection của dòng đang chọn
2. Không được clear detail nếu bản ghi đó vẫn còn tồn tại trong danh sách
3. Sau khi refresh/reload grid, phải cố gắng re-select đúng bản ghi vừa thao tác theo `vehicle_registration.id`
4. Nếu dòng vẫn còn trong grid sau thao tác, phải:
   - giữ selected row
   - scroll into view nếu cần
   - giữ focus hợp lý trên grid/detail, không nhảy vô tội vạ sang control khác
5. Chỉ được mất selection nếu:
   - bản ghi không còn nằm trong filtered result
   - hoặc đã bị loại khỏi danh sách theo business rules mới

Yêu cầu triển khai:
- dùng cơ chế preserve selection by key (`vehicle_registration.id`)
- sau mọi command có reload, restore selection bằng id
- không được tạo UX kiểu bấm nút xong grid nhảy lên đầu và mất dòng đang thao tác

==================================================
4. QUY TẮC HIỂN THỊ DETAIL KHI CHỌN / KHÔNG CHỌN BẢN GHI
==================================================

Khi chưa chọn bản ghi nào:
- tất cả các ô detail phải clear
- không hiển thị dữ liệu cũ
- các nút thao tác phải disable hợp lý

Khi chọn bản ghi:
- detail bind đúng theo registration + current primary ticket
- trạng thái nút và quyền edit phải tính lại ngay

==================================================
5. CÁC TRƯỜNG LUÔN READ-ONLY
==================================================

Các vùng sau **không cho phép edit trực tiếp**:
- khu vực hiển thị số cân live
- Số phiếu
- Trọng lượng hàng

Các ô này chỉ hiển thị:
- live device reading
- ticket number
- net weight tính toán / dữ liệu từ ticket

Không được cho người dùng sửa tay trực tiếp các ô này.

==================================================
6. QUY TẮC AUTO MODE vs MANUAL MODE
==================================================

Phải xử lý rõ 2 chế độ:

### A. Chế độ cân tự động
- các ô input số cân không được edit
- số cân lần 1/lần 2 lấy từ thiết bị hoặc từ luồng capture tự động
- người dùng thao tác qua nút `Cân lần 1` / `Cân lần 2`

### B. Chế độ cân tay
- chỉ cho edit những ô số cân phù hợp với trạng thái bản ghi
- không phải lúc nào cũng cho sửa cả cân lần 1 và cân lần 2

Quy tắc edit bắt buộc phải triển khai:
- `REGISTERED`
  - cho phép nhập tay **số cân lần 1**
  - không cho edit số cân lần 2
- `LOADING_IN_PROGRESS`
  - không cho edit lại số cân lần 1
  - cho phép nhập tay **số cân lần 2**
- `OVERWEIGHT_PENDING_ACTION`
  - mặc định không cho edit trực tiếp số cân, trừ khi màn hiện tại đã có flow xử lý riêng và bạn chứng minh được
- `COMPLETED`
  - không cho edit số cân
- `CANCELLED`
  - không cho edit số cân

Lưu ý:
- “Số cân hiển thị live”, “Số phiếu”, “Trọng lượng hàng” vẫn luôn read-only kể cả manual mode
- chỉ những ô nhập cân lần 1 / cân lần 2 tương ứng mới được editable theo trạng thái

==================================================
7. MA TRẬN ENABLE / DISABLE CHO CÁC NÚT
==================================================

Bạn phải hiện thực rõ enable/disable theo selected record và trạng thái.

### Khi không chọn bản ghi nào
Disable toàn bộ:
- Giao nhận
- In phiếu
- Lưu
- Cân lần 1
- Cân lần 2
- Hủy
- Phiếu liên quan

### Khi chọn record ở trạng thái REGISTERED
- `Cân lần 1`: Enable
- `Cân lần 2`: Disable
- `Lưu`: Disable
- `Giao nhận`: Disable
- `In phiếu`: Disable hoặc chỉ enable nếu business hiện tại đã có phiếu từ trước, nhưng mặc định nên Disable
- `Hủy`: Enable
- `Phiếu liên quan`: Disable nếu chưa có child docs, Enable nếu đã có child docs bất thường từ dữ liệu cũ

### Khi chọn record ở trạng thái LOADING_IN_PROGRESS
- `Cân lần 1`: Disable
- `Cân lần 2`: Enable
- `Lưu`:
  - Auto mode: Disable
  - Manual mode: Enable khi có giá trị cân lần 2 hợp lệ hoặc có thay đổi hợp lệ
- `Giao nhận`: Enable nếu đã có `current_primary_delivery_ticket_id`
- `In phiếu`: Enable nếu đã có `current_primary_weigh_ticket_id`
- `Hủy`: Enable nếu business hiện tại còn cho phép hủy ở giai đoạn này; nếu spec cũ không cấm thì giữ Enable
- `Phiếu liên quan`: Enable nếu có child docs

### Khi chọn record ở trạng thái OVERWEIGHT_PENDING_ACTION
- `Cân lần 1`: Disable
- `Cân lần 2`: Disable mặc định
- `Lưu`: Disable mặc định nếu chưa có flow chỉnh tay riêng
- `Giao nhận`: Enable nếu có delivery ticket
- `In phiếu`: Enable nếu có weigh ticket
- `Hủy`: cân nhắc Disable nếu business không cho hủy sau khi đã phát sinh case quá tải; nếu hệ thống hiện không có rule rõ, hãy báo rõ trong design rồi chọn phương án an toàn
- `Phiếu liên quan`: Enable

### Khi chọn record ở trạng thái COMPLETED
- `Cân lần 1`: Disable
- `Cân lần 2`: Disable
- `Lưu`: Disable
- `Giao nhận`: Enable nếu có delivery ticket
- `In phiếu`: Enable nếu có weigh ticket
- `Hủy`: Disable
- `Phiếu liên quan`: Enable nếu có docs

### Khi chọn record ở trạng thái CANCELLED
- `Cân lần 1`: Disable
- `Cân lần 2`: Disable
- `Lưu`: Disable
- `Giao nhận`: Disable hoặc read-only view only nếu hệ thống có
- `In phiếu`: chỉ enable nếu business cho phép xem/in lại chứng từ cũ; nếu chưa có spec rõ thì chọn Disable là an toàn hơn
- `Hủy`: Disable
- `Phiếu liên quan`: Enable nếu có docs

Quan trọng:
- chưa cân lần 1 thì `Cân lần 2` phải disable
- chưa thực hiện cân lần 1 thì `Lưu` phải disable
- trạng thái các nút phải update ngay khi đổi selection hoặc đổi mode auto/manual

==================================================
8. HÀNH VI CỦA TỪNG NÚT
==================================================

## 8.1 Nút Cân lần 1
### Auto mode
- lấy reading hợp lệ mới nhất từ device snapshot
- thực hiện use case cân lần 1
- sinh `weigh_ticket` + `delivery_ticket` nếu đúng business
- cập nhật `registration_status` sang `LOADING_IN_PROGRESS`
- reload detail/grid nhưng giữ selection

### Manual mode
- dùng giá trị nhập tay của ô cân lần 1
- validate hợp lệ
- thực hiện use case tương đương cân lần 1
- giữ selection

## 8.2 Nút Cân lần 2
### Auto mode
- lấy reading hợp lệ mới nhất từ device snapshot
- cập nhật phiếu cân chính
- tính net weight
- chuyển trạng thái:
  - `COMPLETED`
  - hoặc `OVERWEIGHT_PENDING_ACTION`
- giữ selection

### Manual mode
- dùng giá trị nhập tay của ô cân lần 2
- validate hợp lệ
- cập nhật phiếu
- giữ selection

## 8.3 Nút Lưu
Mục đích của nút này phải được chốt rõ trong code:
- chỉ dùng để lưu thay đổi hợp lệ của các ô edit được phép trên màn
- không dùng thay cho `Cân lần 1` / `Cân lần 2`

Quy tắc:
- Auto mode: Disable
- Manual mode:
  - chỉ enable khi có thay đổi hợp lệ trên những field được edit theo trạng thái
  - không enable khi record còn ở `REGISTERED` và chưa cân lần 1
- save xong phải giữ selection

## 8.4 Nút Giao nhận
- mở chức năng/view liên quan đến `current_primary_delivery_ticket`
- nếu chưa có delivery ticket thì disable
- nếu mở dialog/window thì khi đóng xong phải restore selection dòng hiện tại

## 8.5 Nút In phiếu
- in phiếu cân của `current_primary_weigh_ticket`
- nếu chưa có weigh ticket thì disable
- nếu in thành công thì update `is_printed`
- sau in phải giữ selection

## 8.6 Nút Hủy
- hỏi confirm
- chỉ cho phép ở trạng thái hợp lệ
- update root status sang `CANCELLED`
- reload nhưng giữ selection nếu record vẫn nằm trong danh sách đang xem

## 8.7 Nút Phiếu liên quan
- mở dialog hiển thị toàn bộ child docs:
  - weigh tickets
  - delivery tickets
- không được làm mất selection của dòng cha đang chọn

==================================================
9. TÌM KIẾM Ở HEADER
==================================================

Các ô tìm kiếm trên header như:
- Mã ĐKPT
- Số PTVC
- và các field search khác hiện có trên màn

phải hỗ trợ:
1. nhấn `Enter` để tìm kiếm ngay
2. hỗ trợ tìm kiếm realtime khi đang nhập ký tự

Quy tắc realtime search:
- phải có debounce hợp lý, ví dụ 300–500ms
- không được query mỗi ký tự ngay lập tức nếu chưa debounce
- không được làm app giật hơn

Yêu cầu bổ sung:
- nếu selected record vẫn còn trong kết quả tìm kiếm sau khi filter, cố gắng giữ selection
- nếu không còn trong kết quả thì clear detail đúng cách

==================================================
10. YÊU CẦU GIAO DIỆN GRID
==================================================

Grid của màn hình phải được hoàn thiện về mặt hiển thị như sau:

### A. Hiển thị lưới rõ ràng
- phải có kẻ ô các dòng và các cột rõ ràng
- không để grid phẳng khó nhìn
- dùng border/grid line phù hợp với style desktop vận hành

### B. Text trong grid
- text trong grid phải **bold**
- dễ nhìn, rõ cho người vận hành

### C. Màu text theo nghiệp vụ
Phải áp dụng màu text theo bản chất giao dịch của từng bản ghi:

- **Xuất hàng** (`transaction_type = Xuất` / outbound):  
  => text màu **xanh lá cây**

- **Nhập hàng** (`transaction_type = Nhập` / inbound):  
  => text màu **đen**

- **Quá tải** (`has_overweight_case = true` hoặc trạng thái quá tải):  
  => ưu tiên hiển thị **màu đỏ**

Quy tắc ưu tiên màu:
1. Nếu bản ghi là case quá tải -> màu đỏ
2. Nếu không quá tải:
   - xuất hàng -> xanh lá
   - nhập hàng -> đen

### D. Selection style
- selected row vẫn phải nhìn rõ
- không để màu foreground nghiệp vụ làm mất khả năng đọc khi row được chọn
- phải xử lý selection foreground/background hợp lý

==================================================
11. BỔ SUNG ĐỂ MÀN HÌNH HOÀN THIỆN HƠN
==================================================

Bạn phải chủ động hoàn thiện UX của màn, nhưng không được đi quá spec.

### Nên bổ sung:
- loading state khi đang tìm kiếm / đang gọi action
- busy flag để tránh bấm lặp nút
- confirm dialog cho Hủy
- toast/message ngắn gọn sau thao tác thành công/lỗi
- empty state khi không có bản ghi
- command can-execute update đầy đủ khi:
  - đổi selected record
  - đổi auto/manual mode
  - đổi status
  - đổi dữ liệu input
- clear detail sạch sẽ khi selection = null
- scroll selected row into view sau refresh

### Không được làm:
- không reset form vô lý sau mỗi lệnh
- không nhảy focus lung tung
- không để selected row mất sau reload
- không cho edit những field read-only đã chốt

==================================================
12. PHẠM VI FILE / CODE CẦN RÀ
==================================================

Tùy cấu trúc repo hiện tại, nhưng tối thiểu phải rà và sửa:

- `WeighingView.xaml`
- `WeighingView.xaml.cs` nếu có
- `WeighingViewModel.cs`
- các Commands liên quan:
  - search
  - weigh1
  - weigh2
  - save
  - print
  - cancel
  - delivery
  - related docs
- repositories / use cases mà màn này gọi
- dialog/viewmodel cho `Phiếu liên quan`
- dialog/viewmodel cho `Giao nhận` nếu có
- helper preserve selection / grid refresh logic nếu cần
- styles/resources của DataGrid nếu cần

==================================================
13. YÊU CẦU TRIỂN KHAI
==================================================

Hãy làm theo thứ tự:

### Bước 1 — Review hiện trạng
- đọc XAML màn WeightView hiện tại
- đọc ViewModel hiện tại
- map đúng field đang có trên UI với schema hiện có
- không bịa thêm field

### Bước 2 — Thiết kế state machine UI
- selected record state
- auto/manual mode state
- command enable/disable matrix
- editable/non-editable field matrix
- grid styling rules

### Bước 3 — Implement
- file-by-file
- không pseudo-code
- đầy đủ import
- giữ kiến trúc hiện tại của dự án

### Bước 4 — Test
- selection persistence
- command enable/disable
- auto/manual edit rules
- search Enter
- realtime search with debounce
- clear detail on no selection
- no focus loss after commands
- grid line / bold text / color rules

==================================================
14. OUTPUT BẮT BUỘC
==================================================

Trả kết quả theo format:

## A. REVIEW HIỆN TRẠNG
- màn hiện có gì
- field nào đang dùng
- nút nào đang có nhưng thiếu logic gì

## B. UI/STATE DESIGN
- button state matrix
- field editability matrix
- selection preservation strategy
- search behavior strategy
- grid styling strategy

## C. IMPLEMENTATION
- file tree các file sửa/tạo
- code file-by-file
- XAML
- ViewModel
- commands
- preserve selection logic
- debounce search logic
- DataGrid styles/triggers cho border/bold/color

## D. TEST NOTES
- case nào đã cover
- case nào cần manual test thêm

==================================================
15. QUALITY GATE
==================================================

Không được coi là xong nếu:
- bấm nút xong mất selection
- detail không clear khi không chọn bản ghi
- auto/manual mode không khóa/mở input đúng
- các nút không enable/disable theo trạng thái
- search Enter không hoạt động
- realtime search không debounce
- các ô read-only vẫn edit được
- grid không có đường kẻ rõ
- text grid không bold
- màu text không đúng theo xuất/nhập/quá tải
- code phá spec Phase 2/3 đã chốt
