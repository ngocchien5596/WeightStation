# KẾ HOẠCH CHI TIẾT CHO CHỨC NĂNG HỦY PHIẾU
Áp dụng cho màn Lập phiếu cân / WeightView của StationApp

## A. REVIEW HIỆN TRẠNG
- **Nút Hủy hiện đang làm gì:** Cần rà soát code hiện tại xem nút Hủy đang thực hiện hành động gì (có thể đang dùng sai mục đích như clear form, reset dữ liệu tạm). Ý nghĩa duy nhất từ nay là HỦY PHIẾU NGHIỆP VỤ.
- **Child docs hiện có field delete/cancel chưa:** Các bảng `weigh_tickets` và `delivery_tickets` hiện chưa có field soft delete hoặc cần rà soát lại để chắc chắn.
- **Query nào sẽ bị ảnh hưởng:** Các query dùng cho hiển thị detail phiếu cân, in phiếu cân, related docs, và projection của grid chính (WeightView).

## B. SCHEMA UPDATE DESIGN
- **weigh_tickets:**
  - Bắt buộc thêm: `IsDeleted` (bit, NOT NULL, DEFAULT 0)
  - Khuyến nghị thêm: `DeletedAt` (datetime2, NULL), `DeletedBy` (nvarchar(100), NULL)
- **delivery_tickets:**
  - Bắt buộc thêm: `IsDeleted` (bit, NOT NULL, DEFAULT 0)
  - Khuyến nghị thêm: `DeletedAt` (datetime2, NULL), `DeletedBy` (nvarchar(100), NULL)
- **Lý do thêm:** Để thực hiện soft delete cho TẤT CẢ phiếu cân và phiếu giao nhận liên quan khi hủy 1 registration, đảm bảo dữ liệu không bị xóa vật lý nhưng không còn xuất hiện trong các luồng vận hành bình thường.

## C. BUSINESS FLOW DESIGN
- **Khi nào cho phép hủy:** 
  - `RegistrationStatus = REGISTERED` (chưa hoàn tất, được phép hủy)
  - `RegistrationStatus = LOADING_IN_PROGRESS` (đã cân lần 1 nhưng chưa hoàn tất, vẫn được phép hủy)
- **Khi nào không cho hủy:** 
  - `RegistrationStatus = COMPLETED` (đã hoàn tất chứng từ, không nên hủy bằng flow này)
  - `RegistrationStatus = CANCELLED` (đã hủy rồi, không hủy lại)
- **Root update ra sao:** 
  - Cập nhật bảng `vehicle_registrations`: `RegistrationStatus = CANCELLED`, `IsCancelled = true`, `UpdatedAt = clockService.NowLocal`, `UpdatedBy = currentUser`.
- **Child docs update ra sao:** 
  - Lấy theo `VehicleRegistrationId`, cập nhật TẤT CẢ `weigh_tickets` và `delivery_tickets` liên quan (kể cả phiếu chính, phiếu phụ của luồng quá tải).
  - Cập nhật: `IsDeleted = true`, `DeletedAt = clockService.NowLocal`, `DeletedBy = currentUser`, `UpdatedAt = clockService.NowLocal`, `UpdatedBy = currentUser`.
  - Toàn bộ flow (update root + update child docs) PHẢI nằm trong 1 Transaction.

## D. UI/STATE DESIGN
- **Confirm modal:**
  - Tiêu đề: "Xác nhận hủy phiếu"
  - Nội dung: "Bạn có chắc muốn hủy phiếu này không? Tất cả phiếu cân và phiếu giao nhận liên quan sẽ bị xóa."
  - Nút: "Hủy phiếu" (danger), "Không" (Chỉ thực hiện khi xác nhận, không dùng MessageBox mặc định).
- **Button state trước/sau hủy:** 
  - Chưa chọn phiếu: Disable (hoặc Toast warning "Vui lòng chọn một phiếu để thao tác.").
  - COMPLETED/CANCELLED: Disable (hoặc Toast warning "Phiếu đã hoàn thành, không thể hủy." / "Phiếu đã hủy.").
  - Sau khi hủy thành công: Tất cả các nút LƯU, CÂN LẦN 1, CÂN LẦN 2, GIAO NHẬN, IN PC, IN PGN, HỦY, PHIẾU LIÊN QUAN đều **disable**.
- **Selection behavior:** Sau khi hủy thành công, vẫn giữ selected row của registration vừa hủy trên grid.
- **Grid behavior:** Cột Tình trạng hiển thị "Đã hủy", không xóa dòng khỏi grid ngay.

## E. IMPLEMENTATION
- **File tree các file tạo/sửa:**
  - `WeighingViewModel.cs`: Cập nhật command Cancel, button states, modal confirm, toast integration.
  - Entities: Cập nhật `WeighTicket.cs`, `DeliveryTicket.cs` thêm thuộc tính `IsDeleted`, `DeletedAt`, `DeletedBy`.
  - Data / Migrations: Tạo migration để thêm cột.
  - Repositories: Cập nhật `IVehicleRegistrationRepository`, `IWeighTicketRepository`, `IDeliveryTicketRepository` để hỗ trợ load toàn bộ child docs và update. Thêm filter loại bỏ `IsDeleted = true`.
  - Use Cases: Tạo/Sửa Use Case cho chức năng CancelRegistration (đảm bảo logic transaction và update root + child).
- **Migration:** Lệnh EF Core add migration thêm trường soft delete.
- **Entity/config update:** Cập nhật Entity Framework configurations.
- **Query filter changes:** Mặc định lọc bỏ `IsDeleted = true` trong grid projection, liên kết in ấn, và related docs.

## F. TEST NOTES
- **Case REGISTERED:** Hủy thành công, đúng status, có modal, toast báo thành công.
- **Case LOADING_IN_PROGRESS:** Hủy thành công, đúng status, child docs update đủ.
- **Case COMPLETED:** Không cho phép hủy (nút disable hoặc báo lỗi).
- **Case CANCELLED:** Không cho phép hủy lại.
- **Case split overweight:** Kiểm tra hủy đủ cả phiếu chính và phiếu phụ (tổng cộng có thể là 4 child docs), không bị sót.
- **Case selection persistence:** Hủy xong dòng trên grid không mất, vẫn focus, chuyển tình trạng "Đã hủy".
- **Case print blocked after cancel:** Thử in sau khi hủy -> Bị chặn, hiện warning. Không thể gọi lệnh in từ phím tắt.
