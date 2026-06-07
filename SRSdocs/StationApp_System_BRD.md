# Tài liệu Yêu cầu Kinh doanh (BRD) - Hệ thống Trạm Cân (WeightStation)

## Thông tin Tài liệu

| Trường | Giá trị |
|-------|-------|
| Tên Dự án | WeightStation (Ứng dụng Trạm cân) |
| Phiên bản Tài liệu | 1.2 |
| Ngày tạo | 07/06/2026 |
| Tác giả | Bộ phận Quản lý Sản phẩm (Antigravity AI) |
| Trạng thái | Đang được duyệt |

## Lịch sử Thay đổi

| Phiên bản | Ngày | Tác giả | Mô tả thay đổi |
|---------|------|--------|-------------|
| 1.0 | 07/06/2026 | Antigravity AI | Bản thảo đầu tiên của tài liệu Yêu cầu Kinh doanh (BRD). |
| 1.1 | 07/06/2026 | Antigravity AI | Cập nhật Phần 3 làm nổi bật 11 giải pháp vượt trội so với phần mềm cân cũ theo yêu cầu người dùng. |
| 1.2 | 07/06/2026 | Antigravity AI | Việt hóa toàn bộ tài liệu, hạn chế tối đa việc sử dụng từ tiếng Anh theo yêu cầu của người dùng. |

---

# 1. Tóm tắt Dự án

### 1.1 Tên Dự án & Mô tả ngắn
Dự án **WeightStation (Ứng dụng Trạm cân)** là giải pháp phần mềm quản lý trạm cân xe vận tải cục bộ kết hợp máy chủ trung tâm. Hệ thống hỗ trợ nhân viên trạm cân thực hiện ghi nhận trọng lượng xe (nhập hàng và xuất hàng), in ấn phiếu cân, đối chiếu dung sai hàng hóa, và tự động đồng bộ kết quả thời gian thực về hệ thống ERP của doanh nghiệp.

### 1.2 Bài toán Kinh doanh
Hiện tại, hoạt động đo lường hàng hóa tại các trạm cân của doanh nghiệp đang gặp phải một số thách thức lớn:
- **Tạo lượt cân thủ công**: Nhân viên phải nhập thông tin phương tiện thủ công. Hệ thống chưa có danh mục dữ liệu gốc (Master Data) về phương tiện vận tải nên không hỗ trợ việc nhập nhanh dữ liệu, việc này tốn thời gian và dễ sai sót.
- **Chưa hỗ trợ nghiệp vụ cân cho đơn xuất khẩu**: Các đơn hàng xuất khẩu lớn chạy nhiều chuyến xe con độc lập hiện đang phải quản lý thủ công trên sổ sách, dễ nhầm lẫn khối lượng lũy kế và gây khó khăn khi chốt sản lượng với đối tác.
- **Thiếu hình ảnh kiểm chứng**: Chưa có cơ chế chụp ảnh tự động biển số xe trước/sau lúc cân để lưu trữ bằng chứng đối chiếu khi xảy ra tranh chấp khối lượng.

### 1.3 Giải pháp Đề xuất
Xây dựng ứng dụng trạm cân chạy trên hệ điều hành Windows cục bộ, kết nối trực tiếp với đầu cân điện tử qua cổng nối tiếp (Serial COM) và hệ thống camera giám sát thông qua giao thức truyền tải thời gian thực (RTSP). Ứng dụng hoạt động theo cơ chế **Ưu tiên cục bộ (Offline-First)** (hoạt động ngoại tuyến bình thường khi mất mạng và tự động đồng bộ dữ liệu qua giao thức kết nối RESTful API khi kết nối Internet được phục hồi).

### 1.4 Lợi ích Kinh doanh dự kiến (Hiệu quả đầu tư - ROI)
- **Tối ưu hóa năng suất**: Giảm 50% thời gian cân và lập phiếu cho mỗi lượt xe vận tải nhờ cơ chế tải dữ liệu đăng ký xe từ hệ thống ERP và tự động phân bổ khối lượng.
- **Bảo toàn dữ liệu 100%**: Đảm bảo hoạt động cân xe diễn ra liên tục 24/7 ngay cả khi mất mạng Internet hoàn toàn trong 30 ngày. Hệ thống tự động sao lưu dữ liệu cục bộ hàng ngày đề phòng hỏng hóc phần cứng.
- **Loại bỏ thất thoát & Gian lận**: Cơ chế chụp ảnh tự động biển số xe khi lưu cân, kết hợp kiểm soát chặt chẽ dung sai cho phép trên từng bao hàng và quá tải trọng cho phép (TTCP) của phương tiện.
- **Quyết toán chính xác**: Dữ liệu cân được đồng bộ tự động về hệ thống quản lý trung tâm giúp giảm thiểu thời gian đối soát công nợ từ 3 ngày xuống dưới 5 phút.
- **Nâng cao trải nghiệm người dùng**: Giao diện hiện đại, dễ sử dụng, hỗ trợ in ấn phiếu cân và phiếu giao nhận trực tiếp từ phần mềm.

### 1.5 Chỉ số đo lường thành công (Các chỉ số hiệu quả chính)
- Thời gian thực hiện một lượt cân xe đầy đủ (2 lần cân): **< 2 phút**.
- Tỷ lệ dữ liệu cân bị mất hoặc sai lệch so với thực tế: **0%**.
- Tỷ lệ hoạt động liên tục (Thời gian hoạt động liên tục) của phần mềm tại trạm: **99.9%**.

---

# 2. Mục tiêu Kinh doanh

### 2.1 Mục tiêu chính
Xây dựng và đưa vào vận hành hệ thống phần mềm trạm cân WeightStation tự động, ổn định và an toàn, tích hợp trực tiếp với hệ thống phần cứng trạm cân hiện hữu và đồng bộ dữ liệu thời gian thực với hệ thống ERP trung tâm trước Quý 3/2026.

