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

    public static IEnumerable<object[]> GetRequiredScriptNames()
    {
        return SqlObjectScriptCatalog.ResourceNames.Select(x => new object[] { x });
    }
}
