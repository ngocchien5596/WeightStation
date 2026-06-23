using NSubstitute;
using StationApp.Application.DTOs;
using StationApp.Application.Interfaces;
using StationApp.Application.UseCases;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using Xunit;

namespace StationApp.Application.Tests;

public class ExportScaleUseCasesTests
{
    private readonly ICutOrderRepository _cutOrderRepo = Substitute.For<ICutOrderRepository>();
    private readonly ICustomerRepository _customerRepo = Substitute.For<ICustomerRepository>();
    private readonly IProductRepository _productRepo = Substitute.For<IProductRepository>();
    private readonly IWeighingSessionRepository _sessionRepo = Substitute.For<IWeighingSessionRepository>();
    private readonly IWeighTicketRepository _weighRepo = Substitute.For<IWeighTicketRepository>();
    private readonly IDeliveryTicketRepository _deliveryRepo = Substitute.For<IDeliveryTicketRepository>();
    private readonly ISyncOutboxRepository _syncOutboxRepo = Substitute.For<ISyncOutboxRepository>();
    private readonly IUnitOfWork _uow = Substitute.For<IUnitOfWork>();
    private readonly ISyncPayloadFactory _syncPayloadFactory = Substitute.For<ISyncPayloadFactory>();
    private readonly ICurrentUserContext _userContext = Substitute.For<ICurrentUserContext>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public ExportScaleUseCasesTests()
    {
        _userContext.Username.Returns("tester");
        _clock.NowLocal.Returns(new DateTime(2026, 6, 20, 10, 30, 0));
        _syncPayloadFactory.CreatePayload(Arg.Any<Customer>()).Returns("{}");
        _syncPayloadFactory.CreatePayload(Arg.Any<Product>()).Returns("{}");
        _uow.ExecuteInTransactionAsync(Arg.Any<Func<CancellationToken, Task>>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                var action = call.ArgAt<Func<CancellationToken, Task>>(0);
                await action(call.ArgAt<CancellationToken>(1));
            });
    }

    [Fact]
    public async Task CreateTemporaryExportCutOrder_PersistsNormalizedValues_AndCalculatedBagCount()
    {
        _cutOrderRepo.GenerateTemporaryExportDisplayCodeAsync(Arg.Any<CancellationToken>())
            .Returns("XK-TAM-0001");
        _customerRepo.GetByCodeAsync("C001", Arg.Any<CancellationToken>()).Returns((Customer?)null);
        _productRepo.GetByCodeAsync("PCB40", Arg.Any<CancellationToken>()).Returns((Product?)null);

        var sut = CreateTemporaryExportCutOrderUseCase();

        var cutOrderId = await sut.ExecuteAsync(
            new CreateTemporaryExportCutOrderRequest(
                CustomerCode: " C001 ",
                CustomerName: " Cong ty A ",
                ProductCode: " PCB40 ",
                ProductName: " Xi mang bao ",
                ProductType: ProductTypes.Bagged,
                PlannedWeight: 4550m,
                TareWeightKg: 1200m,
                BagWeightKg: 50m,
                Notes: " Giao gap "),
            CancellationToken.None);

        await _cutOrderRepo.Received(1).AddAsync(
            Arg.Is<CutOrder>(x =>
                x.Id == cutOrderId
                && x.CutOrderSource == CutOrderSource.MANUAL
                && x.CutOrderStatus == CutOrderStatus.IN_SESSION
                && x.TransactionType == TransactionType.OUTBOUND
                && x.VehiclePlate == "XK-TAM-0001"
                && x.CustomerCode == "C001"
                && x.CustomerName == "Cong ty A"
                && x.ProductCode == "PCB40"
                && x.ProductName == "Xi mang bao"
                && x.ProductType == ProductTypes.Bagged
                && x.PlannedWeight == 4550m
                && x.TareWeightKg == 1200m
                && x.BagWeightKg == 50m
                && x.BagCount == 91
                && x.Notes == "Giao gap"
                && x.IsExportScale
                && x.IsTemporaryExport
                && x.TemporaryExportDisplayCode == "XK-TAM-0001"
                && x.CreatedBy == "tester"),
            Arg.Any<CancellationToken>());

        await _customerRepo.Received(1).AddAsync(
            Arg.Is<Customer>(x =>
                x.CustomerCode == "C001"
                && x.CustomerName == "Cong ty A"
                && x.IsActive
                && x.CreatedBy == "tester"),
            Arg.Any<CancellationToken>());

        await _productRepo.Received(1).AddAsync(
            Arg.Is<Product>(x =>
                x.ProductCode == "PCB40"
                && x.ProductName == "Xi mang bao"
                && x.ProductType == ProductTypes.Bagged
                && x.IsActive
                && x.CreatedBy == "tester"),
            Arg.Any<CancellationToken>());

        await _syncOutboxRepo.Received(2).EnqueueAsync(
            Arg.Is<SyncOutbox>(x =>
                x.AggregateType == SyncAggregateTypes.Customer
                || x.AggregateType == SyncAggregateTypes.Product),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTemporaryExportCutOrder_RoundsBagCount_AwayFromZero()
    {
        _cutOrderRepo.GenerateTemporaryExportDisplayCodeAsync(Arg.Any<CancellationToken>())
            .Returns("XK-TAM-0002");

        var sut = CreateTemporaryExportCutOrderUseCase();

        await sut.ExecuteAsync(
            new CreateTemporaryExportCutOrderRequest(
                CustomerCode: "C001",
                CustomerName: "Cong ty A",
                ProductCode: "P001",
                ProductName: "Xi mang bao",
                PlannedWeight: 4575m,
                TareWeightKg: 1000m,
                BagWeightKg: 50m),
            CancellationToken.None);

        await _cutOrderRepo.Received(1).AddAsync(
            Arg.Is<CutOrder>(x => x.BagCount == 92),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTemporaryExportCutOrder_UpdatesExistingMasterData_WhenDifferent()
    {
        _cutOrderRepo.GenerateTemporaryExportDisplayCodeAsync(Arg.Any<CancellationToken>())
            .Returns("XK-TAM-0008");
        _customerRepo.GetByCodeAsync("C001", Arg.Any<CancellationToken>()).Returns(new Customer
        {
            Id = Guid.NewGuid(),
            CustomerCode = "C001",
            CustomerName = "Ten cu",
            IsActive = false
        });
        _productRepo.GetByCodeAsync("P001", Arg.Any<CancellationToken>()).Returns(new Product
        {
            Id = Guid.NewGuid(),
            ProductCode = "P001",
            ProductName = "Hang cu",
            ProductType = "ROI_XA",
            IsActive = false
        });

        var sut = CreateTemporaryExportCutOrderUseCase();

        await sut.ExecuteAsync(
            new CreateTemporaryExportCutOrderRequest(
                CustomerCode: "C001",
                CustomerName: "Cong ty moi",
                ProductCode: "P001",
                ProductName: "Xi mang bao",
                ProductType: ProductTypes.Bagged,
                PlannedWeight: 4550m,
                TareWeightKg: 1000m,
                BagWeightKg: 50m),
            CancellationToken.None);

        await _customerRepo.Received(1).UpdateAsync(
            Arg.Is<Customer>(x =>
                x.CustomerCode == "C001"
                && x.CustomerName == "Cong ty moi"
                && x.IsActive
                && x.UpdatedBy == "tester"),
            Arg.Any<CancellationToken>());

        await _productRepo.Received(1).UpdateAsync(
            Arg.Is<Product>(x =>
                x.ProductCode == "P001"
                && x.ProductName == "Xi mang bao"
                && x.ProductType == ProductTypes.Bagged
                && x.IsActive
                && x.UpdatedBy == "tester"),
            Arg.Any<CancellationToken>());

        await _syncOutboxRepo.Received(2).EnqueueAsync(Arg.Any<SyncOutbox>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTemporaryExportCutOrder_RejectsMissingCustomerName()
    {
        _cutOrderRepo.GenerateTemporaryExportDisplayCodeAsync(Arg.Any<CancellationToken>())
            .Returns("XK-TAM-0003");

        var sut = CreateTemporaryExportCutOrderUseCase();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(
                new CreateTemporaryExportCutOrderRequest(
                    CustomerCode: "C001",
                    CustomerName: " ",
                    ProductCode: "P001",
                    ProductName: "Xi mang bao",
                    PlannedWeight: 4550m,
                    TareWeightKg: 1000m,
                    BagWeightKg: 50m),
                CancellationToken.None));

        Assert.Equal("Khách hàng là bắt buộc.", ex.Message);
    }

    [Fact]
    public async Task CreateTemporaryExportCutOrder_RejectsMissingCustomerCode()
    {
        _cutOrderRepo.GenerateTemporaryExportDisplayCodeAsync(Arg.Any<CancellationToken>())
            .Returns("XK-TAM-0003A");

        var sut = CreateTemporaryExportCutOrderUseCase();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(
                new CreateTemporaryExportCutOrderRequest(
                    CustomerCode: " ",
                    CustomerName: "Cong ty A",
                    ProductCode: "P001",
                    ProductName: "Xi mang bao",
                    PlannedWeight: 4550m,
                    TareWeightKg: 1000m,
                    BagWeightKg: 50m),
                CancellationToken.None));

        Assert.Equal("Mã khách hàng là bắt buộc.", ex.Message);
    }

    [Fact]
    public async Task CreateTemporaryExportCutOrder_RejectsMissingProductName()
    {
        _cutOrderRepo.GenerateTemporaryExportDisplayCodeAsync(Arg.Any<CancellationToken>())
            .Returns("XK-TAM-0004");

        var sut = CreateTemporaryExportCutOrderUseCase();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(
                new CreateTemporaryExportCutOrderRequest(
                    CustomerCode: "C001",
                    CustomerName: "Cong ty A",
                    ProductCode: "P001",
                    ProductName: "",
                    PlannedWeight: 4550m,
                    TareWeightKg: 1000m,
                    BagWeightKg: 50m),
                CancellationToken.None));

        Assert.Equal("Sản phẩm là bắt buộc.", ex.Message);
    }

    [Fact]
    public async Task CreateTemporaryExportCutOrder_RejectsMissingProductCode()
    {
        _cutOrderRepo.GenerateTemporaryExportDisplayCodeAsync(Arg.Any<CancellationToken>())
            .Returns("XK-TAM-0004A");

        var sut = CreateTemporaryExportCutOrderUseCase();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(
                new CreateTemporaryExportCutOrderRequest(
                    CustomerCode: "C001",
                    CustomerName: "Cong ty A",
                    ProductCode: "",
                    ProductName: "Xi mang bao",
                    PlannedWeight: 4550m,
                    TareWeightKg: 1000m,
                    BagWeightKg: 50m),
                CancellationToken.None));

        Assert.Equal("Mã sản phẩm là bắt buộc.", ex.Message);
    }

    [Fact]
    public async Task CreateTemporaryExportCutOrder_RejectsNonPositivePlannedWeight()
    {
        _cutOrderRepo.GenerateTemporaryExportDisplayCodeAsync(Arg.Any<CancellationToken>())
            .Returns("XK-TAM-0005");

        var sut = CreateTemporaryExportCutOrderUseCase();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(
                new CreateTemporaryExportCutOrderRequest(
                    CustomerCode: "C001",
                    CustomerName: "Cong ty A",
                    ProductCode: "P001",
                    ProductName: "Xi mang bao",
                    PlannedWeight: 0m,
                    TareWeightKg: 1000m,
                    BagWeightKg: 50m),
                CancellationToken.None));

        Assert.Equal("Số lượng đặt (kg) phải lớn hơn 0.", ex.Message);
    }

    [Fact]
    public async Task CreateTemporaryExportCutOrder_RejectsNegativeTareWeight()
    {
        _cutOrderRepo.GenerateTemporaryExportDisplayCodeAsync(Arg.Any<CancellationToken>())
            .Returns("XK-TAM-0006");

        var sut = CreateTemporaryExportCutOrderUseCase();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(
                new CreateTemporaryExportCutOrderRequest(
                    CustomerCode: "C001",
                    CustomerName: "Cong ty A",
                    ProductCode: "P001",
                    ProductName: "Xi mang bao",
                    PlannedWeight: 4550m,
                    TareWeightKg: -1m,
                    BagWeightKg: 50m),
                CancellationToken.None));

        Assert.Equal("Trọng lượng vỏ (kg) phải lớn hơn hoặc bằng 0.", ex.Message);
    }

    [Fact]
    public async Task CreateTemporaryExportCutOrder_AcceptsZeroBagWeight()
    {
        _cutOrderRepo.GenerateTemporaryExportDisplayCodeAsync(Arg.Any<CancellationToken>())
            .Returns("XK-TAM-0007");
        _customerRepo.GetByCodeAsync("C001", Arg.Any<CancellationToken>()).Returns((Customer?)null);
        _productRepo.GetByCodeAsync("P001", Arg.Any<CancellationToken>()).Returns((Product?)null);

        var sut = CreateTemporaryExportCutOrderUseCase();

        var cutOrderId = await sut.ExecuteAsync(
            new CreateTemporaryExportCutOrderRequest(
                CustomerCode: "C001",
                CustomerName: "Cong ty A",
                ProductCode: "P001",
                ProductName: "Xi mang bao",
                ProductType: ProductTypes.Bagged,
                PlannedWeight: 4550m,
                TareWeightKg: 1000m,
                BagWeightKg: 0m),
            CancellationToken.None);

        await _cutOrderRepo.Received(1).AddAsync(
            Arg.Is<CutOrder>(x =>
                x.Id == cutOrderId
                && x.BagWeightKg == 0m
                && x.BagCount == 0),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateTemporaryExportCutOrder_RejectsNegativeBagWeight()
    {
        _cutOrderRepo.GenerateTemporaryExportDisplayCodeAsync(Arg.Any<CancellationToken>())
            .Returns("XK-TAM-0007-Neg");

        var sut = CreateTemporaryExportCutOrderUseCase();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.ExecuteAsync(
                new CreateTemporaryExportCutOrderRequest(
                    CustomerCode: "C001",
                    CustomerName: "Cong ty A",
                    ProductCode: "P001",
                    ProductName: "Xi mang bao",
                    PlannedWeight: 4550m,
                    TareWeightKg: 1000m,
                    BagWeightKg: -1m),
                CancellationToken.None));

        Assert.Equal("Trọng lượng bao (kg) phải lớn hơn hoặc bằng 0.", ex.Message);
    }

    [Fact]
    public async Task MapTemporaryExportCutOrder_CopiesBagFields_AndReassignsLinkedRecords()
    {
        var temporaryCutOrderId = Guid.NewGuid();
        var realCutOrderId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var weighTicketId = Guid.NewGuid();
        var deliveryTicketId = Guid.NewGuid();

        var temporaryCutOrder = new CutOrder
        {
            Id = temporaryCutOrderId,
            IsTemporaryExport = true,
            IsExportScale = true,
            TransactionType = TransactionType.OUTBOUND,
            CutOrderStatus = CutOrderStatus.IN_SESSION,
            ProcessingStage = ProcessingStage.WEIGHING,
            CustomerCode = "TMP-CUS",
            CustomerName = "Khach tam",
            ProductCode = "TMP-PROD",
            ProductName = "Hang tam",
            PlannedWeight = 4550m,
            BagCount = 91,
            TareWeightKg = 1200m,
            BagWeightKg = 50m
        };
        var realCutOrder = new CutOrder
        {
            Id = realCutOrderId,
            ErpCutOrderId = "QN.CL.2606/0099",
            IsTemporaryExport = false,
            IsExportScale = false,
            TransactionType = TransactionType.OUTBOUND,
            CutOrderStatus = CutOrderStatus.REGISTERED,
            ProcessingStage = ProcessingStage.IN_YARD,
            CustomerCode = "ERP-CUS",
            CustomerName = "Khach ERP",
            ProductCode = "ERP-PROD",
            ProductName = "Hang ERP",
            PlannedWeight = 4600m,
            TransportMethod = TransportMethod.ROAD,
            Notes = "ERP note"
        };
        var session = new WeighingSession
        {
            Id = sessionId,
            SessionNo = "QN01-LC26060001",
            SessionStatus = WeighingSessionStatus.PENDING_WEIGHT2
        };
        var line = new WeighingSessionLine
        {
            Id = lineId,
            WeighingSessionId = sessionId,
            CutOrderId = temporaryCutOrderId
        };
        var weighTicket = new WeighTicket
        {
            Id = weighTicketId,
            CutOrderId = temporaryCutOrderId,
            WeighingSessionId = sessionId
        };
        var deliveryTicket = new DeliveryTicket
        {
            Id = deliveryTicketId,
            CutOrderId = temporaryCutOrderId,
            WeighingSessionId = sessionId
        };

        _cutOrderRepo.GetByIdAsync(temporaryCutOrderId, Arg.Any<CancellationToken>()).Returns(temporaryCutOrder);
        _cutOrderRepo.GetByIdAsync(realCutOrderId, Arg.Any<CancellationToken>()).Returns(realCutOrder);
        _cutOrderRepo.GetExportVehicleTripsAsync(temporaryCutOrderId, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new ExportVehicleTripListItem(
                    sessionId,
                    lineId,
                    "QN01-LC26060001",
                    "14C-12345",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    WeighingSessionStatus.PENDING_WEIGHT2,
                    null,
                    null,
                    false,
                    false)
            });
        _sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);
        _sessionRepo.GetLinesBySessionIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(new[] { line });
        _weighRepo.GetByWeighingSessionIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(new[] { weighTicket });
        _deliveryRepo.GetByWeighingSessionIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(new[] { deliveryTicket });
        _weighRepo.GetAllByCutOrderIdAsync(realCutOrderId, Arg.Any<CancellationToken>()).Returns(Array.Empty<WeighTicket>());
        _deliveryRepo.GetAllByCutOrderIdAsync(realCutOrderId, Arg.Any<CancellationToken>()).Returns(Array.Empty<DeliveryTicket>());

        var sut = CreateMapTemporaryExportCutOrderUseCase();

        await sut.ExecuteAsync(new MapTemporaryExportCutOrderRequest(temporaryCutOrderId, realCutOrderId), CancellationToken.None);

        await _cutOrderRepo.Received(1).UpdateAsync(
            Arg.Is<CutOrder>(x =>
                x.Id == realCutOrderId
                && x.IsExportScale
                && !x.IsTemporaryExport
                && x.TareWeightKg == 1200m
                && x.BagWeightKg == 50m
                && x.BagCount == 91
                && x.MappedTemporaryCutOrderId == temporaryCutOrderId
                && x.CurrentPrimaryWeighTicketId == weighTicketId
                && x.CurrentPrimaryDeliveryTicketId == deliveryTicketId),
            Arg.Any<CancellationToken>());

        await _cutOrderRepo.Received(1).UpdateAsync(
            Arg.Is<CutOrder>(x =>
                x.Id == temporaryCutOrderId
                && x.MappedRealCutOrderId == realCutOrderId
                && x.CutOrderStatus == CutOrderStatus.COMPLETED
                && x.ProcessingStage == ProcessingStage.OUT_YARD),
            Arg.Any<CancellationToken>());

        await _sessionRepo.Received(1).UpdateLineAsync(
            Arg.Is<WeighingSessionLine>(x =>
                x.Id == lineId
                && x.CutOrderId == realCutOrderId
                && x.CustomerCode == "ERP-CUS"
                && x.ProductCode == "ERP-PROD"
                && x.PlannedWeight == 4600m
                && x.PlannedBagCount == 91),
            Arg.Any<CancellationToken>());

        await _weighRepo.Received(1).UpdateAsync(
            Arg.Is<WeighTicket>(x =>
                x.Id == weighTicketId
                && x.CutOrderId == realCutOrderId
                && x.ErpCutOrderId == "QN.CL.2606/0099"
                && x.BagCount == 91
                && x.Notes == "ERP note"),
            Arg.Any<CancellationToken>());

        await _deliveryRepo.Received(1).UpdateAsync(
            Arg.Is<DeliveryTicket>(x =>
                x.Id == deliveryTicketId
                && x.CutOrderId == realCutOrderId
                && x.ErpCutOrderId == "QN.CL.2606/0099"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task MapTemporaryExportCutOrder_DoesNotOverrideExistingBagFields_OnRealCutOrder()
    {
        var temporaryCutOrderId = Guid.NewGuid();
        var realCutOrderId = Guid.NewGuid();
        var sessionId = Guid.NewGuid();
        var lineId = Guid.NewGuid();

        var temporaryCutOrder = new CutOrder
        {
            Id = temporaryCutOrderId,
            IsTemporaryExport = true,
            IsExportScale = true,
            TransactionType = TransactionType.OUTBOUND,
            CutOrderStatus = CutOrderStatus.IN_SESSION,
            ProcessingStage = ProcessingStage.WEIGHING,
            PlannedWeight = 4550m,
            BagCount = 91,
            TareWeightKg = 1200m,
            BagWeightKg = 50m
        };
        var realCutOrder = new CutOrder
        {
            Id = realCutOrderId,
            ErpCutOrderId = "QN.CL.2606/0100",
            IsTemporaryExport = false,
            TransactionType = TransactionType.OUTBOUND,
            CutOrderStatus = CutOrderStatus.REGISTERED,
            ProcessingStage = ProcessingStage.IN_YARD,
            PlannedWeight = 4600m,
            BagCount = 100,
            TareWeightKg = 1300m,
            BagWeightKg = 46m
        };
        var session = new WeighingSession { Id = sessionId };
        var line = new WeighingSessionLine { Id = lineId, WeighingSessionId = sessionId, CutOrderId = temporaryCutOrderId };

        _cutOrderRepo.GetByIdAsync(temporaryCutOrderId, Arg.Any<CancellationToken>()).Returns(temporaryCutOrder);
        _cutOrderRepo.GetByIdAsync(realCutOrderId, Arg.Any<CancellationToken>()).Returns(realCutOrder);
        _cutOrderRepo.GetExportVehicleTripsAsync(temporaryCutOrderId, Arg.Any<CancellationToken>())
            .Returns(new[]
            {
                new ExportVehicleTripListItem(
                    sessionId,
                    lineId,
                    "QN01-LC26060002",
                    "14C-54321",
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    WeighingSessionStatus.PENDING_WEIGHT2,
                    null,
                    null,
                    false,
                    false)
            });
        _sessionRepo.GetByIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(session);
        _sessionRepo.GetLinesBySessionIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(new[] { line });
        _weighRepo.GetByWeighingSessionIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(Array.Empty<WeighTicket>());
        _deliveryRepo.GetByWeighingSessionIdAsync(sessionId, Arg.Any<CancellationToken>()).Returns(Array.Empty<DeliveryTicket>());
        _weighRepo.GetAllByCutOrderIdAsync(realCutOrderId, Arg.Any<CancellationToken>()).Returns(Array.Empty<WeighTicket>());
        _deliveryRepo.GetAllByCutOrderIdAsync(realCutOrderId, Arg.Any<CancellationToken>()).Returns(Array.Empty<DeliveryTicket>());

        var sut = CreateMapTemporaryExportCutOrderUseCase();

        await sut.ExecuteAsync(new MapTemporaryExportCutOrderRequest(temporaryCutOrderId, realCutOrderId), CancellationToken.None);

        await _cutOrderRepo.Received(1).UpdateAsync(
            Arg.Is<CutOrder>(x =>
                x.Id == realCutOrderId
                && x.TareWeightKg == 1300m
                && x.BagWeightKg == 46m
                && x.BagCount == 100),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task FinalizeExportCutOrder_SumsReturnedBrokenTrips_AsNegativeWeight()
    {
        var cutOrderId = Guid.NewGuid();
        var cutOrder = new CutOrder
        {
            Id = cutOrderId,
            StationCode = "QN01",
            IsExportScale = true,
            TransactionType = TransactionType.OUTBOUND,
            ProcessingStage = ProcessingStage.WEIGHING,
            CutOrderStatus = CutOrderStatus.IN_SESSION,
            PlannedWeight = 3000m
        };

        _cutOrderRepo.GetByIdAsync(cutOrderId, Arg.Any<CancellationToken>()).Returns(cutOrder);
        _cutOrderRepo.GetExportVehicleTripsAsync(cutOrderId, Arg.Any<CancellationToken>()).Returns(new[]
        {
            new ExportVehicleTripListItem(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "QN01-LC26060001",
                "14C-12345",
                null,
                null,
                null,
                null,
                3000m,
                3000m,
                60,
                null,
                null,
                WeighingSessionStatus.COMPLETED,
                null,
                null,
                false,
                false)
            {
                IsReturnedBrokenTrip = false
            },
            new ExportVehicleTripListItem(
                Guid.NewGuid(),
                Guid.NewGuid(),
                "QN01-LC26060002",
                "14C-54321",
                null,
                null,
                null,
                null,
                500m,
                500m,
                10,
                null,
                null,
                WeighingSessionStatus.COMPLETED,
                null,
                null,
                false,
                false)
            {
                IsReturnedBrokenTrip = true
            }
        });

        var sut = new FinalizeExportCutOrderUseCase(_cutOrderRepo, _uow, _userContext, _clock);

        await sut.ExecuteAsync(new FinalizeExportCutOrderRequest(cutOrderId), CancellationToken.None);

        await _cutOrderRepo.Received(1).UpdateAsync(
            Arg.Is<CutOrder>(x =>
                x.Id == cutOrderId
                && x.ExportFinalizedWeight == 2500m
                && x.ExportFinalizedAt.HasValue
                && x.CutOrderStatus == CutOrderStatus.COMPLETED
                && x.ProcessingStage == ProcessingStage.OUT_YARD),
            Arg.Any<CancellationToken>());
    }

    private CreateTemporaryExportCutOrderUseCase CreateTemporaryExportCutOrderUseCase()
        => new(_cutOrderRepo, _customerRepo, _productRepo, _syncOutboxRepo, _syncPayloadFactory, _uow, _userContext, _clock);

    private MapTemporaryExportCutOrderUseCase CreateMapTemporaryExportCutOrderUseCase()
        => new(_cutOrderRepo, _sessionRepo, _weighRepo, _deliveryRepo, _uow, _userContext, _clock);
}
