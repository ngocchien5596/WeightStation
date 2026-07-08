using System;
using System.IO;
using System.Windows;
using System.Windows.Xps.Packaging;

namespace StationApp.UI.Views.Dialogs;

public partial class ReportPreviewWindow : Window
{
    private readonly XpsDocument? _xpsDocument;

    public ReportPreviewWindow(string xpsFilePath)
    {
        InitializeComponent();

        try
        {
            _xpsDocument = new XpsDocument(xpsFilePath, FileAccess.Read);
            DocViewer.Document = _xpsDocument.GetFixedDocumentSequence();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể hiển thị tài liệu: {ex.Message}", "Lỗi xem trước", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        Closed += (s, e) =>
        {
            _xpsDocument?.Close();
        };
    }
}
