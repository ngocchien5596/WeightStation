using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace StationApp.UI.ViewModels.Dialogs;

public enum DialogType
{
    Info,
    Confirm,
    Warning,
    Error
}

public partial class CustomDialogViewModel : ObservableObject
{
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _message = string.Empty;

    [ObservableProperty]
    private string _confirmText = "Đồng ý";

    [ObservableProperty]
    private string _cancelText = "Hủy";

    [ObservableProperty]
    private bool _isCancelVisible = true;

    [ObservableProperty]
    private DialogType _dialogType = DialogType.Info;

    [ObservableProperty]
    private string _iconGeometry = "M12,2A10,10 0 1,0 22,12A10,10 0 0,0 12,2M12,4A8,8 0 1,1 4,12A8,8 0 0,1 12,4M11,10H13V16H11V10M11,8H13V6H11V8Z"; // Default Info Icon

    [ObservableProperty]
    private string _iconColor = "#203B73"; // Default SidebarBrush color

    public bool DialogResult { get; private set; }

    public CustomDialogViewModel(string title, string message, DialogType dialogType, string confirmText = "Đồng ý", string cancelText = "Hủy")
    {
        Title = title;
        Message = message;
        DialogType = dialogType;
        ConfirmText = confirmText;
        CancelText = cancelText;

        ConfigureType(dialogType);
    }

    private void ConfigureType(DialogType type)
    {
        switch (type)
        {
            case DialogType.Info:
                IsCancelVisible = false;
                IconGeometry = "M11,9H13V11H11V9M11,13H13V17H11V13M12,2A10,10 0 1,0 22,12A10,10 0 0,0 12,2M12,20A8,8 0 1,1 20,12A8,8 0 0,1 12,20Z"; // Info Geometry
                IconColor = "#203B73";
                break;
            case DialogType.Confirm:
                IsCancelVisible = true;
                IconGeometry = "M12,2C6.48,2 2,6.48 2,12C2,17.52 6.48,22 12,22C17.52,22 22,17.52 22,12C22,6.48 17.52,2 12,2M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M15.59,7L17,8.41L10,15.41L7,12.41L8.41,11L10,12.58L15.59,7Z"; // Check Circle
                IconColor = "#2ECC71";
                break;
            case DialogType.Warning:
                IsCancelVisible = true;
                IconGeometry = "M12,2L1,21H23L12,2M12,6L19.53,19H4.47L12,6M11,10V14H13V10H11M11,16V18H13V16H11Z"; // Alert Geometry
                IconColor = "#F1C40F";
                break;
            case DialogType.Error:
                IsCancelVisible = false;
                IconGeometry = "M12,2C6.47,2 2,6.47 2,12C2,17.53 6.47,22 12,22C17.53,22 22,17.53 22,12C22,6.47 17.53,2 12,2M12,20C7.59,20 4,16.41 4,12C4,7.59 7.59,4 12,4C16.41,4 20,7.59 20,12C20,16.41 16.41,20 12,20M15.59,7L17,8.41L13.41,12L17,15.59L15.59,17L12,13.41L8.41,17L7,15.59L10.59,12L7,8.41L8.41,7L12,10.59L15.59,7Z"; // Close Circle
                IconColor = "#E74C3C";
                break;
        }
    }

    [RelayCommand]
    private void Confirm()
    {
        DialogResult = true;
        CloseRequested?.Invoke(this, true);
    }

    [RelayCommand]
    private void Cancel()
    {
        DialogResult = false;
        CloseRequested?.Invoke(this, false);
    }

    public event System.EventHandler<bool>? CloseRequested;
}