### 2.2 Mục tiêu cụ thể (Mục tiêu SMART)
1. **Rút ngắn thời gian vận hành**: Giảm thời gian chờ đợi trung bình của mỗi xe tại trạm cân xuống dưới 1,5 phút/xe trong vòng 3 tháng đầu tiên vận hành.
2. **Số hóa 100% tài liệu**: Loại bỏ hoàn toàn việc ghi chép sổ sách thủ công tại trạm cân, tự động sinh và in Phiếu cân tổng hợp và Phiếu giao nhận trực tiếp từ phần mềm.
3. **Độ chính xác dữ liệu**: Đảm bảo 100% các lượt cân xe xuất hàng bao đều được kiểm tra dung sai khối lượng cho phép nhằm tránh thất thoát nguyên vật liệu của nhà máy.
4. **Kiểm soát tải trọng xe**: Phát hiện và xử lý kịp thời 100% các xe chở quá tải trọng cho phép (TTCP) trước khi xe rời khỏi nhà máy để tuân thủ pháp luật giao thông đường bộ.
5. **Khả năng tự phục hồi**: Đảm bảo dữ liệu cục bộ luôn được bảo vệ an toàn thông qua cơ chế tự động sao lưu cơ sở dữ liệu hàng ngày, hỗ trợ phục hồi dữ liệu nhanh trong vòng dưới 30 phút nếu có sự cố máy trạm.

---

# 3. Ưu thế Vượt trội của Giải pháp mới (Giá trị cốt lõi mang lại)

Hệ thống trạm cân WeightStation mới mang đến các cải tiến đột phá và vượt trội hoàn toàn so với phần mềm cân cũ đang sử dụng tại nhà máy:

| Tiêu chí | Phần mềm cân cũ | Giải pháp mới (WeightStation) | Giá trị kinh doanh đem lại |
|---|---|---|---|
| **Nhập liệu đơn hàng** | Nhân viên phải chọn thủ công toàn bộ thông tin xe, tài xế, hàng hóa, khách hàng từ ERP, mất 1-3 phút/xe, dễ sai sót. | **Tự động lấy dữ liệu từ ERP**: Hệ thống tự động đồng bộ danh sách đơn hàng được duyệt (Cut Order) từ ERP xuống trạm cân. | Giảm thời gian đăng ký cân xe, loại bỏ lỗi sai sót biển số hoặc mã hàng. |
| **Quản lý danh mục (Dữ liệu gốc)** | Không lưu trữ dữ liệu danh mục. Mỗi lần tạo đơn cân tay (ví dụ đơn nhập hàng thủ công) nhân viên phải gõ lại từ đầu. | **Tự động lưu dữ liệu gốc**: Lưu trữ danh mục xe, khách hàng, sản phẩm cục bộ để tra cứu thông minh và tự động điền nhanh dữ liệu. | Tiết kiệm thời gian vận hành các lượt cân đột xuất hoặc đơn hàng nhập khẩu thủ công. |
| **Kiểm soát đăng kiểm xe** | Không quản lý thông tin đăng kiểm, dễ xảy ra tình trạng xe hết hạn đăng kiểm vẫn vào cân gây vi phạm pháp luật. | **Lưu trữ tải trọng và đăng kiểm**: Lưu trữ số đăng kiểm xe/mooc, ngày hết hạn và trọng tải cho phép (TTCP) trực tiếp trong danh mục. | Đảm bảo tính pháp lý của phương tiện ra vào nhà máy, tuân thủ luật giao thông đường bộ. |
| **Xử lý xe quá tải** | Chỉ hiển thị cảnh báo thô sơ, nhân viên trạm cân có thể tự ý cho xe đi qua mà không có quy trình kiểm soát chặt chẽ. | **Cơ chế xử lý quá tải nâng cao**: Hỗ trợ duyệt quá tải không tách (ghi nhận nhật ký) hoặc tách lượt cân thành 2 phiếu con hợp lệ nằm trong ngưỡng TTCP. | Ngăn ngừa rủi ro bị xử phạt hành chính khi xe rời nhà máy, tăng tính minh bạch trong kiểm soát tải trọng. |
| **Kiểm soát khối lượng hàng** | Không kiểm soát dung sai của hàng bao, dễ xảy ra tình trạng xuất thừa hàng cho khách hàng gây thất thoát. | **Cảnh báo vượt dung sai**: Tự động tính toán và cảnh báo trên màn hình nếu khối lượng tịnh thực xuất vượt quá dung sai cho phép/bao. | Bảo vệ tài sản nhà máy, ngăn ngừa tình trạng xuất dư nguyên vật liệu hoặc thành phẩm. |
| **Gộp đơn hàng (Nhiều đơn/xe)** | Mỗi xe chỉ cân được 1 đơn hàng. Nếu 1 xe lấy nhiều mặt hàng hoặc cho nhiều nhà phân phối (NPP) phải thực hiện cân và di chuyển xe nhiều lần. | **Mô hình Phiên cân**: Cho phép gộp nhiều đơn hàng, nhiều mặt hàng, nhiều nhà phân phối (NPP) vào cùng 1 lượt cân xe vật lý. | Tối ưu hóa lượt xe chạy, giảm số lần cân xe vật lý thực tế tại trạm, rút ngắn thời gian giải phóng xe. |
| **Cân xuất khẩu đơn hàng lớn** | Không hỗ trợ luồng riêng, nhân viên phải theo dõi ngoài file excel các chuyến xe dễ gây nhầm lẫn, dữ liệu thiếu minh bạch. | **Luồng Cân xuất khẩu chuyên dụng**: Quản lý đơn hàng lớn qua nhiều chuyến xe độc lập, hỗ trợ chuyển chuyến và tự động chốt sản lượng cuối. | Quản lý chính xác sản lượng giao nhận thực tế của các hợp đồng xuất khẩu lớn, minh bạch dữ liệu với đối tác. |
| **Báo cáo & Thống kê** | Không có báo cáo thống kê cục bộ tại trạm cân. Nhân viên phải đợi cuối ngày đối soát dữ liệu từ ERP. | **Thống kê dữ liệu hàng ngày**: Tích hợp phân hệ báo cáo chốt sản lượng xuất khẩu và nhập hàng chi tiết trực tiếp tại trạm theo ngày. | Giúp Trưởng trạm cân và nhân viên kế toán đối soát sản lượng tức thì cuối ca, phát hiện chênh lệch nhanh chóng. |
| **Cấu hình in ấn** | Biểu mẫu in cố định trong mã nguồn. Không thể thay đổi máy in mặc định hoặc điều chỉnh lề in khi máy in bị lệch dòng. | **Cấu hình in linh hoạt**: Hỗ trợ thiết lập máy in mặc định và tùy chỉnh tọa độ lề in (khoảng lệch lề) cho từng loại chứng từ. | Thích ứng tức thì với các loại máy in vật lý khác nhau tại trạm cân mà không cần can thiệp sửa đổi mã nguồn. |
| **Hoạt động ngoại tuyến** | Ngừng hoạt động hoàn toàn khi mất kết nối mạng Internet với máy chủ, gây ùn tắc kéo dài tại cổng nhà máy. | **Ưu tiên ngoại tuyến & Đồng bộ hàng đợi**: Lưu trữ dữ liệu cục bộ và cho phép cân, in phiếu bình thường. Tự động đồng bộ lên máy chủ khi có mạng lại. | Đảm bảo nhà máy vận hành liên tục 24/7, loại bỏ hoàn toàn rủi ro đình trệ do đường truyền Internet gặp sự cố. |
| **An toàn dữ liệu** | Không có cơ chế sao lưu dữ liệu cục bộ tự động. Rất dễ mất dữ liệu lịch sử cân khi máy tính trạm gặp sự cố virus hoặc hỏng ổ cứng. | **Tự động sao lưu dữ liệu cục bộ**: Tác vụ chạy ngầm tự động sao lưu hàng ngày lúc 3:00 AM, lưu trữ file 10 ngày phục vụ khôi phục nhanh. | Đảm bảo an toàn dữ liệu vận hành tuyệt đối tại trạm cân trước mọi sự cố hư hỏng thiết bị phần cứng. |

