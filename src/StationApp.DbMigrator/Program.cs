using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StationApp.Infrastructure.Persistence;

var argsMap = ParseArgs(args);
var configPath = argsMap.GetValueOrDefault("config");
var connectionOverride = argsMap.GetValueOrDefault("connection");

var host = Host.CreateDefaultBuilder()
    .ConfigureAppConfiguration((context, config) =>
    {
        config.Sources.Clear();
        if (!string.IsNullOrWhiteSpace(configPath))
        {
            config.AddJsonFile(configPath, optional: false, reloadOnChange: false);
        }
        else
        {
            config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: false);
        }

        config.AddEnvironmentVariables(prefix: "STATIONAPP_");
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddSimpleConsole();
    })
    .ConfigureServices((context, services) =>
    {
        var connectionString = connectionOverride
            ?? context.Configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Missing connection string 'DefaultConnection'.");

        services.AddDbContext<StationDbContext>(options =>
            options.UseSqlServer(
                connectionString,
                sql => sql.EnableRetryOnFailure()
                          .UseCompatibilityLevel(120)));
    })
    .Build();

using var scope = host.Services.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<StationDbContext>();
var loggerFactory = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILoggerFactory>();
await StationDatabaseInitializer.InitializeAsync(
    db,
    loggerFactory,
    CancellationToken.None,
    deploySqlObjects: true);

static Dictionary<string, string> ParseArgs(string[] argv)
{
    var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
    for (var i = 0; i < argv.Length; i++)
    {
        if (!argv[i].StartsWith("--", StringComparison.Ordinal))
        {
            continue;
        }

        var key = argv[i][2..];
        var value = i + 1 < argv.Length ? argv[i + 1] : string.Empty;
        if (!value.StartsWith("--", StringComparison.Ordinal))
        {
            result[key] = value;
            i++;
        }
        else
        {
            result[key] = "true";
        }
    }

    return result;
}
