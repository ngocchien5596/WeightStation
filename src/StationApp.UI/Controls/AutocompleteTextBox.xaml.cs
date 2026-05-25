using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using StationApp.Application.DTOs;
using StationApp.UI.ViewModels;

namespace StationApp.UI.Controls;

public partial class AutocompleteTextBox : UserControl
{
    public static readonly DependencyProperty StateProperty =
        DependencyProperty.Register(
            nameof(State),
            typeof(AutocompleteInputViewModel),
            typeof(AutocompleteTextBox),
            new PropertyMetadata(null));

    public static readonly DependencyProperty TextBoxStyleProperty =
        DependencyProperty.Register(
            nameof(TextBoxStyle),
            typeof(Style),
            typeof(AutocompleteTextBox),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(
            nameof(IsReadOnly),
            typeof(bool),
            typeof(AutocompleteTextBox),
            new PropertyMetadata(false));

    public AutocompleteTextBox()
    {
        InitializeComponent();
    }

    public AutocompleteInputViewModel? State
    {
        get => (AutocompleteInputViewModel?)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public Style? TextBoxStyle
    {
        get => (Style?)GetValue(TextBoxStyleProperty);
        set => SetValue(TextBoxStyleProperty, value);
    }

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    private void InputTextBox_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (State == null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                if (State.Items.Count > 0)
                {
                    State.MoveSelection(1);
                    SuggestionsList.Focus();
                    e.Handled = true;
                }
                break;
            case Key.Up:
                if (State.Items.Count > 0)
                {
                    State.MoveSelection(-1);
                    SuggestionsList.Focus();
                    e.Handled = true;
                }
                break;
            case Key.Enter:
                if (State.IsDropDownOpen && State.CommitSelection())
                {
                    e.Handled = true;
                }
                break;
            case Key.Escape:
                if (State.IsDropDownOpen)
                {
                    State.Close();
                    e.Handled = true;
                }
                break;
            case Key.Tab:
                if (State.IsDropDownOpen)
                {
                    State.TryCommitSingleSuggestion();
                    State.Close();
                }
                break;
        }
    }

    private void SuggestionsList_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (State == null)
        {
            return;
        }

        switch (e.Key)
        {
            case Key.Down:
                State.MoveSelection(1);
                e.Handled = true;
                break;
            case Key.Up:
                State.MoveSelection(-1);
                e.Handled = true;
                break;
            case Key.Enter:
                if (State.CommitSelection())
                {
                    InputTextBox.Focus();
                    e.Handled = true;
                }
                break;
            case Key.Escape:
                State.Close();
                InputTextBox.Focus();
                e.Handled = true;
                break;
            case Key.Tab:
                State.TryCommitSingleSuggestion();
                State.Close();
                break;
        }
    }

    private void SuggestionsList_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (State == null)
        {
            return;
        }

        if (SuggestionsList.SelectedItem is AutocompleteItem item && State.CommitSelection(item))
        {
            InputTextBox.Focus();
        }
    }

    private void FocusableElement_OnLostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (State == null)
            {
                return;
            }

            if (!InputTextBox.IsKeyboardFocusWithin && !SuggestionsList.IsKeyboardFocusWithin)
            {
                State.Close();
            }
        }, DispatcherPriority.Background);
    }
}
