SELECT
    DB_NAME() AS DatabaseName,
    OBJECT_SCHEMA_NAME(p.object_id) AS SchemaName,
    p.name AS ProcedureName,
    p.create_date AS CreatedAt,
    p.modify_date AS ModifiedAt,
    CASE WHEN m.definition LIKE N'%@Description NVARCHAR(500)%' THEN 1 ELSE 0 END AS HasDescriptionParameter,
    CASE WHEN m.definition LIKE N'%@PrinterName NVARCHAR(100)%' THEN 1 ELSE 0 END AS HasPrinterNameParameter,
    CASE WHEN m.definition LIKE N'%PackagePrinterName = COALESCE(@PrinterName%' THEN 1 ELSE 0 END AS UpdatesPackagePrinterName,
    CASE WHEN m.definition LIKE N'%Notes = COALESCE(@Description%' THEN 1 ELSE 0 END AS UpdatesNotes
FROM sys.procedures p
JOIN sys.sql_modules m ON m.object_id = p.object_id
WHERE p.object_id = OBJECT_ID(N'dbo.sp_UpdateCutOrderErpExtras', N'P');

SELECT
    prm.parameter_id,
    prm.name AS ParameterName,
    TYPE_NAME(prm.user_type_id) AS TypeName,
    prm.max_length,
    prm.has_default_value
FROM sys.parameters prm
WHERE prm.object_id = OBJECT_ID(N'dbo.sp_UpdateCutOrderErpExtras', N'P')
ORDER BY prm.parameter_id;

SELECT OBJECT_DEFINITION(OBJECT_ID(N'dbo.sp_UpdateCutOrderErpExtras', N'P')) AS ProcedureDefinition;
