USE [StationAppLocal]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER OFF
GO

IF OBJECT_ID(N'[dbo].[sp_SoftDeleteCutOrderDocumentsForReissue]', N'P') IS NULL
BEGIN
    EXEC(N'CREATE PROCEDURE [dbo].[sp_SoftDeleteCutOrderDocumentsForReissue] AS BEGIN SET NOCOUNT ON; END;');
END;
GO

ALTER PROCEDURE [dbo].[sp_SoftDeleteCutOrderDocumentsForReissue]
    @ErpCutOrderId NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    IF (@ErpCutOrderId IS NULL OR LTRIM(RTRIM(@ErpCutOrderId)) = '')
    BEGIN
        THROW 50011, N'Phai truyen @ErpCutOrderId.', 1;
    END;

    DECLARE @Now DATETIME2(7) = SYSDATETIME();
    DECLARE @CutOrderId UNIQUEIDENTIFIER;
    DECLARE @SessionId UNIQUEIDENTIFIER;
    DECLARE @Weight1 DECIMAL(18,3);
    DECLARE @Weight1Time DATETIME2(7);
    DECLARE @ActiveLineCount INT;

    SELECT TOP (1)
        @CutOrderId = co.Id,
        @SessionId = co.WeighingSessionId
    FROM dbo.cut_orders co
    WHERE co.ErpCutOrderId = @ErpCutOrderId
      AND ISNULL(co.IsDeleted, 0) = 0;

    IF (@CutOrderId IS NULL)
    BEGIN
        THROW 50012, N'Khong tim thay cat lenh tuong ung.', 1;
    END;

    IF (@SessionId IS NOT NULL)
    BEGIN
        SELECT
            @Weight1 = ws.Weight1,
            @Weight1Time = ws.Weight1Time
        FROM dbo.weighing_sessions ws
        WHERE ws.Id = @SessionId;

        SELECT
            @ActiveLineCount = COUNT(1)
        FROM dbo.weighing_session_lines wsl
        WHERE wsl.WeighingSessionId = @SessionId
          AND ISNULL(wsl.IsDeleted, 0) = 0
          AND wsl.LineStatus <> N'CANCELLED';

        IF (ISNULL(@ActiveLineCount, 0) > 1)
        BEGIN
            THROW 50013, N'Chua ho tro soft delete khi luot can cu con nhieu hon 1 cat lenh.', 1;
        END;
    END;

    UPDATE dbo.weigh_tickets
    SET
        IsDeleted = 1,
        IsCancelled = 1,
        Status = N'TICKET_CANCELLED',
        NetWeight = 0,
        SyncStatus = N'SYNC_QUEUED',
        DeletedAt = @Now,
        DeletedBy = N'ERP_REISSUE',
        UpdatedAt = @Now,
        UpdatedBy = N'ERP_REISSUE'
    WHERE CutOrderId = @CutOrderId
      AND IsDeleted = 0;

    UPDATE dbo.delivery_tickets
    SET
        IsDeleted = 1,
        AllocatedWeight = 0,
        AllocatedBagCount = 0,
        SyncStatus = 1,
        DeletedAt = @Now,
        DeletedBy = N'ERP_REISSUE',
        UpdatedAt = @Now,
        UpdatedBy = N'ERP_REISSUE'
    WHERE CutOrderId = @CutOrderId
      AND IsDeleted = 0;

    UPDATE dbo.weighing_session_lines
    SET
        IsDeleted = 1,
        DeletedAt = @Now,
        DeletedBy = N'ERP_REISSUE',
        LineStatus = N'CANCELLED',
        ActualAllocatedWeight = NULL,
        ActualAllocatedBagCount = NULL,
        DeliveryTicketId = NULL,
        UpdatedAt = @Now,
        UpdatedBy = N'ERP_REISSUE'
    WHERE CutOrderId = @CutOrderId
      AND (@SessionId IS NULL OR WeighingSessionId = @SessionId);

    IF (@SessionId IS NOT NULL AND ISNULL(@ActiveLineCount, 0) <= 1)
    BEGIN
        UPDATE dbo.weighing_sessions
        SET
            IsDeleted = 1,
            DeletedAt = @Now,
            DeletedBy = N'ERP_REISSUE',
            SessionStatus = N'CANCELLED',
            IsCancelled = 1,
            UpdatedAt = @Now,
            UpdatedBy = N'ERP_REISSUE'
        WHERE Id = @SessionId;
    END;

    UPDATE dbo.cut_orders
    SET
        CarryForwardWeight1 = COALESCE(@Weight1, CarryForwardWeight1),
        CarryForwardWeight1Time = COALESCE(@Weight1Time, CarryForwardWeight1Time),
        IsDeleted = 1,
        DeletedAt = @Now,
        DeletedBy = N'ERP_REISSUE',
        WeighingSessionId = NULL,
        CurrentPrimaryWeighTicketId = NULL,
        CurrentPrimaryDeliveryTicketId = NULL,
        HasOverweightCase = 0,
        SyncStatus = N'SYNC_QUEUED',
        UpdatedAt = @Now,
        UpdatedBy = N'ERP_REISSUE'
    WHERE Id = @CutOrderId;

END;
GO
