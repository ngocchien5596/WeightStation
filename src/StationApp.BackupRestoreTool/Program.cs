using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using StationApp.Domain.Entities;
using StationApp.Infrastructure.Persistence;

var options = RestoreOptions.Parse(args);
if (!options.IsValid(out var validationError))
{
    Console.Error.WriteLine(validationError);
    RestoreOptions.PrintUsage();
    return 2;
}

using var http = new HttpClient
{
    BaseAddress = EnsureTrailingSlash(new Uri(options.BackupApiUrl)),
    Timeout = TimeSpan.FromMinutes(5)
};
http.DefaultRequestHeaders.Remove("X-Api-Key");
http.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);

var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};

Console.WriteLine($"Backup restore tool started. Station={options.StationCode}. Mode={(options.Execute ? "EXECUTE" : "DRY-RUN")}");

var health = await http.GetAsync("health");
health.EnsureSuccessStatusCode();
Console.WriteLine("Backup API health: OK");

var summary = await http.GetFromJsonAsync<Dictionary<string, JsonElement>>(
    $"api/backup-export/stations/{options.StationCode}/summary",
    jsonOptions);
Console.WriteLine("Backup summary:");
foreach (var item in summary ?? [])
{
    Console.WriteLine($"  {item.Key}: {item.Value}");
}

if (!options.Execute)
{
    Console.WriteLine("Dry-run only. Add --execute to write data into local DB.");
    return 0;
}

var dbOptions = new DbContextOptionsBuilder<StationDbContext>()
    .UseSqlServer(options.LocalConnectionString, sql => sql.EnableRetryOnFailure().UseCompatibilityLevel(120))
    .Options;

await using var db = new StationDbContext(dbOptions);
await StationDatabaseInitializer.InitializeAsync(db, loggerFactory: null, CancellationToken.None, deploySqlObjects: true);

var total = 0;
total += await RestoreStationScopedAsync<Vehicle>(db, http, $"api/backup-export/stations/{options.StationCode}/vehicles", jsonOptions, options.PageSize);
total += await RestoreStationScopedAsync<Customer>(db, http, $"api/backup-export/stations/{options.StationCode}/customers", jsonOptions, options.PageSize);
total += await RestoreStationScopedAsync<Product>(db, http, $"api/backup-export/stations/{options.StationCode}/products", jsonOptions, options.PageSize);
total += await RestoreStationScopedAsync<IncomingSeedVehicle>(db, http, $"api/backup-export/stations/{options.StationCode}/incoming-seed-vehicles", jsonOptions, options.PageSize);
total += await RestoreStationScopedAsync<CutOrder>(db, http, $"api/backup-export/stations/{options.StationCode}/cut-orders", jsonOptions, options.PageSize);
total += await RestoreStationScopedAsync<WeighingSession>(db, http, $"api/backup-export/stations/{options.StationCode}/weighing-sessions", jsonOptions, options.PageSize);
total += await RestoreStationScopedAsync<WeighingSessionLine>(db, http, $"api/backup-export/stations/{options.StationCode}/weighing-session-lines", jsonOptions, options.PageSize);
total += await RestoreStationScopedAsync<WeighTicket>(db, http, $"api/backup-export/stations/{options.StationCode}/weigh-tickets", jsonOptions, options.PageSize);
total += await RestoreStationScopedAsync<DeliveryTicket>(db, http, $"api/backup-export/stations/{options.StationCode}/delivery-tickets", jsonOptions, options.PageSize);
total += await RestoreStationScopedAsync<WeighingSessionImage>(db, http, $"api/backup-export/stations/{options.StationCode}/weighing-session-images", jsonOptions, Math.Min(options.PageSize, 100));
total += await RestoreStationScopedAsync<AuditLog>(db, http, $"api/backup-export/stations/{options.StationCode}/audit-logs", jsonOptions, options.PageSize);
total += await RestoreStationScopedAsync<User>(db, http, "api/backup-export/users", jsonOptions, options.PageSize);
total += await RestoreStationScopedAsync<UserStationAssignment>(db, http, $"api/backup-export/stations/{options.StationCode}/user-station-assignments", jsonOptions, options.PageSize);
total += await RestoreStationScopedAsync<PrintTemplateProfile>(db, http, "api/backup-export/print-template-profiles", jsonOptions, options.PageSize);

