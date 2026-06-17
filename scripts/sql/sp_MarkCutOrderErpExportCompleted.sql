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
    @StationCode NVARCHAR(50),
    @ErpCutOrderId NVARCHAR(100),
    @ErpRegistrationCode NVARCHAR(100) = NULL,
    @OrderCode NVARCHAR(100) = NULL,
    @IsCompleted BIT = 1,
    @UpdatedAt DATETIME2(7) = NULL,
    @UpdatedBy NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @NowLocal DATETIME2(7) = COALESCE(@UpdatedAt, SYSDATETIME());
    DECLARE @SystemUser NVARCHAR(200) = COALESCE(NULLIF(LTRIM(RTRIM(@UpdatedBy)), N''), N'ERP_EXPORT_COMPLETE');
    DECLARE @MatchedCount INT;

    SET @StationCode = NULLIF(LTRIM(RTRIM(@StationCode)), N'');
    SET @ErpCutOrderId = NULLIF(LTRIM(RTRIM(@ErpCutOrderId)), N'');
    SET @ErpRegistrationCode = NULLIF(LTRIM(RTRIM(@ErpRegistrationCode)), N'');
    SET @OrderCode = NULLIF(LTRIM(RTRIM(@OrderCode)), N'');

    IF @ErpRegistrationCode IS NULL
        SET @ErpRegistrationCode = @ErpCutOrderId;

    IF @OrderCode IS NULL
        SET @OrderCode = @ErpCutOrderId;

    IF @StationCode IS NULL
        THROW 51090, N'StationCode la bat buoc.', 1;

    IF @ErpCutOrderId IS NULL
        THROW 51031, N'ErpCutOrderId la bat buoc.', 1;

    SELECT @MatchedCount = COUNT(1)
    FROM dbo.cut_orders
    WHERE StationCode = @StationCode
      AND (ErpCutOrderId IN (@ErpCutOrderId, @ErpRegistrationCode, @OrderCode)
       OR ErpRegistrationCode IN (@ErpCutOrderId, @ErpRegistrationCode, @OrderCode)
       OR OrderCode IN (@ErpCutOrderId, @ErpRegistrationCode, @OrderCode));

    IF @MatchedCount = 0
        THROW 51032, N'Khong tim thay cut order tuong ung de cap nhat trang thai hoan thanh ERP.', 1;

    UPDATE dbo.cut_orders
    SET
        ErpExportCompleted = ISNULL(@IsCompleted, 1),
        SyncStatus = N'SYNC_QUEUED',
        LastSyncAttemptAt = NULL,
        LastSyncError = NULL,
        UpdatedAt = @NowLocal,
        UpdatedBy = @SystemUser
    WHERE StationCode = @StationCode
      AND (ErpCutOrderId IN (@ErpCutOrderId, @ErpRegistrationCode, @OrderCode)
       OR ErpRegistrationCode IN (@ErpCutOrderId, @ErpRegistrationCode, @OrderCode)
       OR OrderCode IN (@ErpCutOrderId, @ErpRegistrationCode, @OrderCode));

    SELECT
        Id,
        ErpCutOrderId,
        ErpRegistrationCode,
        OrderCode,
        ErpExportCompleted,
        UpdatedAt,
        UpdatedBy
    FROM dbo.cut_orders
    WHERE StationCode = @StationCode
      AND (ErpCutOrderId IN (@ErpCutOrderId, @ErpRegistrationCode, @OrderCode)
       OR ErpRegistrationCode IN (@ErpCutOrderId, @ErpRegistrationCode, @OrderCode)
       OR OrderCode IN (@ErpCutOrderId, @ErpRegistrationCode, @OrderCode))
    ORDER BY UpdatedAt DESC, CreatedAt DESC;
END
GO
