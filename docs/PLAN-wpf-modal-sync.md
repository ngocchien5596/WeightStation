Bạn là Principal WPF UI Architect + Senior Product Designer của dự án StationApp.

Nhiệm vụ của bạn là **đồng bộ toàn bộ giao diện modal/dialog/popup xác nhận trong ứng dụng** để chúng có cùng style với giao diện Station App hiện tại.

Rất quan trọng:
- Không được nhảy vào code ngay
- Phải làm theo đúng thứ tự:
  1. **System Design**
  2. **Implementation**
  3. **Audit & rollout cho tất cả modal cùng loại**
- Không được chỉ sửa một modal duy nhất
- Phải thiết kế được một **modal design system / reusable modal framework**
- Màu sắc, spacing, button style, typography phải đồng bộ với giao diện ứng dụng hiện tại

==================================================
1. BỐI CẢNH
==================================================

Ứng dụng hiện tại là desktop WPF nội bộ, style tổng thể đang có các đặc trưng:
- sidebar nền xanh đậm
- header/topbar xanh đậm
- button chính màu xanh
- button hủy màu đỏ
- giao diện sạch, rõ, thiên về vận hành nội bộ
- không phải style web consumer
- không phải style Material quá hiện đại
- không dùng modal Windows mặc định kiểu cũ nếu có thể tránh

Hiện có các modal/popup xác nhận kiểu như:
- xác nhận quá tải / tách phiếu
- xác nhận hủy
- cảnh báo thao tác không hợp lệ
- hộp thoại chọn loại in
- các confirm/warning/info dialog tương tự

Vấn đề hiện tại:
- modal đang không đồng bộ với giao diện ứng dụng
- một số modal có thể đang dùng MessageBox mặc định hoặc style không đẹp
- cần chuẩn hóa toàn bộ

==================================================
2. MỤC TIÊU
==================================================

Thiết kế và triển khai một hệ thống modal/dialog thống nhất cho toàn app, với các mục tiêu:

1. Đồng bộ giao diện với Station App hiện tại
2. Đẹp, rõ, dễ vận hành
3. Reusable, không copy-paste style từng modal
4. Có thể áp dụng cho tất cả modal confirm/warning/error/info/question tương tự
5. Hỗ trợ tốt cho các case nghiệp vụ:
   - confirm Yes/No
   - confirm OK/Cancel
   - warning
   - error
   - info
   - selection dialog nhẹ nếu cần
6. Không phá MVVM
7. Không phụ thuộc thư viện mới nếu không cần thiết

==================================================
3. PHẢI LÀM SYSTEM DESIGN TRƯỚC
==================================================

Trước khi code, bạn phải xuất ra **System Design đầy đủ** cho modal/dialog system.

==================================================
4. PHẠM VI MODAL CẦN CHUẨN HÓA
==================================================

Bạn phải rà và chuẩn hóa ít nhất các loại modal sau:

### A. Confirm modal
Ví dụ:
- “Trọng lượng hàng vượt TTCP 10%. Bạn có muốn tách phiếu cân không?”
- “Bạn có chắc muốn hủy phiếu không?”

### B. Warning modal
Ví dụ:
- dữ liệu chưa hợp lệ
- thiết bị chưa sẵn sàng
- không thể thực hiện cân lần 2

### C. Error modal
Ví dụ:
- lỗi in phiếu
- lỗi lưu dữ liệu
- lỗi mở thiết bị

### D. Info modal
Ví dụ:
- thông báo trạng thái
- các thông báo cần xác nhận nhẹ

### E. Selection modal nhẹ
Ví dụ:
- chọn in phiếu cân / phiếu giao nhận / in cả hai
- modal danh sách phiếu liên quan nếu dùng kiểu popup/dialog

Nếu hiện tại có các modal custom khác trong app, hãy rà và gom chúng về cùng design system nếu phù hợp.

