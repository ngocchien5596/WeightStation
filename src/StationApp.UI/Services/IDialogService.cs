using System.Threading.Tasks;

namespace StationApp.UI.Services;

public interface IDialogService
{
    Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "Đồng ý", string cancelText = "Hủy");
    Task ShowWarningAsync(string title, string message);
    Task ShowErrorAsync(string title, string message);
    Task ShowInfoAsync(string title, string message);
    Task<TResult?> ShowCustomDialogAsync<TViewModel, TResult>(TViewModel viewModel) where TViewModel : class;
}
