# TÀI LIỆU PHÂN TÍCH YÊU CẦU PHẦN MỀM CÂN (WEIGHT STATION)

## GIỚI THIỆU

### Mục đích tài liệu
Tài liệu Phân tích yêu cầu (PTYC) Phần mềm Cân nhằm trình bày một cách rõ ràng, chi tiết và có hệ thống các yêu cầu nghiệp vụ, tính năng chức năng và phi chức năng đối với hệ thống Phần mềm quản lý Trạm Cân (Weight Station / StationApp). Tài liệu này làm cơ sở thiết kế kiến trúc, phát triển phần mềm, xây dựng kịch bản kiểm thử (test cases) và nghiệm thu dự án.

### Định nghĩa thuật ngữ và các từ viết tắt

| **Thuật ngữ** | **Định nghĩa** | **Ghi chú** |
| :--- | :--- | :--- |
| ERP | Hệ thống hoạch định nguồn lực doanh nghiệp (Enterprise Resource Planning) | Hệ thống quản lý trung tâm |
| NPP | Nhà phân phối / Đại lý bán hàng | Đối tượng mua hàng |
| COM | Cổng nối tiếp truyền tin (Serial Port RS-232 / RS-485) | Kết nối đầu hiển thị cân |
| RTSP | Giao thức truyền tải thời gian thực cho video (Real-Time Streaming Protocol) | Kết nối camera IP |
| TTCP | Tải trọng cho phép của phương tiện theo đăng kiểm giao thông | Kiểm soát quá tải |
| Đơn cắt lệnh | cắt lệnh đã được phê duyệt xuất hoặc nhập từ hệ thống ERP (Cut Order) | Cơ sở đăng ký xe vào cân |
| Phiên cân | Quy trình cân xe vật lý đầy đủ gồm 2 lần cân: cân vào (cân lần 1) và cân ra (cân lần 2) | Mã lưu trữ: `weighing_sessions` |
| Local-First | Kiến trúc ưu tiên xử lý và lưu trữ dữ liệu tại máy trạm cục bộ trước khi đồng bộ | Đảm bảo offline vẫn chạy tốt |
| Autocomplete | Cơ chế tự động tìm kiếm và gợi ý thông tin thông minh khi gõ ký tự | Tối ưu hóa nhập liệu |

### Mô tả tài liệu
Tài liệu bao gồm các phần chính được tổ chức như sau:
* **Phần 1: Giới thiệu** – Trình bày mục đích tài liệu, thuật ngữ viết tắt và cấu trúc tài liệu.
* **Phần 2: Tổng quan về dự án** – Phát biểu bài toán, phân tích hoàn cảnh, hiện trạng, giải pháp đề xuất và giá trị kinh doanh mang lại.
* **Phần 3: Yêu cầu chức năng** – Mô tả quy trình vận hành cân xe tổng quan và chi tiết các yêu cầu chức năng (FR1 đến FR14) đáp ứng các nghiệp vụ của trạm cân.
* **Phần 4: Yêu cầu phi chức năng** – Trình bày các yêu cầu về hiệu năng, bảo mật, tính sẵn sàng và trải nghiệm người dùng (UX).


---

## TỔNG QUAN VỀ DỰ ÁN

### Phát biểu bài toán

#### Hoàn cảnh:
Ban Tổng Giám đốc yêu cầu hiện đại hóa toàn diện các trạm cân tại nhà máy chính, mỏ đá và mỏ sét nhằm kiểm soát chặt chẽ sản lượng xuất nhập hàng, ngăn chặn thất thoát hàng hóa, tuân thủ pháp luật về tải trọng giao thông và nâng cao năng suất giải phóng phương tiện.

#### Hiện trạng:
* **Hệ thống phân tán, lạc hậu**: Mỗi trạm cân đang chạy một phần mềm cũ độc lập, dữ liệu không được quản lý tập trung và giao diện vận hành khó thao tác, tốn nhiều thời gian nhập liệu thủ công.
* **Sai sót thông tin**: Nhân viên trạm cân phải tự gõ biển số xe, thông tin khách hàng/NPP và sản phẩm mà không kế thừa trực tiếp từ cắt lệnh trên ERP, dễ dẫn đến gõ sai ký tự, lệch số liệu đối soát tài chính cuối ngày.
* **Thiếu cơ chế kiểm soát**: Chưa có cảnh báo tự động về tải trọng cho phép (TTCP), hạn đăng kiểm phương tiện và kiểm tra dung sai xuất hàng bao, dẫn đến rủi ro pháp lý khi xe chở quá tải rời nhà máy và thất thoát tài sản do xuất thừa hàng.
* **Không có bằng chứng đối chiếu**: Chưa tích hợp camera chụp ảnh xe tự động tại thời điểm lưu số cân, gây khó khăn khi xảy ra tranh chấp khối lượng với khách hàng hoặc đơn vị vận chuyển.
* **Lập báo cáo thủ công**: Với luồng cân xuất khẩu, nhân viên phải cập nhật thủ công các lượt lấy hàng vào file excel theo dõi ngoài. Với luồng cân mỏ sét, nhân viên phải tổng hợp báo cáo sản lượng cho từng tàu thủ công từ dữ liệu cân rời rạc.
* **Rủi ro gián đoạn mạng**: Phần mềm cũ phụ thuộc hoàn toàn vào mạng Internet. Khi mất mạng kết nối với máy chủ ERP, toàn bộ hoạt động cân xe bị đình trệ, gây ách tắc giao thông nghiêm trọng tại cổng nhà máy.
* **Khó khăn bảo trì và nâng cấp**: Do phần mềm cũ được tặng kèm theo máy cân khi mua từ đơn vị cung cấp, không có quyền truy cập vào mã nguồn, không thể chỉnh sửa hoặc tùy chỉnh theo yêu cầu. 

