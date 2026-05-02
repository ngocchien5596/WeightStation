using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using StationApp.Application.DTOs;

namespace StationApp.UI.ViewModels;

public partial class AutocompleteInputViewModel : ObservableObject, IDisposable
{
    private readonly Func<string, CancellationToken, Task<IReadOnlyList<AutocompleteItem>>> _searchAsync;
    private readonly Action<AutocompleteItem>? _onSelected;
    private CancellationTokenSource? _debounceCts;
    private bool _suppressSearch;
    private bool _disposed;

    public AutocompleteInputViewModel(
        Func<string, CancellationToken, Task<IReadOnlyList<AutocompleteItem>>> searchAsync,
        Action<AutocompleteItem>? onSelected = null,
        int minimumPrefixLength = 1,
        int debounceMilliseconds = 200)
    {
        _searchAsync = searchAsync;
        _onSelected = onSelected;
        MinimumPrefixLength = minimumPrefixLength;
        DebounceMilliseconds = debounceMilliseconds;
    }

    public ObservableCollection<AutocompleteItem> Items { get; } = new();

    [ObservableProperty] private string? _text;
    [ObservableProperty] private bool _isDropDownOpen;
    [ObservableProperty] private bool _hasNoResults;
    [ObservableProperty] private int _selectedIndex = -1;
    [ObservableProperty] private bool _isEnabled = true;

    public int MinimumPrefixLength { get; }
    public int DebounceMilliseconds { get; }
    public bool HasSingleSuggestion => Items.Count == 1 && !HasNoResults;

    partial void OnTextChanged(string? value)
    {
        if (_suppressSearch || !IsEnabled)
        {
            return;
        }

        _ = RefreshSuggestionsAsync(value);
    }

    public void SetText(string? value, bool suppressSearch = true)
    {
        try
        {
            _suppressSearch = suppressSearch;
            Text = value;
        }
        finally
        {
            _suppressSearch = false;
        }

        if (suppressSearch)
        {
            ClearSuggestions();
        }
    }

    public void Close()
    {
        IsDropDownOpen = false;
        SelectedIndex = -1;
        HasNoResults = false;
    }

    public void Clear()
    {
        SetText(null);
    }

    public void MoveSelection(int direction)
    {
        if (Items.Count == 0)
        {
            return;
        }

        if (!IsDropDownOpen)
        {
            IsDropDownOpen = true;
        }

        var nextIndex = SelectedIndex < 0
            ? (direction > 0 ? 0 : Items.Count - 1)
            : (SelectedIndex + direction + Items.Count) % Items.Count;

        SelectedIndex = nextIndex;
    }

    public bool CommitSelection()
    {
        if (SelectedIndex < 0 || SelectedIndex >= Items.Count)
        {
            return false;
        }

        ApplySelection(Items[SelectedIndex]);
        return true;
    }

    public bool CommitSelection(AutocompleteItem item)
    {
        if (!Items.Contains(item))
        {
            return false;
        }

        ApplySelection(item);
        return true;
    }

    public bool TryCommitSingleSuggestion()
    {
        if (!HasSingleSuggestion)
        {
            Close();
            return false;
        }

        ApplySelection(Items[0]);
        return true;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();
    }

    private async Task RefreshSuggestionsAsync(string? value)
    {
        _debounceCts?.Cancel();
        _debounceCts?.Dispose();

        var cts = new CancellationTokenSource();
        _debounceCts = cts;

        var keyword = value?.Trim();
        if (string.IsNullOrWhiteSpace(keyword) || keyword.Length < MinimumPrefixLength)
        {
            ClearSuggestions();
            return;
        }

        try
        {
            await Task.Delay(DebounceMilliseconds, cts.Token);
            var items = await _searchAsync(keyword, cts.Token);

            if (cts.IsCancellationRequested)
            {
                return;
            }

            Items.Clear();
            foreach (var item in items)
            {
                Items.Add(item);
            }

            HasNoResults = keyword.Length >= MinimumPrefixLength && Items.Count == 0;
            IsDropDownOpen = Items.Count > 0 || HasNoResults;
            SelectedIndex = Items.Count > 0 ? 0 : -1;
        }
        catch (OperationCanceledException)
        {
        }
    }

    private void ApplySelection(AutocompleteItem item)
    {
        SetText(item.Value);
        _onSelected?.Invoke(item);
        Close();
    }

    private void ClearSuggestions()
    {
        Items.Clear();
        Close();
    }
}
