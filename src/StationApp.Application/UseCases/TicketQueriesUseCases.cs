using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using StationApp.Application.DTOs;
using StationApp.Application.Formatting;
using StationApp.Application.Interfaces;

namespace StationApp.Application.UseCases;

public class GetWeightViewTicketsUseCase
{
    private readonly ICutOrderRepository _regRepo;

    public GetWeightViewTicketsUseCase(ICutOrderRepository regRepo)
    {
        _regRepo = regRepo;
    }

    public async Task<IReadOnlyList<WeightViewListItem>> ExecuteAsync(string? keyword, CancellationToken ct)
    {
        return await _regRepo.GetWeightViewListAsync(keyword, ct);
    }
}

public class GetRelatedTicketsUseCase
{
    private readonly IWeighTicketRepository _ticketRepo;
    private readonly IDeliveryTicketRepository _deliveryTicketRepo;

    public GetRelatedTicketsUseCase(IWeighTicketRepository ticketRepo, IDeliveryTicketRepository deliveryTicketRepo)
    {
        _ticketRepo = ticketRepo;
        _deliveryTicketRepo = deliveryTicketRepo;
    }

    public async Task<IReadOnlyList<RelatedDocumentListItem>> ExecuteAsync(Guid registrationId, CancellationToken ct)
    {
        var weighTickets = await _ticketRepo.GetByCutOrderIdAsync(registrationId, ct);
        var deliveryTickets = await _deliveryTicketRepo.GetByCutOrderIdAsync(registrationId, ct);

        var items = weighTickets
            .Select(ticket => new RelatedDocumentListItem(
                "PHIEU CAN",
                BusinessNumberFormatter.ToDisplay(ticket.TicketNo),
                null,
                ticket.RecordRole,
                ticket.SplitSequence,
                ticket.Weight1,
                ticket.Weight2,
                ticket.NetWeight,
                ticket.CreatedAt))
            .Concat(deliveryTickets.Select(ticket => new RelatedDocumentListItem(
                "PHIEU GIAO NHAN",
                null,
                BusinessNumberFormatter.ToDisplay(ticket.DeliveryNo),
                ticket.RecordRole,
                ticket.SplitSequence,
                null,
                null,
                null,
                ticket.CreatedAt)))
            .OrderBy(item => item.SplitSequence ?? byte.MaxValue)
            .ThenBy(item => item.DocumentType)
            .ThenBy(item => item.CreatedAt)
            .ToList();

        return items.AsReadOnly();
    }
}


