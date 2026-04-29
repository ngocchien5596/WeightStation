# KẾ HOẠCH: BỔ SUNG YÊU CẦU TOAST NOTIFICATION CHO MÀN LẬP PHIẾU CÂN (WEIGHTVIEW)

## 1. MỤC TIÊU
Màn WeightView phải có hệ thống toast notification rõ ràng, nhất quán, dễ hiểu cho operator.
Sau mỗi thao tác nút, người dùng phải biết:
- Thao tác thành công hay thất bại.
- Vì sao bị chặn (nếu có).
- Bước tiếp theo là gì nếu cần.

**Lưu ý:** Không được chỉ im lặng disable hoặc fail mà không có phản hồi rõ.

---

## 2. NGUYÊN TẮC CHUNG

### A. Toast UI (Tùy chỉnh nhẹ & Không phá MVVM)
- **Tự thiết kế:** Do project hiện không sử dụng MaterialDesignThemes hay thư viện UI ngoài, ta sẽ KHÔNG thêm thư viện mới chỉ để làm toast.
- **Thiết kế:** Tạo một toast control/window nhỏ, nhẹ, hiển thị ở góc màn hình (cụ thể: góc trên bên phải), không che vùng thao tác chính.
- **Kiến trúc:** Triển khai thông qua `IToastService` để có thể gọi từ ViewModel mà không vi phạm nguyên tắc MVVM.

### B. Mức độ Toast
Hỗ trợ tối thiểu 4 mức độ:
- **Success** (Thành công)
- **Warning** (Cảnh báo)
- **Error** (Lỗi)
- **Info** (Thông tin)

### C. Chiến lược Toast trùng lặp (Chống Spam)
- **Không stack vô hạn:** Tránh việc toast xếp chồng kín màn hình.
- **Replace/Coalesce:** 
  - Nếu cùng thông báo hoặc cùng loại lỗi xuất hiện liên tiếp trong thời gian ngắn, toast mới sẽ thay thế (replace) toast cũ hoặc reset thời gian hiển thị.
  - Nếu là các toast có nội dung khác nhau thực sự, chỉ cho phép xếp hàng chờ (queue) hoặc stack ngắn với giới hạn cực thấp (VD: max 3 toast cùng lúc).

### D. Toast không được làm mất selection/focus
- Hiển thị toast nhưng không reset selected row.
- Không làm clear detail.
- Không chặn tiếp thao tác bằng modal trừ những case bắt buộc cần confirm.

### E. Toast tự động ẩn
- Success / Info: tự tắt sau 2–4 giây.
- Warning / Error: tự tắt sau 4–6 giây.

---

## 3. CÁC TRƯỜNG HỢP BẮT BUỘC PHẢI CÓ TOAST

### A. Chưa chọn bản ghi mà bấm nút thao tác
Nếu bấm các nút: Giao nhận, In phiếu, Lưu, Cân lần 1, Cân lần 2, Hủy, Phiếu liên quan mà chưa chọn bản ghi:
- **Warning:** “Vui lòng chọn một phiếu để thao tác.”

### B. Nút bị chặn do trạng thái không hợp lệ
Ví dụ: chưa cân 1 mà bấm cân 2, record đã hoàn thành/hủy mà vẫn thao tác.
- **Warning:** “Phiếu chưa cân lần 1, chưa thể thực hiện cân lần 2.”
- **Warning:** “Phiếu đã hoàn thành, không thể chỉnh sửa thêm.”
- **Warning:** “Phiếu đã hủy, không thể tiếp tục thao tác.”

### C. Cân lần 1 thành công
- **Success:** “Đã ghi nhận cân lần 1 thành công.”
- Nếu tạo luôn phiếu: “Đã ghi nhận cân lần 1 và tạo phiếu cân thành công.”

### D. Cân lần 1 thất bại
- **Error:** “Không thể thực hiện cân lần 1. Vui lòng kiểm tra lại dữ liệu hoặc thiết bị cân.”
- Manual mode thiếu DL: “Vui lòng nhập số cân lần 1 hợp lệ.”
- Auto mode thiếu DL: “Chưa có dữ liệu cân hợp lệ để thực hiện cân lần 1.”

### E. Cân lần 2 thành công
- **Success:** “Đã ghi nhận cân lần 2 thành công.”
- Nếu hoàn thành: “Đã ghi nhận cân lần 2 và hoàn tất phiếu.”
- Nếu quá tải: **Warning/Error:** “Phiếu phát sinh quá tải. Vui lòng xử lý theo hướng dẫn.”

