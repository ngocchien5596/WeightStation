USE [StationAppLocal]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
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
    DECLARE @Weight2 DECIMAL(18,3);
    DECLARE @SessionStatus NVARCHAR(60);
    DECLARE @ActiveLineCount INT;
    DECLARE @PreserveSessionForReuse BIT = 0;
    DECLARE @IsExportScale BIT = 0;
    DECLARE @TransactionType NVARCHAR(30);
    DECLARE @ExportLineCount INT = 0;

    SELECT TOP (1)
        @CutOrderId = co.Id,
        @SessionId = co.WeighingSessionId,
        @IsExportScale = ISNULL(co.IsExportScale, 0),
        @TransactionType = co.TransactionType
    FROM dbo.cut_orders co
    WHERE co.ErpCutOrderId = @ErpCutOrderId
      AND ISNULL(co.IsDeleted, 0) = 0;

    IF (@CutOrderId IS NULL)
    BEGIN
        THROW 50012, N'Khong tim thay cat lenh tuong ung.', 1;
    END;

    SELECT @ExportLineCount = COUNT(1)
    FROM dbo.weighing_session_lines wsl
    INNER JOIN dbo.weighing_sessions ws
        ON ws.Id = wsl.WeighingSessionId
    WHERE wsl.CutOrderId = @CutOrderId
      AND ISNULL(wsl.IsDeleted, 0) = 0
      AND ISNULL(ws.IsDeleted, 0) = 0
      AND ISNULL(ws.IsCancelled, 0) = 0;

    IF (@IsExportScale = 1 AND @TransactionType = N'OUTBOUND' AND @ExportLineCount > 0)
    BEGIN
        DECLARE @TempCutOrderId UNIQUEIDENTIFIER = NEWID();
        DECLARE @TempDisplayCode NVARCHAR(100);
        DECLARE @TempDisplaySeq INT;
        DECLARE @MovedSessions TABLE (Id UNIQUEIDENTIFIER PRIMARY KEY);

        SELECT @TempDisplaySeq = ISNULL(MAX(TRY_CONVERT(INT, SUBSTRING(TemporaryExportDisplayCode, 8, 20))), 0) + 1
        FROM dbo.cut_orders
        WHERE TemporaryExportDisplayCode LIKE N'CL-TAM-[0-9][0-9][0-9][0-9]%';

        SET @TempDisplayCode = CONCAT(N'CL-TAM-', RIGHT(CONCAT(N'0000', @TempDisplaySeq), 4));

        INSERT INTO @MovedSessions(Id)
        SELECT DISTINCT wsl.WeighingSessionId
        FROM dbo.weighing_session_lines wsl
        WHERE wsl.CutOrderId = @CutOrderId
          AND ISNULL(wsl.IsDeleted, 0) = 0;

        INSERT INTO dbo.cut_orders
        (
            Id,
            ErpCutOrderId,
            ErpRegistrationCode,
            CutOrderSource,
            CutOrderStatus,
            TransactionType,
            TransportMethod,
            VehiclePlate,
            MoocNumber,
            ReceiverName,
            ReceiverIdNo,
            CustomerCode,
            CustomerName,
            ProductCode,
            ProductName,
            ProductType,
            OrderCode,
            LotNo,
            RepresentativeName,
            Market,
            ConsumptionPlace,
            LoadingPlace,
            SealNo,
            PlannedWeight,
            BagCount,
            Notes,
            IsCancelled,
            IsDeleted,
            HasOverweightCase,
            ProcessingStage,
            WeighingSessionId,
            CurrentPrimaryWeighTicketId,
            CurrentPrimaryDeliveryTicketId,
            CarryForwardWeight1,
            CarryForwardWeight1Time,
            IsExportScale,
            ExportStartedAt,
            ExportStartedBy,
            ErpExportCompleted,
            IsTemporaryExport,
            MappedRealCutOrderId,
            TemporaryExportCreatedReason,
            TemporaryExportDisplayCode,
            TemporaryExportSourceErpCutOrderId,
            MappedAt,
            MappedBy,
            SyncStatus,
            IdempotencyKey,
            AppVersion,
            IsInboundProcessed,
            CreatedAt,
            CreatedBy,
            UpdatedAt,
            UpdatedBy
        )
        SELECT
            @TempCutOrderId,
            NULL,
            NULL,
            co.CutOrderSource,
            N'IN_SESSION',
            co.TransactionType,
            co.TransportMethod,
            @TempDisplayCode,
            co.MoocNumber,
            co.ReceiverName,
            co.ReceiverIdNo,
            co.CustomerCode,
            co.CustomerName,
            co.ProductCode,
            co.ProductName,
            co.ProductType,
            co.OrderCode,
            co.LotNo,
            co.RepresentativeName,
            co.Market,
            co.ConsumptionPlace,
            co.LoadingPlace,
            co.SealNo,
            co.PlannedWeight,
            co.BagCount,
            co.Notes,
            0,
            0,
            co.HasOverweightCase,
            N'WEIGHING',
            NULL,
            co.CurrentPrimaryWeighTicketId,
            co.CurrentPrimaryDeliveryTicketId,
            co.CarryForwardWeight1,
            co.CarryForwardWeight1Time,
            1,
            COALESCE(co.ExportStartedAt, @Now),
            COALESCE(co.ExportStartedBy, N'ERP_REISSUE'),
            0,
            1,
            co.Id,
            N'ERP_REISSUE_HOLDING',
            @TempDisplayCode,
            @ErpCutOrderId,
            @Now,
            N'ERP_REISSUE',
            N'SYNC_QUEUED',
            NEWID(),
            co.AppVersion,
            0,
            @Now,
            N'ERP_REISSUE',
            @Now,
            N'ERP_REISSUE'
        FROM dbo.cut_orders co
        WHERE co.Id = @CutOrderId;

        UPDATE dbo.weighing_session_lines
        SET
            CutOrderId = @TempCutOrderId,
            SyncStatus = N'SYNC_QUEUED',
            LastSyncAttemptAt = NULL,
            LastSyncError = NULL,
            UpdatedAt = @Now,
            UpdatedBy = N'ERP_REISSUE'
        WHERE CutOrderId = @CutOrderId
          AND ISNULL(IsDeleted, 0) = 0;

        UPDATE dbo.weigh_tickets
        SET
            CutOrderId = @TempCutOrderId,
            ErpCutOrderId = NULL,
            SyncStatus = N'SYNC_QUEUED',
            UpdatedAt = @Now,
            UpdatedBy = N'ERP_REISSUE'
        WHERE CutOrderId = @CutOrderId
          AND ISNULL(IsDeleted, 0) = 0;

        UPDATE dbo.delivery_tickets
        SET
            CutOrderId = @TempCutOrderId,
            ErpCutOrderId = N'',
            SyncStatus = 1,
            UpdatedAt = @Now,
            UpdatedBy = N'ERP_REISSUE'
        WHERE CutOrderId = @CutOrderId
          AND ISNULL(IsDeleted, 0) = 0;

        UPDATE ws
        SET
            SyncStatus = N'SYNC_QUEUED',
            LastSyncAttemptAt = NULL,
            LastSyncError = NULL,
            UpdatedAt = @Now,
            UpdatedBy = N'ERP_REISSUE'
        FROM dbo.weighing_sessions ws
        INNER JOIN @MovedSessions moved
            ON moved.Id = ws.Id;

        UPDATE dbo.cut_orders
        SET
            IsDeleted = 1,
            DeletedAt = @Now,
            DeletedBy = N'ERP_REISSUE',
            MappedTemporaryCutOrderId = @TempCutOrderId,
            WeighingSessionId = NULL,
            CurrentPrimaryWeighTicketId = NULL,
            CurrentPrimaryDeliveryTicketId = NULL,
            SyncStatus = N'SYNC_QUEUED',
            UpdatedAt = @Now,
            UpdatedBy = N'ERP_REISSUE'
        WHERE Id = @CutOrderId;

        SELECT
            @CutOrderId AS DeletedCutOrderId,
            @TempCutOrderId AS TemporaryCutOrderId,
            @TempDisplayCode AS TemporaryExportDisplayCode,
            @ExportLineCount AS MovedLineCount;

        RETURN;
    END;

    IF (@SessionId IS NULL)
    BEGIN
        SELECT TOP (1)
            @SessionId = wsl.WeighingSessionId
        FROM dbo.weighing_session_lines wsl
        INNER JOIN dbo.weighing_sessions ws
            ON ws.Id = wsl.WeighingSessionId
        WHERE wsl.CutOrderId = @CutOrderId
          AND ISNULL(wsl.IsDeleted, 0) = 0
          AND ISNULL(ws.IsDeleted, 0) = 0
          AND ISNULL(ws.IsCancelled, 0) = 0
        ORDER BY
            CASE WHEN ws.Weight2 IS NOT NULL THEN 0 ELSE 1 END,
            ws.Weight2Time DESC,
            ws.UpdatedAt DESC,
            ws.CreatedAt DESC;
    END;

    IF (@SessionId IS NOT NULL)
    BEGIN
        SELECT
            @Weight1 = ws.Weight1,
            @Weight1Time = ws.Weight1Time,
            @Weight2 = ws.Weight2,
            @SessionStatus = ws.SessionStatus
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

        IF (@Weight2 IS NOT NULL)
        BEGIN
            THROW 50014, N'Khong cho phep RA cat lenh khi luot can da co so can lan 2.', 1;
        END;

        IF (@Weight1 IS NOT NULL
            AND @Weight2 IS NULL
            AND ISNULL(@SessionStatus, N'') = N'PENDING_WEIGHT2')
        BEGIN
            SET @PreserveSessionForReuse = 1;
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
      AND IsDeleted = 0
      AND (
          @PreserveSessionForReuse = 0
          OR ISNULL(WeighingSessionId, '00000000-0000-0000-0000-000000000000') <> @SessionId
          OR ISNULL(RecordRole, N'') <> N'MASTER_SESSION'
      );

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

    IF (@SessionId IS NOT NULL AND ISNULL(@ActiveLineCount, 0) <= 1 AND @PreserveSessionForReuse = 0)
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
        WeighingSessionId = CASE WHEN @PreserveSessionForReuse = 1 THEN WeighingSessionId ELSE NULL END,
        CurrentPrimaryWeighTicketId = NULL,
        CurrentPrimaryDeliveryTicketId = NULL,
        HasOverweightCase = 0,
        SyncStatus = N'SYNC_QUEUED',
        UpdatedAt = @Now,
        UpdatedBy = N'ERP_REISSUE'
    WHERE Id = @CutOrderId;

END;
GO