#### Giải pháp:
* Xây dựng **phần mềm quản lý trạm cân tập trung (StationApp)** với giao diện WPF hiện đại, trực quan, hỗ trợ thao tác nhanh.
* Triển khai kiến trúc **Local-First** kết hợp hàng đợi đồng bộ tự động. Phần mềm ghi nhận dữ liệu xuống cơ sở dữ liệu local (SQL Server Express) để đảm bảo hoạt động ngoại tuyến bình thường khi mất mạng và tự động đồng bộ lên Server trung tâm qua RESTful API khi có mạng trở lại.
* Tích hợp tự động hóa phần cứng: Kết nối trực tiếp đầu hiển thị cân (cổng COM) và hệ thống camera IP giám sát (giao thức RTSP) để chụp ảnh xe trước/sau khi cân.
* Đồng bộ hai chiều với ERP: Tự động tải đơn cắt lệnh (Cut Order) được duyệt xuống trạm cân và đẩy kết quả khối lượng thực xuất lên ERP sau khi hoàn thành phiên cân.
* Áp dụng cơ chế lưu trữ **Master Data cục bộ** giúp gợi ý tìm kiếm thông minh và điền nhanh dữ liệu phương tiện, khách hàng.
* Tự động kiểm soát nghiệp vụ thông minh: Cảnh báo thời hạn đăng kiểm xe, đối chiếu tổng trọng lượng với TTCP để xử lý quá tải (cho phép tách phiếu), và kiểm tra dung sai khối lượng xuất hàng bao.
* Hỗ trợ các nghiệp vụ cân phức tạp: 
    * Một xe lấy nhiều sản phẩm
    * Một xe giao hàng cho nhiều NPP
    * Cân xuất khẩu cắt lệnh lớn chạy nhiều chuyến xe con độc lập
    * Cấp lại đơn đổi cắt lệnh ERP kế thừa dữ liệu cân lần 1
    * Cân xuất khẩu chuyên dụng gắn với từng chuyến xe lấy hàng và hoàn hàng rách vỡ, hỗ trợ tạo báo cáo xuất khẩu theo từng cắt lệnh.
    * Cân mỏ đá với cơ chế tự động thiết lập chế độ cân phù hợp để lưu trữ được Trọng lượng bì hiệu lực trong ngày, tự động tái sử dụng trọng lượng bì xe cho các lần cân tiếp theo, hỗ trợ hoàn hàng. 
    * Cân mỏ sét cho phép lưu trữ chuyến xe chở hàng cho từng tàu, hỗ trợ hoàn hàng. 
* Hỗ trợ cấu hình máy in và căn lề phiếu in linh hoạt trực tiếp trên giao diện quản trị.
* Cơ chế tự động cập nhật phiên bản mới từ xa.
* Đồng bộ lịch sử cân từ hệ thống phần mềm cũ, hỗ trợ hiển thị lịch sử cân cũ trên giao diện phần mềm mới.
* Hỗ trợ sửa dữ liệu cân, lưu lại lịch sử chỉnh sửa.

#### Giá trị mang lại:
* **Tối ưu hóa thời gian vận hành**: Giảm thời gian chờ đợi của mỗi xe tại trạm xuống dưới 2 phút/xe nhờ tự động điền dữ liệu cắt lệnh và cơ chế phân bổ nhanh.
* **Hoạt động liên tục 24/7**: Đảm bảo trạm cân vận hành ổn định trước mọi sự cố đứt cáp hoặc mất kết nối Internet. Dữ liệu được bảo toàn tuyệt đối nhờ cơ chế sao lưu tự động hàng ngày.
* **Phòng chống gian lận & Thất thoát**: Hình ảnh camera chụp rõ biển số xe lúc cân và cơ chế tự động chặn vượt dung sai hàng bao giúp hạn chế thất thoát hàng hóa.
* **Tuân thủ pháp luật**: Loại bỏ 100% rủi ro xe quá tải trọng đăng kiểm rời khỏi nhà máy nhờ cơ chế chặn in phiếu cân và hỗ trợ tách lượt cân thông minh.
* **Số hóa quy trình**: Loại bỏ hoàn toàn ghi chép sổ sách thủ công, tự động đối soát sản lượng thực tế tức thì cuối ca. Báo cáo tự động theo từng ca, ngày, tháng, quý, năm và theo từng cắt lệnh.
* **Hỗ trợ quản trị**: Nhân viên quản lý có thể theo dõi hoạt động của trạm cân từ xa, xem báo cáo và thống kê sản lượng.
* **Hỗ trợ đa dạng nghiệp vụ cân**: Tích hợp các nghiệp vụ cân phức tạp của doanh nghiệp giúp nâng cao hiệu quả sử dụng.
* **Dễ dàng bảo trì và nâng cấp**: Do phần mềm được xây dựng bởi đội ngũ kỹ sư của công ty nên dễ dàng tùy chỉnh và nâng cấp theo yêu cầu.

