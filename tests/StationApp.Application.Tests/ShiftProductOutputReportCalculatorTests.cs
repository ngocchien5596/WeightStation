using StationApp.Application.DTOs;
using StationApp.Application.Services;
using Xunit;

namespace StationApp.Application.Tests;

public class ShiftProductOutputReportCalculatorTests
{
    [Fact]
    public void Build_keeps_zero_rows_for_seed_products_and_counts_domestic_cut_orders_in_shift()
    {
        var filter = new ShiftProductOutputReportFilter(
            new DateTime(2026, 8, 16),
            "Ca 1",
            new DateTime(2026, 8, 16, 6, 0, 0),
            new DateTime(2026, 8, 16, 13, 59, 59),
            null);

        var cutOrder1 = Guid.NewGuid();
        var cutOrder2 = Guid.NewGuid();
        var document = ShiftProductOutputReportCalculator.Build(
            filter,
            "tester",
            [
                new ShiftProductOutputReportProductSeed(ShiftProductOutputReportGroups.Bulk, "PCB40", "Xi măng rời PCB40"),
                new ShiftProductOutputReportProductSeed(ShiftProductOutputReportGroups.Bagged, "PCB30", "Xi măng bao PCB30")
            ],
            [
                new ShiftProductOutputReportSourceRow(ShiftProductOutputReportGroups.Bulk, "PCB40", "Xi măng rời PCB40", Guid.NewGuid(), cutOrder1, new DateTime(2026, 8, 16, 5, 30, 0), 10_000m, false),
                new ShiftProductOutputReportSourceRow(ShiftProductOutputReportGroups.Bulk, "PCB40", "Xi măng rời PCB40", Guid.NewGuid(), cutOrder1, new DateTime(2026, 8, 16, 8, 0, 0), 25_000m, false),
                new ShiftProductOutputReportSourceRow(ShiftProductOutputReportGroups.Bulk, "PCB40", "Xi măng rời PCB40", Guid.NewGuid(), cutOrder2, new DateTime(2026, 8, 16, 9, 0, 0), 5_000m, false),
                new ShiftProductOutputReportSourceRow(ShiftProductOutputReportGroups.Bulk, "PCB40", "Xi măng rời PCB40", Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 8, 15, 8, 0, 0), 100_000m, false)
            ]);

        var bulkRow = Assert.Single(document.Groups.Single(x => x.GroupName == ShiftProductOutputReportGroups.Bulk).Rows);
        Assert.Equal(30m, bulkRow.ShiftOutputTon);
        Assert.Equal(2, bulkRow.ReferenceCount);

        var baggedRow = Assert.Single(document.Groups.Single(x => x.GroupName == ShiftProductOutputReportGroups.Bagged).Rows);
        Assert.Equal(0m, baggedRow.ShiftOutputTon);
        Assert.Equal(0, baggedRow.ReferenceCount);
    }

    [Fact]
    public void Build_counts_each_distinct_domestic_cut_order_for_same_product_in_shift()
    {
        var filter = new ShiftProductOutputReportFilter(
            new DateTime(2026, 8, 16),
            "Ca 1",
            new DateTime(2026, 8, 16, 6, 0, 0),
            new DateTime(2026, 8, 16, 13, 59, 59),
            null);

        var cutOrderIds = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToArray();
        var document = ShiftProductOutputReportCalculator.Build(
            filter,
            "tester",
            [],
            cutOrderIds.Select((cutOrderId, index) =>
                new ShiftProductOutputReportSourceRow(
                    ShiftProductOutputReportGroups.Bagged,
                    "PCB30-KPK",
                    "Xi măng bao PCB30 vỏ KPK 50kg",
                    Guid.NewGuid(),
                    cutOrderId,
                    new DateTime(2026, 8, 16, 7 + index, 0, 0),
                    10_000m,
                    false)));

        var row = Assert.Single(document.Groups.Single(x => x.GroupName == ShiftProductOutputReportGroups.Bagged).Rows);
        Assert.Equal(40m, row.ShiftOutputTon);
        Assert.Equal(4, row.ReferenceCount);
    }

    [Fact]
    public void Build_counts_export_trips_in_shift_and_excludes_returned_trips_from_trip_count()
    {
        var filter = new ShiftProductOutputReportFilter(
            new DateTime(2026, 8, 16),
            "Ca 2",
            new DateTime(2026, 8, 16, 14, 0, 0),
            new DateTime(2026, 8, 16, 21, 59, 59),
            null);

        var trip1 = Guid.NewGuid();
        var trip2 = Guid.NewGuid();
        var returnedTrip = Guid.NewGuid();
        var document = ShiftProductOutputReportCalculator.Build(
            filter,
            "tester",
            [],
            [
                new ShiftProductOutputReportSourceRow(ShiftProductOutputReportGroups.Export, "PCB40", "Xi măng XK PCB40", Guid.NewGuid(), Guid.NewGuid(), new DateTime(2026, 8, 15, 9, 0, 0), 50_000m, false),
                new ShiftProductOutputReportSourceRow(ShiftProductOutputReportGroups.Export, "PCB40", "Xi măng XK PCB40", trip1, Guid.NewGuid(), new DateTime(2026, 8, 16, 15, 0, 0), 20_000m, false),
                new ShiftProductOutputReportSourceRow(ShiftProductOutputReportGroups.Export, "PCB40", "Xi măng XK PCB40", trip1, Guid.NewGuid(), new DateTime(2026, 8, 16, 15, 5, 0), 10_000m, false),
                new ShiftProductOutputReportSourceRow(ShiftProductOutputReportGroups.Export, "PCB40", "Xi măng XK PCB40", trip2, Guid.NewGuid(), new DateTime(2026, 8, 16, 16, 0, 0), 10_000m, false),
                new ShiftProductOutputReportSourceRow(ShiftProductOutputReportGroups.Export, "PCB40", "Xi măng XK PCB40", returnedTrip, Guid.NewGuid(), new DateTime(2026, 8, 16, 17, 0, 0), -5_000m, true)
            ]);

        var row = Assert.Single(document.Groups.Single(x => x.GroupName == ShiftProductOutputReportGroups.Export).Rows);
        Assert.Equal(35m, row.ShiftOutputTon);
        Assert.Equal(2, row.ReferenceCount);
    }
}
