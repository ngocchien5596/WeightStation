# Kế hoạch triển khai cập nhật ứng dụng qua Shared Folder nội bộ

## 1. Mục tiêu

Triển khai cơ chế cập nhật `StationApp` ngay trong ứng dụng, để tại máy trạm cân người dùng bấm nút `Cập nhật` là hệ thống tự:

- kiểm tra phiên bản mới trên shared folder nội bộ
- tải package phát hành mới
- đóng app an toàn
- thay thế file ứng dụng
- chạy `DbMigrator`
- mở lại app

Không còn phải copy thủ công thư mục build vào máy trạm.

## 2. Kết luận kiến trúc

Kiến trúc phù hợp nhất với dự án hiện tại là:

- dùng `shared folder` trong mạng LAN nội bộ làm nguồn phát hành
- tạo `StationApp.Updater` chạy ngoài process app chính
- app chính chỉ làm:
  - kiểm tra phiên bản
  - hiển thị thông tin bản mới
  - gọi updater rồi tự thoát
- updater sẽ:
  - backup
  - giải nén package
  - copy file
  - giữ lại `appsettings.json`
  - chạy `DbMigrator`
  - rollback nếu lỗi

Không dùng cơ chế app tự ghi đè chính nó khi đang chạy.

## 3. Phạm vi phase 1

Phase đầu đã chốt:

- nguồn update là 1 shared folder cố định
- update thủ công theo yêu cầu người dùng
- role được phép dùng:
  - `Admin`
  - `Operator`
- không tự động kiểm tra update khi startup
- có backup và rollback
- có chạy `DbMigrator` trong updater flow

Chưa làm trong phase 1:

- auto update silent
- nhiều channel `stable/beta`
- differential update
- ký số package

## 4. Hạ tầng phát hành

### 4.1 Shared folder

Đường dẫn chính thức:

```text
\\10.0.0.3\17. data dung chung\Chienbn\Phan_mem_can
```

Phân quyền:

- máy build/release có quyền ghi
- máy trạm cân có quyền đọc
- không cho máy trạm ghi ngược lại

### 4.2 Thư mục cài app chuẩn trên máy trạm

Đường dẫn chuẩn đã chốt:

```text
C:\Users\hungnt232\Desktop\PMCan
```

Thông tin này vẫn cần vì updater phải biết chính xác:

- thư mục app hiện tại
- nơi backup
- nơi ghi đè file mới
- nơi mở lại `StationApp.UI.exe`

## 5. Metadata bản phát hành

### 5.1 File `latest.json`

Shared folder phải có file:

```json
{
  "version": "1.0.4",
  "packageName": "StationApp_1.0.4_20260601_223418.zip",
  "packagePath": "\\\\10.0.0.3\\17. data dung chung\\Chienbn\\Phan_mem_can\\StationApp_1.0.4_20260601_223418.zip",
  "sha256": "430B5570C5D5FB43A8B352ACE90AC500AC4EFE423422F4E1E7DC932D12D18D60",
  "publishedAt": "2026-06-01T22:36:11.1305452+07:00",
  "releaseNotes": "Fix self-contained update package",
  "dbMigratorRequired": false,
  "minSupportedVersion": "1.0.0"
}
```

### 5.2 Ý nghĩa các field

- `version`: phiên bản chuẩn theo semantic version `major.minor.patch`
- `packageName`: tên file zip phát hành
- `packagePath`: đường dẫn UNC để updater tải/copy
- `sha256`: kiểm tra integrity package
- `publishedAt`: thời điểm phát hành
- `releaseNotes`: ghi chú bản phát hành
- `dbMigratorRequired`: updater có phải chạy migrator hay không
- `minSupportedVersion`: nếu app hiện tại thấp hơn mốc này thì chặn in-app update

### 5.3 Rule sử dụng `dbMigratorRequired`

Chỉ bật `dbMigratorRequired = true` khi bản phát hành có thay đổi liên quan đến DB, ví dụ:

- thêm/sửa schema
- thêm/sửa function SQL
- thêm/sửa procedure SQL
- thay đổi dữ liệu khởi tạo bắt buộc phải chạy migrator

Không bật `dbMigratorRequired` cho các bản chỉ sửa:

- UI
- code nghiệp vụ trong app
- luồng update app
- logging
- cấu hình không cần migrate DB

### 5.4 Lưu ý về quyền chạy `DbMigrator`

`dbMigratorRequired = true` không đồng nghĩa máy trạm chắc chắn chạy migrator thành công.

Việc `DbMigrator` có chạy được hay không phụ thuộc vào:

- connection string trên máy trạm
- account DB mà máy trạm đang dùng
- account đó có đủ quyền DDL hay không

Nếu máy trạm đang dùng `runtime account` hạn chế thì updater có thể:

- copy file app thành công
- nhưng fail ở bước chạy `DbMigrator`

Vì vậy cần phân biệt rõ:

- `dbMigratorRequired = true`
  - nghĩa là bản update này cần migrate DB
- nhưng không tự đảm bảo máy trạm có đủ quyền để migrate

## 6. Chuẩn version

Version của app đã chuẩn hóa thống nhất theo:

```text
major.minor.patch
```

Ví dụ hợp lệ:

- `1.0.0`
- `1.0.1`
- `1.1.0`

Không dùng nữa:

- `1.0.0 0601`
- `v1.0.0`
- `1.0`

## 7. Quy tắc tăng version

- sửa bug nhỏ: tăng `patch`
  - `1.0.0 -> 1.0.1`
- thêm tính năng nhưng vẫn tương thích: tăng `minor`
  - `1.0.7 -> 1.1.0`
- thay đổi lớn/phá tương thích: tăng `major`
  - `1.4.3 -> 2.0.0`

## 8. Luồng phát hành chuẩn

### 8.1 Build ra thư mục local trong `Builds`

Script chuẩn:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-ui-release.ps1 `
  -Configuration Release `
  -RuntimeIdentifier win-x64 `
  -ConfigPath "src/StationApp.UI/appsettings.json" `
  -AppVersion "1.0.1" `
  -ReleaseNotes "Fix bug can xuat khau" `
  -SkipDatabaseSchemaUpdate
```

Kết quả:

- sinh thư mục dạng:
  - `Builds\StationApp_1.0.1_yyyyMMdd_HHmmss`
- có đủ:
  - app publish
  - `Tools\Updater`
  - `Tools\DbMigrator`

### 8.1.1 Trường hợp chỉ cần build local, không cập nhật DB trên máy build

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-ui-release.ps1 `
  -Configuration Release `
  -RuntimeIdentifier win-x64 `
  -ConfigPath "src/StationApp.UI/appsettings.json" `
  -AppVersion "1.0.4" `
  -ReleaseNotes "Fix UI, fix nghiep vu" `
  -SkipDatabaseSchemaUpdate
```

### 8.1.2 Trường hợp build local và có chạy update DB trên máy build

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-ui-release.ps1 `
  -Configuration Release `
  -RuntimeIdentifier win-x64 `
  -ConfigPath "src/StationApp.UI/appsettings.json" `
  -AppVersion "1.0.5" `
  -ReleaseNotes "Fix bug + cap nhat schema"
```

Lưu ý:

- `publish-ui-release.ps1` chỉ tạo bộ build local trong thư mục `Builds`
- script này không có cờ `-DbMigratorRequired`
- việc máy trạm khi update có chạy migrator hay không chỉ được quyết định ở script phát hành lên shared folder

### 8.2 Build và đẩy lên shared folder

Script chuẩn:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-shared-folder-release.ps1 `
  -Configuration Release `
  -RuntimeIdentifier win-x64 `
  -ConfigPath "src/StationApp.UI/appsettings.json" `
  -SharedReleaseRoot "\\10.0.0.3\17. data dung chung\Chienbn\Phan_mem_can" `
  -AppVersion "1.0.1" `
  -ReleaseNotes "Fix bug can xuat khau"
```

Nếu bản phát hành có thay đổi DB/SQL object thì thêm:

```powershell
  -DbMigratorRequired
