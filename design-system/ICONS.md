# Icon Design System — StationApp

> **Source of Truth** cho tất cả icon trong ứng dụng.  
> **Font Family:** `Segoe MDL2 Assets` (built-in Windows 10/11, vector-based)

---

## ⚠️ Quy tắc bắt buộc

1. **KHÔNG sử dụng emoji** (🔄 ⚙ 📋 ...) làm icon trong bất kỳ file XAML hoặc C# nào
2. **LUÔN dùng Segoe MDL2 Assets** với XML entity syntax: `&#xE72C;`
3. **LUÔN set FontFamily** trên TextBlock icon: `FontFamily="Segoe MDL2 Assets"`
4. **Chuẩn kích thước:** FontSize `13` cho button icon, `15` cho menu icon, `18-22` cho header/title icon
5. **Chuẩn layout:** Icon trong `StackPanel Orientation="Horizontal"` với `Width="24"` (menu) hoặc `Margin="0,0,6,0"` (button)

---

## Bảng Icon Registry

### Sidebar Menu Icons

| Mục | Glyph Code | XML Entity | Mô tả |
|---|---|---|---|
| Header App (Scale) | `E774` | `&#xE774;` | Biểu tượng cân |
| Hamburger Menu | `E700` | `&#xE700;` | Menu 3 gạch |
| Trang Chủ | `E80F` | `&#xE80F;` | Home |
| Lập phiếu cân | `E8C8` | `&#xE8C8;` | Page/Document |
| Danh sách phiếu | `E8A4` | `&#xE8A4;` | Bulleted List |
| Diagnostics | `E9D9` | `&#xE9D9;` | Diagnostic Tool |
| Cấu hình hệ thống | `E713` | `&#xE713;` | Settings Gear |
| Sub-menu Chevron | `E76C` | `&#xE76C;` | ChevronRight |
| User/Contact | `E77B` | `&#xE77B;` | Person |
| Đăng Xuất | `E7E8` | `&#xE7E8;` | Sign Out |

### Action Button Icons

| Hành động | Glyph Code | XML Entity | Mô tả |
|---|---|---|---|
| Làm mới / Refresh | `E72C` | `&#xE72C;` | Sync/Refresh |
| Lưu / Save | `E74E` | `&#xE74E;` | Save (Floppy) |
| Tìm kiếm / Search | `E721` | `&#xE721;` | Search |
| Xoá / Ngừng SD | `E74D` | `&#xE74D;` | Delete |
| Đồng bộ ngay | `E895` | `&#xE895;` | Sync |

### Status & Notification Icons

| Mục đích | Glyph Code | XML/C# | Mô tả |
|---|---|---|---|
| Clock (Thời gian) | `E823` | `&#xE823;` | Clock |
| Warning (Cảnh báo) | `E7BA` | `\uE7BA` | Warning Triangle |
| Success (Thành công) | `E73E` | `\uE73E` | Checkmark |
| Error (Lỗi) | `E711` | `\uE711` | Cancel/X |
| Info (Thông tin) | `E946` | `\uE946` | Info Circle |

---

## Template: Button với Icon

```xml
<!-- ĐÚNG: Icon + Text trong StackPanel -->
<Button Command="{Binding SomeCommand}" Style="{StaticResource UtilityButtonStyle}">
    <StackPanel Orientation="Horizontal">
        <TextBlock Text="&#xE72C;" FontFamily="Segoe MDL2 Assets" FontSize="13" 
                   VerticalAlignment="Center" Margin="0,0,6,0"/>
        <TextBlock Text="LÀM MỚI" VerticalAlignment="Center"/>
    </StackPanel>
</Button>

<!-- SAI: KHÔNG dùng emoji trong Content -->
<!-- <Button Content="🔄 LÀM MỚI" ... /> -->
```

## Template: Menu Item với Icon

```xml
<Button Style="{StaticResource SidebarMenuItemStyle}" 
        Command="{Binding NavigateCommand}" CommandParameter="Dashboard">
    <StackPanel Orientation="Horizontal">
        <TextBlock Text="&#xE80F;" FontFamily="Segoe MDL2 Assets" FontSize="15" 
                   Width="24" VerticalAlignment="Center"/>
        <TextBlock Text="Trang Chủ" VerticalAlignment="Center" Margin="10,0,0,0"/>
    </StackPanel>
</Button>
```

## Template: Icon trong C# Code-Behind

```csharp
// ĐÚNG: Unicode escape sequence
IconText.Text = "\uE73E"; // Checkmark

// SAI: KHÔNG dùng emoji
// IconText.Text = "✅";
```

---

## Checklist khi thêm view mới

- [ ] Tất cả button có icon dùng `Segoe MDL2 Assets`
- [ ] Không có emoji character nào trong XAML hoặc C#
- [ ] Icon size nhất quán: 13 (button), 15 (menu), 18-22 (title)
- [ ] Icon có `Width` hoặc `Margin` cố định để align text
- [ ] Tra bảng Icon Registry ở trên trước khi thêm icon mới

---

## Tham khảo thêm

- [Microsoft Segoe MDL2 Assets Icon List](https://learn.microsoft.com/en-us/windows/apps/design/style/segoe-ui-symbol-font)
- Character Map (Windows) → Font: "Segoe MDL2 Assets"