### 3.2 Phân tích Chi tiết các Giải pháp Vượt trội (Phân tích sâu giải pháp)

Dưới đây là mô tả chi tiết của 11 giải pháp vượt trội được tích hợp trên hệ thống WeightStation mới nhằm thay thế toàn diện các thiếu sót của phần mềm cũ, giúp tối ưu hóa hiệu quả vận hành và kiểm soát rủi ro:

1. **Tự động hóa lấy dữ liệu đơn hàng từ ERP**:
   - *Thực trạng cũ*: Nhân viên cân phải nhập thủ công từng trường thông tin (Biển số xe, mooc, tài xế, mã sản phẩm, khách hàng, khối lượng dự kiến) từ các văn bản giấy hoặc tin nhắn, trung bình mất **3-5 phút/lượt xe**, tỷ lệ gõ sai ký tự cao dẫn đến sai sót dữ liệu khi đối chiếu doanh thu.
   - *Giải pháp mới*: Hệ thống tự động đồng bộ thời gian thực danh sách **Đơn hàng được duyệt (Cut Order)** từ ERP xuống máy trạm. Khi xe vào trạm, nhân viên cân chỉ cần gõ biển số xe hoặc chọn từ danh sách hiển thị thông minh, toàn bộ thông tin đơn hàng tương ứng sẽ được tự động điền.
   - *Giá trị mang lại*: Rút ngắn 95% thời gian đăng ký cân xe, giải phóng xe nhanh chóng, giảm ùn tắc và triệt tiêu lỗi nhập liệu do con người.

2. **Tự động lưu trữ danh mục dữ liệu dùng chung (Dữ liệu gốc)**:
   - *Thực trạng cũ*: Không hỗ trợ lưu trữ danh mục cục bộ. Với các đơn cân lẻ (nhập vật tư từ nhà cung cấp nhỏ, cân dịch vụ), nhân viên phải gõ lại thông tin đối tác và sản phẩm từ đầu cho mỗi lượt xe.
   - *Giải pháp mới*: Hệ thống tự động lưu trữ danh mục dữ liệu gốc (bao gồm Xe, Khách hàng, Sản phẩm, Nhà cung cấp) trực tiếp trên cơ sở dữ liệu cục bộ. Khi có lượt cân lẻ mới, hệ thống tự động lưu lại thông tin đối tác và cung cấp cơ chế tự động gợi ý thông minh ở các lượt cân tiếp theo.
   - *Giá trị mang lại*: Tối ưu hóa thời gian xử lý các đơn hàng ngoài luồng ERP, giảm thiểu thao tác lặp lại của nhân viên trạm cân.

3. **Lưu trữ thông tin TTCP và thông tin đăng kiểm xe**:
   - *Thực trạng cũ*: Không quản lý thông tin tải trọng thiết kế và thời hạn đăng kiểm của phương tiện, trạm cân dễ để lọt các phương tiện đã quá hạn đăng kiểm hoặc quá tải trọng thiết kế vào cân và xuất hàng ra ngoài, gây vi phạm luật giao thông đường bộ.
   - *Giải pháp mới*: Cho phép lưu trữ chi tiết số đăng kiểm xe/mooc, thời hạn hiệu lực đăng kiểm và **Tải trọng cho phép (TTCP)** trong danh mục phương tiện. Khi xe đăng ký cân, hệ thống tự động đối chiếu thời hạn đăng kiểm và đưa ra cảnh báo màu đỏ nếu phát hiện phương tiện đã quá hạn hiệu lực.
   - *Giá trị mang lại*: Đảm bảo tính pháp lý của toàn bộ phương tiện ra vào nhà máy, giúp doanh nghiệp tuân thủ nghiêm ngặt Luật Giao thông Đường bộ và bảo vệ an toàn hạ tầng.

