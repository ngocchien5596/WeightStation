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
        SELECT
            CAST(NULL AS decimal(18,2)) AS NetWeightTon,
            CAST(NULL AS datetime2(7)) AS Weight1Time,
            CAST(NULL AS datetime2(7)) AS Weight2Time;

        RETURN;
    END;

    SELECT
        ISNULL(NetWeightTon, 0) AS NetWeightTon,
        Weight1Time,
        Weight2Time
    FROM #Result;
END;
GO
