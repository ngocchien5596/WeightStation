/*
Muc dich:
- Cap quyen cho deploy/migration account de update schema va SQL objects
- Account nay chi dung khi release / update DB, KHONG dung cho app runtime hang ngay

Huong dan:
1. Sua gia tri @DbUserName
2. Chay bang account DBA / account co quyen cap quyen
*/

USE [StationAppLocal];
GO

DECLARE @DbUserName sysname = N'stationapp_deploy';

IF DATABASE_PRINCIPAL_ID(@DbUserName) IS NULL
BEGIN
    THROW 50001, N'Database user khong ton tai. Hay tao user/login truoc khi cap quyen deploy.', 1;
END;

DECLARE @Sql nvarchar(max) = N'
GRANT SELECT, INSERT, UPDATE, DELETE TO ' + QUOTENAME(@DbUserName) + N';
GRANT EXECUTE TO ' + QUOTENAME(@DbUserName) + N';
GRANT CREATE TABLE TO ' + QUOTENAME(@DbUserName) + N';
GRANT CREATE VIEW TO ' + QUOTENAME(@DbUserName) + N';
GRANT CREATE PROCEDURE TO ' + QUOTENAME(@DbUserName) + N';
GRANT CREATE FUNCTION TO ' + QUOTENAME(@DbUserName) + N';
GRANT CREATE INDEX TO ' + QUOTENAME(@DbUserName) + N';
GRANT ALTER TO ' + QUOTENAME(@DbUserName) + N';
';

EXEC sp_executesql @Sql;
GO

PRINT N'Da cap quyen deploy/migration de update schema va SQL objects.';
PRINT N'Khuyen nghi: chi dung account nay cho DbMigrator/update schema, khong dung cho app runtime.';