4. **Cơ chế kiểm soát và xử lý quá tải thông minh**:
   - *Thực trạng cũ*: Hệ thống cũ chỉ cảnh báo thô sơ khi xe quá tải trọng, không có cơ chế xử lý nghiệp vụ tại hiện trường khiến nhân viên cân thường tự ý bỏ qua hoặc phải yêu cầu xe lùi lại rất phức tạp và mất thời gian.
   - *Giải pháp mới*: Hệ thống tích hợp thuật toán tự động đối chiếu tổng trọng lượng xe cân thực tế với TTCP của phương tiện trong danh mục đăng kiểm. Nếu phát hiện quá tải, hệ thống sẽ chặn không cho in phiếu cân tiêu chuẩn và cung cấp hai cơ chế xử lý nghiệp vụ linh hoạt:
     - **Phê duyệt không tách phiếu (Bỏ qua)**: Cho phép nhân viên cân xác nhận lý do quá tải (ví dụ: hàng hóa đặc thù không thể dỡ lẻ) và lưu lại để xuất báo cáo nhật ký kiểm toán gửi Ban Giám đốc phê duyệt sau.
     - **Tách lượt cân**: Hỗ trợ chia tổng khối lượng tịnh thực xuất thành 2 phiếu giao nhận/phiếu cân con độc lập sao cho tải trọng mỗi lượt đều nằm trong ngưỡng an toàn của xe.
   - *Giá trị mang lại*: Kiểm soát chặt chẽ tải trọng xe rời nhà máy, giảm thiểu rủi ro bị xử phạt hành chính trên đường vận chuyển, đồng thời linh hoạt xử lý trong các tình huống thực tế.

5. **Cảnh báo vượt dung sai hàng bao (Kiểm tra dung sai)**:
   - *Thực trạng cũ*: Không có cơ chế đối chiếu khối lượng tịnh thực xuất với khối lượng kế hoạch của đơn hàng bao. Việc xuất thừa hàng bao cho đối tác diễn ra âm thầm gây thất thoát nghiêm trọng tài sản của nhà máy.
   - *Giải pháp mới*: Tự động áp dụng công thức đối chiếu dung sai cho phép trên từng bao hàng (ví dụ: ±0.5%/bao). Khi cân lần 2, hệ thống tự động tính toán tổng số bao quy đổi và đối chiếu với khối lượng kế hoạch. Nếu khối lượng tịnh thực tế vượt quá ngưỡng dung sai cho phép, phần mềm sẽ hiển thị cảnh báo đỏ nổi bật trên màn hình và khóa nút Lưu lượt cân.
   - *Giá trị mang lại*: Ngăn ngừa hoàn toàn tình trạng xuất thừa hàng hóa ngoài kế hoạch, bảo vệ tài sản của nhà máy và duy trì tính nhất quán của chất lượng sản phẩm giao nhận.

6. **Cơ chế xử lý một xe lấy nhiều mặt hàng, nhiều nhà phân phối (NPP)**:
   - *Thực trạng cũ*: Một xe chỉ có thể gán cho một đơn hàng duy nhất. Nếu một xe lớn muốn lấy nhiều loại sản phẩm hoặc vận chuyển cho nhiều NPP khác nhau, xe bắt buộc phải di chuyển ra vào trạm cân nhiều lần để làm nhiều phiếu cân riêng biệt, cực kỳ tốn thời gian và nhiên liệu.
   - *Giải pháp mới*: Áp dụng mô hình **Phiên Cân**. Cho phép gộp nhiều đơn hàng khác nhau từ các sản phẩm hoặc NPP khác nhau vào cùng một lượt cân xe vật lý. Phần mềm cung cấp giao diện phân bổ khối lượng tịnh thực tế (Phân bổ khối lượng tịnh) cho từng đơn hàng chi tiết một cách trực quan.
   - *Giá trị mang lại*: Tối đa hóa hiệu suất xe vận tải, giảm 60% số lần di chuyển xe vật lý trên bàn cân, tối ưu hóa năng suất giải phóng hàng của nhà máy.

7. **Luồng cân xuất khẩu chuyên dụng cho đơn hàng lớn**:
   - *Thực trạng cũ*: Các hợp đồng xuất khẩu lớn (ví dụ hàng nghìn tấn clinker hoặc xi măng rời) giao nhận qua nhiều chuyến xe độc lập trong nhiều ngày không có luồng quản lý chung. Nhân viên phải ghi chép sổ sách thủ công để cộng dồn sản lượng, rất dễ xảy ra nhầm lẫn khối lượng lũy kế.
   - *Giải pháp mới*: Xây dựng luồng nghiệp vụ **Cân xuất khẩu** chuyên dụng. Cho phép tạo một phiên cân xuất khẩu tổng theo hợp đồng/tàu, hỗ trợ đăng ký nhanh nhiều lượt xe con ra vào lấy hàng. Dữ liệu cân của từng chuyến xe sẽ được tự động cộng dồn lũy kế thời gian thực vào sản lượng xuất khẩu tổng của hợp đồng, có hỗ trợ in Phiếu giao nhận chi tiết từng lượt xe và nút "Chốt sản lượng" để đóng phiên cân xuất khẩu.
   - *Giá trị mang lại*: Số hóa toàn diện hoạt động cân xuất khẩu số lượng lớn, đảm bảo số liệu chốt sản lượng chính xác tuyệt đối với khách hàng và đơn vị vận chuyển.

8. **Báo cáo và thống kê dữ liệu hàng ngày**:
   - *Thực trạng cũ*: Không có giao diện báo cáo tại trạm. Nhân viên trạm cân muốn đối soát sản lượng cuối ngày phải tự ghi chép hoặc đợi kế toán trung tâm xuất báo cáo đối chiếu từ hệ thống ERP rất mất thời gian.
   - *Giải pháp mới*: Tích hợp phân hệ **Báo cáo Thống kê Ngày** trực quan ngay trên ứng dụng máy trạm. Cho phép nhân viên và Trưởng trạm cân truy cập nhanh báo cáo tổng hợp: Tổng số lượt cân, Tổng khối lượng nhập/xuất, Chi tiết sản lượng theo từng sản phẩm, từng NPP/Khách hàng và tình trạng đồng bộ dữ liệu lên ERP của ngày hiện tại.
   - *Giá trị mang lại*: Giúp chốt ca nhanh chóng cuối ngày, phát hiện và xử lý ngay lập tức các sai lệch về sản lượng thực tế giao nhận trước khi chuyển giao ca.

