# TÀI LIỆU CHI TIẾT
# THIẾT KẾ NGHIỆP VỤ VẬN HÀNH TRẠM CÂN THEO MÔ HÌNH WEIGHING SESSION

## 1. MỤC TIÊU TÀI LIỆU

Tài liệu này mô tả đầy đủ thiết kế nghiệp vụ và kỹ thuật cho hệ thống trạm cân theo mô hình mới, nhằm giải quyết các bài toán thực tế sau:

*   1 xe vào lấy cho nhiều đơn / nhiều đăng ký phương tiện
*   1 xe vào lấy hàng cho nhiều nhà phân phối
*   Tách rõ luồng vận hành thành 3 màn:
    *   Danh sách xe vào
    *   Lập phiếu cân
    *   Danh sách xe ra
*   Hỗ trợ cả:
    *   OUTBOUND
    *   INBOUND
*   Tách rõ:
    *   trạng thái vận hành thực tế của lượt xe
    *   trạng thái của đăng ký phương tiện
    *   trạng thái của chứng từ

Đảm bảo hệ thống đủ rõ ràng để AI/dev code đúng nghiệp vụ.

## 2. BỐI CẢNH NGHIỆP VỤ VÀ VẤN ĐỀ CẦN GIẢI QUYẾT

Thiết kế cũ đang thiên về mô hình:

`1 vehicle_registration = 1 luồng vận hành cân`

Mô hình này không còn phù hợp trong thực tế ngoài trạm cân, vì có các tình huống:

*   1 xe vào lấy cho nhiều đơn
*   1 xe vào lấy cho nhiều đăng ký phương tiện
*   1 xe vào lấy cho nhiều nhà phân phối

nhưng thực tế vật lý chỉ có:

*   1 lần cân vào
*   1 lần cân ra

Vì vậy cần tách rõ:

*   Lớp dữ liệu nguồn từ ERP
    *   vehicle_registrations
*   Lớp vận hành thực tế ngoài trạm
    *   weighing_sessions
*   Lớp chi tiết liên kết giữa session và registration
    *   weighing_session_lines

## 3. NGUYÊN TẮC THIẾT KẾ CHỐT CUỐI

### 3.1 vehicle_registrations vẫn giữ

vehicle_registrations vẫn là dữ liệu nguồn từ ERP hoặc dữ liệu tạo tay, dùng để biểu diễn:

*   1 đăng ký phương tiện
*   1 đơn hàng
*   1 dòng nghiệp vụ gốc

Không bỏ bảng này.

### 3.2 Thêm root vận hành mới: weighing_sessions

weighing_sessions là lượt cân thực tế của 1 xe.

Một session tương ứng với:

*   1 xe thực tế vào trạm
*   1 lần cân vào
*   1 lần cân ra
*   1 net weight tổng của cả xe

### 3.3 Thêm bảng liên kết chi tiết: weighing_session_lines

weighing_session_lines dùng để gắn:

*   1 session
*   với 1..N vehicle_registrations

Mỗi line tương ứng:

*   1 đơn / 1 registration / 1 nhà phân phối trong cùng chuyến xe

### 3.4 Quy tắc cấp dữ liệu
*   Cân ở cấp session
*   Phân bổ thực giao ở cấp line
*   Phiếu cân là chứng từ cấp session
*   Phiếu giao nhận là chứng từ cấp line

## 4. KẾT QUẢ NGHIỆP VỤ MONG MUỐN

Thiết kế mới phải bảo đảm:

### 4.1 Với 1 xe – 1 đơn

Hệ thống vẫn chạy bình thường như trước, nhưng nội bộ sẽ đi qua weighing_session có 1 line.

### 4.2 Với 1 xe – nhiều đơn / nhiều nhà phân phối

Hệ thống phải cho phép:

*   gộp nhiều registration vào 1 session
*   cân một lần cho cả xe
*   sau đó phân bổ thực giao cho từng line
*   in:
    *   1 phiếu cân tổng
    *   N phiếu giao nhận tương ứng từng line

## 5. THIẾT KẾ DỮ LIỆU (DATA MODEL)

