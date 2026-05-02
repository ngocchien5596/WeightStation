# KẾ HOẠCH CHI TIẾT
# TRIỂN KHAI CHỨC NĂNG GỢI Ý NHẬP LIỆU (AUTOCOMPLETE / TYPEAHEAD) Ở MÀN DANH SÁCH XE VÀO

Bạn là Principal WPF Product Engineer + UX/System Design Architect + .NET Search Interaction Engineer của dự án StationApp.

Nhiệm vụ của bạn là thiết kế và triển khai chức năng gợi ý nhập liệu realtime cho các ô input ở màn Danh sách xe vào, theo đúng nguyên tắc:

*   người dùng gõ tới đâu
*   hệ thống tự động gợi ý các giá trị phù hợp với chuỗi đang nhập
*   danh sách gợi ý hiển thị ngay bên dưới ô input
*   giao diện phải đồng bộ với system design hiện tại của app
*   phải làm theo đúng thứ tự:
    *   Review hiện trạng
    *   System design
    *   UI/UX design
    *   Implementation
    *   Test

Tài liệu này là source of truth cho chức năng autocomplete ở màn Danh sách xe vào.

## 1. MỤC TIÊU

Tôi cần ở màn Danh sách xe vào có chức năng gợi ý nhập liệu cho các ô input.

**Kết quả mong muốn**

Khi người dùng gõ vào một ô nhập liệu:

*   hệ thống tìm các giá trị phù hợp với chuỗi đã gõ
*   hiển thị một danh sách gợi ý ngay bên dưới ô input
*   người dùng có thể:
    *   click chọn
    *   dùng bàn phím để chọn
*   khi chọn xong:
    *   giá trị được điền vào ô input
    *   form cập nhật đúng
    *   không phá flow nhập liệu hiện tại

**Mục tiêu UX**
*   nhập liệu nhanh hơn
*   giảm sai chính tả
*   giảm tạo trùng dữ liệu
*   phù hợp với vận hành trạm cân
*   không làm giao diện bị lệch phong cách app

## 2. PHẠM VI ÁP DỤNG

Chức năng này áp dụng cho màn Danh sách xe vào.

**Áp dụng cho 2 tình huống:**
*   Khu vực tạo thủ công xe vào / xe nhập hàng
*   Khu vực tìm kiếm / lọc dữ liệu

**Các field bắt buộc phải review và áp dụng autocomplete nếu đang là input text**
*   Số PTVC
*   Mooc
*   Tên tài xế
*   Khách hàng
*   Mã sản phẩm
*   Tên sản phẩm

**Lưu ý về field TTCP**
*   đổi cách gọi hiển thị từ TTCP 10% thành TTCP
*   TTCP không nằm trong danh sách bắt buộc áp dụng autocomplete
*   nếu field này là field tính toán / read-only / lấy từ master data xe thì không cần autocomplete
*   chỉ review để đảm bảo label/UI thống nhất là TTCP

**Không áp dụng cho**
*   trường số cân
*   trường ngày giờ
*   checkbox
*   field read-only
*   các field không có nguồn dữ liệu gợi ý hữu ích

## 3. NGUYÊN TẮC CHUNG CỦA AUTOCOMPLETE

### 3.1 Gợi ý theo chuỗi đang gõ

Autocomplete phải hoạt động theo kiểu:

*   user gõ ký tự
*   hệ thống tìm giá trị chứa hoặc bắt đầu bằng chuỗi đó
*   hiển thị suggestion list ngay dưới input

### 3.2 Không chờ bấm nút tìm

Đây là tính năng realtime theo typing, không phải search bằng nút.

### 3.3 Không phá nhập liệu tay

Người dùng vẫn phải được phép:

*   tiếp tục gõ tay
*   không bắt buộc chọn gợi ý

### 3.4 Không khóa UI

Autocomplete phải mượt:

*   không giật
*   không block UI thread
*   không gọi DB mỗi ký tự theo kiểu nặng nề

## 4. SYSTEM DESIGN BẮT BUỘC

