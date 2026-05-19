IF OBJECT_ID(N'dbo.fn_GetCutOrderNetWeight', N'IF') IS NOT NULL
    DROP FUNCTION dbo.fn_GetCutOrderNetWeight;
GO

CREATE FUNCTION dbo.fn_GetCutOrderNetWeight
(
    @ErpCutOrderId NVARCHAR(100)
)
RETURNS TABLE
AS
RETURN
(
    SELECT
        CAST(
            CASE
                WHEN co.ProductType = N'Bao'
                    THEN ISNULL(co.PlannedWeight, 0) / 1000.0
                ELSE
                    ISNULL(lineAgg.ActualAllocatedWeight, 0) / 1000.0
            END
            AS decimal(18,3)
        ) AS NetWeightTon,
        CASE
            WHEN sessionAgg.Weight1Time IS NULL THEN NULL
            ELSE
                CAST(DATEDIFF(SECOND, CAST('1970-01-01T00:00:00' AS datetime2), DATEADD(HOUR, -7, CAST(sessionAgg.Weight1Time AS datetime2))) AS bigint) * 1000
                + DATEPART(MILLISECOND, CAST(sessionAgg.Weight1Time AS datetime2))
        END AS Weight1Timestamp,
        CASE
            WHEN sessionAgg.Weight2Time IS NULL THEN NULL
            ELSE
                CAST(DATEDIFF(SECOND, CAST('1970-01-01T00:00:00' AS datetime2), DATEADD(HOUR, -7, CAST(sessionAgg.Weight2Time AS datetime2))) AS bigint) * 1000
                + DATEPART(MILLISECOND, CAST(sessionAgg.Weight2Time AS datetime2))
        END AS Weight2Timestamp,
        CASE
            WHEN sessionAgg.Weight2Time IS NULL THEN NULL
            ELSE
                CAST(DATEDIFF(SECOND, CAST('1970-01-01T00:00:00' AS datetime2), DATEADD(HOUR, -7, CAST(sessionAgg.Weight2Time AS datetime2))) AS bigint) * 1000
                + DATEPART(MILLISECOND, CAST(sessionAgg.Weight2Time AS datetime2))
        END AS PickupTimestamp
    FROM cut_orders co
    OUTER APPLY
    (
        SELECT
            SUM(ISNULL(wsl.ActualAllocatedWeight, 0)) AS ActualAllocatedWeight
        FROM weighing_session_lines wsl
        WHERE wsl.CutOrderId = co.Id
    ) lineAgg
    OUTER APPLY
    (
        SELECT
            MAX(ws.Weight1Time) AS Weight1Time,
            MAX(ws.Weight2Time) AS Weight2Time
        FROM weighing_session_lines wsl
        INNER JOIN weighing_sessions ws
            ON ws.Id = wsl.WeighingSessionId
        WHERE wsl.CutOrderId = co.Id
    ) sessionAgg
    WHERE co.ErpCutOrderId = @ErpCutOrderId
);
