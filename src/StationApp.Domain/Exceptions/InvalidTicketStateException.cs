namespace StationApp.Domain.Exceptions;

public class InvalidTicketStateException : Exception
{
    public InvalidTicketStateException(string message) : base(message) { }
}
