using StationApp.Application.Printing;
using StationApp.Domain.Entities;
using StationApp.Domain.Enums;
using Xunit;

namespace StationApp.Application.Tests;

public class PrintComposerTests
{
    [Fact]
    public void WeighTicketComposer_UsesSnapshotRegistrationNumbers_AndRootExtensions()
    {
        var composer = new WeighTicketPrintComposer();
        var registration = new CutOrder
        {
            Id = Guid.NewGuid(),
            VehiclePlate = "51C-12345",
            MoocNumber = "51R-99999",
            CustomerName = "Cong ty A",
            ProductName = "Xi mang",
            LotNo = "LO-01",
            RepresentativeName = "Nguyen Van B",
            Notes = "Ghi chu root",
            TransactionType = TransactionType.OUTBOUND
        };

        var vehicle = new Vehicle
        {
            VehiclePlate = registration.VehiclePlate,
            MoocNumber = registration.MoocNumber ?? string.Empty,
            VehicleRegistrationNo = "DK-XE-FALLBACK",
            MoocRegistrationNo = "DK-MOOC-FALLBACK"
        };

        var ticket = new WeighTicket
        {
            Id = Guid.NewGuid(),
            TicketNo = "PC0001",
            CutOrderId = registration.Id,
            VehiclePlate = registration.VehiclePlate,
            MoocNumber = registration.MoocNumber,
            CustomerName = "Cong ty A",
            ProductName = "Xi mang",
            TransactionType = TransactionType.OUTBOUND,
            Weight1 = 12000,
            Weight2 = 34000,
            NetWeight = 22000,
            VehicleRegistrationNoSnapshot = "DK-XE-SNAPSHOT",
            MoocRegistrationNoSnapshot = "DK-MOOC-SNAPSHOT"
        };

        var model = composer.Compose(registration, ticket, actualBagCount: null, isReturnedBrokenTrip: false, vehicle, new DateTime(2026, 4, 27, 9, 30, 0), "Test User");

        Assert.Equal("PC0001", model.TicketNo);
        Assert.Equal("DK-XE-SNAPSHOT", model.Fields.Single(x => x.FieldKey == "VehicleRegistrationNo").Value);
        Assert.Equal("DK-MOOC-SNAPSHOT", model.Fields.Single(x => x.FieldKey == "MoocRegistrationNo").Value);
        Assert.Equal("LO-01", model.Fields.Single(x => x.FieldKey == "LotNo").Value);
        Assert.Equal("Nguyen Van B", model.Fields.Single(x => x.FieldKey == "RepresentativeName").Value);
        Assert.Equal("12.0", model.Fields.Single(x => x.FieldKey == "EmptyWeight").Value);
        Assert.Equal("34.0", model.Fields.Single(x => x.FieldKey == "GrossWeight").Value);
    }

    [Fact]
    public void WeighTicketComposer_AppendsReturnedBrokenTripNote_ForExportScaleTrip()
    {
        var composer = new WeighTicketPrintComposer();
        var registration = new CutOrder
        {
            Id = Guid.NewGuid(),
            IsExportScale = true,
            TransactionType = TransactionType.OUTBOUND,
            ProductType = "Bao",
            BagWeightKg = 50m
        };

        var ticket = new WeighTicket
        {
            Id = Guid.NewGuid(),
            TicketNo = "PC0002",
            CutOrderId = registration.Id,
            TransactionType = TransactionType.OUTBOUND,
            NetWeight = 1000m
        };

        var model = composer.Compose(
            registration,
            ticket,
            actualBagCount: 20,
            isReturnedBrokenTrip: true,
            vehicle: null,
            new DateTime(2026, 4, 27, 9, 30, 0),
            "Test User");

        Assert.Equal("20 bao - Rách vỡ", model.Fields.Single(x => x.FieldKey == "Notes").Value);
    }

