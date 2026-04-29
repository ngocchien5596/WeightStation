using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using StationApp.Application.Interfaces;
using StationApp.Device.Abstractions;
using StationApp.Device.Implementations;
using StationApp.UI.Services;

namespace StationApp.UI.ViewModels.Settings
{
    public partial class ScaleDeviceConfigViewModel : ObservableObject
    {
        private readonly IServiceScopeFactory _scopeFactory;

        public ScaleDeviceConfigViewModel(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
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

        [ObservableProperty] private string _sampleRawFrame = "02 004560";
        [ObservableProperty] private string _parsedStringResult = string.Empty;
        [ObservableProperty] private string _parsedWeightResult = string.Empty;

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

        [RelayCommand]
        private async Task SaveAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAppConfigRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();

            try
            {
                await repo.SetValueAsync("device_com_port", ComPort.Trim(), CancellationToken.None);
                await repo.SetValueAsync("device_baudrate", Baudrate.Trim(), CancellationToken.None);
                await repo.SetValueAsync("device_parser_type", ParserType.Trim(), CancellationToken.None);
                await repo.SetValueAsync("device_frame_end_char", FrameEndChar.Trim(), CancellationToken.None);
                await repo.SetValueAsync("weight_substring_start", WeightSubstringStart.Trim(), CancellationToken.None);
                await repo.SetValueAsync("weight_substring_length", WeightSubstringLength.Trim(), CancellationToken.None);

                await uow.SaveChangesAsync(CancellationToken.None);

                // Update runtime live parser configuration
                try
                {
                    var parser = scope.ServiceProvider.GetService<IWeightFrameParser>() as YaohuaWeightFrameParser;
                    if (parser != null)
                    {
                        if (int.TryParse(WeightSubstringStart, out var startVal)) parser.WeightSubstringStart = startVal;
                        if (int.TryParse(WeightSubstringLength, out var lenVal)) parser.WeightSubstringLength = lenVal;
                    }
                }
                catch { /* Swallow runtime casting failures safely */ }

                await dialogService.ShowInfoAsync("Thông báo", "Lưu tham số thiết bị thành công!");
            }
            catch (Exception ex)
            {
                await dialogService.ShowErrorAsync("Lỗi hệ thống", $"Lỗi khi lưu cấu hình: {ex.Message}");
            }
        }

        [RelayCommand]
        private async Task TestParseAsync()
        {
            using var scope = _scopeFactory.CreateScope();
            var dialogService = scope.ServiceProvider.GetRequiredService<IDialogService>();

            if (string.IsNullOrEmpty(SampleRawFrame))
            {
                await dialogService.ShowWarningAsync("Lỗi", "Vui lòng nhập chuỗi raw frame mẫu để kiểm tra!");
                return;
            }

            if (!int.TryParse(WeightSubstringStart, out var start) || start < 0)
            {
                await dialogService.ShowWarningAsync("Lỗi", "Vị trí bắt đầu cắt chuỗi không hợp lệ!");
                return;
            }

            if (!int.TryParse(WeightSubstringLength, out var length) || length <= 0)
            {
                await dialogService.ShowWarningAsync("Lỗi", "Độ dài cắt chuỗi không hợp lệ!");
                return;
            }

            try
            {
                if (SampleRawFrame.Length < start + length)
                {
                    await dialogService.ShowErrorAsync("Lỗi", $"Chuỗi raw frame quá ngắn ({SampleRawFrame.Length} ký tự) so với cấu hình cắt chuỗi (yêu cầu ít nhất {start + length} ký tự)!");
                    return;
                }

                var sliced = SampleRawFrame.Substring(start, length);
                ParsedStringResult = sliced;

                // Strip non-numeric chars for parse testing
                var numericOnly = new System.Text.StringBuilder();
                foreach (var c in sliced)
                {
                    if (char.IsDigit(c)) numericOnly.Append(c);
                }

                if (int.TryParse(numericOnly.ToString(), out var weightVal))
                {
                    ParsedWeightResult = weightVal.ToString();
                }
                else
                {
                    ParsedWeightResult = "0 (Lỗi định dạng số)";
                }
            }
            catch (Exception ex)
            {
                await dialogService.ShowErrorAsync("Lỗi", $"Lỗi xử lý cắt chuỗi: {ex.Message}");
            }
        }
    }
}
