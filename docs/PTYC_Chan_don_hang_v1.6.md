TK


**CÔNG TY CỔ PHẦN XI MĂNG CẨM PHẢ**

**Phòng Công nghệ thông tin**





**TÀI LIỆU PHÂN TÍCH YÊU CẦU**

**CHẶN ĐƠN HÀNG**




**Mã hiệu dự án:  XMCP-CDH**

**Mã hiệu tài liệu: CDH.PTYC**








**Quảng Ninh, 07/2026BẢNG GHI NHẬN THAY ĐỔI**


*A – Tạo mới, M – Sửa đổi, D – Xóa bỏ

| **Ngày** **thay đổi** | **Vị trí** **thay đổi** | **A*** **M, D** | **Người tạo** | **Phiên** **bản cũ** | **Mô tả thay đổi** | **Phiên** **bản mới** |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- |
| 01/12/2025 |  | A | Bùi Ngọc Chiến |  | Tạo mới tài liệu | V1.0 |
| 04/12/2025 |  | M | Bùi Ngọc Chiến | V1.0 | Thêm mô tả nguyên tắc bỏ chặn đơn hàng | V1.1 |
| 12/12/2025 |  | M | Bùi Ngọc Chiến | V1.1 | Bổ sung FR8 Sửa Yêu cầu phi chức năng NFR2 | V1.2 |
| 15/12/2025 |  | M | Bùi Ngọc Chiến | V1.2 | Sửa FR8 | V1.3 |
| 17/12/2025 |  | M | Bùi Ngọc Chiến | V1.3 | Sửa 3.4.1 Thêm loại Phiếu kế toán (Khách hàng thanh toán) | V1.4 |
| 16/01/2026 |  | M | Bùi Ngọc Chiến | V1.4 | Sửa theo phản hồi của Kế toán trưởng cho luồng duyệt cấu hình, các chứng từ liên quan TK 131,… | V1.5 |
| 01/07/2026 |  | M | Bùi Ngọc Chiến | V1.5 | Thêm tính năng Gửi tin nhắn cho sale và NPP | V1.6 |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |
|  |  |  |  |  |  |  |

**TRANG KÝ**

| Người xem xét: |  |
| :--- | :--- |
| Người xem xét: |  |
| Người xem xét: |  |
| Người xem xét: |  |
| Người xem xét: |  |
| Người xem xét: |  |


**MỤC LỤC**




























































## GIỚI THIỆU

### Mục đích tài liệu.

Tài liệu Phân tích yêu cầu người dùng nhằm trình bày một cách tường minh yêu cầu của người sử dụng đối với các chức năng có liên quan đến việc tính toán công nợ và tuổi nợ, từ đó tự động chặn thao tác với đơn hàng mà không thỏa mãn yêu cầu về công nợ và tuổi nợ.

### Định nghĩa thuật ngữ và các từ viết tắt

| **Thuật ngữ** | **Định nghĩa** | **Ghi chú** |
| :--- | :--- | :--- |
| ERP | Hệ thống hoạch định nguồn lực doanh nghiệp |  |
| NPP | Nhà phân phối |  |
| NMC | Nhà máy chính |  |
| CNPN | Chi nhánh phía Nam |  |
| HĐBH | Hóa đơn bán hàng |  |
| CKTM | Chiết khấu thương mại |  |

### Mô tả tài liệu

Tài liệu bao gồm 5 phần được tổ chức như sau:

Phần 1: Giới thiệu – Phần này sẽ trình bày về mục đích, phạm vi và ý nghĩa của tài liệu.

Phần 2: Tổng quan về dự án – Phần này sẽ trình bày cái nhìn tổng quan về hệ thống.

Phần 3: Yêu cầu chức năng - Phần này sẽ trình bày yêu cầu, nghiệp mong muốn của phòng ban mà hệ thống cần đáp ứng.

Phần 4: Yêu cầu phi chức năng - Phần này sẽ trình bày yêu cầu về phi chức năng mà hệ thống cần đáp ứng.

