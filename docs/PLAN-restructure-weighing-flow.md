# KẾ HOẠCH CHI TIẾT
# TÁI CẤU TRÚC LUỒNG VẬN HÀNH THÀNH 3 MÀN:
# DANH SÁCH XE VÀO – LẬP PHIẾU CÂN – DANH SÁCH XE RA

Bạn là Principal WPF Product Engineer + Workflow Architect + Business Analyst của dự án StationApp.

Nhiệm vụ của bạn là thiết kế và triển khai lại luồng vận hành trạm cân thành 3 màn hình riêng, để đúng với thực tế vận hành và dễ dùng hơn.

Tài liệu này là source of truth cho việc thiết kế và code.

1. MỤC TIÊU

Tôi cần tách luồng vận hành thành 3 màn rõ ràng:

A. Màn Danh sách xe vào

Dùng để:

hiển thị các xe đang chờ vào cân
tạo thủ công phiếu cho xe nhập hàng
xác nhận xe vào cân
đẩy xe từ màn này sang màn Lập phiếu cân
B. Màn Lập phiếu cân

Dùng để:

xử lý xe đang cân
thao tác:
cân lần 1
lưu
cân lần 2
lưu
giao nhận
in phiếu
phiếu liên quan
hủy phiếu
C. Màn Danh sách xe ra

Dùng để:

hiển thị các xe đã cân lần 2 xong
và đã in xong phiếu cân + phiếu giao nhận
phục vụ tra cứu / xác nhận xe ra khỏi quy trình cân
2. TƯ DUY THIẾT KẾ MỚI
2.1 vehicle_registrations vẫn là aggregate root

Không thay đổi nguyên tắc:

vehicle_registrations là root
weigh_tickets là child docs
delivery_tickets là child docs
2.2 Ba màn chỉ là ba “góc nhìn vận hành” khác nhau trên cùng một registration

Tức là:

không tạo 3 loại entity khác nhau
vẫn dùng cùng vehicle_registrations
chỉ khác nhau ở:
trạng thái
bucket vận hành
điều kiện hiển thị
3. CẦN THÊM MỘT TRƯỜNG MỚI ĐỂ ĐIỀU PHỐI 3 MÀN

Các trạng thái hiện tại như:

REGISTERED
LOADING_IN_PROGRESS
COMPLETED
CANCELLED

là chưa đủ để điều phối đúng 3 màn.

Lý do:

một phiếu REGISTERED có thể đang nằm ở Danh sách xe vào
nhưng khi user bấm “Vào cân” thì vẫn chưa lưu cân lần 1, nên status nghiệp vụ chưa đổi
lúc đó cần biết phiếu đã được đẩy sang màn Lập phiếu cân hay chưa
3.1 Bổ sung field mới ở vehicle_registrations

Phải thêm:

ProcessingStage nvarchar(30) NOT NULL
3.2 Giá trị hợp lệ của ProcessingStage

Chỉ dùng 3 giá trị:

IN_YARD
WEIGHING
OUT_YARD
3.3 Ý nghĩa
IN_YARD
xe đang ở danh sách xe vào
chưa được xác nhận đưa vào cân
WEIGHING
xe đang ở quy trình cân
đang thao tác trên màn Lập phiếu cân
OUT_YARD
xe đã cân xong
đã in xong phiếu cân + phiếu giao nhận
hiển thị ở Danh sách xe ra
3.4 Giá trị mặc định

Khi tạo mới registration:

ProcessingStage = IN_YARD
4. LUỒNG TỔNG THỂ CỦA 3 MÀN
4.1 Xe từ ERP đổ xuống

Khi ERP insert một vehicle_registration mới:

ProcessingStage = IN_YARD
xuất hiện ở màn Danh sách xe vào
4.2 Xe nhập hàng tạo tay

Khi user tạo tay xe nhập hàng:

TransactionType = INBOUND
RegistrationSource = MANUAL
RegistrationStatus = REGISTERED
ProcessingStage = IN_YARD

=> cũng xuất hiện ở màn Danh sách xe vào

4.3 Khi user xác nhận “Vào cân”

Từ màn Danh sách xe vào, user bấm action:

Xác nhận vào cân

Hệ thống phải:

update ProcessingStage = WEIGHING
update UpdatedAt, UpdatedBy
điều hướng/mở sang màn Lập phiếu cân
chọn sẵn đúng registration đó
4.4 Khi xe đang cân

Xe nằm ở màn Lập phiếu cân cho đến khi:

lưu cân lần 1
lưu cân lần 2
in phiếu cân
in phiếu giao nhận
hoàn tất in đầy đủ
4.5 Khi xe hoàn tất và in xong

Khi thỏa đồng thời:

RegistrationStatus = COMPLETED
đã in xong đủ toàn bộ phiếu cân cần in
đã in xong đủ toàn bộ phiếu giao nhận cần in

thì hệ thống phải:

update ProcessingStage = OUT_YARD

=> bản ghi sẽ xuất hiện ở màn Danh sách xe ra

5. ĐIỀU KIỆN HIỂN THỊ CỦA TỪNG MÀN
5.1 Màn Danh sách xe vào

Hiển thị các registration thỏa:

ProcessingStage = IN_YARD
IsCancelled = false

Không hiển thị:

phiếu đã hủy
phiếu đã ra khỏi quy trình
Có thể hiển thị:
cả outbound từ ERP
cả inbound tạo tay
5.2 Màn Lập phiếu cân

Hiển thị / làm việc với các registration thỏa:

ProcessingStage = WEIGHING
IsCancelled = false

Lưu ý:

màn này là màn thao tác chính
grid của màn này chỉ nên hiển thị các xe đang cân
không trộn với xe chờ cân và xe đã xong
5.3 Màn Danh sách xe ra

Hiển thị các registration thỏa:

ProcessingStage = OUT_YARD
RegistrationStatus = COMPLETED
IsCancelled = false
Và thêm điều kiện bắt buộc:
đã in xong đủ phiếu cân
đã in xong đủ phiếu giao nhận

Nếu registration là case split:

phải in đủ toàn bộ child docs liên quan
rới mới được sang OUT_YARD
6. MÀN DANH SÁCH XE VÀO – YÊU CẦU CHI TIẾT
6.1 Mục đích

Màn này là màn quản lý queue đầu vào.

6.2 Chức năng bắt buộc
Xem danh sách xe vào
Tìm kiếm / lọc
Tạo thủ công xe nhập hàng
Chọn 1 xe và bấm Xác nhận vào cân
Có thể xem nhanh thông tin phiếu
6.3 Tạo thủ công xe nhập hàng

Chỉ tạo tay cho hàng nhập ở màn này.

Khi tạo mới:
TransactionType = INBOUND
RegistrationSource = MANUAL
RegistrationStatus = REGISTERED
ProcessingStage = IN_YARD
Dùng lại các field đang có của form hiện tại / system design

Tối thiểu:

Số PTVC
Mooc
Tên tài xế
Mã ĐKPT
Khách hàng
Mã sản phẩm
SL đặt
Số bao
Ghi chú
các field vận hành hiện có khác nếu đang dùng
Không tạo màn tạo nhập riêng

Tạo xe nhập hàng phải là một phần của màn Danh sách xe vào.

6.4 Action “Xác nhận vào cân”

Đây là action quan trọng nhất của màn này.

Khi user bấm:
cập nhật ProcessingStage = WEIGHING
điều hướng sang màn Lập phiếu cân
load đúng registration vừa chọn
đảm bảo không làm mất context
6.5 Grid của màn Danh sách xe vào

Nên hiển thị tối thiểu:

