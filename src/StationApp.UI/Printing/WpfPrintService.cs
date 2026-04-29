using System.Printing;
using Microsoft.Extensions.Logging;
using StationApp.Application.Printing;

namespace StationApp.UI.Printing;

public sealed class WpfPrintService : IPrintService
{
    private readonly PrintOverlayRenderer _renderer;
    private readonly ILogger<WpfPrintService> _logger;

    public WpfPrintService(PrintOverlayRenderer renderer, ILogger<WpfPrintService> logger)
    {
        _renderer = renderer;
        _logger = logger;
    }

    public Task<PrintExecutionResult> PrintAsync(
        PrintTemplateDefinition template,
        PrintBatchPreviewModel batch,
        PrintOptionsModel options,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(options.SelectedPrinterName))
        {
            throw new InvalidOperationException("Printer name is required.");
        }

        if (options.CopyCount <= 0)
        {
            throw new InvalidOperationException("Copy count must be greater than zero.");
        }

        using var server = new LocalPrintServer();
        var queue = server.GetPrintQueue(options.SelectedPrinterName);
        var results = new List<PrintDocumentResult>(batch.Pages.Count);

        foreach (var page in batch.Pages)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                for (var copy = 0; copy < options.CopyCount; copy++)
                {
                    var document = _renderer.CreateDocument(template, [page], options, previewMode: false);
                    var writer = PrintQueue.CreateXpsDocumentWriter(queue);
                    writer.Write(document.DocumentPaginator);
                }

                results.Add(new PrintDocumentResult(page.DocumentId, page.DisplayNumber, true));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Print failed for {DisplayNumber}", page.DisplayNumber);
                results.Add(new PrintDocumentResult(page.DocumentId, page.DisplayNumber, false, ex.Message));
            }
        }

        return Task.FromResult(new PrintExecutionResult { Documents = results });
    }
}
