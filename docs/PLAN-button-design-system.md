# Chuẩn Hóa Hệ Thống Nút Bấm - Button Design System cho StationApp.UI

## Mục tiêu:
Thiết kế và áp dụng hệ thống style nút bấm dùng chung cho toàn bộ ứng dụng WPF StationApp.UI thông qua XAML ResourceDictionary.

Không hard-code màu/font/style trực tiếp trong từng View hoặc code-behind.

### 1. Core UI Framework

`[NEW]` `Styles/ButtonStyles.xaml`

Tạo ResourceDictionary mới tại:
`StationApp.UI/Styles/ButtonStyles.xaml`

#### 1.1. Button Design Tokens

Định nghĩa token dùng chung:
- FontFamily: Segoe UI
- FontWeight: SemiBold hoặc Bold
- BorderThickness: 0
- BorderRadius:
  + Small: 6px
  + Medium: 8px
  + Large: 10px
- Height:
  + Small: 30px
  + Medium: 38px
  + Large: 46px
- MinWidth:
  + Small: 90px
  + Medium: 120px
  + Large: 150px
- Padding:
  + Small: 12,4
  + Medium: 16,6
  + Large: 20,8

#### 1.2. Button Role / Color Tokens

PrimaryAction:
- Normal: #2E86C1
- Hover: #3498DB
- Pressed: #21618C
- DisabledBackground: #C9D6E2
- DisabledForeground: #7A8A99

WorkflowAction:
- Normal: #0F4C75
- Hover: #145A8D
- Pressed: #0B3A59
- DisabledBackground: #D9DEE3
- DisabledForeground: #8A949E

UtilityAction:
- Normal: #5D6D7E
- Hover: #708090
- Pressed: #465A69
- DisabledBackground: #D9DEE3
- DisabledForeground: #8A949E

DangerAction:
- Normal: #C0392B
- Hover: #E74C3C
- Pressed: #922B21
- DisabledBackground: #E6D8D6
- DisabledForeground: #A0706A

NeutralAction:
- Normal: #7F8C8D
- Hover: #95A5A6
- Pressed: #626E70
- DisabledBackground: #D9DEE3
- DisabledForeground: #8A949E

#### 1.3. Button States

Mỗi style phải hỗ trợ đủ:
- Normal
- Hover / MouseOver
- Pressed
- Disabled

Yêu cầu:
- Disabled không fallback về màu xám mặc định của WPF.
- Disabled phải dùng đúng DisabledBackground và DisabledForeground theo từng role.
- Hover và Pressed chỉ áp dụng khi IsEnabled = true.
- Không đưa logic nghiệp vụ vào Style.

#### 1.4. Button Styles

Tạo các Style dùng chung:
- PrimaryButtonStyle
- WorkflowButtonStyle
- UtilityButtonStyle
- DangerButtonStyle
- NeutralButtonStyle

Mỗi Style:
- TargetType="{x:Type Button}"
- Dùng ControlTemplate riêng để vẽ Border bo góc.
- Set Background, Foreground, BorderBrush, BorderThickness.
- Set FontFamily, FontWeight, FontSize.
- Set Height, MinWidth, Padding.
- Set Cursor="Hand" khi enabled.
- Dùng Trigger cho IsMouseOver, IsPressed, IsEnabled=false.
- Medium size là mặc định cho các button nghiệp vụ.

### 2. App Resource Integration

`[MODIFY]` `App.xaml`

Merge ButtonStyles.xaml vào Application.Resources:

```xml
<ResourceDictionary.MergedDictionaries>
    <ResourceDictionary Source="Styles/ButtonStyles.xaml" />
</ResourceDictionary.MergedDictionaries>
```

Yêu cầu:
- Không làm mất ResourceDictionary hiện có.
- Nếu App.xaml đã có MergedDictionaries, chỉ thêm ButtonStyles.xaml vào danh sách.
- Đảm bảo không lỗi path resource.

### 3. Refactor WeighingView.xaml

`[MODIFY]` `WeighingView.xaml`

Refactor toàn bộ button nghiệp vụ trong màn cân sang StaticResource.

Mapping đúng theo text và mục đích sử dụng:

- IN PGN
  Ý nghĩa: In phiếu giao nhận
  Style="{StaticResource UtilityButtonStyle}"

- IN PC
  Ý nghĩa: In phiếu cân
  Style="{StaticResource UtilityButtonStyle}"

- LƯU
  Ý nghĩa: Lưu số cân lần 1 hoặc số cân lần 2 xuống dữ liệu
  Style="{StaticResource PrimaryButtonStyle}"

- CÂN LẦN 1
  Ý nghĩa: Lấy số cân hiện tại để đưa vào ô Cân lần 1
  Style="{StaticResource WorkflowButtonStyle}"

- CÂN LẦN 2
  Ý nghĩa: Lấy số cân hiện tại để đưa vào ô Cân lần 2
  Style="{StaticResource PrimaryButtonStyle}"

- HỦY
  Ý nghĩa: Hủy thao tác hoặc hủy phiếu theo điều kiện nghiệp vụ
  Style="{StaticResource DangerButtonStyle}"

- PHIẾU LIÊN QUAN
  Ý nghĩa: Xem / mở phiếu liên quan
  Style="{StaticResource NeutralButtonStyle}"

Lưu ý:
- Không còn nút “GIAO NHẬN”.
- Nút cũ “GIAO NHẬN” phải được đổi text thành “IN PGN”.
- Nút cũ “IN PHIẾU” phải được đổi text thành “IN PC”.