9. **Cấu hình các trường in và máy in mặc định cho từng loại chứng từ**:
   - *Thực trạng cũ*: Định dạng in và máy in được lập trình cố định trong mã nguồn. Khi trạm cân thay đổi loại máy in (ví dụ từ máy in kim sang máy in laser) hoặc mẫu giấy in bị lệch lề, nhân viên không thể tự sửa đổi mà phải chờ bộ phận kỹ thuật can thiệp sửa mã nguồn và đóng gói lại phần mềm.
   - *Giải pháp mới*: Cung cấp tính năng cấu hình in ấn động trực tiếp trên giao diện quản trị của ứng dụng. Quản trị viên có thể tùy chọn máy in mặc định cho từng loại chứng từ (Phiếu cân tổng hợp, Phiếu giao nhận chi tiết) và cấu hình điều chỉnh tọa độ lề in (khoảng lệch ngang offsetX, lệch dọc offsetY) để khớp hoàn hảo với phôi giấy in thực tế.
   - *Giá trị mang lại*: Thích ứng linh hoạt với mọi thay đổi thiết bị phần cứng tại chỗ, giảm thiểu 100% thời gian chờ đợi hỗ trợ kỹ thuật từ xa.

10. **Cơ chế lưu dữ liệu cục bộ và đồng bộ hàng đợi**:
    - *Thực trạng cũ*: Hoạt động cân phụ thuộc hoàn toàn vào đường truyền mạng Internet kết nối với máy chủ ERP trung tâm. Khi mạng gặp sự cố đứt cáp hoặc chập chờn, phần mềm trạm cân bị treo hoặc không thể lưu dữ liệu, gây ách tắc giao thông nghiêm trọng trước cổng nhà máy.
    - *Giải pháp mới*: Thiết kế kiến trúc **Ưu tiên lưu trữ cục bộ (Local-First)**. Mọi thao tác ghi số cân từ đầu đọc, chụp ảnh biển số xe từ camera giám sát, lưu lượt cân và in phiếu đều được thực hiện trực tiếp trên cơ sở dữ liệu cục bộ (SQL Server Express) của máy trạm. Đồng thời, hệ thống sử dụng hàng đợi đồng bộ để tự động đồng bộ tuần tự dữ liệu lên máy chủ ERP khi có kết nối Internet trở lại.
    - *Giá trị mang lại*: Đảm bảo trạm cân hoạt động liên tục 24/7 trước mọi sự cố mạng viễn thông, triệt tiêu hoàn toàn rủi ro đình trệ xuất nhập hàng tại cổng nhà máy.

11. **Cơ chế tự động sao lưu dữ liệu cục bộ hàng ngày**:
    - *Thực trạng cũ*: Không có cơ chế sao lưu dữ liệu cục bộ tự động. Nếu máy tính trạm cân gặp sự cố hỏng ổ cứng, cháy nguồn hoặc bị virus tấn công, toàn bộ dữ liệu lịch sử cân và hình ảnh camera chưa kịp đồng bộ lên ERP sẽ bị mất sạch, không thể khôi phục.
    - *Giải pháp mới*: Tích hợp dịch vụ Windows chạy ngầm tự động thực hiện sao lưu toàn bộ cơ sở dữ liệu cục bộ (sao lưu SQL Server) hàng ngày vào lúc **3:00 AM** ra thư mục lưu trữ chỉ định (có hỗ trợ cấu hình tự động dọn dẹp file sao lưu cũ sau 10 ngày để giải phóng bộ nhớ máy trạm). Đồng thời, cung cấp nút chức năng cho phép quản trị viên sao lưu thủ công tức thì trước khi thực hiện bảo trì.
    - *Giá trị mang lại*: Bảo vệ an toàn dữ liệu vận hành tuyệt đối tại trạm cân trước mọi rủi ro hư hỏng thiết bị phần cứng, đảm bảo thời gian phục hồi mục tiêu (RTO) dưới 30 phút.

---

# 4. Động lực thay đổi
Để đáp ứng tốc độ tăng trưởng sản lượng xuất - nhập hàng và chiến lược số hóa toàn diện của doanh nghiệp, việc nâng cấp lên một hệ thống trạm cân thông minh, kết nối an toàn và hoạt động liên tục (ưu tiên ngoại tuyến) là yêu cầu cấp thiết và bắt buộc.

---

# 5. Phân tích các Bên liên quan

Hệ thống trạm cân mới ảnh hưởng trực tiếp đến các nhóm đối tượng sau:

| Nhóm đối tượng | Vai trò trong dự án | Mối quan tâm chính | Yêu cầu cốt lõi đối với hệ thống |
|----------------|---------------------|--------------------|-----------------------------------|
| **Ban Giám đốc** | Nhà tài trợ dự án | Tối ưu hóa chi phí, tránh thất thoát hàng hóa, đảm bảo tính tuân thủ pháp luật (quá tải). | Hệ thống chạy ổn định, có báo cáo chốt sản lượng chính xác, minh bạch hóa hình ảnh và lịch sử kiểm toán chống gian lận. |
| **Nhân viên trạm cân** | Người vận hành chính | Giao diện trực quan, số chữ cân to dễ đọc, các thao tác in ấn và phân bổ khối lượng nhanh bằng phím tắt. | Tự động đọc số cân từ đầu hiển thị, chụp ảnh tự động từ camera, tự động phân bổ khối lượng khi xe chỉ cân 1 đơn hàng, hiển thị gợi ý kế thừa lượt cân cũ khi đơn hàng bị đổi (cấp lại đơn). Quy trình phân bổ khối lượng tịnh cho từng đơn hàng rõ ràng, tự động tính số bao thực tế, in ấn đầy đủ chứng từ (phiếu giao nhận chi tiết) trước khi cho xe ra. |
| **Quản trị viên trạm cân** | Quản trị kỹ thuật trạm | Sao lưu dự phòng dữ liệu an toàn, cấu hình thiết bị phần cứng linh hoạt. | Quyền cấu hình cổng COM kết nối cân, cài đặt đường dẫn camera RTSP, quản lý tài khoản người dùng trạm cân, thực hiện sao lưu/phục hồi dữ liệu cục bộ chủ động. |