Mã ĐKPT
TransactionType
Số PTVC
Mooc
Tên tài xế
Khách hàng
Mã sản phẩm
SL đặt
Số bao
Trạng thái nghiệp vụ
Thời gian tạo
7. MÀN LẬP PHIẾU CÂN – YÊU CẦU CHI TIẾT
7.1 Mục đích

Chỉ dùng cho xe đang cân.

7.2 Dữ liệu hiển thị

Màn này chỉ làm việc với registration có:

ProcessingStage = WEIGHING
7.3 Chức năng bắt buộc
Cân lần 1
Lưu
Cân lần 2
Lưu
Giao nhận
In PC
In PGN
Phiếu liên quan
Hủy
7.4 Luồng outbound

Giữ nguyên logic hiện tại:

Weight1, Weight2, NetWeight theo outbound flow đang dùng
7.5 Luồng inbound

Phải hỗ trợ ngay trên màn này:

TransactionType = INBOUND
Weight1 = trọng lượng tổng
Weight2 = trọng lượng xe
NetWeight = Weight1 - Weight2
Validate:
Weight1 >= Weight2
Flow:
Cân lần 1 -> Lưu
dỡ hàng
Cân lần 2 -> Lưu
hoàn tất
7.6 Khi nào được chuyển sang Danh sách xe ra

Không phải ngay khi lưu cân lần 2.

Chỉ khi:

RegistrationStatus = COMPLETED
đã in xong đủ PC
đã in xong đủ PGN

thì mới:

ProcessingStage = OUT_YARD
7.7 Với case split quá tải

Nếu là outbound split:

grid chính của WeightView vẫn chỉ hiển thị phiếu chính/dòng chính
nhưng nghiệp vụ in phải in đủ child docs
chỉ khi in đủ thì mới cho chuyển OUT_YARD
8. MÀN DANH SÁCH XE RA – YÊU CẦU CHI TIẾT
8.1 Mục đích

Hiển thị các xe đã hoàn tất quy trình cân và in chứng từ.

8.2 Điều kiện hiển thị

Chỉ hiển thị registration thỏa:

ProcessingStage = OUT_YARD
RegistrationStatus = COMPLETED
đã in xong đủ phiếu cân
đã in xong đủ phiếu giao nhận
8.3 Chức năng chính
tra cứu
xem thông tin đã hoàn tất
có thể mở phiếu liên quan / xem lại nếu cần
không phải là màn để cân tiếp
8.4 Grid của màn Danh sách xe ra

Nên hiển thị tối thiểu:

Mã ĐKPT
TransactionType
Số PTVC
Mooc
Tên tài xế
Khách hàng
Mã sản phẩm
NetWeight
Thời gian cân xong
Trạng thái in PC
Trạng thái in PGN
9. QUY TẮC CHUYỂN STAGE
9.1 Tạo mới hoặc ERP insert
ProcessingStage = IN_YARD
9.2 Xác nhận vào cân
IN_YARD -> WEIGHING
9.3 Hoàn tất cân lần 2 nhưng chưa in đủ
vẫn giữ WEIGHING
9.4 Hoàn tất cân lần 2 và đã in đủ chứng từ
WEIGHING -> OUT_YARD
9.5 Hủy phiếu

Nếu phiếu bị hủy:

RegistrationStatus = CANCELLED
IsCancelled = true
child docs IsDeleted = true
không hiển thị ở 3 màn vận hành thường nữa, hoặc hiển thị theo rule riêng nếu có màn audit

Khuyến nghị ở phase này:

phiếu hủy không hiển thị ở Danh sách xe vào / Lập phiếu cân / Danh sách xe ra
10. QUY TẮC IN ẤN LIÊN QUAN ĐẾN CHUYỂN MÀN XE RA
10.1 Với phiếu bình thường
IN PC in 1 phiếu
IN PGN in 1 phiếu
khi cả 2 loại đã in xong thì cho sang OUT_YARD
10.2 Với phiếu split quá tải
IN PC phải in đủ tất cả phiếu cân liên quan
IN PGN phải in đủ tất cả phiếu giao nhận liên quan
chỉ khi in đủ tất cả thì mới:
ProcessingStage = OUT_YARD
10.3 Cần có rule xác định “đã in đủ”

