IF OBJECT_ID(N'dbo.sp_GetCutOrderNetWeight', N'P') IS NOT NULL
    DROP PROCEDURE dbo.sp_GetCutOrderNetWeight;
GO

CREATE PROCEDURE dbo.sp_GetCutOrderNetWeight
    @ErpCutOrderId NVARCHAR(100),
    @StationCode NVARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;

    SET @StationCode = NULLIF(LTRIM(RTRIM(@StationCode)), N'');

    IF (@ErpCutOrderId IS NULL OR LTRIM(RTRIM(@ErpCutOrderId)) = '')
    BEGIN
        THROW 50001, N'Phai truyen @ErpCutOrderId.', 1;
    END;

    IF (@StationCode IS NULL)
    BEGIN
        THROW 50005, N'Phai truyen @StationCode.', 1;
    END;

    SELECT *
    INTO #Result
    FROM dbo.fn_GetCutOrderNetWeight(@ErpCutOrderId, @StationCode);

    IF NOT EXISTS (SELECT 1 FROM #Result)
    BEGIN
        THROW 50002, N'Khong tim thay cat lenh tuong ung trong DB can.', 1;
    END;

    IF EXISTS (
        SELECT 1
        FROM #Result r
        INNER JOIN cut_orders co
            ON co.ErpCutOrderId = @ErpCutOrderId
        WHERE co.StationCode = @StationCode
          AND ISNULL(co.IsDeleted, 0) = 0
          AND ISNULL(co.IsExportScale, 0) = 1
          AND co.ExportFinalizedWeight IS NULL
    )
    BEGIN
        THROW 50004, N'Cat lenh xuat khau chua duoc chot tong, chua co SL thuc xuat de cung cap cho ERP.', 1;
    END;

    IF EXISTS (SELECT 1 FROM #Result WHERE ISNULL(NetWeightTon, 0) <= 0)
    BEGIN
        THROW 50003, N'Cat lenh chua co netweight hop le hoac netweight <= 0.', 1;
    END;

    SELECT
        NetWeightTon,
        Weight1Time,
        Weight2Time
    FROM #Result;
END;
GO