### 5.1 Bảng vehicle_registrations

Đây là dữ liệu nguồn từ ERP hoặc tạo tay.

**Ý nghĩa**
*   1 đăng ký phương tiện
*   1 đơn nghiệp vụ gốc
*   1 dòng đầu vào cho hệ thống

**Vai trò sau khi có weighing_session**
Không còn là root vận hành chính của lượt cân nữa.

**Cần bổ sung cột mới**
*   `ProcessingStage nvarchar(30) NOT NULL`
*   `WeighingSessionId uniqueidentifier NULL`

**Ý nghĩa cột mới**
*   **ProcessingStage**
    Dùng để điều phối 3 màn:
    *   IN_YARD
    *   WEIGHING
    *   OUT_YARD
*   **WeighingSessionId**
    Cho biết registration hiện đang thuộc session nào.

### 5.2 Bảng mới weighing_sessions

Đây là root vận hành thực tế của 1 lượt xe.

**Các cột tối thiểu**
*   Id uniqueidentifier NOT NULL
*   SessionNo nvarchar(50) NOT NULL
*   TransactionType nvarchar(30) NOT NULL
*   VehiclePlate nvarchar(50) NULL
*   MoocNumber nvarchar(50) NULL
*   DriverName nvarchar(150) NULL
*   Weight1 decimal(...) NULL
*   Weight2 decimal(...) NULL
*   NetWeight decimal(...) NULL
*   Weight1Time datetime2 NULL
*   Weight2Time datetime2 NULL
*   SessionStatus nvarchar(30) NOT NULL
*   IsCancelled bit NOT NULL DEFAULT 0
*   HasPrintedMasterWeighTicket bit NOT NULL DEFAULT 0
*   CreatedAt datetime2 NOT NULL
*   CreatedBy nvarchar(100) NULL
*   UpdatedAt datetime2 NULL
*   UpdatedBy nvarchar(100) NULL

### 5.3 Bảng mới weighing_session_lines

Đây là line detail của một session.

**Các cột tối thiểu**
*   Id uniqueidentifier NOT NULL
*   WeighingSessionId uniqueidentifier NOT NULL
*   VehicleRegistrationId uniqueidentifier NOT NULL
*   SequenceNo int NOT NULL
*   CustomerCode nvarchar(...) NULL
*   CustomerName nvarchar(...) NULL
*   DistributorCode nvarchar(...) NULL
*   DistributorName nvarchar(...) NULL
*   ProductCode nvarchar(...) NULL
*   ProductName nvarchar(...) NULL
*   PlannedWeight decimal(...) NULL
*   PlannedBagCount int NULL
*   ActualAllocatedWeight decimal(...) NULL
*   ActualAllocatedBagCount int NULL
*   LineStatus nvarchar(30) NOT NULL
*   HasPrintedDeliveryTicket bit NOT NULL DEFAULT 0
*   DeliveryTicketId uniqueidentifier NULL
*   CreatedAt datetime2 NOT NULL
*   CreatedBy nvarchar(100) NULL
*   UpdatedAt datetime2 NULL
*   UpdatedBy nvarchar(100) NULL

### 5.4 Bảng weigh_tickets

Sau khi có weighing_session, weigh_tickets phải được hiểu là:

Phiếu cân tổng cấp session

Không còn là chứng từ gốc theo từng registration trong case multi-order nữa.

**Cần bổ sung**
*   WeighingSessionId uniqueidentifier NULL
*   IsDeleted nếu chưa có

**Quy tắc**
*   mỗi session có tối đa 1 phiếu cân tổng active trong phase này

### 5.5 Bảng delivery_tickets

delivery_tickets trong phase này là:

Phiếu giao nhận cấp line

Mỗi line sinh 1 PGN.

**Cần bổ sung**
*   WeighingSessionId uniqueidentifier NULL
*   WeighingSessionLineId uniqueidentifier NULL
*   IsDeleted nếu chưa có

## 6. TRẠNG THÁI CỦA TỪNG THỰC THỂ

Đây là phần rất quan trọng. Khi đã có weighing_session, cần tách vai trò trạng thái theo đúng cấp thực thể.