---

# 6. Phạm vi Dự án

### 6.1 Trong phạm vi dự án
Dự án tập trung xây dựng ứng dụng trạm cân cục bộ với các tính năng nghiệp vụ sau:

*   **Quản lý đăng ký phương tiện (Đơn hàng/Hàng đợi xe)**: Tải và đồng bộ danh sách xe đã được đăng ký giao nhận từ ERP xuống máy trạm. Hỗ trợ tạo tay các đơn hàng khẩn cấp hoặc đơn nhập hàng thủ công.
*   **Vận hành lượt cân xe tiêu chuẩn**: 
    - Ghi nhận cân lần 1 và cân lần 2 từ đầu hiển thị cân (chế độ tự động). Hỗ trợ chế độ quản trị viên duyệt nhập tay khối lượng (chế độ thủ công) trong trường hợp khẩn cấp.
    - Hiển thị luồng hình ảnh camera thời gian thực trên giao diện ứng dụng trạm cân để nhân viên giám sát trực quan vị trí đỗ xe của phương tiện trên bàn cân, đảm bảo xe đỗ đúng vị trí trước khi tiến hành ghi nhận khối lượng.
    - Lưu trữ hình ảnh biển số xe trước/sau tự động chụp từ camera IP giao thức RTSP đi kèm lúc cân.
    - Tính toán khối lượng tịnh thực tế của lượt xe.
    - Hỗ trợ luồng cân xuất khẩu chuyên dụng cho các đơn hàng lớn (theo hợp đồng/tàu), quản lý các lượt xe con ra vào lấy hàng, tự động cộng dồn sản lượng lũy kế thời gian thực của hợp đồng và hỗ trợ chốt sản lượng để đóng phiên.
*   **Phân bổ trọng lượng thực tế**: Cho phép nhân viên phân bổ tổng khối lượng tịnh của xe cho một hoặc nhiều đơn hàng đi kèm (gộp đơn cùng xe). Tự động tính số lượng bao thực tế cho hàng bao.
*   **Kiểm soát quá tải & Dung sai**:
    - Đối chiếu khối lượng tịnh thực tế với khối lượng kế hoạch để kiểm tra dung sai hàng bao (dung sai cho phép tính trên mỗi bao). Nhân viên cân được quyền xác nhận bỏ qua để lưu dữ liệu khi vượt dung sai sau khi trao đổi và có sự đồng ý của bộ phận xuất hàng.
    - Đối chiếu tổng khối lượng xe với tải trọng cho phép (TTCP) của phương tiện trong danh mục xe để phát hiện quá tải. Cung cấp quy trình phê duyệt không tách phiếu hoặc tách lượt cân thành 2 phiếu cân/phiếu giao nhận con độc lập nằm trong ngưỡng TTCP cho phép.
*   **In ấn chứng từ**: Hỗ trợ in Phiếu cân tổng hợp cấp lượt cân và Phiếu giao nhận cấp dòng chi tiết trực tiếp ra máy in của trạm.
*   **Quy trình cấp lại đơn (ERP đổi cắt lệnh)**: Nhận biết xe đã cân lần 1 nhưng bị hủy đơn trên ERP, cho phép nhân viên cân xác nhận gắn đơn hàng mới vào lượt cân cũ để kế thừa cân lần 1 trong cửa sổ thời gian 24 giờ.
*   **Tự động & Thủ công Sao lưu dữ liệu cục bộ**: Tác vụ chạy ngầm tự động sao lưu dữ liệu cục bộ hàng ngày lúc 3:00 AM ra thư mục chỉ định, duy trì tệp sao lưu trong 10 ngày để giải phóng bộ nhớ máy trạm, và hỗ trợ quản trị viên thực hiện sao lưu thủ công tức thì.
*   **Đồng bộ dữ liệu ưu tiên ngoại tuyến**: Tự động đưa dữ liệu nghiệp vụ vào hàng đợi đồng bộ để đồng bộ an toàn lên máy chủ trung tâm khi có mạng Internet, hỗ trợ chế độ lưu trữ và vận hành ngoại tuyến bình thường khi mất mạng.

### 6.2 Ngoài phạm vi dự án
Các chức năng sau đây sẽ không được phát triển trong dự án này (do được quản lý bởi các hệ thống khác hoặc để ở giai đoạn sau):

*   **Nghiệp vụ Tài chính & Thanh toán tại trạm**: Trạm cân **chỉ** thực hiện nghiệp vụ cân để xác định trọng lượng hàng hóa thực tế và in chứng từ giao nhận. Không thực hiện thu tiền cân, không thu tiền hàng, không xuất hóa đơn tài chính trực tiếp tại trạm cân. Mọi giao dịch tài chính, thanh toán và quyết toán công nợ sẽ được thực hiện tập trung 100% trên ERP của công ty.
*   **Quản lý Kho bãi chi tiết**: Không quản lý vị trí xếp hàng, quy trình bốc xếp hay sơ đồ kho bãi bên trong nhà máy.
*   **Định vị GPS phương tiện**: Không tích hợp thiết bị giám sát hành trình hay định vị vị trí xe vận chuyển bên ngoài trạm cân.

---

# 7. Yêu cầu Kinh doanh

Các yêu cầu kinh doanh dưới đây định hình các năng lực cốt lõi mà giải pháp trạm cân phải mang lại.

### 7.1 Yêu cầu Chức năng Mức cao

#### BR-001: Đăng ký Phương tiện thông qua ERP
- **Yêu cầu**: Hệ thống phải tự động tải thông tin phương tiện đã đăng ký (Biển số xe, mooc, tên tài xế, mã sản phẩm, khối lượng kế hoạch) từ ERP trung tâm xuống máy trạm để sẵn sàng phục vụ hoạt động cân.
- **Giá trị kinh doanh**: Loại bỏ sai sót nhập liệu thủ công bằng tay của nhân viên cân, tăng tính nhất quán dữ liệu giữa trạm cân và hệ thống ERP.

