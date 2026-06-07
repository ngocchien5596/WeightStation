import os

srs_path = r'g:\Source-code\pmcan_C#\SRSdocs\StationApp_System_SRS.md'

with open(srs_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Replace document info title / status
content = content.replace('| Tên Dự án | WeightStation (StationApp) |', '| Tên Dự án | WeightStation (Ứng dụng Trạm cân) |')
content = content.replace('| Phiên bản Tài liệu | 1.1 |', '| Phiên bản Tài liệu | 1.2 |')
content = content.replace('| Trạng thái | Đang Đánh giá (Under Review) |', '| Trạng thái | Đang được duyệt |')

# Replace changes history
change_history_old = """| 1.1 | 07/06/2026 | Antigravity AI | Cập nhật toàn diện dựa trên rà soát mã nguồn C# thực tế (UseCase, Configurations, Workers) và feedback người dùng (bổ sung tự động sao lưu, loại bỏ Supervisor, khớp 100% logic code). |"""
change_history_new = """| 1.1 | 07/06/2026 | Antigravity AI | Cập nhật toàn diện dựa trên rà soát mã nguồn C# thực tế. |
| 1.2 | 07/06/2026 | Antigravity AI | Cập nhật đồng bộ với tài liệu Yêu cầu Kinh doanh (BRD) mới: Việt hóa thuật ngữ, bổ sung hiển thị camera thời gian thực để giám sát đỗ xe, và phân tách quy trình cân nội địa / cân xuất khẩu. |"""
content = content.replace(change_history_old, change_history_new)

# Replace Definitions, Acronyms table
definitions_old = """| **SRS** | Software Requirements Specification (Đặc tả Yêu cầu Phần mềm) |
| **WPF** | Windows Presentation Foundation |
| **Scale Indicator** | Bộ chỉ thị cân (đầu đọc số cân điện tử kết nối qua cổng COM) |
| **Cut Order** | Cắt lệnh giao hàng (tương ứng với thông tin đăng ký phương tiện gốc từ ERP) |
| **Weighing Session** | Lượt cân vật lý của xe (1 lần cân vào, 1 lần cân ra) |
| **Weighing Session Line** | Dòng chi tiết đơn hàng được gán trong một lượt cân |
| **Weigh Ticket** | Phiếu cân tổng hợp cấp lượt cân (Weighing Session) |
| **Delivery Ticket** | Phiếu giao nhận (PGN) cấp dòng chi tiết (Weighing Session Line) |
| **RBAC** | Role-Based Access Control (Kiểm soát truy cập dựa trên vai trò) |
| **TTCP** | Trọng tải cho phép của phương tiện theo đăng kiểm |
| **Outbox Sync** | Cơ chế hàng đợi gửi dữ liệu thay đổi cục bộ lên Server một cách tuần tự và an toàn |
| **Idempotency** | Tính chất đảm bảo một yêu cầu trùng lặp gửi lên Server chỉ được xử lý đúng một lần |"""

definitions_new = """| **SRS** | Đặc tả Yêu cầu Phần mềm (Software Requirements Specification) |
| **WPF** | Công nghệ giao diện đồ họa Windows Presentation Foundation |
| **Đầu hiển thị cân** | Bộ chỉ thị cân (Scale Indicator - đầu đọc số cân điện tử kết nối qua cổng nối tiếp COM) |
| **Đơn cắt lệnh** | Đơn hàng được phê duyệt xuất/nhập (Cut Order - tương ứng với thông tin đăng ký phương tiện gốc từ ERP) |
| **Phiên cân** | Lượt cân vật lý thực tế của xe (Weighing Session - 1 lần cân vào, 1 lần cân ra) |
| **Dòng chi tiết phiên cân** | Dòng chi tiết đơn hàng được gán trong một phiên cân (Weighing Session Line) |
| **Phiếu cân tổng hợp** | Chứng từ cân tổng hợp cấp phiên cân (Weigh Ticket) |
| **Phiếu giao nhận** | Phiếu giao nhận hàng hóa cấp dòng chi tiết (Delivery Ticket) |
| **Phân quyền theo vai trò** | Kiểm soát truy cập dựa trên vai trò của người dùng (RBAC - Role-Based Access Control) |
| **TTCP** | Tải trọng cho phép của phương tiện theo đăng kiểm giao thông |
| **Hàng đợi đồng bộ** | Cơ chế hàng đợi gửi dữ liệu thay đổi cục bộ lên máy chủ một cách tuần tự và an toàn (Outbox Sync) |
| **Cơ chế chống trùng lặp** | Đảm bảo một yêu cầu trùng lặp gửi lên máy chủ chỉ được xử lý đúng một lần duy nhất (Idempotency) |"""

content = content.replace(definitions_old, definitions_new)

# Replace HW-CAM-001 and section 3.1.3
cam_old = """### 3.1.3 Tích hợp Camera Giám sát (Camera Interface)
- **HW-CAM-001**: Hệ thống phải hiển thị luồng video trực tiếp từ tối đa 2 camera IP hỗ trợ chuẩn RTSP ngay trên màn hình lập phiếu cân.
- **HW-CAM-002**: Khi lưu cân lần 1 hoặc lần 2, hệ thống tự động gửi lệnh chụp ảnh nhanh (snapshot) từ camera và lưu trữ tập tin ảnh cục bộ dưới định dạng JPEG/PNG, đồng thời ghi nhận vào bảng `weighing_session_images`.
- **HW-CAM-003**: Hệ thống phải tự động chọn cấu hình camera tùy vào loại đơn hàng: sử dụng camera trạm cân `"C6"` cho đơn hàng xuất khẩu (`IsExportScale = true`) và camera trạm `"C2"` cho đơn hàng thông thường.
- **HW-CAM-004 (Fault Tolerance)**: Các lỗi liên quan đến camera hoặc lỗi luồng RTSP không được phép làm dừng hoặc lỗi luồng cân chính (phải bắt exception và ghi log)."""

cam_new = """### 3.1.3 Tích hợp Camera Giám sát
- **HW-CAM-001**: Hệ thống phải hiển thị trực tiếp (thời gian thực) luồng video từ các camera IP giám sát trạm cân lên giao diện lập phiếu cân, giúp nhân viên cân kiểm tra trực quan vị trí đỗ xe của phương tiện trên bàn cân, đảm bảo toàn bộ bánh xe nằm trọn vẹn trong vùng cân hợp lệ để tránh sai lệch số cân hoặc gian lận trước khi tiến hành lưu kết quả.
- **HW-CAM-002**: Khi lưu cân lần 1 hoặc lần 2, hệ thống tự động chụp ảnh nhanh (snapshot) từ camera và lưu trữ tập tin ảnh cục bộ dưới định dạng JPEG/PNG, đồng thời ghi nhận vào bảng `weighing_session_images` để làm bằng chứng đối chiếu.
- **HW-CAM-003**: Hệ thống phải tự động chọn cấu hình camera tùy vào loại đơn hàng: sử dụng camera trạm cân `"C6"` cho đơn hàng xuất khẩu (`IsExportScale = true`) và camera trạm `"C2"` cho đơn hàng thông thường/nội địa.
- **HW-CAM-004 (Khả năng chịu lỗi)**: Các lỗi liên quan đến camera hoặc lỗi kết nối luồng RTSP không được phép làm dừng hoặc gây lỗi cho luồng cân chính (hệ thống phải bắt exception và ghi nhận nhật ký lỗi)."""

content = content.replace(cam_old, cam_new)

# Replace FR-AUTH-002
rbac_old = """#### FR-AUTH-002: Kiểm soát Truy cập dựa trên Vai trò (RBAC)
- **Mô tả**: Phân quyền hệ thống dựa trên vai trò thực tế của người dùng đang đăng nhập (`StationAuthorization.cs`).
- **Quy tắc phân quyền chi tiết**:
  - **Chỉ ADMIN được phép**: Cân tay (MANUAL mode), Quản lý tài khoản người dùng, Cấu hình thông số hệ thống, Cấu hình thiết bị cân cổng COM, Cấu hình camera, Thiết lập mẫu in, Xem chẩn đoán kết nối, và Quản trị thông tin đồng bộ.
  - **ADMIN và OPERATOR được phép**: Xem danh mục/master data, Xem các màn hình vận hành, Tra cứu phiếu cân, Cân tự động (AUTO mode), và Cập nhật phiên bản phần mềm."""

rbac_new = """#### FR-AUTH-002: Phân quyền theo vai trò (Kiểm soát truy cập dựa trên vai trò - RBAC)
- **Mô tả**: Phân quyền hệ thống dựa trên vai trò thực tế của người dùng đang đăng nhập (`StationAuthorization.cs`).
- **Quy tắc phân quyền chi tiết**:
  - **Chỉ ADMIN (Quản trị viên) được phép**: Thực hiện cân tay (chế độ thủ công MANUAL - tự nhập số cân), Quản trị tài khoản người dùng, Cấu hình thông số hệ thống, Cấu hình kết nối cổng COM thiết bị cân, Cấu hình camera giám sát, Thiết lập mẫu phôi in ấn, Xem chẩn đoán kết nối, Quản trị thông tin đồng bộ và cấu hình/thực hiện sao lưu dữ liệu cục bộ.
  - **OPERATOR (Nhân viên cân) được phép**: Thực hiện cân tự động (chế độ tự động AUTO - đọc số cân từ cổng COM), Xem danh mục dữ liệu gốc, Xem các màn hình vận hành, Tra cứu phiếu cân, In ấn chứng từ giao nhận, và thực hiện bỏ qua (bypass) dung sai hàng bao tại hiện trường sau khi trao đổi và có sự đồng ý của bộ phận xuất hàng."""

content = content.replace(rbac_old, rbac_new)

# Replace Section 3.2.2 standard weighing flow
weigh_flow_old = """### 3.2.2 Quy trình Vận hành Trạm cân Tiêu chuẩn (Standard Weighing Flow)

#### FR-WEIGH-001: Tạo Lượt cân (Weighing Session)
- **Mô tả**: Người dùng chọn một hoặc nhiều đăng ký phương tiện (Cut Order) cùng biển số xe trong danh sách chờ để tạo ra một lượt cân (`weighing_session`) mới ở trạng thái `PENDING_WEIGHT1`.
- **Ràng buộc**: Giai đoạn hiện tại không hỗ trợ gộp nhiều phiếu nhập hàng (`INBOUND`) vào một lượt cân. Không thể gộp cắt lệnh nhập và xuất trong cùng một lượt cân.

#### FR-WEIGH-002: Cân lần 1 (Weight 1 Capture)
- **Mô tả**: Ghi nhận khối lượng cân lần 1 từ thiết bị cân khi xe vào trạm.
- **Xử lý**: 
  1. Cho phép lưu kể cả khi số cân chưa ổn định nhưng phải lưu kèm cờ `Weight1IsStable = true/false` và chế độ cân `Weight1Mode = AUTO/MANUAL`.
  2. Tạo bản ghi Phiếu cân tổng Master (`weigh_tickets` với `RecordRole = MasterSession`) và copy snapshot thông tin đăng kiểm của xe/mooc từ danh mục xe.
  3. Cập nhật `SessionStatus` của lượt cân thành `PENDING_WEIGHT2`.
  4. Chụp ảnh từ camera IP liên quan (chọn camera trạm `C6` nếu là đơn xuất khẩu và `C2` nếu là đơn thường) và lưu vào `weighing_session_images`.
- **Ràng buộc kiểm tra TTCP**: Đối với giao dịch `OUTBOUND` (Xuất hàng), xe bắt buộc phải có trọng tải cho phép (TTCP) hợp lệ trong danh mục xe, nếu không hệ thống sẽ ném lỗi `InvalidOperationException` và chặn không cho thực hiện lưu cân lần 1.

#### FR-WEIGH-003: Cân lần 2 và Tính Khối lượng Tịnh (Weight 2 & Net Weight)
- **Mô tả**: Ghi nhận khối lượng cân lần 2 khi xe quay lại trạm và tự động tính toán khối lượng tịnh.
- **Xử lý**:
  1. Ghi nhận số cân vào `Weight2` và cập nhật `SessionStatus` thành `ALLOCATION_PENDING`.
  2. Tính toán `NetWeight = Math.Abs(Weight1 - Weight2)`.
  3. Kiểm tra dung sai cho hàng bao nếu `BypassTolerance = false` (xem chi tiết tại phần *Xử lý Dung sai & Quá tải*). Operator được quyền bypass dung sai hàng bao khi bấm xác nhận đồng ý vượt dung sai trên giao diện.
  4. Chụp ảnh camera lần 2.
- **Ràng buộc INBOUND**: Đối với giao dịch `INBOUND` (Nhập hàng), hệ thống yêu cầu Cân lần 1 phải lớn hơn hoặc bằng Cân lần 2 (xe chở hàng vào trạm nặng hơn lúc ra), nếu không báo lỗi và chặn không cho lưu.
- **Tự động phân bổ (Auto-Allocation)**: Nếu sau khi lưu cân lần 2, lượt cân chỉ chứa **đúng 1 dòng chi tiết** (1 line), hệ thống hiển thị hộp thoại xác nhận với Operator. Nếu Operator đồng ý:
  - Tự động phân bổ toàn bộ Net Weight cho dòng này.
  - Nếu mặt hàng là hàng bao (`ProductType` chuẩn hóa bằng `Bao`), tự động tính số bao bằng `Round(NetWeight / 50)`.
  - Tự động gọi `AllocateWeighingSessionUseCase` để phân bổ, kiểm tra quá tải, sinh DeliveryTicket và chuyển trạng thái lượt cân sang `READY_TO_COMPLETE`.
  - Đối với giao dịch `INBOUND`, hệ thống cũng tự động hoàn thành luôn các `cut_orders` liên quan sang `COMPLETED / OUT_YARD` mà không check quá tải."""

weigh_flow_new = """### 3.2.2 Quy trình Vận hành Cân Nội địa Tiêu chuẩn (Standard Domestic Weighing Flow)

#### FR-WEIGH-001: Tạo Phiên cân nội địa (Domestic Weighing Session)
- **Mô tả**: Nhân viên cân chọn một hoặc nhiều đơn hàng được duyệt (Cut Order) cùng biển số xe trong danh sách phương tiện chờ để tạo một phiên cân (`weighing_session`) mới ở trạng thái `PENDING_WEIGHT1`.
- **Ràng buộc**: Cho phép gộp nhiều đơn hàng hoặc nhiều nhà phân phối (NPP) vào cùng một phiên cân xe vật lý đối với giao dịch xuất hàng trong nước (Outbound). Giai đoạn hiện tại không hỗ trợ gộp nhiều phiếu nhập hàng (`INBOUND`) vào cùng một phiên cân. Không thể gộp đơn hàng nhập và xuất trong cùng một phiên cân.

#### FR-WEIGH-002: Cân lần 1 nội địa (Domestic Weight 1 Capture)
- **Mô tả**: Ghi nhận khối lượng cân lần 1 từ đầu hiển thị cân khi xe vào trạm.
- **Xử lý**: 
  1. Cho phép lưu kể cả khi số cân chưa ổn định nhưng phải lưu kèm cờ trạng thái ổn định `Weight1IsStable = true/false` và chế độ cân `Weight1Mode = AUTO` (chế độ cân tự động). Đối với Admin, hỗ trợ chế độ cân tay `Weight1Mode = MANUAL` (chế độ tự nhập số cân).
  2. Tạo bản ghi Phiếu cân tổng hợp chính (`weigh_tickets` với `RecordRole = MasterSession`) và lưu snapshot thông tin đăng kiểm của xe/mooc từ danh mục phương tiện.
  3. Cập nhật trạng thái phiên cân `SessionStatus` thành `PENDING_WEIGHT2`.
  4. Hiển thị luồng camera giám sát thời gian thực để nhân viên cân kiểm tra vị trí đỗ xe của phương tiện trên bàn cân, đảm bảo xe đỗ đúng quy định. Tự động chụp ảnh từ camera trạm `C2` (dành cho đơn nội địa/thông thường) và lưu vào `weighing_session_images`.
- **Ràng buộc kiểm tra tải trọng đăng kiểm (TTCP)**: Đối với giao dịch xuất hàng `OUTBOUND`, xe bắt buộc phải có thông tin tải trọng cho phép (TTCP) hợp lệ trong danh mục xe, nếu không hệ thống sẽ chặn và báo lỗi, không cho thực hiện lưu cân lần 1.

#### FR-WEIGH-003: Cân lần 2 và Tính Khối lượng Tịnh nội địa (Domestic Weight 2 & Net Weight)
- **Mô tả**: Ghi nhận khối lượng cân lần 2 khi xe quay lại trạm cân ra và tự động tính toán khối lượng tịnh thực xuất của hàng hóa nội địa.
- **Xử lý**:
  1. Ghi nhận số cân vào `Weight2` và cập nhật `SessionStatus` thành `ALLOCATION_PENDING`.
  2. Tính toán khối lượng tịnh thực tế: `NetWeight = Math.Abs(Weight1 - Weight2)`.
  3. Kiểm tra dung sai cho hàng bao nếu cờ bypass vượt dung sai `BypassTolerance = false` (xem chi tiết tại phần *Xử lý Dung sai & Quá tải*). Nhân viên cân (Operator) được quyền xác nhận bỏ qua (bypass) dung sai hàng bao khi bấm xác nhận đồng ý vượt dung sai trên giao diện sau khi đã trao đổi và có sự đồng ý của bộ phận xuất hàng.
  4. Hiển thị camera giám sát thời gian thực kiểm tra vị trí đỗ xe, và tự động chụp ảnh camera lần 2 từ camera trạm `C2`.
- **Ràng buộc đối với đơn nhập hàng (INBOUND)**: Hệ thống yêu cầu khối lượng cân lần 1 phải lớn hơn hoặc bằng cân lần 2 (xe chở hàng vào trạm nặng hơn lúc ra), nếu không báo lỗi và chặn không cho lưu.
- **Tự động phân bổ khối lượng tịnh (Auto-Allocation)**: Nếu sau khi lưu cân lần 2, phiên cân chỉ chứa **đúng 1 dòng chi tiết** (1 đơn hàng), hệ thống hiển thị hộp thoại xác nhận phân bổ tự động. Nếu nhân viên cân đồng ý:
  - Tự động phân bổ toàn bộ khối lượng tịnh cho đơn hàng này.
  - Nếu mặt hàng thuộc loại hàng bao (`ProductType` là `Bao`), tự động tính số bao quy đổi: `Round(NetWeight / 50)`.
  - Tự động gọi quy trình phân bổ để kiểm tra quá tải, sinh phiếu giao nhận và chuyển trạng thái phiên cân sang `READY_TO_COMPLETE`.
  - Đối với đơn nhập hàng `INBOUND`, tự động hoàn thành luôn các đơn hàng liên quan sang trạng thái hoàn tất `COMPLETED / OUT_YARD` mà không cần kiểm tra quá tải."""

content = content.replace(weigh_flow_old, weigh_flow_new)

# Replace NFR-SEC-002
audit_old = """- **NFR-SEC-002 (Nhật ký Kiểm toán - Audit Logging)**: Mọi hành động nhạy cảm như phê duyệt cân tay, sửa đổi số lượng phân bổ, in lại phiếu cân, hủy lượt cân hoặc thay đổi cấu hình hệ thống bắt buộc phải được ghi lại vào bảng `audit_logs` cục bộ và đồng bộ lên server để phục vụ mục đích kiểm toán."""
audit_new = """- **NFR-SEC-002 (Nhật ký Kiểm toán)**: Mọi hành động nhạy cảm như cân tay (nhập khối lượng thủ công), bỏ qua (bypass) dung sai hàng bao, sửa đổi số lượng phân bổ, in lại phiếu cân, hủy lượt cân hoặc thay đổi cấu hình hệ thống bắt buộc phải được ghi lại vào bảng nhật ký kiểm toán (`audit_logs`) cục bộ và tự động đồng bộ lên máy chủ trung tâm để phục vụ mục đích hậu kiểm."""
content = content.replace(audit_old, audit_new)

with open(srs_path, 'w', encoding='utf-8') as f:
    f.write(content)

print("SRS file updated successfully via Python!")
