using System.ComponentModel;
using System.Runtime.InteropServices;

namespace StationApp.UI.Printing;

internal static class RawPrinterWriter
{
    public static void SendBytes(string printerName, string documentName, byte[] bytes)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            throw new ArgumentException("Printer name is required.", nameof(printerName));
        }

        var defaults = new PrinterDefaults
        {
            DesiredAccess = PrinterAccessUse
        };

        if (!OpenPrinter(printerName, out var printerHandle, ref defaults))
        {
            ThrowLastWin32Error($"Cannot open printer '{printerName}'.");
        }

        try
        {
            var docInfo = new DocInfo
            {
                DocumentName = documentName,
                DataType = "RAW"
            };

            if (!StartDocPrinter(printerHandle, 1, ref docInfo))
            {
                ThrowLastWin32Error($"Cannot start RAW print job '{documentName}'.");
            }

            try
            {
                if (!StartPagePrinter(printerHandle))
                {
                    ThrowLastWin32Error($"Cannot start RAW print page '{documentName}'.");
                }

                try
                {
                    if (!WritePrinter(printerHandle, bytes, bytes.Length, out var written) || written != bytes.Length)
                    {
                        ThrowLastWin32Error($"Cannot write RAW print data for '{documentName}'.");
                    }
                }
                finally
                {
                    EndPagePrinter(printerHandle);
                }
            }
            finally
            {
                EndDocPrinter(printerHandle);
            }
        }
        finally
        {
            ClosePrinter(printerHandle);
        }
    }

    private static void ThrowLastWin32Error(string message)
    {
        throw new Win32Exception(Marshal.GetLastWin32Error(), message);
    }

    private const int PrinterAccessUse = 0x00000008;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct DocInfo
    {
        [MarshalAs(UnmanagedType.LPWStr)] public string DocumentName;
        [MarshalAs(UnmanagedType.LPWStr)] public string? OutputFile;
        [MarshalAs(UnmanagedType.LPWStr)] public string DataType;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PrinterDefaults
    {
        public nint DataType;
        public nint DevMode;
        public int DesiredAccess;
    }

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool OpenPrinter(string printerName, out nint printerHandle, ref PrinterDefaults defaults);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool ClosePrinter(nint printerHandle);

    [DllImport("winspool.drv", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool StartDocPrinter(nint printerHandle, int level, ref DocInfo docInfo);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndDocPrinter(nint printerHandle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool StartPagePrinter(nint printerHandle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool EndPagePrinter(nint printerHandle);

    [DllImport("winspool.drv", SetLastError = true)]
    private static extern bool WritePrinter(nint printerHandle, byte[] data, int count, out int written);
}
