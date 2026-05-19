using StationApp.Application.Printing;

namespace StationApp.UI.Printing;

public static class PrinterSelectionHelper
{
    public static IReadOnlyList<PrinterDescriptor> ApplyPreferredPrinter(
        IReadOnlyList<PrinterDescriptor> printers,
        string? preferredPrinterName)
    {
        if (printers.Count == 0 || string.IsNullOrWhiteSpace(preferredPrinterName))
        {
            return printers;
        }

        var hasPreferred = printers.Any(p => string.Equals(p.Name, preferredPrinterName, StringComparison.OrdinalIgnoreCase));
        if (!hasPreferred)
        {
            return printers;
        }

        return printers
            .Select(p => new PrinterDescriptor(
                p.Name,
                string.Equals(p.Name, preferredPrinterName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }
}