```

Kết quả:

- build local ra:
  - `Builds\StationApp_1.0.1_yyyyMMdd_HHmmss`
- sinh package:
  - `StationApp_1.0.1_yyyyMMdd_HHmmss.zip`
- sinh manifest theo version:
  - `StationApp_1.0.1_yyyyMMdd_HHmmss.json`
- cập nhật:
  - `latest.json`

### 8.3 Lưu ý bắt buộc cho các bản dùng để test auto-update

- Các bản phát hành từ `1.0.4` trở đi đã được verify là `self-contained` đúng nghĩa.
- Các gói cũ phát hành trước khi sửa `publish-ui-release.ps1` có thể vẫn là `framework-dependent`, dù manifest để `dbMigratorRequired = false`.
- Khi test chức năng `Cập nhật ứng dụng`, ưu tiên dùng bản trên shared folder từ `1.0.4` trở đi.
- Không sửa tay riêng mỗi `latest.json` nếu package zip tương ứng được build từ script cũ, vì updater vẫn sẽ copy đúng package cũ đó.

## 9. Quy tắc đặt tên output

Từ nay output phải có cả version trong tên:

- thư mục local:
  - `Builds\StationApp_1.0.3_20260601_162238`
- package zip:
  - `StationApp_1.0.3_20260601_162238.zip`
- manifest version:
  - `StationApp_1.0.3_20260601_162238.json`

Mục tiêu:

- dễ truy vết bản build
- tránh nhầm giữa nhiều bản cùng ngày
- dễ đối chiếu package với `latest.json`

## 10. Màn hình cập nhật ứng dụng

Phần cập nhật ứng dụng đã được tách ra thành 1 màn chức năng riêng, không nằm lẫn trong `Tham số hệ thống`.

Hiện trạng UI:

- có menu riêng: `Cập nhật ứng dụng`
- `Admin` và `Operator` đều truy cập được
- màn hình hiển thị:
  - phiên bản hiện tại
  - phiên bản mới nhất
  - trạng thái
  - ghi chú phát hành
  - nút `Kiểm tra cập nhật`
  - nút `Cập nhật ngay`

## 11. Luồng kiểm tra cập nhật

1. User mở màn `Cập nhật ứng dụng`
2. User bấm `Kiểm tra cập nhật`
3. App đọc `latest.json` từ shared folder
4. App so sánh:
   - version hiện tại từ assembly
   - version trong manifest
5. Nếu không có bản mới:
   - báo `Đang là phiên bản mới nhất`
6. Nếu có bản mới:
   - hiển thị version mới
   - hiển thị `releaseNotes`
   - cho phép bấm `Cập nhật ngay`

## 12. Điều kiện cho phép update

Update chỉ được thực hiện khi:

- user có role `Admin` hoặc `Operator`
- đọc được `latest.json`
- version trong manifest lớn hơn version hiện tại
- version hiện tại không thấp hơn `minSupportedVersion`
- package zip tồn tại
- `sha256` khớp

Không update nếu:

- bản mới nhỏ hơn hoặc bằng bản hiện tại
- manifest lỗi hoặc thiếu file
- hash sai
- current version quá cũ so với `minSupportedVersion`

## 13. Luồng thực hiện update

1. App copy package zip từ shared folder về local temp/cache
2. App kiểm tra `sha256`
3. App gọi `StationApp.Updater.exe`
4. App chính tự thoát
5. Updater chờ app tắt hẳn
6. Updater backup thư mục app hiện tại
7. Updater giải nén package mới
8. Updater copy file vào thư mục cài đặt
9. Updater giữ lại `appsettings.json` local
10. Updater chạy `DbMigrator`
11. Nếu thành công:
   - dọn temp
   - mở lại app
12. Nếu lỗi:
   - rollback từ backup
   - ghi log
   - báo lỗi

Lưu ý:

- nếu `dbMigratorRequired = false` thì updater sẽ bỏ qua bước chạy `DbMigrator`
- đây là mode mặc định an toàn cho các bản chỉ fix app/UI

## 14. Xử lý `appsettings.json`

Đây là rule bắt buộc:

- không được ghi đè mù `appsettings.json`
- phải giữ nguyên thông tin cũ trên máy trạm
- hiện tại file này đang lưu thông tin kết nối DB
- nếu về sau thêm config mới thì chỉ được:
  - bổ sung key mới còn thiếu
  - không được xóa giá trị cũ

## 15. Database trong flow update

Updater bắt buộc phải chạy `DbMigrator`.

Lý do:

- app runtime không còn là nơi deploy SQL object
- các object như:
  - `fn_GetCutOrderNetWeight`
  - `sp_GetCutOrderNetWeight`
  phải được cập nhật đúng version
- nếu chỉ copy file app mà không migrate DB thì có thể mismatch schema/object

Điều này chỉ áp dụng khi manifest phát hành bật:

```json
"dbMigratorRequired": true
```

Với các bản chỉ sửa app/UI, manifest phải để:

```json
"dbMigratorRequired": false
```

## 15.1 Ma trận phát hành và vận hành

### Trường hợp A - Release không đổi DB

Ví dụ:

- sửa UI
- sửa logic app
- sửa updater
- sửa logging

Cách phát hành:

- không truyền `-DbMigratorRequired`

Máy trạm:

- update in-app bình thường
- không cần chạy `DbMigrator`

Lệnh phát hành đầy đủ:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-shared-folder-release.ps1 `
  -Configuration Release `
  -RuntimeIdentifier win-x64 `
  -ConfigPath "src/StationApp.UI/appsettings.json" `
  -SharedReleaseRoot "\\10.0.0.3\17. data dung chung\Chienbn\Phan_mem_can" `
  -AppVersion "1.0.4" `
  -ReleaseNotes "Fix UI, fix nghiep vu"
```

