using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Security;
using StationApp.Application.UseCases;
using StationApp.Device.Abstractions;
using StationApp.Device.Implementations;

namespace StationApp.UI.ViewModels.Settings;

public partial class ScaleDeviceConfigViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ICurrentUserContext _currentUserContext;

    public ScaleDeviceConfigViewModel(IServiceScopeFactory scopeFactory, ICurrentUserContext currentUserContext)
    {
        _scopeFactory = scopeFactory;
        _currentUserContext = currentUserContext;
    }

    [ObservableProperty] private string _comPort = "COM6";
    [ObservableProperty] private string _baudrate = "9600";
    [ObservableProperty] private string _parity = "None";
    [ObservableProperty] private string _dataBits = "8";
    [ObservableProperty] private string _stopBits = "One";
    [ObservableProperty] private string _parserType = "DEFAULT";
    [ObservableProperty] private string _frameEndChar = "3";
    [ObservableProperty] private string _stableCycles = "3";
    [ObservableProperty] private string _weightSubstringStart = "0";
    [ObservableProperty] private string _weightSubstringLength = "7";
    [ObservableProperty] private string _sampleRawFrame = "\u000102 004560\u0002\u0003";
    [ObservableProperty] private string _parsedStringResult = string.Empty;
    [ObservableProperty] private string _parsedWeightResult = string.Empty;

    public bool CanManageDeviceConfiguration => StationAuthorization.CanManageDeviceConfiguration(_currentUserContext.RoleCode);

    public async Task LoadAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();

        ComPort = await repo.GetValueAsync("device_com_port", CancellationToken.None) ?? "COM6";
        Baudrate = await repo.GetValueAsync("device_baudrate", CancellationToken.None) ?? "9600";
        ParserType = await repo.GetValueAsync("device_parser_type", CancellationToken.None) ?? "DEFAULT";
        FrameEndChar = await repo.GetValueAsync("device_frame_end_char", CancellationToken.None) ?? "3";
        WeightSubstringStart = await repo.GetValueAsync("weight_substring_start", CancellationToken.None) ?? "0";
        WeightSubstringLength = await repo.GetValueAsync("weight_substring_length", CancellationToken.None) ?? "7";
    }

    [RelayCommand(CanExecute = nameof(CanManageDeviceConfiguration))]
    private async Task SaveAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<Services.IDialogService>();

        try
        {
            var useCase = scope.ServiceProvider.GetRequiredService<UpdateScaleDeviceSettingsUseCase>();
            await useCase.ExecuteAsync(
                new UpdateScaleDeviceSettingsRequest(
                    ComPort,
                    Baudrate,
                    ParserType,
                    FrameEndChar,
                    WeightSubstringStart,
                    WeightSubstringLength),
                CancellationToken.None);

            try
            {
                var parser = scope.ServiceProvider.GetService<IWeightFrameParser>() as YaohuaWeightFrameParser;
                if (parser != null)
                {
                    if (int.TryParse(WeightSubstringStart, out var startVal))
                    {
                        parser.WeightSubstringStart = startVal;
                    }

                    if (int.TryParse(WeightSubstringLength, out var lenVal))
                    {
                        parser.WeightSubstringLength = lenVal;
                    }
                }
            }
            catch
            {
            }

            await dialogService.ShowInfoAsync("Thong bao", "Luu tham so thiet bi thanh cong!");
        }
        catch (Exception ex)
        {
            await dialogService.ShowErrorAsync("Loi he thong", $"Loi khi luu cau hinh: {ex.Message}");
        }
    }

    [RelayCommand]
    private async Task TestParseAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var dialogService = scope.ServiceProvider.GetRequiredService<Services.IDialogService>();

        if (string.IsNullOrEmpty(SampleRawFrame))
        {
            await dialogService.ShowWarningAsync("Loi", "Vui long nhap chuoi raw frame mau de kiem tra!");
            return;
        }

        if (!int.TryParse(WeightSubstringStart, out var start) || start < 0)
        {
            await dialogService.ShowWarningAsync("Loi", "Vi tri bat dau cat chuoi khong hop le!");
            return;
        }

        if (!int.TryParse(WeightSubstringLength, out var length) || length <= 0)
        {
            await dialogService.ShowWarningAsync("Loi", "Do dai cat chuoi khong hop le!");
            return;
        }

        try
        {
            if (SampleRawFrame.Length < start + length)
            {
                await dialogService.ShowErrorAsync("Loi", $"Chuoi raw frame qua ngan ({SampleRawFrame.Length} ky tu) so voi cau hinh cat chuoi (yeu cau it nhat {start + length} ky tu)!");
                return;
            }

            var sliced = SampleRawFrame.Substring(start, length);
            ParsedStringResult = sliced;

            var numericOnly = new System.Text.StringBuilder();
            foreach (var c in sliced)
            {
                if (char.IsDigit(c))
                {
                    numericOnly.Append(c);
                }
            }

            ParsedWeightResult = int.TryParse(numericOnly.ToString(), out var weightVal)
                ? weightVal.ToString()
                : "0 (Loi dinh dang so)";
        }
        catch (Exception ex)
        {
            await dialogService.ShowErrorAsync("Loi", $"Loi xu ly cat chuoi: {ex.Message}");
        }
    }
}
