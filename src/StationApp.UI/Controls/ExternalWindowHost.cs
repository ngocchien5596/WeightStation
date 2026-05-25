using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace StationApp.UI.Controls;

public sealed class ExternalWindowHost : HwndHost
{
    private IntPtr _hwndHost;

    public IntPtr HostHandle => _hwndHost;

    protected override HandleRef BuildWindowCore(HandleRef hwndParent)
    {
        _hwndHost = NativeMethods.CreateWindowEx(
            0,
            "static",
            string.Empty,
            NativeMethods.WS_CHILD | NativeMethods.WS_VISIBLE,
            0,
            0,
            Math.Max(1, (int)ActualWidth),
            Math.Max(1, (int)ActualHeight),
            hwndParent.Handle,
            IntPtr.Zero,
            IntPtr.Zero,
            IntPtr.Zero);

        return new HandleRef(this, _hwndHost);
    }

    protected override void DestroyWindowCore(HandleRef hwnd)
    {
        if (hwnd.Handle != IntPtr.Zero)
        {
            NativeMethods.DestroyWindow(hwnd.Handle);
        }

        _hwndHost = IntPtr.Zero;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_hwndHost != IntPtr.Zero)
        {
            NativeMethods.MoveWindow(
                _hwndHost,
                0,
                0,
                Math.Max(1, (int)finalSize.Width),
                Math.Max(1, (int)finalSize.Height),
                true);
        }

        return base.ArrangeOverride(finalSize);
    }

    private static class NativeMethods
    {
        internal const int WS_CHILD = 0x40000000;
        internal const int WS_VISIBLE = 0x10000000;

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        internal static extern IntPtr CreateWindowEx(
            int exStyle,
            string className,
            string windowName,
            int style,
            int x,
            int y,
            int width,
            int height,
            IntPtr parentHandle,
            IntPtr menuHandle,
            IntPtr instanceHandle,
            IntPtr parameter);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool DestroyWindow(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        internal static extern bool MoveWindow(IntPtr hwnd, int x, int y, int width, int height, bool repaint);
    }
}