    [Fact]
    public void DeliveryTicketComposer_FallsBackToRegistrationData_AndLeavesActualBagCountBlank()
    {
        var composer = new DeliveryTicketPrintComposer();
        var registration = new CutOrder
        {
            Id = Guid.NewGuid(),
            VehiclePlate = "51C-12345",
            MoocNumber = "51R-99999",
            ErpCutOrderId = "ERP-001",
            CustomerName = "Cong ty A",
            CustomerCode = "C001",
            ProductName = "Xi mang",
            ProductType = "Bao",
            PlannedWeight = 21000,
            BagCount = 400,
            ConsumptionPlace = "Noi tieu thu",
            LoadingPlace = "Noi xuat hang",
            LotNo = "LO-01",
            SealNo = "SEAL-01",
            Notes = "Ghi chu root"
        };

        var ticket = new DeliveryTicket
        {
            Id = Guid.NewGuid(),
            CutOrderId = registration.Id,
            DeliveryNo = "PGN0001",
            ErpCutOrderId = "ERP-001"
        };

        var weigh = new WeighTicket
        {
            Id = Guid.NewGuid(),
            CutOrderId = registration.Id,
            TicketNo = "PC0001",
            VehiclePlate = registration.VehiclePlate,
            MoocNumber = registration.MoocNumber,
            NetWeight = 20500,
            VehicleRegistrationNoSnapshot = "DK-XE-SNAPSHOT",
            MoocRegistrationNoSnapshot = "DK-MOOC-SNAPSHOT"
        };

        var model = composer.Compose(registration, ticket, weigh, sessionLine: null, vehicle: null, new DateTime(2026, 4, 27, 9, 30, 0), "Test User");

        Assert.Equal("PGN0001", model.DeliveryNo);
        Assert.Equal("ERP-001", model.Fields.Single(x => x.FieldKey == "ReferenceCode").Value);
        Assert.Equal("20.5", model.Fields.Single(x => x.FieldKey == "ActualWeight").Value);
        Assert.Null(model.Fields.Single(x => x.FieldKey == "ActualBagCount").Value);
        Assert.Equal("Noi tieu thu", model.Fields.Single(x => x.FieldKey == "ConsumptionPlace").Value);
        Assert.Equal("Noi xuat hang", model.Fields.Single(x => x.FieldKey == "LoadingPlace").Value);
    }

    [Fact]
    public void DeliveryTicketComposer_UsesSessionLineAllocation_WhenProvided()
    {
        var composer = new DeliveryTicketPrintComposer();
        var registration = new CutOrder
        {
            Id = Guid.NewGuid(),
            VehiclePlate = "51C-12345",
            CustomerName = "Cong ty A",
            ProductName = "Xi mang",
            ProductType = "Bao"
        };

        var ticket = new DeliveryTicket
        {
            Id = Guid.NewGuid(),
            CutOrderId = registration.Id,
            DeliveryNo = "PGN0002",
            ErpCutOrderId = "ERP-002"
        };

        var weigh = new WeighTicket
        {
            Id = Guid.NewGuid(),
            CutOrderId = registration.Id,
            TicketNo = "PC0002",
            VehiclePlate = registration.VehiclePlate,
            NetWeight = 20500
        };

        var sessionLine = new WeighingSessionLine
        {
            Id = Guid.NewGuid(),
            CutOrderId = registration.Id,
            SequenceNo = 1,
            ActualAllocatedWeight = 10250,
            ActualAllocatedBagCount = 200
        };

        var model = composer.Compose(registration, ticket, weigh, sessionLine, vehicle: null, new DateTime(2026, 4, 27, 9, 30, 0), "Test User");

        Assert.Equal("10.25", model.Fields.Single(x => x.FieldKey == "ActualWeight").Value);
        Assert.Equal("200", model.Fields.Single(x => x.FieldKey == "ActualBagCount").Value);
    }

    [Fact]
    public void DeliveryTicketComposer_PrefersDeliveryTicketAllocatedValues_OverSessionLine()
    {
        var composer = new DeliveryTicketPrintComposer();
        var registration = new CutOrder
        {
            Id = Guid.NewGuid(),
            VehiclePlate = "51C-12345",
            CustomerName = "Cong ty A",
            ProductName = "Xi mang",
            ProductType = "Bao"
        };

        var ticket = new DeliveryTicket
        {
            Id = Guid.NewGuid(),
            CutOrderId = registration.Id,
            DeliveryNo = "PGN0003",
            ErpCutOrderId = "ERP-003",
            AllocatedWeight = 2500,
            AllocatedBagCount = 50
        };

        var weigh = new WeighTicket
        {
            Id = Guid.NewGuid(),
            CutOrderId = registration.Id,
            TicketNo = "PC0003",
            VehiclePlate = registration.VehiclePlate,
            NetWeight = 20500
        };

        var sessionLine = new WeighingSessionLine
        {
            Id = Guid.NewGuid(),
            CutOrderId = registration.Id,
            SequenceNo = 1,
            ActualAllocatedWeight = 10250,
            ActualAllocatedBagCount = 200
        };

        var model = composer.Compose(registration, ticket, weigh, sessionLine, vehicle: null, new DateTime(2026, 4, 27, 9, 30, 0), "Test User");

        Assert.Equal("2.5", model.Fields.Single(x => x.FieldKey == "ActualWeight").Value);
        Assert.Equal("50", model.Fields.Single(x => x.FieldKey == "ActualBagCount").Value);
    }
}


