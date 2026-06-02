# Quy ước App Version

## 1. Mục tiêu

Chuẩn hóa version của `StationApp` để:

- hiển thị trong app nhất quán
- ghi xuống DB nhất quán
- so sánh cập nhật chính xác
- script phát hành không bị nhập nhầm
- tên thư mục build/package dễ truy vết

## 2. Format chuẩn

Chỉ dùng **semantic version 3 phần**:

```text
major.minor.patch
```

Ví dụ hợp lệ:

- `1.0.0`
- `1.0.1`
- `1.1.0`
- `2.0.0`

Ví dụ không hợp lệ:

- `1.0`
- `1.0.0 0601`
- `v1.0.0`
- `1.0.0-beta`

## 3. Quy tắc tăng version

### 3.1 Sửa bug nhỏ

Tăng `patch`.

Ví dụ:

- `1.0.0 -> 1.0.1`
- `1.0.1 -> 1.0.2`

Áp dụng cho:

- sửa lỗi nghiệp vụ
- sửa lỗi UI
- sửa lỗi SQL/function/procedure
- sửa lỗi sync/update

### 3.2 Thêm tính năng nhưng vẫn tương thích

Tăng `minor`, reset `patch` về `0`.

Ví dụ:

- `1.0.7 -> 1.1.0`

### 3.3 Thay đổi lớn hoặc phá tương thích

Tăng `major`, reset `minor` và `patch` về `0`.

Ví dụ:

- `1.4.3 -> 2.0.0`

## 4. Nguồn version duy nhất

Nguồn version chuẩn của bản build là:

- property `StationAppVersion`

MSBuild sẽ dùng giá trị này để sinh:

- `Version`
- `AssemblyVersion`
- `FileVersion`
- `InformationalVersion`

Runtime app, update manifest, và `AppVersion` ghi xuống dữ liệu nghiệp vụ đều phải lấy từ cùng nguồn này.

## 5. Rule bắt buộc

- không phát hành nếu không truyền `-AppVersion`
- `-AppVersion` phải đúng format `x.y.z`
- không dùng version theo ngày kiểu `1.0.0 0601`
- không dùng hậu tố `-beta`, `-rc`, `v1.0.0`

## 6. Tên output chuẩn

Từ nay output build/phát hành phải có cả version trong tên:

- thư mục local:
  - `Builds\StationApp_1.0.3_20260601_162238`
- package zip:
  - `StationApp_1.0.3_20260601_162238.zip`
- manifest version:
  - `StationApp_1.0.3_20260601_162238.json`

## 7. Script phát hành chuẩn

### 7.1 Publish local

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-ui-release.ps1 `
  -Configuration Release `
  -RuntimeIdentifier win-x64 `
  -ConfigPath "src/StationApp.UI/appsettings.json" `
  -AppVersion "1.0.1" `
  -ReleaseNotes "Fix bug can xuat khau" `
  -SkipDatabaseSchemaUpdate
```

### 7.2 Publish lên shared folder

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-shared-folder-release.ps1 `
  -Configuration Release `
  -RuntimeIdentifier win-x64 `
  -ConfigPath "src/StationApp.UI/appsettings.json" `
  -SharedReleaseRoot "\\10.0.0.3\17. data dung chung\Chienbn\Phan_mem_can" `
  -AppVersion "1.0.1" `
  -ReleaseNotes "Fix bug can xuat khau"
```

## 8. Tác động trong hệ thống

Giá trị version được dùng thống nhất cho:

- text version hiển thị trong app
- so sánh cập nhật
- `latest.json` trên shared folder
- `AppVersion` ghi xuống DB ở các bản ghi nghiệp vụ
