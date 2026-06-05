/*
Muc dich:
- Cap quyen EXECUTE toi thieu cho user ERP goi cac stored procedure public cua StationAppLocal.
- Chay script bang account DBA / db_owner.

Huong dan:
1. Sua @DbUserName thanh database user ma connection ERP dang dung.
2. Execute script tren database StationAppLocal.
*/

USE [StationAppLocal];
GO

DECLARE @DbUserName sysname = N'erp_runtime';

IF DATABASE_PRINCIPAL_ID(@DbUserName) IS NULL
BEGIN
    THROW 53001, N'Database user khong ton tai. Hay tao user/login hoac sua @DbUserName cho dung user ERP dang dung.', 1;
END;

DECLARE @Sql nvarchar(max) = N'';

IF OBJECT_ID(N'dbo.sp_GetCutOrderNetWeight', N'P') IS NOT NULL
    SET @Sql += N'GRANT EXECUTE ON OBJECT::dbo.sp_GetCutOrderNetWeight TO ' + QUOTENAME(@DbUserName) + N';' + CHAR(13);

IF OBJECT_ID(N'dbo.sp_UpdateCutOrderErpExtras', N'P') IS NOT NULL
    SET @Sql += N'GRANT EXECUTE ON OBJECT::dbo.sp_UpdateCutOrderErpExtras TO ' + QUOTENAME(@DbUserName) + N';' + CHAR(13);

IF OBJECT_ID(N'dbo.sp_MarkCutOrderErpExportCompleted', N'P') IS NOT NULL
    SET @Sql += N'GRANT EXECUTE ON OBJECT::dbo.sp_MarkCutOrderErpExportCompleted TO ' + QUOTENAME(@DbUserName) + N';' + CHAR(13);

IF OBJECT_ID(N'dbo.sp_AdjustWeighingResult', N'P') IS NOT NULL
    SET @Sql += N'GRANT EXECUTE ON OBJECT::dbo.sp_AdjustWeighingResult TO ' + QUOTENAME(@DbUserName) + N';' + CHAR(13);

IF OBJECT_ID(N'dbo.sp_SoftDeleteCutOrderDocumentsForReissue', N'P') IS NOT NULL
    SET @Sql += N'GRANT EXECUTE ON OBJECT::dbo.sp_SoftDeleteCutOrderDocumentsForReissue TO ' + QUOTENAME(@DbUserName) + N';' + CHAR(13);

IF @Sql = N''
BEGIN
    THROW 53002, N'Khong tim thay stored procedure nao de cap quyen.', 1;
END;

EXEC sp_executesql @Sql;

PRINT N'Da cap quyen EXECUTE cho user ERP:';
PRINT @DbUserName;
GO
