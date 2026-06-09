# HƯỚNG DẪN SỬ DỤNG PHẦN MỀM TRẠM CÂN (STATIONAPP)

Tài liệu này hướng dẫn nhân viên vận hành trạm cân (Operator) và quản trị viên (Admin) thực hiện các nghiệp vụ cân xe và xử lý sự cố.

---

# PHẦN 1. GIAO DIỆN PHẦN MỀM

Ứng dụng quản lý trạm cân có các màn hình và khu vực chính sau:

## 1.1 Bảng tra cứu nhanh các màn hình và khu vực trên giao diện chính

| Tên màn hình / Khu vực | Dùng để làm gì? | Khi nào sử dụng? | Lưu ý khi thao tác |
| :--- | :--- | :--- | :--- |
| **Menu điều hướng chính (Bên trái)** | Chuyển đổi giữa các màn hình: **Trang chủ**, **Danh sách xe vào**, **Cân nội địa**, **Cân xuất khẩu**, **Danh sách xe ra**, **Báo cáo**, **Danh sách phiếu**, **Diagnostics**, **Cấu hình hệ thống**. | Khi cần thực hiện nghiệp vụ khác nhau hoặc xem báo cáo, cấu hình. | Bấm chuột trái vào tên màn hình để mở. |
| **Danh sách xe vào (IncomingVehicleListView)** | Hiển thị các phương tiện đã đăng ký từ ERP hoặc tạo thủ công đang xếp hàng ở cổng trạm chờ cân lần 1. | Khi xe mới vào trạm và cần bắt đầu lượt cân. | Có thể tích chọn cột **CHỌN** của nhiều dòng cùng biển số để thực hiện gộp chuyến cân xe. |
| **Form thông tin xe (Phía trên lưới)** | Sửa thông tin xe: **Mã cắt lệnh**, **Số PTVC**, **Mooc**, **HTVC**, **Mã KH**, **Khách hàng**, **Mã SP**, **Sản phẩm**, **Số ĐK Xe**, **Số ĐK Mooc**, **TTCP (kg)**, **Hạn ĐK Xe**, **Hạn ĐK Mooc**, **Tên tài xế**, **Loại**, **SL đặt**, **Số bao**, **Ghi chú**. | Khi xe chờ ở bãi vào cần bổ sung, sửa đổi thông tin đăng ký phương tiện hoặc tạo đơn hàng nhập thủ công. | Sử dụng tính năng tự động gợi ý (Autocomplete) khi nhập biển số, tài xế, sản phẩm để điền nhanh dữ liệu. |
| **Cân nội địa (WeighingView)** | Thực hiện cân lần 1, cân lần 2, gộp thêm cắt lệnh, phân bổ và hoàn tất xe ra. | Khi xe đang dừng đỗ trực tiếp trên bàn cân nội địa. | Số cân chỉ hiển thị trạng thái nút lấy cân khi chữ hiển thị báo màu sắc ổn định (**ỔN ĐỊNH**). |
| **Cân xuất khẩu (ExportWeighingView)** | Quản lý cân lũy kế nhiều chuyến xe con của đơn hàng lớn xuống tàu/sà lan. | Khi thực hiện cân xuất khẩu cho hợp đồng lớn chạy liên tục nhiều chuyến. | Cho phép tạo cắt lệnh tạm, tạo chuyến xe con, chuyển chuyến và chốt tổng. |
| **Danh sách xe ra (OutgoingVehicleListView)** | Quản lý các lượt xe đã cân xong, thực hiện xem chi tiết phân bổ, xem ảnh camera lịch sử, in lại chứng từ. | Khi cần in lại phiếu cân (PC)/phiếu giao nhận (PGN) hoặc tra cứu thông tin xe đã cân xong. | Có thể check chọn **K LẤY ĐỦ SỐ LƯỢNG** để bỏ qua cảnh báo dung sai hàng bao sau khi cân. |

## 1.2 Bảng tra cứu trạng thái hiển thị trên giao diện

Hệ thống sử dụng các trạng thái hiển thị bằng tiếng Việt chuẩn hóa trên giao diện để nhân viên trạm cân nhận biết và xử lý:

### 1.2.1 Trạng thái Lượt cân
Hiển thị trên lưới **DANH SÁCH LƯỢT CÂN HOẠT ĐỘNG** của màn hình **Cân nội địa**, lưới chuyến xe của **Cân xuất khẩu** và lưới **Danh sách xe ra**:

| Tên trạng thái trên giao diện | Ý nghĩa nghiệp vụ | Hành động của nhân viên trạm cân |
| :--- | :--- | :--- |
| **Chờ cân lần 1** | Xe mới tạo lượt cân thành công và đang trên bàn cân chờ ghi nhận khối lượng lần đầu. | Yêu cầu xe đỗ đúng tâm cân, tắt máy và bấm **CÂN LẦN 1**. |
| **Chờ cân lần 2** | Xe đã lưu số cân lần 1, đang di chuyển vào kho để lấy hàng hoặc trút hàng. | Chờ xe lấy hàng xong quay lại bàn cân để tiến hành cân lần 2. |
| **Chờ phân bổ** | Xe đã cân lần 2 xong nhưng chưa phân chia khối lượng tịnh thực tế cho các đơn hàng gộp. | Bắt buộc chọn lượt cân và bấm nút **PHÂN BỔ** để chia sản lượng thực tế. |
| **Sẵn sàng hoàn tất** | Lượt cân đã được phân bổ xong khối lượng tịnh cho tất cả các đơn hàng gộp chi tiết. | Kiểm tra thông tin, bấm **IN PC** / **IN PGN** và bấm **CHUYỂN XE RA**. |
| **Đã hoàn tất** | Lượt cân đã hoàn tất hoàn toàn, xe đã đi ra khỏi trạm cân và dữ liệu được khóa gửi lên ERP. | Trạng thái lưu trữ. Có thể in lại phiếu tại màn hình **Danh sách xe ra**. |
| **Đã hủy** | Lượt cân bị hủy bỏ do sai biển số xe, sai sản phẩm hoặc tài xế hủy đơn. | Trạng thái lưu trữ của các lượt cân đã bị hủy (hủy bỏ dữ liệu cân). |

### 1.2.2 Trạng thái Đơn cắt lệnh
Hiển thị trên cột **TRẠNG THÁI** của lưới **Danh sách xe vào** và lưới **DANH SÁCH CẮT LỆNH XUẤT KHẨU**:

| Tên trạng thái trên giao diện | Ý nghĩa nghiệp vụ | Hành động của nhân viên trạm cân |
| :--- | :--- | :--- |
| **Đã đăng ký** | Cắt lệnh gốc được đồng bộ từ ERP xuống hoặc được tạo thủ công tại trạm cân đang ở bãi chờ cân. | Chọn cắt lệnh để tạo lượt cân hoặc chuyển luồng cân xuất khẩu. |
| **Đang trong lượt cân** | Cắt lệnh đã được gộp vào một lượt cân đang hoạt động tại trạm (đang cân 1 hoặc cân 2). | Không được tạo lượt cân mới cho cắt lệnh này nữa. |
| **Đang lấy hàng** | Xe đang bốc xếp hàng hóa trong kho bãi của nhà máy. | Trạng thái hiển thị tiến độ trong kho. |
| **Đã hoàn tất** | Đơn cắt lệnh đã hoàn thành bốc hàng/nhập hàng, đã in phiếu và chốt sản lượng gửi ERP. | Cắt lệnh được khóa dữ liệu, không thể thao tác thêm. |
| **Đã hủy** | Cắt lệnh bị hủy bỏ bởi ERP hoặc Admin trạm cân. | Trạng thái lưu trữ, không hiển thị trên danh sách chờ cân. |

### 1.2.3 Trạng thái phân bổ dòng đơn chi tiết
| Tên trạng thái trên giao diện | Ý nghĩa nghiệp vụ |
| :--- | :--- |
| **Chưa phân bổ** | Dòng đơn gộp chưa được gán khối lượng thực tế sau khi xe cân lần 2. |
| **Đã phân bổ** | Dòng đơn gộp đã được gán đầy đủ khối lượng tịnh thực tế. |
| **Đã hủy** | Dòng đơn gộp bị xóa bỏ khỏi lượt cân. |

### 1.2.4 Trạng thái giải quyết quá tải xe con
| Tên trạng thái trên giao diện | Ý nghĩa nghiệp vụ |
| :--- | :--- |
| **Không cần tách tải** | Xe con cân ra có khối lượng tịnh nằm trong giới hạn tải trọng cho phép (TTCP + 10%). |
| **Chờ tách tải** | Xe con bị quá tải trọng cho phép, nút Tách tải sáng lên chờ xử lý. |
| **Đã xác nhận tách tải** | Đã hoàn tất chia khối lượng quá tải thành 2 phiếu cân con hợp lệ. |
| **Đã xác nhận không tách tải** | Đã được Admin phê duyệt phê duyệt lưu số cân quá tải gốc và cho xe ra không cần tách phiếu. |

---

# PHẦN 2. QUY TRÌNH NGHIỆP VỤ VẬN HÀNH CHI TIẾT

## 2.1 QUY TRÌNH CÂN NỘI ĐỊA (XE XUẤT HÀNG TIÊU CHUẨN)

### 1. Mục đích
Ghi nhận khối lượng xe không chở hàng (xe rỗng) khi vào (Cân lần 1), khối lượng xe đầy hàng khi ra (Cân lần 2) để tính toán khối lượng hàng hóa thực xuất đã bán cho khách hàng.

### 2. Điều kiện trước khi thao tác
* Cắt lệnh của xe đã có trên hệ thống ở trạng thái **Đã đăng ký**.
* Bàn cân sạch sẽ, không có người đứng trên bàn cân, đầu cân hiển thị 0 kg.
* Các camera hoạt động rõ nét và luồng video live preview hoạt động bình thường.

### 3. Các bước thực hiện

#### A. THAO TÁC XE VÀO (CÂN LẦN 1)
* **Bước 1:** Trên Menu điều hướng bên trái, bấm vào **Danh sách xe vào**.
* **Bước 2:** Nhập biển số xe vào ô lọc **Số PTVC:** ở thanh công cụ phía trên và nhấn **Enter**.
  * *Mẹo thao tác nhanh:* Chỉ cần gõ 3 chữ số cuối của biển số xe, hệ thống sẽ tự hiển thị danh sách gợi ý xổ xuống. Nhấp phím mũi tên Xuống để chọn biển số và nhấn **Enter** để chọn nhanh.
* **Bước 3:** Click chọn dòng cắt lệnh cần cân trên lưới dữ liệu. Toàn bộ thông tin đăng ký phương tiện sẽ tự động nạp lên Form thông tin xe.
* **Bước 4:** Kiểm tra thông tin mooc, hạn đăng kiểm xe và mooc. Nếu có trường nào báo chữ màu đỏ (hết hạn), hệ thống sẽ chặn không cho cân.
* **Bước 5:** Tích chọn cột **CHỌN** trên lưới nếu muốn gộp nhiều đơn hàng có cùng biển số xe của chuyến này.
* **Bước 6:** Bấm nút **CÂN NỘI ĐỊA** ở góc dưới bên phải của Form nhập liệu. Giao diện tự động chuyển sang màn hình **Cân nội địa**.
* **Bước 7:** Hướng dẫn tài xế cho xe đỗ đúng tâm bàn cân. Quan sát qua khung hiển thị camera trên màn hình xem xe đã đỗ gọn trong bàn cân chưa.
* **Bước 8:** Chờ số cân hiển thị lớn ở bảng cân chuyển sang trạng thái **"ỔN ĐỊNH"** (màu xanh lá). Bấm nút **CÂN LẦN 1** để ghi nhận khối lượng lần đầu của xe.
* **Bước 9:** Bấm nút **LƯU** ở bảng điều khiển để ghi nhận. Hệ thống sẽ chụp ảnh camera đầu xe làm minh chứng, lưu khối lượng cân lần 1 và chuyển trạng thái lượt cân thành **Chờ cân lần 2**.
* **Bước 10:** Bấm nút **IN PGN** để in phiếu giao nhận (PGN) cấp cho tài xế đi vào kho lấy hàng.

#### B. THAO TÁC XE RA (CÂN LẦN 2)
* **Bước 1:** Khi xe lấy hàng xong từ kho quay lại bàn cân ra, nhân viên vào màn hình **Cân nội địa**.
* **Bước 2:** Tại lưới **DANH SÁCH LƯỢT CÂN HOẠT ĐỘNG** ở dưới cùng, click chọn dòng xe tương ứng biển số xe đang đỗ trên bàn cân (ở trạng thái **Chờ cân lần 2**).
* **Bước 3:** Quan sát camera xem xe đỗ đúng vị trí. Chờ số cân hiển thị lớn báo **"ỔN ĐỊNH"** (màu xanh lá).
* **Bước 4:** Bấm nút **CÂN LẦN 2** để ghi nhận số cân lần 2.
* **Bước 5:** Bấm nút **LƯU** để lưu khối lượng cân lần 2. Hệ thống sẽ chụp ảnh camera đuôi xe làm minh chứng, tự động tính toán và hiển thị khối lượng tịnh:
  $$\text{TL hàng (Net Weight)} = |\text{Cân lần 1} - \text{Cân lần 2}|$$
