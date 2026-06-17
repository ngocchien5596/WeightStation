IF OBJECT_ID(N'dbo.fn_GetCutOrderNetWeight', N'IF') IS NOT NULL
    DROP FUNCTION dbo.fn_GetCutOrderNetWeight;
GO

CREATE FUNCTION dbo.fn_GetCutOrderNetWeight
(
    @ErpCutOrderId NVARCHAR(100),
    @StationCode NVARCHAR(50)
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        CAST(
            CASE
                WHEN ISNULL(co.IsExportScale, 0) = 1
                     AND co.ExportFinalizedWeight IS NOT NULL
                    THEN co.ExportFinalizedWeight / 1000.0
                WHEN ISNULL(co.IsExportScale, 0) = 1
                     AND co.ExportFinalizedWeight IS NULL
                    THEN NULL
                WHEN COALESCE(NULLIF(LTRIM(RTRIM(co.ProductType)), N''), p.ProductType) = N'Bao'
                     AND ISNULL(sessionAgg.UseActualWeightForBaggedCutOrders, 0) = 0
                    THEN ISNULL(co.PlannedWeight, 0) / 1000.0
                ELSE
                    ISNULL(lineAgg.ActualAllocatedWeight, 0) / 1000.0
            END
            AS decimal(18,3)
        ) AS NetWeightTon,
        CAST(sessionAgg.Weight1Time AS datetime2(7)) AS Weight1Time,
        CAST(sessionAgg.Weight2Time AS datetime2(7)) AS Weight2Time
    FROM cut_orders co
    LEFT JOIN products p
        ON p.ProductCode = co.ProductCode
    OUTER APPLY
    (
        SELECT
            SUM(ISNULL(wsl.ActualAllocatedWeight, 0)) AS ActualAllocatedWeight
        FROM weighing_session_lines wsl
        INNER JOIN weighing_sessions ws
            ON ws.Id = wsl.WeighingSessionId
        WHERE wsl.CutOrderId = co.Id
          AND wsl.StationCode = @StationCode
          AND ws.StationCode = @StationCode
          AND ISNULL(wsl.IsDeleted, 0) = 0
          AND ISNULL(ws.IsDeleted, 0) = 0
          AND ISNULL(ws.SessionStatus, N'') <> N'CANCELLED'
    ) lineAgg
    OUTER APPLY
    (
        SELECT
            MIN(ws.Weight1Time) AS Weight1Time,
            MAX(ws.Weight2Time) AS Weight2Time,
            MAX(CASE WHEN ISNULL(ws.UseActualWeightForBaggedCutOrders, 0) = 1 THEN 1 ELSE 0 END) AS UseActualWeightForBaggedCutOrders
        FROM weighing_session_lines wsl
        INNER JOIN weighing_sessions ws
            ON ws.Id = wsl.WeighingSessionId
        WHERE wsl.CutOrderId = co.Id
          AND wsl.StationCode = @StationCode
          AND ws.StationCode = @StationCode
          AND ISNULL(wsl.IsDeleted, 0) = 0
          AND ISNULL(ws.IsDeleted, 0) = 0
          AND ISNULL(ws.SessionStatus, N'') <> N'CANCELLED'
    ) sessionAgg
    WHERE co.ErpCutOrderId = @ErpCutOrderId
      AND co.StationCode = @StationCode
      AND ISNULL(co.IsDeleted, 0) = 0
);
