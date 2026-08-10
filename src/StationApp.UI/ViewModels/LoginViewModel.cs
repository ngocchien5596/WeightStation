using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases;

namespace StationApp.UI.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LoginViewModel>? _logger;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _username = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private string _password = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(LoginCommand))]
    private bool _isBusy;

    [ObservableProperty]
    private bool _isPasswordVisible;

    public string AppTitle => "Station App";
    public string Subtitle => "Đăng nhập để sử dụng hệ thống";
    public string VersionText { get; }
    public bool IsInputEnabled => !IsBusy;
    public string LoginButtonText => IsBusy ? "Đang đăng nhập..." : "Đăng nhập";
    public bool CanLogin => !IsBusy && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
    public event EventHandler<bool>? CloseRequested;

    public LoginViewModel(
        IServiceScopeFactory scopeFactory,
        IAppVersionProvider versionProvider,
        ILogger<LoginViewModel>? logger = null)
    {
        _scopeFactory = scopeFactory;
        VersionText = $"Phiên bản {versionProvider.GetVersion()}";
        _logger = logger;
    }

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(IsInputEnabled));
        OnPropertyChanged(nameof(LoginButtonText));
    }

    [RelayCommand(CanExecute = nameof(CanLogin))]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        IsBusy = true;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var loginUseCase = scope.ServiceProvider.GetRequiredService<LoginUseCase>();
            var result = await loginUseCase.ExecuteAsync(new LoginRequest(Username, Password), CancellationToken.None);
            if (!result.Success)
            {
                Password = string.Empty;
                ErrorMessage = result.ErrorMessage ?? "Không thể đăng nhập. Vui lòng thử lại.";
                return;
            }

            CloseRequested?.Invoke(this, true);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Login failed unexpectedly.");
            Password = string.Empty;
            ErrorMessage = $"Không thể đăng nhập: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
