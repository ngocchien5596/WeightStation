using System.Collections.ObjectModel;
using System.IO.Ports;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Application.UseCases;
using StationApp.Device.Abstractions;
using StationApp.Device.Implementations;
using StationApp.Domain.Constants;

namespace StationApp.UI.ViewModels.Settings;

public partial class ScaleDeviceConfigViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentUserContext _currentUserContext;

    public ScaleDeviceConfigViewModel(IServiceScopeFactory scopeFactory, ICurrentUserContext currentUserContext)
    {
        _scopeFactory = scopeFactory;
        _currentUserContext = currentUserContext;

        AvailableBaudrates = new ObservableCollection<string>(["1200", "2400", "4800", "9600", "19200", "38400", "57600", "115200"]);
        AvailableParities = new ObservableCollection<string>(["None", "Even", "Odd", "Mark", "Space"]);
        AvailableDataBits = new ObservableCollection<string>(["7", "8"]);
        AvailableStopBits = new ObservableCollection<string>(["One", "Two"]);
        AvailableParserTypes = new ObservableCollection<string>([ScaleConnectionSettings.ParserTypeYaohua, ScaleConnectionSettings.ParserTypeDefault]);
        AvailableFrameEndChars = new ObservableCollection<string>(["CR", "LF", "ETX"]);
        AvailableStableCycles = new ObservableCollection<string>(["2", "3", "4", "5"]);
        AvailableSubstringStarts = new ObservableCollection<string>(["", "0", "7", "8", "9", "10"]);
        AvailableSubstringLengths = new ObservableCollection<string>(["", "7", "8", "9", "10"]);
    }

    [ObservableProperty] private ObservableCollection<string> _availablePorts = new();
    [ObservableProperty] private ObservableCollection<string> _availableBaudrates;
    [ObservableProperty] private ObservableCollection<string> _availableParities;
    [ObservableProperty] private ObservableCollection<string> _availableDataBits;
    [ObservableProperty] private ObservableCollection<string> _availableStopBits;
    [ObservableProperty] private ObservableCollection<string> _availableParserTypes;
    [ObservableProperty] private ObservableCollection<string> _availableFrameEndChars;
    [ObservableProperty] private ObservableCollection<string> _availableStableCycles;
    [ObservableProperty] private ObservableCollection<string> _availableSubstringStarts;
    [ObservableProperty] private ObservableCollection<string> _availableSubstringLengths;

    [ObservableProperty] private string _comPort = "COM6";
    [ObservableProperty] private string _baudrate = "9600";
    [ObservableProperty] private string _parity = "None";
    [ObservableProperty] private string _dataBits = "8";
    [ObservableProperty] private string _stopBits = "One";
    [ObservableProperty] private string _parserType = ScaleConnectionSettings.ParserTypeYaohua;
    [ObservableProperty] private string _frameEndChar = "CR";
    [ObservableProperty] private string _stableCycles = "3";
    [ObservableProperty] private string _weightSubstringStart = string.Empty;
    [ObservableProperty] private string _weightSubstringLength = string.Empty;
    [ObservableProperty] private string _sampleRawFrame = "ST,GS,+  00025350 kg\\r";
    [ObservableProperty] private string _parsedStringResult = string.Empty;
    [ObservableProperty] private string _parsedWeightResult = string.Empty;
    [ObservableProperty] private string _connectionTestResult = string.Empty;

    public bool CanManageDeviceConfiguration => StationAuthorization.CanManageDeviceConfiguration(_currentUserContext.RoleCode);

    [RelayCommand]
    public async Task LoadAsync()
    {
        RefreshAvailablePortsInternal();

        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();

        ComPort = await repo.GetValueAsync(AppConfigKeys.DeviceComPort, CancellationToken.None) ?? FirstOrDefault(AvailablePorts, "COM6");
        Baudrate = await repo.GetValueAsync(AppConfigKeys.DeviceBaudrate, CancellationToken.None) ?? "9600";
        Parity = await repo.GetValueAsync(AppConfigKeys.DeviceParity, CancellationToken.None) ?? "None";
        DataBits = await repo.GetValueAsync(AppConfigKeys.DeviceDataBits, CancellationToken.None) ?? "8";
        StopBits = await repo.GetValueAsync(AppConfigKeys.DeviceStopBits, CancellationToken.None) ?? "One";
        ParserType = await repo.GetValueAsync(AppConfigKeys.DeviceParserType, CancellationToken.None) ?? ScaleConnectionSettings.ParserTypeYaohua;
        FrameEndChar = await repo.GetValueAsync(AppConfigKeys.DeviceFrameEndChar, CancellationToken.None) ?? "CR";
        StableCycles = await repo.GetValueAsync(AppConfigKeys.DeviceStableCycles, CancellationToken.None) ?? "3";
        WeightSubstringStart = await repo.GetValueAsync(AppConfigKeys.WeightSubstringStart, CancellationToken.None) ?? string.Empty;
        WeightSubstringLength = await repo.GetValueAsync(AppConfigKeys.WeightSubstringLength, CancellationToken.None) ?? string.Empty;

        EnsureOption(AvailablePorts, ComPort);
        EnsureOption(AvailableBaudrates, Baudrate);
        EnsureOption(AvailableParities, Parity);
        EnsureOption(AvailableDataBits, DataBits);
        EnsureOption(AvailableStopBits, StopBits);
        EnsureOption(AvailableParserTypes, ParserType);
        EnsureOption(AvailableFrameEndChars, FrameEndChar);
        EnsureOption(AvailableStableCycles, StableCycles);
        EnsureOption(AvailableSubstringStarts, WeightSubstringStart);
        EnsureOption(AvailableSubstringLengths, WeightSubstringLength);
    }

    [RelayCommand(CanExecute = nameof(CanManageDeviceConfiguration))]
    private async Task SaveAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<Services.IDialogService>();

        if (string.IsNullOrWhiteSpace(ComPort))
        {
            await dialogService.ShowWarningAsync("Lỗi", "Vui lòng chọn cổng COM.");
            return;
        }

        try
        {
            var useCase = scope.ServiceProvider.GetRequiredService<UpdateScaleDeviceSettingsUseCase>();
            await useCase.ExecuteAsync(
                new UpdateScaleDeviceSettingsRequest(
                    ComPort.Trim(),
                    Baudrate.Trim(),
                    Parity.Trim(),
                    DataBits.Trim(),
                    StopBits.Trim(),
                    ParserType.Trim(),
                    FrameEndChar.Trim(),
                    StableCycles.Trim(),
                    WeightSubstringStart.Trim(),
                    WeightSubstringLength.Trim()),
                CancellationToken.None);

            await dialogService.ShowInfoAsync("Thông báo", "Lưu tham số thiết bị cân thành công.");
        }
        catch (Exception ex)
        {
            await dialogService.ShowErrorAsync("Lỗi hệ thống", $"Lỗi khi lưu cấu hình: {ex.Message}");
        }
    }

    [RelayCommand]
    private Task RefreshPortsAsync()
    {
        RefreshAvailablePortsInternal();
        if (string.IsNullOrWhiteSpace(ComPort))
        {
            ComPort = FirstOrDefault(AvailablePorts, "COM6");
        }

        return Task.CompletedTask;
    }

    [RelayCommand]
    private async Task TestParseAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<Services.IDialogService>();

        if (string.IsNullOrWhiteSpace(SampleRawFrame))
        {
            await dialogService.ShowWarningAsync("Lỗi", "Vui lòng nhập chuỗi raw frame mẫu để kiểm tra.");
            return;
        }

        try
        {
            var decodedRaw = DecodeDisplayText(SampleRawFrame);
            var start = TryGetOptionalNonNegativeInt(WeightSubstringStart);
            var length = TryGetOptionalNonNegativeInt(WeightSubstringLength);
            var parser = ScaleConnectionSettings.CreateParser(ParserType, FrameEndChar, start, length);

            ParsedStringResult = BuildPreviewSlice(decodedRaw, start, length);
            var parsed = parser.TryParse(decodedRaw, out var isStable);
            ParsedWeightResult = parsed.HasValue
                ? $"{parsed.Value:N0} kg | {(isStable ? "Ổn định" : "Chưa ổn định")}"
                : "Không parse được";
        }
        catch (Exception ex)
        {
            await dialogService.ShowErrorAsync("Lỗi", $"Lỗi khi kiểm tra parse: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task TestConnectionAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<Services.IDialogService>();

        if (string.IsNullOrWhiteSpace(ComPort))
        {
            await dialogService.ShowWarningAsync("Lỗi", "Vui lòng chọn cổng COM trước khi test.");
            return;
        }

        try
        {
            var parser = ScaleConnectionSettings.CreateParser(
                ParserType,
                FrameEndChar,
                TryGetOptionalNonNegativeInt(WeightSubstringStart),
                TryGetOptionalNonNegativeInt(WeightSubstringLength));

            var stabilityDetector = new StabilityDetector(requiredCycles: ScaleConnectionSettings.ResolveStableCycles(StableCycles));
            using var device = new SerialScaleDevice(
                ComPort.Trim(),
                ScaleConnectionSettings.ResolveBaudRate(Baudrate),
                parser,
                stabilityDetector,
                parity: ScaleConnectionSettings.ResolveParity(Parity),
                dataBits: ScaleConnectionSettings.ResolveDataBits(DataBits),
                stopBits: ScaleConnectionSettings.ResolveStopBits(StopBits));

            string? lastRaw = null;
            string? lastError = null;
            decimal? weight = null;
            bool isStable = false;
            var resultSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnWeightReceived(object? _, Device.Models.ScaleReading reading)
            {
                lastRaw = reading.RawPayload;
                weight = reading.Weight;
                isStable = reading.IsStable;
                resultSource.TrySetResult(true);
            }

            void OnDiagnosticsReceived(object? _, string raw)
            {
                lastRaw = raw;
            }

            void OnErrorOccurred(object? _, string error)
            {
                lastError = error;
                resultSource.TrySetResult(false);
            }

            device.WeightReceived += OnWeightReceived;
            device.DiagnosticsReceived += OnDiagnosticsReceived;
            device.ErrorOccurred += OnErrorOccurred;

            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await device.ConnectAsync(cts.Token);

                if (!device.IsConnected)
                {
                    ConnectionTestResult = "Không mở được cổng COM với bộ thông số hiện tại.";
                    await dialogService.ShowErrorAsync("Test kết nối", ConnectionTestResult);
                    return;
                }

                var completedTask = await Task.WhenAny(resultSource.Task, Task.Delay(TimeSpan.FromSeconds(5), cts.Token));
                if (completedTask == resultSource.Task && resultSource.Task.Result && weight.HasValue)
                {
                    ConnectionTestResult = $"Kết nối thành công. Đã nhận số cân {weight.Value:N0} kg ({(isStable ? "ổn định" : "chưa ổn định")}).";
                    await dialogService.ShowInfoAsync(
                        "Test kết nối",
                        $"{ConnectionTestResult}\n\nRaw frame:\n{ToDisplayText(lastRaw ?? string.Empty)}");
                    return;
                }

                if (!string.IsNullOrWhiteSpace(lastError))
                {
                    ConnectionTestResult = lastError;
                    await dialogService.ShowErrorAsync("Test kết nối", lastError);
                    return;
                }

                ConnectionTestResult = "Mở cổng COM thành công nhưng chưa nhận được dữ liệu từ đầu cân trong 5 giây.";
                await dialogService.ShowWarningAsync(
                    "Test kết nối",
                    $"{ConnectionTestResult}\n\nRaw gần nhất:\n{ToDisplayText(lastRaw ?? string.Empty)}");
            }
            finally
            {
                device.WeightReceived -= OnWeightReceived;
                device.DiagnosticsReceived -= OnDiagnosticsReceived;
                device.ErrorOccurred -= OnErrorOccurred;
                await device.DisconnectAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            ConnectionTestResult = ex.Message;
            await dialogService.ShowErrorAsync("Test kết nối", $"Không thể test kết nối: {ex.Message}");
        }
    }

    private void RefreshAvailablePortsInternal()
    {
        var ports = SerialPort.GetPortNames()
            .OrderBy(static port => port, StringComparer.OrdinalIgnoreCase)
            .ToList();

        AvailablePorts = new ObservableCollection<string>(ports);
        if (AvailablePorts.Count == 0)
        {
            AvailablePorts.Add("COM1");
        }
    }

    private static void EnsureOption(ObservableCollection<string> options, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !options.Contains(value))
        {
            options.Add(value);
        }
    }

    private static string FirstOrDefault(IEnumerable<string> values, string fallback)
    {
        return values.FirstOrDefault() ?? fallback;
    }

    private static int? TryGetOptionalNonNegativeInt(string? raw)
    {
        return int.TryParse(raw, out var value) && value >= 0 ? value : null;
    }

    private static string DecodeDisplayText(string raw)
    {
        return raw
            .Replace("\\r\\n", "\r\n", StringComparison.OrdinalIgnoreCase)
            .Replace("\\r", "\r", StringComparison.OrdinalIgnoreCase)
            .Replace("\\n", "\n", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToDisplayText(string raw)
    {
        return raw.Replace("\r", "\\r").Replace("\n", "\\n");
    }

    private static string BuildPreviewSlice(string raw, int? start, int? length)
    {
        if (!start.HasValue || !length.HasValue || length.Value <= 0)
        {
            return ToDisplayText(raw);
        }

        if (start.Value >= raw.Length)
        {
            return string.Empty;
        }

        var actualLength = Math.Min(length.Value, raw.Length - start.Value);
        return ToDisplayText(raw.Substring(start.Value, actualLength));
    }
}
