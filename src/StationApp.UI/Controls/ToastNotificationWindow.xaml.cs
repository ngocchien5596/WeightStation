using System.Windows;
using System.Windows.Media;

namespace StationApp.UI.Controls;

public partial class ToastNotificationWindow : Window
{
    public ToastNotificationWindow()
    {
        InitializeComponent();
    }

    public void SetContent(string message, string level)
    {
        MessageText.Text = message;
        switch (level)
        {
            case "Success":
                ToastBorder.Background = new SolidColorBrush(Color.FromRgb(46, 213, 115));
                IconText.Text = "\uE73E"; // Checkmark
                break;
            case "Warning":
                ToastBorder.Background = new SolidColorBrush(Color.FromRgb(255, 165, 2));
                IconText.Text = "\uE7BA"; // Warning
                break;
            case "Error":
                ToastBorder.Background = new SolidColorBrush(Color.FromRgb(255, 71, 87));
                IconText.Text = "\uE711"; // Cancel
                break;
            default: // Info
                ToastBorder.Background = new SolidColorBrush(Color.FromRgb(30, 144, 255));
                IconText.Text = "\uE946"; // Info
                break;
        }
    }
}
