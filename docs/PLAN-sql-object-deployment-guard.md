# Plan: Chặn máy trạm cũ ghi đè Stored Procedure mới

## 1. Mục tiêu

Bài toán cần giải quyết:

> Không cho app/máy trạm cũ ghi đè stored procedure mới trên SQL Server.

Nguyên tắc chốt:

- App runtime không được deploy SQL object.
- Runtime DB user không có quyền DDL.
- Stored procedure/function chỉ được deploy bằng `StationApp.DbMigrator` với deploy account riêng.

Không xây registry/hash/version cho từng stored procedure trong giai đoạn này. Cách đó phức tạp hơn nhu cầu thực tế và không bảo vệ được binary cũ đã phát hành.

## 2. Root Cause

Hiện tại SQL script được embed vào `StationApp.Infrastructure.dll`.

Nếu một máy trạm vẫn chạy binary cũ và code cũ còn gọi:

```csharp
InitializeAsync(..., deploySqlObjects: true)
```

thì nó có thể chạy:

```sql
ALTER PROCEDURE ...
```

và ghi đè stored procedure mới trên SQL Server bằng nội dung cũ trong DLL.

Do đó biện pháp chắc chắn nhất không phải là version guard trong app mới, mà là:

> DB user dùng bởi app runtime không có quyền `ALTER PROCEDURE`.

Khi máy cũ cố ghi đè SP, SQL Server trả `permission denied`, SP mới vẫn nguyên vẹn.

## 3. Kiến trúc quyền sau khi sửa

```text
                 SQL SERVER
                     |
          +----------+-----------+
          |                      |
 stationapp_runtime       stationapp_deploy
          |                      |
 SELECT/INSERT/UPDATE      ALTER/CREATE/MIGRATION
 DELETE/EXECUTE            deploy stored procedure
          |                      |
   StationApp.UI        StationApp.DbMigrator
          |
          X
   Không được ALTER SP
```

## 4. Phạm vi thực hiện

### Bắt buộc làm

| Việc | Mục đích |
|---|---|
| Thu hồi quyền DDL khỏi runtime DB user | Chặn cả máy trạm cũ còn chạy binary cũ |
| UI không deploy SQL object nữa | App mới không vô tình `ALTER PROCEDURE` |
| DbMigrator là nơi duy nhất deploy SP | Có một đường deploy rõ ràng, kiểm soát được |
| Giữ log SHA256 khi deploy SQL object | Truy vết khi cần |

### Không làm trong giai đoạn này

| Việc | Lý do bỏ |
|---|---|
| Bảng `sql_object_deployments` | Phức tạp, chưa cần |
| `ScriptVersion` từng SP | Dễ quên tăng version, tự tạo vấn đề mới |
| Hash mismatch/downgrade policy | Không bảo vệ được binary cũ đã phát hành |
| Diagnostics hiển thị SP version | Nice-to-have, chưa cần |
| Hệ thống tự phát hiện SQL script đổi khi publish | Chỉ cần quy ước release rõ ràng |

## 5. Thứ tự triển khai

### Bước 1. Thu hồi quyền DDL khỏi runtime DB user

**Mô tả:** Runtime account chỉ được quyền nghiệp vụ và `EXECUTE`, không có quyền sửa cấu trúc DB.

Runtime user ví dụ:

```text
stationapp_runtime
```

Quyền được phép:

- `SELECT`
- `INSERT`
- `UPDATE`
- `DELETE`
- `EXECUTE`

Quyền không được phép:

- `ALTER`
- `CREATE PROCEDURE`
- `DROP`
- `CONTROL`
- `db_owner`
- `db_ddladmin`

**Acceptance criteria:**

- [ ] App runtime vẫn chạy nghiệp vụ bình thường.
- [ ] Runtime user không thể chạy `ALTER PROCEDURE`.
- [ ] Máy trạm cũ nếu cố deploy procedure sẽ bị SQL Server từ chối.

**Verification:**

Chạy bằng runtime user:

```sql
ALTER PROCEDURE dbo.sp_UpdateCutOrderErpExtras
AS
BEGIN
    SET NOCOUNT ON;
END
```

Kết quả mong muốn:

```text
permission denied
```

**Files likely touched:**

- `scripts/sql/GrantRuntimePermissions_StationAppLocal.sql`
- `scripts/sql/GrantDeployPermissions_StationAppLocal.sql`
- tài liệu triển khai quyền DB

**Priority:** P0

### Bước 2. Tắt deploy SQL object trong StationApp.UI

**Mô tả:** Sửa startup của `StationApp.UI` để không gọi deploy stored procedure/function.

Hiện tại cần rà trong:

```text
src/StationApp.UI/App.xaml.cs
```

đoạn:

```csharp
await StationDatabaseInitializer.InitializeAsync(
    db,
    loggerFactory,
    CancellationToken.None,
    deploySqlObjects: true);
```

Đổi thành:

```csharp
await StationDatabaseInitializer.InitializeAsync(
    db,
    loggerFactory,
    CancellationToken.None,
    deploySqlObjects: false);
```

