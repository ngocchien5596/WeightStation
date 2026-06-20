/*
Clone cac bang cau hinh / master data giua 2 database cung schema.

Cach dung:
1. Sua @SourceDb, @TargetDb cho dung ten DB.
2. Mac dinh script se UPSERT (insert/update) vao DB dich, khong xoa du lieu du thua.
3. Neu muon DB dich giong het DB nguon cho cac bang nay, set @DeleteMissingRows = 1.
4. Chay script trong SQL Server voi quyen doc DB nguon va ghi DB dich.

Bang duoc clone:
- users
- app_config
- print_template_profiles
- station_feature_flags
- station_operation_settings
- stations
- user_station_assignments
*/

SET NOCOUNT ON;
SET XACT_ABORT ON;

DECLARE @SourceDb sysname = N'StationAppLocal_Source';
DECLARE @TargetDb sysname = N'StationAppLocal';
DECLARE @DeleteMissingRows bit = 0;

IF DB_ID(@SourceDb) IS NULL
    THROW 51001, N'Database nguon khong ton tai.', 1;

IF DB_ID(@TargetDb) IS NULL
    THROW 51002, N'Database dich khong ton tai.', 1;

IF @SourceDb = @TargetDb
    THROW 51003, N'Database nguon va dich khong duoc trung nhau.', 1;

DECLARE @Sql nvarchar(max) = N'';

SET @Sql = N'
BEGIN TRANSACTION;

