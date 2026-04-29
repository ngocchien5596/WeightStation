# ĐỀ XUẤT GIAO DIỆN LOGIN - STATIONAPP (CẨM PHẢ CEMENT)

Chào bạn, dựa trên yêu cầu tối giản và chuyên nghiệp, tôi đã thiết kế lại màn hình Login tập trung vào nhận diện thương hiệu và sự rõ ràng.

## 1. MOCKUP GIAO DIỆN

![Login Mockup](file:///C:/Users/Chienbn/.gemini/antigravity/brain/9b21dc16-5868-458d-bff7-68da7f84a22f/stationapp_login_mockup_1777364436273.png)

## 2. CHI TIẾT THIẾT KẾ (MINIMALISM)

### A. Khu vực Thương hiệu (Top)
- **Logo:** Đặt chính giữa trên cùng (sử dụng logo Xi măng Cẩm Phả).
- **Tên công ty:** "CÔNG TY CỔ PHẦN XI MĂNG CẨM PHẢ" (Font: Inter/Segoe UI, Bold, màu Navy #1B2631).
- **Tên hệ thống:** "Hệ thống quản lý trạm cân" (Font: Inter, Medium, màu Blue #3B82F6).

### B. Khu vực Nhập liệu (Center)
- **Card:** Nền trắng, bo góc (12px), bóng đổ nhẹ (Shadow Depth: Medium).
- **Username:** Ô nhập liệu có icon người dùng, label ẩn (Watermark/Placeholder).
- **Password:** Ô nhập liệu có icon khóa, hỗ trợ Enter để đăng nhập.
- **Nút Đăng nhập:** Màu xanh thương hiệu, hiệu ứng Hover mượt mà.

### C. Thông tin bản quyền (Bottom)
- **Copyright:** "Copyright by 2026 CNTT" (Căn giữa, cỡ chữ nhỏ 10-11px, màu Slate-400).

## 3. CẤU TRÚC XAML DỰ KIẾN (TÓM TẮT)

```xml
<Grid Background="#F8FAFC">
    <StackPanel VerticalAlignment="Center" HorizontalAlignment="Center" Width="400">
        <!-- Logo & Header -->
        <Image Source="Logo_CamPha.png" Width="120" Margin="0,0,0,20"/>
        <TextBlock Text="CÔNG TY CỔ PHẦN XI MĂNG CẨM PHẢ" Style="{StaticResource CompanyNameStyle}"/>
        <TextBlock Text="Hệ thống quản lý trạm cân" Style="{StaticResource SystemNameStyle}"/>

        <!-- Login Card -->
        <Border Background="White" CornerRadius="12" Padding="30" Margin="0,30,0,10" Effect="{StaticResource DropShadow}">
            <StackPanel>
                <TextBox x:Name="txtUsername" Tag="Tên đăng nhập" Style="{StaticResource ModernTextBox}"/>
                <PasswordBox x:Name="txtPassword" Tag="Mật khẩu" Style="{StaticResource ModernPasswordBox}" Margin="0,20,0,0"/>
                
                <!-- Nút Đăng nhập -->
                <Button Content="ĐĂNG NHẬP" Style="{StaticResource PrimaryButton}" Margin="0,30,0,0"/>
            </StackPanel>
        </Border>

        <!-- Footer -->
        <TextBlock Text="Copyright by 2026 CNTT" Style="{StaticResource FooterTextStyle}"/>
    </StackPanel>
</Grid>
```

---
> [!TIP]
> Bạn thấy mockup và phong cách này đã đủ "tối giản" và "chuyên nghiệp" chưa? Nếu ok, tôi sẽ tiến hành cập nhật vào Plan chính và bắt đầu triển khai.