### F. Cân lần 2 thất bại
- **Error:** “Không thể thực hiện cân lần 2. Vui lòng kiểm tra lại dữ liệu hoặc thiết bị cân.”
- Manual mode thiếu DL: “Vui lòng nhập số cân lần 2 hợp lệ.”
- Auto mode thiếu DL: “Chưa có dữ liệu cân hợp lệ để thực hiện cân lần 2.”

### G. Lưu thành công
- **Success:** “Đã lưu thay đổi thành công.”

### H. Lưu thất bại
- **Error:** “Không thể lưu thay đổi. Vui lòng kiểm tra lại dữ liệu.”
- Nếu không có thay đổi hợp lệ: **Warning:** “Không có thay đổi hợp lệ để lưu.”

### I. Giao nhận
- Mở form: **Info:** “Đang mở phiếu giao nhận.”
- Lưu thành công: **Success:** “Đã cập nhật phiếu giao nhận thành công.”
- Chưa có phiếu: **Warning:** “Phiếu chưa có thông tin giao nhận.”

### J. In phiếu thành công
- **Success:** “Đã in phiếu thành công.”

### K. In phiếu thất bại
- **Error:** “Không thể in phiếu. Vui lòng kiểm tra máy in.”
- Chưa có phiếu cân: **Warning:** “Chưa có phiếu cân để in.”

### L. Hủy thành công
- **Success:** “Đã hủy phiếu thành công.”

### M. Hủy thất bại
- **Error:** “Không thể hủy phiếu. Vui lòng thử lại.”
- Không cho hủy: **Warning:** “Phiếu ở trạng thái hiện tại không thể hủy.”

### N. Phiếu liên quan
- Mở form: **Info:** “Đang mở danh sách phiếu liên quan.”
- Không có chứng từ: **Warning:** “Phiếu hiện chưa có chứng từ liên quan.”

### O. Tìm kiếm
- Lỗi tìm kiếm: **Error:** “Không thể tải dữ liệu tìm kiếm. Vui lòng thử lại.”
- Không có dữ liệu: **Info:** “Không tìm thấy dữ liệu phù hợp.”

### P. Clear selection / no selection state
- Không cần toast nhưng UI detail phải clear đúng.

### Q. Thiết bị cân chưa sẵn sàng
Khi bấm Cân (auto) mà thiết bị mất kết nối hoặc không có dữ liệu:
- **Warning/Error:** “Thiết bị cân chưa sẵn sàng. Vui lòng kiểm tra kết nối cân.” hoặc “Chưa nhận được dữ liệu cân hợp lệ từ thiết bị.”

---

## 4. TOAST MESSAGE PHẢI NHẤT QUÁN VỚI STATE
- Không được nói mâu thuẫn với tình trạng UI.
- Nêu đúng lý do bị chặn nếu nút đáng lẽ bị disable nhưng vẫn được gọi command.
- Map đúng theo: `selected record`, `registration_status`, `auto/manual mode`, `child docs`, `device reading`.

---

## 5. TOAST SERVICE THIẾT KẾ
Sẽ tạo mới một Interface `IToastService` (không phá vỡ MVVM):
```csharp
public interface IToastService
{
    void ShowSuccess(string message);
    void ShowWarning(string message);
    void ShowError(string message);
    void ShowInfo(string message);
}
```
- Phải inject được vào ViewModel.
- Triển khai `WpfToastService` có chứa logic debounce / coalesce để replace các message trùng lặp trong thời gian ngắn và quản lý số lượng toast tối đa hiển thị cùng lúc.
- Thiết kế Control/Window popup góc trên bên phải màn hình với animation mượt mà.

---

## 6. PHẦN BỔ SUNG YÊU CẦU OUTPUT KHI THỰC HIỆN

### E. TOAST DESIGN
- Sẽ liệt kê danh sách các case, message, level và toast service class được dùng.

### F. IMPLEMENTATION – TOAST
- Sẽ report các file/service được tạo/sửa.
- Liệt kê các command đã gắn logic toast.

### G. TEST NOTES – TOAST
- Sẽ xác nhận kết quả test cho các case đã yêu cầu (Chưa chọn bản ghi, cân 1, cân 2, lưu, in, hủy, phiếu liên quan, trạng thái thiết bị cân, spam click).