### Trường hợp B - Release có đổi DB, và DB đã được deploy trước

Ví dụ:

- có sửa schema/function/procedure
- DBA hoặc deploy account đã chạy migrator trước trên DB thật

Cách phát hành:

- có thể vẫn để `dbMigratorRequired = false`
- vì DB thực tế đã được cập nhật trước rồi

Máy trạm:

- chỉ update app binary
- không cần tự migrate

Đây là cách an toàn hơn cho môi trường vận hành thực tế.

Lệnh phát hành đầy đủ:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-shared-folder-release.ps1 `
  -Configuration Release `
  -RuntimeIdentifier win-x64 `
  -ConfigPath "src/StationApp.UI/appsettings.json" `
  -SharedReleaseRoot "\\10.0.0.3\17. data dung chung\Chienbn\Phan_mem_can" `
  -AppVersion "1.0.5" `
  -ReleaseNotes "Fix bug + da deploy DB truoc"
```

### Trường hợp C - Release có đổi DB, và muốn máy trạm tự migrate

Ví dụ:

- update nhỏ
- môi trường cho phép máy trạm có quyền chạy migrator

Cách phát hành:

- truyền `-DbMigratorRequired`

Điều kiện bắt buộc:

- account DB trên máy trạm phải đủ quyền DDL để migrator chạy được

Rủi ro:

- nếu máy trạm chỉ có runtime account hạn chế thì update sẽ rollback vì migrator fail

Khuyến nghị:

- chỉ dùng trường hợp này khi đã xác nhận rõ quyền DB ở máy trạm
- không coi đây là mặc định

Lệnh phát hành đầy đủ:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-shared-folder-release.ps1 `
  -Configuration Release `
  -RuntimeIdentifier win-x64 `
  -ConfigPath "src/StationApp.UI/appsettings.json" `
  -SharedReleaseRoot "\\10.0.0.3\17. data dung chung\Chienbn\Phan_mem_can" `
  -AppVersion "1.0.6" `
  -ReleaseNotes "Fix bug + cap nhat schema" `
  -DbMigratorRequired
```

## 15.2 Cách deploy DB trước bằng deploy account

Đây là cách khuyến nghị cho môi trường vận hành thực tế.

### Bước 1 - Cấp quyền deploy

- mở [GrantDeployPermissions_StationAppLocal.sql](g:/Source-code/pmcan_C#/scripts/sql/GrantDeployPermissions_StationAppLocal.sql)
- sửa `@DbUserName` thành đúng DB user dùng để deploy
- chạy script này bằng account DBA hoặc account có quyền cấp quyền

### Bước 2 - Chuẩn bị connection string cho deploy account

Có thể dùng:

- SQL login có quyền deploy, ví dụ:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=StationAppLocal;User Id=stationapp_deploy;Password=MatKhau;Encrypt=False;TrustServerCertificate=True;"
  }
}
```

- hoặc Windows account đã được map vào DB user deploy

### Bước 3 - Chạy `DbMigrator` trước

Có thể chạy bằng config riêng:

```powershell
dotnet run --project src\StationApp.DbMigrator\StationApp.DbMigrator.csproj -- --config appsettings.deploy.json
```

Hoặc truyền thẳng connection string:

```powershell
dotnet run --project src\StationApp.DbMigrator\StationApp.DbMigrator.csproj -- --connection "Server=.;Database=StationAppLocal;User Id=stationapp_deploy;Password=MatKhau;Encrypt=False;TrustServerCertificate=True;"
```

### Bước 4 - Phát hành app sau khi DB đã được deploy trước

Khi DB thật đã update xong, phát hành lên shared folder mà không bật `-DbMigratorRequired`:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-shared-folder-release.ps1 `
  -Configuration Release `
  -RuntimeIdentifier win-x64 `
  -ConfigPath "src/StationApp.UI/appsettings.json" `
  -SharedReleaseRoot "\\10.0.0.3\17. data dung chung\Chienbn\Phan_mem_can" `
  -AppVersion "1.0.5" `
  -ReleaseNotes "Fix bug + da deploy DB truoc"