---

## YÊU CẦU CHỨC NĂNG

### Tổng quan quy trình Vận hành Trạm cân
Quy trình vận hành trạm cân đáp ứng các luồng công việc cụ thể sau:
1. **Luồng nhập hàng**: Xe chở nguyên liệu đầy tải đi vào cân lần 1 -> Tiến hành dỡ hàng tại kho -> Xe không tải quay lại cân lần 2 -> Hệ thống tự động tính khối lượng hàng thực nhận -> Lưu, in phiếu và tự động đồng bộ kết quả lên ERP.
2. **Luồng xuất hàng nội địa**: Xe rỗng đi vào cân lần 1 -> Tiến hành bốc xếp sản phẩm lên xe tại kho -> Xe đầy tải quay lại cân lần 2 -> Hệ thống kiểm tra dung sai hàng bao và kiểm tra quá tải trọng đường bộ -> Phân bổ khối lượng cho các cắt lệnh ghép -> In phiếu cân, phiếu giao nhận -> Đồng bộ dữ liệu thực xuất lên ERP.
3. **Luồng xuất hàng xuất khẩu**: Tàu cập cảng để lấy hàng số lượng lớn (hàng rời hoặc đóng bao). Phần mềm khởi tạo một phiên cân xuất khẩu tổng. Từng chuyến xe tải nhỏ chở hàng từ nhà máy ra cảng sẽ thực hiện quy trình cân xe 2 lần độc lập, số lượng cân được tự động cộng dồn lũy kế vào sản lượng xuất của tàu. Khi bốc xong hàng, thực hiện "Chốt tổng" để đẩy sản lượng lên ERP.
4. **Luồng cân mỏ đá**: Chuyên dùng cho xe chở đá nguyên liệu. Đầu ca, xe cân 2 lần (lúc đầy và lúc rỗng) để xác định chính xác Trọng lượng bì (trọng lượng xe không tải) hợp lệ trong ngày. Các chuyến chạy tiếp theo trong ca, xe chở đầy đá chỉ cần đỗ lên cân 1 lần duy nhất, hệ thống tự động trừ đi Trọng lượng bì đã lưu để tính ra khối lượng đá, giúp tăng tốc độ giải phóng xe.
5. **Luồng cân mỏ sét**: Đất sét nhập về cảng được vận chuyển bằng nhiều xe tải về kho của nhà máy. Mỗi chuyến xe tải chở sét sẽ được đăng ký chạy gắn với chuyến tàu sét tương ứng. Xe vận hành cân 2 lần để tính sản lượng thực tế và tự động cộng dồn lũy kế để chốt tổng sản lượng của cả tàu sét.

### Tổng hợp yêu cầu chức năng

