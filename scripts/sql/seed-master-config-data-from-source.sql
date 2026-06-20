/*
Chen them du lieu cau hinh / master data tu DB nguon vao DB local moi tao.

Cach dung:
1. Sua @SourceDb va @TargetDb.
2. Mac dinh script CHI INSERT cac ban ghi con thieu, khong UPDATE, khong DELETE.
3. Phu hop khi DB dich da duoc tao schema san va can nap them master/config data.

Bang duoc seed:
- stations
- users
- app_config
- print_template_profiles
- station_feature_flags
- station_operation_settings
- user_station_assignments
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @SourceDb sysname = N'StationAppLocal_Source';
DECLARE @TargetDb sysname = N'StationAppLocal';

IF DB_ID(@SourceDb) IS NULL
    THROW 51001, N'Database nguon khong ton tai.', 1;

IF DB_ID(@TargetDb) IS NULL
    THROW 51002, N'Database dich khong ton tai.', 1;

IF @SourceDb = @TargetDb
    THROW 51003, N'Database nguon va dich khong duoc trung nhau.', 1;

DECLARE @Sql nvarchar(max) = N'
BEGIN TRANSACTION;

BEGIN TRY
    INSERT INTO ' + QUOTENAME(@TargetDb) + N'.dbo.stations
    (
        Id,
        StationCode,
        StationName,
        IsActive,
        SortOrder,
        CreatedAt,
        CreatedBy,
        UpdatedAt,
        UpdatedBy
    )
    SELECT
        s.Id,
        s.StationCode,
        s.StationName,
        s.IsActive,
        s.SortOrder,
        s.CreatedAt,
        s.CreatedBy,
        s.UpdatedAt,
        s.UpdatedBy
    FROM ' + QUOTENAME(@SourceDb) + N'.dbo.stations s
    WHERE NOT EXISTS (
        SELECT 1
        FROM ' + QUOTENAME(@TargetDb) + N'.dbo.stations t
        WHERE t.Id = s.Id
           OR t.StationCode = s.StationCode
    );

    INSERT INTO ' + QUOTENAME(@TargetDb) + N'.dbo.users
    (
        Id,
        Username,
        DisplayName,
        RoleCode,
        IsActive,
        CreatedAt,
        UpdatedAt,
        PasswordHash,
        LastLoginAt,
        CreatedBy,
        UpdatedBy
    )
    SELECT
        s.Id,
        s.Username,
        s.DisplayName,
        s.RoleCode,
        s.IsActive,
        s.CreatedAt,
        s.UpdatedAt,
        s.PasswordHash,
        s.LastLoginAt,
        s.CreatedBy,
        s.UpdatedBy
    FROM ' + QUOTENAME(@SourceDb) + N'.dbo.users s
    WHERE NOT EXISTS (
        SELECT 1
        FROM ' + QUOTENAME(@TargetDb) + N'.dbo.users t
        WHERE t.Id = s.Id
           OR t.Username = s.Username
    );

    INSERT INTO ' + QUOTENAME(@TargetDb) + N'.dbo.app_config
    (
        ConfigKey,
        ConfigValue,
        UpdatedAt,
        CreatedAt,
        CreatedBy,
        UpdatedBy
    )
    SELECT
        s.ConfigKey,
        s.ConfigValue,
        s.UpdatedAt,
        s.CreatedAt,
        s.CreatedBy,
        s.UpdatedBy
    FROM ' + QUOTENAME(@SourceDb) + N'.dbo.app_config s
    WHERE NOT EXISTS (
        SELECT 1
        FROM ' + QUOTENAME(@TargetDb) + N'.dbo.app_config t
        WHERE t.ConfigKey = s.ConfigKey
    );

    INSERT INTO ' + QUOTENAME(@TargetDb) + N'.dbo.print_template_profiles
    (
        Id,
        TemplateKind,
        ProfileKey,
        DisplayName,
        IsDefault,
        OffsetXmm,
        OffsetYmm,
        TemplateVersion,
        LayoutJson,
        CreatedAt,
        CreatedBy,
        UpdatedAt,
        UpdatedBy
    )
    SELECT
        s.Id,
        s.TemplateKind,
        s.ProfileKey,
        s.DisplayName,
        s.IsDefault,
        s.OffsetXmm,
        s.OffsetYmm,
        s.TemplateVersion,
        s.LayoutJson,
        s.CreatedAt,
        s.CreatedBy,
        s.UpdatedAt,
        s.UpdatedBy
    FROM ' + QUOTENAME(@SourceDb) + N'.dbo.print_template_profiles s
    WHERE NOT EXISTS (
        SELECT 1
        FROM ' + QUOTENAME(@TargetDb) + N'.dbo.print_template_profiles t
        WHERE t.Id = s.Id
           OR (t.TemplateKind = s.TemplateKind AND t.ProfileKey = s.ProfileKey)
    );

    INSERT INTO ' + QUOTENAME(@TargetDb) + N'.dbo.station_feature_flags
    (
        Id,
        StationCode,
        FeatureKey,
        FeatureValue,
        CreatedAt,
        CreatedBy,
        UpdatedAt,
        UpdatedBy
    )
    SELECT
        s.Id,
        s.StationCode,
        s.FeatureKey,
        s.FeatureValue,
        s.CreatedAt,
        s.CreatedBy,
        s.UpdatedAt,
        s.UpdatedBy
    FROM ' + QUOTENAME(@SourceDb) + N'.dbo.station_feature_flags s
    WHERE NOT EXISTS (
        SELECT 1
        FROM ' + QUOTENAME(@TargetDb) + N'.dbo.station_feature_flags t
        WHERE t.Id = s.Id
           OR (t.StationCode = s.StationCode AND t.FeatureKey = s.FeatureKey)
    );

    INSERT INTO ' + QUOTENAME(@TargetDb) + N'.dbo.station_operation_settings
    (
        Id,
        StationCode,
        SettingKey,
        SettingValue,
        CreatedAt,
        CreatedBy,
        UpdatedAt,
        UpdatedBy
    )
    SELECT
        s.Id,
        s.StationCode,
        s.SettingKey,
        s.SettingValue,
        s.CreatedAt,
        s.CreatedBy,
        s.UpdatedAt,
        s.UpdatedBy
    FROM ' + QUOTENAME(@SourceDb) + N'.dbo.station_operation_settings s
    WHERE NOT EXISTS (
        SELECT 1
        FROM ' + QUOTENAME(@TargetDb) + N'.dbo.station_operation_settings t
        WHERE t.Id = s.Id
           OR (t.StationCode = s.StationCode AND t.SettingKey = s.SettingKey)
    );

    INSERT INTO ' + QUOTENAME(@TargetDb) + N'.dbo.user_station_assignments
    (
        Id,
        UserId,
        StationCode,
        IsDefault,
        IsActive,
        CreatedAt,
        CreatedBy,
        UpdatedAt,
        UpdatedBy
    )
    SELECT
        s.Id,
        s.UserId,
        s.StationCode,
        s.IsDefault,
        s.IsActive,
        s.CreatedAt,
        s.CreatedBy,
        s.UpdatedAt,
        s.UpdatedBy
    FROM ' + QUOTENAME(@SourceDb) + N'.dbo.user_station_assignments s
    WHERE EXISTS (
        SELECT 1
        FROM ' + QUOTENAME(@TargetDb) + N'.dbo.users tu
        WHERE tu.Id = s.UserId
    )
      AND EXISTS (
        SELECT 1
        FROM ' + QUOTENAME(@TargetDb) + N'.dbo.stations ts
        WHERE ts.StationCode = s.StationCode
    )
      AND NOT EXISTS (
        SELECT 1
        FROM ' + QUOTENAME(@TargetDb) + N'.dbo.user_station_assignments t
        WHERE t.Id = s.Id
           OR (t.UserId = s.UserId AND t.StationCode = s.StationCode)
    );

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
';

PRINT N'Seeding master/config data from [' + @SourceDb + N'] to [' + @TargetDb + N']';
EXEC sys.sp_executesql @Sql;