Phần 5: Kế hoạch phát triển dự án – Phần này trình bày về kế hoạch các giai đoạn phát triển của dự án.

## TỔNG QUAN VỀ DỰ ÁN

### Phát biểu bài toán

#### Hoàn cảnh:

Ban Tổng Giám đốc yêu cầu siết chặt quản lý công nợ của đối tác nhằm hạn chế phát sinh nợ xấu từ sớm.

Phòng Kế toán cần tự động hóa quy trình kiểm tra công nợ để xác định đối tác/khách hàng có đủ điều kiện lấy hàng hay không.

#### Hiện trạng:

Kế toán đang theo dõi công nợ đối tác bằng file Excel, dễ sai sót và thiếu đồng bộ.

Hệ thống ERP chưa hỗ trợ tự động phân bổ Báo Có, Phiếu Kế Toán cho các hóa đơn chưa thanh toán theo thứ tự ưu tiên (hóa đơn có số ngày quá hạn lớn nhất).

Hệ thống ERP chưa có cơ chế xử lý hoặc cảnh báo khi đối tác vi phạm công nợ.

#### Giải pháp:

Xây dựng chức năng Cấu hình định mức thanh toán theo từng đối tác và từng thị trường.

Xây dựng chức năng tự động tính toán công nợ, tuổi nợ và mức phạt thanh toán.

Phát triển chức năng tự động phân bổ Báo Có/Phiếu Kế Toán.

Tự động kiểm tra và chặn thao tác đơn hàng khi đối tác vi phạm quy định công nợ hoặc tuổi nợ.

Xây dựng bộ báo cáo quản trị công nợ, bao gồm Báo cáo công nợ của các Nhà phân phối (NPP).

#### Giá trị mang lại

Giảm rủi ro nợ xấu nhờ kiểm soát công nợ và tuổi nợ tự động, phát hiện sớm vi phạm.

Tự động hóa quy trình kế toán, giảm thao tác thủ công và sai sót khi phân bổ công nợ.

Tăng tính chính xác và minh bạch, dữ liệu công nợ được tính toán và cập nhật nhất quán.

Kiểm soát bán hàng chặt chẽ, tự động chặn đơn khi đối tác vi phạm công nợ.

Cung cấp báo cáo kịp thời, hỗ trợ lãnh đạo ra quyết định nhanh.


## YÊU CẦU CHỨC NĂNG

### Tổng quan quy trình Tự động chặn đơn hàng


### Tổng hợp yêu cầu chức năng

| **ID** | **Diễn giải** |
| :--- | :--- |
| FR1 | **Chốt dữ liệu công nợ cũ:** Lưu trữ được dữ liệu số dư công nợ đầu kỳ khi chuyển số liệu từ file excel sang hệ thống ERP. Cập nhật trạng thái thanh toán cho hóa đơn để thu hẹp phạm vi hóa đơn sẽ phân bổ thanh toán từ Báo Có/Phiếu kế toán. Lưu trữ, tính toán lại công nợ hiện tại khi phát sinh các nhân tố ảnh hưởng đến công nợ hiện tại. |
| FR2 | **Cấu hình hạn mức công nợ:** Cho phép CLKD tạo cấu hình công nợ NPP và Kế toán duyệt cấu hình công nợ. Lưu trữ được dữ liệu cho Hạn mức, Bảo lãnh/Tín chấp cho các đối tác theo các thị trường. Tính toán Hạn mức tín dụng. |
| FR3 | **Phân bổ Báo Có/Phiếu kế toán:** Hỗ trợ phân bổ chỉ định và phân bổ tự động. Hỗ trợ hiển thị hóa đơn bán hàng, tiền phạt theo đúng nguyên tắc. Lưu trữ, hiển thị lịch sử phân bổ. Cập nhật thông tin thanh toán cho các chứng từ liên quan. Lưu trữ được số dư khi chưa phân bổ hết. Tự động phân bổ Báo Có/ Phiếu kế toán vẫn còn số dư chưa phân bổ. |
| FR4 | **Tự động chặn đơn hàng:** Thực hiện chặn đơn hàng khi vi phạm về công nợ và tuổi nợ. Hỗ trợ hiển thị thông báo khi vi phạm. Hỗ trợ xác nhận cho phép tiếp tục xử lý đơn hàng. Gửi tin nhắn cho kế toán phụ trách khi NPP vi phạm (tùy chọn). |
| FR5 | **Process dọn dẹp đơn đặt hàng quá ngày hiệu lực** |
| FR6 | **Xây dựng các báo cáo quản trị** |
| FR7 | **Gộp Phiếu kế toán (CKTM) khi tính chiết khấu** |
| FR8 | **Tự động chốt tiền phạt** |
| FR9 | **Gửi tin nhắn thông tin công nợ thời điểm** |