| **Mã FR** | **Tên yêu cầu chức năng** | **Mô tả chi tiết** |
| :--- | :--- | :--- |
| **FR1** | Đồng bộ đơn cắt lệnh từ ERP | - Khi ERP duyệt đơn (duyệt CO), tự động gửi thông tin cắt lệnh xuống phần mềm cân.<br>- Khi cắt lệnh bị hủy hoặc sửa trên ERP (duyệt RA), tự động ẩn/xóa mềm cắt lệnh cũ trên phần mềm cân để tránh cân nhầm. |
| **FR2** | Danh sách xe vào | - Hiển thị danh sách cắt lệnh chờ cân đẩy từ ERP.<br>- Tìm kiếm nhanh biển số để đăng ký xe vào cân.<br>- Cho phép sửa thông tin cắt lệnh trước khi cân (gồm thông tin mooc, đăng kiểm, tải trọng tối đa cho phép - TTCP).<br>- Tạo nhanh thông tin cho xe nhập hàng hoặc xe cân dịch vụ lẻ và lưu Master Data cục bộ.<br>- Hỗ trợ gộp nhiều cắt lệnh đi cùng một xe để chuyển sang màn hình Cân nội địa hoặc Cân xuất khẩu.<br>- Hủy lượt cân cho xe không lấy hàng.<br>- Ghép nối cắt lệnh tạm với cắt lệnh chính thức từ ERP.<br>- Cấp lại cắt lệnh để kế thừa kết quả cân lần 1 cũ trong vòng 24 giờ. |
| **FR3** | Cân nội địa | - Tìm kiếm thông tin xe theo biển số, mã cắt lệnh.<br>- Thực hiện quy trình cân xe 2 lần (lưu cân lần 1, lưu cân lần 2) để tính khối lượng hàng thực tế.<br>- Hỗ trợ in Phiếu cân tổng hợp và Phiếu giao nhận chi tiết ngay sau khi cân xong.<br>- Hiển thị camera tại bàn cân và tự động chụp ảnh biển số trước/sau xe khi lưu cân.<br>- Phân bổ khối lượng tịnh cho nhiều cắt lệnh/khách hàng/NPP đi cùng một xe.<br>- Tự động kiểm soát quá tải (chặn in phiếu nếu quá tải, hỗ trợ duyệt bỏ qua hoặc tách thành các phiếu con hợp lệ).<br>- Cảnh báo vượt dung sai hàng đóng bao.<br>- Đánh dấu các trường hợp đặc biệt: xe không lấy đủ số lượng đặt, xe hủy cân sau khi cân lần 1, xe đi chuyền tải hàng ra tàu.<br>- Hỗ trợ Admin nhập tay số cân khi thiết bị lỗi.<br>- Hỗ trợ thêm cắt lệnh mới vào lượt cân đang chạy. |
| **FR4** | Cân xuất khẩu | - Tạo và sửa cắt lệnh tạm khi chưa có cắt lệnh chính thức từ ERP.<br>- Tạo các chuyến xe con vận chuyển hàng cho đơn xuất khẩu.<br>- Theo dõi sản lượng cắt lệnh: tổng lượng đặt, sản lượng đã xuất lũy kế, lượng còn lại (đơn vị: tấn và bao).<br>- Vận hành cân xe 2 lần tính khối lượng tịnh thực tế từng chuyến xe.<br>- Tích hợp hiển thị camera và tự động chụp ảnh xe trước/sau.<br>- Hỗ trợ sửa lỗi: xóa chuyến xe cân dở, chuyển chuyến xe cân nhầm sang đơn khác, cân hàng hoàn rách vỡ trả lại.<br>- Chuyển nhanh xe đã hoàn thành cân sang màn hình Danh sách xe ra.<br>- Nhập số lượng xuất không qua cân và thực hiện Chốt tổng sản lượng xuất khẩu để đẩy kết quả lên ERP. |
| **FR5** | Danh sách xe ra | - Hiển thị danh sách toàn bộ xe nhập, xuất nội địa và xuất khẩu đã hoàn thành cân.<br>- Cho phép kiểm tra trạng thái cắt lệnh trên ERP đã đóng hay chưa.<br>- Hỗ trợ nhân viên bổ sung đánh dấu xe không lấy đủ số lượng hoặc xe chuyền tải trực tiếp tại đây. |
| **FR6** | Cân mỏ đá | - Thực hiện cân 2 lần đầu ca để xác định và lưu Trọng lượng bì (xe rỗng) của ngày hôm đó.<br>- Các lượt cân đá tiếp theo trong ngày chỉ cần cân 1 lần (cân xe đầy tải), tự động trừ đi Trọng lượng bì đã lưu để ra lượng đá thực tế.<br>- Chỉ cho phép cân các xe có sẵn trong danh mục đăng ký để tránh gian lận.<br>- Hỗ trợ in phiếu, sửa nhanh biển số xe nhập nhầm và cân hoàn hàng đá lỗi. |
| **FR7** | Cân mỏ sét | - Khai báo thông tin tàu sét nhập khẩu và đăng ký các chuyến xe tải chở sét cho tàu tương ứng.<br>- Xe vận hành cân 2 lần để tính khối lượng sét thực nhận từng chuyến.<br>- Tự động cộng dồn lũy kế sản lượng đất sét đã nhập cho cả tàu.<br>- Hỗ trợ đầy đủ nghiệp vụ: cân tự động/cân tay, chụp ảnh, xóa chuyến, chuyển chuyến xe sang tàu khác, cân hoàn sét lỗi và chốt tổng tàu. |
| **FR8** | Báo cáo | - Trích xuất các loại báo cáo sản lượng ca/ngày.<br>- Báo cáo chi tiết các chuyến xe xuất khẩu, sản lượng sét theo tàu, sản lượng đá...<br>- Hỗ trợ xuất dữ liệu báo cáo ra file Excel. |
| **FR9** | Cấu hình hệ thống | - Giao diện dành riêng cho Admin để cài đặt phần cứng (cổng COM đầu cân, camera IP).<br>- Căn chỉnh mẫu in phiếu cân, phiếu giao nhận bằng cách thay đổi tọa độ trực tiếp trên màn hình.<br>- Thiết lập quy tắc nghiệp vụ: % dung sai hàng bao, tỷ lệ quá tải, giờ tự động sao lưu dữ liệu.<br>- Quản lý tài khoản nhân viên, phân quyền trạm cân; quản lý danh mục Master Data (xe, sản phẩm, khách hàng).<br>- Tra cứu lịch sử chỉnh sửa dữ liệu cân nhạy cảm và lịch sử cân từ hệ thống phần mềm cũ.<br>- Hỗ trợ đồng bộ lại các lượt cân bị lỗi mạng lên ERP. |
| **FR10** | Đồng bộ dữ liệu lên Server | - Dữ liệu cân và ảnh chụp xe được tự động đưa vào hàng đợi đồng bộ.<br>- Đồng bộ lên server trung tâm an toàn, có cơ chế chống trùng lặp dữ liệu (Idempotency). |

---

### Chi tiết các yêu cầu chức năng