```

## 15.3 Cách setup máy trạm có quyền tự chạy `DbMigrator`

Chỉ dùng khi thật sự muốn máy trạm tự migrate DB.

### Hướng 1 - Giữ `Trusted_Connection=True`

Máy trạm hiện đang đọc connection string từ `appsettings.json` và dùng Windows account hiện tại.

Muốn tự chạy migrator được thì:

- Windows user chạy app trên máy trạm phải được map vào DB user có quyền deploy
- DB user đó phải được grant theo [GrantDeployPermissions_StationAppLocal.sql](g:/Source-code/pmcan_C#/scripts/sql/GrantDeployPermissions_StationAppLocal.sql)

### Hướng 2 - Đổi sang SQL login có quyền deploy

Sửa `appsettings.json` trên máy trạm:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=StationAppLocal;User Id=stationapp_deploy;Password=MatKhau;Encrypt=False;TrustServerCertificate=True;"
  }
}
```

Sau đó mới phát hành bản có:

```powershell
-DbMigratorRequired
```

### Cảnh báo

- cách này làm máy trạm mang quyền DB mạnh hơn
- nếu không chắc về quyền DB thì không nên dùng
- khuyến nghị ưu tiên deploy DB trước bằng deploy account riêng

## 16. Logging và rollback

### 16.1 App chính

Phải log:

- kiểm tra update
- đọc manifest
- so sánh version
- gọi updater

### 16.2 Updater

Phải log riêng:

- thời điểm bắt đầu
- package đang dùng
- backup path
- copy file
- chạy migrator
- rollback có xảy ra hay không

## 17. Các lỗi đã gặp và đã chốt cách xử lý

### 17.1 Lỗi switch parameter khi publish shared folder

Đã sửa:

- `publish-shared-folder-release.ps1` không còn truyền `-SelfContained` qua hashtable
- chuyển sang truyền raw args

### 17.2 Lỗi quyền ghi cache build trong repo

Đã sửa:

- build/publish dùng `StationAppBuildRoot` ở `%TEMP%`
- tránh lỗi lock/quyền ghi ở `._repo_build` và `._wpf_build_isolated`

### 17.3 Lỗi mở màn `Cập nhật ứng dụng`

Đã sửa:

- `AppUpdateView.xaml` không còn dùng `StaticResource GridAreaShadow` bị thiếu
- thay bằng `DropShadowEffect` inline

### 17.4 Lỗi app mới đòi cài .NET sau khi update

Nguyên nhân đã xác định:

- package zip trên shared folder được build theo kiểu `framework-dependent`
- manifest có thể đã để `dbMigratorRequired = false`, nhưng điều này không ảnh hưởng tới việc app mới có tự mang runtime hay không
- máy trạm copy xong gói đó vẫn đòi `.NET Desktop Runtime 8`

Đã sửa:

- `scripts\publish-ui-release.ps1` không còn publish với `--no-build`
- `dotnet publish` sẽ rebuild đúng theo mode `self-contained`
- package chuẩn để test lại luồng update là từ `1.0.4` trở đi

## 18. Test cases cần có

- có bản mới, update thành công
- app mở lại đúng bản mới
- `DbMigrator` chạy thành công
- hash package sai thì bị chặn
- package lỗi thì rollback
- không ghi đè mất `appsettings.json`
- máy trạm chỉ có quyền đọc shared folder vẫn update được
- màn `Cập nhật ứng dụng` mở được bình thường

## 19. Tiêu chí nghiệm thu

Hoàn thành khi đạt:

- không cần copy tay thư mục build vào máy trạm
- user cập nhật được ngay trong app
- app mở lại sau update
- DB migrator chạy trong updater flow
- rollback được nếu lỗi
- không làm mất config local
- tên output build/package có cả version

## 20. Các thông tin đã chốt

1. Shared folder:
   - `\\10.0.0.3\17. data dung chung\Chienbn\Phan_mem_can`
2. Thư mục cài app chuẩn trên máy trạm:
   - `C:\Users\hungnt232\Desktop\PMCan`
3. `appsettings.json`:
   - giữ nguyên dữ liệu cũ
   - chỉ bổ sung key mới nếu cần
4. Role được cập nhật:
   - `Admin`
   - `Operator`
5. Không auto check khi startup:
   - chỉ cập nhật khi user bấm nút

## 21. Kết luận

Hướng `shared folder + updater riêng + DbMigrator trong flow update` là hướng đã chốt và phù hợp nhất với dự án hiện tại.

Nó đảm bảo:

- ít rủi ro hơn copy tay
- không để app tự ghi đè chính nó
- tương thích với workflow DB đã chuẩn hóa
- đủ an toàn để vận hành trong môi trường trạm cân nội bộ
