using Xunit;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using StationApp.Domain.Exceptions;
using StationApp.Domain.Services;

namespace StationApp.Domain.Tests;

public class TicketStateGuardTests
{
    [Fact]
    public void Cannot_CaptureWeight1_On_Cancelled_Ticket()
    {
        var ticket = new WeighTicket { Status = TicketStatus.TICKET_CREATED, IsCancelled = true };
        Assert.Throws<InvalidTicketStateException>(() => TicketStateGuard.EnsureCanCaptureWeight1(ticket));
    }

    [Fact]
    public void Cannot_CaptureWeight1_When_Status_Not_Created()
    {
        var ticket = new WeighTicket { Status = TicketStatus.LOADING_STARTED };
        Assert.Throws<InvalidTicketStateException>(() => TicketStateGuard.EnsureCanCaptureWeight1(ticket));
    }

    [Fact]
    public void Cannot_CaptureWeight2_Without_Weight1()
    {
        var ticket = new WeighTicket { Status = TicketStatus.LOADING_STARTED, Weight1 = null };
        Assert.Throws<InvalidTicketStateException>(() => TicketStateGuard.EnsureCanCaptureWeight2(ticket));
    }

    [Fact]
    public void Cannot_Complete_Without_Both_Weights()
    {
        var ticket = new WeighTicket { Status = TicketStatus.LOADING_STARTED, Weight1 = 10000m, Weight2 = null };
        Assert.Throws<InvalidTicketStateException>(() => TicketStateGuard.EnsureCanComplete(ticket));
    }

    [Fact]
    public void Cannot_Cancel_Completed_Ticket()
    {
        var ticket = new WeighTicket { Status = TicketStatus.TICKET_COMPLETED };
        Assert.Throws<InvalidTicketStateException>(() => TicketStateGuard.EnsureCanCancel(ticket));
    }

    [Fact]
    public void Cannot_Cancel_Already_Cancelled_Ticket()
    {
        var ticket = new WeighTicket { Status = TicketStatus.TICKET_CANCELLED };
        Assert.Throws<InvalidTicketStateException>(() => TicketStateGuard.EnsureCanCancel(ticket));
    }

    [Fact]
    public void Can_Cancel_Created_Ticket()
    {
        var ticket = new WeighTicket { Status = TicketStatus.TICKET_CREATED };
        TicketStateGuard.EnsureCanCancel(ticket); // should not throw
    }

    [Fact]
    public void Can_Cancel_Loading_Started_Ticket()
    {
        var ticket = new WeighTicket { Status = TicketStatus.LOADING_STARTED };
        TicketStateGuard.EnsureCanCancel(ticket); // should not throw
    }
}