#### FR1: Đồng bộ đơn cắt lệnh từ ERP
* **Mục đích**: Giúp trạm cân tự động nhận thông tin cắt lệnh đã được duyệt trên hệ thống ERP. Nhân viên trạm cân không cần phải gõ lại thông tin bằng tay, từ đó tránh hoàn toàn lỗi nhập sai biển số, sai tên sản phẩm hoặc chọn nhầm khách hàng.
* **Phạm vi áp dụng**: Tất cả các cắt lệnh xuất hàng.
* **Cách hoạt động**:
  * Khi nhân viên trạm cân CO cắt lệnh, hệ thống ERP sẽ tự động chuyển thông tin cắt lệnh đó sang phần mềm trạm cân. Thông tin bao gồm: biển số xe, mooc, tên tài xế, mã sản phẩm, loại hàng (bao hay rời), số lượng đăng ký và nhà phân phối.
  * Nhân viên trạm cân sẽ thấy thông tin xe chờ sẵn trên màn hình Danh sách xe vào.

#### FR2: Danh sách xe vào
* **Mục đích**: Là nơi nhân viên trạm cân quản lý, chuẩn bị thông tin và làm thủ tục cho các xe đang xếp hàng chờ cân.
* **Phạm vi áp dụng**: Tất cả các lệnh đẩy từ ERP và đơn nhập hàng được tạo thủ công.
* **Cách hoạt động**:
  * **Đăng ký xe vào**: Khi xe đến trạm, nhân viên chỉ cần gõ biển số xe để tìm kiếm cắt lệnh tương ứng được truyền từ ERP xuống, hoặc tạo thủ công đơn nhập hàng bằng cách nhập thông tin xe, sản phẩm, khách hàng...
  * **Kiểm tra đăng kiểm và tải trọng**: Phần mềm hiển thị thông tin đăng kiểm của xe (thời hạn đăng kiểm, tải trọng tối đa cho phép - TTCP). Nếu phát hiện xe đã quá hạn đăng kiểm hợp lệ, hệ thống sẽ cảnh báo đỏ nổi bật để nhân viên nhắc nhở tài xế.
  * **Hủy lượt cân**: Cho phép nhân viên bấm nút "Không lấy hàng" để đưa xe ra khỏi danh sách chờ nếu tài xế quyết định hủy đơn trước khi cân.
  * **Ghép cắt lệnh**: Cho phép gộp nhiều cắt lệnh (nhiều sản phẩm hoặc giao cho nhiều NPP) cho cùng một xe và chọn đi vào luồng cân nội địa.
  * **Map cắt lệnh tạm**: Cho phép ghép nối "cắt lệnh tạm" với cắt lệnh chính thức từ ERP.
  * **Dùng lại lượt cân cũ**: Nếu xe đã cân lần 1  nhưng cần RA cắt lệnh để sửa lại thông tin và sau đó thực hiện CO lại cắt lệnh trên ERP, nhân viên chỉ cần chọn cắt lệnh mới được đẩy xuống phần mềm cân và nhấn nút Cân nội địa để sử dụng lại lượt cân lần 1 cũ.

#### FR3: Cân nội địa
* **Mục đích**: Thực hiện cân xe 2 lần (lúc vào và lúc ra) để tính ra khối lượng hàng thực tế.
* **Cách hoạt động**:
  * **Cân lần 1 (Cân xe rỗng/xe đầy tải)**: Xe đỗ lên bàn cân. Nhân viên giám sát camera hiển thị trực tiếp trên màn hình phần mềm để xem xe đỗ chuẩn vị trí chưa. Khi số cân hiển thị ổn định, nhân viên nhấn nút lưu. Hệ thống chụp ảnh biển số trước/sau xe và lưu kết quả cân lần 1 (Weight1).
  * **Cân lần 2 (Cân xe đầy tải/xe rỗng)**: Xe chở hàng quay lại bàn cân. Hệ thống đọc số cân ổn định lần 2 (Weight2), chụp ảnh xe lần nữa và tính ra khối lượng hàng thực tế bằng hiệu số của hai lần cân. Sau đó, nhân viên bấm in Phiếu cân và Phiếu giao nhận trực tiếp từ màn hình.
  * **Kiểm soát dung sai**: Đối với hàng đóng bao, nếu cân thực tế lệch quá nhiều so với số lượng đặt (vượt dung sai cho phép), phần mềm hiển thị cảnh báo để nhân viên kiểm tra lại.
  * **Phân bổ sản lượng**: Sau khi cân xong, hệ thống phân bổ sản lượng cho các cắt lệnh. Nhân viên xác nhận phân bổ theo kế hoạch hoặc tích ưu tiên để phân bổ cho cắt lệnh nào trước hoặc nhập tay số lượng phân bổ cho các cắt lệnh.
  * **Kiểm soát quá tải**: Hệ thống đối chiếu tổng trọng lượng xe thực tế với tải trọng cho phép (TTCP 10%). Nếu quá tải, nhân viên phải xử lý bằng cách:
    * *Không tách phiếu*: Nếu xe quá tải quá ít hoặc tài xế yêu cầu không tách phiếu.
    * *Tách phiếu*: Phần mềm hỗ trợ tách khối lượng hàng thực tế thành 2 phiếu con để xe chạy an toàn trên đường.
  * **Các tính năng hỗ trợ vận hành**:
    * Cho phép đánh dấu xe không lấy đủ số lượng (xe chỉ lấy một phần hàng rồi đi ra) để khi kết thúc cắt lệnh, ERP ghi nhận đúng số lượng thực tế.
    * Đánh dấu xe không lấy hàng sau khi đã cân lần 2 để không tính lượt cân này vào báo cáo sản lượng.
    * Đánh dấu xe đi chuyền tải (chở xi măng ra tàu cảng) để xuất báo cáo riêng cho các chuyến chuyền tải này.
    * Hỗ trợ sửa biển số mooc, số niêm chì trực tiếp trước khi in phiếu.
    * Hỗ trợ thêm cắt lệnh mới vào lượt cân để lấy thêm hàng ghép xe.
    * Hỗ trợ Admin nhập tay số cân nếu đầu hiển thị cân bị lỗi.

