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
        THROW 50001, N'Phải truyền @ErpCutOrderId.', 1;
    END;

    SELECT *
    INTO #Result
    FROM dbo.fn_GetCutOrderNetWeight(@ErpCutOrderId);

    IF NOT EXISTS (SELECT 1 FROM #Result)
    BEGIN
        THROW 50002, N'Không tìm thấy cắt lệnh tương ứng trong DB cân.', 1;
    END;

    IF EXISTS (SELECT 1 FROM #Result WHERE ISNULL(NetWeightKg, 0) <= 0)
    BEGIN
        THROW 50003, N'Cắt lệnh chưa có netweight hợp lệ hoặc netweight <= 0.', 1;
    END;

    SELECT NetWeightKg
    FROM #Result;
END;