* **Bước 6:** **Phân bổ sản lượng thực tế (Bắt buộc đối với xe gộp nhiều đơn hàng):**
  * Nếu xe chở gộp nhiều cắt lệnh, nút **PHÂN BỔ** sẽ sáng lên. Nhân viên bắt buộc bấm nút này.
  * Trong hộp thoại **PHÂN BỔ THỰC GIAO**, bấm nút **THEO KẾ HOẠCH** để hệ thống tự chia tỉ lệ, hoặc tự nhập khối lượng phân bổ bằng tay vào cột `KL THỰC (KG)` của từng dòng đơn.
  * *Lưu ý quan trọng:* Tổng khối lượng phân bổ của tất cả các dòng phải **bằng chính xác 100%** TL hàng của xe. Bấm **XÁC NHẬN** để lưu và đóng hộp thoại.
* **Bước 7:** **Đối chiếu Dung sai hàng bao (chỉ áp dụng đối với hàng đóng bao):**
  * Nếu khối lượng thực cân vượt quá khối lượng kế hoạch cộng với dung sai cho phép (Số bao * 1.75 kg), hệ thống hiển thị cảnh báo. Operator kiểm tra kỹ lý do và bấm check chọn **K LẤY ĐỦ SỐ LƯỢNG** để bỏ qua cảnh báo dung sai nếu được tài xế giải trình hợp lý.
* **Bước 8:** Bấm nút **IN PC** để in phiếu cân tổng hợp giao tài xế.
* **Bước 9:** Bấm nút **CHUYỂN XE RA**. Lượt cân chuyển trạng thái thành **Đã hoàn tất**, xe biến mất khỏi màn hình cân chính và chuyển sang **Danh sách xe ra**.

### 4. Kết quả mong đợi
Xe hoàn tất cân 2 lần, được in phiếu cân (PC) và phiếu giao nhận (PGN) rõ ràng. Dữ liệu được lưu trữ cục bộ dưới trạng thái **Đã hoàn tất** và tự động đồng bộ lên ERP.

### 5. Lỗi thường gặp
* **Chặn in phiếu:** Nhân viên chưa thực hiện phân bổ khối lượng đối với xe gộp nhiều đơn nhưng đã bấm in phiếu.
* **Cân khi số cân dao động:** Bấm lấy số cân khi xe chưa dừng hẳn dẫn đến số cân nhảy liên tục và nút lấy cân bị khóa.

### 6. Cách xử lý
* Bấm nút **PHÂN BỔ** và hoàn tất phân bổ trước khi in.
* Yêu cầu tài xế tắt máy xe, dừng hẳn xe trên bàn cân để đầu cân trả về trạng thái **ỔN ĐỊNH**.

### 7. Khi nào cần gọi quản lý / IT
* Khi bàn cân trống nhưng số cân hiển thị bị lệch âm hoặc dương quá nhiều (ví dụ lệch > 50 kg) cần gọi kỹ thuật hiệu chuẩn lại bàn cân.

---

## 2.2 QUY TRÌNH CÂN HÀNG NHẬP (NGUYÊN VẬT LIỆU VÀO NHÀ MÁY)

### 1. Mục đích
Ghi nhận khối lượng xe đầy hàng khi vào trạm (Cân lần 1), khối lượng xe rỗng sau khi trút hàng khi ra (Cân lần 2) để xác định chính xác khối lượng nguyên vật liệu nhập vào nhà máy.

### 2. Điều kiện trước khi thao tác
* Cắt lệnh nhập hàng đã được tạo thủ công hoặc đồng bộ từ ERP.
* Loại giao dịch của cắt lệnh phải được chọn chính xác là **Nhập hàng** (Inbound).

### 3. Các bước thực hiện
* **Bước 1:** Trên Menu điều hướng bên trái, bấm vào **Danh sách xe vào**.
* **Bước 2:** Click chọn nút **LÀM MỚI** để đưa Form thông tin xe phía trên về trạng thái tạo mới.
* **Bước 3:** Tại ô **Loại**, chọn **Nhập hàng** (Inbound).
* **Bước 4:** Nhập biển số xe vào ô gợi ý **Số PTVC**. Hệ thống sẽ tự động điền các thông tin mooc, tài xế, hạn đăng kiểm và TTCP nếu xe đã từng cân tại trạm.
* **Bước 5:** Nhập mã khách hàng (Nhà cung cấp), mã sản phẩm (Nguyên vật liệu nhập), và khối lượng kế hoạch.
* **Bước 6:** Bấm nút **CÂN NỘI ĐỊA** để tạo lượt cân và chuyển sang màn hình **Cân nội địa**.
* **Bước 7:** Cho xe đầy hàng đỗ lên bàn cân. Chờ số cân báo **"ỔN ĐỊNH"**. Bấm nút **CÂN LẦN 1** để lấy số cân, sau đó bấm nút **LƯU** để lưu lại. Số cân lần 1 này chính là Khối lượng tổng (Gross Weight).
* **Bước 8:** Bấm nút **IN PGN** ở thanh công cụ phía dưới để in Phiếu giao nhận (PGN) cấp cho tài xế đi vào kho trút hàng.
* **Bước 9:** Sau khi xe trút sạch hàng, cho xe rỗng quay lại đỗ lên bàn cân. Click chọn xe từ lưới **DANH SÁCH LƯỢT CÂN HOẠT ĐỘNG** ở phía dưới (ở trạng thái **Chờ cân lần 2**).
* **Bước 10:** Chờ số cân báo **"ỔN ĐỊNH"**. Bấm nút **CÂN LẦN 2** để lấy số cân, sau đó bấm nút **LƯU** để lưu. Số cân lần 2 này chính là Khối lượng bì (Tare Weight).
* **Bước 11:** Hệ thống tự động tính khối lượng hàng nhập thực tế:
  $$\text{TL hàng (Net Weight)} = \text{Cân lần 1} - \text{Cân lần 2}$$
* **Bước 12:** Bấm nút **IN PC** để in Phiếu cân, và bấm **CHUYỂN XE RA** để hoàn tất và giải phóng xe khỏi trạm.

### 4. Kết quả mong đợi
Khối lượng hàng nhập thực tế được ghi nhận chính xác và đồng bộ về ERP để khấu trừ sản lượng đơn hàng nhập của nhà cung cấp.

### 5. Lỗi thường gặp
* Chọn nhầm loại giao dịch là **Xuất hàng** (Outbound) dẫn đến quy trình đảo ngược (coi cân lần 1 là xe không, cân lần 2 là xe đầy), gây sai lệch số liệu.

### 6. Cách xử lý
* Nếu lỡ tạo nhầm phiên cân Xuất hàng cho xe nhập hàng mà chưa lưu cân lần 2: Bấm nút **HỦY** (Hủy phiên cân) trên màn hình Cân chính để giải phóng cắt lệnh gốc về lại Danh sách xe vào. Sửa lại loại giao dịch trên Form thành **Nhập hàng** và xác nhận cân lại.

### 7. Khi nào cần gọi quản lý / IT
* Khi xe đã trút hàng xong nhưng cân lần 2 lại có khối lượng bì lớn hơn khối lượng tổng lần 1 (TL hàng bị âm).

---

## 2.3 QUY TRÌNH CÂN XUẤT KHẨU ĐƠN HÀNG LỚN (NHIỀU CHUYẾN XE CON CỘNG DỒN)

### 1. Mục đích
Quản lý các hợp đồng xuất khẩu khối lượng lớn xuống tàu/sà lan, cho phép một đơn cắt lệnh xuất khẩu lớn chạy nhiều chuyến xe con ra vào liên tục để lấy hàng và chốt tổng sản lượng sau cùng.

### 2. Điều kiện trước khi thao tác
* Cắt lệnh xuất khẩu lớn đã được đồng bộ từ ERP xuống trạm cân dưới dạng cờ xuất khẩu (`IsExportScale = true`).

### 3. Các bước thực hiện

#### A. THAO TÁC KHỞI ĐỘNG ĐƠN HÀNG XUẤT KHẨU
* **Bước 1:** Trên Menu điều hướng bên trái, bấm vào **Cân xuất khẩu**.
* **Bước 2:** Click chọn đơn cắt lệnh xuất khẩu lớn đang hoạt động tại lưới **DANH SÁCH CẮT LỆNH XUẤT KHẨU** ở phía trên. Thông tin chi tiết về sản lượng kế hoạch, lũy kế đã bốc và sản lượng còn lại sẽ hiển thị trên Panel chi tiết đơn hàng cha.
  * Mặc định lưới này ẩn các cắt lệnh đã **chốt tổng** và đã được ERP xác nhận **Hoàn thành xuất hàng**.
  * Khi cần tra cứu/đối chiếu lại các cắt lệnh đã hoàn thành ERP, tích checkbox **Đã hoàn thành** ở cùng dòng tiêu đề **DANH SÁCH CẮT LỆNH XUẤT KHẨU**. Hệ thống sẽ hiển thị thêm các cắt lệnh đã chốt tổng và có trạng thái ERP hoàn thành. Các cắt lệnh này chỉ dùng để xem lại, không cho tạo thêm chuyến xe hoặc cân tiếp.
  * **Trường hợp chưa có cắt lệnh thật từ ERP:**
    * Bấm nút **TẠO CẮT LỆNH TẠM** trên thanh công cụ phía trên.
    * Hệ thống sẽ tự động tạo một cắt lệnh tạm với mã hiển thị có định dạng `CL-TAM-####`.
    * Sử dụng cắt lệnh tạm này để bắt đầu vận hành cân các chuyến xe con bình thường.
  * **Trường hợp ERP truyền cắt lệnh thật xuống sau:**
    * Khi cắt lệnh thật xuất hiện, nhấp chọn đơn cắt lệnh thật ở màn hình **Danh sách xe vào** và bấm **CÂN XUẤT KHẨU**.
    * Hệ thống phát hiện có các cắt lệnh tạm đang hoạt động và hiển thị bảng đối chiếu map.
    * Click chọn cắt lệnh tạm tương ứng và bấm **Xác nhận map**. Hệ thống sẽ tự động chuyển toàn bộ các chuyến xe con đã cân từ cắt lệnh tạm sang cắt lệnh thật và cập nhật sản lượng lũy kế.

#### B. THAO TÁC CÂN CHUYẾN XE CON
* **Bước 1:** Khi xe con vào trạm cân lần 1, nhập biển số xe con vào Form thông tin chuyến xe ở phía trên (bao gồm các trường **Số PTVC**, **Mooc**, **Tài xế**). Hệ thống sẽ tự động gợi ý biển số xe và điền các thông tin mooc, tài xế, đăng kiểm và TTCP.
* **Bước 2:** Kiểm tra hạn đăng kiểm xe và mooc. Nếu hợp lệ, bấm nút **TẠO CHUYẾN XE** ở thanh công cụ giữa màn hình. Chuyến xe con dở dang sẽ xuất hiện ở lưới **DANH SÁCH CHUYẾN XE** phía dưới cùng với trạng thái **Chờ cân lần 1**.
* **Bước 3:** Cho xe con đỗ lên bàn cân lần 1. Chọn chuyến xe con trên lưới **DANH SÁCH CHUYẾN XE** phía dưới. Chờ số cân báo **"ỔN ĐỊNH"** (màu xanh lá). Bấm nút **CÂN LẦN 1** để lấy số cân, sau đó bấm nút **LƯU** ở góc phải bảng cân để lưu số cân lần 1.
* **Bước 4:** Bấm nút **IN PGN** ở thanh công cụ giữa màn hình để in phiếu giao nhận cho xe con đi lấy hàng.
* **Bước 5:** Khi xe con đầy hàng quay lại bàn cân lần 2, chọn lại đúng chuyến xe con đó trên lưới **DANH SÁCH CHUYẾN XE** phía dưới (ở trạng thái **Chờ cân lần 2**).
* **Bước 6:** Chờ số cân báo **"ỔN ĐỊNH"** (màu xanh lá). Bấm nút **CÂN LẦN 2** để lấy số cân.
* **Bước 7:** Bấm nút **LƯU** ở góc phải bảng cân để lưu khối lượng cân lần 2. Hệ thống sẽ tự động tính khối lượng tịnh cho chuyến xe con:
  * *Cảnh báo vượt sản lượng còn lại:* Nếu khối lượng tịnh của chuyến xe làm cho sản lượng còn lại của đơn cha bị âm, hệ thống hiển thị cảnh báo. Operator kiểm tra kỹ và bấm Xác nhận đồng ý nếu được phép bốc dung sai dư tải.
  * *Cảnh báo vượt dung sai:* Nếu hàng đóng bao bị vượt quá dung sai quy định, phần mềm hiển thị hộp thoại cảnh báo vượt dung sai. Bấm **Vẫn lưu** hoặc **Hủy**.