### FR1: Chốt dữ liệu công nợ cũ

#### Mục đích

Nghiệp vụ Báo Có/Phiếu Kế Toán hiện tại không thể hiện việc phân bổ thanh toán vào từng hóa đơn cụ thể, dẫn đến hệ thống không xác định được trạng thái thanh toán của các hóa đơn đang lưu trữ trong hệ thống.

Hệ thống cần thực hiện chốt dữ liệu công nợ tại một mốc thời gian để thiết lập số dư công nợ đầu kỳ, làm cơ sở tính toán chính xác công nợ khi có Báo Có, Phiếu Kế Toán, Tiền phạt và hóa đơn mới.

Các dữ liệu lưu trữ bao gồm:

Số dư công nợ đầu kỳ.

Ngày phát sinh công nợ đầu tiên.

Công nợ hiện tại.

#### Phạm vi

Do có số lượng lớn hóa đơn cũ đã thanh toán nhưng chưa được cập nhật trạng thái thanh toán trên ERP, kế toán cần nhập Ngày chốt công nợ để hệ thống bỏ qua các hóa đơn cũ trước thời điểm này và chỉ thực hiện phân bổ Báo Có/Phiếu kế toán cho các hóa đơn phát sinh từ Ngày chốt công nợ trở về sau.

#### Cách thực hiện

Từ file excel theo dõi công nợ NPP bên ngoài, kế toán chốt số dư công nợ hiện tại và nhập lên hệ thống ERP để thiết lập số dư công nợ đầu kỳ.

Xác định được Ngày chốt công nợ là ngày phát sinh hóa đơn cũ nhất mà chưa thanh toán, nhập lên hệ thống ERP.

#### Công thức liên quan

Công nợ đầu kỳ (Số dư công nợ đầu kỳ) = Tổng hóa đơn bán hàng chưa thanh toán + Tổng tiền phạt (cho CNPN).

Công nợ cuối kỳ (Công nợ hiện tại) = Công nợ đầu kỳ – Báo có – Phiếu kế toán + Tổng tiền phạt + Hóa đơn mới.

### FR2: Cấu hình hạn mức công nợ

#### Mục đích

Hệ thống ERP cho phép cấu hình và lưu trữ các thông số liên quan để tính được Hạn mức tín dụng cho từng đối tác và Số ngày nợ quá hạn cho từng hóa đơn. Các dữ liệu được lưu trữ gồm:

Số dư công nợ đầu kì: Công nợ tại thời điểm duyệt cấu hình công nợ.

Công nợ hiện tại: Công nợ hiện tại được cập nhật liên tục sau khi phát sinh các chứng từ liên quan.

Hạn mức: Hạn mức mà công ty ngầm cho NPP được hưởng, mặc định là 200 triệu.

Bảo lãnh/Tín chấp: Bảo lãnh ngân hàng hoặc tín chấp ngân hàng mà công ty cho NPP được hưởng.

Số ngày nợ cho phép theo hợp đồng cho chủng loại bao.

Số ngày nợ cho phép theo hợp đồng cho chủng loại rời/xá.

Mức phạt thanh toán (VNĐ/tấn).