Trước khi code, phải thiết kế Autocomplete System Design cho toàn màn.

System design phải trả lời rõ:

*   field nào có autocomplete
*   field đó lấy dữ liệu từ đâu
*   kiểu matching là gì
*   khi chọn suggestion thì auto-fill gì thêm
*   keyboard behavior thế nào
*   popup hiển thị ra sao
*   cách đồng bộ với system design hiện tại của app

## 5. NGUỒN DỮ LIỆU GỢI Ý

Phải thiết kế rõ từng field sẽ lấy suggestion từ đâu. Không được làm mơ hồ.

### 5.1 Số PTVC

Nguồn gợi ý ưu tiên:

*   master data phương tiện
*   lịch sử registration gần đây
*   dữ liệu vận hành gần đây nếu có

### 5.2 Mooc

Nguồn gợi ý:

*   master data mooc
*   lịch sử registration / session gần đây

### 5.3 Tên tài xế

Nguồn gợi ý:

*   lịch sử registration / session
*   master data tài xế nếu có
*   nếu chưa có master data tài xế thì dùng dữ liệu lịch sử

### 5.4 Khách hàng

Nguồn gợi ý:

*   master data khách hàng
*   nếu hệ thống đang dùng chung với nhà phân phối thì bám đúng nguồn hiện có trong codebase
*   phải nêu rõ source thực tế trong phần review

### 5.5 Mã sản phẩm

Nguồn gợi ý:

*   master data sản phẩm

### 5.6 Tên sản phẩm

Nguồn gợi ý:

*   master data sản phẩm
*   nên dùng cùng source với Mã sản phẩm

### 5.7 Quy tắc chung

Mỗi field phải có suggestion source riêng hoặc mapping source rõ ràng, không trả lời chung chung kiểu “lấy từ database”.

## 6. KIỂU MATCHING PHẢI DÙNG

Autocomplete phải dùng matching hợp lý theo từng field.

### 6.1 Mức tối thiểu

Phải hỗ trợ:

*   StartsWith
*   hoặc Contains

### 6.2 Khuyến nghị theo field

**Với Số PTVC, Mooc, Mã sản phẩm**

Ưu tiên:

*   StartsWith

**Với Tên tài xế, Khách hàng, Tên sản phẩm**

Có thể dùng:

*   Contains
*   hoặc StartsWith + Contains fallback

### 6.3 Chuẩn hóa chuỗi

Phải xử lý tối thiểu:

*   trim khoảng trắng đầu/cuối
*   không phân biệt hoa thường

## 7. TRIGGER HIỂN THỊ GỢI Ý

### 7.1 Khi nào bắt đầu gợi ý

Khuyến nghị:

*   với field ngắn như mã / biển số / mooc: bắt đầu từ 1 ký tự
*   với field tên dài như khách hàng / tài xế / tên sản phẩm: bắt đầu từ 2 ký tự

### 7.2 Debounce

Phải có debounce để tránh query quá dày.

Khuyến nghị:

*   150ms đến 300ms

### 7.3 Khi nào ẩn gợi ý

Suggestion list phải ẩn khi:

*   input rỗng
*   user chọn xong một item
*   user nhấn Esc
*   input mất focus và không chuyển xuống suggestion list
*   không còn kết quả phù hợp

## 8. UI/UX DESIGN CHO DROPDOWN GỢI Ý

Đây là phần bắt buộc phải làm đẹp, đúng style.

### 8.1 Vị trí hiển thị

Danh sách gợi ý phải hiển thị:

*   ngay bên dưới ô input
*   cùng bề rộng hoặc tối thiểu align theo ô input
*   không bật popup ở nơi xa
*   không làm lệch layout

### 8.2 Hình thức hiển thị

Danh sách gợi ý nên là:

*   popup/dropdown nhẹ
*   nền sáng
*   viền rõ
*   item hover/selected rõ
*   cùng font/cỡ chữ với app
*   khoảng cách item vừa phải
*   có scroll nếu nhiều kết quả

