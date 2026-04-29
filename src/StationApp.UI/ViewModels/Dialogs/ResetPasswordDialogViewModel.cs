using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StationApp.UI.ViewModels.Dialogs;

public sealed partial class ResetPasswordDialogViewModel : ObservableObject
{
    [ObservableProperty] private string _title;
    [ObservableProperty] private string _username;
    [ObservableProperty] private string _newPassword = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private string? _validationMessage;

    public ResetPasswordDialogResult? DialogResultValue { get; private set; }
    public event EventHandler<bool>? CloseRequested;

    public ResetPasswordDialogViewModel(string username)
    {
        _title = "Reset mật khẩu";
        _username = username;
    }

    [RelayCommand]
    private void Confirm()
    {
        ValidationMessage = Validate();
        if (ValidationMessage != null)
        {
            return;
        }

        DialogResultValue = new ResetPasswordDialogResult(NewPassword, ConfirmPassword);
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResultValue = null;
        CloseRequested?.Invoke(this, false);
    }

    private string? Validate()
    {
        if (string.IsNullOrWhiteSpace(NewPassword))
        {
            return "Mật khẩu mới là bắt buộc.";
        }

        if (string.IsNullOrWhiteSpace(ConfirmPassword))
        {
            return "Xác nhận mật khẩu là bắt buộc.";
        }

        if (NewPassword.Length < 8)
        {
            return "Mật khẩu phải có ít nhất 8 ký tự.";
        }

        if (!string.Equals(NewPassword, ConfirmPassword, StringComparison.Ordinal))
        {
            return "Mật khẩu xác nhận không khớp.";
        }

        return null;
    }
}

public sealed record ResetPasswordDialogResult(
    string NewPassword,
    string ConfirmPassword
);