### 6.1 weighing_sessions.SessionStatus

Đây là source of truth vận hành chính.

Chỉ dùng các giá trị sau:
*   PENDING_WEIGHT1
*   PENDING_WEIGHT2
*   ALLOCATION_PENDING
*   READY_TO_PRINT
*   COMPLETED
*   CANCELLED

**Ý nghĩa**
*   PENDING_WEIGHT1: Mới tạo session, chưa lưu cân lần 1
*   PENDING_WEIGHT2: Đã lưu cân lần 1, chờ cân lần 2
*   ALLOCATION_PENDING: Đã có net tổng, chưa phân bổ cho các line
*   READY_TO_PRINT: Đã phân bổ xong, sẵn sàng in
*   COMPLETED: Đã in đủ chứng từ cần thiết
*   CANCELLED: Session bị hủy

### 6.2 vehicle_registrations.RegistrationStatus

Khi đã có session, vehicle_registration chỉ nên mang trạng thái lifecycle của registration, không mang trạng thái cân chi tiết nữa.

Chỉ dùng:
*   REGISTERED
*   IN_SESSION
*   COMPLETED
*   CANCELLED

**Ý nghĩa**
*   REGISTERED: Registration đã tồn tại, đang chờ được đưa vào session
*   IN_SESSION: Registration đã được gắn vào một session đang active
*   COMPLETED: Registration đã được thực hiện xong trong session
*   CANCELLED: Registration bị hủy nghiệp vụ

### 6.3 weighing_session_lines.LineStatus

Chỉ dùng:
*   PENDING
*   ALLOCATED
*   PRINTED
*   CANCELLED

**Ý nghĩa**
*   PENDING: Line đã nằm trong session nhưng chưa phân bổ
*   ALLOCATED: Đã phân bổ thực giao
*   PRINTED: Đã in xong PGN của line
*   CANCELLED: Line bị hủy

### 6.4 weigh_tickets – trạng thái hợp lý

Về lâu dài, nếu muốn rõ ràng, nên có WeighTicketStatus:
*   DRAFT
*   READY_TO_PRINT
*   PRINTED
*   VOIDED

**Tuy nhiên**
Để phase đầu không quá nặng, có thể chưa thêm cột status riêng ngay, mà dùng tạm:
*   IsPrinted
*   IsDeleted

**Mapping tạm thời**
*   IsPrinted = false, IsDeleted = false ~ READY_TO_PRINT
*   IsPrinted = true ~ PRINTED
*   IsDeleted = true ~ VOIDED

### 6.5 delivery_tickets – trạng thái hợp lý

Tương tự, về lâu dài có thể có DeliveryTicketStatus:
*   DRAFT
*   READY_TO_PRINT
*   PRINTED
*   VOIDED

**Phase đầu**
Cũng có thể dùng tạm:
*   IsPrinted
*   IsDeleted

## 7. ProcessingStage DÙNG ĐỂ LÀM GÌ

ProcessingStage chỉ dùng để điều phối 3 màn vận hành:
*   IN_YARD
*   WEIGHING
*   OUT_YARD

**Ý nghĩa**
*   IN_YARD: hiển thị ở Danh sách xe vào
*   WEIGHING: hiển thị ở Lập phiếu cân
*   OUT_YARD: hiển thị ở Danh sách xe ra

**Quy tắc**
*   ProcessingStage là field phục vụ UI/query nhanh.
*   Source of truth vận hành vẫn là SessionStatus + RegistrationStatus.

## 8. THIẾT KẾ 3 MÀN VẬN HÀNH

### 8.1 MÀN DANH SÁCH XE VÀO
**Mục đích**
Quản lý các xe / registration đang chờ vào cân.

**Điều kiện hiển thị**
Hiển thị các vehicle_registrations thỏa:
*   ProcessingStage = IN_YARD
*   IsCancelled = false

**Chức năng bắt buộc**
*   Xem danh sách xe vào
*   Tìm kiếm / lọc
*   Tạo thủ công xe nhập hàng
*   Chọn 1 hoặc nhiều registration
*   Bấm:
    *   Tạo lượt cân
    *   hoặc Xác nhận vào cân