hoặc bỏ hẳn tham số vì mặc định đang là `false`.

**Acceptance criteria:**

- [ ] Mở app không deploy SQL object.
- [ ] App vẫn bootstrap/migrate schema bảng như hiện tại.
- [ ] Log khi mở app không còn dòng `Deployed SQL object script ...`.

**Verification:**

- [ ] Build UI pass.
- [ ] Mở app bằng runtime user không lỗi vì thiếu quyền `ALTER PROCEDURE`.
- [ ] Procedure trên DB không đổi khi chỉ mở app.

**Files likely touched:**

- `src/StationApp.UI/App.xaml.cs`

**Priority:** P0

### Bước 3. Chuẩn hóa DbMigrator là đường duy nhất deploy SP

**Mô tả:** Stored procedure/function chỉ được deploy qua:

```text
StationApp.DbMigrator
```

DbMigrator dùng deploy account riêng:

```text
stationapp_deploy
```

Account này có quyền:

- migrate schema,
- tạo/sửa stored procedure,
- deploy SQL object.

**Release flow chuẩn:**

```text
Sửa code / SQL
      |
      v
Build release
      |
      v
Có thay đổi DB/SP?
      |
      +-- Không --> Publish app
      |
      +-- Có --> Chạy DbMigrator bằng stationapp_deploy
                    |
                    v
                 Publish app
```

**Acceptance criteria:**

- [ ] `StationApp.DbMigrator` vẫn deploy SQL object bằng `deploySqlObjects: true`.
- [ ] Release có thay đổi DB/SP phải chạy DbMigrator trước khi publish app.
- [ ] App runtime không cần quyền DDL.

**Verification:**

Chạy DbMigrator bằng deploy account:

```powershell
dotnet run --project src\StationApp.DbMigrator\StationApp.DbMigrator.csproj -- --connection "Server=.;Database=StationAppLocal;User Id=stationapp_deploy;Password=MatKhau;Encrypt=False;TrustServerCertificate=True;"
```

Kiểm tra procedure:

```sql
SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_UpdateCutOrderErpExtras', N'P'));
```

Hoặc chạy:

```text
scripts/sql/check-sp-update-cut-order-erp-extras.sql
```

**Files likely touched:**

- `src/StationApp.DbMigrator/Program.cs` nếu cần bổ sung option/log
- `scripts/update-local-db-schema.ps1`
- `docs/PLAN-db-permissions-and-release-workflow.md`

**Priority:** P0

## 6. Quyền SQL đề xuất

### 6.1. Runtime user

Ví dụ:

```sql
CREATE LOGIN stationapp_runtime WITH PASSWORD = 'RuntimePassword';
GO

USE StationAppLocal;
GO

CREATE USER stationapp_runtime FOR LOGIN stationapp_runtime;
GO
```

Cấp quyền nghiệp vụ:

```sql
GRANT SELECT, INSERT, UPDATE, DELETE TO stationapp_runtime;
GRANT EXECUTE TO stationapp_runtime;
```

Đảm bảo không thuộc các role:

```sql
EXEC sp_droprolemember N'db_owner', N'stationapp_runtime';
EXEC sp_droprolemember N'db_ddladmin', N'stationapp_runtime';
```

Không cấp:

```sql
ALTER
CONTROL
CREATE PROCEDURE
DROP
```

### 6.2. Deploy user

Ví dụ:

```sql
CREATE LOGIN stationapp_deploy WITH PASSWORD = 'DeployPassword';
GO

USE StationAppLocal;
GO

CREATE USER stationapp_deploy FOR LOGIN stationapp_deploy;
GO
```

Giai đoạn đầu có thể cấp:

```sql
ALTER ROLE db_owner ADD MEMBER stationapp_deploy;
```

Sau này nếu muốn siết quyền hơn, giảm dần từ `db_owner` sang quyền DDL cần thiết.

## 7. Checklist release khi có thay đổi Stored Procedure

Trước khi publish app:

- [ ] Đã sửa file SQL trong `scripts/sql/*.sql`.
- [ ] Đã build pass.
- [ ] Đã chạy DbMigrator bằng deploy account trên DB thật.
- [ ] Đã chạy script kiểm tra procedure.
- [ ] Đã xác nhận app runtime dùng runtime account không có DDL.
- [ ] Sau đó mới publish app.

Không để máy trạm runtime tự chạy deploy SQL object.

## 8. Test chống tái diễn

Giữ các test hiện có:

- `SqlObjectDeploymentTests`
- test script `sp_UpdateCutOrderErpExtras` có `@Description`, `@PrinterName`, update `Notes`, update `PackagePrinterName`.
- test split batch bỏ qua `USE [xxx]`.

Không cần thêm test registry/version.

## 9. Kết luận

Plan tối giản còn 3 việc bắt buộc:

1. Thu hồi DDL runtime account.
2. UI không deploy SQL object.
3. DbMigrator là đường duy nhất deploy SP.

Đây là cách giải quyết đúng gốc vì bảo vệ được cả trường hợp máy trạm cũ vẫn còn chạy binary cũ có DLL embed stored procedure cũ.
