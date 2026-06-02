# Kế hoạch quyền DB và workflow release an toàn cho Station App

## 1. Mục tiêu

Chuẩn hóa cách cấp quyền database và workflow phát hành để:

- app runtime trên máy trạm không cần account quá mạnh
- không bị lỗi startup do thiếu quyền DDL
- function/procedure SQL được cập nhật đúng quy trình
- build/release/update app hoạt động ổn định

## 2. Nguyên tắc bắt buộc

Tách 2 loại account:

- `runtime account`
  - dùng khi operator mở app hằng ngày
  - chỉ có quyền dữ liệu nghiệp vụ
  - không có quyền DDL mạnh
- `deploy account`
  - dùng khi update schema / function / procedure
  - có quyền DDL cần thiết

Không dùng account mạnh kiểu:

- `sa`
- `db_owner`
- account quyền rộng cố định cho mọi máy trạm ở runtime

## 3. Quyền DB đề xuất

### 3.1 Runtime account

Script mẫu:

- [GrantRuntimePermissions_StationAppLocal.sql](g:/Source-code/pmcan_C#/scripts/sql/GrantRuntimePermissions_StationAppLocal.sql)

Quyền:

- `SELECT`
- `INSERT`
- `UPDATE`
- `DELETE`
- `EXECUTE`

Không cấp:

- `CREATE FUNCTION`
- `CREATE PROCEDURE`
- `ALTER`
- `DROP`

### 3.2 Deploy account

Script mẫu:

- [GrantDeployPermissions_StationAppLocal.sql](g:/Source-code/pmcan_C#/scripts/sql/GrantDeployPermissions_StationAppLocal.sql)

Quyền:

- toàn bộ quyền của runtime
- thêm quyền DDL để chạy migrator/deploy SQL object

## 4. Luồng chuẩn hiện tại

### 4.1 Runtime app

App runtime:

- không tự deploy SQL object tại startup
- không còn là nơi cập nhật `fn/sp` custom
- nếu thiếu quyền DDL thì không được làm app chết

### 4.2 Deploy schema và SQL object

Việc cập nhật:

- schema
- `fn_GetCutOrderNetWeight`
- `sp_GetCutOrderNetWeight`
- các SQL object custom khác

phải đi qua:

- `StationApp.DbMigrator`
- hoặc workflow phát hành chuẩn có gọi migrator

## 5. Script build/release chuẩn

### 5.1 Build thường

```powershell
powershell -File .\scripts\build-ui.ps1 -Configuration Debug -ConfigPath "src/StationApp.UI/appsettings.json"
```

### 5.2 Build thường nhưng không đụng DB

```powershell
powershell -File .\scripts\build-ui.ps1 -Configuration Debug -SkipDatabaseSchemaUpdate
```

### 5.3 Publish local ra thư mục `Builds`

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-ui-release.ps1 `
  -Configuration Release `
  -RuntimeIdentifier win-x64 `
  -ConfigPath "src/StationApp.UI/appsettings.json" `
  -AppVersion "1.0.1" `
  -ReleaseNotes "Fix bug can xuat khau" `
  -SkipDatabaseSchemaUpdate
```

### 5.4 Publish lên shared folder

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\publish-shared-folder-release.ps1 `
  -Configuration Release `
  -RuntimeIdentifier win-x64 `
  -ConfigPath "src/StationApp.UI/appsettings.json" `
  -SharedReleaseRoot "\\10.0.0.3\17. data dung chung\Chienbn\Phan_mem_can" `
  -AppVersion "1.0.1" `
  -ReleaseNotes "Fix bug can xuat khau"
```

Nếu bản phát hành có thay đổi DB hoặc SQL object, thêm:

```powershell
  -DbMigratorRequired
```

## 6. Rule quan trọng đã chốt

### 6.1 Version

- chỉ dùng semantic version `x.y.z`
- version là tham số bắt buộc khi publish
- output phải chứa cả version trong tên

### 6.2 Build root

Build/publish dùng `StationAppBuildRoot` trong `%TEMP%` để tránh:

- lỗi lock file
- lỗi quyền ghi ở `._repo_build`
- lỗi quyền ghi ở `._wpf_build_isolated`

### 6.3 Shared-folder release script

`publish-shared-folder-release.ps1` đã được sửa để:

- truyền `switch` đúng cách
- không lỗi `SelfContained` khi gọi `publish-ui-release.ps1`
- mặc định phát hành với `dbMigratorRequired = false`
- chỉ bật chạy migrator ở máy trạm khi người phát hành chủ động truyền `-DbMigratorRequired`

## 7. Điều cần tuân thủ khi phát hành

- app runtime không phải nơi cập nhật function/procedure
- nếu sửa SQL object quan trọng thì phải đi qua migrator/release workflow
- máy trạm update app sẽ chạy `DbMigrator` trong updater flow
- không copy tay package mà bỏ qua migrator nếu bản đó có thay đổi DB
- không bật `-DbMigratorRequired` cho các bản chỉ sửa app/UI thông thường

## 7.1 Ma trận sử dụng `-DbMigratorRequired`

### Case 1 - Bản phát hành không đổi DB

- không truyền `-DbMigratorRequired`
- máy trạm chỉ update app
- đây là flow mặc định

### Case 2 - Bản phát hành có đổi DB, nhưng DB đã được deploy trước

- ưu tiên không truyền `-DbMigratorRequired`
- vì DB thật đã được cập nhật bởi `deploy account`
- máy trạm chỉ cần update binary

Đây là cách an toàn hơn trong môi trường sản xuất.

### Case 3 - Bản phát hành có đổi DB và muốn máy trạm tự chạy migrator

- truyền `-DbMigratorRequired`
- chỉ dùng khi đã xác nhận account DB trên máy trạm đủ quyền DDL

Nếu không đủ quyền:

- updater sẽ copy file xong
- `DbMigrator` fail
- updater rollback

Nói cách khác, `-DbMigratorRequired` không đảm bảo migrator sẽ chạy được; nó chỉ yêu cầu updater cố chạy migrator.

## 8. Checklist release

Trước khi phát hành:

- đã chốt `AppVersion`
- đã build pass
- đã verify `publish-ui-release.ps1`
- nếu dùng shared folder:
  - đã verify `publish-shared-folder-release.ps1`
  - đã có `latest.json` mới
- đã chắc rằng package có đủ:
  - `Tools\Updater`
  - `Tools\DbMigrator`

## 8.1 Cách deploy DB trước bằng deploy account

Khuyến nghị dùng flow này cho release có đổi DB.

### Chuẩn bị quyền

- chạy [GrantDeployPermissions_StationAppLocal.sql](g:/Source-code/pmcan_C#/scripts/sql/GrantDeployPermissions_StationAppLocal.sql)
- DB user deploy phải có đủ quyền DDL

### Cách chạy

Qua file config riêng:

```powershell
dotnet run --project src\StationApp.DbMigrator\StationApp.DbMigrator.csproj -- --config appsettings.deploy.json
```

Hoặc truyền connection string trực tiếp:

```powershell
dotnet run --project src\StationApp.DbMigrator\StationApp.DbMigrator.csproj -- --connection "Server=.;Database=StationAppLocal;User Id=stationapp_deploy;Password=MatKhau;Encrypt=False;TrustServerCertificate=True;"
```

Sau khi deploy DB xong:

- phát hành app lên shared folder
- không bật `-DbMigratorRequired`

## 8.2 Cách setup máy trạm có quyền tự chạy migrator

Chỉ dùng khi thật sự muốn máy trạm tự chạy `DbMigrator`.

### Cách 1

- giữ `Trusted_Connection=True`
- cấp quyền deploy cho chính Windows account chạy app trên máy trạm

### Cách 2

- đổi `appsettings.json` trên máy trạm sang SQL login có quyền deploy

Ví dụ:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=StationAppLocal;User Id=stationapp_deploy;Password=MatKhau;Encrypt=False;TrustServerCertificate=True;"
  }
}
```

Sau đó mới dùng bản phát hành có `-DbMigratorRequired`.

### Khuyến nghị

- chỉ dùng cách này khi đã xác nhận rõ quyền DB
- flow an toàn hơn vẫn là: deploy DB trước, máy trạm chỉ update app

## 9. Kết luận

Workflow an toàn và chuẩn hiện tại là:

- runtime app dùng account hạn chế
- deploy/update schema dùng deploy account
- release đi qua script chuẩn
- update máy trạm đi qua updater + migrator

Như vậy sẽ giảm mạnh rủi ro:

- app chết startup vì thiếu quyền DDL
- SQL object trong source mới nhưng DB runtime vẫn cũ
- build/release bị lỗi do cache build trong repo