#### FR4: Cân xuất khẩu
* **Mục đích**: Quản lý và theo dõi sản lượng cho các cắt lệnh xuất khẩu, được vận chuyển bằng nhiều chuyến xe con chạy liên tục nhiều ngày.
* **Cách hoạt động**:
  * **Cắt lệnh tạm**: Nếu tàu đã vào cảng bốc hàng nhưng ERP chưa tạo cắt lệnh, phần mềm cho phép tạo "Cắt lệnh tạm" để xe chạy trước, sau đó sẽ map lại với đơn ERP thật.
  * **Cộng dồn tự động**: Mỗi chuyến xe tải nhỏ chở hàng ra cảng sẽ thực hiện cân xe 2 lần. Hệ thống tự động cộng dồn khối lượng của tất cả các chuyến xe để tính ra tổng sản lượng đã xuất lũy kế và lượng hàng còn lại của tàu (hiển thị rõ số tấn và số bao).
  * **Xử lý sự cố & Hoàn hàng**:
    * Cho phép xóa chuyến xe đang cân dở (chưa cân lần 2).
    * Cho phép chuyển một chuyến xe cân nhầm sang cắt lệnh xuất khẩu khác mà không phải cân lại.
    * Hỗ trợ cân riêng lượng hàng bị rách vỡ, lỗi trả lại nhà máy (cân hoàn hàng).
  * **Map cắt lệnh & Chốt đơn**: Sau khi hoàn thành số lượng cho đơn hàng xuất khẩu, thực hiện tạo và đẩy cắt lệnh xuất khẩu từ ERP xuông phần mềm cân. Sau đó nhấn nút Chốt tổng, lúc này modal hiện lên cho phép nhập thêm số lượng xuất không qua cân (nếu có) và nhấn Chốt tổng để khóa sản lượng và đẩy số liệu thực xuất lên ERP.

#### FR5: Danh sách xe ra
* **Mục đích**: Lưu trữ, quản lý thông tin của các xe đã hoàn thành cân và rời trạm, giúp dễ dàng tra cứu nhanh và xem trạng thái kết thúc xuất hàng đồng bộ từ ERP.
* **Cách hoạt động**:
  * Hiển thị danh sách toàn bộ xe nhập, xuất nội địa và xuất khẩu đã hoàn thành cân 2 lần.
  * Nhân viên có thể xem nhanh trạng thái của cắt lệnh trên ERP (đã hoàn thành xuất hàng hay chưa mà không cần tra cứu lại trên ERP).
  * Hỗ trợ đánh dấu nhanh xe không lấy đủ số lượng hoặc xe chuyền tải trực tiếp tại màn hình này nếu nhân viên quên đánh dấu ở bước cân trước.
  * Hỗ trợ xem lại chứng từ, ảnh cân và in chứng từ cho các xe đã ra khỏi trạm.

#### FR6: Cân mỏ đá
* **Mục đích**: Tối ưu hóa quy trình cân đá nguyên liệu từ mỏ về trạm đập. Bằng cách lưu Trọng lượng bì (xe rỗng) một lần trong ngày, xe chở đá không cần phải cân lại trọng lượng bì nữa.
* **Phạm vi áp dụng**: Dành riêng cho trạm cân tại khu vực mỏ đá.
* **Cách hoạt động**:
  * **Xác định trọng lượng xe rỗng (Trọng lượng bì)**: Đầu ca, xe sẽ thực hiện cân 2 lần đầy đủ để lưu lại Trọng lượng bì chính xác của xe trong ngày hôm đó.
  * **Cân 1 lần**: Ở các chuyến chở đá tiếp theo trong ngày, xe chỉ cần cân 1 lần duy nhất lúc chở đầy đá (cân xe đầy tải). Phần mềm tự động trừ đi Trọng lượng bì đã lưu trước đó để tính ra khối lượng đá thực tế.
  * **Kiểm soát chặt chẽ**: Để tránh gian lận, hệ thống chỉ cho phép cân các xe đã được đăng ký từ trước (không tự động tạo xe mới khi nhập thông tin xe chưa có trong danh sách xe).
  * **Hỗ trợ vận hành**: Cho phép sửa biển số xe nếu nhân viên cân nhầm cho xe khác. 
  * **Hoàn hàng**: Trong trường hợp xe đã cân và đã ghi nhận trọng lượng đá, nhưng do sự cố phải hoàn hàng, thực hiện cân lại trọng lượng tổng để trừ đi số lượng đá đã cân.

