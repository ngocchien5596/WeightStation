using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace StationApp.UI.Printing;

internal static class WordDocumentPrinter
{
    private const int RpcCallRejected = unchecked((int)0x80010001);
    private const int RpcCallRetryLater = unchecked((int)0x8001010A);
    private const int ComRetryCount = 40;
    private const int ComRetryDelayMs = 250;

    public static void Print(string documentPath, string printerName, int copyCount)
    {
        if (string.IsNullOrWhiteSpace(documentPath))
        {
            throw new ArgumentException("Document path is required.", nameof(documentPath));
        }

        if (string.IsNullOrWhiteSpace(printerName))
        {
            throw new ArgumentException("Printer name is required.", nameof(printerName));
        }

        if (copyCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(copyCount), "Copy count must be greater than zero.");
        }

        if (TryPrintUsingShell(documentPath, printerName, copyCount))
        {
            return;
        }

        var wordType = Type.GetTypeFromProgID("Word.Application")
            ?? throw new InvalidOperationException("Không tìm thấy Microsoft Word để in file biên bản mẫu.");

        dynamic? word = null;
        dynamic? document = null;
        try
        {
            word = Activator.CreateInstance(wordType)
                ?? throw new InvalidOperationException("Không thể khởi tạo Microsoft Word để in biên bản.");
            InvokeComWithRetry(() => word.Visible = true);
            InvokeComWithRetry(() => word.DisplayAlerts = 0);

            document = InvokeComWithRetry(() => word.Documents.Open(
                FileName: documentPath,
                ReadOnly: true,
                AddToRecentFiles: false,
                Visible: true));

            InvokeComWithRetry(() => document.Activate());
            InvokeComWithRetry(() => word.Activate());
            TrySetMinimized(word);
            InvokeComWithRetry(() => word.ActivePrinter = printerName);

            InvokeComWithRetry(() => document.PrintOut(
                Background: false,
                Copies: copyCount));
        }
        finally
        {
            if (document != null)
            {
                try
                {
                    InvokeComWithRetry(() => document.Close(false));
                }
                catch
                {
                    // Best effort cleanup for COM automation.
                }
                finally
                {
                    ReleaseComObject(document);
                }
            }

            if (word != null)
            {
                try
                {
                    InvokeComWithRetry(() => word.Quit(false));
                }
                catch
                {
                    // Best effort cleanup for COM automation.
                }
                finally
                {
                    ReleaseComObject(word);
                }
            }
        }
    }

    private static bool TryPrintUsingShell(string documentPath, string printerName, int copyCount)
    {
        for (var copy = 0; copy < copyCount; copy++)
        {
            try
            {
                using var process = Process.Start(new ProcessStartInfo
                {
                    FileName = documentPath,
                    Verb = "printto",
                    Arguments = QuoteShellArgument(printerName),
                    UseShellExecute = true,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden
                });

                if (process == null)
                {
                    return false;
                }

                process.WaitForExit(15000);
            }
            catch
            {
                return false;
            }
        }

        // Give the shell handler a small window to hand the job to the spooler before
        // the temporary document is cleaned up by the caller.
        Thread.Sleep(1000);
        return true;
    }

    private static string QuoteShellArgument(string value)
        => $"\"{value.Replace("\"", "\\\"", StringComparison.Ordinal)}\"";

    private static void InvokeComWithRetry(Action action)
    {
        InvokeComWithRetry<object?>(() =>
        {
            action();
            return null;
        });
    }

    private static T InvokeComWithRetry<T>(Func<T> action)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return action();
            }
            catch (COMException ex) when (IsRetryableComBusy(ex) && attempt < ComRetryCount)
            {
                Thread.Sleep(ComRetryDelayMs);
            }
        }
    }

    private static bool IsRetryableComBusy(COMException ex)
        => ex.HResult == RpcCallRejected || ex.HResult == RpcCallRetryLater;

    private static void ReleaseComObject(object value)
    {
        try
        {
            if (Marshal.IsComObject(value))
            {
                Marshal.FinalReleaseComObject(value);
            }
        }
        catch
        {
            // Ignore cleanup failures.
        }
    }

    private static void TrySetMinimized(dynamic word)
    {
        try
        {
            // wdWindowStateMinimize = 2. Word needs an active document window for PrintOut,
            // but keeping it minimized avoids a disruptive foreground window.
            InvokeComWithRetry(() => word.WindowState = 2);
        }
        catch
        {
            // Some Word versions may reject WindowState before a document is open.
        }
    }
}
