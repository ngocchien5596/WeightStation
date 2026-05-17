using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.Printing;
using StationApp.Application.Security;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.UI.Printing;
using StationApp.UI.Resources;
using StationApp.UI.Services;
using StationApp.UI.ViewModels.Dialogs;

namespace StationApp.UI.ViewModels;

public partial class OutgoingVehicleListViewModel : ObservableObject
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IToastService _toastService;
    private readonly IDialogService _dialogService;
    private readonly IClock _clock;
    private readonly ICurrentUserContext _currentUserContext;
    private readonly ILogger<OutgoingVehicleListViewModel>? _logger;

    [ObservableProperty] private ObservableCollection<OutgoingSessionListItem> _vehicles = new();
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ShowDetailsCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintWeighTicketCommand))]
    [NotifyCanExecuteChangedFor(nameof(PrintDeliveryTicketCommand))]
    [NotifyCanExecuteChangedFor(nameof(ShowRelatedTicketsCommand))]
    private OutgoingSessionListItem? _selectedVehicle;
    [ObservableProperty] private string? _searchSessionNo;
    [ObservableProperty] private string? _searchVehiclePlate;
    [ObservableProperty] private DateTime? _selectedCompletedDate;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private bool _isDetailsVisible;
    [ObservableProperty] private bool _isRelatedTicketsVisible;
    [ObservableProperty] private ObservableCollection<WeighingSessionLineRow> _detailLines = new();
    [ObservableProperty] private ObservableCollection<RelatedDocumentListItem> _relatedTickets = new();

    public OutgoingVehicleListViewModel(
        IServiceScopeFactory scopeFactory,
        IToastService toastService,
        IDialogService dialogService,
        IClock clock,
        ICurrentUserContext currentUserContext,
        ILogger<OutgoingVehicleListViewModel>? logger = null)
    {
        _scopeFactory = scopeFactory;
        _toastService = toastService;
        _dialogService = dialogService;
        _clock = clock;
        _currentUserContext = currentUserContext;
        _logger = logger;
        SelectedCompletedDate = _clock.NowLocal.Date;
    }

    partial void OnSelectedCompletedDateChanged(DateTime? value)
    {
        if (IsLoading)
        {
            return;
        }

        _ = ReloadForCompletedDateChangeAsync();
    }

    private bool CanShowDetails() => SelectedVehicle != null;
    private bool CanPrintWeighTicket() => SelectedVehicle != null;
    private bool CanPrintDeliveryTicket() => SelectedVehicle != null;
    private bool CanShowRelatedTickets() => SelectedVehicle != null;

    [RelayCommand]
    private async Task LoadVehiclesAsync()
    {
        await LoadVehiclesInternalAsync(true);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        SearchSessionNo = null;
        SearchVehiclePlate = null;
        SelectedCompletedDate = _clock.NowLocal.Date;
        IsDetailsVisible = false;
        IsRelatedTicketsVisible = false;
        DetailLines = new ObservableCollection<WeighingSessionLineRow>();
        RelatedTickets = new ObservableCollection<RelatedDocumentListItem>();
        SelectedVehicle = null;
        await LoadVehiclesInternalAsync(false);
    }

    private async Task LoadVehiclesInternalAsync(bool keepSelection)
    {
        IsLoading = true;
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var repo = scope.ServiceProvider.GetRequiredService<IWeighingSessionRepository>();
            var list = await repo.SearchCompletedSessionsAsync(null, SelectedCompletedDate, CancellationToken.None);
            var filtered = list.Where(x =>
                MatchesSearch(x.SessionNo, SearchSessionNo)
                && MatchesSearch(x.VehiclePlate, SearchVehiclePlate));
            Vehicles = new ObservableCollection<OutgoingSessionListItem>(filtered);

            if (keepSelection && SelectedVehicle != null)
            {
                SelectedVehicle = Vehicles.FirstOrDefault(x => x.SessionId == SelectedVehicle.SessionId);
            }

            if (list.Count == 0 && HasSearchFilters())
            {
                _toastService.ShowInfo(UiText.Common.NoMatchingData);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "LoadOutgoingVehicles failed");
            _toastService.ShowError(UiText.Common.SearchOutgoingLoadError);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanShowDetails))]
    private async Task ShowDetailsAsync()
    {
        if (SelectedVehicle == null)
        {
            return;
        }

        await LoadSessionDetailsAsync(SelectedVehicle.SessionId);
        IsDetailsVisible = true;
    }

    [RelayCommand]
    private void CloseDetails()
    {
        IsDetailsVisible = false;
    }

    [RelayCommand(CanExecute = nameof(CanShowRelatedTickets))]
    private async Task ShowRelatedTicketsAsync()
    {
        if (SelectedVehicle == null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var weighRepo = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();
        var deliveryRepo = scope.ServiceProvider.GetRequiredService<IDeliveryTicketRepository>();
        var weighTickets = await weighRepo.GetByWeighingSessionIdAsync(SelectedVehicle.SessionId, CancellationToken.None);
        var deliveryTickets = await deliveryRepo.GetByWeighingSessionIdAsync(SelectedVehicle.SessionId, CancellationToken.None);

        RelatedTickets = new ObservableCollection<RelatedDocumentListItem>(
            weighTickets.Select(ticket => new RelatedDocumentListItem(
                    UiText.Weighing.RelatedWeighTicket,
                    ticket.TicketNo,
                    null,
                    ticket.RecordRole,
                    ticket.SplitSequence,
                    ticket.Weight1,
                    ticket.Weight2,
                    ticket.NetWeight,
                    ticket.CreatedAt))
                .Concat(deliveryTickets.Select(ticket => new RelatedDocumentListItem(
                    UiText.Weighing.RelatedDeliveryTicket,
                    null,
                    ticket.DeliveryNo,
                    ticket.RecordRole,
                    ticket.SplitSequence,
                    null,
                    null,
                    ticket.AllocatedWeight,
                    ticket.CreatedAt)))
                .OrderBy(x => x.DocumentType)
                .ThenBy(x => x.SplitSequence ?? byte.MaxValue)
                .ThenBy(x => x.CreatedAt));

        IsRelatedTicketsVisible = true;
    }

    [RelayCommand]
    private void CloseRelatedTickets()
    {
        IsRelatedTicketsVisible = false;
    }

    [RelayCommand(CanExecute = nameof(CanPrintWeighTicket))]
    private async Task PrintWeighTicketAsync()
    {
        if (SelectedVehicle == null)
        {
            return;
        }

        await ExecutePrintFlowAsync(PrintDocumentKind.WeighTicket, SelectedVehicle.SessionId, "phiếu cân");
    }

    [RelayCommand(CanExecute = nameof(CanPrintDeliveryTicket))]
    private async Task PrintDeliveryTicketAsync()
    {
        if (SelectedVehicle == null)
        {
            return;
        }

        await ExecutePrintFlowAsync(PrintDocumentKind.DeliveryTicket, SelectedVehicle.SessionId, "phiếu giao nhận");
    }

    public async Task InitializeAsync()
    {
        await LoadVehiclesAsync();
    }

    private async Task ReloadForCompletedDateChangeAsync()
    {
        try
        {
            await LoadVehiclesInternalAsync(false);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Reload outgoing vehicles on completed date change failed");
        }
    }

    private bool HasSearchFilters()
    {
        return !string.IsNullOrWhiteSpace(SearchSessionNo)
            || !string.IsNullOrWhiteSpace(SearchVehiclePlate);
    }

    private static bool MatchesSearch(string? source, string? keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(source)
            && source.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    private async Task LoadSessionDetailsAsync(Guid sessionId)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<IWeighingSessionRepository>();
        var lines = await repo.GetLineItemsBySessionIdAsync(sessionId, CancellationToken.None);
        DetailLines = new ObservableCollection<WeighingSessionLineRow>(lines.Select(x => new WeighingSessionLineRow(x)));
    }

    private async Task ExecutePrintFlowAsync(PrintDocumentKind kind, Guid sessionId, string displayName)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var context = await LoadPrintContextAsync(scope, sessionId);
            if (context == null)
            {
                _toastService.ShowWarning(string.Format(UiText.Weighing.NoPrintableDocumentFormat, displayName));
                return;
            }

            var templateProvider = scope.ServiceProvider.GetRequiredService<IPrintTemplateProvider>();
            var printerDiscovery = scope.ServiceProvider.GetRequiredService<IPrinterDiscoveryService>();
            var printService = scope.ServiceProvider.GetRequiredService<IPrintService>();
            var renderer = scope.ServiceProvider.GetRequiredService<PrintOverlayRenderer>();
            var template = await templateProvider.GetTemplateAsync(kind, CancellationToken.None);
            var preview = BuildPrintBatchPreview(scope, context, kind);
            if (preview.Pages.Count == 0)
            {
                _toastService.ShowWarning(string.Format(UiText.Weighing.NoPrintableDocumentFormat, displayName));
                return;
            }

            var dialogVm = new PrintOptionsDialogViewModel(
                kind == PrintDocumentKind.WeighTicket ? UiText.Weighing.PrintDialogWeighTicket : UiText.Weighing.PrintDialogDeliveryTicket,
                template,
                preview,
                printerDiscovery.GetInstalledPrinters(),
                renderer,
                templateProvider,
                StationAuthorization.CanManagePrintLayout(_currentUserContext.RoleCode));

            var printOptions = await _dialogService.ShowCustomDialogAsync<PrintOptionsDialogViewModel, PrintOptionsModel>(dialogVm);
            if (printOptions == null)
            {
                return;
            }

            var result = await printService.PrintAsync(template, preview, printOptions, CancellationToken.None);
            await PersistPrintResultAsync(scope, context, kind, result);

            if (result.HasFailures)
            {
                _toastService.ShowError(string.Format(UiText.Weighing.PrintErrorFormat, displayName));
                return;
            }

            _toastService.ShowSuccess(string.Format(UiText.Weighing.PrintSuccessFormat, displayName));
            await ReloadAndReselectAsync(sessionId);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Outgoing print flow failed");
            _toastService.ShowError(string.Format(UiText.Weighing.PrintErrorFormat, displayName));
        }
    }

    private async Task ReloadAndReselectAsync(Guid sessionId)
    {
        await LoadVehiclesAsync();
        SelectedVehicle = Vehicles.FirstOrDefault(x => x.SessionId == sessionId);
        if (IsDetailsVisible && SelectedVehicle != null)
        {
            await LoadSessionDetailsAsync(sessionId);
        }
    }

    private async Task<SessionPrintContext?> LoadPrintContextAsync(IServiceScope scope, Guid sessionId)
    {
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IWeighingSessionRepository>();
        var regRepo = scope.ServiceProvider.GetRequiredService<IVehicleRegistrationRepository>();
        var weighRepo = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();
        var deliveryRepo = scope.ServiceProvider.GetRequiredService<IDeliveryTicketRepository>();
        var vehicleRepo = scope.ServiceProvider.GetRequiredService<IVehicleRepository>();

        var session = await sessionRepo.GetByIdAsync(sessionId, CancellationToken.None);
        if (session == null)
        {
            return null;
        }

        var lines = await sessionRepo.GetLinesBySessionIdAsync(sessionId, CancellationToken.None);
        var registrations = await regRepo.GetByWeighingSessionIdAsync(sessionId, CancellationToken.None);
        var registrationsById = registrations.ToDictionary(x => x.Id);
        var weighTickets = await weighRepo.GetByWeighingSessionIdAsync(sessionId, CancellationToken.None);
        var deliveryTickets = await deliveryRepo.GetByWeighingSessionIdAsync(sessionId, CancellationToken.None);
        var vehicle = await vehicleRepo.GetByPlateAndMoocAsync(session.VehiclePlate, session.MoocNumber ?? string.Empty, CancellationToken.None)
            ?? (await vehicleRepo.GetByPlateAsync(session.VehiclePlate, CancellationToken.None)).FirstOrDefault();

        return new SessionPrintContext(session, lines, registrationsById, weighTickets, deliveryTickets, vehicle);
    }

    private PrintBatchPreviewModel BuildPrintBatchPreview(IServiceScope scope, SessionPrintContext context, PrintDocumentKind kind)
    {
        var printedAtLocal = _clock.NowLocal;
        var splitConfirmed = context.MasterSession.OverweightResolutionStatus == OverweightResolutionStatus.SPLIT_CONFIRMED;

        if (kind == PrintDocumentKind.WeighTicket)
        {
            var composer = scope.ServiceProvider.GetRequiredService<IWeighTicketPrintComposer>();
            var primaryRegistration = context.RegistrationsById.Values.OrderBy(x => x.CreatedAt).FirstOrDefault();
            if (primaryRegistration == null)
            {
                return new PrintBatchPreviewModel { Kind = kind, Title = UiText.Weighing.PrintPreviewWeigh, Pages = [] };
            }

            var ticketsToPrint = splitConfirmed
                ? context.WeighTickets.Where(x => x.RecordRole == WeighTicketRecordRoles.SplitDerived && !x.IsDeleted).OrderBy(x => x.SplitSequence)
                : context.WeighTickets.Where(x => x.RecordRole == WeighTicketRecordRoles.MasterSession && !x.IsDeleted).Take(1);

            return new PrintBatchPreviewModel
            {
                Kind = kind,
                Title = splitConfirmed ? "In phiếu cân tách tải" : UiText.Weighing.PrintPreviewWeighMaster,
                Pages = ticketsToPrint.Select(ticket => composer.Compose(primaryRegistration, ticket, context.Vehicle, printedAtLocal, _currentUserContext.DisplayName)).Cast<PrintPreviewPageModel>().ToList()
            };
        }

        var deliveryComposer = scope.ServiceProvider.GetRequiredService<IDeliveryTicketPrintComposer>();
        var pages = new List<PrintPreviewPageModel>();
        var deliveryTicketsToPrint = splitConfirmed
            ? context.DeliveryTickets.Where(x => x.RecordRole == DeliveryTicketRecordRoles.SplitDerived && !x.IsDeleted).OrderBy(x => x.SplitSequence).ThenBy(x => x.CreatedAt)
            : context.DeliveryTickets.Where(x => x.RecordRole == DeliveryTicketRecordRoles.Normal && !x.IsDeleted).OrderBy(x => x.WeighingSessionLineId).ThenBy(x => x.CreatedAt);

        foreach (var ticket in deliveryTicketsToPrint)
        {
            if (!ticket.WeighingSessionLineId.HasValue)
            {
                continue;
            }

            var line = context.Lines.FirstOrDefault(x => x.Id == ticket.WeighingSessionLineId.Value);
            if (line == null || !context.RegistrationsById.TryGetValue(line.VehicleRegistrationId, out var registration))
            {
                continue;
            }

            var relatedWeighTicket = splitConfirmed
                ? context.WeighTickets.FirstOrDefault(x => x.RecordRole == WeighTicketRecordRoles.SplitDerived && x.SplitGroupId == ticket.SplitGroupId && !x.IsDeleted)
                : context.WeighTickets.FirstOrDefault(x => x.RecordRole == WeighTicketRecordRoles.MasterSession && !x.IsDeleted);

            pages.Add(deliveryComposer.Compose(registration, ticket, relatedWeighTicket, line, context.Vehicle, printedAtLocal, _currentUserContext.DisplayName));
        }

        return new PrintBatchPreviewModel
        {
            Kind = kind,
            Title = UiText.Weighing.PrintPreviewDelivery,
            Pages = pages
        };
    }

    private async Task PersistPrintResultAsync(IServiceScope scope, SessionPrintContext context, PrintDocumentKind kind, PrintExecutionResult result)
    {
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IWeighingSessionRepository>();
        var weighRepo = scope.ServiceProvider.GetRequiredService<IWeighTicketRepository>();
        var deliveryRepo = scope.ServiceProvider.GetRequiredService<IDeliveryTicketRepository>();
        var now = _clock.NowLocal;
        var splitConfirmed = context.MasterSession.OverweightResolutionStatus == OverweightResolutionStatus.SPLIT_CONFIRMED;

        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().ExecuteInTransactionAsync(async innerCt =>
        {
            if (kind == PrintDocumentKind.WeighTicket)
            {
                var ticketsToPersist = splitConfirmed
                    ? context.WeighTickets.Where(x => x.RecordRole == WeighTicketRecordRoles.SplitDerived && !x.IsDeleted)
                    : context.WeighTickets.Where(x => x.RecordRole == WeighTicketRecordRoles.MasterSession && !x.IsDeleted);

                foreach (var weighTicket in ticketsToPersist)
                {
                    var ticketResult = result.Documents.FirstOrDefault(x => x.DocumentId == weighTicket.Id);
                    if (ticketResult == null || !ticketResult.Success)
                    {
                        continue;
                    }

                    weighTicket.IsPrinted = true;
                    weighTicket.LastPrintedAt = now;
                    weighTicket.LastPrintError = null;
                    weighTicket.UpdatedAt = now;
                    weighTicket.UpdatedBy = _currentUserContext.Username;
                    await weighRepo.UpdateAsync(weighTicket, innerCt);
                }

                if (!splitConfirmed && !result.HasFailures)
                {
                    context.MasterSession.HasPrintedMasterWeighTicket = true;
                    context.MasterSession.UpdatedAt = now;
                    context.MasterSession.UpdatedBy = _currentUserContext.Username;
                    await sessionRepo.UpdateAsync(context.MasterSession, innerCt);
                }

                return;
            }

            foreach (var deliveryTicket in context.DeliveryTickets.Where(x => !x.IsDeleted))
            {
                var ticketResult = result.Documents.FirstOrDefault(x => x.DocumentId == deliveryTicket.Id);
                if (ticketResult == null || !ticketResult.Success)
                {
                    continue;
                }

                deliveryTicket.IsPrinted = true;
                deliveryTicket.LastPrintedAt = now;
                deliveryTicket.LastPrintError = null;
                deliveryTicket.UpdatedAt = now;
                deliveryTicket.UpdatedBy = _currentUserContext.Username;
                await deliveryRepo.UpdateAsync(deliveryTicket, innerCt);
            }

            foreach (var line in context.Lines)
            {
                var relevantTickets = splitConfirmed
                    ? context.DeliveryTickets.Where(x => x.RecordRole == DeliveryTicketRecordRoles.SplitDerived && x.WeighingSessionLineId == line.Id && !x.IsDeleted).ToList()
                    : context.DeliveryTickets.Where(x => x.RecordRole == DeliveryTicketRecordRoles.Normal && x.WeighingSessionLineId == line.Id && !x.IsDeleted).ToList();

                line.HasPrintedDeliveryTicket = relevantTickets.Count > 0 && relevantTickets.All(x => x.IsPrinted);
                line.UpdatedAt = now;
                line.UpdatedBy = _currentUserContext.Username;
                await sessionRepo.UpdateLineAsync(line, innerCt);
            }
        }, CancellationToken.None);
    }

    private sealed record SessionPrintContext(
        WeighingSession MasterSession,
        IReadOnlyList<WeighingSessionLine> Lines,
        IReadOnlyDictionary<Guid, VehicleRegistration> RegistrationsById,
        IReadOnlyList<WeighTicket> WeighTickets,
        IReadOnlyList<DeliveryTicket> DeliveryTickets,
        Vehicle? Vehicle);
}
