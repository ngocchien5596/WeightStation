import os

srs_path = r'g:\Source-code\pmcan_C#\SRSdocs\StationApp_System_SRS.md'

with open(srs_path, 'r', encoding='utf-8') as f:
    content = f.read()

# Dictionary of replacements for localization and terminology consistency
replacements = {
    # General translations
    "mô hình Lượt Cân (Weighing Session)": "mô hình Phiên cân (Weighing Session)",
    "offline-first": "ưu tiên cục bộ (offline-first)",
    "Chống gian lận & Audit": "Chống gian lận & Nhật ký kiểm toán",
    "mô hình Weighing Session": "mô hình Phiên cân (Weighing Session)",
    "CO lại cắt lệnh": "cấp lại đơn cắt lệnh",
    "kế thừa lượt cân cũ": "kế thừa phiên cân cũ",
    "tạo đơn thủ công nhập hàng": "tạo đơn cắt lệnh nhập hàng thủ công",
    "tạo đơn hàng": "đơn cắt lệnh",
    "phân bổ cân": "phân bổ khối lượng tịnh",
    "Cut Order/Vehicle Registration": "Đơn cắt lệnh/Đăng ký xe",
    "luồng INBOUND": "luồng Nhập hàng (Inbound)",
    "Vận hành Lượt cân (Weighing Session): Gom nhiều cắt lệnh có cùng biển số xe vào một lượt cân. Thực hiện cân vào (Weight 1), cân ra (Weight 2) và tính toán trọng lượng tịnh thực tế (Net Weight).": 
        "Vận hành Phiên cân (Weighing Session): Gộp nhiều đơn cắt lệnh có cùng biển số xe vào một phiên cân. Thực hiện cân lần 1 (Weight 1), cân lần 2 (Weight 2) và tính toán khối lượng tịnh thực tế (Net Weight).",
    "Phân bổ trọng lượng thực tế (Weight Allocation): Khi một lượt xe cân nhiều đơn, Operator phân bổ tổng Net Weight của xe cho từng dòng cắt lệnh chi tiết.": 
        "Phân bổ khối lượng tịnh thực tế (Weight Allocation): Khi một xe cân gộp nhiều đơn, Nhân viên cân (Operator) phân bổ tổng khối lượng tịnh (Net Weight) của xe cho từng dòng đơn cắt lệnh chi tiết.",
    "gán kèm lượt cân": "gán kèm phiên cân",
    "(cấp Session)": "(cấp Phiên cân)",
    "(cấp Line)": "(cấp Dòng chi tiết)",
    "quy trình tách phiếu khi Net Weight": "quy trình tách phiên cân khi khối lượng tịnh (Net Weight)",
    "Cho phép một cắt lệnh": "Cho phép một đơn cắt lệnh",
    "hàng đợi sync_outbox": "hàng đợi đồng bộ (sync_outbox)",
    "chạy offline": "chạy ngoại tuyến (offline)",
    "dữ liệu local": "dữ liệu cục bộ (local)",
    "hỗ trợ Admin kích hoạt": "hỗ trợ Quản trị viên (Admin) kích hoạt",
    "hủy session": "hủy phiên cân (Session)",
    "Operator chỉ được phép": "Nhân viên cân (Operator) chỉ được phép",
    "bypass dung sai": "bỏ qua (bypass) dung sai",
    "lưu cân 2": "lưu cân lần 2",
    "Admin có đầy đủ quyền hạn của Operator": "Quản trị viên (Admin) có đầy đủ quyền hạn của Nhân viên cân (Operator)",
    "sao lưu/phục hồi dữ liệu local": "sao lưu/phục hồi dữ liệu cục bộ (local)",
    "Audit Logs": "Nhật ký kiểm toán (Audit Logs)",
    "Ràng buộc Net Weight: Giá trị Net Weight thực tế của phiếu cân không được nhỏ hơn 0. Nếu là Outbound, Net Weight = |Weight 2 - Weight 1|.": 
        "Ràng buộc khối lượng tịnh (Net Weight): Khối lượng tịnh thực tế của phiếu cân không được nhỏ hơn 0. Nếu là đơn xuất hàng (Outbound), Net Weight = |Weight 2 - Weight 1|.",
    "Lượt cân chỉ được chuyển": "Phiên cân chỉ được chuyển",
    "Phiếu cân tổng": "Phiếu cân tổng hợp",
    "Mọi lỗi cơ sở dữ liệu local": "Mọi lỗi cơ sở dữ liệu cục bộ",
    "tạo đơn Inbound thủ công. Nút \"Tạo lượt cân\"": "tạo đơn cắt lệnh nhập hàng (Inbound) thủ công. Nút \"Tạo phiên cân\"",
    "Grid các dòng chi tiết trong lượt cân.": "Grid các dòng chi tiết trong phiên cân.",
    "Hủy lượt cân.": "Hủy phiên cân.",
    "Grid các lượt cân đã hoàn tất.": "Grid các phiên cân đã hoàn tất.",
    "Scale Device": "Thiết bị cân (Scale Device)",
    "cơ sở dữ liệu local": "cơ sở dữ liệu cục bộ (local)",
    "vai trò (Role: ADMIN hoặc OPERATOR)": "vai trò (Role: ADMIN - Quản trị viên hoặc OPERATOR - Nhân viên cân)",
    "lượt cân chứa nhiều đơn hàng (nhiều dòng chi tiết), Operator phải phân bổ tổng khối lượng tịnh của xe cho từng dòng.": 
        "phiên cân chứa nhiều đơn hàng (nhiều dòng chi tiết), Nhân viên cân (Operator) phải phân bổ tổng khối lượng tịnh của xe cho từng dòng.",
    "của lượt cân.": "của phiên cân.",
    "trạng thái lượt cân chuyển thành `READY_TO_COMPLETE`.": "trạng thái phiên cân chuyển thành `READY_TO_COMPLETE`.",
    "Sinh 1 phiếu cân tổng Master (`weigh_tickets` có `RecordRole = MasterSession`) và các phiếu giao nhận chi tiết (`delivery_tickets` có `RecordRole = Normal`) cho từng dòng chi tiết.": 
        "Sinh 1 phiếu cân tổng hợp chính (Master) (`weigh_tickets` có `RecordRole = MasterSession`) và các phiếu giao nhận chi tiết (`delivery_tickets` có `RecordRole = Normal`) cho từng dòng chi tiết.",
    "1 phiếu giao nhận Master (`DeliveryTicket` với `RecordRole = Master`) đại diện cho toàn bộ lượt cân.": 
        "1 phiếu giao nhận chính (Master) (`DeliveryTicket` với `RecordRole = Master`) đại diện cho toàn bộ phiên cân.",
    "Các phiếu cân con (`WeighTicket` với `RecordRole = CutOrderDerived`) tương ứng với từng dòng chi tiết để ERP đồng bộ.": 
        "Các phiếu cân con (`WeighTicket` với `RecordRole = CutOrderDerived`) tương ứng với từng dòng chi tiết để ERP đồng bộ.",
    "Phân bổ số lượng bao và trọng lượng tương ứng vào các phiếu này.": 
        "Phân bổ số lượng bao và khối lượng tương ứng vào các phiếu này.",
    "các phiếu Master và phiếu con dư thừa sẽ tự động được soft-delete (`IsDeleted = true`).": 
        "các phiếu chính (Master) và phiếu con dư thừa sẽ tự động được xóa mềm (`IsDeleted = true`).",
    "Gọi `RefreshSessionOverweightState` để tính toán trạng thái quá tải của lượt cân.": 
        "Gọi `RefreshSessionOverweightState` để tính toán trạng thái quá tải của phiên cân.",
    "hoàn tất lượt cân": "hoàn tất phiên cân",
    "Phiếu cân tổng Master": "Phiếu cân tổng hợp chính (Master)",
    "FR-OUT-001: Hoàn tất lượt cân và cho xe ra": "FR-OUT-001: Hoàn tất phiên cân và cho xe ra",
    "Hoàn tất lượt cân": "Hoàn tất phiên cân",
    "Lượt cân ở trạng thái `READY_TO_COMPLETE`": "Phiên cân ở trạng thái `READY_TO_COMPLETE`",
    "cắt lệnh liên quan (`cut_orders`)": "đơn cắt lệnh liên quan (`cut_orders`)",
    "hàng đợi `sync_outbox` để đồng bộ lên Central Server.": "hàng đợi đồng bộ (`sync_outbox`) để đồng bộ lên máy chủ trung tâm (Central Server).",
    "FR-EXPORT-001: Cân đơn xuất khẩu lớn qua nhiều lượt xe": "FR-EXPORT-001: Cân đơn xuất khẩu lớn qua nhiều phiên cân xe",
    "cắt lệnh xuất hàng (`OUTBOUND`)": "đơn cắt lệnh xuất hàng (`OUTBOUND`)",
    "Tạo một `WeighingSession` con độc lập ở trạng thái dở dang (`PENDING_WEIGHT1`) cho mỗi chuyến xe.": 
        "Tạo một phiên cân (`WeighingSession`) con độc lập ở trạng thái dở dang (`PENDING_WEIGHT1`) cho mỗi chuyến xe.",
    "PlannedWeight gốc - tổng NetWeight của các chuyến xe trước đó đã hoàn tất hoặc sẵn sàng hoàn tất": 
        "khối lượng kế hoạch (PlannedWeight) gốc - tổng khối lượng tịnh (NetWeight) của các chuyến xe trước đó đã hoàn tất hoặc sẵn sàng hoàn tất",
    "cắt lệnh xuất khẩu nguồn sang cắt lệnh xuất khẩu đích.": 
        "đơn cắt lệnh xuất khẩu nguồn sang đơn cắt lệnh xuất khẩu đích.",
    "Chuyến xe con phải có **đúng 1 dòng cắt lệnh** hoạt động (`activeLines.Count == 1`). Cả hai đơn hàng nguồn và đích đều chưa chốt và thuộc luồng xuất khẩu.": 
        "Chuyến xe con phải có **đúng 1 dòng đơn cắt lệnh** hoạt động (`activeLines.Count == 1`). Cả hai đơn cắt lệnh nguồn và đích đều chưa chốt và thuộc luồng xuất khẩu.",
    "dòng chi tiết (`WeighingSessionLine`), tất cả các phiếu cân (`WeighTicket`) và phiếu giao nhận (`DeliveryTicket`) liên quan sang cắt lệnh đích. Đồng thời tính toán lại và cập nhật ID phiếu cân chính (`CurrentPrimaryWeighTicketId`) và phiếu giao nhận chính (`CurrentPrimaryDeliveryTicketId`) cho cả hai đơn hàng nguồn và đích.": 
        "dòng chi tiết phiên cân (`WeighingSessionLine`), tất cả các phiếu cân tổng hợp (`WeighTicket`) và phiếu giao nhận (`DeliveryTicket`) liên quan sang đơn cắt lệnh đích. Đồng thời tính toán lại và cập nhật ID phiếu cân chính (`CurrentPrimaryWeighTicketId`) và phiếu giao nhận chính (`CurrentPrimaryDeliveryTicketId`) cho cả hai đơn cắt lệnh nguồn và đích.",
    "cho phép Admin/Operator chốt sản lượng cho đơn hàng xuất khẩu lớn.": 
        "cho phép Quản trị viên hoặc Nhân viên cân chốt sản lượng cho đơn cắt lệnh xuất khẩu lớn.",
    "đơn hàng cha": "đơn cắt lệnh cha",
    "trạng thái đơn cha": "trạng thái đơn cắt lệnh cha",
    "FR-REISSUE-001: Kế thừa số cân lần 1 và tự động phân bổ khi CO lại cắt lệnh": 
        "FR-REISSUE-001: Kế thừa số cân lần 1 và tự động phân bổ khi cấp lại đơn cắt lệnh",
    "khi ERP hủy cắt lệnh cũ và đẩy xuống cắt lệnh mới có cùng mã đăng ký gốc.": 
        "khi ERP hủy đơn cắt lệnh cũ và đẩy xuống đơn cắt lệnh mới có cùng mã đăng ký gốc.",
    "người dùng chọn cắt lệnh mới trùng mã `ErpRegistrationCode` tại màn hình xe vào, hệ thống tìm lượt cân dở dang gần nhất (`PENDING_WEIGHT2` hoặc `ALLOCATION_PENDING` khi bị mồ côi dòng).": 
        "Nhân viên cân chọn đơn cắt lệnh mới trùng mã đăng ký ERP (`ErpRegistrationCode`) tại màn hình xe vào, hệ thống tự động tìm kiếm phiên cân dở dang gần nhất (`PENDING_WEIGHT2` hoặc `ALLOCATION_PENDING` khi bị mồ côi dòng).",
    "thời gian cân lần 1 của lượt cân cũ": "thời gian cân lần 1 của phiên cân cũ",
    "số cân lần 1 (`Weight1`) của lượt cân cũ": "số cân lần 1 (`Weight1`) của phiên cân cũ",
    "Nếu Operator đồng ý:": "Nếu Nhân viên cân đồng ý:",
    "Gắn cắt lệnh mới vào lượt cân cũ (`AppendCutOrdersToWeighingSessionUseCase`)": 
        "Gắn đơn cắt lệnh mới vào phiên cân cũ (`AppendCutOrdersToWeighingSessionUseCase`)",
    "dòng chi tiết mồ côi (các dòng cũ của đơn đã bị hủy).": 
        "dòng chi tiết mồ côi (các dòng cũ của đơn cắt lệnh đã bị hủy).",
    "Nếu Operator đồng ý, hệ thống tự động gán `ActualAllocatedWeight = NetWeight`, tính số bao `Round(NetWeight / 50)` (nếu hàng bao), và tự động gọi `AllocateWeighingSessionUseCase` để ghi nhận và chuyển trạng thái lượt cân sang `READY_TO_COMPLETE`.": 
        "Nếu Nhân viên cân đồng ý, hệ thống tự động gán `ActualAllocatedWeight = NetWeight`, tính số bao `Round(NetWeight / 50)` (nếu hàng bao), và tự động gọi `AllocateWeighingSessionUseCase` để ghi nhận và chuyển trạng thái phiên cân sang `READY_TO_COMPLETE`.",
    "Nếu Operator từ chối: Hệ thống tạo lượt cân hoàn toàn mới, đặt `Weight1 = null` và cân lại từ đầu.": 
        "Nếu Nhân viên cân từ chối: Hệ thống tạo phiên cân hoàn toàn mới, đặt `Weight1 = null` và cân lại từ đầu.",
    "tách tải thành 2 phiếu cân độc lập.": "tách tải thành 2 phiên cân con độc lập.",
    "khi lượt cân đã phân bổ đầy đủ:": "khi phiên cân đã phân bổ đầy đủ:",
    "trạng thái lượt cân thành `IsOverweight = true`": "trạng thái phiên cân thành `IsOverweight = true`",
    "yêu cầu tài khoản Admin phê duyệt hoặc Operator ghi nhận tùy cấu hình.": 
        "yêu cầu tài khoản Quản trị viên (Admin) phê duyệt hoặc Nhân viên cân (Operator) ghi nhận tùy cấu hình.",
    "giúp Operator thao tác nhanh": "giúp Nhân viên cân (Operator) thao tác nhanh",
    "để Operator dễ dàng đọc": "để Nhân viên cân (Operator) dễ dàng đọc",
    "B.1 Bảng `cut_orders` (Thông tin Cắt lệnh / Đăng ký từ ERP hoặc Tạo tay)": 
        "B.1 Bảng `cut_orders` (Thông tin Đơn cắt lệnh / Đăng ký từ ERP hoặc Tạo tay)",
    "Mã cắt lệnh từ ERP.": "Mã đơn cắt lệnh từ ERP.",
    "ID lượt cân active hiện tại.": "ID phiên cân đang hoạt động hiện tại.",
    "ID phiếu cân chính hiện tại.": "ID phiếu cân tổng hợp chính hiện tại.",
    "ID phiếu giao nhận chính hiện tại.": "ID phiếu giao nhận chính hiện tại.",
    "B.2 Bảng `weighing_sessions` (Lượt cân thực tế)": 
        "B.2 Bảng `weighing_sessions` (Phiên cân thực tế)",
    "Mã lượt cân (sinh tự động).": "Mã phiên cân (sinh tự động).",
    "B.3 Bảng `weighing_session_lines` (Dòng chi tiết của lượt cân)": 
        "B.3 Bảng `weighing_session_lines` (Dòng chi tiết của phiên cân)",
    "Liên kết tới `weighing_sessions`.": "Liên kết tới phiên cân `weighing_sessions`.",
    "Liên kết tới `cut_orders`.": "Liên kết tới đơn cắt lệnh `cut_orders`.",
    "B.4 Bảng `weigh_tickets` (Chứng từ Phiếu cân tổng cấp Session)": 
        "B.4 Bảng `weigh_tickets` (Chứng từ Phiếu cân tổng hợp cấp Phiên cân)",
    "B.5 Bảng `delivery_tickets` (Phiếu giao nhận cấp Line)": 
        "B.5 Bảng `delivery_tickets` (Phiếu giao nhận cấp Dòng chi tiết)",
    "C.1 Vòng đời của một Lượt cân (Weighing Session Lifecycle)": 
        "C.1 Vòng đời của một Phiên cân (Weighing Session Lifecycle)",
    "Tạo lượt cân từ Danh sách xe vào": "Tạo phiên cân từ Danh sách xe vào",
    "In đủ Phiếu cân tổng & tất cả PGN chi tiết": "In đủ Phiếu cân tổng hợp chính & tất cả phiếu giao nhận chi tiết",
    "Operator hủy lượt cân": "Nhân viên cân (Operator) hủy phiên cân",
    "Trả các Cut Orders về lại trạng thái chờ (IN_YARD)": "Trả các đơn cắt lệnh (Cut Orders) về lại trạng thái chờ (`IN_YARD`)",
    "C.2 Trạng thái của Cắt lệnh gốc (Cut Order Lifecycle)": 
        "C.2 Trạng thái của Đơn cắt lệnh gốc (Cut Order Lifecycle)",
    "Được gắn vào một lượt cân đang hoạt động (WEIGHING)": "Được gắn vào một phiên cân đang hoạt động (WEIGHING)",
    "Lượt cân chứa đơn hoàn tất cân lần 2 + in ấn (OUT_YARD)": "Phiên cân chứa đơn hoàn tất cân lần 2 + in ấn (OUT_YARD)",
    "Bấm chốt sản lượng (Finalize) đơn hàng lớn": "Bấm chốt sản lượng (Finalize) đơn cắt lệnh lớn",
    "Lượt cân bị hủy (trả về hàng đợi)": "Phiên cân bị hủy (trả về hàng đợi)",
    "Quản lý xe chờ cân cục bộ": "Quản lý xe chờ cân cục bộ",
    "Đo lường cân tự động": "Đo lường cân tự động",
    "Phân bổ sản lượng nhiều đơn": "Phân bổ khối lượng nhiều đơn",
    "Vận hành offline khi mất mạng": "Vận hành ngoại tuyến khi mất mạng",
    "Xử lý đơn hàng lớn xuất khẩu": "Xử lý đơn cắt lệnh lớn xuất khẩu",
    "Xử lý CO lại của ERP": "Xử lý cấp lại đơn cắt lệnh của ERP",
    "Tự động sao lưu dữ liệu local": "Tự động sao lưu dữ liệu cục bộ",
}

