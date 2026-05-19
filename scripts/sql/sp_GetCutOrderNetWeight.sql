IF OBJECT_ID(N'dbo.sp_GetCutOrderNetWeight', N'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetCutOrderNetWeight;
GO

CREATE PROCEDURE dbo.sp_GetCutOrderNetWeight
    @ErpCutOrderId NVARCHAR(100) = NULL,
    @CutOrderId UNIQUEIDENTIFIER = NULL
AS
BEGIN
    SET NOCOUNT ON;

    IF (
        (@ErpCutOrderId IS NULL OR LTRIM(RTRIM(@ErpCutOrderId)) = '')
        AND @CutOrderId IS NULL
    )
    BEGIN
        THROW 50001, N'Phải truyền @ErpCutOrderId hoặc @CutOrderId.', 1;
    END;

    ;WITH CutOrderWeights AS
    (
        SELECT
            co.Id AS CutOrderId,
            co.ErpCutOrderId,
            co.OrderCode,
            co.VehiclePlate,
            co.CustomerCode,
            co.CustomerName,
            co.ProductCode,
            co.ProductName,
            co.ProductType,
            co.PlannedWeight,
            co.BagCount AS PlannedBagCount,
            co.CutOrderStatus,
            co.ProcessingStage,
            SUM(ISNULL(wsl.ActualAllocatedWeight, 0)) AS NetWeightKg,
            SUM(ISNULL(wsl.ActualAllocatedBagCount, 0)) AS ActualBagCount,
            COUNT(DISTINCT wsl.WeighingSessionId) AS SessionCount
        FROM cut_orders co
        LEFT JOIN weighing_session_lines wsl
            ON wsl.CutOrderId = co.Id
        WHERE
            (@CutOrderId IS NULL OR co.Id = @CutOrderId)
            AND (
                @ErpCutOrderId IS NULL
                OR LTRIM(RTRIM(@ErpCutOrderId)) = ''
                OR co.ErpCutOrderId = @ErpCutOrderId
            )
        GROUP BY
            co.Id,
            co.ErpCutOrderId,
            co.OrderCode,
            co.VehiclePlate,
            co.CustomerCode,
            co.CustomerName,
            co.ProductCode,
            co.ProductName,
            co.ProductType,
            co.PlannedWeight,
            co.BagCount,
            co.CutOrderStatus,
            co.ProcessingStage
    )
    SELECT *
    INTO #Result
    FROM CutOrderWeights;

    IF NOT EXISTS (SELECT 1 FROM #Result)
    BEGIN
        THROW 50002, N'Không tìm thấy cắt lệnh tương ứng trong DB cân.', 1;
    END;

    IF EXISTS (SELECT 1 FROM #Result WHERE ISNULL(NetWeightKg, 0) <= 0)
    BEGIN
        THROW 50003, N'Cắt lệnh chưa có netweight hợp lệ hoặc netweight <= 0.', 1;
    END;

    SELECT
        CutOrderId,
        ErpCutOrderId,
        OrderCode,
        VehiclePlate,
        CustomerCode,
        CustomerName,
        ProductCode,
        ProductName,
        ProductType,
        PlannedWeight,
        PlannedBagCount,
        NetWeightKg,
        ActualBagCount,
        SessionCount,
        CutOrderStatus,
        ProcessingStage
    FROM #Result;
END;