* **Bước 8:** Bấm nút **IN PC** ở thanh công cụ giữa để in phiếu cân cho chuyến xe con.



#### C. CHUYỂN CHUYẾN XE CON
Sử dụng khi một xe con đã lấy hàng xong nhưng cần chuyển sản lượng sang một đơn hàng xuất khẩu lớn khác:
* **Bước 1:** Chọn chuyến xe con cần chuyển trên lưới **DANH SÁCH CHUYẾN XE** phía dưới cùng.
* **Bước 2:** Bấm nút **CHUYỂN CHUYẾN**.
* **Bước 3:** Chọn đơn cắt lệnh xuất khẩu lớn đích trong danh sách xổ xuống của hộp thoại.
* **Bước 4:** Bấm **Xác nhận chuyển**. Hệ thống tự động dời dữ liệu chuyến xe sang đơn đích và tính toán lại lũy kế sản lượng của cả 2 đơn.

#### D. CHỐT TỔNG SẢN LƯỢNG ĐƠN HÀNG XUẤT KHẨU
* **Bước 1:** Khi tàu/sà lan đã bốc đủ sản lượng kế hoạch, click chọn đơn cắt lệnh cha xuất khẩu lớn ở lưới phía trên.
* **Bước 2:** Bấm nút **CHỐT TỔNG**.
* **Bước 3:** Xác nhận hộp thoại cảnh báo: *"Bạn có chắc chắn muốn chốt tổng sản lượng cho đơn xuất khẩu này? Sau khi chốt sẽ không thể tạo thêm chuyến xe con mới."*
* **Bước 4:** Hệ thống khóa trạng thái đơn hàng thành **Đã hoàn tất**, cập nhật tổng sản lượng thực xuất và tự động đẩy dữ liệu chốt về ERP.

---

# PHẦN 3. CÁC TÌNH HUỐNG PHÁT SINH VÀ CÁCH XỬ LÝ

> [!IMPORTANT]
> Đây là phần hướng dẫn quan trọng nhất. Nhân viên vận hành trạm cân phải tra cứu kỹ mục này trước khi thực hiện bất kỳ hành động can thiệp kỹ thuật nào hoặc gọi điện cho IT/Quản lý.

---

### TÌNH HUỐNG 1: Xe chưa có đăng ký phương tiện từ ERP
1. **Mục đích:** Tạo đơn đăng ký cân thủ công trực tiếp tại trạm để cho xe vào cân, tránh ùn tắc bàn cân.
2. **Điều kiện trước khi thao tác:** Xe đã đỗ trước cổng trạm cân nhưng kiểm tra trên lưới xe chờ vào không thấy thông tin. Tài xế xuất trình được phiếu xuất kho giấy hoặc tin nhắn lệnh từ điều độ.
3. **Các bước thực hiện:**
   * **Bước 1:** Tại màn hình **Danh sách xe vào**, bấm nút **LÀM MỚI** trên thanh công cụ để xóa trống Form thông tin xe phía trên và đưa về chế độ tạo mới.
   * **Bước 2:** Nhập biển số xe vào ô gợi ý **Số PTVC**. Hệ thống sẽ tự gợi ý và điền mooc, tài xế, đăng kiểm, TTCP (nếu xe đã từng cân tại trạm).
   * **Bước 3:** Chọn loại giao dịch (Nhập hàng - Inbound / Xuất hàng - Outbound).
   * **Bước 4:** Gõ mã hoặc tên khách hàng, mã sản phẩm. Nhập khối lượng kế hoạch.
   * **Bước 5:** Nhập ngày hết hạn đăng kiểm xe/mooc đúng theo sổ đăng kiểm tài xế xuất trình.
   * **Bước 6:** Bấm nút **CÂN NỘI ĐỊA**. Hệ thống tự tạo cắt lệnh thủ công (Source = MANUAL) và tự cập nhật xe mới vào danh mục phương tiện.
4. **Kết quả mong đợi:** Tạo phiên cân thành công và điều hướng sang màn hình **Cân nội địa**.
5. **Lỗi thường gặp:** Nhập sai hạn đăng kiểm dẫn đến hệ thống chặn không cho cân.
6. **Cách xử lý:** Kiểm tra kỹ sổ đăng kiểm của tài xế và nhập đúng ngày hết hạn đăng kiểm trên phần mềm.
7. **Khi nào cần gọi quản lý / IT:** Khi tạo đơn nhập tay nhưng hệ thống báo lỗi cơ sở dữ liệu hoặc không tìm thấy mã sản phẩm trong danh mục.

---

### TÌNH HUỐNG 2: Không tìm thấy xe trên Danh sách xe chờ vào
1. **Mục đích:** Tìm kiếm và định vị đơn đăng ký xe bị trôi hoặc lệch thông tin lọc.
2. **Điều kiện trước khi thao tác:** Xe đã được điều độ thông báo đã ký duyệt trên ERP.
3. **Các bước thực hiện:**
   * **Bước 1:** Bấm nút **LÀM MỚI** ở góc trên thanh công cụ để làm mới lưới dữ liệu.
   * **Bước 2:** Xóa sạch ô lọc biển số xe và ô mã cắt lệnh (nếu đang có chữ).
   * **Bước 3:** Nhập chính xác mã cắt lệnh ERP vào ô tìm kiếm **Mã cắt lệnh:** và nhấn **Enter**.
   * **Bước 4:** Nếu vẫn không thấy, nhập 4 chữ số cuối của biển số xe vào ô tìm kiếm **Số PTVC:** và nhấn **Enter**.
4. **Kết quả mong đợi:** Tìm thấy dòng đăng ký xe bị nhập sai ký tự biển số (ví dụ dấu gạch ngang, dấu chấm).
5. **Lỗi thường gặp:** ERP truyền xuống bị sai ký tự biển số xe (Ví dụ: đăng ký là `29C-123.45` nhưng xe thực tế là `29C-12345`).
6. **Cách xử lý:** Chọn dòng xe bị sai biển số đó, sửa lại biển số xe cho đúng trên Form thông tin xe phía trên, bấm **Lưu** (SaveDetailCommand), sau đó bấm **CÂN NỘI ĐỊA**.
7. **Khi nào cần gọi quản lý / IT:** Khi đã tìm kiếm bằng mọi cách mà không thấy dữ liệu, cần gọi bộ phận ERP kiểm tra xem đơn hàng đã được truyền xuống server chi nhánh trạm cân chưa.

---

### TÌNH HUỐNG 3: Phát hiện sai biển số xe khi xe đang đỗ trên bàn cân lần 1
1. **Mục đích:** Chỉnh sửa biển số xe cho đúng thực tế trước khi lưu số cân lần 1.
2. **Điều kiện trước khi thao tác:** Phiên cân đang ở trạng thái **Chờ cân lần 1**, chưa bấm lấy cân 1.
3. **Các bước thực hiện:**
   * **Bước 1:** Bấm nút **HỦY** trên màn hình Cân chính.
   * **Bước 2:** Quay lại màn hình **Danh sách xe vào**.
   * **Bước 3:** Chọn đơn cắt lệnh gốc vừa hủy.
   * **Bước 4:** Tại Form thông tin xe phía trên, sửa lại biển số xe chính xác.
   * **Bước 5:** Bấm **Lưu** (SaveDetailCommand) và bấm **CÂN NỘI ĐỊA** để bắt đầu lại lượt cân.
4. **Kết quả mong đợi:** Biển số xe được cập nhật đúng trên màn hình cân chính và trên phiếu cân in ra sau này.
5. **Lỗi thường gặp:** Vẫn bấm lấy cân 1 với biển số sai, dẫn đến khi cân lần 2 không thể tìm thấy xe hoặc in phiếu bị sai biển số.
6. **Cách xử lý:** Tuyệt đối không lưu cân khi phát hiện biển số xe trên phần mềm lệch với biển số xe thực tế đỗ trên bàn cân.
7. **Khi nào cần gọi quản lý / IT:** Khi nút HỦY bị khóa hoặc không tìm lại được cắt lệnh gốc sau khi hủy.

---

### TÌNH HUỐNG 4: Phát hiện sai thông tin sản phẩm trên đơn cân
1. **Mục đích:** Thay đổi mã sản phẩm cho đúng với thực tế xe chở hàng.
2. **Điều kiện trước khi thao tác:** Xe chưa cân lần 2 và chưa in phiếu.
3. **Các bước thực hiện:**
   * **Bước 1:** Bấm **HỦY** trên màn hình cân để trả đơn hàng về trạng thái chờ vào.
   * **Bước 2:** Chọn đơn hàng đó tại **Danh sách xe vào**.
   * **Bước 3:** Nhấp vào ô nhập mã sản phẩm trên Form thông tin xe phía trên, gõ mã sản phẩm đúng để hệ thống tự gợi ý và chọn sản phẩm mới.
   * **Bước 4:** Bấm **Lưu** (SaveDetailCommand) và bắt đầu lại quy trình cân.
4. **Kết quả mong đợi:** Sản phẩm được cập nhật đúng và tự động đổi phân loại hàng bao/hàng rời tương ứng.
5. **Lỗi thường gặp:** Chọn sai sản phẩm dẫn đến tính toán sai dung sai hàng bao hoặc gán sai đơn giá hàng hóa trên ERP.
6. **Cách xử lý:** Làm theo các bước trên. Nếu là đơn từ ERP đồng bộ xuống, nhân viên không được tự ý sửa sản phẩm trừ khi có lệnh từ điều độ bằng văn bản.
7. **Khi nào cần gọi quản lý / IT:** Khi đơn hàng đồng bộ từ ERP bị khóa không cho sửa đổi sản phẩm tại trạm cân.

---

### TÌNH HUỐNG 5: Phát hiện sai thông tin Khách hàng / Nhà phân phối
1. **Mục đích:** Sửa thông tin khách hàng nhận hàng để in hóa đơn/phiếu xuất kho chính xác.
2. **Điều kiện trước khi thao tác:** Xe chưa hoàn tất cân lần 2.
3. **Các bước thực hiện:**
   * **Bước 1:** Bấm **HỦY** trên màn hình cân.
   * **Bước 2:** Tìm đơn tại **Danh sách xe vào**.
   * **Bước 3:** Sửa Mã khách hàng trên Form, bấm **Lưu**.
   * **Bước 4:** Thực hiện lại quy trình cân vào bàn cân.
4. **Kết quả mong đợi:** Thông tin khách hàng được hiển thị đúng trên biểu mẫu phiếu in.
5. **Lỗi thường gặp:** Nhập sai mã khách hàng dẫn đến việc xuất hóa đơn nhầm cho đơn vị khác.
6. **Cách xử lý:** Đối chiếu thông tin khách hàng trên lệnh xuất hàng giấy với thông tin hiển thị trên phần mềm trước khi bấm xác nhận vào cân.
7. **Khi nào cần gọi quản lý / IT:** Khi sửa mã khách hàng nhưng hệ thống báo lỗi không tồn tại khách hàng trong danh mục gốc cục bộ.

---

### TÌNH HUỐNG 6: Xe đã cân lần 1 rồi nhưng khi quay lại cân lần 2 không thấy dữ liệu dở dang
1. **Mục đích:** Tìm lại phiên cân dở dang của xe để thực hiện cân lần 2, tránh bắt xe phải cân lại lần 1 từ đầu.
2. **Điều kiện trước khi thao tác:** Xe chắc chắn đã hoàn thành cân lần 1 và có cầm Phiếu giao nhận (PGN) có in Mã lượt cân/Số phiên cân.
3. **Các bước thực hiện:**
   * **Bước 1:** Vào màn hình **Cân nội địa**.
   * **Bước 2:** Nhập chính xác Số phiên cân vào ô tìm kiếm **Số lượt cân:** ở thanh công cụ phía trên và nhấn **Enter**.
   * **Bước 3:** Nếu không thấy, nhập biển số xe vào ô tìm kiếm **Số PTVC:** và nhấn **Enter**.
   * **Bước 4:** Kiểm tra xem dòng xe có đang nằm ở **Danh sách xe ra** do nhân viên ca trước bấm nhầm nút hoàn tất xe ra hay không.
4. **Kết quả mong đợi:** Tìm thấy phiên cân dở dang đang ở trạng thái **Chờ cân lần 2** và nạp lên màn hình cân chính.
5. **Lỗi thường gặp:** Nhập sai bộ lọc tìm kiếm hoặc ca trước đã bấm nhầm nút chuyển xe ra khiến xe chuyển sang danh sách hoàn thành.
6. **Cách xử lý:** Nếu xe nằm ở danh sách xe ra nhưng chưa cân lần 2, Admin phải sử dụng chức năng khôi phục phiên cân hoặc mở khóa để cho xe cân lại lần 2.
7. **Khi nào cần gọi quản lý / IT:** Khi đã tìm kiếm khắp hệ thống mà không thấy số lượt cân cũ, buộc phải gọi IT kiểm tra log cơ sở dữ liệu để tìm nguyên nhân mất dữ liệu phiên cân.