for old, new in replacements.items():
    content = content.replace(old, new)

# Let's perform additional cleanup for any leftover instances of 'lượt cân' or 'Lượt cân' outside structural code
# we will replace 'lượt cân' -> 'phiên cân' and 'Lượt cân' -> 'Phiên cân' in natural text while avoiding code properties
# However, to be safe, let's do a few explicit text block replacements
content = content.replace("hủy lượt cân", "hủy phiên cân")
content = content.replace("Hủy lượt cân", "Hủy phiên cân")
content = content.replace("tải lượt cân", "tải phiên cân")
content = content.replace("Tải lượt cân", "Tải phiên cân")
content = content.replace("lượt cân dở dang", "phiên cân dở dang")
content = content.replace("Lượt cân dở dang", "Phiên cân dở dang")
content = content.replace("lượt cân dở dang", "phiên cân dở dang")
content = content.replace("lượt cân active", "phiên cân active")
content = content.replace("lượt cân cũ", "phiên cân cũ")
content = content.replace("lượt cân mới", "phiên cân mới")
content = content.replace("lượt cân con", "phiên cân con")
content = content.replace("lượt cân con", "phiên cân con")
content = content.replace("lượt cân tổng", "phiên cân tổng")
content = content.replace("các lượt cân", "các phiên cân")
content = content.replace("mỗi lượt cân", "mỗi phiên cân")
content = content.replace("những lượt cân", "những phiên cân")
content = content.replace("một lượt cân", "một phiên cân")
content = content.replace("1 lượt cân", "1 phiên cân")
content = content.replace("tạo lượt cân", "tạo phiên cân")
content = content.replace("Tạo lượt cân", "Tạo phiên cân")
content = content.replace("hoàn tất lượt cân", "hoàn tất phiên cân")
content = content.replace("Hoàn tất lượt cân", "Hoàn tất phiên cân")
content = content.replace("của lượt cân", "của phiên cân")
content = content.replace("trong lượt cân", "trong phiên cân")
content = content.replace("chờ lượt cân", "chờ phiên cân")

with open(srs_path, 'w', encoding='utf-8') as f:
    f.write(content)

print("SRS localization and polishing completed successfully!")