Console.WriteLine($"Restore completed. Upserted records: {total}");
return 0;

static async Task<int> RestoreStationScopedAsync<TEntity>(
    StationDbContext db,
    HttpClient http,
    string endpoint,
    JsonSerializerOptions jsonOptions,
    int pageSize) where TEntity : class
{
    var skip = 0;
    var total = 0;
    while (true)
    {
        var separator = endpoint.Contains('?') ? '&' : '?';
        var page = await http.GetFromJsonAsync<List<TEntity>>(
            $"{endpoint}{separator}skip={skip}&take={pageSize}",
            jsonOptions) ?? [];

        if (page.Count == 0)
        {
            break;
        }

        foreach (var item in page)
        {
            await UpsertAsync(db, item);
            total++;
        }

        await db.SaveChangesAsync();
        Console.WriteLine($"  {typeof(TEntity).Name}: restored {total}");

        if (page.Count < pageSize)
        {
            break;
        }

        skip += pageSize;
    }

    return total;
}

static async Task UpsertAsync<TEntity>(StationDbContext db, TEntity incoming) where TEntity : class
{
    var key = db.Model.FindEntityType(typeof(TEntity))?.FindPrimaryKey()
        ?? throw new InvalidOperationException($"No primary key metadata for {typeof(TEntity).Name}.");
    var keyValues = key.Properties
        .Select(p => p.PropertyInfo?.GetValue(incoming)
            ?? throw new InvalidOperationException($"Could not read key {p.Name} for {typeof(TEntity).Name}."))
        .ToArray();

    var existing = await db.Set<TEntity>().FindAsync(keyValues);
    if (existing == null)
    {
        await db.Set<TEntity>().AddAsync(incoming);
        return;
    }

    db.Entry(existing).CurrentValues.SetValues(incoming);
}

static Uri EnsureTrailingSlash(Uri uri)
    => uri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal) ? uri : new Uri($"{uri.AbsoluteUri}/");

internal sealed record RestoreOptions(
    string BackupApiUrl,
    string ApiKey,
    string StationCode,
    string LocalConnectionString,
    bool Execute,
    int PageSize)
{
    public bool IsValid(out string error)
    {
        error = string.Empty;
        if (string.IsNullOrWhiteSpace(BackupApiUrl) || !Uri.TryCreate(BackupApiUrl, UriKind.Absolute, out _))
        {
            error = "--backup-api-url is required and must be an absolute URL.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            error = "--api-key is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(StationCode))
        {
            error = "--station-code is required.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(LocalConnectionString))
        {
            error = "--local-connection is required.";
            return false;
        }

        return true;
    }

    public static RestoreOptions Parse(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var execute = false;
        for (var i = 0; i < args.Length; i++)
        {
            var arg = args[i];
            if (string.Equals(arg, "--execute", StringComparison.OrdinalIgnoreCase))
            {
                execute = true;
                continue;
            }

            if (!arg.StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
            {
                continue;
            }

            values[arg[2..]] = args[++i];
        }

        var pageSize = values.TryGetValue("page-size", out var rawPageSize) && int.TryParse(rawPageSize, out var parsed)
            ? Math.Clamp(parsed, 1, 1000)
            : 500;

        return new RestoreOptions(
            values.GetValueOrDefault("backup-api-url") ?? string.Empty,
            values.GetValueOrDefault("api-key") ?? string.Empty,
            (values.GetValueOrDefault("station-code") ?? string.Empty).Trim().ToUpperInvariant(),
            values.GetValueOrDefault("local-connection") ?? string.Empty,
            execute,
            pageSize);
    }

    public static void PrintUsage()
    {
        Console.WriteLine("""
Usage:
  StationApp.BackupRestoreTool --backup-api-url https://backup-domain/ --api-key <key> --station-code QN02 --local-connection "<sql connection>" [--execute]

Notes:
  Without --execute, the tool only checks health and prints backup summary.
""");
    }
}