*   Tạo weighing_session
*   Tạo weighing_session_lines
*   Chuyển các registration sang WEIGHING
*   Điều hướng sang màn Lập phiếu cân

**Grid gợi ý**
*   Mã ĐKPT
*   TransactionType
*   Số PTVC
*   Mooc
*   Tên tài xế
*   Khách hàng / Nhà phân phối
*   Mã sản phẩm
*   SL đặt
*   Số bao
*   Thời gian tạo

### 8.2 MÀN LẬP PHIẾU CÂN
**Mục đích**
Chỉ dùng cho xe đang cân.

**Điều kiện dữ liệu**
Màn này làm việc theo weighing_session, không theo từng registration riêng.

**Header cấp session**
*   SessionNo
*   TransactionType
*   VehiclePlate
*   MoocNumber
*   DriverName
*   Weight1
*   Weight2
*   NetWeight
*   SessionStatus

**Grid line chi tiết**
Hiển thị toàn bộ weighing_session_lines:
*   SequenceNo
*   Mã ĐKPT
*   Nhà phân phối / Khách hàng
*   Mã sản phẩm
*   SL kế hoạch
*   Số bao kế hoạch
*   SL thực giao phân bổ
*   Số bao thực giao phân bổ
*   LineStatus
*   trạng thái in PGN

**Chức năng bắt buộc**
*   Cân lần 1
*   Lưu cân lần 1
*   Cân lần 2
*   Lưu cân lần 2
*   Phân bổ thực giao
*   In phiếu cân tổng
*   In phiếu giao nhận theo từng line
*   Phiếu liên quan
*   Hủy session

### 8.3 MÀN DANH SÁCH XE RA
**Mục đích**
Hiển thị các xe đã hoàn tất quy trình cân và in đủ chứng từ.

**Điều kiện hiển thị**
Hiển thị các weighing_sessions:
*   SessionStatus = COMPLETED
*   IsCancelled = false

**Grid gợi ý**
*   SessionNo
*   TransactionType
*   VehiclePlate
*   MoocNumber
*   DriverName
*   NetWeight
*   Số line trong session
*   Trạng thái in phiếu cân
*   Trạng thái in PGN toàn session
*   Thời gian hoàn tất

## 9. LUỒNG NGHIỆP VỤ TỔNG THỂ

**Bước 1 — Registration xuất hiện ở Danh sách xe vào**
Nguồn có thể từ:
*   ERP insert xuống
*   hoặc user tạo tay (cho INBOUND)

Khi đó:
*   RegistrationStatus = REGISTERED
*   ProcessingStage = IN_YARD
*   WeighingSessionId = NULL

**Bước 2 — Tạo session**
Từ màn Danh sách xe vào, user chọn:
*   1 hoặc nhiều vehicle_registrations

Sau đó bấm:
*   Tạo lượt cân
*   hoặc Xác nhận vào cân

Hệ thống phải:
*   tạo 1 weighing_session
*   tạo N weighing_session_lines
*   cập nhật từng registration:
    *   RegistrationStatus = IN_SESSION
    *   ProcessingStage = WEIGHING
    *   WeighingSessionId = session.Id
*   điều hướng sang màn Lập phiếu cân với session đó

**Bước 3 — Cân lần 1**
Tại màn Lập phiếu cân:
user thao tác trên session

Sau khi lưu cân lần 1:
*   SessionStatus = PENDING_WEIGHT2

**Bước 4 — Cân lần 2**
Sau khi lưu cân lần 2:
*   tính NetWeight cấp session
*   chuyển:
    *   SessionStatus = ALLOCATION_PENDING

**Bước 5 — Phân bổ thực giao**
User mở màn / modal phân bổ thực giao.
Sau khi confirm phân bổ thành công:
*   tất cả line được update ActualAllocatedWeight, ActualAllocatedBagCount
*   LineStatus = ALLOCATED
*   session:
    *   SessionStatus = READY_TO_PRINT