Số tiền nợ đối tác: Là số tiền Phải trả khách hàng đã đến hạn trả NPP nhưng chưa kịp hoàn thiện hồ sơ thanh toán, kế toán tạm nhập số tiền đó vào trường này, sau khi tạo và PO Phiếu kế toán để đối trừ công nợ, kế toán nhập số tiền về 0.

Ngày chốt công nợ: Ngày bắt đầu phát sinh hóa đơn cũ nhất có Số tiền chưa thanh toán > 0.

Ngày bắt đầu phân bổ: Ngày phát sinh Báo có/ PKT mà chưa phân bổ thanh toán (lấy theo dữ liệu chốt công nợ).

Luồng duyệt Cấu hình công nợ NPP: CLKD tạo, nhập thông tin về hạn mức tín dụng, sau đó CO cấu hình công nợ. Kế toán vào nhập thông tin dữ liệu chốt công nợ, sau đó duyệt cấu hình công nợ.

#### Công thức liên quan

Hạn mức tín dụng = Hạn mức + Bảo lãnh/Tín chấp + Tiền nợ đối tác.

Tiền phạt = Mức phạt thanh toán x Số tấn (trong HĐBH) x Số ngày nợ quá hạn.

Ngày lấy hàng = Ngày hóa đơn bán hàng.

Hạn thanh toán = Ngày lấy hàng + Số ngày nợ cho phép theo hợp đồng.

### FR3: Phân bổ Báo Có, Phiếu kế toán

#### Định nghĩa

Tại NMC:

Báo Có (Khách hàng thanh toán):

TK Nợ: 112x.

TK Có: 131.

Phiếu kế toán (CKTM):

TK Nợ: 521x.

TK Có: 131.

Tại CNPN:

Phiếu kế toán (Khách hàng thanh toán):

TK Nợ: 13689.

TK Có: 131.

Phiếu kế toán (CKTM):

TK Nợ: 521x.

TK Có: 131.

Phiếu kế toán (Tiền phạt):

TK Nợ: 131.

TK Có: 711.

#### Quy trình


#### Cách thức phân bổ

Hệ thống hỗ trợ 2 cách thức phân bổ:

Phân bổ chỉ định: Cho phép người dùng vào chức năng Chọn chứng từ để tìm kiếm và chủ động lựa chọn chứng từ để thực hiện phân bổ thanh toán.

Phân bổ tự động: Tự động xác định và lựa chọn các chứng từ theo đúng thứ tự phân bổ và nguyên tắc phân bổ.

Đối với Báo Có và Phiếu kế toán (Khách hàng thanh toán):

Hệ thống cho phép phân bổ chỉ định và tự động.

Hệ thống hỗ trợ hiển thị các chứng từ sẽ được phân bổ để người dùng kiểm tra lại thông tin sẽ phân bổ.

Đối với Phiếu kế toán (CKTM):

Hệ thống cho phép phân bổ chỉ định và tự động.

Hệ thống hỗ trợ hiển thị các chứng từ sẽ được phân bổ để người dùng kiểm tra lại thông tin sẽ phân bổ.

#### Thứ tự phân bổ

Hệ thống tự động phân bổ thanh toán theo thứ tự ưu tiên sau (tất cả đều lọc các chứng từ chưa hoàn thành thanh toán, sắp xếp Số ngày nợ quá hạn giảm dần):

Thứ tự phân bổ Báo có và Phiếu kế toán (Khách hàng thanh toán):

Phiếu kế toán (Tiền phạt).

Hóa đơn hàng gửi.

Hóa đơn hàng bán.

Thứ tự phân bổ Phiếu kế toán (CKTM):

Hóa đơn hàng bán.

#### Nguyên tắc phân bổ


Việc phân bổ sẽ dừng khi toàn bộ số tiền của Báo Có hoặc Phiếu Kế Toán đã được sử dụng hết hoặc hết chứng từ để phân bổ.

Nếu số tiền phân bổ khả dụng đủ hoặc thừa để thanh toán toàn bộ cho một chứng từ

