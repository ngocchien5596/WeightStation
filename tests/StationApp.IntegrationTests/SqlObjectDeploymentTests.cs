using StationApp.Infrastructure.Persistence;
using Xunit;

namespace StationApp.IntegrationTests;

public class SqlObjectDeploymentTests
{
    [Fact]
    public void SqlObjectScriptCatalog_ContainsRequiredCutOrderNetWeightScripts()
    {
        Assert.Contains(
            "StationApp.Infrastructure.SqlScripts.fn_GetCutOrderNetWeight.sql",
            SqlObjectScriptCatalog.ResourceNames);
        Assert.Contains(
            "StationApp.Infrastructure.SqlScripts.sp_GetCutOrderNetWeight.sql",
            SqlObjectScriptCatalog.ResourceNames);
        Assert.Contains(
            "StationApp.Infrastructure.SqlScripts.sp_UpdateCutOrderErpExtras.sql",
            SqlObjectScriptCatalog.ResourceNames);
        Assert.Contains(
            "StationApp.Infrastructure.SqlScripts.sp_UpsertCutOrderFromErp.sql",
            SqlObjectScriptCatalog.ResourceNames);
    }

    [Theory]
    [MemberData(nameof(GetRequiredScriptNames))]
    public void SqlObjectScriptCatalog_CanReadEmbeddedScript(string resourceName)
    {
        var content = SqlObjectScriptCatalog.ReadRequiredScript(resourceName);

        Assert.False(string.IsNullOrWhiteSpace(content));
    }

    [Fact]
    public void SplitBatches_SplitsSqlScriptByGoSeparators()
    {
        const string script = """
CREATE TABLE #t(Id int);
GO
INSERT INTO #t(Id) VALUES (1);
GO
SELECT * FROM #t;
""";

        var batches = SqlObjectDeploymentService.SplitBatches(script);

        Assert.Equal(3, batches.Count);
        Assert.Contains("CREATE TABLE #t(Id int);", batches[0]);
        Assert.Contains("INSERT INTO #t(Id) VALUES (1);", batches[1]);
        Assert.Contains("SELECT * FROM #t;", batches[2]);
    }

    [Fact]
    public void SplitBatches_IgnoresUseDatabaseBatch()
    {
        const string script = """
USE [StationAppLocal]
GO
CREATE OR ALTER PROCEDURE dbo.TestProc AS
BEGIN
    SELECT 1;
END
GO
""";

        var batches = SqlObjectDeploymentService.SplitBatches(script);

        Assert.Single(batches);
        Assert.DoesNotContain("USE [StationAppLocal]", batches[0]);
        Assert.Contains("CREATE OR ALTER PROCEDURE dbo.TestProc", batches[0]);
    }

    [Fact]
    public void UpsertCutOrderScript_ValidatesProductTypeByNormalizedNull()
    {
        var content = SqlObjectScriptCatalog.ReadRequiredScript(
            "StationApp.Infrastructure.SqlScripts.sp_UpsertCutOrderFromErp.sql");

        Assert.Contains("SET @ProductType = NULL;", content);
        Assert.Contains("IF @NormalizedProductType IS NOT NULL AND @ProductType IS NULL", content);
        Assert.Contains("N'Roi'", content);
        Assert.Contains("N'Clinker'", content);
        Assert.Contains("N'Bao'", content);
        Assert.DoesNotContain("@ProductType IS NOT NULL AND @ProductType NOT IN", content);
    }

    [Fact]
    public void UpdateCutOrderErpExtrasScript_ContainsLatestErpExtrasContract()
    {
        var content = SqlObjectScriptCatalog.ReadRequiredScript(
            "StationApp.Infrastructure.SqlScripts.sp_UpdateCutOrderErpExtras.sql");

        Assert.Contains("@Description NVARCHAR(500) = NULL", content);
        Assert.Contains("@PrinterName NVARCHAR(100) = NULL", content);
        Assert.Contains("PackagePrinterName = COALESCE(@PrinterName, PackagePrinterName)", content);
        Assert.Contains("Notes = COALESCE(@Description, Notes)", content);
    }

    public static IEnumerable<object[]> GetRequiredScriptNames()
    {
        return SqlObjectScriptCatalog.ResourceNames.Select(x => new object[] { x });
    }
}
