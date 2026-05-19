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
        SUM(ISNULL(wsl.ActualAllocatedWeight, 0)) AS NetWeightKg
    FROM cut_orders co
    LEFT JOIN weighing_session_lines wsl
        ON wsl.CutOrderId = co.Id
    WHERE
        co.ErpCutOrderId = @ErpCutOrderId
);
