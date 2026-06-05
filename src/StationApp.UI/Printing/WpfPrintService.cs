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
        queue.Refresh();
        EnsureQueueCanAcceptJobs(queue);

        var pagesToPrint = options.SelectedDocumentIds.Count == 0
            ? batch.Pages
            : batch.Pages.Where(x => options.SelectedDocumentIds.Contains(x.DocumentId)).ToList();

        if (pagesToPrint.Count == 0)
        {
            throw new InvalidOperationException("Không có phiếu nào được chọn để in.");
        }

        var results = new List<PrintDocumentResult>(pagesToPrint.Count);

        _logger.LogInformation(
            "Print started. Printer={PrinterName} DocumentKind={DocumentKind} DocumentCount={DocumentCount} CopyCount={CopyCount}",
            queue.Name,
            batch.Kind,
            pagesToPrint.Count,
            options.CopyCount);

        foreach (var page in pagesToPrint)
        {
            ct.ThrowIfCancellationRequested();

            try
            {
                for (var copy = 0; copy < options.CopyCount; copy++)
                {
                    queue.Refresh();
                    EnsureQueueCanAcceptJobs(queue);

                    var document = _renderer.CreateDocument(template, [page], options, previewMode: false);
                    var jobName = BuildJobName(batch.Kind, page.DisplayNumber, copy + 1, options.CopyCount);
                    
                    // Render WPF FixedPage to RenderTargetBitmap (300 DPI for high quality printing)
                    var pageContent = document.Pages[0];
                    var fixedPage = (System.Windows.Documents.FixedPage)pageContent.GetPageRoot(false);

                    var pageWidth = MmToDip(template.PageWidthMm);
                    var pageHeight = MmToDip(template.PageHeightMm);
                    fixedPage.Measure(new System.Windows.Size(pageWidth, pageHeight));
                    fixedPage.Arrange(new System.Windows.Rect(0, 0, pageWidth, pageHeight));
                    fixedPage.UpdateLayout();

                    double dpiScale = 300d / 96d;
                    int pixelWidth = (int)Math.Round(pageWidth * dpiScale);
                    int pixelHeight = (int)Math.Round(pageHeight * dpiScale);

                    var renderTarget = new System.Windows.Media.Imaging.RenderTargetBitmap(
                        pixelWidth,
                        pixelHeight,
                        300d,
                        300d,
                        System.Windows.Media.PixelFormats.Pbgra32);

                    renderTarget.Render(fixedPage);

                    // Convert RenderTargetBitmap to System.Drawing.Bitmap
                    using (var bmp = new System.Drawing.Bitmap(renderTarget.PixelWidth, renderTarget.PixelHeight, System.Drawing.Imaging.PixelFormat.Format32bppPArgb))
                    {
                        var bmpData = bmp.LockBits(
                            new System.Drawing.Rectangle(0, 0, bmp.Width, bmp.Height),
                            System.Drawing.Imaging.ImageLockMode.WriteOnly,
                            bmp.PixelFormat);

                        renderTarget.CopyPixels(
                            System.Windows.Int32Rect.Empty,
                            bmpData.Scan0,
                            bmpData.Stride * bmpData.Height,
                            bmpData.Stride);

                        bmp.UnlockBits(bmpData);

                        // Print using legacy GDI PrintDocument (directly compatible with GDI printers like Canon LBP2900)
                        using (var printDoc = new System.Drawing.Printing.PrintDocument())
                        {
                            printDoc.PrinterSettings.PrinterName = queue.Name;
                            printDoc.DocumentName = jobName;
                            printDoc.DefaultPageSettings.Margins = new System.Drawing.Printing.Margins(0, 0, 0, 0);

                            // Page dimensions in hundredths of an inch (1 mm = 3.93701 hundredths of an inch)
                            var paperWidth = (int)Math.Round(template.PageWidthMm * 3.93701);
                            var paperHeight = (int)Math.Round(template.PageHeightMm * 3.93701);
                            printDoc.DefaultPageSettings.PaperSize = new System.Drawing.Printing.PaperSize("Custom", paperWidth, paperHeight);

                            var targetWidth = template.PageWidthMm * 3.93701;
                            var targetHeight = template.PageHeightMm * 3.93701;

                            printDoc.PrintPage += (sender, e) =>
                            {
                                if (e.Graphics == null) return;
                                e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                                e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;

                                double availablePhysicalHeight = (template.PageHeightMm <= 150.0 && e.PageBounds.Height > 800)
                                    ? e.PageBounds.Height / 2.0
                                    : e.PageBounds.Height;

                                double px = (e.PageBounds.Width - targetWidth) / 2.0;
                                double py = (availablePhysicalHeight - targetHeight) / 2.0;

                                px = Math.Max(0.0, px);
                                py = Math.Max(0.0, py);

                                float drawX = (float)(px - e.PageSettings.HardMarginX);
                                float drawY = (float)(py - e.PageSettings.HardMarginY);

                                e.Graphics.DrawImage(bmp, drawX, drawY, (float)targetWidth, (float)targetHeight);
                                e.HasMorePages = false;
                            };

                            printDoc.Print();
                        }
                    }

                    DetectImmediateQueueError(queue, jobName, ct);

                    _logger.LogInformation(
                        "Print job submitted. Printer={PrinterName} JobName={JobName} DisplayNumber={DisplayNumber} Copy={Copy}/{CopyCount}",
                        queue.Name,
                        jobName,
                        page.DisplayNumber,
                        copy + 1,
                        options.CopyCount);
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

    private void DetectImmediateQueueError(PrintQueue queue, string jobName, CancellationToken ct)
    {
        const int attempts = 12;
        for (var i = 0; i < attempts; i++)
        {
            ct.ThrowIfCancellationRequested();
            Thread.Sleep(500);
            queue.Refresh();
            EnsureQueueCanAcceptJobs(queue);
            InspectPrintJobs(queue, jobName, i + 1);
        }
    }

    private static void EnsureQueueCanAcceptJobs(PrintQueue queue)
    {
        var status = queue.QueueStatus;
        var errors = new List<string>();

        AddIf(status, PrintQueueStatus.Offline, errors, "offline");
        AddIf(status, PrintQueueStatus.Error, errors, "error");
        AddIf(status, PrintQueueStatus.NotAvailable, errors, "not available");
        AddIf(status, PrintQueueStatus.PaperOut, errors, "out of paper");
        AddIf(status, PrintQueueStatus.PaperJam, errors, "paper jam");
        AddIf(status, PrintQueueStatus.PaperProblem, errors, "paper problem");
        AddIf(status, PrintQueueStatus.OutputBinFull, errors, "output bin full");
        AddIf(status, PrintQueueStatus.NoToner, errors, "no toner");
        AddIf(status, PrintQueueStatus.DoorOpen, errors, "door open");
        AddIf(status, PrintQueueStatus.Paused, errors, "paused");
        AddIf(status, PrintQueueStatus.UserIntervention, errors, "requires user intervention");
        AddIf(status, PrintQueueStatus.ServerUnknown, errors, "server unknown");

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                $"Máy in '{queue.Name}' chưa sẵn sàng: {string.Join(", ", errors)}.");
        }
    }

    private static void AddIf(PrintQueueStatus status, PrintQueueStatus flag, List<string> errors, string label)
    {
        if ((status & flag) == flag)
        {
            errors.Add(label);
        }
    }

    private void InspectPrintJobs(PrintQueue queue, string jobName, int attempt)
    {
        try
        {
            var jobs = queue.GetPrintJobInfoCollection()
                .Select(job => new
                {
                    job.JobIdentifier,
                    job.Name,
                    job.JobStatus,
                    job.NumberOfPages,
                    job.NumberOfPagesPrinted
                })
                .ToList();

            _logger.LogInformation(
                "Print queue snapshot. Printer={PrinterName} JobName={JobName} Attempt={Attempt} QueueStatus={QueueStatus} JobCount={JobCount} Jobs={Jobs}",
                queue.Name,
                jobName,
                attempt,
                queue.QueueStatus,
                jobs.Count,
                string.Join(" | ", jobs.Select(job => $"#{job.JobIdentifier}:{job.Name}:{job.JobStatus}:{job.NumberOfPagesPrinted}/{job.NumberOfPages}")));

            var failedJob = jobs.FirstOrDefault(job => HasFatalJobStatus(job.JobStatus));
            if (failedJob != null)
            {
                throw new InvalidOperationException(
                    $"Job in của máy '{queue.Name}' đang lỗi: #{failedJob.JobIdentifier} {failedJob.Name} - {failedJob.JobStatus}.");
            }
        }
        catch (Exception ex)
        {
            if (ex is InvalidOperationException)
            {
                throw;
            }

            _logger.LogWarning(ex, "Cannot inspect print queue. Printer={PrinterName} JobName={JobName}", queue.Name, jobName);
        }
    }

    private static bool HasFatalJobStatus(PrintJobStatus status)
    {
        return HasJobFlag(status, PrintJobStatus.Error)
            || HasJobFlag(status, PrintJobStatus.Offline)
            || HasJobFlag(status, PrintJobStatus.PaperOut)
            || HasJobFlag(status, PrintJobStatus.Blocked)
            || HasJobFlag(status, PrintJobStatus.UserIntervention);
    }

    private static bool HasJobFlag(PrintJobStatus status, PrintJobStatus flag)
        => (status & flag) == flag;

    private static string BuildJobName(PrintDocumentKind kind, string displayNumber, int copy, int copyCount)
    {
        var kindText = kind == PrintDocumentKind.WeighTicket ? "PC" : "PGN";
        return copyCount <= 1
            ? $"StationApp {kindText} {displayNumber}"
            : $"StationApp {kindText} {displayNumber} copy {copy}/{copyCount}";
    }

    private static double MmToDip(double mm) => mm * 96d / 25.4d;
}