#### BR-002: Quy trình cân nội địa
- **Yêu cầu**: Hệ thống phải hỗ trợ quy trình cân xe 2 lần (bao gồm cân xe đầy tải và cân xe không tải) để xác định trọng lượng tịnh hàng hóa đối với các đơn hàng giao nhận trong nước. Hệ thống hỗ trợ kế thừa đơn hàng tự động từ ERP, cho phép gộp nhiều đơn hàng hoặc nhiều nhà phân phối (NPP) trên cùng một lượt cân xe vật lý, thực hiện phân bổ khối lượng tịnh và in Phiếu giao nhận/Phiếu cân tổng hợp.
- **Giá trị kinh doanh**: Đảm bảo đo lường chính xác và tự động hóa luồng giao nhận nội địa, giảm thời gian giải phóng xe và đáp ứng linh hoạt các yêu cầu phân bổ khối lượng phức tạp.

#### BR-003: Quy trình cân xuất khẩu
- **Yêu cầu**: Hệ thống phải hỗ trợ luồng nghiệp vụ cân xuất khẩu cho các đơn hàng lớn (theo tàu/hợp đồng). Cho phép tạo phiên cân xuất khẩu tổng, hỗ trợ đăng ký nhanh nhiều chuyến xe con ra vào cân 2 lần liên tục mà không cần tạo đơn mới cho từng chuyến. Dữ liệu cân của từng chuyến xe tự động cộng dồn lũy kế thời gian thực vào sản lượng xuất khẩu tổng của hợp đồng, có hỗ trợ in Phiếu giao nhận chi tiết cho từng chuyến xe và cung cấp chức năng chốt sản lượng để hoàn thành phiên cân xuất khẩu.
- **Giá trị kinh doanh**: Quản lý tập trung sản lượng của các hợp đồng xuất khẩu lớn, ngăn ngừa sai sót khi cộng dồn khối lượng thủ công trên sổ sách, và minh bạch số liệu giao nhận với đối tác quốc tế.

#### BR-004: Giám sát camera thời gian thực & Chụp ảnh đối chiếu
- **Yêu cầu**: Hệ thống phải hiển thị trực tiếp (thời gian thực) luồng hình ảnh từ các camera giám sát lên giao diện ứng dụng trạm cân, giúp nhân viên cân dễ dàng kiểm tra trực quan vị trí đỗ xe của phương tiện trên bàn cân, đảm bảo toàn bộ thân xe nằm trọn vẹn trong vùng cân hợp lệ để tránh sai số đo lường. Đồng thời, hệ thống tự động chụp ảnh biển số trước/sau tại thời điểm nhân viên lưu cân lần 1 và lần 2 để đính kèm hình ảnh đối chiếu vào lịch sử lượt cân.
- **Giá trị kinh doanh**: Đảm bảo tính chính xác và tin cậy tuyệt đối của dữ liệu khối lượng cân (tránh các lỗi sai số hoặc gian lận do đỗ xe sai vị trí), minh bạch hóa quy trình giao nhận và cung cấp bằng chứng hình ảnh đối chứng trực quan khi có khiếu nại hoặc tranh chấp.

#### BR-005: Kiểm soát Dung sai hàng bao (Kiểm tra dung sai)
- **Yêu cầu**: Hệ thống phải tự động đối chiếu khối lượng tịnh thực xuất với khối lượng kế hoạch của đơn hàng bao. Nếu vượt quá ngưỡng dung sai cho phép trên mỗi bao, hệ thống phải cảnh báo và yêu cầu nhân viên cân xác nhận bỏ qua vượt dung sai mới cho phép lưu cân lần 2.
- **Giá trị kinh doanh**: Tránh thất thoát tài sản và hàng hóa của nhà máy do xuất vượt định mức kế hoạch.

#### BR-006: Kiểm soát và xử lý quá tải trọng xe (Xử lý quá tải)
- **Yêu cầu**: Hệ thống phải đối chiếu tổng khối lượng xe cân thực tế với tải trọng cho phép (TTCP) của xe trong danh mục đăng kiểm. Nếu xe bị quá tải, hệ thống phải chặn lại và cho phép nhân viên cân xác nhận xử lý (bằng cách phê duyệt không tách phiếu hoặc thực hiện tách lượt cân thành 2 phiếu con độc lập nằm trong tải trọng cho phép).
- **Giá trị kinh doanh**: Tuân thủ luật an toàn giao thông đường bộ, tránh việc xe chở quá tải trọng rời khỏi nhà máy gây phạt tiền và hư hỏng hạ tầng đường bộ.

#### BR-007: Đồng bộ hóa an toàn ngoại tuyến
- **Yêu cầu**: Hệ thống phải hỗ trợ hoạt động cân xe, chụp ảnh và in ấn bình thường tại trạm cân ngay cả khi mất kết nối mạng Internet. Khi có mạng trở lại, hệ thống phải tự động đồng bộ hóa toàn bộ dữ liệu cục bộ lên máy chủ trung tâm một cách tuần tự và đảm bảo cơ chế chống trùng lặp dữ liệu.
- **Giá trị kinh doanh**: Đảm bảo hoạt động sản xuất, xuất nhập hàng của nhà máy không bao giờ bị dừng lại do sự cố mạng.

#### BR-008: Tự động Sao lưu dữ liệu cục bộ
- **Yêu cầu**: Hệ thống phải tự động thực hiện sao lưu dữ liệu cục bộ SQL Server hàng ngày lúc 3:00 AM ra thư mục chỉ định, duy trì tệp sao lưu trong 10 ngày để giải phóng bộ nhớ máy trạm, và hỗ trợ phục hồi dữ liệu khi có sự cố.
- **Giá trị kinh doanh**: Bảo vệ dữ liệu vận hành cục bộ trước nguy cơ hỏng hóc máy tính trạm cân, giúp trạm cân nhanh chóng khôi phục hoạt động.

---

### 7.2 Yêu cầu Phi Chức năng Mức cao