Yêu cầu:
- Không đổi Command binding hiện có nếu command vẫn đúng nghiệp vụ.
- Nếu command cũ đang đặt tên theo Giao nhận/In phiếu nhưng thực tế dùng để in PGN/In PC, cần đổi tên command/property cho rõ nghĩa nếu phạm vi refactor cho phép.
- Không đổi Click event hiện có nếu không cần.
- Không đổi IsEnabled binding hiện có.
- Không đổi Visibility binding hiện có.
- Không đổi layout lớn của màn hình.
- Chỉ remove inline style liên quan đến màu/font/border nếu đã thay bằng StaticResource.
- Không đưa logic nghiệp vụ vào XAML style.

### 4. Đồng bộ button cho các màn khác

`[MODIFY]` `Other XAML Views`

Sau khi refactor WeighingView.xaml, rà soát toàn bộ file *.xaml trong StationApp.UI để đồng bộ button style.

Phạm vi:
- Tất cả file *.xaml trong project StationApp.UI.
- Ưu tiên màn có button nghiệp vụ, lưu, hủy, in, tìm kiếm, đóng, xác nhận, xóa.

Mapping chung:

- Lưu / Xác nhận / Hoàn tất / Đồng ý
  → PrimaryButtonStyle

- Cân / Xử lý / Thực hiện
  → WorkflowButtonStyle

- In / In PGN / In PC / Tìm kiếm / Làm mới / Xem chi tiết / Phiếu liên quan
  → UtilityButtonStyle

- Hủy / Xóa / Từ chối
  → DangerButtonStyle

- Đóng / Quay lại / Bỏ qua
  → NeutralButtonStyle

Yêu cầu:
- Tất cả Button phải dùng style từ Button Design System nếu phù hợp.
- Không để button hard-code màu/font/border riêng lẻ nếu không có lý do đặc biệt.
- Không thay đổi Command binding, Click event, IsEnabled binding, Visibility binding hoặc layout nghiệp vụ hiện có.
- Chỉ refactor phần giao diện button.
- Nếu button chưa rõ vai trò, chọn style theo ý nghĩa nghiệp vụ gần nhất.
- Nếu không chắc chắn, dùng NeutralButtonStyle.
- Không tạo style riêng cho từng màn nếu không có nhu cầu đặc biệt.

### 5. Business Interaction Rules

Style system chỉ xử lý giao diện.

ViewModel/code-behind vẫn chịu trách nhiệm điều khiển:
- Khi nào IN PGN enable/disable.
- Khi nào IN PC enable/disable.
- Khi nào CÂN LẦN 1 enable/disable.
- Khi nào CÂN LẦN 2 enable/disable.
- Khi nào LƯU enable/disable.
- Khi nào HỦY enable/disable.
- Khi nào PHIẾU LIÊN QUAN enable/disable.

Không set màu thủ công trong ViewModel/code-behind.

### 6. Verification Plan

#### 6.1. Build
- Chạy dotnet build.
- Không có lỗi XAML resource.
- Không có lỗi StaticResource not found.
- Không có lỗi binding phát sinh.

#### 6.2. Visual Check
Kiểm tra từng style:
- PrimaryButtonStyle
- WorkflowButtonStyle
- UtilityButtonStyle
- DangerButtonStyle
- NeutralButtonStyle

Mỗi style phải có đủ:
- Normal
- Hover
- Pressed
- Disabled

#### 6.3. Disabled Check
- Disabled button không về màu xám mặc định của WPF.
- Disabled text vẫn đọc được.
- Disabled button không nổi bật hơn enabled button.

#### 6.4. WeighingView Check
- IN PGN hiển thị text đúng là “IN PGN” và dùng UtilityButtonStyle.
- IN PC hiển thị text đúng là “IN PC” và dùng UtilityButtonStyle.
- LƯU dùng PrimaryButtonStyle.
- CÂN LẦN 1 dùng WorkflowButtonStyle.
- CÂN LẦN 2 dùng PrimaryButtonStyle.
- HỦY dùng DangerButtonStyle.
- PHIẾU LIÊN QUAN dùng NeutralButtonStyle.
- Không còn text button “GIAO NHẬN”.
- Không còn text button “IN PHIẾU” nếu nút đó thực tế là in phiếu cân.

#### 6.5. Whole App Check
- Các màn XAML khác đã được rà soát.
- Button ở các màn chính đều dùng StaticResource từ ButtonStyles.xaml.
- Không còn tình trạng mỗi màn một kiểu button.
- Không còn hard-code màu button nếu màu đó đã thuộc Design System.
- Không có duplicated style block cho từng button.
- Không có business logic trong ButtonStyles.xaml.

### Acceptance Criteria:
- Button Design System được tạo bằng WPF XAML ResourceDictionary.
- App.xaml đã merge ButtonStyles.xaml thành công.
- WeighingView.xaml đã dùng StaticResource cho toàn bộ button nghiệp vụ.
- Nút “IN PGN” dùng để in phiếu giao nhận.
- Nút “IN PC” dùng để in phiếu cân.
- Không còn nút “GIAO NHẬN” trên WeighingView.
- Không còn nút “IN PHIẾU” nếu ý nghĩa thực tế là in phiếu cân.
- Các màn XAML khác trong StationApp.UI đã được rà soát và đồng bộ button style.
- Giao diện button toàn ứng dụng đồng bộ, hiện đại, rõ phân cấp hành động.
- LƯU và CÂN LẦN 2 nổi bật nhất trong màn cân.
- HỦY / XÓA là nhóm nút nguy hiểm duy nhất dùng màu đỏ.
- IN PGN, IN PC, PHIẾU LIÊN QUAN, TÌM KIẾM, XEM CHI TIẾT không nổi bật hơn nút chính.
- Disabled state đẹp, dịu, dễ đọc.
- dotnet build thành công.