Bạn phải thiết kế rõ:

Với weigh tickets
tất cả weigh_tickets active của registration đó đều IsPrinted = true
Với delivery tickets
tất cả delivery_tickets active của registration đó đều IsPrinted = true

Khi cả 2 điều kiện đều đúng:

registration đủ điều kiện sang màn xe ra
11. ẢNH HƯỞNG ĐẾN PHÂN QUYỀN

Theo rule role hiện tại:

ADMIN
dùng được cả 3 màn
được cân tay
được hủy phiếu
OPERATOR
dùng được cả 3 màn
được vào master data
được dùng chức năng in
chỉ được cân tự động
được hủy phiếu
Với màn Danh sách xe vào

Cần chốt:

OPERATOR được tạo tay xe nhập hàng
=> Có, nếu đây là nghiệp vụ vận hành thường ngày

Tôi khuyến nghị:

cả ADMIN và OPERATOR đều được tạo inbound manual ở màn Danh sách xe vào
12. THAY ĐỔI SCHEMA BẮT BUỘC
12.1 vehicle_registrations

Thêm cột:

ProcessingStage nvarchar(30) NOT NULL

Giá trị hợp lệ:

IN_YARD
WEIGHING
OUT_YARD
12.2 Khuyến nghị index

Nên tạo index cho:

ProcessingStage
RegistrationStatus
TransactionType
IsCancelled

để phục vụ query 3 màn

13. YÊU CẦU UI / SYSTEM DESIGN

Phải làm theo đúng system design của app hiện tại.

13.1 Màn Danh sách xe vào
cùng style với các màn cấu hình/vận hành đã có
có khu tìm kiếm
có grid
có form tạo nhanh xe nhập hàng hoặc modal tạo mới cùng style app
có nút Xác nhận vào cân
13.2 Màn Lập phiếu cân
vẫn giữ vai trò màn thao tác chính cho xe đang cân
grid của màn này chỉ hiển thị các xe đang ở WEIGHING
13.3 Màn Danh sách xe ra
cùng style
có khu tìm kiếm / lọc
có grid
hiển thị trạng thái hoàn tất/in ấn
14. CHỨC NĂNG “XÁC NHẬN VÀO CÂN”

Đây là chức năng bắt buộc của màn Danh sách xe vào.

Hành vi

Khi user chọn 1 xe ở Danh sách xe vào và bấm:

Xác nhận vào cân

Hệ thống phải:

validate registration hợp lệ
update:
ProcessingStage = WEIGHING
UpdatedAt
UpdatedBy
chuyển sang màn Lập phiếu cân
load/select đúng bản ghi đó trên màn Lập phiếu cân
Toast
Success:
“Đã chuyển xe vào màn Lập phiếu cân.”
Error:
“Không thể chuyển xe vào cân. Vui lòng thử lại.”
15. CHỨC NĂNG “TẠO XE NHẬP HÀNG” Ở DANH SÁCH XE VÀO
Hành vi

Từ màn Danh sách xe vào, user được tạo thủ công registration nhập hàng.

Kết quả

Registration mới phải có:

TransactionType = INBOUND
RegistrationSource = MANUAL
RegistrationStatus = REGISTERED
ProcessingStage = IN_YARD
Sau khi tạo thành công
phiếu mới xuất hiện trong grid Danh sách xe vào
người dùng có thể chọn và bấm Xác nhận vào cân
16. CHỨC NĂNG “CHUYỂN XE RA”
Không cần nút tay riêng ở phase này

Việc vào Danh sách xe ra nên là tự động.

Rule

Khi một registration thỏa:

RegistrationStatus = COMPLETED
đủ tất cả PC đã in
đủ tất cả PGN đã in