Hệ thống thực hiện phân bổ thanh toán cho chứng từ đó.

Số tiền phân bổ khả dụng = Tổng tiền (Báo Có/ Phiếu kế toán) – Số tiền đã phân bổ.

Cập nhật trạng thái chứng từ là Hoàn thành thanh toán và cập nhật lại công nợ.

Tiếp tục thực hiện phân bổ cho các chứng từ tiếp theo.

Nếu số tiền phân bổ khả dụng không đủ để thanh toán toàn bộ cho một chứng từ

Hệ thống dùng toàn bộ số tiền còn lại để phân bổ cho chứng từ đó.

Số tiền phân bổ khả dụng cập nhật về 0.

Số tiền phân bổ vào chứng từ = phần tiền khả dụng cuối cùng.

Cập nhật trạng thái chứng từ là Chưa hoàn thành thanh toán và cập nhật lại công nợ.

Nếu đã hết chứng từ để phân bổ trong khi số tiền phân bổ khả dụng vẫn còn > 0:

Lưu trữ số tiền dư này.

Tự động trừ phần dư này khi phát sinh hóa đơn bán hàng mới.

#### Cập nhật trạng thái thanh toán

Sau khi phân bổ xong, hệ thống phải cập nhật trạng thái của các chứng từ liên quan:

Báo Có/ Phiếu kế toán (Khách hàng thanh toán).

Phiếu kế toán (CKTM).

Hóa đơn.

Phiếu kế toán (Tiền phạt).

### FR4: Tự động chặn đơn hàng

#### Định nghĩa

Tổng tiền hàng đang đặt hàng:

NMC: Là tổng giá trị các cắt lệnh của khách hàng thỏa mãn các điều kiện sau:

Không gồm phần lấy hàng gửi.

Cắt lệnh ứng với nó đã được CO, chưa kết thúc xuất hàng.

CNPN: Là tổng giá trị các đơn đặt hàng của khách hàng thỏa mãn các điều kiện sau:

Là đơn bán hàng

Đã được duyệt

Còn hiệu lực

Trạng thái chờ lấy hàng

Chưa được đánh dấu Không lấy hàng dưới phần mềm cân

#### Quy trình


#### Thời điểm

Tại NMC, thực hiện chặn không cho chọn đơn hàng khi cắt lệnh xuất hàng.

Tại CNPN, thực hiện chặn khi CO đơn đặt hàng.

#### Nguyên tắc chặn đơn

Điều kiện về công nợ:

Một đối tác được coi là vi phạm công nợ khi Công nợ + Tổng tiền hàng đang đặt hàng > Hạn mức tín dụng.

Điều kiện về tuổi nợ:

Một đối tác được coi là vi phạm về tuổi nợ nếu tổng tiền của các hóa đơn quá hạn thanh toán > Hạn mức.

Hệ thống thực hiện kiểm tra điều kiện về công nợ của đối tác và tuổi nợ của các hóa đơn bán hàng:

Nếu vi phạm ít nhất một trong hai điều kiện, hệ thống chặn đơn hàng và hiển thị lý do chặn cho người dùng.

Nếu không vi phạm cả hai điều kiện, hệ thống cho phép tiếp tục xử lý đơn hàng.

#### Nguyên tắc bỏ chặn đơn

Khi kế toán ghi nhận thêm các chứng từ thanh toán như Báo Có/ Phiếu kế toán (Khách hàng thanh toán) hoặc Phiếu kế toán (CKTM), số dư công nợ của đối tác sẽ giảm. Sau đó người dùng thực hiện CO lại cắt lệnh (với NMC) hoặc duyệt lại đơn đặt hàng (với CNPN), hệ thống sẽ tự động kiểm tra lại điều kiện công nợ và tuổi nợ theo Nguyên tắc chặn đơn đã thiết lập và thực hiện các hành động tương ứng với đơn hàng đó.