### 8.3 Đồng bộ system design

Phải bám style app hiện tại:

*   palette màu hiện có
*   input style hiện có
*   border, spacing, typography cùng hệ
*   không dùng dropdown mặc định xấu nếu không kiểm soát được UI

### 8.4 Nội dung từng item

**Với Số PTVC**
*   dòng chính: số PTVC / biển số
*   dòng phụ: mooc / tài xế / thông tin phụ nếu có

**Với Mooc**
*   dòng chính: mooc number
*   dòng phụ: PTVC liên quan nếu có

**Với Tên tài xế**
*   dòng chính: tên tài xế
*   dòng phụ: PTVC hoặc mooc gần nhất nếu có

**Với Khách hàng**
*   dòng chính: tên khách hàng
*   dòng phụ: mã khách hàng nếu có

**Với Mã sản phẩm**
*   dòng chính: mã sản phẩm
*   dòng phụ: tên sản phẩm

**Với Tên sản phẩm**
*   dòng chính: tên sản phẩm
*   dòng phụ: mã sản phẩm

## 9. HÀNH VI BÀN PHÍM VÀ CHUỘT

Autocomplete phải dùng được cả chuột và bàn phím.

### 9.1 Chuột
*   click item -> chọn item
*   điền dữ liệu vào input
*   ẩn dropdown

### 9.2 Bàn phím

Phải hỗ trợ:

*   Down Arrow: di chuyển xuống danh sách gợi ý
*   Up Arrow: di chuyển lên
*   Enter: chọn item đang highlight
*   Esc: đóng danh sách gợi ý

### 9.3 Rule mới bắt buộc cho Tab

**Nếu danh sách gợi ý hiện tại chỉ có đúng 1 kết quả**
*   khi user nhấn Tab
*   hệ thống phải tự chọn gợi ý đó
*   điền giá trị vào input
*   rồi mới chuyển focus sang field tiếp theo

**Nếu có nhiều hơn 1 gợi ý**
*   Tab không được tự đoán chọn bừa
*   xử lý theo rule bình thường của form:
    *   đóng popup hoặc giữ giá trị user đã gõ
    *   rồi chuyển focus
*   phải ghi rõ behavior này trong design

**Nếu không có gợi ý nào**
*   Tab xử lý bình thường như input thường
*   chuyển focus sang control tiếp theo

## 10. HÀNH VI SAU KHI CHỌN GỢI Ý

Sau khi user chọn một gợi ý:

### 10.1 Với field đơn giản

Ví dụ:

*   Tên tài xế
*   Khách hàng
*   Tên sản phẩm

Thì:

*   điền giá trị vào đúng ô input

### 10.2 Với field có dữ liệu liên quan

Ví dụ chọn Số PTVC, hệ thống có thể auto-fill thêm:

*   Mooc
*   Tên tài xế
*   TTCP
*   thông tin master data liên quan nếu có sẵn

### 10.3 Rule bắt buộc

Phải thiết kế rõ:

*   field nào chỉ fill chính nó
*   field nào được phép auto-fill field liên quan

### 10.4 Khuyến nghị auto-fill

**Chọn Số PTVC**

có thể auto-fill:

*   Mooc
*   Tên tài xế
*   TTCP
*   hạn đăng kiểm nếu UI hiện có field đó và dữ liệu có sẵn

**Chọn Mã sản phẩm**

có thể auto-fill:

*   Tên sản phẩm

**Chọn Tên sản phẩm**

có thể auto-fill:

*   Mã sản phẩm

**Chọn Khách hàng**

có thể auto-fill:

*   mã/tên còn lại nếu form đang dùng song song

## 11. RULE CHO 2 NHÓM FIELD

Phải phân biệt rõ 2 nhóm field:

### 11.1 Field tạo dữ liệu

Là các field ở form tạo xe vào / tạo xe nhập hàng.

Autocomplete ở đây nhằm:

*   nhập liệu nhanh
*   reuse dữ liệu cũ/master data
*   auto-fill các field liên quan