**Bước 6 — In phiếu cân tổng**
In 1 phiếu cân tổng cho session.
In thành công:
*   weigh_tickets.IsPrinted = true
*   weighing_sessions.HasPrintedMasterWeighTicket = true

**Bước 7 — In phiếu giao nhận**
In 1 phiếu giao nhận cho mỗi line.
In thành công từng line:
*   delivery_tickets.IsPrinted = true
*   weighing_session_lines.HasPrintedDeliveryTicket = true
*   weighing_session_lines.LineStatus = PRINTED

**Bước 8 — Hoàn tất session**
Khi thỏa đồng thời:
*   SessionStatus = READY_TO_PRINT
*   HasPrintedMasterWeighTicket = true
*   tất cả line active đều có HasPrintedDeliveryTicket = true

thì hệ thống phải:
*   SessionStatus = COMPLETED

và cập nhật tất cả registration trong session:
*   RegistrationStatus = COMPLETED
*   ProcessingStage = OUT_YARD

## 10. NGHIỆP VỤ INBOUND

### 10.1 Quy tắc TransactionType
Dùng đúng:
*   TransactionType = INBOUND

### 10.2 Tạo inbound manual
Tạo ở màn Danh sách xe vào, không tạo màn riêng.

### 10.3 Kết quả tạo mới
*   TransactionType = INBOUND
*   RegistrationSource = MANUAL
*   RegistrationStatus = REGISTERED
*   ProcessingStage = IN_YARD

### 10.4 Quy tắc cân cho INBOUND
Ở cấp session
*   Weight1 = trọng lượng tổng
*   Weight2 = trọng lượng xe
*   NetWeight = Weight1 - Weight2

**Validate bắt buộc**
Weight1 >= Weight2

Nếu:
Weight1 < Weight2

thì:
*   báo lỗi
*   không cho hoàn tất

### 10.5 Gộp nhiều inbound vào 1 session?
Phase này chưa cần.

**Quyết định phase hiện tại**
INBOUND manual ở phase đầu đi theo:
*   1 registration = 1 session
*   chưa hỗ trợ gộp nhiều inbound manual vào 1 session

**Lý do:**
*   nhu cầu multi-order/multi-distributor hiện tại chủ yếu là outbound
*   giữ phase đầu ổn định, giảm độ phức tạp

## 11. PHÂN BỔ THỰC GIAO

Đây là chức năng mới bắt buộc của phase này.

### 11.1 Khi nào được phân bổ
Khi session có:
*   Weight1
*   Weight2
*   NetWeight
*   SessionStatus = ALLOCATION_PENDING

### 11.2 Cách phân bổ
Hệ thống phải có màn / modal:
Phân bổ thực giao

Mỗi line hiển thị:
*   PlannedWeight
*   PlannedBagCount
*   ActualAllocatedWeight
*   ActualAllocatedBagCount

### 11.3 Hai cách thao tác
**Cách A — Nhập tay**
User nhập tay cho từng line

**Cách B — Gợi ý tự động**
Hệ thống có thể có nút:
Phân bổ theo kế hoạch

Gợi ý theo:
*   PlannedWeight
*   hoặc PlannedBagCount

Sau đó user chỉnh lại nếu cần.

### 11.4 Validation bắt buộc
Không được cho confirm nếu:
*   tổng ActualAllocatedWeight các line không bằng session.NetWeight

Nếu dùng bao:
*   tổng ActualAllocatedBagCount phải hợp lệ theo rule business hiện hành

### 11.5 Kết quả khi confirm
*   line:
    *   LineStatus = ALLOCATED
*   session:
    *   SessionStatus = READY_TO_PRINT

## 12. RULE IN ẤN

### 12.1 Phiếu cân
Phase này chốt:
Chỉ in 1 phiếu cân tổng cho cả session
Không in 1 phiếu cân cho mỗi line.

### 12.2 Phiếu giao nhận
Phase này chốt:
In 1 phiếu giao nhận cho mỗi line

Tức là:
*   1 weighing_session_line = 1 delivery_ticket