---

### TÌNH HUỐNG 7: Cổng cân kết nối bình thường nhưng số cân hiển thị trên màn hình đứng im ở mức 0 kg khi xe đã đỗ lên bàn cân
1. **Mục đích:** Khôi phục việc đọc số cân trực tiếp từ bàn cân vật lý.
2. **Điều kiện trước khi thao tác:** Xe đã đỗ hẳn lên bàn cân, đầu cân vật lý ngoài sân hiển thị đúng số tải trọng xe (ví dụ 15,320 kg).
3. **Các bước thực hiện:**
   * **Bước 1:** Vào Tab **Diagnostics** để kiểm tra log dữ liệu thô nhận được từ cổng COM.
   * **Bước 2:** Nếu log dữ liệu thô rỗng hoặc hiển thị các ký tự lạ, kiểm tra cáp RS-232 kết nối cổng COM xem có bị lỏng hoặc đứt dây không.
   * **Bước 3:** Vào Tab **Cấu hình hệ thống** -> **Cấu hình Cân** -> Bấm nút **LÀM MỚI** (RefreshPorts) -> Chọn lại cổng COM khác (nếu máy tính trạm có nhiều cổng) -> Bấm **Lưu**.
4. **Kết quả mong đợi:** Khung hiển thị số cân lớn trên phần mềm nhảy số và khớp với số trên đầu cân vật lý.
5. **Lỗi thường gặp:** Hỏng cổng COM trên máy tính trạm hoặc lỏng đầu cáp DB9 nối từ đầu cân vào máy tính.
6. **Cách xử lý:** Rút đầu cáp COM ra cắm lại chặt, vệ sinh chân cắm, khởi động lại phần mềm trạm cân.
7. **Khi nào cần gọi quản lý / IT:** Khi cáp kết nối vẫn tốt, đầu cân hiển thị số bình thường nhưng phần mềm hoàn toàn không nhận được dữ liệu thô.

---

### TÌNH HUỐNG 8: Số cân hiển thị trên phần mềm nhảy liên tục không dừng (Dao động liên tục)
1. **Mục đích:** Làm ổn định số cân hiển thị để nút lấy cân được mở khóa.
2. **Điều kiện trước khi thao tác:** Xe đã đỗ trên bàn cân, bàn cân không có dị vật đè lên.
3. **Các bước thực hiện:**
   * **Bước 1:** Yêu cầu tài xế tắt máy xe hoàn toàn (để tránh rung lắc bàn cân do động cơ nổ).
   * **Bước 2:** Yêu cầu tài xế và những người đi cùng xuống xe, không đứng hoặc tựa vào thành bàn cân.
   * **Bước 3:** Kiểm tra khe hở xung quanh bàn cân xem có bị kẹt đất đá, sắt thép vụn hoặc rác thải chèn vào bàn cân hay không. Vệ sinh sạch sẽ khe hở bàn cân.
   * **Bước 4:** Kiểm tra xem dưới gầm bàn cân có bị ngập nước hoặc có vật lạ chèn dưới dầm cân không.
4. **Kết quả mong đợi:** Số cân hiển thị ổn định, chữ cảnh báo chuyển thành **"ỔN ĐỊNH"** và nút lấy cân sáng lên.
5. **Lỗi thường gặp:** Xe nổ máy gây rung bàn cân, hoặc kẹt rác ở khe co giãn của bàn cân.
6. **Cách xử lý:** Làm theo các bước vệ sinh và tắt máy xe ở trên.
7. **Khi nào cần gọi quản lý / IT:** Khi bàn cân đã sạch sẽ, xe đã tắt máy mà số cân vẫn nhảy liên tục lệch hàng trăm kg.

---

### TÌNH HUỐNG 9: Khung camera hiển thị màn hình đen hoặc mất hình ảnh
1. **Mục đích:** Khôi phục hình ảnh camera để kiểm soát vị trí đỗ xe của tài xế.
2. **Điều kiện trước khi thao tác:** Mạng LAN nội bộ tại trạm cân hoạt động bình thường.
3. **Các bước thực hiện:**
   * **Bước 1:** Kiểm tra phích cắm nguồn của camera ngoài sân cân.
   * **Bước 2:** Kiểm tra switch mạng trạm cân xem cổng mạng cắm camera có sáng đèn xanh không.
   * **Bước 3:** Khởi động lại camera bằng cách rút phích cắm điện của camera ra, chờ 10 giây rồi cắm lại.
   * **Bước 4:** Tắt phần mềm StationApp đi và mở lại để hệ thống kết nối lại luồng stream RTSP.
4. **Kết quả mong đợi:** Camera lên hình rõ nét trên giao diện cân.
5. **Lỗi thường gặp:** Hỏng adaptor nguồn camera hoặc lỏng đầu cắm dây mạng ngoài trời do mưa gió.
6. **Cách xử lý:** Thực hiện khởi động lại camera và phần mềm.
7. **Khi nào cần gọi quản lý / IT:** Khi camera bị hỏng phần cứng hoặc đứt dây cáp mạng ngoài trời.

---

### TÌNH HUỐNG 10: Máy in bị kẹt giấy hoặc báo lỗi đèn đỏ không in được
1. **Mục đích:** Xử lý lỗi máy in vật lý để in phiếu cho xe ra.
2. **Điều kiện trước khi thao tác:** Máy in đang báo lỗi đèn đỏ nhấp nháy hoặc màn hình máy in hiển thị mã lỗi.
3. **Các bước thực hiện:**
   * **Bước 1:** Tắt công tắc nguồn máy in.
   * **Bước 2:** Mở nắp máy in, rút hộp mực ra ngoài. Nhẹ nhàng kéo tờ giấy bị kẹt ra theo chiều đi của giấy.
   * **Bước 3:** Lắp lại hộp mực, đóng nắp máy in và bật nguồn lên lại.
   * **Bước 4:** Kiểm tra xem khay giấy có bị lệch khổ giấy hoặc hết giấy không. Nạp thêm giấy đúng khổ.
4. **Kết quả mong đợi:** Đèn máy in chuyển sang màu xanh lá ổn định (Ready). Bấm in lại trên phần mềm phiếu in ra bình thường.
5. **Lỗi thường gặp:** Giấy in bị ẩm, nạp giấy xéo vào khay gây kẹt.
6. **Cách xử lý:** Thay giấy mới khô ráo, căn chỉnh thanh kẹp khay giấy cho khít với khổ giấy in.
7. **Khi nào cần gọi quản lý / IT:** Máy in bị hỏng cơ cuốn giấy, hỏng sấy hoặc không lên nguồn.

---

### TÌNH HUỐNG 11: Phiếu in ra bị lỗi (lệch chữ, mất dòng hoặc chữ mờ)
1. **Mục đích:** Điều chỉnh lại căn lề in offset hoặc vệ sinh máy in để phiếu in rõ ràng, đúng biểu mẫu quy định.
2. **Điều kiện trước khi thao tác:** Máy in vẫn hoạt động bình thường nhưng bản in ra giấy bị lỗi trình bày.
3. **Các bước thực hiện:**
   * **Bước 1:** Nếu chữ bị mờ, lấy hộp mực máy in ra lắc nhẹ đều sang hai bên rồi lắp lại.
   * **Bước 2:** Nếu chữ bị lệch lề: Đăng nhập tài khoản **ADMIN** -> Vào **Cấu hình hệ thống** -> **Cấu hình in** -> Chọn phôi in cần sửa -> Bấm **Cấu hình**.
   * **Bước 3:** Nhập giá trị dịch lề Offset X và Offset Y tương ứng, sau đó bấm **Lưu**.
   * **Bước 4:** Tiến hành in thử một phiếu cũ ở màn hình **Danh sách xe ra** để kiểm tra vị trí chữ.
4. **Kết quả mong đợi:** Nội dung in khớp hoàn toàn vào các ô trống trên phôi in giấy của nhà máy.
5. **Lỗi thường gặp:** Thay đổi máy in mới có driver lề in khác máy in cũ, hoặc hộp mực sắp hết.
6. **Cách xử lý:** Căn chỉnh lại offset X/Y trên phần mềm và lắc mực như hướng dẫn.
7. **Khi nào cần gọi quản lý / IT:** Khi đã chỉnh offset rất nhiều lần mà chữ vẫn bị tràn lề hoặc driver máy in bị lỗi font chữ tiếng Việt.

---

### TÌNH HUỐNG 12: Xe cân lần 2 phát hiện bị quá tải trọng cho phép (TTCP) lưu hành
1. **Mục đích:** Xử lý hạ tải hoặc tách phiếu theo đúng quy định đăng kiểm giao thông trước khi cho xe ra bãi.
2. **Điều kiện trước khi thao tác:** Phần mềm hiển thị cảnh báo quá tải màu đỏ và khóa nút Cho xe ra. Nút **TÁCH TẢI** sáng lên.
3. **Các bước thực hiện:**
   * **Bước 1:** Bấm nút **TÁCH TẢI** (ShowOverweightHandlingCommand).
   * **Bước 2:** Trao đổi với tài xế xem có đồng ý thực hiện tách phiếu hay không:
     * **Nếu tài xế đồng ý tách phiếu:** Chọn phương án **XÁC NHẬN TÁCH TẢI** sau khi hệ thống tự động đề xuất chia khối lượng tịnh của chuyến thành 2 phiếu cân con nhỏ hợp lệ tải trọng.
     * **Nếu tài xế không đồng ý tách phiếu và chấp nhận quay lại kho hạ tải:** Bấm nút **HỦY** trên màn hình cân, hướng dẫn tài xế cho xe quay lại kho bốc xếp dỡ bớt hàng xuống, sau đó cho xe quay lại bàn cân để cân lại lần 2 từ đầu.
     * **Trường hợp đặc biệt được đi quá tải (chỉ chạy nội bộ):** Yêu cầu Quản trị viên (Admin) đăng nhập phê duyệt chọn phương án **KHÔNG TÁCH TẢI**.
4. **Kết quả mong đợi:** Lượt cân được giải quyết quá tải hợp lệ, cho phép in phiếu và hoàn tất cho xe ra.
5. **Lỗi thường gặp:** Nhân viên cố tình cho xe quá tải ra bãi mà không xử lý tách phiếu hoặc hạ tải, vi phạm luật giao thông đường bộ.
6. **Cách xử lý:** Luôn tuân thủ quy trình tách phiếu tự động của phần mềm để bảo đảm an toàn.
7. **Khi nào cần gọi quản lý / IT:** Khi tài xế không hợp tác hoặc khi phương án tách tải tự động báo lỗi không lưu được dữ liệu.

---

### TÌNH HUỐNG 13: Bắt buộc phải tách phiếu cân do yêu cầu đơn hàng hoặc quá tải trọng
1. **Mục đích:** Thực hiện chia nhỏ một phiếu cân lớn thành các phiếu cân con độc lập.
2. **Điều kiện trước khi thao tác:** Lượt cân đã có số cân lần 2 ổn định.
3. **Các bước thực hiện:**
   * **Bước 1:** Tại màn hình cân chính, bấm nút **TÁCH TẢI**.
   * **Bước 2:** Chọn chế độ tách thủ công (gõ trực tiếp số cân vào ô Phiếu 1 hoặc Phiếu 2).
   * **Bước 3:** Nhập số cân mong muốn phân bổ cho Phiếu 1 (hoặc Phiếu 2). Hệ thống tự động tính khối lượng phiếu còn lại.
   * **Bước 4:** Kiểm tra xem khối lượng của cả 2 phiếu đã nhỏ hơn giới hạn tải trọng cho phép của xe chưa. Nếu hợp lệ, bấm **XÁC NHẬN TÁCH TẢI**.
4. **Kết quả mong đợi:** Hệ thống tự động sinh ra 2 phiếu cân con độc lập với mã số phiếu riêng biệt để in ra cấp cho tài xế.
5. **Lỗi thường gặp:** Nhập khối lượng phiếu quá lớn dẫn đến phiếu vẫn bị quá tải, phần mềm sẽ chặn không cho lưu.
6. **Cách xử lý:** Nhập lại số cân phân chia sao cho cả hai phiếu đều nằm trong ngưỡng tải trọng an toàn của xe con.
7. **Khi nào cần gọi quản lý / IT:** Khi bấm xác nhận tách thủ công nhưng hệ thống báo lỗi giao dịch SQL Server hoặc không in được phiếu tách.

---

