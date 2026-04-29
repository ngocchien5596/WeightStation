namespace StationApp.UI.Services;

public interface IToastService
{
    void ShowSuccess(string message);
    void ShowWarning(string message);
    void ShowError(string message);
    void ShowInfo(string message);
}
