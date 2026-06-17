SELECT @@SERVERNAME AS ServerName, DB_NAME() AS DatabaseName;

DECLARE @Expected TABLE
(
    ObjectName sysname NOT NULL,
    ObjectType nvarchar(20) NOT NULL,
    ExpectedStationCodePosition int NOT NULL
);

INSERT INTO @Expected(ObjectName, ObjectType, ExpectedStationCodePosition)
VALUES
    (N'sp_UpsertCutOrderFromErp', N'PROCEDURE', 1),
    (N'sp_UpdateCutOrderErpExtras', N'PROCEDURE', 1),
    (N'sp_MarkCutOrderErpExportCompleted', N'PROCEDURE', 1),
    (N'sp_SoftDeleteCutOrderDocumentsForReissue', N'PROCEDURE', 2),
    (N'sp_GetCutOrderNetWeight', N'PROCEDURE', 2),
    (N'fn_GetCutOrderNetWeight', N'FUNCTION', 2);

SELECT
    e.ObjectType,
    e.ObjectName,
    CASE WHEN o.object_id IS NULL THEN N'MISSING_OBJECT'
         WHEN stationParam.parameter_id IS NULL THEN N'MISSING_STATIONCODE'
         WHEN stationParam.parameter_id <> e.ExpectedStationCodePosition THEN N'WRONG_POSITION'
         ELSE N'OK'
    END AS CheckStatus,
    stationParam.parameter_id AS StationCodePosition,
    e.ExpectedStationCodePosition
FROM @Expected AS e
LEFT JOIN sys.objects AS o
    ON o.name = e.ObjectName
LEFT JOIN sys.parameters AS stationParam
    ON stationParam.object_id = o.object_id
   AND stationParam.name = N'@StationCode'
ORDER BY e.ObjectName;

SELECT
    o.name AS ObjectName,
    p.parameter_id,
    p.name AS ParameterName
FROM sys.objects AS o
INNER JOIN sys.parameters AS p
    ON p.object_id = o.object_id
WHERE o.name IN (SELECT ObjectName FROM @Expected)
ORDER BY o.name, p.parameter_id;