### TÌNH HUỐNG 14: Xe quay lại cân lần 2 nhưng số cân hiển thị không khớp logic (Sai lệch bất thường)
1. **Mục đích:** Phát hiện và ngăn chặn gian lận thay đổi vỏ xe, mooc hoặc sai lệch do dị vật đè lên bàn cân.
2. **Điều kiện trước khi thao tác:** Số cân lần 2 bị lệch bất thường so với cân lần 1.
3. **Các bước thực hiện:**
   * **Bước 1:** Tuyệt đối không bấm Lưu số cân lần 2.
   * **Bước 2:** Yêu cầu tài xế đỗ xe ra ngoài bàn cân để kiểm tra.
   * **Bước 3:** Kiểm tra xem xe có đỗ lệch cầu cân không.
   * **Bước 4:** Đối chiếu biển số đầu xe và biển số mooc thực tế với thông tin đăng ký lần 1. Kiểm tra xem tài xế có tự ý thay đổi rơ-mooc khác trong lúc vào kho lấy hàng không.
   * **Bước 5:** Bấm **XEM ẢNH** để xem lại ảnh chụp camera lần 1 làm đối chứng hình dạng xe và mooc.
4. **Kết quả mong đợi:** Phát hiện ra nguyên nhân sai lệch khối lượng (do đỗ lệch cân hoặc đổi mooc xê dịch tải trọng vỏ).
5. **Lỗi thường gặp:** Tài xế đỗ lệch bánh xe ra ngoài bàn cân để giảm số cân tổng, hoặc thay mooc nhẹ hơn để gian lận tăng khối lượng tịnh của hàng.
6. **Cách xử lý:** Hướng dẫn tài xế lái xe đỗ lại chính xác vào tâm bàn cân và tắt máy cân lại.
7. **Khi nào cần gọi quản lý / IT:** Khi xe đã đỗ đúng vị trí, mooc chuẩn nhưng khối lượng bì vẫn lệch quá lớn (trên 500 kg) so với đăng kiểm, cần báo quản lý lập biên bản kiểm tra xe trước khi cho ra.

---

### TÌNH HUỐNG 15: Mất điện đột ngột tại trạm cân
1. **Mục đích:** Bảo vệ dữ liệu lượt cân đang thực hiện và hướng dẫn giải phóng xe thủ công khi có điện lại.
2. **Điều kiện trước khi thao tác:** Máy tính trạm cân và đầu hiển thị cân bị tắt nguồn hoàn toàn do mất điện lưới.
3. **Các bước thực hiện:**
   * **Bước 1:** Ngay lập tức tắt công tắc nguồn của máy tính trạm, máy in và đầu cân để tránh sốc điện khi có điện lưới trở lại.
   * **Bước 2:** Yêu cầu các xe đang đỗ trên bàn cân di chuyển ra ngoài bãi tạm để tránh cản trở giao thông.
   * **Bước 3:** Khi có điện lại: Bật nguồn đầu cân trước, kiểm tra số cân hiển thị ổn định ở mức 0 kg.
   * **Bước 4:** Bật máy tính trạm, mở phần mềm StationApp và đăng nhập lại.
   * **Bước 5:** Vào màn hình **Cân nội địa**, tìm lại phiên cân dở dang bằng cách gõ biển số xe ở thanh tìm kiếm phía trên để tiếp tục thực hiện cân tiếp.
4. **Kết quả mong đợi:** Phần mềm tự động khôi phục dữ liệu phiên cân dở dang đến thời điểm trước khi mất điện do cơ sở dữ liệu SQL Server có cơ chế Transaction tự bảo vệ. Mọi dữ liệu đã lưu không bị mất mát.
5. **Lỗi thường gặp:** Bật máy tính trước khi đầu cân khởi động xong dẫn đến phần mềm bị lỗi kết nối cổng COM.
6. **Cách xử lý:** Luôn tuân thủ bật đầu cân ổn định trước khi bật máy tính và khởi động phần mềm.
7. **Khi nào cần gọi quản lý / IT:** Khi có điện lại nhưng máy tính không lên nguồn, hoặc ổ cứng bị lỗi không khởi động được hệ điều hành Windows.

---

### TÌNH HUỐNG 16: Mất kết nối mạng Internet (Ngoại tuyến / Offline)
1. **Mục đích:** Đảm bảo trạm cân vẫn hoạt động cân xe bình thường khi không có mạng internet.
2. **Điều kiện trước khi thao tác:** Đèn kết nối Central API trên Trang chủ báo màu đỏ. Phần mềm hiển thị thông báo chuyển sang chế độ hoạt động ngoại tuyến.
3. **Các bước thực hiện:**
   * **Bước 1:** Nhân viên cân tiếp tục thực hiện các thao tác cân xe lần 1, lần 2, phân bổ khối lượng và in phiếu bình thường. Phần mềm sử dụng cơ sở dữ liệu SQL Server Express cục bộ nên hoàn toàn không bị ảnh hưởng bởi mạng.
   * **Bước 2:** Các bản ghi phiếu cân hoàn thành sẽ được hệ thống tự động đưa vào hàng đợi đồng bộ outbox cục bộ ở trạng thái PENDING.
   * **Bước 3:** Không tắt máy tính trạm cân, giữ nguyên phần mềm hoạt động.
   * **Bước 4:** Khi mạng internet có lại, hệ thống sẽ tự động quét hàng đợi outbox và đẩy dữ liệu lên ERP Server chạy ngầm mà không cần nhân viên thao tác gì thêm.
4. **Kết quả mong đợi:** Hoạt động cân xe không bị gián đoạn. Dữ liệu tự động đồng bộ đầy đủ lên ERP khi có mạng.
5. **Lỗi thường gặp:** Nhân viên hoảng loạn dừng cân xe khi thấy báo mất mạng, gây ùn tắc giao thông nghiêm trọng tại trạm cân.
6. **Cách xử lý:** Cứ tiếp tục vận hành cân xe bình thường theo đúng quy trình tiêu chuẩn.
7. **Khi nào cần gọi quản lý / IT:** Khi mất mạng kéo dài quá 24 giờ để IT kiểm tra đường truyền cáp quang hoặc modem mạng của trạm.

---

### TÌNH HUỐNG 17: Phần mềm trạm cân bị treo (đứng hình, click chuột không phản hồi)
1. **Mục đích:** Khởi động lại ứng dụng một cách an toàn mà không làm mất dữ liệu phiên cân đang làm việc.
2. **Điều kiện trước khi thao tác:** Phần mềm bị đơ, con trỏ chuột quay tròn liên tục hoặc nút bấm không phản hồi.
3. **Các bước thực hiện:**
   * **Bước 1:** Nhấn tổ hợp phím **Ctrl + Shift + Esc** để mở cửa sổ Task Manager của Windows.
   * **Bước 2:** Tại danh sách ứng dụng, click chuột phải vào **WeightStation / StationApp** và chọn **End Task** để buộc đóng phần mềm.
   * **Bước 3:** Chờ 5 giây, sau đó nhấp đúp chuột vào biểu tượng phần mềm trên Desktop để mở lại.
   * **Bước 4:** Đăng nhập lại hệ thống, vào màn hình **Cân nội địa**, tìm lại biển số xe đang cân dở dang để tiếp tục thực hiện thao tác.
4. **Kết quả mong đợi:** Phần mềm khởi động lại mượt mà, nạp đầy đủ dữ liệu phiên cân dở dang tại thời điểm trước khi bị treo.
5. **Lỗi thường gặp:** Nhân viên tự ý tắt nguồn máy tính bằng nút nguồn vật lý (nút cứng) trên thùng máy, dễ gây lỗi hỏng hệ điều hành Windows và mất dữ liệu.
6. **Cách xử lý:** Chỉ buộc đóng ứng dụng qua Task Manager như hướng dẫn ở trên.
7. **Khi nào cần gọi quản lý / IT:** Khi mở lại phần mềm nhưng lập tức bị treo lại liên tục, hoặc phần mềm báo lỗi không kết nối được cơ sở dữ liệu SQL Server cục bộ.

---

### TÌNH HUỐNG 18: Không đăng nhập được vào phần mềm (Báo sai tài khoản hoặc mật khẩu)
1. **Mục đích:** Xác thực và đăng nhập vào hệ thống để bắt đầu ca làm việc.
2. **Điều kiện trước khi thao tác:** Máy tính trạm đã bật, phần mềm hiển thị cửa sổ đăng nhập.
3. **Các bước thực hiện:**
   * **Bước 1:** Kiểm tra đèn bàn phím Caps Lock có đang bật không. Nếu có, tắt Caps Lock đi và gõ lại mật khẩu.
   * **Bước 2:** Kiểm tra xem bộ gõ tiếng Việt (Unikey/Vietkey) có đang ở chế độ gõ tiếng Việt không. Chuyển sang chế độ gõ tiếng Anh để tránh gõ mật khẩu bị tự động thêm dấu tiếng Việt.
   * **Bước 3:** Nhập chính xác tên đăng nhập (chữ thường, viết liền không dấu).
   * **Bước 4:** Yêu cầu nhân viên khác có tài khoản Admin đăng nhập vào màn hình **Cấu hình hệ thống** -> **Quản lý tài khoản** để kiểm tra tài khoản của bạn có bị khóa (IsActive = 0) hoặc thực hiện reset mật khẩu cấp mật khẩu mới cho bạn.
4. **Kết quả mong đợi:** Đăng nhập thành công vào màn hình Dashboard.
5. **Lỗi thường gặp:** Gõ sai ký tự mật khẩu do bật Caps Lock hoặc chế độ gõ tiếng Việt.
6. **Cách xử lý:** Tắt Caps Lock và chuyển Unikey sang chữ E trước khi nhập mật khẩu.
7. **Khi nào cần gọi quản lý / IT:** Khi tài khoản của bạn bị mất hoàn toàn trong hệ thống hoặc không có tài khoản Admin nào khác tại trạm để hỗ trợ reset mật khẩu.

---

### TÌNH HUỐNG 19: Nhân viên cân nhầm xe (Lưu nhầm số cân của xe này cho xe khác)
1. **Mục đích:** Hủy bỏ số cân bị sai lệch và thực hiện cân lại cho đúng phương tiện.
2. **Điều kiện trước khi thao tác:** Phiên cân bị nhầm lẫn chưa bấm hoàn tất cho xe ra và chưa in phiếu giao nhận cuối cùng.
3. **Các bước thực hiện:**
   * **Bước 1:** Tại màn hình cân chính, click chọn phiên cân bị nhầm lẫn.
   * **Bước 2:** Bấm nút **HỦY** (Hủy phiên cân). Xác nhận lý do hủy nhầm xe.
   * **Bước 3:** Hệ thống tự động trả các đơn cắt lệnh liên quan về trạng thái chờ ở màn hình **Danh sách xe vào**.
   * **Bước 4:** Hướng dẫn tài xế cho xe đỗ lại lên bàn cân và thực hiện quy trình cân từ đầu với đúng biển số xe đăng ký.
4. **Kết quả mong đợi:** Bản ghi cân nhầm bị hủy bỏ (trạng thái đổi thành **Đã hủy**), tạo lại lượt cân mới chính xác.
5. **Lỗi thường gặp:** Vẫn lưu số cân nhầm và in phiếu cho xe ra bãi, dẫn đến sai lệch tồn kho sản phẩm nghiêm trọng và tài xế cầm sai chứng từ giao hàng.
6. **Cách xử lý:** Luôn đối chiếu biển số thực tế ngoài sân với biển số hiển thị trên phần mềm trước khi bấm nút lấy số cân.
7. **Khi nào cần gọi quản lý / IT:** Khi xe cân nhầm đã hoàn tất ra bãi và đã in phiếu. Khi đó chỉ có tài khoản Admin hoặc IT mới được phép sửa đổi/hủy phiếu đã hoàn thành.

---

### TÌNH HUỐNG 20: Cân nhầm chiều xe vào/ra (Cân nhầm lần 1 thành lần 2)
1. **Mục đích:** Đưa lượt cân trở lại đúng trạng thái cân lần 1 để thực hiện đúng quy trình.
2. **Điều kiện trước khi thao tác:** Phiên cân bị nhầm lẫn chưa hoàn tất.
3. **Các bước thực hiện:**
   * **Bước 1:** Bấm nút **HỦY** để xóa bỏ lượt cân bị sai chiều.
   * **Bước 2:** Quay lại màn hình **Danh sách xe vào**.
   * **Bước 3:** Chọn lại cắt lệnh gốc, kiểm tra xem loại giao dịch là Nhập hàng hay Xuất hàng đã được chọn đúng chưa.
   * **Bước 4:** Bấm **CÂN NỘI ĐỊA** để tạo phiên cân mới và hướng dẫn xe đi qua cầu cân cân lại lần 1 đúng chiều quy định.
4. **Kết quả mong đợi:** Phiên cân được thực hiện đúng chiều vào/ra.
5. **Lỗi thường gặp:** Xe xuất hàng nhưng nhân viên bấm cân lần 1 khi xe đang đầy hàng, dẫn đến tính Net Weight bị âm khi xe ra.
6. **Cách xử lý:** Thực hiện hủy và cân lại từ đầu.
7. **Khi nào cần gọi quản lý / IT:** Khi hệ thống không cho hủy phiên cân do đơn hàng đã khóa trên ERP.

