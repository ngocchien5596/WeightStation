namespace StationApp.Domain.Exceptions;

public class TicketNotFoundException : Exception
{
    public TicketNotFoundException(Guid ticketId)
        : base($"Ticket with ID '{ticketId}' was not found.") { }

    public TicketNotFoundException(string ticketNo)
        : base($"Ticket with No '{ticketNo}' was not found.") { }
}