### 12.3 Rule xác định “đã in đủ”
**Với phiếu cân**
*   HasPrintedMasterWeighTicket = true

**Với phiếu giao nhận**
*   tất cả line active:
    *   HasPrintedDeliveryTicket = true

Chỉ khi đủ cả 2 mới hoàn tất session.

## 13. HỦY NGHIỆP VỤ

### 13.1 Hủy session
Nếu user bấm Hủy tại màn Lập phiếu cân:
Hệ thống phải thực hiện trong transaction:
*   weighing_sessions.SessionStatus = CANCELLED
*   weighing_sessions.IsCancelled = true
*   tất cả line:
    *   LineStatus = CANCELLED
*   tất cả weigh_tickets liên quan:
    *   IsDeleted = true
*   tất cả delivery_tickets liên quan:
    *   IsDeleted = true

### 13.2 Với vehicle_registrations
Phải phân biệt rõ Hủy session và Hủy registration.

**Quyết định phase này**
Khi hủy session, registration không bị hủy vĩnh viễn ngay nếu mục tiêu chỉ là hủy lượt cân.

**Cách xử lý hợp lý:**
*   vehicle_registration.RegistrationStatus = REGISTERED
*   vehicle_registration.WeighingSessionId = NULL
*   vehicle_registration.ProcessingStage = IN_YARD

Tức là:
*   trả registration về queue đầu vào để xử lý lại

**Chỉ khi hủy registration thực sự**
mới set:
*   RegistrationStatus = CANCELLED
*   IsCancelled = true

## 14. PHÂN QUYỀN

Chỉ có 2 role:
*   ADMIN
*   OPERATOR

### 14.1 ADMIN
Được:
*   dùng cả 3 màn
*   tạo inbound manual
*   tạo session
*   cân tự động
*   cân tay
*   phân bổ thực giao
*   in
*   hủy session
*   dùng toàn bộ hệ thống

### 14.2 OPERATOR
Được:
*   dùng cả 3 màn
*   tạo inbound manual
*   tạo session
*   cân tự động
*   phân bổ thực giao
*   in
*   hủy session

Không được:
*   cân tay
*   vào quản lý tài khoản

### 14.3 Rule cân tay / cân tự động
*   ADMIN: auto + manual
*   OPERATOR: chỉ auto

Phải chặn ở cả:
*   UI
*   command
*   service/use case

## 15. YÊU CẦU UI / UX

Tất cả 3 màn phải:
*   đồng bộ với system design hiện tại
*   cùng palette màu
*   cùng kiểu grid, form, button, modal
*   không lạc phong cách

### 15.1 Danh sách xe vào
Phải có:
*   search/filter
*   grid
*   tạo inbound manual
*   multi-select
*   nút Tạo lượt cân / Xác nhận vào cân

### 15.2 Lập phiếu cân
Phải có:
*   header session
*   grid line detail
*   nút:
    *   Cân lần 1
    *   Cân lần 2
    *   Lưu
    *   Phân bổ
    *   In PC
    *   In PGN
    *   Phiếu liên quan
    *   Hủy

### 15.3 Danh sách xe ra
Phải có:
*   search/filter
*   grid tra cứu
*   thông tin session đã hoàn tất

### 15.4 Preserve selection
Sau các thao tác:
*   tạo session
*   vào cân
*   lưu cân
*   phân bổ
*   in
*   hủy

phải giữ selection/current record đúng nếu bản ghi còn nằm trong màn.

## 16. PHẠM VI FILE / CODE CẦN RÀ

Tối thiểu phải rà và sửa/tạo:
*   migration cho:
    *   weighing_sessions
    *   weighing_session_lines
    *   vehicle_registrations.ProcessingStage
    *   vehicle_registrations.WeighingSessionId
*   FK cho weigh_tickets
*   FK cho delivery_tickets
*   entity/models
*   repositories
*   query services
*   IncomingVehicleListView.xaml
*   IncomingVehicleListViewModel.cs
*   WeighingView.xaml
*   WeighingViewModel.cs
*   OutgoingVehicleListView.xaml
*   OutgoingVehicleListViewModel.cs
*   màn / modal phân bổ thực giao
*   logic in phiếu mới
*   logic hủy session
*   role guards
*   toast/modal integration

