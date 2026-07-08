using System;
using System.IO;
using System.Windows;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Xps.Packaging;

namespace StationApp.UI.Helpers;

public record XpsPreviewResult(
    XpsDocument? XpsDocument,
    string ExcelPath,
    string XpsPath,
    bool Success,
    string? ErrorMessage
);

public static class ReportPreviewHelper
{
    public static async Task<XpsPreviewResult> GeneratePreviewAsync(
        string reportName,
        Func<string, Task> exportAction)
    {
        var tempFolder = Path.Combine(Path.GetTempPath(), "StationApp_Reports");
        if (!Directory.Exists(tempFolder))
        {
            Directory.CreateDirectory(tempFolder);
        }

        var uniqueId = Guid.NewGuid().ToString("N");
        var tempExcelPath = Path.Combine(tempFolder, $"{reportName}_{uniqueId}.xlsx");
        var tempXpsPath = Path.Combine(tempFolder, $"{reportName}_{uniqueId}.xps");

        try
        {
            // 1. Export Excel to temporary file
            await exportAction(tempExcelPath);

            // 2. Convert Excel to XPS using Excel Interop (late binding)
            bool ok = ConvertExcelToXps(tempExcelPath, tempXpsPath);
            if (!ok)
            {
                return new XpsPreviewResult(null, tempExcelPath, tempXpsPath, false, "Không thể khởi chạy trình xem trước Excel (yêu cầu máy có cài đặt MS Excel).");
            }

            // 3. Load XPS Document
            var xpsDoc = new XpsDocument(tempXpsPath, FileAccess.Read);
            return new XpsPreviewResult(xpsDoc, tempExcelPath, tempXpsPath, true, null);
        }
        catch (Exception ex)
        {
            return new XpsPreviewResult(null, tempExcelPath, tempXpsPath, false, ex.Message);
        }
    }

    public static void CleanupPreview(XpsDocument? xpsDoc, string? excelPath, string? xpsPath)
    {
        if (xpsDoc != null)
        {
            try
            {
                xpsDoc.Close();
            }
            catch { }
        }

        _ = Task.Run(() =>
        {
            try
            {
                System.Threading.Thread.Sleep(1000); // Wait for viewer to release handles
                if (!string.IsNullOrEmpty(excelPath) && File.Exists(excelPath)) File.Delete(excelPath);
                if (!string.IsNullOrEmpty(xpsPath) && File.Exists(xpsPath)) File.Delete(xpsPath);
            }
            catch { }
        });
    }

    private static bool ConvertExcelToXps(string excelPath, string xpsPath)
    {
        Type? excelType = Type.GetTypeFromProgID("Excel.Application");
        if (excelType == null)
        {
            return false;
        }

        object? excelApp = null;
        object? workbooks = null;
        object? workbook = null;
        try
        {
            excelApp = Activator.CreateInstance(excelType);
            if (excelApp == null) return false;

            // excelApp.Visible = false;
            excelType.InvokeMember("Visible", System.Reflection.BindingFlags.SetProperty, null, excelApp, new object[] { false });

            // workbooks = excelApp.Workbooks;
            workbooks = excelType.InvokeMember("Workbooks", System.Reflection.BindingFlags.GetProperty, null, excelApp, null);
            if (workbooks == null) return false;

            // workbook = workbooks.Open(excelPath);
            workbook = workbooks.GetType().InvokeMember("Open", System.Reflection.BindingFlags.InvokeMethod, null, workbooks, new object[] { excelPath });
            if (workbook == null) return false;

            // workbook.ExportAsFixedFormat(XlFixedFormatType.xlTypeXPS, xpsPath);
            // xlTypeXPS = 1
            workbook.GetType().InvokeMember("ExportAsFixedFormat", System.Reflection.BindingFlags.InvokeMethod, null, workbook, new object[] { 1, xpsPath });

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (workbook != null)
            {
                try
                {
                    workbook.GetType().InvokeMember("Close", System.Reflection.BindingFlags.InvokeMethod, null, workbook, new object[] { false });
                }
                catch { }
            }
            if (excelApp != null)
            {
                try
                {
                    excelType.InvokeMember("Quit", System.Reflection.BindingFlags.InvokeMethod, null, excelApp, null);
                }
                catch { }
            }
        }
    }
}
