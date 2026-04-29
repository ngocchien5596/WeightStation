# Điều chỉnh lại state model và flow quá tải như sau:

1. Khi phiếu đang LOADING_IN_PROGRESS, người dùng đã có dữ liệu Cân lần 2 và bấm LƯU:
   - hệ thống tính net_weight
   - hệ thống tính TTCP 10%
   - nếu net_weight <= TTCP 10%:
     - lưu bình thường
     - registration_status = COMPLETED

2. Nếu net_weight > TTCP 10%:
   - hiện modal “Cảnh báo quá tải”
   - nội dung:
     “Trọng lượng hàng vượt TTCP 10%. Bạn có muốn tách phiếu cân không?”

3. Logic nút của modal:
   - Tách phiếu:
     - thực hiện nghiệp vụ tách phiếu quá tải
     - cập nhật phiếu cân hiện tại thành phiếu 1
     - tạo phiếu cân 2
     - cập nhật phiếu giao nhận hiện tại thành phiếu giao nhận 1
     - tạo phiếu giao nhận 2
     - cập nhật các field split liên quan
     - set HasOverweightCase = true
     - registration_status = COMPLETED

   - Lưu không tách:
     - KHÔNG tách phiếu
     - vẫn cho phép lưu dữ liệu cân lần 2
     - vẫn set HasOverweightCase = true
     - cập nhật phiếu cân / phiếu giao nhận hiện tại theo dữ liệu cân lần 2
     - registration_status = COMPLETED

4. Bỏ hoàn toàn status OVERWEIGHT_PENDING_ACTION khỏi flow này.
5. Update lại:
   - state machine
   - button state matrix
   - toast messages
   - related docs logic
   - field flags IsOverWeight / HasOverweightCase theo logic mới
6. Nếu user chọn No, phiếu vẫn hoàn thành bình thường nhưng được ghi nhận là case quá tải không tách phiếu.

7. Edge Cases xử lý bổ sung:
   - Nếu phiếu trước đó đã chọn 'Lưu không tách' (HasOverweightCase = true), sau này mở lại sửa số cân và lần lưu mới nhất không còn vượt TTCP 10%:
     * Phải gỡ HasOverweightCase về false.
     * HasOverweightCase phản ánh trạng thái hiện hành cuối cùng của phiếu, không phải lịch sử.
     * Nếu cần lưu lịch sử “đã từng quá tải”, dùng audit log / event log, không dùng HasOverweightCase.
   - Với case 'Lưu không tách':
     * KHÔNG hiển thị bất kỳ cảnh báo / ghi chú / watermark nào trên phiếu cân và phiếu giao nhận khi in.
     * Phiếu vẫn in ra như bình thường.
     * HasOverweightCase chỉ dùng cho logic nội bộ, UI nội bộ, filter hoặc audit, không đưa lên bản in.

