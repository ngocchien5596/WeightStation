/*
Muc dich:
- Cap quyen toi thieu cho runtime account cua Station App
- KHONG cap quyen DDL manh nhu CREATE/ALTER/DROP FUNCTION/PROCEDURE/TABLE

Huong dan:
1. Sua gia tri @DbUserName cho dung user trong database StationAppLocal
2. Chay script bang account DBA / account co quyen cap quyen
*/

USE [StationAppLocal];
GO

DECLARE @DbUserName sysname = N'stationapp_runtime';

IF DATABASE_PRINCIPAL_ID(@DbUserName) IS NULL
BEGIN
    THROW 50001, N'Database user khong ton tai. Hay tao user/login truoc khi cap quyen runtime.', 1;
END;

DECLARE @Sql nvarchar(max) = N'
GRANT SELECT, INSERT, UPDATE, DELETE TO ' + QUOTENAME(@DbUserName) + N';
GRANT EXECUTE TO ' + QUOTENAME(@DbUserName) + N';
';

EXEC sp_executesql @Sql;
GO

PRINT N'Da cap quyen runtime co ban: SELECT / INSERT / UPDATE / DELETE / EXECUTE.';
PRINT N'Khong cap quyen CREATE/ALTER/DROP schema object.';