### 11.2 Field tìm kiếm / lọc

Autocomplete ở đây nhằm:

*   gợi ý giá trị đã có để tìm nhanh
*   không auto-fill các field khác
*   không làm thay đổi dữ liệu form tạo mới

## 12. THIẾT KẾ KỸ THUẬT BẮT BUỘC

Phải thiết kế thành cơ chế reusable, không code chắp vá từng ô.

### 12.1 Cần có abstraction chung

Khuyến nghị tạo các thành phần:

*   IAutocompleteService
*   AutocompleteQuery
*   AutocompleteItem
*   AutocompletePopup hoặc control tương đương
*   AutocompleteBehavior / helper cho TextBox

### 12.2 AutocompleteItem

Phải hỗ trợ tối thiểu:

*   Value
*   DisplayText
*   SecondaryText
*   Payload
*   FieldType

### 12.3 IAutocompleteService

Phải có khả năng:

*   nhận field type + query text
*   trả về danh sách suggestion tương ứng

Ví dụ field types:

*   Vehicle
*   Mooc
*   Driver
*   Customer
*   ProductCode
*   ProductName

### 12.4 Không duplicate logic

Không được viết từng cụm logic riêng rời rạc cho từng TextBox nếu có thể tái sử dụng chung.

## 13. PERFORMANCE RULES

### 13.1 Không query quá nặng

Autocomplete phải nhẹ:

*   có debounce
*   limit số lượng kết quả
*   không load full table rồi filter trên UI nếu bảng lớn

### 13.2 Giới hạn số lượng item

Khuyến nghị:

*   tối đa 10 đến 20 item mỗi dropdown

### 13.3 Query có filter phù hợp

Ví dụ:

*   customer autocomplete chỉ lấy customer active hoặc relevant nếu business cần
*   product autocomplete chỉ lấy sản phẩm hợp lệ
*   vehicle/mooc ưu tiên dữ liệu đang active hoặc recent

### 13.4 Cache nhẹ nếu phù hợp

Có thể cache:

*   product master
*   customer master
*   vehicle master nhỏ

Nhưng phải ghi rõ cache strategy trong design nếu dùng.

## 14. DATA / SOURCE MAPPING BẮT BUỘC

Phải có bảng mapping rõ ràng như sau trong output:

*   Số PTVC -> Source thực tế: ...
*   Mooc -> Source thực tế: ...
*   Tên tài xế -> Source thực tế: ...
*   Khách hàng -> Source thực tế: ...
*   Mã sản phẩm -> Source thực tế: ...
*   Tên sản phẩm -> Source thực tế: ...

Không được viết mơ hồ kiểu “lấy từ DB”.

## 15. UI STATE RULES

### 15.1 Khi input rỗng
*   không hiện suggestion

### 15.2 Khi không có kết quả

Khuyến nghị:

*   hiện một dòng nhẹ:
    *   “Không có dữ liệu phù hợp”

### 15.3 Khi field bị disable/read-only
*   không bật autocomplete

## 16. PHÂN QUYỀN

Autocomplete ở màn Danh sách xe vào phải dùng được cho:

*   ADMIN
*   OPERATOR

Miễn là user có quyền vào màn này.

## 17. TÍCH HỢP VỚI SYSTEM DESIGN HIỆN TẠI

Đây là yêu cầu bắt buộc.

### 17.1 Trước khi code phải làm system design

AI/dev phải xuất ra:

*   design cho autocomplete dropdown
*   cách hiển thị item
*   cách focus
*   cách dùng Tab
*   cách đồng bộ với palette/input style hiện tại

### 17.2 Không làm UI lệch tông

Không được:

*   dùng popup mặc định xấu
*   dùng màu lệch hệ
*   dùng font/spacing khác app

### 17.3 Reuse resource hiện có

Phải reuse:

*   input styles
*   popup/dropdown styles nếu có
*   typography
*   colors
*   border styles

## 18. PHẠM VI FILE / CODE CẦN RÀ

Tối thiểu phải rà và sửa/tạo:

*   IncomingVehicleListView.xaml
*   IncomingVehicleListViewModel.cs
*   shared styles/resources của app
*   input controls hiện có
*   repository/query service cho:
    *   vehicle
    *   mooc
    *   driver
    *   customer
    *   product
*   autocomplete service
*   popup/dropdown control
*   focus/keyboard handling logic

## 19. THỨ TỰ TRIỂN KHAI

**Bước 1 — Review hiện trạng**
*   đọc màn Danh sách xe vào hiện tại
*   xác định các input field
*   xác định data source thực tế cho từng field
*   xác định shared style/resources hiện có
*   xác định popup/dropdown pattern hiện có nếu có
*   xác định label TTCP 10% ở đâu và đổi thành TTCP

**Bước 2 — System design**
*   thiết kế autocomplete architecture
*   field-to-source mapping
*   keyboard/focus behavior
*   Tab behavior
*   popup design
*   auto-fill rules

**Bước 3 — UI/UX design**
*   style dropdown
*   item layout
*   selected/hover state
*   no-result state
*   alignment dưới input

**Bước 4 — Implement**
*   file-by-file
*   không pseudo-code
*   không làm chắp vá từng field
*   ưu tiên reusable solution

**Bước 5 — Test**
*   gõ và ra suggestion đúng
*   chọn bằng chuột
*   chọn bằng Enter
*   chọn bằng Tab khi chỉ có 1 gợi ý
*   không tự chọn sai khi có nhiều gợi ý
*   auto-fill field liên quan nếu có
*   không block UI
*   đúng style system design

## 20. OUTPUT BẮT BUỘC

Trả kết quả theo format:

**A. REVIEW HIỆN TRẠNG**
*   màn Danh sách xe vào hiện có những field nào
*   field nào cần autocomplete
*   source thực tế của từng field là gì
*   label TTCP đã được cập nhật ở đâu
*   style resources hiện tại là gì

**B. SYSTEM DESIGN**
*   autocomplete architecture
*   field-to-source mapping
*   popup behavior
*   keyboard/focus rules
*   Tab selection rule
*   auto-fill rules

**C. UI/UX DESIGN**
*   dropdown style
*   item layout
*   selected/hover state
*   no-result state
*   alignment dưới input

**D. IMPLEMENTATION**
*   file tree các file tạo/sửa
*   code file-by-file
*   reusable controls/services
*   không pseudo-code

**E. TEST NOTES**
*   typing flow
*   mouse select
*   Enter select
*   Tab select when exactly one suggestion
*   no result
*   debounce
*   auto-fill behavior
*   style consistency

## 21. QUALITY GATE

Không được coi là xong nếu:

*   chưa đổi label TTCP 10% thành TTCP
*   thiếu autocomplete cho các field bắt buộc:
    *   Số PTVC
    *   Mooc
    *   Tên tài xế
    *   Khách hàng
    *   Mã sản phẩm
    *   Tên sản phẩm
*   suggestion không hiển thị ngay dưới input
*   UI dropdown lệch style app
*   không dùng được bằng bàn phím
*   không hỗ trợ Tab chọn khi chỉ có 1 gợi ý
*   Tab tự chọn sai khi có nhiều hơn 1 gợi ý
*   không có debounce
*   query quá nặng
*   không chỉ rõ source dữ liệu từng field
*   code copy/paste rời rạc thay vì reusable

## 22. MỤC TIÊU CUỐI CÙNG

Tôi cần ở màn Danh sách xe vào có chức năng autocomplete/typeahead đúng chuẩn:

*   label TTCP đúng theo UI mới
*   gợi ý realtime cho các field bắt buộc
*   suggestion hiển thị ngay dưới ô input
*   click hoặc dùng bàn phím để chọn
*   nhấn Tab sẽ tự chọn nếu chỉ có đúng 1 gợi ý
*   giao diện đẹp, đồng bộ với system design hiện tại
*   code đủ chuẩn để mở rộng tiếp cho các màn khác nếu cần
