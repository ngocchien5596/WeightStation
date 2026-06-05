USE [StationAppLocal]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'dbo.sp_MarkCutOrderErpExportCompleted', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [dbo].[sp_MarkCutOrderErpExportCompleted] AS BEGIN SET NOCOUNT ON; END');
GO

ALTER PROCEDURE [dbo].[sp_MarkCutOrderErpExportCompleted]
    @ErpCutOrderId NVARCHAR(100),
    @IsCompleted BIT = 1,
    @UpdatedAt DATETIME2(7) = NULL,
    @UpdatedBy NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @NowLocal DATETIME2(7) = COALESCE(@UpdatedAt, SYSDATETIME());
    DECLARE @SystemUser NVARCHAR(200) = COALESCE(NULLIF(LTRIM(RTRIM(@UpdatedBy)), N''), N'ERP_EXPORT_COMPLETE');
    DECLARE @ActiveCount INT;

    SET @ErpCutOrderId = NULLIF(LTRIM(RTRIM(@ErpCutOrderId)), N'');

    IF @ErpCutOrderId IS NULL
        THROW 51031, N'ErpCutOrderId la bat buoc.', 1;

    SELECT @ActiveCount = COUNT(1)
    FROM dbo.cut_orders
    WHERE ErpCutOrderId = @ErpCutOrderId
      AND ISNULL(IsDeleted, 0) = 0;

    IF @ActiveCount = 0
        THROW 51032, N'Khong tim thay cut order active tuong ung de cap nhat trang thai hoan thanh ERP.', 1;

    UPDATE dbo.cut_orders
    SET
        ErpExportCompleted = ISNULL(@IsCompleted, 1),
        SyncStatus = N'SYNC_QUEUED',
        LastSyncAttemptAt = NULL,
        LastSyncError = NULL,
        UpdatedAt = @NowLocal,
        UpdatedBy = @SystemUser
    WHERE ErpCutOrderId = @ErpCutOrderId
      AND ISNULL(IsDeleted, 0) = 0;

    SELECT
        Id,
        ErpCutOrderId,
        ErpExportCompleted,
        UpdatedAt,
        UpdatedBy
    FROM dbo.cut_orders
    WHERE ErpCutOrderId = @ErpCutOrderId
      AND ISNULL(IsDeleted, 0) = 0
    ORDER BY UpdatedAt DESC, CreatedAt DESC;
END
GO