==================================================
5. YÊU CẦU SYSTEM DESIGN
==================================================

Bạn phải thiết kế các phần sau:

## 5.1 Information Architecture
Định nghĩa rõ:
- modal framework dùng cho toàn app
- loại modal nào dùng cho trường hợp nào
- khi nào dùng toast, khi nào dùng modal
- khi nào dùng non-blocking, khi nào dùng blocking confirm

## 5.2 Visual Design Rules
Thiết kế rõ:
- kích thước modal
- khoảng trắng
- padding
- tiêu đề
- icon
- màu nền
- màu border
- button primary/secondary/danger
- typography
- màu chữ
- shadow/border radius nếu có
- overlay nền mờ nếu dùng

## 5.3 Interaction Design
- mở modal như thế nào
- keyboard support:
  - Enter
  - Esc
  - default button
- focus management
- close behavior
- disable click outside nếu là confirm quan trọng
- giữ context màn hình phía sau

## 5.4 Modal Types
Phải định nghĩa ít nhất các loại:
- ConfirmDialog
- WarningDialog
- ErrorDialog
- InfoDialog
- SelectionDialog

## 5.5 Service/Architecture Design
Phải đề xuất abstraction để ViewModel gọi modal mà không phá MVVM, ví dụ:
- `IDialogService`
- `IModalService`
- `IConfirmationDialogService`
- hoặc kiến trúc tương đương

ViewModel không được new thẳng `Window` bừa bãi nếu dự án đang theo MVVM.

## 5.6 Reusability Strategy
Phải chỉ rõ:
- style chung ở đâu
- template chung ở đâu
- dialog base class/base view model nếu cần
- cách thêm modal mới về sau mà vẫn đồng bộ style

==================================================
6. YÊU CẦU GIAO DIỆN MODAL
==================================================

## 6.1 Tinh thần giao diện
Modal phải đồng bộ với Station App hiện tại:
- nền sáng sạch
- viền/border rõ nhưng không nặng
- header/title tone xanh đậm hoặc nhấn vừa phải
- nút chính màu xanh
- nút danger màu đỏ
- button phụ màu xám/xanh đậm muted
- icon cảnh báo/thông tin/lỗi phải hiện đại vừa phải, không xấu như MessageBox mặc định Windows

## 6.2 Không được dùng MessageBox mặc định của Windows cho các case nghiệp vụ chính
Nếu hiện tại app đang dùng:
- `MessageBox.Show(...)`
thì phải xem là legacy path và cần chuyển dần sang modal system mới, tối thiểu với các modal nghiệp vụ quan trọng.

## 6.3 Kích thước và bố cục
Modal phải:
- gọn
- dễ đọc
- không quá to
- không lệch tông với app

Khuyến nghị:
- title ở trên
- icon + message ở giữa
- button action căn phải hoặc căn giữa hợp lý
- khoảng cách đẹp, desktop-friendly

## 6.4 Button style
Phải đồng bộ với app:
- Primary: xanh
- Secondary: xanh đậm muted hoặc xám xanh
- Danger: đỏ
- Disabled: xám rõ ràng

Ví dụ với modal quá tải:
- `Tách phiếu` = primary
- `Không` = secondary

Không dùng Yes/No kiểu Windows cổ điển nếu UX có thể cải thiện bằng text nghiệp vụ rõ hơn.

==================================================
7. ÁP DỤNG CHO CASE THỰC TẾ ĐANG CÓ
==================================================

Phải dùng case đang thấy trên màn WeightView làm ví dụ bắt buộc.

### Case 1 — Quá tải
Modal hiện tại:
- “Trọng lượng hàng vượt TTCP 10%. Bạn có muốn tách phiếu cân không?”

Phải redesign lại để:
- đẹp hơn
- đồng bộ app
- text rõ
- button rõ nghĩa

Khuyến nghị text button:
- `Tách phiếu`
- `Không`

