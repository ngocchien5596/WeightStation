using System.Globalization;
using System.Runtime.InteropServices;

namespace StationApp.UI.Printing;

internal static class WordDocumentPrinter
{
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

        var wordType = Type.GetTypeFromProgID("Word.Application")
            ?? throw new InvalidOperationException("Không tìm thấy Microsoft Word để in file biên bản mẫu.");

        dynamic? word = null;
        dynamic? document = null;
        try
        {
            word = Activator.CreateInstance(wordType)
                ?? throw new InvalidOperationException("Không thể khởi tạo Microsoft Word để in biên bản.");
            word.Visible = false;
            word.DisplayAlerts = 0;
            word.ActivePrinter = printerName;

            document = word.Documents.Open(
                FileName: documentPath,
                ReadOnly: true,
                AddToRecentFiles: false,
                Visible: false);

            document.PrintOut(
                Background: false,
                Copies: copyCount.ToString(CultureInfo.InvariantCulture));
        }
        finally
        {
            if (document != null)
            {
                try
                {
                    document.Close(false);
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
                    word.Quit(false);
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
}
