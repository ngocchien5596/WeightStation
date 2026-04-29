using System.Printing;
using StationApp.Application.Printing;

namespace StationApp.UI.Printing;

public sealed class PrinterDiscoveryService : IPrinterDiscoveryService
{
    public IReadOnlyList<PrinterDescriptor> GetInstalledPrinters()
    {
        using var server = new LocalPrintServer();
        var defaultName = server.DefaultPrintQueue?.Name;
        return server.GetPrintQueues()
            .OrderBy(q => q.Name)
            .Select(q => new PrinterDescriptor(q.Name, string.Equals(q.Name, defaultName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public bool PrinterExists(string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return false;
        }

        using var server = new LocalPrintServer();
        return server.GetPrintQueues().Any(q => string.Equals(q.Name, printerName, StringComparison.OrdinalIgnoreCase));
    }
}
