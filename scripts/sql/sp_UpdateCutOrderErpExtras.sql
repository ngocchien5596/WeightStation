USE [StationAppLocal]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'dbo.sp_UpdateCutOrderErpExtras', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [dbo].[sp_UpdateCutOrderErpExtras] AS BEGIN SET NOCOUNT ON; END');
GO

ALTER PROCEDURE [dbo].[sp_UpdateCutOrderErpExtras]
    @ErpCutOrderId NVARCHAR(100),
    @LotNo NVARCHAR(100) = NULL,
    @SealNo NVARCHAR(100) = NULL,
    @LoadingPlace NVARCHAR(255) = NULL,
    @UpdatedAt DATETIME2(7) = NULL,
    @UpdatedBy NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @NowLocal DATETIME2(7) = COALESCE(@UpdatedAt, SYSDATETIME());
    DECLARE @SystemUser NVARCHAR(200) = COALESCE(NULLIF(LTRIM(RTRIM(@UpdatedBy)), N''), N'ERP_PATCH_EXTRAS');
    DECLARE @ActiveCount INT;

    SET @ErpCutOrderId = NULLIF(LTRIM(RTRIM(@ErpCutOrderId)), N'');
    SET @LotNo = NULLIF(LTRIM(RTRIM(@LotNo)), N'');
    SET @SealNo = NULLIF(LTRIM(RTRIM(@SealNo)), N'');
    SET @LoadingPlace = NULLIF(LTRIM(RTRIM(@LoadingPlace)), N'');

    IF @ErpCutOrderId IS NULL
        THROW 51021, N'ErpCutOrderId la bat buoc.', 1;

    SELECT @ActiveCount = COUNT(1)
    FROM dbo.cut_orders
    WHERE ErpCutOrderId = @ErpCutOrderId
      AND ISNULL(IsDeleted, 0) = 0;

    IF @ActiveCount = 0
        THROW 51022, N'Khong tim thay cut order active tuong ung de cap nhat.', 1;

    UPDATE dbo.cut_orders
    SET
        LotNo = COALESCE(@LotNo, LotNo),
        SealNo = COALESCE(@SealNo, SealNo),
        LoadingPlace = COALESCE(@LoadingPlace, LoadingPlace),
        SyncStatus = N'SYNC_QUEUED',
        LastSyncAttemptAt = NULL,
        LastSyncError = NULL,
        UpdatedAt = @NowLocal,
        UpdatedBy = @SystemUser
    WHERE ErpCutOrderId = @ErpCutOrderId
      AND ISNULL(IsDeleted, 0) = 0;
END
GO
