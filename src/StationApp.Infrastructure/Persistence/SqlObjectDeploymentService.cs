using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace StationApp.Infrastructure.Persistence;

public static class SqlObjectDeploymentService
{
    private static readonly Regex BatchSeparatorRegex = new(
        @"^\s*GO\s*(?:--.*)?$",
        RegexOptions.Multiline | RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex UseDatabaseBatchRegex = new(
        @"^\s*USE\s+(?:\[[^\]]+\]|[A-Za-z0-9_]+)\s*;?\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static async Task DeployRequiredObjectsAsync(
        StationDbContext db,
        ILogger? logger,
        CancellationToken ct)
    {
        foreach (var resourceName in SqlObjectScriptCatalog.ResourceNames)
        {
            var script = SqlObjectScriptCatalog.ReadRequiredScript(resourceName);
            var batches = SplitBatches(script);
            var scriptHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(script)));

            foreach (var batch in batches)
            {
                if (string.IsNullOrWhiteSpace(batch))
                {
                    continue;
                }

                await db.Database.ExecuteSqlRawAsync(batch, ct);
            }

            logger?.LogInformation(
                "Deployed SQL object script {ResourceName}. Sha256={Sha256}.",
                resourceName,
                scriptHash);
        }
    }

    public static IReadOnlyList<string> SplitBatches(string script)
    {
        if (string.IsNullOrWhiteSpace(script))
        {
            return Array.Empty<string>();
        }

        var normalized = script.Replace("\r\n", "\n");
        var segments = BatchSeparatorRegex.Split(normalized)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Where(x => !UseDatabaseBatchRegex.IsMatch(x))
            .ToList();

        return segments.AsReadOnly();
    }
}
