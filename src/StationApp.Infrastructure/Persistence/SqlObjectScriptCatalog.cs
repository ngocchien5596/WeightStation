using System.Reflection;

namespace StationApp.Infrastructure.Persistence;

public static class SqlObjectScriptCatalog
{
    public static IReadOnlyList<string> ResourceNames { get; } =
    [
        "StationApp.Infrastructure.SqlScripts.fn_GetCutOrderNetWeight.sql",
        "StationApp.Infrastructure.SqlScripts.sp_GetCutOrderNetWeight.sql"
    ];

    public static string ReadRequiredScript(string resourceName)
    {
        using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded SQL script not found: {resourceName}");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