---

### TÌNH HUỐNG 21: Xe đổi rơ-mooc (mooc) khác so với đăng ký ban đầu từ ERP
1. **Mục đích:** Cập nhật biển số mooc thực tế vào hệ thống trước khi xe vào bàn cân lần 1.
2. **Điều kiện trước khi thao tác:** Xe chưa bấm CÂN NỘI ĐỊA ở màn hình Danh sách xe vào.
3. **Các bước thực hiện:**
   * **Bước 1:** Tại lưới danh sách xe chờ vào, click chọn đơn cắt lệnh của xe.
   * **Bước 2:** Di chuyển chuột đến ô nhập liệu **Mooc** trên Form thông tin xe phía trên.
   * **Bước 3:** Nhập biển số mooc thực tế mới của xe con (hệ thống sẽ tự gợi ý mooc cũ và điền hạn đăng kiểm mooc mới nếu có sẵn trong danh mục).
   * **Bước 4:** Nhập số đăng kiểm mooc mới và ngày hết hạn đăng kiểm mooc mới nếu hệ thống chưa có dữ liệu.
   * **Bước 5:** Bấm nút **Lưu** (SaveDetailCommand) và bấm **CÂN NỘI ĐỊA** để bắt đầu quy trình cân bình thường.
4. **Kết quả mong đợi:** Biển số mooc mới được lưu trữ và in đúng trên phiếu giao nhận.
5. **Lỗi thường gặp:** Tự ý gõ mooc mới nhưng quên cập nhật hạn đăng kiểm mooc dẫn đến hệ thống chặn cân do đăng kiểm cũ của mooc đã hết hạn.
6. **Cách xử lý:** Luôn kiểm tra kỹ hạn đăng kiểm của mooc mới nhập vào.
7. **Khi nào cần gọi quản lý / IT:** Khi ERP khóa không cho sửa đổi thông tin mooc đối với các đơn hàng xuất khẩu lớn.

---

### TÌNH HUỐNG 22: Xe đổi tài xế / người đại diện phương tiện
1. **Mục đích:** Cập nhật tên tài xế mới chịu trách nhiệm ký nhận phiếu cân.
2. **Điều kiện trước khi thao tác:** Xe chưa hoàn tất cân lần 2.
3. **Các bước thực hiện:**
   * **Bước 1:** Nếu xe đang ở màn hình Danh sách xe vào: Chọn đơn hàng, gõ tên tài xế mới vào ô **Tên tài xế** trên Form thông tin xe phía trên, bấm **Lưu**.
   * **Bước 2:** Nếu xe đang ở màn hình Cân nội địa (khi lưu cân lần 2): Khi bấm nút **LƯU** (SaveCapturedWeightCommand), hệ thống hiển thị hộp thoại đại diện xe. Nhân viên cân chọn dòng tài xế mới từ danh sách hoặc gõ tên tài xế mới trực tiếp vào ô nhập liệu đại diện và bấm **Xác nhận**.
4. **Kết quả mong đợi:** Tên tài xế mới hiển thị chính xác trên Phiếu Cân và Phiếu Giao Nhận in ra.
5. **Lỗi thường gặp:** Giữ nguyên tên tài xế cũ đã nghỉ việc dẫn đến tranh chấp pháp lý khi xảy ra thất thoát hàng hóa.
6. **Cách xử lý:** Luôn kiểm tra đối chiếu tên tài xế ký nhận thực tế tại bàn cân.
7. **Khi nào cần gọi quản lý / IT:** Khi danh sách gợi ý tài xế bị lỗi không hiển thị hoặc không cho phép nhập tên mới bằng tay.

---

### TÌNH HUỐNG 23: Xe hủy đơn hàng giữa chừng (đã cân lần 1 nhưng không lấy hàng nữa và muốn ra ngoài)
1. **Mục đích:** Hủy lượt cân dở dang một cách hợp lệ để trả đơn hàng về trạng thái gốc và giải phóng xe khỏi trạm.
2. **Điều kiện trước khi thao tác:** Xe đã cân lần 1 (trạng thái phiên cân là **Chờ cân lần 2**), đang nằm chờ trong bãi nhà máy.
3. **Các bước thực hiện:**
   * **Bước 1:** Vào màn hình **Cân nội địa**, nhấp chọn phiên cân của xe.
   * **Bước 2:** Tích chọn checkbox **K LẤY HÀNG** trên màn hình.
   * **Bước 3:** Hệ thống tự động chuyển đổi lượt cân sang chế độ xe ra không tải (`IsNoLoad = true`), bỏ qua cân lần 2.
   * **Bước 4:** Bấm nút **CHUYỂN XE RA** để giải phóng xe ra ngoài bãi trạm cân.
   * **Bước 5:** Đối với đơn cắt lệnh gốc: Đăng nhập tài khoản Admin, vào màn hình danh sách xe chờ, chọn đơn hàng đó và bấm hủy đơn hàng để trả trạng thái đơn về ERP hoặc đóng đơn.
4. **Kết quả mong đợi:** Xe được giải phóng ra bãi hợp lệ, phiên cân được ghi nhận cờ không tải và không tính sản lượng tịnh.
5. **Lỗi thường gặp:** Nhân viên bấm HỦY phiên cân thông thường thay vì đánh dấu cờ không tải, khiến xe đi ra ngoài cổng trạm bảo vệ không có phiếu kiểm soát.
6. **Cách xử lý:** Làm theo các bước đánh dấu không tải ở trên để in phiếu xe ra không tải cấp cho bảo vệ kiểm soát cổng.
7. **Khi nào cần gọi quản lý / IT:** Khi đơn hàng bị treo không cho hủy trên phần mềm trạm cân.

---

### TÌNH HUỐNG 24: Xe đã in phiếu hoàn thành nhưng phát hiện thông tin trên phiếu bị sai và cần chỉnh sửa
1. **Mục đích:** Sửa đổi thông tin phụ (tên tài xế, ghi chú, lề in) của phiếu cân đã hoàn thành và in lại phiếu mới.
2. **Điều kiện trước khi thao tác:** Phiên cân đã ở trạng thái **Đã hoàn tất** và nằm ở **Danh sách xe ra**. Hệ thống **tuyệt đối chặn không cho sửa số cân** (khối lượng cân 1, cân 2, Net Weight) để chống gian lận. Chỉ cho phép sửa đổi các trường thông tin phi số liệu cân.
3. **Các bước thực hiện:**
   * **Bước 1:** Vào màn hình **Danh sách xe ra**.
   * **Bước 2:** Chọn xe cần chỉnh sửa, bấm nút **XEM CHI TIẾT** (ShowDetailsCommand).
   * **Bước 3:** Yêu cầu tài khoản **ADMIN** thực hiện sửa đổi các thông tin được phép sửa (Tên tài xế, Ghi chú) trên Form chi tiết.
   * **Bước 4:** Bấm **Lưu**.
   * **Bước 5:** Chọn dòng xe và bấm nút **IN PC** hoặc **IN PGN** trên thanh công cụ.
4. **Kết quả mong đợi:** Phiếu mới được in ra với thông tin đã sửa đổi chính xác.
5. **Lỗi thường gặp:** Nhân viên cố tình tìm cách sửa số cân tịnh của phiếu đã hoàn thành, điều này là bất khả thi trên phần mềm trạm cân.
6. **Cách xử lý:** Nếu số cân bị sai hoàn toàn do lỗi kỹ thuật cân nhầm, buộc phải báo Quản lý trạm để tiến hành quy trình hủy phiếu đặc biệt (Void Ticket) và cân lại xe từ đầu.
7. **Khi nào cần gọi quản lý / IT:** Khi cần hủy hẳn một phiếu cân đã hoàn tất đồng bộ lên ERP để làm lại phiếu mới.

---

### TÌNH HUỐNG 25: Xe cân xong hoàn tất nhưng dữ liệu không đồng bộ lên server (Bị kẹt ở Outbox)
1. **Mục đích:** Buộc hệ thống đồng bộ lại bản ghi bị lỗi lên ERP Server.
2. **Điều kiện trước khi thao tác:** Đèn API trên Trang chủ báo đỏ hoặc Trang chủ báo số lượng bản ghi Outbox chờ đồng bộ tăng liên tục không giảm.
3. **Các bước thực hiện:**
   * **Bước 1:** Đăng nhập tài khoản **ADMIN** -> Vào **Cấu hình hệ thống** -> **Cấu hình đồng bộ**.
   * **Bước 2:** Tại lưới danh sách outbox, kiểm tra các dòng có trạng thái `SYNC_FAILED`. Click chọn dòng bị lỗi và xem chi tiết lỗi hiển thị ở cột **Lỗi cuối** (ColLastError).
   * **Bước 3:** Nếu lỗi do mất mạng tạm thời: Bấm nút **ĐỒNG BỘ NGAY** ở phía trên để hệ thống quét và gửi lại toàn bộ.
   * **Bước 4:** Nếu lỗi do dữ liệu bị từ chối: Click chọn dòng, bấm nút đồng bộ lại dòng này (ResyncSelectedCommand). Hệ thống sẽ tự động tạo lại payload JSON mới nhất từ DB cục bộ và đưa về hàng đợi gửi lại.
4. **Kết quả mong đợi:** Bản ghi outbox chuyển sang trạng thái đồng bộ thành công và số lượng hàng chờ đồng bộ về 0.
5. **Lỗi thường gặp:** Không theo dõi hàng chờ outbox dẫn đến cuối tháng số liệu trạm cân lệch hoàn toàn với số liệu báo cáo trên ERP tổng.
6. **Cách xử lý:** Mỗi cuối ca làm việc, nhân viên Admin bắt buộc phải vào kiểm tra hàng đợi outbox để đảm bảo không có bản ghi nào bị lỗi tồn đọng.
7. **Khi nào cần gọi quản lý / IT:** Khi bấm đồng bộ lại mà hệ thống báo lỗi HTTP 500 hoặc lỗi bảo mật API Key không hợp lệ.

---

# PHẦN 4. LOGIC RẼ NHÁNH NGHIỆP VỤ (NẾU... THÌ...)

Để phần mềm hoạt động trơn tru và bảo mật, các logic nghiệp vụ sau đây được lập trình tự động trên hệ thống:

* **NẾU** xe chưa hoàn thành cân lần 1 $\rightarrow$ **THÌ** hệ thống chặn đứng không cho phép thực hiện cân lần 2 (nút **CÂN LẦN 2** bị khóa).
* **NẾU** xe có tải trọng tịnh thực tế (Net Weight) vượt quá tải trọng thiết kế cho phép lưu hành của xe (TTCP + 10%) $\rightarrow$ **THÌ** hệ thống hiển thị cảnh báo quá tải màu đỏ, khóa nút **CHUYỂN XE RA** và chỉ mở khóa sau khi nhân viên cân bấm chọn nút **TÁCH TẢI** để thực hiện tách phiếu cân con hoặc được Admin duyệt phương án Không tách tải.
* **NẾU** thiết bị cân cổng COM gặp sự cố mất kết nối $\rightarrow$ **THÌ** phần mềm cho phép Quản trị viên (ADMIN) chuyển sang chế độ **Cân tay** để tự nhập số cân bằng tay từ bàn phím. Đối với tài khoản Nhân viên cân (OPERATOR), ô nhập số cân tay sẽ bị chặn cứng hoàn toàn để chống gian lận tự ý sửa số cân.
* **NẾU** hạn đăng kiểm của xe hoặc rơ-mooc đã hết hạn so với ngày hiện tại $\rightarrow$ **THÌ** hệ thống sẽ chặn không cho nhân viên bấm nút **CÂN NỘI ĐỊA** và khóa hoàn toàn việc tạo phiên cân mới cho phương tiện đó.
* **NẾU** xe quay lại cân lần 2 có sản lượng thực tế vượt quá sản lượng còn lại chưa bốc của đơn cắt lệnh xuất khẩu lớn $\rightarrow$ **THÌ** hệ thống hiển thị hộp thoại cảnh báo số lượng âm và yêu cầu nhân viên xác nhận đồng ý thì mới cho phép lưu số cân.
* **NẾU** xe thực hiện cân lần 1 trong vòng 24 giờ qua và nhân viên điền mã số lượt cân cũ vào ô lượt cân $\rightarrow$ **THÌ** hệ thống hiển thị hộp thoại cho phép lựa chọn kế thừa lại số cân lần 1 cũ (Carry Forward Weight 1) để gộp tiếp cắt lệnh mới mà không bắt tài xế phải xuống xe cân lại lần 1.
* **NẾU** đến thời điểm **03:00 AM** hàng ngày $\rightarrow$ **THÌ** tiến trình chạy ngầm của phần mềm tự động thực hiện sao lưu cơ sở dữ liệu cục bộ ra tệp tin `.bak` và tự động xóa các tệp tin sao lưu cũ hơn 10 ngày để giải phóng bộ nhớ đĩa cứng.