BEGIN TRY
    MERGE ' + QUOTENAME(@TargetDb) + N'.dbo.stations AS target
    USING (
        SELECT
            Id,
            StationCode,
            StationName,
            IsActive,
            SortOrder,
            CreatedAt,
            CreatedBy,
            UpdatedAt,
            UpdatedBy
        FROM ' + QUOTENAME(@SourceDb) + N'.dbo.stations
    ) AS source
    ON target.Id = source.Id
    WHEN MATCHED THEN UPDATE SET
        target.StationCode = source.StationCode,
        target.StationName = source.StationName,
        target.IsActive = source.IsActive,
        target.SortOrder = source.SortOrder,
        target.CreatedAt = source.CreatedAt,
        target.CreatedBy = source.CreatedBy,
        target.UpdatedAt = source.UpdatedAt,
        target.UpdatedBy = source.UpdatedBy
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (Id, StationCode, StationName, IsActive, SortOrder, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
        VALUES (source.Id, source.StationCode, source.StationName, source.IsActive, source.SortOrder, source.CreatedAt, source.CreatedBy, source.UpdatedAt, source.UpdatedBy)
' + CASE WHEN @DeleteMissingRows = 1 THEN N'    WHEN NOT MATCHED BY SOURCE THEN DELETE' ELSE N'' END + N';

    MERGE ' + QUOTENAME(@TargetDb) + N'.dbo.users AS target
    USING (
        SELECT
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
        FROM ' + QUOTENAME(@SourceDb) + N'.dbo.users
    ) AS source
    ON target.Id = source.Id
    WHEN MATCHED THEN UPDATE SET
        target.Username = source.Username,
        target.DisplayName = source.DisplayName,
        target.RoleCode = source.RoleCode,
        target.IsActive = source.IsActive,
        target.CreatedAt = source.CreatedAt,
        target.UpdatedAt = source.UpdatedAt,
        target.PasswordHash = source.PasswordHash,
        target.LastLoginAt = source.LastLoginAt,
        target.CreatedBy = source.CreatedBy,
        target.UpdatedBy = source.UpdatedBy
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (Id, Username, DisplayName, RoleCode, IsActive, CreatedAt, UpdatedAt, PasswordHash, LastLoginAt, CreatedBy, UpdatedBy)
        VALUES (source.Id, source.Username, source.DisplayName, source.RoleCode, source.IsActive, source.CreatedAt, source.UpdatedAt, source.PasswordHash, source.LastLoginAt, source.CreatedBy, source.UpdatedBy)
' + CASE WHEN @DeleteMissingRows = 1 THEN N'    WHEN NOT MATCHED BY SOURCE THEN DELETE' ELSE N'' END + N';

    MERGE ' + QUOTENAME(@TargetDb) + N'.dbo.app_config AS target
    USING (
        SELECT
            ConfigKey,
            ConfigValue,
            UpdatedAt,
            CreatedAt,
            CreatedBy,
            UpdatedBy
        FROM ' + QUOTENAME(@SourceDb) + N'.dbo.app_config
    ) AS source
    ON target.ConfigKey = source.ConfigKey
    WHEN MATCHED THEN UPDATE SET
        target.ConfigValue = source.ConfigValue,
        target.UpdatedAt = source.UpdatedAt,
        target.CreatedAt = source.CreatedAt,
        target.CreatedBy = source.CreatedBy,
        target.UpdatedBy = source.UpdatedBy
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (ConfigKey, ConfigValue, UpdatedAt, CreatedAt, CreatedBy, UpdatedBy)
        VALUES (source.ConfigKey, source.ConfigValue, source.UpdatedAt, source.CreatedAt, source.CreatedBy, source.UpdatedBy)
' + CASE WHEN @DeleteMissingRows = 1 THEN N'    WHEN NOT MATCHED BY SOURCE THEN DELETE' ELSE N'' END + N';

    MERGE ' + QUOTENAME(@TargetDb) + N'.dbo.print_template_profiles AS target
    USING (
        SELECT
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
        FROM ' + QUOTENAME(@SourceDb) + N'.dbo.print_template_profiles
    ) AS source
    ON target.Id = source.Id
    WHEN MATCHED THEN UPDATE SET
        target.TemplateKind = source.TemplateKind,
        target.ProfileKey = source.ProfileKey,
        target.DisplayName = source.DisplayName,
        target.IsDefault = source.IsDefault,
        target.OffsetXmm = source.OffsetXmm,
        target.OffsetYmm = source.OffsetYmm,
        target.TemplateVersion = source.TemplateVersion,
        target.LayoutJson = source.LayoutJson,
        target.CreatedAt = source.CreatedAt,
        target.CreatedBy = source.CreatedBy,
        target.UpdatedAt = source.UpdatedAt,
        target.UpdatedBy = source.UpdatedBy
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (Id, TemplateKind, ProfileKey, DisplayName, IsDefault, OffsetXmm, OffsetYmm, TemplateVersion, LayoutJson, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
        VALUES (source.Id, source.TemplateKind, source.ProfileKey, source.DisplayName, source.IsDefault, source.OffsetXmm, source.OffsetYmm, source.TemplateVersion, source.LayoutJson, source.CreatedAt, source.CreatedBy, source.UpdatedAt, source.UpdatedBy)
' + CASE WHEN @DeleteMissingRows = 1 THEN N'    WHEN NOT MATCHED BY SOURCE THEN DELETE' ELSE N'' END + N';

    MERGE ' + QUOTENAME(@TargetDb) + N'.dbo.station_feature_flags AS target
    USING (
        SELECT
            Id,
            StationCode,
            FeatureKey,
            FeatureValue,
            CreatedAt,
            CreatedBy,
            UpdatedAt,
            UpdatedBy
        FROM ' + QUOTENAME(@SourceDb) + N'.dbo.station_feature_flags
    ) AS source
    ON target.Id = source.Id
    WHEN MATCHED THEN UPDATE SET
        target.StationCode = source.StationCode,
        target.FeatureKey = source.FeatureKey,
        target.FeatureValue = source.FeatureValue,
        target.CreatedAt = source.CreatedAt,
        target.CreatedBy = source.CreatedBy,
        target.UpdatedAt = source.UpdatedAt,
        target.UpdatedBy = source.UpdatedBy
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (Id, StationCode, FeatureKey, FeatureValue, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
        VALUES (source.Id, source.StationCode, source.FeatureKey, source.FeatureValue, source.CreatedAt, source.CreatedBy, source.UpdatedAt, source.UpdatedBy)
' + CASE WHEN @DeleteMissingRows = 1 THEN N'    WHEN NOT MATCHED BY SOURCE THEN DELETE' ELSE N'' END + N';

    MERGE ' + QUOTENAME(@TargetDb) + N'.dbo.station_operation_settings AS target
    USING (
        SELECT
            Id,
            StationCode,
            SettingKey,
            SettingValue,
            CreatedAt,
            CreatedBy,
            UpdatedAt,
            UpdatedBy
        FROM ' + QUOTENAME(@SourceDb) + N'.dbo.station_operation_settings
    ) AS source
    ON target.Id = source.Id
    WHEN MATCHED THEN UPDATE SET
        target.StationCode = source.StationCode,
        target.SettingKey = source.SettingKey,
        target.SettingValue = source.SettingValue,
        target.CreatedAt = source.CreatedAt,
        target.CreatedBy = source.CreatedBy,
        target.UpdatedAt = source.UpdatedAt,
        target.UpdatedBy = source.UpdatedBy
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (Id, StationCode, SettingKey, SettingValue, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
        VALUES (source.Id, source.StationCode, source.SettingKey, source.SettingValue, source.CreatedAt, source.CreatedBy, source.UpdatedAt, source.UpdatedBy)
' + CASE WHEN @DeleteMissingRows = 1 THEN N'    WHEN NOT MATCHED BY SOURCE THEN DELETE' ELSE N'' END + N';

    MERGE ' + QUOTENAME(@TargetDb) + N'.dbo.user_station_assignments AS target
    USING (
        SELECT
            Id,
            UserId,
            StationCode,
            IsDefault,
            IsActive,
            CreatedAt,
            CreatedBy,
            UpdatedAt,
            UpdatedBy
        FROM ' + QUOTENAME(@SourceDb) + N'.dbo.user_station_assignments
    ) AS source
    ON target.Id = source.Id
    WHEN MATCHED THEN UPDATE SET
        target.UserId = source.UserId,
        target.StationCode = source.StationCode,
        target.IsDefault = source.IsDefault,
        target.IsActive = source.IsActive,
        target.CreatedAt = source.CreatedAt,
        target.CreatedBy = source.CreatedBy,
        target.UpdatedAt = source.UpdatedAt,
        target.UpdatedBy = source.UpdatedBy
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (Id, UserId, StationCode, IsDefault, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy)
        VALUES (source.Id, source.UserId, source.StationCode, source.IsDefault, source.IsActive, source.CreatedAt, source.CreatedBy, source.UpdatedAt, source.UpdatedBy)
' + CASE WHEN @DeleteMissingRows = 1 THEN N'    WHEN NOT MATCHED BY SOURCE THEN DELETE' ELSE N'' END + N';

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRANSACTION;

    THROW;
END CATCH;
';

PRINT N'Cloning data from [' + @SourceDb + N'] to [' + @TargetDb + N']';
EXEC sys.sp_executesql @Sql;
