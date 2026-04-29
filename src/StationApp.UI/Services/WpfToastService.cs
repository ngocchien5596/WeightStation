using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using StationApp.UI.Controls;

namespace StationApp.UI.Services;

public class WpfToastService : IToastService
{
    private class ToastRequest
    {
        public string Message { get; set; } = string.Empty;
        public string Level { get; set; } = string.Empty;
        public int DurationMs { get; set; }
    }

    private readonly Queue<ToastRequest> _queue = new();
    private ToastNotificationWindow? _currentWindow;
    private DispatcherTimer? _hideTimer;
    private bool _isShowing = false;
    private ToastRequest? _currentRequest;

    public void ShowSuccess(string message) => EnqueueToast(message, "Success", 3000);
    public void ShowWarning(string message) => EnqueueToast(message, "Warning", 5000);
    public void ShowError(string message) => EnqueueToast(message, "Error", 5000);
    public void ShowInfo(string message) => EnqueueToast(message, "Info", 3000);

    private void EnqueueToast(string message, string level, int durationMs)
    {
        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher == null) return;

        dispatcher.InvokeAsync(() =>
        {
            // Coalesce / Replace logic
            if (_isShowing && _currentRequest != null && _currentRequest.Message == message && _currentRequest.Level == level)
            {
                // Reset timer instead of stacking
                _hideTimer?.Stop();
                _hideTimer?.Start();
                return;
            }

            // Prevent duplicates in queue
            if (_queue.Any(q => q.Message == message && q.Level == level))
            {
                return;
            }

            // Max queue size to prevent spam
            if (_queue.Count >= 3)
            {
                _queue.Dequeue();
            }

            _queue.Enqueue(new ToastRequest { Message = message, Level = level, DurationMs = durationMs });

            if (!_isShowing)
            {
                ProcessNext();
            }
        });
    }

    private void ProcessNext()
    {
        if (_queue.Count == 0)
        {
            _isShowing = false;
            return;
        }

        _isShowing = true;
        _currentRequest = _queue.Dequeue();

        if (_currentWindow == null)
        {
            _currentWindow = new ToastNotificationWindow();
            _currentWindow.Closed += (s, e) => _currentWindow = null;
        }

        _currentWindow.SetContent(_currentRequest.Message, _currentRequest.Level);
        
        // Ensure size is measured before positioning
        _currentWindow.UpdateLayout(); 
        PositionWindow();
        
        _currentWindow.Show();
        FadeIn();

        _hideTimer?.Stop();
        _hideTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(_currentRequest.DurationMs) };
        _hideTimer.Tick += (s, e) =>
        {
            _hideTimer?.Stop();
            FadeOutAndNext();
        };
        _hideTimer.Start();
    }

    private void PositionWindow()
    {
        if (_currentWindow == null) return;
        
        var mainWindow = System.Windows.Application.Current.MainWindow;
        if (mainWindow != null && mainWindow.IsVisible)
        {
            try
            {
                if (_currentWindow.Owner != mainWindow)
                {
                    _currentWindow.Owner = mainWindow;
                }

                if (mainWindow.WindowState != WindowState.Minimized)
                {
                    // Use PointToScreen to get precise top-right corner of visual bounds
                    Point physicalTopRight = mainWindow.PointToScreen(new Point(mainWindow.ActualWidth, 0));
                    
                    var source = PresentationSource.FromVisual(mainWindow);
                    if (source != null)
                    {
                        var matrix = source.CompositionTarget.TransformFromDevice;
                        Point logicalTopRight = matrix.Transform(physicalTopRight);

                        _currentWindow.Left = logicalTopRight.X - _currentWindow.Width - 20;
                        _currentWindow.Top = logicalTopRight.Y + 20;
                        return;
                    }
                    
                    // Fallback to absolute screen coordinates if visual mapping fails
                    var workAreaBounds = SystemParameters.WorkArea;
                    _currentWindow.Left = workAreaBounds.Right - _currentWindow.Width - 20;
                    _currentWindow.Top = workAreaBounds.Top + 20;
                    return;
                }
            }
            catch (Exception)
            {
                // Fallback to absolute screen coordinates if anything goes wrong with Owner or state
            }
        }
        
        // Fallback to absolute screen coordinates
        var workArea = SystemParameters.WorkArea;
        _currentWindow.Left = workArea.Right - _currentWindow.Width - 20;
        _currentWindow.Top = workArea.Top + 20; // Top right
    }

    private void FadeIn()
    {
        if (_currentWindow == null) return;
        var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300));
        _currentWindow.BeginAnimation(UIElement.OpacityProperty, anim);
    }

    private void FadeOutAndNext()
    {
        if (_currentWindow == null)
        {
            ProcessNext();
            return;
        }

        var anim = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
        anim.Completed += (s, e) =>
        {
            _currentWindow.Hide();
            ProcessNext();
        };
        _currentWindow.BeginAnimation(UIElement.OpacityProperty, anim);
    }
}