#### FR7: Cân mỏ sét
* **Mục đích**: Quản lý việc cân đất sét nguyên liệu nhập về theo từng chuyến tàu.
* **Phạm vi áp dụng**: Trạm cân đất sét nguyên liệu.
* **Cách hoạt động**:
  * **Khai báo tàu sét**: Nhân viên khai báo thông tin chuyến tàu sét. Mỗi chuyến xe tải chở sét từ mỏ về cảng sẽ được đăng ký và liên kết với chuyến tàu tương ứng.
  * **Cân xe & Lũy kế**: Xe tải vận hành cân 2 lần để tính khối lượng đất sét thực nhận trên xe. Hệ thống tự động cộng dồn khối lượng của từng xe vào tổng sản lượng sét đã nhập của tàu.
  * **Hỗ trợ nghiệp vụ**: Cung cấp đầy đủ các tính năng: cân tự động/cân tay, chụp ảnh biển số trước/sau, xóa chuyến xe cân lỗi, chuyển chuyến xe sang tàu khác do nhầm lẫn, cân hoàn hàng và chốt tổng sản lượng.

#### FR8: Báo cáo
* **Mục đích**: Cung cấp số liệu báo cáo sản lượng xuất, nhập và vận hành trạm cân một cách nhanh chóng, chính xác theo ca làm việc, ngày, tháng, quý, năm hoặc theo từng cắt lệnh.
* **Cách hoạt động**:
  * Phần mềm tự động tổng hợp số liệu giao dịch cân tại trạm.
  * Nhân viên có thể dễ dàng truy cập và trích xuất các mẫu báo cáo sản lượng theo ca/ngày, báo cáo chi tiết các chuyến xe xuất khẩu, báo cáo sản lượng nhập sét theo tàu, báo cáo đá nguyên liệu... dưới dạng bảng biểu trực quan và xuất ra file Excel để phục vụ đối soát.

#### FR9: Cấu hình hệ thống
* **Mục đích**: Giao diện tập trung dành riêng cho quản trị viên (Admin) để cài đặt các thông số hoạt động của phần mềm, cấu hình máy in và thiết bị phần cứng trực tiếp mà không cần nhờ lập trình viên sửa mã nguồn.
* **Cách hoạt động**:
  * **Cấu hình phần cứng**: Thiết lập cổng kết nối với đầu cân (COM Port), địa chỉ camera IP (luồng RTSP, độ phân giải chụp ảnh).
  * **Căn chỉnh mẫu in phiếu**: Cho phép điều chỉnh trực tiếp tọa độ của mẫu in Phiếu cân và Phiếu giao nhận trên giao diện phần mềm.
  * **Cấu hình nghiệp vụ**: Thiết lập mức dung sai hàng đóng bao, tỷ lệ quá tải cho phép, giờ tự động sao lưu cơ sở dữ liệu.
  * **Quản trị người dùng & Master Data**: Thêm, sửa, xóa tài khoản nhân viên và phân quyền trạm cân; quản lý danh mục xe, sản phẩm, khách hàng.
  * **Tra cứu nâng cao**: Tra cứu lịch sử cân từ hệ thống phần mềm cũ (chỉ hiển thị xem, không cho sửa). Xem toàn bộ lịch sử chỉnh sửa các dữ liệu cân nhạy cảm để chống gian lận.
  * **Đồng bộ dữ liệu**: Hiển thị danh sách dữ liệu cần đồng bộ lên Server và trạng thái đồng bộ (thành công/thất bại). Nếu có dữ liệu thất bại, hệ thống sẽ hiển thị mã lỗi và cho phép người dùng thực hiện đồng bộ lại.
  * **Tự động cập nhật**: Cho phép kiểm tra và cài đặt phiên bản mới từ xa.

#### FR10: Đồng bộ dữ liệu lên Server
* **Mục đích**: Đảm bảo toàn bộ dữ liệu cân và hình ảnh chụp xe tại trạm cân được đẩy về máy chủ trung tâm an toàn và nhanh chóng, phục vụ đối soát tài chính của công ty.
* **Cách hoạt động**:
  * Nhờ kiến trúc **Local-First**, mọi dữ liệu cân và ảnh chụp trước tiên được ghi và lưu trữ an toàn dưới máy trạm local.
  * Hệ thống sử dụng một hàng đợi đồng bộ chạy ngầm. Khi máy tính có kết nối mạng Internet, tiến trình này tự động quét hàng đợi và gửi tuần tự dữ liệu lên máy chủ trung tâm.
  * Mỗi lượt cân được gán một mã nhận diện duy nhất (Idempotency Key). Khi đồng bộ lên Server, API sử dụng mã này để đối chiếu. Nếu mạng chập chữa gây gửi lặp yêu cầu, máy chủ trung tâm chỉ ghi nhận đúng một lần duy nhất, tránh tình trạng trùng lặp số liệu.

---

## YÊU CẦU PHI CHỨC NĂNG

Hệ thống Phần mềm Cân phải đáp ứng các tiêu chuẩn phi chức năng sau để đảm bảo tính ổn định, an toàn và hiệu năng vận hành thực tế tại nhà máy:

| **Mã NFR** | **Loại yêu cầu** | **Diễn giải yêu cầu chi tiết** |
| :--- | :--- | :--- |
| **NFR1** | **Hiệu năng & Phản hồi** | * Thời gian hiển thị số cân từ đầu đọc cân lên màn hình phải gần như thời gian thực (độ trễ < 200ms).<br>* Thời gian phản hồi xử lý các thao tác lưu cân, kiểm tra dung sai, và in phiếu phải **dưới 3 giây**.<br>* Thời gian tải danh sách đơn đặt hàng từ database local lên lưới hiển thị UI phải dưới 1 giây. |
| **NFR2** | **Bảo mật & Phân quyền (RBAC)** | * Phân quyền rõ ràng theo vai trò người dùng:<br>  * *Nhân viên cân*: Chỉ thực hiện cân tự động, phân bổ khối lượng, yêu cầu bỏ qua dung sai và in phiếu.<br>  * *Quản trị viên trạm*: Được phép cấu hình thiết bị (cổng COM, RTSP camera), thực hiện cân thủ công (nhập tay số cân khi đầu hiển thị lỗi), phê duyệt bỏ qua quá tải trọng và sao lưu dữ liệu.<br>* Toàn bộ mật khẩu tài khoản phải được mã hóa trước khi lưu trữ. |
| **NFR3** | **Kiểm soát & Lưu vết (Audit Log)** | * Hệ thống phải tự động ghi nhận nhật ký kiểm toán (Audit Log) chi tiết cho các hành động nhạy cảm:<br>  * Cân thủ công (nhập tay khối lượng).<br>  * Phê duyệt bỏ qua quá tải trọng xe.<br>  * Nhấn nút bỏ qua vượt dung sai hàng bao.<br>  * In lại phiếu cân cũ.<br>  * Hủy hoặc sửa đổi trạng thái phiên cân.<br>* Thông tin ghi nhận gồm: Thời gian, Tài khoản thực hiện, Máy trạm, Giá trị trước/sau khi thay đổi và Lý do thực hiện. Nhật ký này không được phép sửa hoặc xóa bởi bất kỳ tài khoản nào. |
| **NFR4** | **Tính sẵn sàng & Hoạt động offline** | * Phần mềm trạm cân phải đạt tỷ lệ sẵn sàng hoạt động tại máy trạm là **99.9%**.<br>* Nhờ cơ chế Local-First, hệ thống phải đảm bảo khả năng hoạt động ngoại tuyến (offline) hoàn toàn ổn định liên tục trong tối thiểu **30 ngày** khi mất mạng Internet mà không làm gián đoạn quy trình cân xe và in phiếu tại trạm. |
| **NFR5** | **Trải nghiệm người dùng (UX/UI)** | * Giao diện vận hành chính phải trực quan, dễ nhìn trong điều kiện ánh sáng ngoài trời trạm cân.<br>* **Số hiển thị cân lớn**: Số cân hiện thời từ đầu hiển thị phải được hiển thị bằng chữ cỡ lớn nổi bật (kích thước chữ tối thiểu **36pt**, màu sắc tương phản cao) để nhân viên dễ dàng quan sát từ xa.<br>* **Hỗ trợ phím tắt**: Thiết kế tối ưu hóa 100% thao tác nghiệp vụ chính (Đăng ký xe, Lưu cân 1, Lưu cân 2, Phân bổ, In phiếu) thông qua các phím tắt trên bàn phím (F1-F12, Enter) giúp nhân viên cân thao tác cực nhanh mà không cần dùng chuột. |

---

## KẾ HOẠCH PHÁT TRIỂN DỰ ÁN

Dự kiến các giai đoạn triển khai và các mốc quan trọng đối với dự án nâng cấp Phần mềm Cân (Weight Station):

| **Giai đoạn** | **Nội dung triển khai chính** | **Thời gian dự kiến** |
| :--- | :--- | :--- |
| **Giai đoạn 1** | Biên soạn tài liệu Phân tích yêu cầu (PTYC) chi tiết và thống nhất nghiệp vụ với các bên liên quan (Trưởng trạm cân, phòng Kế toán, phòng CNTT). | Tuần 1 - Tuần 2 |
| **Giai đoạn 2** | Xây dựng giao diện WPF (StationApp) và cấu hình cơ sở dữ liệu local SQL Server Express; thiết lập kết nối phần cứng đầu hiển thị cân (cổng COM) và tích hợp luồng RTSP camera IP. | Tuần 3 - Tuần 7 |
| **Giai đoạn 3** | Phát triển phân hệ đồng bộ dữ liệu hai chiều với ERP trung tâm và xây dựng hàng đợi đồng bộ ưu tiên ngoại tuyến (Offline-First Sync Queue). | Tuần 8 - Tuần 10 |
| **Giai đoạn 4** | Phát triển các tính năng kiểm soát nghiệp vụ thông minh: cảnh báo đăng kiểm, kiểm tra dung sai hàng bao, kiểm soát quá tải (tách phiếu) và cấp lại đơn kế thừa cân lần 1. | Tuần 11 - Tuần 13 |
| **Giai đoạn 5** | Thử nghiệm nội bộ (Dry Run) tại môi trường test, tích hợp dữ liệu lịch sử từ phần mềm cũ và tiến hành kiểm thử nghiệm thu người dùng (UAT). | Tuần 14 - Tuần 15 |
| **Giai đoạn 6** | Triển khai cài đặt thực tế tại các trạm cân nhà máy, cấu hình in ấn, bàn giao tài liệu hướng dẫn sử dụng và chính thức đưa vào vận hành. | Tuần 16 |
