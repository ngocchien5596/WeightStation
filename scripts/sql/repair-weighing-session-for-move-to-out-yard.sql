USE [StationAppLocal];
GO

DECLARE @SessionNo nvarchar(50) = N'LC26060004';
DECLARE @Now datetime2(7) = SYSDATETIME();
DECLARE @User sysname = N'DATA_FIX';

BEGIN TRY
    BEGIN TRAN;

    DECLARE @SessionId uniqueidentifier;
    DECLARE @TransactionType nvarchar(30);
    DECLARE @Weight1 decimal(18, 3);
    DECLARE @Weight2 decimal(18, 3);
    DECLARE @NetWeight decimal(18, 3);
    DECLARE @LineCount int;
    DECLARE @MissingAllocatedCount int;

    SELECT
        @SessionId = ws.Id,
        @TransactionType = ws.TransactionType,
        @Weight1 = ws.Weight1,
        @Weight2 = ws.Weight2,
        @NetWeight = ws.NetWeight
    FROM dbo.weighing_sessions AS ws WITH (UPDLOCK, ROWLOCK)
    WHERE ws.SessionNo = @SessionNo
      AND ws.IsDeleted = 0;

    IF @SessionId IS NULL
    BEGIN
        THROW 50001, N'Không tìm thấy lượt cân theo SessionNo đã khai báo.', 1;
    END

    IF @TransactionType <> N'INBOUND'
    BEGIN
        THROW 50002, N'Script này chỉ áp dụng cho luồng nhập hàng.', 1;
    END

    IF @Weight1 IS NULL OR @Weight2 IS NULL OR @NetWeight IS NULL
    BEGIN
        THROW 50003, N'Lượt cân chưa đủ Weight1/Weight2/NetWeight để chuyển xe ra.', 1;
    END

    SELECT
        @LineCount = COUNT(*),
        @MissingAllocatedCount = SUM(CASE WHEN wl.ActualAllocatedWeight IS NULL THEN 1 ELSE 0 END)
    FROM dbo.weighing_session_lines AS wl WITH (UPDLOCK, ROWLOCK)
    WHERE wl.WeighingSessionId = @SessionId
      AND wl.IsDeleted = 0;

    IF @LineCount = 0
    BEGIN
        THROW 50004, N'Lượt cân chưa có line nào nên không thể chuyển xe ra.', 1;
    END

    IF @LineCount > 1 AND @MissingAllocatedCount > 0
    BEGIN
        THROW 50005, N'Lượt cân có nhiều line nhưng còn line chưa có ActualAllocatedWeight. Cần bổ sung dữ liệu line trước khi chuyển xe ra.', 1;
    END

    UPDATE dbo.weighing_sessions
    SET SessionStatus = N'READY_TO_COMPLETE',
        IsOverweight = 0,
        OverweightAmount = 0,
        OverweightResolutionStatus = N'NOT_APPLICABLE',
        OverweightResolvedAt = NULL,
        OverweightResolvedBy = NULL,
        SyncStatus = N'SYNC_QUEUED',
        UpdatedAt = @Now,
        UpdatedBy = @User
    WHERE Id = @SessionId;

    IF @LineCount = 1
    BEGIN
        UPDATE wl
        SET wl.LineStatus = N'ALLOCATED',
            wl.ActualAllocatedWeight = COALESCE(wl.ActualAllocatedWeight, @NetWeight),
            wl.SyncStatus = N'SYNC_QUEUED',
            wl.UpdatedAt = @Now,
            wl.UpdatedBy = @User
        FROM dbo.weighing_session_lines AS wl
        WHERE wl.WeighingSessionId = @SessionId
          AND wl.IsDeleted = 0;
    END
    ELSE
    BEGIN
        UPDATE wl
        SET wl.LineStatus = N'ALLOCATED',
            wl.SyncStatus = N'SYNC_QUEUED',
            wl.UpdatedAt = @Now,
            wl.UpdatedBy = @User
        FROM dbo.weighing_session_lines AS wl
        WHERE wl.WeighingSessionId = @SessionId
          AND wl.IsDeleted = 0;
    END

    UPDATE co
    SET co.WeighingSessionId = @SessionId,
        co.SyncStatus = N'SYNC_QUEUED',
        co.UpdatedAt = @Now,
        co.UpdatedBy = @User
    FROM dbo.cut_orders AS co
    INNER JOIN dbo.weighing_session_lines AS wl
        ON wl.CutOrderId = co.Id
    WHERE wl.WeighingSessionId = @SessionId
      AND wl.IsDeleted = 0
      AND co.IsDeleted = 0
      AND co.IsCancelled = 0;

    COMMIT TRAN;

    SELECT
        ws.SessionNo,
        ws.TransactionType,
        ws.SessionStatus,
        ws.Weight1,
        ws.Weight2,
        ws.NetWeight,
        ws.IsOverweight,
        ws.OverweightResolutionStatus,
        COUNT(wl.Id) AS LineCount
    FROM dbo.weighing_sessions AS ws
    LEFT JOIN dbo.weighing_session_lines AS wl
        ON wl.WeighingSessionId = ws.Id
       AND wl.IsDeleted = 0
    WHERE ws.Id = @SessionId
    GROUP BY
        ws.SessionNo,
        ws.TransactionType,
        ws.SessionStatus,
        ws.Weight1,
        ws.Weight2,
        ws.NetWeight,
        ws.IsOverweight,
        ws.OverweightResolutionStatus;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRAN;

    THROW;
END CATCH;
GO
