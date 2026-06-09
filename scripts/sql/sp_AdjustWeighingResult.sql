USE [StationAppLocal]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

IF OBJECT_ID(N'[dbo].[sp_AdjustWeighingResult]', N'P') IS NULL
    EXEC(N'CREATE PROCEDURE [dbo].[sp_AdjustWeighingResult] AS BEGIN SET NOCOUNT ON; END');
GO

ALTER PROCEDURE [dbo].[sp_AdjustWeighingResult]
    @ErpCutOrderId NVARCHAR(100),
    @Weight1 DECIMAL(18,3) = NULL,
    @Weight2 DECIMAL(18,3) = NULL,
    @ActualAllocatedWeight DECIMAL(18,3) = NULL,
    @ActualAllocatedBagCount INT = NULL,
    @UpdateSessionMaster BIT = NULL,
    @UpdateExportFinalizedWeight BIT = 1,
    @AdjustedAt DATETIME2(7) = NULL,
    @AdjustedBy NVARCHAR(100) = NULL,
    @Reason NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @Now DATETIME2(7) = COALESCE(@AdjustedAt, SYSDATETIME());
    DECLARE @Actor NVARCHAR(100) = COALESCE(NULLIF(LTRIM(RTRIM(@AdjustedBy)), N''), N'MANUAL_WEIGHT_ADJUST');
    DECLARE @TrimmedErpCutOrderId NVARCHAR(100) = NULLIF(LTRIM(RTRIM(@ErpCutOrderId)), N'');
    DECLARE @ReasonText NVARCHAR(500) = NULLIF(LTRIM(RTRIM(@Reason)), N'');

    IF @TrimmedErpCutOrderId IS NULL
        THROW 52001, N'Phai truyen @ErpCutOrderId.', 1;

    IF @Weight1 IS NULL AND @Weight2 IS NULL AND @ActualAllocatedWeight IS NULL AND @ActualAllocatedBagCount IS NULL
        THROW 52002, N'Phai truyen it nhat mot gia tri can dieu chinh.', 1;

    IF @ActualAllocatedWeight IS NOT NULL AND @ActualAllocatedWeight < 0
        THROW 52003, N'@ActualAllocatedWeight khong duoc am.', 1;

    IF @ActualAllocatedBagCount IS NOT NULL AND @ActualAllocatedBagCount < 0
        THROW 52004, N'@ActualAllocatedBagCount khong duoc am.', 1;

    DECLARE
        @CutOrderId UNIQUEIDENTIFIER,
        @SessionId UNIQUEIDENTIFIER,
        @LineId UNIQUEIDENTIFIER,
        @IsExportScale BIT,
        @OldExportFinalizedWeight DECIMAL(18,3),
        @OldSessionWeight1 DECIMAL(18,3),
        @OldSessionWeight2 DECIMAL(18,3),
        @OldSessionNetWeight DECIMAL(18,3),
        @OldSessionStatus NVARCHAR(30),
        @OldLineActualWeight DECIMAL(18,3),
        @OldLineActualBagCount INT,
        @OldLineStatus NVARCHAR(30),
        @ActiveLineCount INT,
        @OtherLinesWeight DECIMAL(18,3),
        @ResolvedUpdateSessionMaster BIT,
        @ResolvedWeight1 DECIMAL(18,3),
        @ResolvedWeight2 DECIMAL(18,3),
        @ComputedNetWeight DECIMAL(18,3),
        @TargetActualWeight DECIMAL(18,3),
        @TargetActualBagCount INT,
        @TargetSessionNetWeight DECIMAL(18,3),
        @OldJson NVARCHAR(MAX),
        @NewJson NVARCHAR(MAX);

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT TOP (1)
            @CutOrderId = co.Id,
            @IsExportScale = ISNULL(co.IsExportScale, 0),
            @OldExportFinalizedWeight = co.ExportFinalizedWeight
        FROM dbo.cut_orders co WITH (UPDLOCK, HOLDLOCK)
        WHERE co.ErpCutOrderId = @TrimmedErpCutOrderId
          AND ISNULL(co.IsDeleted, 0) = 0
        ORDER BY co.CreatedAt DESC;

        IF @CutOrderId IS NULL
            THROW 52005, N'Khong tim thay cut order active theo @ErpCutOrderId.', 1;

        SELECT TOP (1)
            @LineId = wsl.Id,
            @SessionId = wsl.WeighingSessionId,
            @OldLineActualWeight = wsl.ActualAllocatedWeight,
            @OldLineActualBagCount = wsl.ActualAllocatedBagCount,
            @OldLineStatus = CONVERT(NVARCHAR(30), wsl.LineStatus)
        FROM dbo.weighing_session_lines wsl WITH (UPDLOCK, HOLDLOCK)
        INNER JOIN dbo.weighing_sessions ws WITH (UPDLOCK, HOLDLOCK)
            ON ws.Id = wsl.WeighingSessionId
        WHERE wsl.CutOrderId = @CutOrderId
          AND ISNULL(wsl.IsDeleted, 0) = 0
          AND ISNULL(ws.IsDeleted, 0) = 0
          AND ISNULL(ws.IsCancelled, 0) = 0
        ORDER BY
            CASE WHEN ws.Weight2Time IS NOT NULL THEN 0 ELSE 1 END,
            ws.Weight2Time DESC,
            ws.UpdatedAt DESC,
            ws.CreatedAt DESC,
            wsl.SequenceNo DESC;

        IF @LineId IS NULL OR @SessionId IS NULL
            THROW 52006, N'Khong tim thay weighing session/line active cua cut order.', 1;

        SELECT
            @OldSessionWeight1 = ws.Weight1,
            @OldSessionWeight2 = ws.Weight2,
            @OldSessionNetWeight = ws.NetWeight,
            @OldSessionStatus = CONVERT(NVARCHAR(30), ws.SessionStatus)
        FROM dbo.weighing_sessions ws WITH (UPDLOCK, HOLDLOCK)
        WHERE ws.Id = @SessionId;

        SELECT
            @ActiveLineCount = COUNT(1),
            @OtherLinesWeight = SUM(CASE WHEN wsl.Id <> @LineId THEN ISNULL(wsl.ActualAllocatedWeight, 0) ELSE 0 END)
        FROM dbo.weighing_session_lines wsl WITH (UPDLOCK, HOLDLOCK)
        WHERE wsl.WeighingSessionId = @SessionId
          AND ISNULL(wsl.IsDeleted, 0) = 0
          AND CONVERT(NVARCHAR(30), wsl.LineStatus) <> N'CANCELLED';

        SET @ActiveLineCount = ISNULL(@ActiveLineCount, 0);
        SET @OtherLinesWeight = ISNULL(@OtherLinesWeight, 0);
        SET @ResolvedUpdateSessionMaster = COALESCE(@UpdateSessionMaster, CASE WHEN @ActiveLineCount = 1 THEN 1 ELSE 0 END);

        SET @ResolvedWeight1 = COALESCE(@Weight1, @OldSessionWeight1);
        SET @ResolvedWeight2 = COALESCE(@Weight2, @OldSessionWeight2);
        SET @ComputedNetWeight = CASE
            WHEN @ResolvedWeight1 IS NOT NULL AND @ResolvedWeight2 IS NOT NULL
                THEN ABS(@ResolvedWeight1 - @ResolvedWeight2)
            ELSE NULL
        END;

        SET @TargetActualWeight = COALESCE(
            @ActualAllocatedWeight,
            CASE WHEN @ResolvedUpdateSessionMaster = 1 AND @ActiveLineCount = 1 THEN @ComputedNetWeight END,
            @OldLineActualWeight
        );

        IF @TargetActualWeight IS NULL
            THROW 52007, N'Khong xac dinh duoc ActualAllocatedWeight moi.', 1;

        IF @ActiveLineCount = 1
           AND @ActualAllocatedWeight IS NOT NULL
           AND @ComputedNetWeight IS NOT NULL
           AND ABS(@ActualAllocatedWeight - @ComputedNetWeight) > 0.001
            THROW 52008, N'Voi luot can 1 cat lenh, ActualAllocatedWeight phai bang ABS(Weight1 - Weight2).', 1;

        SET @TargetActualBagCount = COALESCE(@ActualAllocatedBagCount, @OldLineActualBagCount);
        SET @TargetSessionNetWeight = CASE
            WHEN @ResolvedUpdateSessionMaster = 1 AND @ActiveLineCount = 1 THEN @TargetActualWeight
            WHEN @ResolvedUpdateSessionMaster = 1 AND @ActiveLineCount > 1 THEN @OtherLinesWeight + @TargetActualWeight
            ELSE @OldSessionNetWeight
        END;

        IF @ResolvedUpdateSessionMaster = 1
           AND @ComputedNetWeight IS NOT NULL
           AND ABS(@ComputedNetWeight - @TargetSessionNetWeight) > 0.001
            THROW 52009, N'Tong ActualAllocatedWeight sau dieu chinh khong khop ABS(Weight1 - Weight2).', 1;

        SET @OldJson = CONCAT(
            N'ErpCutOrderId=', COALESCE(@TrimmedErpCutOrderId, N''), N'; ',
            N'CutOrderId=', CONVERT(NVARCHAR(36), @CutOrderId), N'; ',
            N'WeighingSessionId=', CONVERT(NVARCHAR(36), @SessionId), N'; ',
            N'WeighingSessionLineId=', CONVERT(NVARCHAR(36), @LineId), N'; ',
            N'ActiveLineCount=', CONVERT(NVARCHAR(20), @ActiveLineCount), N'; ',
            N'UpdateSessionMaster=', CONVERT(NVARCHAR(1), @ResolvedUpdateSessionMaster), N'; ',
            N'SessionWeight1=', COALESCE(CONVERT(NVARCHAR(50), @OldSessionWeight1), N'NULL'), N'; ',
            N'SessionWeight2=', COALESCE(CONVERT(NVARCHAR(50), @OldSessionWeight2), N'NULL'), N'; ',
            N'SessionNetWeight=', COALESCE(CONVERT(NVARCHAR(50), @OldSessionNetWeight), N'NULL'), N'; ',
            N'SessionStatus=', COALESCE(@OldSessionStatus, N'NULL'), N'; ',
            N'LineActualAllocatedWeight=', COALESCE(CONVERT(NVARCHAR(50), @OldLineActualWeight), N'NULL'), N'; ',
            N'LineActualAllocatedBagCount=', COALESCE(CONVERT(NVARCHAR(50), @OldLineActualBagCount), N'NULL'), N'; ',
            N'LineStatus=', COALESCE(@OldLineStatus, N'NULL'), N'; ',
            N'ExportFinalizedWeight=', COALESCE(CONVERT(NVARCHAR(50), @OldExportFinalizedWeight), N'NULL')
        );

        UPDATE dbo.weighing_session_lines
        SET
            ActualAllocatedWeight = @TargetActualWeight,
            ActualAllocatedBagCount = @TargetActualBagCount,
            LineStatus = CASE WHEN @TargetActualWeight > 0 THEN N'ALLOCATED' ELSE LineStatus END,
            SyncStatus = N'SYNC_QUEUED',
            LastSyncAttemptAt = NULL,
            LastSyncError = NULL,
            UpdatedAt = @Now,
            UpdatedBy = @Actor
        WHERE Id = @LineId;

        UPDATE dbo.delivery_tickets
        SET
            AllocatedWeight = @TargetActualWeight,
            AllocatedBagCount = @TargetActualBagCount,
            SyncStatus = N'SYNC_QUEUED',
            UpdatedAt = @Now,
            UpdatedBy = @Actor
        WHERE CutOrderId = @CutOrderId
          AND ISNULL(IsDeleted, 0) = 0
          AND (
              WeighingSessionLineId = @LineId
              OR WeighingSessionId = @SessionId
              OR ErpCutOrderId = @TrimmedErpCutOrderId
          );

        UPDATE dbo.weigh_tickets
        SET
            NetWeight = @TargetActualWeight,
            Weight2 = CASE
                WHEN Weight1 IS NULL THEN Weight2
                WHEN TransactionType = N'OUTBOUND' THEN ROUND(Weight1 + @TargetActualWeight, 3)
                ELSE ROUND(Weight1 - @TargetActualWeight, 3)
            END,
            Weight2Time = COALESCE(Weight2Time, (SELECT Weight2Time FROM dbo.weighing_sessions WHERE Id = @SessionId)),
            Status = CASE WHEN @TargetActualWeight > 0 THEN N'TICKET_COMPLETED' ELSE Status END,
            SyncStatus = N'SYNC_QUEUED',
            UpdatedAt = @Now,
            UpdatedBy = @Actor
        WHERE CutOrderId = @CutOrderId
          AND ISNULL(IsDeleted, 0) = 0
          AND ISNULL(RecordRole, N'') <> N'MASTER_SESSION';

        IF @ResolvedUpdateSessionMaster = 1
        BEGIN
            UPDATE dbo.weighing_sessions
            SET
                Weight1 = @ResolvedWeight1,
                Weight2 = @ResolvedWeight2,
                NetWeight = @TargetSessionNetWeight,
                IsNoLoad = CASE WHEN @TargetSessionNetWeight > 0 THEN 0 ELSE IsNoLoad END,
                SyncStatus = N'SYNC_QUEUED',
                LastSyncAttemptAt = NULL,
                LastSyncError = NULL,
                UpdatedAt = @Now,
                UpdatedBy = @Actor
            WHERE Id = @SessionId;

            UPDATE dbo.weigh_tickets
            SET
                Weight1 = @ResolvedWeight1,
                Weight2 = @ResolvedWeight2,
                NetWeight = @TargetSessionNetWeight,
                SyncStatus = N'SYNC_QUEUED',
                UpdatedAt = @Now,
                UpdatedBy = @Actor
            WHERE WeighingSessionId = @SessionId
              AND ISNULL(IsDeleted, 0) = 0
              AND ISNULL(RecordRole, N'') = N'MASTER_SESSION';
        END;

        UPDATE dbo.cut_orders
        SET
            ExportFinalizedWeight = CASE
                WHEN @UpdateExportFinalizedWeight = 1 AND @IsExportScale = 1 THEN @TargetActualWeight
                ELSE ExportFinalizedWeight
            END,
            SyncStatus = N'SYNC_QUEUED',
            LastSyncAttemptAt = NULL,
            LastSyncError = NULL,
            UpdatedAt = @Now,
            UpdatedBy = @Actor
        WHERE Id = @CutOrderId;

        SET @NewJson = CONCAT(
            N'ErpCutOrderId=', COALESCE(@TrimmedErpCutOrderId, N''), N'; ',
            N'CutOrderId=', CONVERT(NVARCHAR(36), @CutOrderId), N'; ',
            N'WeighingSessionId=', CONVERT(NVARCHAR(36), @SessionId), N'; ',
            N'WeighingSessionLineId=', CONVERT(NVARCHAR(36), @LineId), N'; ',
            N'ActiveLineCount=', CONVERT(NVARCHAR(20), @ActiveLineCount), N'; ',
            N'UpdateSessionMaster=', CONVERT(NVARCHAR(1), @ResolvedUpdateSessionMaster), N'; ',
            N'SessionWeight1=', COALESCE(CONVERT(NVARCHAR(50), @ResolvedWeight1), N'NULL'), N'; ',
            N'SessionWeight2=', COALESCE(CONVERT(NVARCHAR(50), @ResolvedWeight2), N'NULL'), N'; ',
            N'SessionNetWeight=', COALESCE(CONVERT(NVARCHAR(50), @TargetSessionNetWeight), N'NULL'), N'; ',
            N'LineActualAllocatedWeight=', COALESCE(CONVERT(NVARCHAR(50), @TargetActualWeight), N'NULL'), N'; ',
            N'LineActualAllocatedBagCount=', COALESCE(CONVERT(NVARCHAR(50), @TargetActualBagCount), N'NULL'), N'; ',
            N'ExportFinalizedWeight=', COALESCE(CONVERT(NVARCHAR(50), CASE WHEN @UpdateExportFinalizedWeight = 1 AND @IsExportScale = 1 THEN @TargetActualWeight ELSE @OldExportFinalizedWeight END), N'NULL')
        );

        IF OBJECT_ID(N'dbo.audit_logs', N'U') IS NOT NULL
        BEGIN
            INSERT INTO dbo.audit_logs
            (
                Id,
                Actor,
                Action,
                EntityType,
                EntityId,
                DetailJson,
                CreatedAt
            )
            VALUES
            (
                NEWID(),
                @Actor,
                N'ADJUST_WEIGHING_RESULT',
                N'CutOrder',
                @CutOrderId,
                CONCAT(
                    N'Reason=', COALESCE(@ReasonText, N''), CHAR(13), CHAR(10),
                    N'Old=', COALESCE(@OldJson, N''), CHAR(13), CHAR(10),
                    N'New=', COALESCE(@NewJson, N'')
                ),
                @Now
            );
        END;

        COMMIT TRANSACTION;

        SELECT
            @TrimmedErpCutOrderId AS ErpCutOrderId,
            @CutOrderId AS CutOrderId,
            @SessionId AS WeighingSessionId,
            @LineId AS WeighingSessionLineId,
            @ActiveLineCount AS ActiveLineCount,
            @ResolvedUpdateSessionMaster AS UpdatedSessionMaster,
            @ResolvedWeight1 AS Weight1,
            @ResolvedWeight2 AS Weight2,
            @TargetSessionNetWeight AS SessionNetWeight,
            @TargetActualWeight AS ActualAllocatedWeight,
            @TargetActualBagCount AS ActualAllocatedBagCount,
            CASE WHEN @UpdateExportFinalizedWeight = 1 AND @IsExportScale = 1 THEN @TargetActualWeight ELSE @OldExportFinalizedWeight END AS ExportFinalizedWeight;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;

        THROW;
    END CATCH
END
GO