#### BR-NFR-001: Tính khả dụng và Độ tin cậy
- Phần mềm trạm cân phải đạt tỷ lệ sẵn sàng hoạt động tại máy trạm là 99.9%.
- Hệ thống phải hoạt động ngoại tuyến ổn định liên tục tối thiểu 30 ngày mà không cần kết nối Internet.

#### BR-NFR-002: Bảo mật và Nhật ký kiểm toán
- Phân quyền theo vai trò (RBAC): Chỉ vai trò QUẢN TRỊ VIÊN mới được cấu hình hệ thống, thiết bị, camera và cân tay. Vai trò NHÂN VIÊN CÂN thực hiện cân tự động và bỏ qua dung sai tại hiện trường.
- Ghi nhận đầy đủ nhật ký kiểm toán cho các hành động nhạy cảm như cân tay, bỏ qua dung sai, in lại phiếu, hủy lượt cân hoặc thay đổi cấu hình phần cứng.

#### BR-NFR-003: Tính dễ sử dụng
- Giao diện lập phiếu cân phải tối giản, hiển thị số cân lớn rõ ràng từ khoảng cách xa (kích thước chữ tối thiểu 36pt).
- Hỗ trợ thao tác nhanh qua phím tắt trên bàn phím để nhân viên cân không cần sử dụng chuột, đẩy nhanh tốc độ vận hành.

---

# 8. Giả định và Ràng buộc

### 8.1 Giả định
1. **Thiết bị cân**: Trạm cân vật lý, đầu hiển thị cân hoạt động bình thường và truyền dữ liệu chuỗi liên tục qua cổng nối tiếp COM.
2. **Hạ tầng camera**: Các camera IP giám sát được lắp đặt đúng góc độ, ghi hình rõ nét biển số xe và luồng video giao thức RTSP hoạt động ổn định.
3. **Đào tạo nhân viên**: Nhân viên trạm cân được đào tạo đầy đủ về quy trình sử dụng phần mềm mới và các phím tắt vận hành nhanh.

### 8.2 Ràng buộc
1. **Pháp lý và Kiểm định**: Đầu hiển thị cân vật lý phải được kiểm định định kỳ bởi cơ quan đo lường của Nhà nước để đảm bảo tính pháp lý của số cân ghi nhận.
2. **Hệ điều hành máy trạm**: Ứng dụng máy trạm desktop chạy trên hệ điều hành Windows 10 hoặc 11 (64-bit).
3. **Tương thích cơ sở dữ liệu**: Cơ sở dữ liệu cục bộ sử dụng phiên bản SQL Server Express 2016 trở lên để đảm bảo tính năng đồng bộ và sao lưu tự động hoạt động mượt mà.

---

# 9. Rủi ro và Biện pháp giảm thiểu

| Rủi ro | Tác động | Khả năng xảy ra | Biện pháp giảm thiểu |
|--------|----------|-----------------|----------------------|
| **Sự cố hỏng hóc phần cứng máy trạm** | Cao | Thấp | Cấu hình cơ chế tự động sao lưu dữ liệu hàng ngày ra ổ cứng ngoài hoặc thư mục mạng dùng chung. Chuẩn bị sẵn máy tính trạm dự phòng đã cài đặt sẵn phần mềm để thay thế nhanh trong 30 phút. |
| **Mất kết nối Internet kéo dài** | Trung bình | Trung bình | Cơ chế ưu tiên ngoại tuyến của ứng dụng cho phép cân và in phiếu bình thường. Dữ liệu đồng bộ sẽ tự động nằm trong hàng đợi và gửi đi ngay khi có kết nối trở lại. |
| **Lỗi kết nối cổng COM hoặc mất tín hiệu cân** | Cao | Trung bình | Thiết kế vòng lặp kết nối lại tự động kết nối lại cổng COM bằng thuật toán tăng trần, hiển thị cảnh báo trực quan cho nhân viên cân kiểm tra cáp kết nối vật lý. |
| **Camera IP bị mất kết nối RTSP** | Thấp | Trung bình | Luồng nghiệp vụ cân chính vẫn được thực hiện bình thường (phần mềm bắt lỗi camera, không gây lỗi giao diện). Ghi nhận nhật ký lỗi để quản trị viên kiểm tra lại hạ tầng mạng camera. |

---

# 10. Lịch trình và Mốc quan trọng

Dự kiến lịch trình triển khai dự án WeightStation như sau:

| Giai đoạn | Mốc quan trọng | Thời gian thực hiện | Ngày hoàn thành dự kiến |
|-----------|----------------------------|---------------------|--------------------------|
| **Khởi động** | Phê duyệt tài liệu BRD | 1 tuần | 15/06/2026 |
| **Đặc tả** | Hoàn thiện đặc tả chi tiết SRS | 1 tuần | 22/06/2026 |
| **Phát triển** | Hoàn thành xây dựng giao diện & các kịch bản sử dụng | 6 tuần | 03/08/2026 |
| **Tích hợp** | Hoàn thành tích hợp phần cứng (Cân, Camera) & kết nối đồng bộ | 2 tuần | 17/08/2026 |
| **Kiểm thử** | Hoàn thành kiểm thử nội bộ và thử nghiệm người dùng (UAT) | 2 tuần | 31/08/2026 |
| **Triển khai** | Cấu hình cài đặt thực tế tại các trạm cân & Vận hành chính thức | 1 tuần | 07/09/2026 |

---

# 11. Phê duyệt và Ký duyệt

Tài liệu BRD này đại diện cho sự thống nhất về mặt mục tiêu, phạm vi và yêu cầu kinh doanh của dự án WeightStation giữa các bên liên quan.

| Đại diện các bên | Chức danh / Vai trò | Chữ ký ký duyệt | Ngày ký |
|------------------|---------------------|-----------------|---------|
| | **Nhà tài trợ dự án (Ban Giám đốc)** | | |
| | **Chủ sở hữu sản phẩm (Trưởng bộ phận Vận hành)** | | |
| | **Trưởng nhóm kỹ thuật / Kiến trúc sư giải pháp** | | |
| | **Chuyên viên nghiệp vụ / Quản lý sản phẩm** | | |
