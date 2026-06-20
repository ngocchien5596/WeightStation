SET NOCOUNT ON;

DECLARE @SourceDb sysname = N'StationAppLocal';
DECLARE @SourceStationCode nvarchar(50) = N'QN01';
DECLARE @TargetStationCode nvarchar(50) = N'QN02';

DECLARE @Statements TABLE
(
    SortOrder int NOT NULL,
    TableName sysname NOT NULL,
    InsertSql nvarchar(max) NOT NULL
);

DECLARE @Sql nvarchar(max);

SET @Sql = N'
SELECT
    10 AS SortOrder,
    N''users'' AS TableName,
    N''IF NOT EXISTS (SELECT 1 FROM dbo.users WHERE Username = N'''''' + REPLACE([Username], '''''', '''''''''''') + N'''''''') ''
    + N''INSERT INTO dbo.users (Id, Username, DisplayName, RoleCode, PasswordHash, IsActive, LastLoginAt, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) VALUES (''
    + N'''''''' + CONVERT(nvarchar(36), [Id]) + N'''''''', ''
    + N''N'''''' + REPLACE([Username], '''''', '''''''''''') + N'''''''', ''
    + N''N'''''' + REPLACE([DisplayName], '''''', '''''''''''') + N'''''''', ''
    + N''N'''''' + REPLACE([RoleCode], '''''', '''''''''''') + N'''''''', ''
    + CASE WHEN [PasswordHash] IS NULL THEN N''NULL'' ELSE N''N'''''' + REPLACE([PasswordHash], '''''', '''''''''''') + N'''''''''' END + N'', ''
    + CASE WHEN [IsActive] = 1 THEN N''1'' ELSE N''0'' END + N'', ''
    + CASE WHEN [LastLoginAt] IS NULL THEN N''NULL'' ELSE N''CAST(N'''''' + CONVERT(nvarchar(33), [LastLoginAt], 126) + N'''''' AS datetime2)'' END + N'', ''
    + N''CAST(N'''''' + CONVERT(nvarchar(33), [CreatedAt], 126) + N'''''' AS datetime2), ''
    + CASE WHEN [CreatedBy] IS NULL THEN N''NULL'' ELSE N''N'''''' + REPLACE([CreatedBy], '''''', '''''''''''') + N'''''''''' END + N'', ''
    + CASE WHEN [UpdatedAt] IS NULL THEN N''NULL'' ELSE N''CAST(N'''''' + CONVERT(nvarchar(33), [UpdatedAt], 126) + N'''''' AS datetime2)'' END + N'', ''
    + CASE WHEN [UpdatedBy] IS NULL THEN N''NULL'' ELSE N''N'''''' + REPLACE([UpdatedBy], '''''', '''''''''''') + N'''''''''' END + N'');''
FROM ' + QUOTENAME(@SourceDb) + N'.dbo.users
ORDER BY [Username];';

INSERT INTO @Statements
EXEC sys.sp_executesql @Sql;

SET @Sql = N'
SELECT
    20 AS SortOrder,
    N''app_config'' AS TableName,
    CASE
        WHEN [ConfigKey] IN (N''station_code'', N''default_station_code'', N''ticket_prefix'') THEN
            N''IF EXISTS (SELECT 1 FROM dbo.app_config WHERE ConfigKey = N'''''' + REPLACE([ConfigKey], '''''', '''''''''''') + N'''''''') ''
            + N''UPDATE dbo.app_config SET ConfigValue = ''
            + CASE
                WHEN [ConfigKey] IN (N''station_code'', N''default_station_code'') THEN N''N'''''' + REPLACE(@TargetStationCode, '''''', '''''''''''') + N''''''''
                WHEN [ConfigKey] = N''ticket_prefix'' THEN N''N'''''' + REPLACE(@TargetStationCode, '''''', '''''''''''') + N''''''''
                ELSE CASE WHEN [ConfigValue] IS NULL THEN N''NULL'' ELSE N''N'''''' + REPLACE([ConfigValue], '''''', '''''''''''') + N'''''''''' END
            END
            + N'', UpdatedAt = CAST(N'''''' + CONVERT(nvarchar(33), [UpdatedAt], 126) + N'''''' AS datetime2), UpdatedBy = ''
            + CASE WHEN [UpdatedBy] IS NULL THEN N''NULL'' ELSE N''N'''''' + REPLACE([UpdatedBy], '''''', '''''''''''') + N'''''''''' END
            + N'' WHERE ConfigKey = N'''''' + REPLACE([ConfigKey], '''''', '''''''''''') + N''''''''; ''
            + N''ELSE INSERT INTO dbo.app_config (ConfigKey, ConfigValue, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) VALUES (''
            + N''N'''''' + REPLACE([ConfigKey], '''''', '''''''''''') + N'''''''', ''
            + CASE
                WHEN [ConfigKey] IN (N''station_code'', N''default_station_code'') THEN N''N'''''' + REPLACE(@TargetStationCode, '''''', '''''''''''') + N''''''''
                WHEN [ConfigKey] = N''ticket_prefix'' THEN N''N'''''' + REPLACE(@TargetStationCode, '''''', '''''''''''') + N''''''''
                ELSE CASE WHEN [ConfigValue] IS NULL THEN N''NULL'' ELSE N''N'''''' + REPLACE([ConfigValue], '''''', '''''''''''') + N'''''''''' END
            END
            + N'', CAST(N'''''' + CONVERT(nvarchar(33), [CreatedAt], 126) + N'''''' AS datetime2), ''
            + N''N'''''' + REPLACE([CreatedBy], '''''', '''''''''''') + N'''''''', ''
            + N''CAST(N'''''' + CONVERT(nvarchar(33), [UpdatedAt], 126) + N'''''' AS datetime2), ''
            + CASE WHEN [UpdatedBy] IS NULL THEN N''NULL'' ELSE N''N'''''' + REPLACE([UpdatedBy], '''''', '''''''''''') + N'''''''''' END
            + N'');''
        ELSE
            N''IF NOT EXISTS (SELECT 1 FROM dbo.app_config WHERE ConfigKey = N'''''' + REPLACE([ConfigKey], '''''', '''''''''''') + N'''''''') ''
            + N''INSERT INTO dbo.app_config (ConfigKey, ConfigValue, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) VALUES (''
            + N''N'''''' + REPLACE([ConfigKey], '''''', '''''''''''') + N'''''''', ''
            + CASE WHEN [ConfigValue] IS NULL THEN N''NULL'' ELSE N''N'''''' + REPLACE([ConfigValue], '''''', '''''''''''') + N'''''''''' END + N'', ''
            + N''CAST(N'''''' + CONVERT(nvarchar(33), [CreatedAt], 126) + N'''''' AS datetime2), ''
            + N''N'''''' + REPLACE([CreatedBy], '''''', '''''''''''') + N'''''''', ''
            + N''CAST(N'''''' + CONVERT(nvarchar(33), [UpdatedAt], 126) + N'''''' AS datetime2), ''
            + CASE WHEN [UpdatedBy] IS NULL THEN N''NULL'' ELSE N''N'''''' + REPLACE([UpdatedBy], '''''', '''''''''''') + N'''''''''' END
            + N'');''
    END AS InsertSql
FROM ' + QUOTENAME(@SourceDb) + N'.dbo.app_config
ORDER BY [ConfigKey];';

INSERT INTO @Statements
EXEC sys.sp_executesql @Sql, N'@TargetStationCode nvarchar(50)', @TargetStationCode = @TargetStationCode;

SET @Sql = N'
SELECT
    30 AS SortOrder,
    N''print_template_profiles'' AS TableName,
    N''IF NOT EXISTS (SELECT 1 FROM dbo.print_template_profiles WHERE TemplateKind = N'''''' + REPLACE([TemplateKind], '''''', '''''''''''') + N'''''' AND ProfileKey = N'''''' + REPLACE([ProfileKey], '''''', '''''''''''') + N'''''''') ''
    + N''INSERT INTO dbo.print_template_profiles (Id, TemplateKind, ProfileKey, DisplayName, IsDefault, OffsetXmm, OffsetYmm, TemplateVersion, LayoutJson, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) VALUES (''
    + N'''''''' + CONVERT(nvarchar(36), [Id]) + N'''''''', ''
    + N''N'''''' + REPLACE([TemplateKind], '''''', '''''''''''') + N'''''''', ''
    + N''N'''''' + REPLACE([ProfileKey], '''''', '''''''''''') + N'''''''', ''
    + N''N'''''' + REPLACE([DisplayName], '''''', '''''''''''') + N'''''''', ''
    + CASE WHEN [IsDefault] = 1 THEN N''1'' ELSE N''0'' END + N'', ''
    + REPLACE(CONVERT(varchar(50), CAST([OffsetXmm] AS decimal(18,3))), '','',''.'') + N'', ''
    + REPLACE(CONVERT(varchar(50), CAST([OffsetYmm] AS decimal(18,3))), '','',''.'') + N'', ''
    + CONVERT(varchar(20), [TemplateVersion]) + N'', ''
    + N''N'''''' + REPLACE([LayoutJson], '''''', '''''''''''') + N'''''''', ''
    + N''CAST(N'''''' + CONVERT(nvarchar(33), [CreatedAt], 126) + N'''''' AS datetime2), ''
    + N''N'''''' + REPLACE([CreatedBy], '''''', '''''''''''') + N'''''''', ''
    + N''CAST(N'''''' + CONVERT(nvarchar(33), [UpdatedAt], 126) + N'''''' AS datetime2), ''
    + N''N'''''' + REPLACE([UpdatedBy], '''''', '''''''''''') + N'''''''''' + N'');''
FROM ' + QUOTENAME(@SourceDb) + N'.dbo.print_template_profiles
ORDER BY [TemplateKind], [ProfileKey];';

INSERT INTO @Statements
EXEC sys.sp_executesql @Sql;

SET @Sql = N'
SELECT
    40 AS SortOrder,
    N''stations'' AS TableName,
    N''IF NOT EXISTS (SELECT 1 FROM dbo.stations WHERE StationCode = N'''''' + REPLACE(@TargetStationCode, '''''', '''''''''''') + N'''''''') ''
    + N''INSERT INTO dbo.stations (Id, StationCode, StationName, IsActive, SortOrder, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) VALUES (''
    + N'''''''' + CONVERT(nvarchar(36), [Id]) + N'''''''', ''
    + N''N'''''' + REPLACE(@TargetStationCode, '''''', '''''''''''') + N'''''''', ''
    + N''N'''''' + REPLACE(
        CASE WHEN [StationCode] = @SourceStationCode THEN [StationName] ELSE [StationName] END,
        '''''',
        ''''''''''''
      ) + N'''''''', ''
    + CASE WHEN [IsActive] = 1 THEN N''1'' ELSE N''0'' END + N'', ''
    + CONVERT(varchar(20), [SortOrder]) + N'', ''
    + N''CAST(N'''''' + CONVERT(nvarchar(33), [CreatedAt], 126) + N'''''' AS datetime2), ''
    + CASE WHEN [CreatedBy] IS NULL THEN N''NULL'' ELSE N''N'''''' + REPLACE([CreatedBy], '''''', '''''''''''') + N'''''''''' END + N'', ''
    + CASE WHEN [UpdatedAt] IS NULL THEN N''NULL'' ELSE N''CAST(N'''''' + CONVERT(nvarchar(33), [UpdatedAt], 126) + N'''''' AS datetime2)'' END + N'', ''
    + CASE WHEN [UpdatedBy] IS NULL THEN N''NULL'' ELSE N''N'''''' + REPLACE([UpdatedBy], '''''', '''''''''''') + N'''''''''' END
    + N'');''
FROM ' + QUOTENAME(@SourceDb) + N'.dbo.stations
WHERE [StationCode] = @SourceStationCode;';

INSERT INTO @Statements
EXEC sys.sp_executesql
    @Sql,
    N'@SourceStationCode nvarchar(50), @TargetStationCode nvarchar(50)',
    @SourceStationCode = @SourceStationCode,
    @TargetStationCode = @TargetStationCode;

SET @Sql = N'
SELECT
    50 AS SortOrder,
    N''station_feature_flags'' AS TableName,
    N''IF NOT EXISTS (SELECT 1 FROM dbo.station_feature_flags WHERE StationCode = N'''''' + REPLACE(@TargetStationCode, '''''', '''''''''''') + N'''''' AND FeatureKey = N'''''' + REPLACE([FeatureKey], '''''', '''''''''''') + N'''''''') ''
    + N''INSERT INTO dbo.station_feature_flags (Id, StationCode, FeatureKey, FeatureValue, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) VALUES (''
    + N'''''''' + CONVERT(nvarchar(36), [Id]) + N'''''''', ''
    + N''N'''''' + REPLACE(@TargetStationCode, '''''', '''''''''''') + N'''''''', ''
    + N''N'''''' + REPLACE([FeatureKey], '''''', '''''''''''') + N'''''''', ''
    + N''N'''''' + REPLACE([FeatureValue], '''''', '''''''''''') + N'''''''', ''
    + N''CAST(N'''''' + CONVERT(nvarchar(33), [CreatedAt], 126) + N'''''' AS datetime2), ''
    + CASE WHEN [CreatedBy] IS NULL THEN N''NULL'' ELSE N''N'''''' + REPLACE([CreatedBy], '''''', '''''''''''') + N'''''''''' END + N'', ''
    + CASE WHEN [UpdatedAt] IS NULL THEN N''NULL'' ELSE N''CAST(N'''''' + CONVERT(nvarchar(33), [UpdatedAt], 126) + N'''''' AS datetime2)'' END + N'', ''
    + CASE WHEN [UpdatedBy] IS NULL THEN N''NULL'' ELSE N''N'''''' + REPLACE([UpdatedBy], '''''', '''''''''''') + N'''''''''' END
    + N'');''
FROM ' + QUOTENAME(@SourceDb) + N'.dbo.station_feature_flags
WHERE [StationCode] = @SourceStationCode
ORDER BY [FeatureKey];';

INSERT INTO @Statements
EXEC sys.sp_executesql
    @Sql,
    N'@SourceStationCode nvarchar(50), @TargetStationCode nvarchar(50)',
    @SourceStationCode = @SourceStationCode,
    @TargetStationCode = @TargetStationCode;

SET @Sql = N'
SELECT
    60 AS SortOrder,
    N''station_operation_settings'' AS TableName,
    N''IF NOT EXISTS (SELECT 1 FROM dbo.station_operation_settings WHERE StationCode = N'''''' + REPLACE(@TargetStationCode, '''''', '''''''''''') + N'''''' AND SettingKey = N'''''' + REPLACE([SettingKey], '''''', '''''''''''') + N'''''''') ''
    + N''INSERT INTO dbo.station_operation_settings (Id, StationCode, SettingKey, SettingValue, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) VALUES (''
    + N'''''''' + CONVERT(nvarchar(36), [Id]) + N'''''''', ''
    + N''N'''''' + REPLACE(@TargetStationCode, '''''', '''''''''''') + N'''''''', ''
    + N''N'''''' + REPLACE([SettingKey], '''''', '''''''''''') + N'''''''', ''
    + N''N'''''' + REPLACE([SettingValue], '''''', '''''''''''') + N'''''''', ''
    + N''CAST(N'''''' + CONVERT(nvarchar(33), [CreatedAt], 126) + N'''''' AS datetime2), ''
    + N''N'''''' + REPLACE([CreatedBy], '''''', '''''''''''') + N'''''''', ''
    + CASE WHEN [UpdatedAt] IS NULL THEN N''NULL'' ELSE N''CAST(N'''''' + CONVERT(nvarchar(33), [UpdatedAt], 126) + N'''''' AS datetime2)'' END + N'', ''
    + CASE WHEN [UpdatedBy] IS NULL THEN N''NULL'' ELSE N''N'''''' + REPLACE([UpdatedBy], '''''', '''''''''''') + N'''''''''' END
    + N'');''
FROM ' + QUOTENAME(@SourceDb) + N'.dbo.station_operation_settings
WHERE [StationCode] = @SourceStationCode
ORDER BY [SettingKey];';

INSERT INTO @Statements
EXEC sys.sp_executesql
    @Sql,
    N'@SourceStationCode nvarchar(50), @TargetStationCode nvarchar(50)',
    @SourceStationCode = @SourceStationCode,
    @TargetStationCode = @TargetStationCode;

SET @Sql = N'
SELECT
    70 AS SortOrder,
    N''user_station_assignments'' AS TableName,
    N''IF NOT EXISTS (SELECT 1 FROM dbo.user_station_assignments WHERE UserId = '''''' + CONVERT(nvarchar(36), [UserId]) + N'''''' AND StationCode = N'''''' + REPLACE(@TargetStationCode, '''''', '''''''''''') + N'''''''') ''
    + N''INSERT INTO dbo.user_station_assignments (Id, UserId, StationCode, IsDefault, IsActive, CreatedAt, CreatedBy, UpdatedAt, UpdatedBy) VALUES (''
    + N'''''''' + CONVERT(nvarchar(36), [Id]) + N'''''''', ''
    + N'''''''' + CONVERT(nvarchar(36), [UserId]) + N'''''''', ''
    + N''N'''''' + REPLACE(@TargetStationCode, '''''', '''''''''''') + N'''''''', ''
    + CASE WHEN [IsDefault] = 1 THEN N''1'' ELSE N''0'' END + N'', ''
    + CASE WHEN [IsActive] = 1 THEN N''1'' ELSE N''0'' END + N'', ''
    + N''CAST(N'''''' + CONVERT(nvarchar(33), [CreatedAt], 126) + N'''''' AS datetime2), ''
    + CASE WHEN [CreatedBy] IS NULL THEN N''NULL'' ELSE N''N'''''' + REPLACE([CreatedBy], '''''', '''''''''''') + N'''''''''' END + N'', ''
    + CASE WHEN [UpdatedAt] IS NULL THEN N''NULL'' ELSE N''CAST(N'''''' + CONVERT(nvarchar(33), [UpdatedAt], 126) + N'''''' AS datetime2)'' END + N'', ''
    + CASE WHEN [UpdatedBy] IS NULL THEN N''NULL'' ELSE N''N'''''' + REPLACE([UpdatedBy], '''''', '''''''''''') + N'''''''''' END
    + N'');''
FROM ' + QUOTENAME(@SourceDb) + N'.dbo.user_station_assignments
WHERE [StationCode] = @SourceStationCode
ORDER BY [UserId], [IsDefault] DESC, [Id];';

INSERT INTO @Statements
EXEC sys.sp_executesql
    @Sql,
    N'@SourceStationCode nvarchar(50), @TargetStationCode nvarchar(50)',
    @SourceStationCode = @SourceStationCode,
    @TargetStationCode = @TargetStationCode;

SELECT TableName, InsertSql
FROM @Statements
ORDER BY SortOrder, TableName, InsertSql;