## 17. THỨ TỰ TRIỂN KHAI

**Bước 1 — Review hiện trạng**
*   đọc schema hiện tại
*   xác định flow WeightView hiện tại
*   xác định inbound manual flow hiện tại
*   xác định print logic hiện tại
*   xác định cancel logic hiện tại

**Bước 2 — Schema design**
*   thêm bảng weighing_sessions
*   thêm bảng weighing_session_lines
*   thêm ProcessingStage
*   thêm WeighingSessionId
*   thêm FK/index cần thiết

**Bước 3 — Workflow design**
*   define SessionStatus
*   define RegistrationStatus
*   define LineStatus
*   define create session flow
*   define allocation flow
*   define print-complete flow
*   define cancel session flow

**Bước 4 — UI/UX design**
*   3 màn
*   multi-select tạo session
*   create inbound flow
*   session-level weigh flow
*   allocation modal/screen

**Bước 5 — Implement**
*   file-by-file
*   không pseudo-code
*   single-order phải hoạt động như session có 1 line
*   không phá các flow cũ đang còn dùng được nếu chưa refactor hết trong một lượt

**Bước 6 — Test**
*   single registration → single session
*   multi registration same vehicle → one session
*   multi distributor → one session many lines
*   inbound manual → one session
*   weigh1 / weigh2
*   allocation
*   print master weigh ticket
*   print all PGNs
*   auto move to completed / out yard
*   cancel session
*   role behavior

## 18. OUTPUT BẮT BUỘC CHO AI/DEV

Trả kết quả theo format:

**A. REVIEW HIỆN TRẠNG**
*   flow hiện tại đang bị giới hạn ở đâu
*   vì sao không cover được 1 xe nhiều đơn / nhiều nhà phân phối
*   điểm nào của WeightView hiện tại cần refactor

**B. SCHEMA UPDATE DESIGN**
*   bảng mới nào được thêm
*   cột nào thêm vào bảng cũ
*   kiểu dữ liệu
*   FK/index
*   lý do thiết kế

**C. STATUS MODEL DESIGN**
*   weighing_sessions.SessionStatus
*   vehicle_registrations.RegistrationStatus
*   weighing_session_lines.LineStatus
*   cách dùng IsPrinted, IsDeleted cho chứng từ

**D. WORKFLOW DESIGN**
*   incoming flow
*   session creation flow
*   weighing flow
*   allocation flow
*   print flow
*   completion flow
*   cancel flow

**E. UI/UX DESIGN**
*   layout từng màn
*   grid columns
*   action buttons
*   allocation modal
*   selection behavior

**F. IMPLEMENTATION**
*   file tree
*   migration
*   entity/config update
*   repository/query changes
*   view/viewmodel code file-by-file

**G. TEST NOTES**
*   single order
*   multi order
*   multi distributor
*   inbound manual
*   allocation
*   print complete
*   cancel session
*   role behavior

## 19. QUALITY GATE

Không được coi là xong nếu:
*   vẫn cân theo từng registration cho case nhiều đơn
*   không có weighing_session
*   không có weighing_session_lines
*   không có bước phân bổ thực giao
*   vẫn in phiếu cân theo từng line trong phase này
*   không tách rõ 3 màn
*   không xử lý được multi-select tạo session
*   không auto chuyển sang OUT_YARD khi hoàn tất
*   UI không đồng bộ system design

## 20. MỤC TIÊU CUỐI CÙNG

Tôi cần hệ thống vận hành đúng thực tế:
*   1 xe = 1 lượt cân
*   1 lượt cân = N đơn / N registration / N nhà phân phối
*   cân ở cấp session
*   phân bổ ở cấp line
*   in 1 phiếu cân tổng
*   in N phiếu giao nhận chi tiết
*   có 3 màn:
    *   Danh sách xe vào
    *   Lập phiếu cân
    *   Danh sách xe ra
*   hỗ trợ cả INBOUND và OUTBOUND
*   và usable thật tại trạm cân
