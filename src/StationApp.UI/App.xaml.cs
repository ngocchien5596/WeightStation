using System.Net.Http;
using System.Text;
using System.Windows;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using StationApp.Application.Interfaces;
using StationApp.Application.Printing;
using StationApp.Application.Services;
using StationApp.Application.UseCases;
using StationApp.Application.UseCases.MasterData;
using StationApp.Device.Abstractions;
using StationApp.Device.Implementations;
using StationApp.Infrastructure.Persistence;
using StationApp.Infrastructure.Repositories;
using StationApp.Infrastructure.Services;
using StationApp.Sync.Services;
using StationApp.UI.Printing;
using StationApp.UI.Resources;
using StationApp.UI.Services;
using StationApp.UI.ViewModels;
using StationApp.UI.Views;

namespace StationApp.UI;

public partial class App : System.Windows.Application
{
    private IHost? _host;
    private bool _isInitialized;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DispatcherUnhandledException += (s, args) =>
        {
            Log.Fatal(args.Exception, "Unhandled UI Exception");
            MessageBox.Show(string.Format(UiText.Startup.UiExceptionFormat, args.Exception.Message), UiText.Startup.UiExceptionTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            args.Handled = true;
            Shutdown(1);
        };

        AppDomain.CurrentDomain.UnhandledException += (s, args) =>
        {
            var ex = args.ExceptionObject as Exception;
            Log.Fatal(ex, "Unhandled AppDomain Exception");
            if (args.IsTerminating)
            {
                MessageBox.Show(string.Format(UiText.Startup.FatalExceptionFormat, ex?.Message), UiText.Startup.UiExceptionTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        InitializeAppAsync();
    }

    private async void InitializeAppAsync()
    {
        using (Helpers.PerformanceLogger.Track("App Startup"))
        {
            try
            {
                if (!_isInitialized)
                {
                    await BuildHostAsync();
                    await PrepareInfrastructureAsync();
                    await _host!.StartAsync();
                    await RunStartupChecksAsync();
                    _isInitialized = true;
                }

                await ShowLoginFlowAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Startup Error:\n{ex.Message}\n\n{ex.InnerException?.Message}",
                    "Station App - Fatal Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                Shutdown(1);
            }
        }
    }

    public async Task LogoutAsync()
    {
        if (_host == null)
        {
            Shutdown(1);
            return;
        }

        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _host.Services.GetRequiredService<ICurrentUserContext>().SignOut();

        var currentMainWindow = MainWindow;
        MainWindow = null;
        currentMainWindow?.Close();

        await ShowLoginFlowAsync();
    }

    private Task BuildHostAsync()
    {
        using (Helpers.PerformanceLogger.Track("DI Container Build"))
        {
            _host = Host.CreateDefaultBuilder()
                .UseSerilog((context, services, configuration) =>
                {
                    var isDiag = context.Configuration.GetValue<bool>("DiagnosticMode");
                    configuration
                        .MinimumLevel.Debug()
                        .MinimumLevel.Override("Microsoft", isDiag ? LogEventLevel.Information : LogEventLevel.Warning)
                        .Enrich.FromLogContext()
                        .WriteTo.Console()
                        .WriteTo.File("logs/station.txt", rollingInterval: RollingInterval.Day);
                })
                .ConfigureServices((context, services) =>
                {
                    var connStr = context.Configuration.GetConnectionString("DefaultConnection")
                        ?? "Server=.;Database=StationAppLocal;Trusted_Connection=True;Encrypt=False;TrustServerCertificate=True;";

                    services.AddDbContext<StationDbContext>(options =>
                        options.UseSqlServer(
                            connStr,
                            sql => sql.EnableRetryOnFailure()
                                      .UseCompatibilityLevel(120)));

                    services.AddScoped<ITicketRepository, TicketRepository>();
                    services.AddScoped<IWeighTicketRepository, TicketRepository>();
                    services.AddScoped<IVehicleRegistrationRepository, VehicleRegistrationRepository>();
                    services.AddScoped<IWeighingSessionRepository, WeighingSessionRepository>();
                    services.AddScoped<ISyncOutboxRepository, SyncOutboxRepository>();
                    services.AddScoped<IAuditLogRepository, AuditLogRepository>();
                    services.AddScoped<IAppConfigRepository, AppConfigRepository>();
                    services.AddScoped<IDeviceConfigRepository, DeviceConfigRepository>();
                    services.AddScoped<IUserRepository, UserRepository>();
                    services.AddScoped<IVehicleRepository, VehicleRepository>();
                    services.AddScoped<ICustomerRepository, CustomerRepository>();
                    services.AddScoped<IProductRepository, ProductRepository>();
                    services.AddScoped<IDeliveryTicketRepository, DeliveryTicketRepository>();
                    services.AddScoped<IUnitOfWork, EfUnitOfWork>();

                    services.AddScoped<ITicketNumberGenerator, TicketNumberGenerator>();
                    services.AddScoped<IDeliveryNumberGenerator, DeliveryNumberGenerator>();
                    services.AddScoped<IUserPasswordHasher, BcryptUserPasswordHasher>();
                    services.AddSingleton<IAppVersionProvider, AppVersionProvider>();
                    services.AddSingleton<IClock, SystemClock>();
                    services.AddSingleton<ICurrentUserContext, CurrentUserContext>();
                    services.AddScoped<IToleranceProvider, ToleranceProvider>();
                    services.AddScoped<IAuditService, AuditService>();
                    services.AddScoped<ISyncPayloadFactory, SyncPayloadFactory>();
                    services.AddSingleton<WeighingSessionOverweightService>();
                    services.AddSingleton<WeighingSessionTicketSyncService>();
                    services.AddSingleton<PrintOverlayRenderer>();
                    services.AddScoped<IWeighTicketPrintComposer, WeighTicketPrintComposer>();
                    services.AddScoped<IDeliveryTicketPrintComposer, DeliveryTicketPrintComposer>();
                    services.AddScoped<IPrintTemplateProvider, PrintTemplateProvider>();
                    services.AddSingleton<IPrinterDiscoveryService, PrinterDiscoveryService>();
                    services.AddScoped<IPrintService, WpfPrintService>();
                    services.AddSingleton<IToastService, WpfToastService>();
                    services.AddSingleton<IDialogService, WpfDialogService>();

                    services.AddScoped<CreateTicketUseCase>();
                    services.AddScoped<CaptureWeight1UseCase>();
                    services.AddScoped<CaptureWeight2UseCase>();
                    services.AddScoped<SplitOverweightTicketUseCase>();
                    services.AddScoped<CompleteOverweightTicketWithoutSplitUseCase>();
                    services.AddScoped<CompleteTicketUseCase>();
                    services.AddScoped<CancelTicketUseCase>();
                    services.AddScoped<SyncMasterDataFromInboundTicketUseCase>();
                    services.AddScoped<SearchVehicleSuggestionsUseCase>();
                    services.AddScoped<SearchVehicleMoocOptionsUseCase>();
                    services.AddScoped<SearchCustomerSuggestionsUseCase>();
                    services.AddScoped<SearchProductSuggestionsUseCase>();
                    services.AddScoped<EnsureInboundMasterDataUseCase>();
                    services.AddScoped<IAutocompleteService, AutocompleteService>();
                    services.AddScoped<GetWeightViewTicketsUseCase>();
                    services.AddScoped<GetRelatedTicketsUseCase>();
                    services.AddScoped<EnsurePrimaryDeliveryTicketUseCase>();
                    services.AddScoped<SearchUsersUseCase>();
                    services.AddScoped<CreateUserAccountUseCase>();
                    services.AddScoped<UpdateUserAccountUseCase>();
                    services.AddScoped<SetUserActiveStatusUseCase>();
                    services.AddScoped<ResetUserPasswordUseCase>();
                    services.AddScoped<UpdateSystemSettingsUseCase>();
                    services.AddScoped<UpdateScaleDeviceSettingsUseCase>();
                    services.AddScoped<LoginUseCase>();
                    services.AddScoped<ConfirmEnterWeighingUseCase>();
                    services.AddScoped<CreateInboundRegistrationUseCase>();
                    services.AddScoped<UpdateIncomingRegistrationUseCase>();
                    services.AddScoped<CreateWeighingSessionUseCase>();
                    services.AddScoped<CaptureSessionWeight1UseCase>();
                    services.AddScoped<CaptureSessionWeight2UseCase>();
                    services.AddScoped<AllocateWeighingSessionUseCase>();
                    services.AddScoped<MarkRegistrationsNoLoadUseCase>();
                    services.AddScoped<PreviewWeighingSessionOverweightSplitUseCase>();
                    services.AddScoped<ResolveWeighingSessionOverweightSplitUseCase>();
                    services.AddScoped<ResolveWeighingSessionOverweightNoSplitUseCase>();
                    services.AddScoped<MarkWeighingSessionNoLoadUseCase>();
                    services.AddScoped<CompleteWeighingSessionUseCase>();
                    services.AddScoped<CancelWeighingSessionUseCase>();
                    services.AddScoped<GetWeighingSessionsUseCase>();

                    services.AddTransient<IncomingVehicleListViewModel>();
                    services.AddTransient<OutgoingVehicleListViewModel>();

                    services.AddSingleton<StabilityDetector>();
                    services.AddSingleton<IWeightFrameParser>(_ => new YaohuaWeightFrameParser('\r')
                    {
                        WeightSubstringStart = 0,
                        WeightSubstringLength = 7
                    });
                    services.AddSingleton<IScaleDevice>(sp =>
                    {
                        var parser = sp.GetRequiredService<IWeightFrameParser>();
                        var stability = sp.GetRequiredService<StabilityDetector>();
                        var logger = sp.GetRequiredService<ILogger<SerialScaleDevice>>();
                        return new SerialScaleDevice(
                            comPort: "COM6",
                            baudRate: 9600,
                            parser: parser,
                            stabilityDetector: stability,
                            logger: logger,
                            configurationProvider: async ct =>
                            {
                                using var scope = sp.CreateScope();
                                var appRepo = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();

                                var comPort = await appRepo.GetValueAsync("device_com_port", ct);
                                var baudRateRaw = await appRepo.GetValueAsync("device_baudrate", ct);
                                var startRaw = await appRepo.GetValueAsync("weight_substring_start", ct);
                                var lengthRaw = await appRepo.GetValueAsync("weight_substring_length", ct);

                                var baudRate = int.TryParse(baudRateRaw, out var parsedBaudRate) && parsedBaudRate > 0
                                    ? parsedBaudRate
                                    : 9600;

                                int? start = int.TryParse(startRaw, out var parsedStart) ? parsedStart : null;
                                int? length = int.TryParse(lengthRaw, out var parsedLength) ? parsedLength : null;

                                return new SerialScaleDeviceConfiguration(comPort, baudRate, start, length);
                            });
                    });

                    services.AddTransient<ApiKeyDelegatingHandler>();
                    services.AddHttpClient<ICentralApiClient, CentralApiClient>(client =>
                    {
                        client.Timeout = TimeSpan.FromSeconds(30);
                    }).AddHttpMessageHandler<ApiKeyDelegatingHandler>();

                    services.AddHostedService<SyncOutboxWorker>();
                    services.AddHostedService<InboundMasterDataWorker>();
                    services.AddHostedService<VehicleRegistrationInboundProcessor>();

                    services.AddTransient<LoginViewModel>();
                    services.AddTransient<MainViewModel>();
                    services.AddTransient<WeighingViewModel>();
                    services.AddTransient<DashboardViewModel>();
                    services.AddTransient<TicketListViewModel>();
                    services.AddTransient<DiagnosticsViewModel>();
                    services.AddTransient<SettingsViewModel>();

                    services.AddSingleton<StartupChecks>();
                })
                .Build();
        }

        return Task.CompletedTask;
    }

    private async Task PrepareInfrastructureAsync()
    {
        using (Helpers.PerformanceLogger.Track("DB Connection Duration"))
        {
            using var scope = _host!.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StationDbContext>();
            var loggerFactory = scope.ServiceProvider.GetService<ILoggerFactory>();
            await StationDatabaseInitializer.InitializeAsync(db, loggerFactory, CancellationToken.None);
        }
    }

    private async Task RunStartupChecksAsync()
    {
        using (Helpers.PerformanceLogger.Track("Startup Health Checks Duration"))
        {
            var startupChecks = _host!.Services.GetRequiredService<StartupChecks>();
            await startupChecks.RunAllAsync(CancellationToken.None);

            if (startupChecks.HasCriticalFailure)
            {
                var sb = new StringBuilder(UiText.Startup.StartupChecksHeader);
                foreach (var r in startupChecks.Results.Where(r => !r.IsOk))
                {
                    sb.AppendLine($"[X] {r.Name}: {r.Message}");
                }

                sb.AppendLine(UiText.Startup.StartupChecksFooter);
                MessageBox.Show(sb.ToString(), UiText.Startup.StartupChecksTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private Task ShowLoginFlowAsync()
    {
        if (_host == null)
        {
            Shutdown(1);
            return Task.CompletedTask;
        }

        _host.Services.GetRequiredService<ICurrentUserContext>().SignOut();
        ShutdownMode = ShutdownMode.OnExplicitShutdown;

        var loginWindow = new LoginWindow
        {
            DataContext = _host.Services.GetRequiredService<LoginViewModel>()
        };

        var result = loginWindow.ShowDialog();
        if (result == true)
        {
            ShowMainShell();
        }
        else
        {
            Shutdown();
        }

        return Task.CompletedTask;
    }

    private void ShowMainShell()
    {
        var mainWindow = new MainWindow
        {
            DataContext = _host!.Services.GetRequiredService<MainViewModel>()
        };

        MainWindow = mainWindow;
        ShutdownMode = ShutdownMode.OnMainWindowClose;
        mainWindow.Show();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host != null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}

internal class ApiKeyDelegatingHandler : DelegatingHandler
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ApiKeyDelegatingHandler(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var config = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
            var apiKey = await config.GetValueAsync("central_api_key", cancellationToken);
            if (!string.IsNullOrEmpty(apiKey))
            {
                request.Headers.Remove("X-Api-Key");
                request.Headers.Add("X-Api-Key", apiKey);
            }
        }
        catch
        {
            // Ignore and let the request continue without the header.
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