thì hệ thống tự:

set ProcessingStage = OUT_YARD

Không cần user phải bấm thêm nút “Cho xe ra” ở phase này.

17. FILE / CODE PHẠM VI CẦN RÀ

Tối thiểu phải rà và sửa/tạo:

vehicle_registrations entity + migration
query/repository cho 3 màn
menu/sidebar
IncomingVehicleListView.xaml
IncomingVehicleListViewModel.cs
WeighingView.xaml
WeighingViewModel.cs
OutgoingVehicleListView.xaml
OutgoingVehicleListViewModel.cs
create inbound manual flow
confirm enter weighing flow
complete + print -> move to out yard flow
toast/modal integration
permission guards
18. THỨ TỰ TRIỂN KHAI
Bước 1 — Review hiện trạng
đọc schema hiện tại
xác định luồng hiện tại của WeightView
xác định inbound manual flow hiện có hay chưa
xác định print-complete logic hiện có
xác định role rules hiện tại
Bước 2 — Schema design
thêm ProcessingStage
tạo index nếu cần
Bước 3 — Workflow design
thiết kế stage transitions
thiết kế query filter cho 3 màn
thiết kế create inbound flow
thiết kế confirm enter weighing flow
thiết kế auto move to out yard flow
Bước 4 — UI/UX design
3 màn hình
tìm kiếm / grid / form / action buttons
đồng bộ system design
Bước 5 — Implement
file-by-file
không pseudo-code
không phá logic cũ đang dùng được
refactor WeightView về đúng vai trò “xe đang cân”
Bước 6 — Test
xe từ ERP vào Danh sách xe vào
tạo tay xe nhập hàng
xác nhận vào cân
cân lần 1 / cân lần 2
in đủ phiếu
tự chuyển sang Danh sách xe ra
case split quá tải
case inbound
case cancel
19. OUTPUT BẮT BUỘC

Trả kết quả theo format:

A. REVIEW HIỆN TRẠNG
WeightView hiện đang làm gì
đâu là điểm chưa phù hợp với vận hành thực tế
inbound manual hiện đang ở đâu
print complete logic hiện ở đâu
B. SCHEMA UPDATE DESIGN
cột mới ProcessingStage
kiểu dữ liệu
default value
index đề xuất
C. WORKFLOW DESIGN
định nghĩa 3 màn
stage transition matrix
điều kiện vào/ra từng màn
auto move to out yard logic
D. UI/UX DESIGN
layout từng màn
grid columns
action buttons
create inbound flow
confirm enter weighing flow
E. IMPLEMENTATION
file tree
migration
entity/config update
query/repository changes
view/viewmodel code file-by-file
F. TEST NOTES
incoming list
create inbound
confirm enter weighing
weighing flow
outbound flow
inbound flow
print complete to out yard
cancel flow
20. QUALITY GATE

Không được coi là xong nếu:

WeightView vẫn trộn cả xe chờ cân và xe đã cân xong
không có màn Danh sách xe vào
không có màn Danh sách xe ra
không tạo được inbound manual ở Danh sách xe vào
không có action xác nhận vào cân
không tự chuyển xe sang Danh sách xe ra sau khi hoàn tất và in đủ
không dùng ProcessingStage hoặc cơ chế tương đương đủ rõ để điều phối 3 màn
UI không đồng bộ system design hiện tại
21. MỤC TIÊU CUỐI CÙNG

Tôi cần hệ thống vận hành theo đúng logic thực tế:

Danh sách xe vào: quản lý xe chờ cân, tạo tay xe nhập hàng
Lập phiếu cân: chỉ xử lý xe đang cân
Danh sách xe ra: hiển thị xe đã cân xong và in đủ phiếu

Tất cả phải:

dùng chung vehicle_registrations làm root
đồng bộ với system design hiện tại
usable thật trong vận hành
và rõ ràng hơn rất nhiều so với việc dồn tất cả vào một màn
