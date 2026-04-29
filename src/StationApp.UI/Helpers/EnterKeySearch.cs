using System.Windows;
using System.Windows.Input;

namespace StationApp.UI.Helpers;

public static class EnterKeySearch
{
    public static readonly DependencyProperty CommandProperty =
        DependencyProperty.RegisterAttached(
            "Command",
            typeof(ICommand),
            typeof(EnterKeySearch),
            new PropertyMetadata(null, OnCommandChanged));

    public static readonly DependencyProperty CommandParameterProperty =
        DependencyProperty.RegisterAttached(
            "CommandParameter",
            typeof(object),
            typeof(EnterKeySearch),
            new PropertyMetadata(null));

    public static ICommand? GetCommand(DependencyObject obj) => (ICommand?)obj.GetValue(CommandProperty);

    public static void SetCommand(DependencyObject obj, ICommand? value) => obj.SetValue(CommandProperty, value);

    public static object? GetCommandParameter(DependencyObject obj) => obj.GetValue(CommandParameterProperty);

    public static void SetCommandParameter(DependencyObject obj, object? value) => obj.SetValue(CommandParameterProperty, value);

    private static void OnCommandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not UIElement element)
        {
            return;
        }

        element.PreviewKeyDown -= OnPreviewKeyDown;

        if (e.NewValue is ICommand)
        {
            element.PreviewKeyDown += OnPreviewKeyDown;
        }
    }

    private static void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not DependencyObject dependencyObject)
        {
            return;
        }

        var command = GetCommand(dependencyObject);
        var parameter = GetCommandParameter(dependencyObject);

        if (command is null || !command.CanExecute(parameter))
        {
            return;
        }

        command.Execute(parameter);
        e.Handled = true;
    }
}