Nếu kế toán được ủy quyền cho phép tiếp tục xử lý đơn hàng để xuất hàng cho đối tác, hệ thống cho phép đánh dấu đối tác ở trạng thái được bỏ qua kiểm tra công nợ và tuổi nợ cho đơn hàng đó.

#### Công thức liên quan

Số ngày nợ thực tế = Ngày hiện tại – Ngày lấy hàng + 1 (đối với hóa đơn chưa thanh toán)

hoặc Số ngày nợ thực tế = Ngày thanh toán – Ngày lấy hàng + 1 (đối với hóa đơn đã được thanh toán).

Số ngày nợ quá hạn = Số ngày nợ thực tế - Số ngày nợ cho phép theo hợp đồng

Số ngày nợ quá hạn < -3: Trong hạn.

-3 <= Số ngày nợ quá hạn < 0: Sắp đến hạn.

Số ngày nợ quá hạn = 0: Đến hạn.

Số ngày nợ quá hạn > 0: Quá hạn.

### FR5: Process dọn dẹp đơn đặt hàng quá ngày hiệu lực

#### Mục đích

Khi đối tác yêu cầu, người dùng sẽ tạo đơn đặt hàng. Tuy nhiên, nhiều đơn trong số này có thể không được lấy hàng, dẫn đến tồn đọng các đơn không còn nhu cầu sử dụng.

Các đơn tồn đọng này làm tăng tổng giá trị tiền hàng đang đặt, khiến đối tác dễ vi phạm điều kiện công nợ.

Vì vậy, cần xây dựng quy trình tự động dọn dẹp các đơn đặt hàng không còn hiệu lực để giảm tồn đọng, đồng thời phản ánh đúng công nợ và nhu cầu thực tế.

#### Đối tượng thực hiện

Chỉ thực hiện dọn dẹp cho các đơn đặt hàng của CNPN thỏa mãn các điều kiện:

Đơn đã được duyệt.

Cắt lệnh ứng với nó chưa được CO.

Bản ghi tương ứng trong phần mềm Cân có 1 trong các trạng thái sau:

Cancel.

Undeliveried.

Pending Delivery.

#### Thời điểm chạy

Có thể chạy thủ công theo nhu cầu.

Hoặc thiết lập chạy tự động theo lịch tùy mục đích vận hành.

### FR6: Xây dựng các báo cáo quản trị

#### Báo cáo theo dõi công nợ NPP


Link template:

#### Báo cáo tổng hợp phân tích công nợ


Link template:

#### Báo cáo chi tiết CKTM


Link template:

### FR7: Gộp Phiếu kế toán (CKTM) theo đối tác khi tính chiết khấu

#### Hiện trạng

Ngoài NMC

Khi nhân viên CLKD nhấn nút [Chuyển số liệu tài chính], hệ thống tự động tạo Phiếu kế toán mà trong tab Chi tiết có nhiều chi tiết nhỏ, phân tách cho từng sản phẩm, thị trường, đối tác.

Việc phân tách nhỏ này gây khó khăn cho việc tính toán, phân bổ Phiếu kế toán (CKTM), số tiền còn dư sau phân bổ và theo dõi lịch sử phân bổ.

Trong CNPN

Với mỗi chương trình chiết khấu, kế toán tạo thủ công Phiếu kế toán (CKTM) cho từng đối tác và chi tiết các sản phẩm được liệt kê ở tab Chi tiết.

Vì vậy, cần thay đổi code để thực hiện thay đổi liên quan đến phần này.

#### Đối tượng

Các Phiếu kế toán được tạo ra sau khi thực hiện tính chiết khấu tại NMC.

#### Nguyên tắc tạo Phiếu kế toán (CKTM)

Để thống nhất giữa NMC và CNPN trong việc tạo Phiếu kế toán (CKTM), cần thay đổi cách tạo các bản ghi Phiếu kế toán (CKTM) tự động ở NMC để giống với cách tạo trong CNPN.

