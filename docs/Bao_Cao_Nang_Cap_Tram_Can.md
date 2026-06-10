# BÁO CÁO KHẢO SÁT HIỆN TRẠNG

# ĐỀ XUẤT PHƯƠNG ÁN NÂNG CẤP PHẦN MỀM TRẠM CÂN

## 1. Tóm tắt dự án
Hoạt động cân đo khối lượng hàng hóa xuất - nhập khẩu đóng vai trò huyết mạch trong chuỗi cung ứng và quản lý tài sản của nhà máy. Hiện nay, hệ thống phần mềm cũ tại trạm cân đã bộc lộ nhiều điểm hạn chế về cả giao diện vận hành, tính pháp lý và khả năng tích hợp.

Dự án **phần mềm cân** được đề xuất nhằm thay thế toàn diện phần mềm cũ bằng một giải pháp chạy trên nền tảng Windows Desktop hiện đại (WPF/C# .NET 8), hoạt động theo cơ chế **Local-First** (Ưu tiên ngoại tuyến), tích hợp camera giám sát, lưu trữ dữ liệu master data cục bộ phục vụ nhập liệu nhanh, kiểm soát dung sai cho phép, xử lý quá tải xe, và đồng bộ hóa dữ liệu với hệ thống ERP trung tâm.

---

## 2. Phân tích hiện trạng hệ thống phần mềm cũ

Qua quá trình khảo sát thực tế và ghi nhận phản hồi từ đội ngũ nhân viên trạm cân, hệ thống cũ đang gặp phải những hạn chế sau:

### 2.1. Trải nghiệm vận hành
* **Giao diện người dùng (UI/UX) không thân thiện**: Giao diện lỗi thời, không thân thiện với người dùng.
* **Nhập liệu thủ công**: Không có danh mục dữ liệu master data cục bộ. Mỗi khi có xe vào cân, nhân viên phải nhập lại các thông tin: Biển số xe, mooc, tên tài xế. Điều này làm tăng nguy cơ sai lệch dữ liệu do gõ nhầm ký tự.

### 2.2. Hạn chế về tích hợp, nâng cấp
* **Sự phụ thuộc vào bên thứ ba**: Phần mềm do bên thứ ba cung cấp được đóng gói khép kín, không bàn giao mã nguồn. Đơn vị vận hành hoàn toàn không thể can thiệp sửa lỗi hay nâng cấp tính năng.
* **Tách biệt dữ liệu**: Không có khả năng kết nối hay đồng bộ hóa tự động dữ liệu với hệ thống ERP trung tâm hoặc các phần mềm quản lý khác, đòi hỏi phải đối soát thủ công bằng các file Excel vào cuối ngày.

### 2.3. Thiếu kiểm soát thất thoát, rủi ro pháp lý
* **Không kiểm soát dung sai hàng bao**: Hệ thống cũ không tự động tính toán số bao thực tế và đối chiếu dung sai khối lượng cho phép. Việc kiểm tra tính toán trọng lượng hàng có vượt quá dung sai cho phép đang thực hiện thủ công, có thể nhầm lẫn gây thất thoát tài sản.
* **Chưa lưu trữ thông tin đăng kiểm và tải trọng cho phép (TTCP)**: Không theo dõi thời hạn hiệu lực đăng kiểm phương tiện và tải trọng giới hạn của xe. Dễ để lọt xe quá hạn đăng kiểm hoặc quá tải trọng quy định lưu thông ra đường, tiềm ẩn rủi ro bị xử phạt hành chính lớn.

### 2.4. Hạn chế nghiệp vụ & khả năng mở rộng
* **Giới hạn kết nối**: Chỉ cho phép duy nhất một máy tính kết nối trực tiếp với đầu cân/phần mềm cũ.
* **Chưa đáp ứng được luồng cân phức tạp**:
  * Không phản ánh được trường hợp một phương tiện vào cân để lấy cùng lúc nhiều loại sản phẩm khác nhau.
  * Không hỗ trợ trường hợp xe lấy hàng phân bổ cho nhiều khách hàng hoặc nhà phân phối (NPP) khác nhau trong cùng một lượt cân (phải thực hiện cân và chạy vòng nhiều lần).

---

## 3. Tính ưu việt vượt trội của hệ thống phần mềm mới

Phần mềm cân mới được thiết kế và xây dựng để giải quyết triệt để tất cả các tồn tại của hệ thống cũ thông qua các cải tiến vượt trội sau:

* **Tối ưu hóa trải nghiệm vận hành**:
  * **Giao diện hiện đại & thân thiện**: Phát triển trên nền tảng WPF hiện đại, chữ số hiển thị kích thước lớn rõ ràng, giảm thời gian xử lý xuống dưới 1.5 phút/xe.
  * **Nhập liệu nhanh qua dữ liệu master data**: Tích hợp danh mục dữ liệu master data cục bộ (phương tiện, mooc, tài xế). Khi xe vào cân, hệ thống tự động gợi ý và điền nhanh thông tin giúp giảm thao tác và triệt tiêu lỗi gõ nhầm ký tự.

* **Nâng cao khả năng tích hợp & vận hành liên tục**:
  * **Đồng bộ hóa tự động với ERP trung tâm**: Kết nối API hai chiều tự động để lấy thông tin đơn hàng được duyệt (Cut Order) và tự động trả dữ liệu kết quả cân về ERP, loại bỏ đối soát thủ công cuối ngày.
  * **Kiến trúc local-first**: Việc sử dụng database local cho phép trạm cân vận hành độc lập, cân xe và in phiếu bình thường ngay cả khi mất kết nối mạng Internet.
  * **Kiến trúc đa kết nối**: Khắc phục giới hạn đơn lập (chỉ 1 máy tính kết nối của phần mềm cũ), hệ thống mới hỗ trợ nhiều máy trạm kết nối đồng thời, phân quyền sử dụng rõ ràng.

* **Kiểm soát chặt chẽ thất thoát & pháp lý**:
  * **Kiểm soát dung sai hàng bao tự động**: Tự động tính toán số bao thực tế và đối chiếu dung sai khối lượng cho phép. Hiển thị cảnh báo trực quan nếu phát hiện khối lượng vượt quá dung sai cho phép, ngăn ngừa thất thoát tài sản nhà máy từ việc tính toán thủ công trước đây.
  * **Lưu trữ thông tin đăng kiểm và tải trọng cho phép (TTCP)**: Tự động quản lý thông tin tải trọng đăng kiểm của phương tiện. Hệ thống cảnh báo đối với các xe quá hạn đăng kiểm hoặc quá tải trọng quy định, cung cấp quy trình tách lượt cân thành các phiếu con hợp lệ để đảm bảo tuân thủ Luật giao thông.
  * **Tích hợp camera giám sát**: Hiển thị trực tiếp luồng video cầu cân để kiểm tra đỗ xe đúng vị trí. Tự động chụp ảnh biển số trước/sau khi lưu cân lần 1 và lần 2, lưu trữ hình ảnh vào lịch sử lượt cân làm bằng chứng đối chiếu.

* **Hỗ trợ nghiệp vụ cân phức tạp**:
  * **Mô hình lượt cân**: Hỗ trợ một phương tiện lấy cùng lúc nhiều loại sản phẩm hoặc lấy hàng phân bổ cho nhiều khách hàng/NPP khác nhau trong cùng 1 lượt cân xe vật lý. Hệ thống hỗ trợ phân bổ khối lượng tịnh thực tế trực quan.
  * **Luồng cân xuất khẩu chuyên dụng**: Cho phép tạo lượt cân xuất khẩu tổng cho đơn hàng lớn (theo tàu/hợp đồng), hỗ trợ tạo nhiều lượt cân con độc lập cho các xe khác nhau, tự động cộng dồn sản lượng lũy kế thời gian thực và chốt sản lượng cuối cùng.
  * **Kế thừa lượt cân trong 24 giờ**: Khi có sự thay đổi thông tin đơn hàng, phải RA cắt lệnh và sửa thông tin đơn hàng, khi đó ERP sẽ cấp lại cắt lệnh mới đẩy xuống cân, phần mềm cho phép nhân viên cân gắn cắt lệnh mới vào lượt cân cũ để kế thừa dữ liệu cân lần 1 (đối với luồng cân nội địa) và dữ liệu chuyến xe (đối với luồng cân xuất khẩu), tránh mất dữ liệu.
* **Quản trị**:
  * **Cấu hình in ấn động**: Cho phép người dùng tự cấu hình máy in mặc định cho từng loại phiếu và vị trí in trực tiếp trên phần mềm cho từng loại chứng từ mà không cần sửa đổi mã nguồn.
  * **Tự động sao lưu dữ liệu cục bộ**: Dịch vụ chạy ngầm tự động backup SQL Server lúc 1:00 AM hàng ngày, lưu trữ 10 ngày phục vụ khôi phục nhanh, tránh nguy cơ mất trắng dữ liệu khi xảy ra sự cố phần cứng.
  * **Tự động đồng bộ dữ liệu lên server**: Khi mạng phục hồi, dữ liệu trong hàng đợi đồng bộ dữ liệu sẽ tự động đồng bộ lên Server trung tâm, đảm bảo dữ liệu được lưu trữ lên bộ nhớ thứ 2 ổn định đề phòng sự cố phần cứng.

---

## 4. Đề xuất phương án & lộ trình triển khai

Lộ trình triển khai dự án nâng cấp phần mềm trạm cân được chia làm 4 giai đoạn cơ bản của quy trình phát triển phần mềm:

| Giai đoạn | Thời gian | Nội dung công việc chính |
| :--- | :--- | :--- |
| **Giai đoạn 1**: Khảo sát & Phân tích yêu cầu | Từ 01/05/2026 đến 20/05/2026 | Khảo sát hạ tầng thiết bị, phần mềm cân cũ, luồng quy trình cân nội địa và cân xuất khẩu, thống nhất phương án kết nối dữ liệu với hệ thống ERP trung tâm và phê duyệt tài liệu đặc tả yêu cầu phần mềm. |
| **Giai đoạn 2**: Thiết kế & Phát triển hệ thống | Từ 21/05/2026 đến 10/06/2026 | Thiết kế kiến trúc hệ thống, phát triển các chức năng phần mềm, tích hợp thiết bị ngoại vi và xây dựng cơ chế đồng bộ dữ liệu. |
| **Giai đoạn 3**: Kiểm thử & Triển khai thử nghiệm | Từ 11/06/2026 đến 30/06/2026 | Thực hiện kiểm thử tích hợp, cài đặt vận hành thử nghiệm song song tại một trạm cân và đào tạo hướng dẫn sử dụng cho nhân viên vận hành. |
| **Giai đoạn 4**: Triển khai diện rộng & Bàn giao | Từ 01/07/2026 đến 31/07/2026 | Triển khai cài đặt đồng loạt cho tất cả các trạm cân còn lại, chuyển giao hoạt động chính thức sang hệ thống mới, hỗ trợ vận hành. |


