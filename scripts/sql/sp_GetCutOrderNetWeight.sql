IF OBJECT_ID(N'dbo.sp_GetCutOrderNetWeight', N'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetCutOrderNetWeight;
GO

CREATE PROCEDURE dbo.sp_GetCutOrderNetWeight
    @ErpCutOrderId NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    IF (@ErpCutOrderId IS NULL OR LTRIM(RTRIM(@ErpCutOrderId)) = '')
    BEGIN
        THROW 50001, N'Phai truyen @ErpCutOrderId.', 1;
    END;

    SELECT *
    INTO #Result
    FROM dbo.fn_GetCutOrderNetWeight(@ErpCutOrderId);

    IF NOT EXISTS (SELECT 1 FROM #Result)
    BEGIN
        THROW 50002, N'Khong tim thay cat lenh tuong ung trong DB can.', 1;
    END;

    IF EXISTS (SELECT 1 FROM #Result WHERE ISNULL(NetWeightTon, 0) <= 0)
    BEGIN
        THROW 50003, N'Cat lenh chua co netweight hop le hoac netweight <= 0.', 1;
    END;

    SELECT
        NetWeightTon,
        Weight1Time,
        Weight2Time,
        PickupTime
    FROM #Result;
END;
GO
