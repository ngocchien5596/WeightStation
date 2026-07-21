using System.Printing;
using System.Drawing.Printing;
using Microsoft.Win32;
using StationApp.Application.Printing;

namespace StationApp.UI.Printing;

public sealed class PrinterDiscoveryService : IPrinterDiscoveryService
{
    public IReadOnlyList<PrinterDescriptor> GetInstalledPrinters()
    {
        var printerNames = GetKnownPrinterNames();
        var defaultName = GetDefaultPrinterName();

        return printerNames
            .OrderBy(name => name)
            .Select(name => new PrinterDescriptor(name, string.Equals(name, defaultName, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public bool PrinterExists(string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return false;
        }

        var trimmedPrinterName = printerName.Trim();
        if (GetKnownPrinterNames().Contains(trimmedPrinterName))
        {
            return true;
        }

        try
        {
            using var server = new LocalPrintServer();
            return server.GetPrintQueues().Any(q => string.Equals(q.Name, trimmedPrinterName, StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }

    private static HashSet<string> GetKnownPrinterNames()
    {
        var printerNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string printerName in PrinterSettings.InstalledPrinters)
        {
            AddPrinterName(printerNames, printerName);
        }

        try
        {
            using var server = new LocalPrintServer();
            foreach (var queue in server.GetPrintQueues())
            {
                AddPrinterName(printerNames, queue.Name);
            }
        }
        catch
        {
            // Some per-user/network printers are visible to GDI but fail through System.Printing.
            // The print flow below can still submit jobs through PrinterSettings for those printers.
        }

        AddPrintersFromRegistry(printerNames);
        return printerNames;
    }

    private static void AddPrintersFromRegistry(HashSet<string> printerNames)
    {
        AddPrinterNamesFromRegistryValueNames(
            printerNames,
            Registry.CurrentUser,
            @"Software\Microsoft\Windows NT\CurrentVersion\Devices");
        AddPrinterNamesFromRegistryValueNames(
            printerNames,
            Registry.CurrentUser,
            @"Software\Microsoft\Windows NT\CurrentVersion\PrinterPorts");
        AddPrinterNamesFromRegistryValueNames(
            printerNames,
            Registry.LocalMachine,
            @"SYSTEM\CurrentControlSet\Control\Print\Printers");
        AddSharedPrinterConnectionsFromRegistry(printerNames);
        AddDefaultPrinterFromRegistry(printerNames);
    }

    private static void AddPrinterNamesFromRegistryValueNames(HashSet<string> printerNames, RegistryKey root, string subKeyPath)
    {
        try
        {
            using var key = root.OpenSubKey(subKeyPath);
            if (key == null)
            {
                return;
            }

            foreach (var valueName in key.GetValueNames())
            {
                AddPrinterName(printerNames, valueName);
            }

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                AddPrinterName(printerNames, subKeyName);
            }
        }
        catch
        {
        }
    }

    private static void AddSharedPrinterConnectionsFromRegistry(HashSet<string> printerNames)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Printers\Connections");
            if (key == null)
            {
                return;
            }

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                AddPrinterName(printerNames, ParseSharedConnectionName(subKeyName));
                AddPrinterName(printerNames, subKeyName);
            }
        }
        catch
        {
        }
    }

    private static void AddDefaultPrinterFromRegistry(HashSet<string> printerNames)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows NT\CurrentVersion\Windows");
            var device = key?.GetValue("Device") as string;
            if (string.IsNullOrWhiteSpace(device))
            {
                return;
            }

            var printerName = device.Split(',').FirstOrDefault();
            AddPrinterName(printerNames, printerName);
        }
        catch
        {
        }
    }

    private static string? ParseSharedConnectionName(string? registrySubKeyName)
    {
        if (string.IsNullOrWhiteSpace(registrySubKeyName))
        {
            return null;
        }

        var parts = registrySubKeyName.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length >= 2
            ? $@"\\{parts[^2]}\{parts[^1]}"
            : registrySubKeyName;
    }

    private static void AddPrinterName(HashSet<string> printerNames, string? printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
        {
            return;
        }

        printerNames.Add(printerName.Trim());
    }

    private static string? GetDefaultPrinterName()
    {
        try
        {
            using var server = new LocalPrintServer();
            if (!string.IsNullOrWhiteSpace(server.DefaultPrintQueue?.Name))
            {
                return server.DefaultPrintQueue.Name;
            }
        }
        catch
        {
        }

        try
        {
            var settings = new PrinterSettings();
            return settings.IsDefaultPrinter ? settings.PrinterName : null;
        }
        catch
        {
            return null;
        }
    }
}
