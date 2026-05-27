using System.Threading.Tasks;

namespace StationApp.UI.Services;

public interface IDialogService
{
    Task<bool> ShowConfirmAsync(string title, string message, string confirmText = "\u0110\u1ed3ng \u00fd", string cancelText = "H\u1ee7y");
    Task<bool?> ShowConfirmOrCloseAsync(string title, string message, string confirmText = "\u0110\u1ed3ng \u00fd", string cancelText = "H\u1ee7y");
    Task ShowWarningAsync(string title, string message);
    Task ShowErrorAsync(string title, string message);
    Task ShowInfoAsync(string title, string message);
    Task<TResult?> ShowCustomDialogAsync<TViewModel, TResult>(TViewModel viewModel) where TViewModel : class;
}
