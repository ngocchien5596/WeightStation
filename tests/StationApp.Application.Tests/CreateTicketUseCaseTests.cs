using NSubstitute;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using Xunit;

namespace StationApp.Application.Tests;

public class CreateTicketUseCaseTests
{
    private readonly ITicketRepository _ticketRepo = Substitute.For<ITicketRepository>();
    private readonly ISyncOutboxRepository _outboxRepo = Substitute.For<ISyncOutboxRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ITicketNumberGenerator _ticketNoGen = Substitute.For<ITicketNumberGenerator>();
    private readonly IAppVersionProvider _versionProvider = Substitute.For<IAppVersionProvider>();
    private readonly ICurrentUserContext _userContext = Substitute.For<ICurrentUserContext>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IAuditService _audit = Substitute.For<IAuditService>();
    private readonly ISyncPayloadFactory _payloadFactory = Substitute.For<ISyncPayloadFactory>();

    private CreateTicketUseCase CreateSut()
    {
        _ticketNoGen.GenerateAsync(Arg.Any<CancellationToken>()).Returns("QN26040001");
        _versionProvider.GetVersion().Returns("1.0.0");
        _userContext.Username.Returns("admin");
        _clock.NowLocal.Returns(new DateTime(2026, 4, 24, 0, 0, 0, DateTimeKind.Unspecified));
        _payloadFactory.CreatePayload(Arg.Any<WeighTicket>()).Returns("{}");
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(ci => ci.Arg<Func<CancellationToken, Task>>().Invoke(CancellationToken.None));
        return new CreateTicketUseCase(_ticketRepo, _outboxRepo, _uow, _ticketNoGen, _versionProvider, _userContext, _clock, _audit, _payloadFactory);
    }

    [Fact]
    public async Task CreateTicket_Generates_Valid_TicketNo()
    {
        var sut = CreateSut();
        var result = await sut.ExecuteAsync(new CreateTicketRequest("ABC-1234", TransactionType.OUTBOUND), CancellationToken.None);
        Assert.True(result.Success);
        Assert.Equal("QN26040001", result.Data!.TicketNo);
    }

    [Fact]
    public async Task CreateTicket_Sets_Initial_Status()
    {
        var sut = CreateSut();
        var result = await sut.ExecuteAsync(new CreateTicketRequest("ABC-1234", TransactionType.OUTBOUND), CancellationToken.None);
        Assert.Equal(TicketStatus.TICKET_CREATED, result.Data!.Status);
        Assert.Equal(SyncStatus.SYNC_QUEUED, result.Data!.SyncStatus);
    }

    [Fact]
    public async Task CreateTicket_Generates_IdempotencyKey()
    {
        var sut = CreateSut();
        var result = await sut.ExecuteAsync(new CreateTicketRequest("ABC-1234", TransactionType.OUTBOUND), CancellationToken.None);
        Assert.NotEqual(Guid.Empty, result.Data!.IdempotencyKey);
    }

    [Fact]
    public async Task CreateTicket_Sets_AppVersion()
    {
        var sut = CreateSut();
        var result = await sut.ExecuteAsync(new CreateTicketRequest("ABC-1234", TransactionType.OUTBOUND), CancellationToken.None);
        Assert.Equal("1.0.0", result.Data!.AppVersion);
    }

    [Fact]
    public async Task CreateTicket_Enqueues_Outbox()
    {
        var sut = CreateSut();
        await sut.ExecuteAsync(new CreateTicketRequest("ABC-1234", TransactionType.OUTBOUND), CancellationToken.None);
        await _outboxRepo.Received(1).EnqueueAsync(Arg.Any<SyncOutbox>(), Arg.Any<CancellationToken>());
    }
}