hoặc nếu muốn giữ generic:
- `Có`
- `Không`
nhưng phải giải thích trong design

### Case 2 — Hủy phiếu
- confirm hủy
- warning rõ
- nút danger rõ

### Case 3 — In phiếu
- modal chọn loại in
- đồng bộ style

### Case 4 — Phiếu liên quan
Nếu có popup/dialog liên quan, cũng phải bám design system mới

==================================================
8. YÊU CẦU TRIỂN KHAI KỸ THUẬT
==================================================

Bạn phải làm theo thứ tự:

### Bước 1 — Review hiện trạng
- rà tất cả modal/dialog/popup hiện có
- tìm các chỗ đang dùng:
  - `MessageBox.Show`
  - custom popup
  - dialog window riêng
- liệt kê tất cả các modal kiểu này trong app

### Bước 2 — System Design
- xuất ra modal design system
- modal service design
- reusable styles/templates
- mapping case-by-case

### Bước 3 — Implement
- file-by-file
- không pseudo-code
- tạo reusable modal components/services/styles
- áp dụng ít nhất cho các modal trọng yếu trước:
  - overweight confirm
  - cancel confirm
  - print selection
  - warning/error/info dialog chuẩn

### Bước 4 — Audit & rollout
- rà lại toàn solution
- thay thế hoặc wrap các modal cùng loại bằng design system mới
- báo rõ chỗ nào đã migrate xong
- chỗ nào còn legacy
- nếu chưa migrate hết, phải liệt kê backlog rõ

==================================================
9. FILE / CODE PHẠM VI CẦN RÀ
==================================================

Tùy cấu trúc repo, tối thiểu phải rà:

- `WeighingViewModel.cs`
- các command liên quan:
  - save
  - cancel
  - overweight split
  - print
- toàn bộ các nơi dùng `MessageBox.Show`
- dialog/popup hiện có
- resource dictionaries / styles hiện có
- App-level shared UI resources
- service layer / MVVM helpers

Nếu chưa có dialog service chuẩn:
- tạo mới

==================================================
10. OUTPUT BẮT BUỘC
==================================================

Trả kết quả theo format:

## A. REVIEW HIỆN TRẠNG
- app đang có những loại modal nào
- chỗ nào đang dùng MessageBox mặc định
- chỗ nào style lệch ứng dụng

## B. SYSTEM DESIGN
- modal design system
- visual rules
- interaction rules
- modal types
- service architecture
- reuse strategy

## C. IMPLEMENTATION PLAN
- file tree các file tạo/sửa
- apply strategy
- thứ tự migrate modal

## D. IMPLEMENTATION
- code file-by-file
- XAML
- styles
- dialog windows / user controls
- dialog service
- usage examples trong ViewModel

## E. AUDIT & ROLLOUT REPORT
- modal nào đã được migrate
- modal nào còn legacy
- bước tiếp theo nếu chưa xong toàn bộ

## F. TEST NOTES
- keyboard support
- focus
- selection preservation của WeightView
- modal open/close behavior
- visual consistency

==================================================
11. QUALITY GATE
==================================================

Không được coi là xong nếu:
- chỉ sửa 1 modal riêng lẻ
- vẫn còn confirm nghiệp vụ chính dùng MessageBox Windows mặc định
- modal mới không đồng bộ màu sắc với app
- button style không đồng bộ
- phá MVVM
- mở modal làm mất selection/focus logic của WeightView
- không có audit các modal cùng loại trong toàn app

==================================================
12. MỤC TIÊU CUỐI CÙNG
==================================================

Tôi muốn tất cả modal kiểu xác nhận/cảnh báo/chọn lựa trong app:
- nhìn cùng một hệ thiết kế
- đồng bộ với giao diện Station App hiện tại
- dễ dùng cho operator
- đủ đẹp, đủ chuyên nghiệp cho production nội bộ
- và có kiến trúc để về sau thêm modal mới không bị lệch style nữa