Sau khi có đủ dữ liệu chương trình CKTM, tính tổng số lượng được hưởng CKTM của từng nhà theo loại Bao và Rời, sau đó thực hiện tạo tối đa 2 bản ghi Phiếu kế toán cho 1 đối tác. Thông tin chiết khấu được hưởng của các sản phẩm sẽ được thể hiện ở tab Chi tiết.

### FR8: Hỗ trợ số liệu tiền phạt

#### Mục đích

Hiện tại kế toán đang theo dõi tiền phạt từ file excel ngoài, số liệu chưa được xác thực bởi hệ thống ERP.

Hệ thống hỗ trợ xuất báo cáo theo dõi tiền phạt của đối tác.

Hệ thống tự động tạo bản ghi phiếu kế toán để lưu trữ tiền phạt của đối tác (xem xét sau).

#### Công thức liên quan

Tổng tiền phạt tháng N của đối tác = ∑ Tiền phạt của từng hóa đơn của đối tác bị quá hạn thanh toán trong tháng N.

### FR9: Gửi tin nhắn thông tin công nợ thời điểm

#### Đối tượng

Nhân viên sale phụ trách và NPP.

Trước mắt chỉ thực hiện gửi cho nhân viên sale.

#### Mục đích

Hệ thống thực hiện gửi tin nhắn tự động đến nhóm SĐT được cấu hình sẵn.

#### Thời điểm

Vào lúc 11h30 và 17h30 hàng ngày.

#### Nội dung tin nhắn

“Tính đến 11h30, công nợ hiện tại của <tên NPP> là <công nợ hiện tại>, công nợ quá hạn là < tổng tiền hóa đơn nợ quá hạn >, công nợ sắp tới hạn là <tổng tiền hóa đơn sắp tới hạn>”

## YÊU CẦU PHI CHỨC NĂNG

| **ID** | **Loại** | **Diễn giải** |
| :--- | :--- | :--- |
| NFR1 | Hiệu năng | Hệ thống phải trả về kết quả chặn trong vòng < 3 giây kể từ khi bấm duyệt/ chọn đơn hàng. |
| NFR2 | Bảo mật | Chỉ những tài khoản kế toán được phân quyền mới được phép thực hiện thao tác bảo lãnh đối tác. |
| NFR3 | Kiểm soát | Ghi lại toàn bộ lịch sử: Thời gian, Lý do chặn, Người bỏ chặn (không được phép sửa/xóa log này). |
| NFR4 | Linh hoạt | Cho phép người dùng sửa đổi hạn mức và quy tắc chặn trên giao diện mà không cần can thiệp code. |
| NFR5 | Trải nghiệm (UX) | Thông báo lỗi phải hiển thị chính xác lý do bị chặn (VD: "Đơn hàng đã bị chặn do đối tác vi phạm công nợ") cho người dùng biết. |
| NFR6 | Bảo mật | Những tài khoản kế toán được phân quyền mới được phép duyệt cấu hình công nợ. |

## 

## KẾ HOẠCH PHÁT TRIỂN DỰ ÁN

| **Giai đoạn phát triển** | **Nội dung triển khai** | **Thời gian dự kiến** |
| :--- | :--- | :--- |
| 1 | Viết tài liệu Phân tích yêu cầu và chốt với phòng Kế Toán. | 22/11/2025 - 12/03/2026 |
| 2 | Xây dựng các chức năng phục vụ quy trình Tự động chặn đơn hàng. | 13/03/2026 - 14/04/2026 |
| 3 | Kiểm thử chức năng Tự động chặn đơn hàng trên hệ thống ERP (môi trường test). | 15/04/2026 – 23/04/2026 |
| 4 | Chuẩn bị dữ liệu cần thiết và triển khai chức năng Tự động chặn đơn hàng trên hệ thống ERP (môi trường prod) cho NMC. | 24/04/2026 - 19/06/2026 |
| 5 | Chuẩn bị dữ liệu cần thiết và triển khai chức năng Tự động chặn đơn hàng trên hệ thống ERP (môi trường prod) cho CNPN. | 29/06/2026 - 15/08/2026 |


