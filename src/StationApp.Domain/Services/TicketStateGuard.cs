using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Domain.Exceptions;

namespace StationApp.Domain.Services;

public static class TicketStateGuard
{
    public static void EnsureCanCaptureWeight1(WeighTicket ticket)
    {
        if (ticket.IsCancelled)
            throw new InvalidTicketStateException("Cannot capture weight on a cancelled ticket.");
        if (ticket.Status != TicketStatus.TICKET_CREATED)
            throw new InvalidTicketStateException(
                $"Cannot capture weight 1. Expected status TICKET_CREATED, got {ticket.Status}.");
    }

    public static void EnsureCanCaptureWeight2(WeighTicket ticket)
    {
        if (ticket.IsCancelled)
            throw new InvalidTicketStateException("Cannot capture weight on a cancelled ticket.");
        if (ticket.Status != TicketStatus.LOADING_STARTED)
            throw new InvalidTicketStateException(
                $"Cannot capture weight 2. Expected status LOADING_STARTED, got {ticket.Status}.");
        if (ticket.Weight1 is null)
            throw new InvalidTicketStateException("Cannot capture weight 2 before weight 1.");
    }

    public static void EnsureCanComplete(WeighTicket ticket)
    {
        if (ticket.Status != TicketStatus.LOADING_STARTED)
            throw new InvalidTicketStateException(
                $"Cannot complete. Expected status LOADING_STARTED, got {ticket.Status}.");
        if (ticket.Weight1 is null || ticket.Weight2 is null)
            throw new InvalidTicketStateException("Cannot complete ticket without both weights.");
    }

    public static void EnsureCanCancel(WeighTicket ticket)
    {
        if (ticket.Status == TicketStatus.TICKET_COMPLETED)
            throw new InvalidTicketStateException("Cannot cancel a completed ticket.");
        if (ticket.Status == TicketStatus.TICKET_CANCELLED)
            throw new InvalidTicketStateException("Ticket is already cancelled.");
    }
}