---

# PHẦN 5. CÁC LƯU Ý VÀ RÀNG BUỘC QUAN TRỌNG

## 5.1 Những lỗi nhân viên hay gặp
* **Quên chọn đúng loại sản phẩm hàng bao/hàng rời:** Dẫn đến hệ thống không tính toán đúng dung sai hàng bao và số lượng bao quy đổi.
* **Quên tắt máy xe khi cân:** Làm cho số cân bị dao động liên tục và không thể bấm lưu số cân ổn định.
* **In sai phôi giấy:** Đặt phôi in Phiếu cân tổng hợp (phôi lớn) vào khay in Phiếu giao nhận (phôi nhỏ) gây rách giấy và hỏng máy in.

## 5.2 Những thao tác bị cấm
* **Tuyệt đối cấm tự ý nhập tay số cân:** Nhân viên cân (Operator) không được phép dùng chế độ cân tay hoặc tìm cách can thiệp phần cứng đầu cân để sửa số cân. Mọi hành vi dùng cân tay của Admin đều bị ghi lại trong Nhật ký kiểm toán.
* **Cấm tắt phần mềm khi đang đồng bộ outbox:** Có thể gây lỗi nghẽn hoặc trùng lặp dữ liệu trên ERP Server.
* **Cấm xóa tệp tin trong thư mục sao lưu dữ liệu cục bộ:** Trừ khi được IT hướng dẫn trực tiếp để giải phóng ổ đĩa.

## 5.3 Kiểm tra trước khi in phiếu và cho xe ra
1. Biển số xe thực tế đỗ ngoài sân phải khớp 100% với biển số xe hiển thị trên màn hình phần mềm.
2. Tên tài xế thực tế ký nhận phải trùng với tên tài xế trên phần mềm.
3. Khối lượng hàng (Net Weight) phải hợp lệ (lớn hơn 0 kg).
4. Dữ liệu outbox của lượt cân phải ở trạng thái sẵn sàng đồng bộ.

---

# PHẦN 6. CÂU HỎI THƯỜNG GẶP (FAQ) - 30 CÂU HỎI THỰC TẾ

### Q1: Vì sao số cân lớn hiển thị trên phần mềm bị lệch 10-20 kg so với thực tế?
**Trả lời:** Bàn cân có thể bị dính bùn đất, rác kẹt dưới gầm dầm cân hoặc do thời tiết mưa gió ẩm ướt làm ảnh hưởng đến cảm biến lực (loadcell). Nhân viên cần quét dọn sạch sẽ bàn cân và gầm bàn cân. Nếu vẫn lệch, báo quản lý để hiệu chuẩn lại bàn cân.

### Q2: Tại sao tôi không thể nhấn được nút "CÂN LẦN 1" hoặc "CÂN LẦN 2"?
**Trả lời:** Nút lấy cân chỉ hoạt động khi số cân ở trạng thái **ỔN ĐỊNH**. Nếu số cân báo **DAO ĐỘNG** (do xe chưa dừng hẳn, động cơ xe còn nổ gây rung lắc bàn cân hoặc gió lớn thổi vào thùng xe), nút bấm sẽ bị phần mềm khóa tự động. Yêu cầu tài xế tắt máy và đứng im xe trên bàn cân.

### Q3: Làm thế nào khi xe đổi mooc khác so với đăng ký ERP?
**Trả lời:** Tại màn hình **Danh sách xe vào**, chọn đơn hàng của xe, di chuyển chuột đến ô nhập liệu **Mooc** trên Form thông tin xe phía trên, gõ biển số mooc thực tế mới của xe rồi bấm **Lưu** trước khi bấm **CÂN NỘI ĐỊA**.

### Q4: Vì sao hệ thống chặn không cho tạo phiên cân và báo lỗi "Hạn ĐK Xe" hoặc "Hạn ĐK Mooc" báo đỏ?
**Trả lời:** Phần mềm có cơ chế tự động đối chiếu ngày hết hạn đăng kiểm xe và mooc trong danh mục phương tiện. Nếu ngày hết hạn nhỏ hơn ngày hiện tại, hệ thống chặn cứng không cho cân để đảm bảo an toàn giao thông đường bộ. Tài xế bắt buộc phải xuất trình giấy đăng kiểm mới để Admin cập nhật lại hạn đăng kiểm trên phần mềm thì mới cho cân.

### Q5: Khi nào thì tôi cần bấm nút "PHÂN BỔ"?
**Trả lời:** Bấm nút **PHÂN BỔ** khi xe của bạn chở gộp nhiều đơn hàng (nhiều cắt lệnh) trên cùng một chuyến xe con. Sau khi lấy số cân lần 2 xong, nút PHÂN BỔ sẽ sáng lên và bạn bắt buộc phải bấm vào để chia khối lượng tịnh thực tế cho từng đơn hàng trước khi in phiếu.

### Q6: Tôi phân bổ khối lượng nhưng phần mềm báo lỗi lệch khối lượng không cho lưu?
**Trả lời:** Tổng khối lượng phân bổ cho các đơn hàng chi tiết của xe phải khớp **chính xác 100%** (lệch 0 kg) với khối lượng tịnh Net Weight thực tế cân được của xe đó. Kiểm tra và điều chỉnh lại số lượng phân bổ của từng dòng cho khớp.

### Q7: Tại sao tôi không tìm thấy đơn cắt lệnh của xe vừa đỗ ngoài cổng?
**Trả lời:** Đơn cắt lệnh từ ERP có thể chưa được điều độ ký duyệt hoặc đường truyền mạng internet từ ERP tổng xuống trạm cân bị mất kết nối tạm thời. Nhân viên bấm nút **LÀM MỚI** ở góc trên thanh công cụ để quét lại dữ liệu mới nhất. Nếu vẫn không thấy, báo bộ phận ERP kiểm tra trạng thái đơn hàng.

### Q8: Tôi có quyền tự tạo đơn cân lẻ thủ công trực tiếp trên phần mềm không?
**Trả lời:** Có. Đối với các đơn hàng cân lẻ hoặc nguyên vật liệu nhập khẩu tự do chưa có trên ERP, nhân viên bấm nút **LÀM MỚI** để clear Form thông tin xe phía trên, chọn loại giao dịch, nhập thông tin xe và bấm lưu để tự tạo đơn cân thủ công (Source = MANUAL).

### Q9: Làm thế nào khi xe bị quá tải trọng cho phép (TTCP) lưu hành?
**Trả lời:** Bấm nút **TÁCH TẢI** trên màn hình cân chính. Chọn phương án **XÁC NHẬN TÁCH TẢI** sau khi hệ thống tự động đề xuất chia khối lượng tịnh của chuyến thành 2 phiếu cân con nhỏ hợp lệ tải trọng, hoặc hướng dẫn tài xế quay lại kho bãi hạ bớt tải trọng xuống rồi cân lại.

### Q10: Khi tách tải tự động, tỷ lệ chia khối lượng của 2 phiếu cân con được tính thế nào?
**Trả lời:** Phần mềm tự động tính toán đề xuất phương án chia đôi hoặc chia theo tỷ lệ tải trọng cho phép của xe sao cho cả hai phiếu đều nhỏ hơn ngưỡng tải tối đa. Tỷ lệ này kết hợp với hệ số ngẫu nhiên dao động nhỏ (từ 0.0001 đến 0.0025) để đảm bảo số liệu tách ra tự nhiên và hợp lệ pháp lý.

### Q11: Tài khoản Operator (Nhân viên cân) có được dùng chức năng Cân tay (nhập số thủ công) không?
**Trả lời:** Không. Chức năng Cân tay chỉ dành riêng cho tài khoản **ADMIN (Quản trị viên)** sử dụng khi cổng COM kết nối đầu cân bị hỏng phần cứng hoặc đứt dây truyền thông để tránh ùn tắc trạm cân. Nhân viên Operator bắt buộc phải cân tự động.

### Q12: Mọi thao tác sử dụng Cân tay có được ghi lại không?
**Trả lời:** Có. Hệ thống tự động ghi nhận tài khoản thực hiện, thời gian thực hiện, số cân nhập tay và trạng thái vào bảng nhật ký kiểm toán cục bộ để phục vụ công tác thanh tra chống gian lận định kỳ của ban giám đốc.

### Q13: Tôi lỡ bấm nhầm HỦY phiên cân thì đơn hàng gốc có bị mất không?
**Trả lời:** Không. Khi bấm **HỦY** trên màn hình cân chính, lượt cân dở dang đó sẽ bị xóa bỏ (trạng thái đổi thành **Đã hủy**), nhưng các đơn cắt lệnh gốc liên quan sẽ tự động được giải phóng và quay về trạng thái chờ (**Đã đăng ký**) ở **Danh sách xe vào** để bạn có thể tiến hành cân lại bình thường.

### Q14: Làm thế nào để in lại một phiếu cân cũ đã hoàn thành từ hôm trước?
**Trả lời:** Vào màn hình **Danh sách xe ra** -> Chọn ngày hoàn thành của xe con đó ở ô DatePicker **Thời gian xong:** -> Nhập biển số xe tìm kiếm -> Chọn dòng xe trên lưới dữ liệu và bấm nút **IN PC**.

### Q15: Làm thế nào để điều chỉnh vị trí chữ in bị lệch dòng trên phiếu in giấy?
**Trả lời:** Đăng nhập tài khoản Admin -> Vào **Cấu hình hệ thống** -> **Cấu hình in** -> Chọn phôi cần sửa và bấm **Cấu hình**. Nhập tăng giá trị `Offset X` (để đẩy chữ sang phải) hoặc `Offset Y` (để đẩy chữ xuống dưới) rồi bấm **Lưu**.

### Q16: Tại sao Trang chủ báo số lượng Outbox Pending tăng liên tục và không giảm về 0?
**Trả lời:** Hệ thống đang bị mất kết nối mạng internet hoặc mất kết nối tới Central Server ERP. Hệ thống vẫn lưu dữ liệu cục bộ an toàn. Khi đường truyền mạng hoạt động bình thường trở lại, hệ thống sẽ tự động đồng bộ hàng đợi outbox chạy ngầm lên server.

### Q17: Làm sao để kiểm tra kết nối API tới server ERP hoạt động tốt hay không?
**Trả lời:** Vào **Cấu hình hệ thống** -> **Tham số hệ thống** -> Bấm nút **KIỂM TRA KẾT NỐI**. Phần mềm sẽ tự động gửi gói tin thử nghiệm và hiển thị thông báo kết quả kết nối thành công hay thất bại.

### Q18: Cơ sở dữ liệu của trạm cân có được tự động sao lưu an toàn không?
**Trả lời:** Có. Hệ thống tích hợp dịch vụ chạy ngầm tự động sao lưu cơ sở dữ liệu cục bộ vào lúc **03:00 AM** hàng ngày ra tệp tin `.bak` lưu trữ tại thư mục chỉ định và tự động xóa các file sao lưu cũ hơn 10 ngày để tránh đầy bộ nhớ.

### Q19: Tôi có thể chủ động sao lưu cơ sở dữ liệu ngay lập tức mà không đợi đến 3 giờ sáng không?
**Trả lời:** Có. Đăng nhập tài khoản Admin -> Vào **Cấu hình hệ thống** -> **Tham số hệ thống** -> Nhấp vào nút **SAO LƯU NGAY**. Hệ thống sẽ ngay lập tức tạo một tệp tin sao lưu mới trong thư mục lưu trữ.

### Q20: Cắt lệnh tạm xuất khẩu (CL-TAM-####) dùng để làm gì?
**Trả lời:** Dùng trong luồng cân xuất khẩu đơn lớn, khi xe con đã vào bàn cân lần 1 bốc xếp hàng nhưng hệ thống ERP chưa kịp duyệt tạo mã cắt lệnh xuất khẩu thật. Nhân viên cân tạo cắt lệnh tạm để cân trước và map sang cắt lệnh thật sau khi ERP đồng bộ xuống.

### Q21: Làm cách nào để map cắt lệnh tạm sang cắt lệnh thật?
**Trả lời:** Khi cắt lệnh thật được đồng bộ xuống, nhân viên chọn cắt lệnh thật ở màn **Danh sách xe vào** và bấm **CÂN XUẤT KHẨU**. Hệ thống phát hiện cắt lệnh tạm đang hoạt động và hiển thị bảng đối chiếu map. Chọn đúng mã cắt lệnh tạm tương ứng và bấm Xác nhận map.

### Q22: Tôi bấm nhầm cho xe ra theo diện "KHÔNG LẤY HÀNG" (No Load), tôi có cân lại được xe đó không?
**Trả lời:** Xe sẽ chuyển sang danh sách xe ra với trạng thái không tải. Nếu muốn cân lại xe đó, bạn phải tìm đơn cắt lệnh gốc ở danh sách xe chờ vào (nếu đơn đã hoàn thành thì báo Admin mở khóa/phục hồi đơn) để bắt đầu lại lượt cân.

### Q23: Dung sai cho phép đối với hàng đóng bao là bao nhiêu?
**Trả lời:** Mặc định được cấu hình là **1.75 kg trên mỗi bao** (hoặc cấu hình động trong tham số hệ thống `ToleranceKgPerBag`). Ví dụ xe chở 500 bao xi măng, dung sai cho phép là: $500 \times 1.75 = 875\text{ kg}$. Nếu khối lượng tịnh thực tế lệch quá 875 kg so với kế hoạch bốc 25 tấn, hệ thống sẽ cảnh báo vượt dung sai.

### Q24: Tôi có thể bỏ qua cảnh báo vượt dung sai hàng bao để lưu phiếu cân được không?
**Trả lời:** Được. Đối với nhân viên cân (Operator), hệ thống hiển thị cảnh báo nhưng cho phép check chọn **K LẤY ĐỦ SỐ LƯỢNG** trên màn hình để bỏ qua cảnh báo dung sai hàng bao khi lưu. Hành vi này sẽ được ghi log tự động kèm tên tài khoản người thực hiện để phục vụ hậu kiểm.

### Q25: Làm thế nào khi xe con của đơn xuất khẩu lớn bị đổi sang đơn hàng xuất khẩu khác?
**Trả lời:** Chọn chuyến xe con cần chuyển trên lưới chuyến xe con phía dưới màn hình Cân xuất khẩu -> Bấm nút **CHUYỂN CHUYẾN** -> Chọn đơn xuất khẩu lớn đích từ danh sách -> Bấm **Xác nhận**.

### Q26: Tại sao cột khối lượng kế hoạch của chuyến xe xuất khẩu ở Danh sách xe ra lại hiển thị bằng khối lượng thực tế?
**Trả lời:** Vì đơn xuất khẩu lớn có khối lượng kế hoạch chung hàng nghìn tấn cho cả lô tàu/sà lan. Đối với từng chuyến xe con đơn lẻ, khối lượng kế hoạch hiển thị bằng khối lượng thực tế của chuyến đó để tránh việc hiển thị sai lệch số lượng lớn trên danh sách xe ra.

### Q27: Làm thế nào để xem lại cắt lệnh xuất khẩu đã chốt tổng và đã hoàn thành trên ERP?
**Trả lời:** Vào màn hình **Cân xuất khẩu**, tích checkbox **Đã hoàn thành** ở cùng dòng tiêu đề **DANH SÁCH CẮT LỆNH XUẤT KHẨU**. Lưới sẽ hiển thị thêm các cắt lệnh đã chốt tổng và có trạng thái ERP hoàn thành. Các dòng này chỉ phục vụ xem lại/đối chiếu, không dùng để tạo chuyến xe mới hoặc cân tiếp.

### Q28: Làm thế nào để xuất báo cáo sản lượng cân trong tháng ra file Excel?
**Trả lời:** Vào màn hình **Báo cáo** -> Chọn báo cáo nhập hàng hoặc báo cáo xuất hàng -> Chọn khoảng thời gian Ngày/Giờ bắt đầu và kết thúc -> Bấm **XUẤT BÁO CÁO** -> Chọn thư mục lưu file trên máy tính (mặc định gợi ý thư mục Downloads) và lưu file `.xlsx`.

### Q29: Tôi có thể sửa đổi số cân của phiếu cân đã in hoàn thành và cho xe ra không?
**Trả lời:** **Tuyệt đối không.** Hệ thống chặn cứng việc sửa đổi số cân lần 1, số cân lần 2 và Net Weight của các phiếu cân đã hoàn thành nhằm chống gian lận thương mại. Mọi chỉnh sửa chỉ được phép thực hiện trên các trường thông tin phi số liệu (Tên tài xế, Ghi chú).

### Q30: Làm thế nào khi phần mềm trạm cân bị đơ/treo không bấm được nút?
**Trả lời:** Nhấn tổ hợp phím **Ctrl + Shift + Esc** để mở cửa sổ Task Manager -> Chọn ứng dụng StationApp và bấm **End Task** để đóng phần mềm. Sau đó nhấp đúp chuột mở lại phần mềm trên Desktop và đăng nhập lại.

### Q31: Khi nào thì tôi cần liên hệ ngay với bộ phận IT để được hỗ trợ?
**Trả lời:** Khi máy tính không lên nguồn, bàn cân bị hỏng vật lý hoặc số cân nhảy liên tục không dừng khi xe đã tắt máy, máy in bị lỗi phần cứng hỏng sấy, hoặc cơ sở dữ liệu cục bộ bị lỗi không kết nối được.

---

# PHẦN 7. CHECKLIST THAO TÁC NHANH TẠI BÀN CÂN

*Nhân viên cân có thể in trang này ra giấy và dán tại bàn làm việc để tra cứu nhanh khi thao tác.*

### 7.1 CHECKLIST THAO TÁC XE VÀO (CÂN LẦN 1)
- [ ] 1. Kiểm tra biển số xe thực tế ngoài sân cân.
- [ ] 2. Tìm kiếm và chọn xe trên lưới **Danh sách xe vào** khớp biển số.
- [ ] 3. Kiểm tra hạn đăng kiểm xe/mooc trên Form thông tin xe phía trên (Đảm bảo chữ không báo đỏ).
- [ ] 4. Bấm **CÂN NỘI ĐỊA** để chuyển sang màn hình cân chính.
- [ ] 5. Hướng dẫn tài xế dừng hẳn xe đúng tâm bàn cân.
- [ ] 6. Quan sát camera đỗ xe, chờ số cân báo **"ỔN ĐỊNH"**.
- [ ] 7. Bấm nút **CÂN LẦN 1** để lấy số cân lần 1.
- [ ] 8. Bấm nút **LƯU** để lưu lại số cân lần 1 (Hệ thống tự động chụp ảnh camera đầu xe).
- [ ] 9. Bấm nút **IN PGN** cấp cho tài xế và hướng dẫn xe vào kho bốc hàng.

### 7.2 CHECKLIST THAO TÁC XE RA (CÂN LẦN 2)
- [ ] 1. Kiểm tra xe quay lại đỗ đúng tâm bàn cân ra.
- [ ] 2. Click chọn đúng biển số xe trên lưới **DANH SÁCH LƯỢT CÂN HOẠT ĐỘNG** phía dưới.
- [ ] 3. Chờ tài xế tắt máy xe hoàn toàn, chờ số cân báo **"ỔN ĐỊNH"**.
- [ ] 4. Bấm nút **CÂN LẦN 2** để lấy số cân lần 2.
- [ ] 5. Bấm nút **LƯU** để lưu lại số cân lần 2 (Hệ thống tự động chụp ảnh camera đuôi xe).
- [ ] 6. **Bắt buộc:** Bấm nút **PHÂN BỔ** để chia khối lượng tịnh (nếu xe gộp nhiều đơn).
- [ ] 7. Kiểm tra cảnh báo quá tải/cảnh báo dung sai (nếu có, thực hiện tách phiếu quá tải).
- [ ] 8. Chọn đúng tên tài xế ký nhận thực tế ở hộp thoại đại diện xe.
- [ ] 9. Bấm **IN PC** (in phôi lớn) giao cho tài xế.
- [ ] 10. Bấm **CHUYỂN XE RA** để giải phóng xe và đồng bộ dữ liệu.

### 7.3 CHECKLIST CUỐI CA LÀM VIỆC
- [ ] 1. Vào **Danh sách xe ra**, kiểm tra xem có xe nào dở dang trong ngày chưa hoàn thành không.
- [ ] 2. Vào **Cấu hình hệ thống** -> **Cấu hình đồng bộ**, đảm bảo không còn bản ghi nào bị lỗi `SYNC_FAILED` chưa xử lý.
- [ ] 3. Kiểm tra khay giấy máy in, nạp đầy đủ giấy in sẵn sàng cho ca sau.
- [ ] 4. Vệ sinh sạch sẽ bàn phím, màn hình máy tính trạm và dọn dẹp rác xung quanh bàn cân.
- [ ] 5. Thực hiện bàn giao ca trên phần mềm (Đăng xuất tài khoản của bạn để ca sau đăng nhập tài khoản của họ).

---

# PHẦN 8. BẢNG TRA CỨU SỰ CỐ NHANH (TROUBLESHOOTING)

| Hiện tượng lỗi | Nguyên nhân thường gặp | Cách xử lý nhanh | Khi nào cần gọi IT? |
| :--- | :--- | :--- | :--- |
| **Đèn Cổng Cân (COM) báo Đỏ** | Lỏng cáp truyền thông RS-232 kết nối máy tính với đầu hiển thị cân. | Rút đầu cáp cổng COM phía sau thùng máy tính ra cắm lại chặt. Chọn lại cổng COM trong tab cấu hình phần mềm. | Đã cắm lại cáp và cấu hình đúng cổng COM mà phần mềm vẫn không nhận tín hiệu thô. |
| **Đèn Camera báo Đỏ / Mất hình** | Mất nguồn điện cấp cho camera hoặc lỏng cáp mạng LAN kết nối camera. | Rút nguồn điện cấp cho camera ra cắm lại để khởi động lại camera. Khởi động lại phần mềm StationApp. | Camera hỏng phần cứng hoặc đứt dây cáp mạng LAN ngoài trời. |
| **Số cân hiển thị âm hoặc lệch số lớn** | Bàn cân bị kẹt đất đá ở khe co giãn hoặc bị ngập nước dưới gầm. | Dùng vòi nước xịt rửa khe bàn cân, quét sạch bùn đất trên mặt cân. | Đã vệ sinh sạch sẽ mặt cân nhưng số cân không trả về 0 kg khi bàn cân trống. |
| **Báo lỗi "Không thể in phiếu"** | Chưa chọn máy in mặc định trên phần mềm hoặc máy in bị offline trong Windows. | Vào Cấu hình hệ thống -> Cấu hình in, chọn lại đúng tên máy in đang hoạt động và bấm Lưu. | Máy in bị hỏng cơ cuốn giấy, lỗi phần cứng báo đèn đỏ liên tục. |
| **Bản in phiếu bị lệch dòng chữ** | Lệch thông số căn lề in offset của phôi in. | Vào Cấu hình hệ thống -> Cấu hình in -> Bấm Cấu hình phôi bị lệch -> Điều chỉnh Offset X/Y tăng hoặc giảm vài mm. | Đã chỉnh Offset X/Y rất nhiều lần nhưng chữ vẫn bị tràn rìa giấy. |
| **Số Outbox Pending tăng liên tục** | Mất mạng Internet tại trạm cân hoặc Server API trung tâm gặp sự cố. | Vẫn tiếp tục thực hiện cân xe bình thường. Dữ liệu sẽ tự động đẩy lên ERP khi có mạng lại. | Mạng internet tại trạm mất kết nối kéo dài quá 24 giờ. |

---

# PHẦN 9. BẢNG PHÂN QUYỀN THAO TÁC CƠ BẢN

Để đảm bảo an toàn bảo mật dữ liệu trạm cân, hệ thống phân chia quyền hạn chặt chẽ giữa 2 vai trò tài khoản:

| Chức năng vận hành | Vai trò tài khoản OPERATOR (Nhân viên cân) | Vai trò tài khoản ADMIN (Quản trị viên) |
| :--- | :---: | :---: |
| **Đăng nhập và xem Dashboard** | **ĐƯỢC PHÉP** | **ĐƯỢC PHÉP** |
| **Cân tự động (AUTO - Đọc cổng COM)** | **ĐƯỢC PHÉP** | **ĐƯỢC PHÉP** |
| **Cân tay (MANUAL - Tự gõ số cân)** | **BỊ CẤM** | **ĐƯỢC PHÉP** *(Ghi nhật ký kiểm toán)* |
| **Bỏ qua (Bypass) dung sai hàng bao** | **ĐƯỢC PHÉP** *(Ghi nhật ký kiểm toán)* | **ĐƯỢC PHÉP** *(Ghi nhật ký kiểm toán)* |
| **Hủy phiên cân dở dang** | **ĐƯỢC PHÉP** | **ĐƯỢC PHÉP** |
| **In và In lại phiếu cân / giao nhận** | **ĐƯỢC PHÉP** | **ĐƯỢC PHÉP** |
| **Cấu hình thiết bị (Cân, Camera, Phôi in)**| **BỊ CẤM** | **ĐƯỢC PHÉP** |
| **Quản lý tài khoản và reset mật khẩu** | **BỊ CẤM** | **ĐƯỢC PHÉP** |
| **Sao lưu và phục hồi dữ liệu cục bộ** | **BỊ CẤM** | **ĐƯỢC PHÉP** |
| **Xem Nhật ký hệ thống (Audit Logs)** | **BỊ CẤM** | **ĐƯỢC PHÉP** |

---
**HẾT TÀI LIỆU HƯỚNG DẪN**
