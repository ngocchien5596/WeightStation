using StationApp.Application.DTOs;

namespace StationApp.Application.Services;

public static class ShiftProductOutputReportCalculator
{
    public static ShiftProductOutputReportDocument Build(
        ShiftProductOutputReportFilter filter,
        string preparedByDisplayName,
        IEnumerable<ShiftProductOutputReportProductSeed> productSeeds,
        IEnumerable<ShiftProductOutputReportSourceRow> sourceRows)
    {
        var seeds = productSeeds
            .Where(x => !string.IsNullOrWhiteSpace(x.GroupName)
                && !string.IsNullOrWhiteSpace(x.ProductCode))
            .Select(NormalizeSeed)
            .DistinctBy(x => (Group: x.GroupName.ToUpperInvariant(), Product: x.ProductCode.ToUpperInvariant()))
            .ToList();

        var rows = sourceRows
            .Where(x => !string.IsNullOrWhiteSpace(x.GroupName)
                && !string.IsNullOrWhiteSpace(x.ProductCode))
            .Select(NormalizeSourceRow)
            .Where(x => string.IsNullOrWhiteSpace(filter.ProductCode)
                || string.Equals(x.ProductCode, filter.ProductCode.Trim(), StringComparison.OrdinalIgnoreCase))
            .ToList();

        var keys = seeds
            .Select(x => new ProductKey(x.GroupName, x.ProductCode, x.ProductName))
            .Concat(rows.Select(x => new ProductKey(x.GroupName, x.ProductCode, x.ProductName)))
            .Where(x => string.IsNullOrWhiteSpace(filter.ProductCode)
                || string.Equals(x.ProductCode, filter.ProductCode.Trim(), StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => (Group: x.GroupName.ToUpperInvariant(), Product: x.ProductCode.ToUpperInvariant()))
            .Select(x => x.OrderByDescending(item => string.IsNullOrWhiteSpace(item.ProductName) ? 0 : 1).First())
            .ToList();

        var groups = new List<ShiftProductOutputReportGroup>();
        var stt = 1;
        foreach (var groupName in ShiftProductOutputReportGroups.Ordered)
        {
            var groupRows = keys
                .Where(x => string.Equals(x.GroupName, groupName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(x => x.ProductCode, StringComparer.OrdinalIgnoreCase)
                .Select(key =>
                {
                    var productRows = rows
                        .Where(x => string.Equals(x.GroupName, key.GroupName, StringComparison.OrdinalIgnoreCase)
                            && string.Equals(x.ProductCode, key.ProductCode, StringComparison.OrdinalIgnoreCase))
                        .ToList();

                    var shiftOutputTon = RoundTon(productRows
                        .Where(x => x.ExportedAt >= filter.FromTime && x.ExportedAt <= filter.ToTime)
                        .Sum(x => x.SignedWeightKg));

                    var shiftRows = productRows
                        .Where(x => x.ExportedAt >= filter.FromTime && x.ExportedAt <= filter.ToTime)
                        .ToList();

                    var referenceCount = string.Equals(key.GroupName, ShiftProductOutputReportGroups.Export, StringComparison.OrdinalIgnoreCase)
                        ? shiftRows
                            .Where(x => !x.IsReturnedBrokenTrip)
                            .Select(x => x.SessionId)
                            .Distinct()
                            .Count()
                        : shiftRows
                            .Where(x => x.CutOrderId.HasValue)
                            .Select(x => x.CutOrderId!.Value)
                            .Distinct()
                            .Count();

                    return new ShiftProductOutputReportRow(
                        stt++,
                        key.GroupName,
                        key.ProductCode,
                        string.IsNullOrWhiteSpace(key.ProductName) ? key.ProductCode : key.ProductName,
                        shiftOutputTon,
                        referenceCount);
                })
                .ToList();

            groups.Add(new ShiftProductOutputReportGroup(
                groupName,
                groupRows,
                groupRows.Sum(x => x.ShiftOutputTon),
                groupRows.Sum(x => x.ReferenceCount)));
        }

        return new ShiftProductOutputReportDocument(
            filter.ReportDate.Date,
            filter.ShiftCode,
            filter.FromTime,
            filter.ToTime,
            string.IsNullOrWhiteSpace(filter.ProductCode) ? null : filter.ProductCode.Trim(),
            preparedByDisplayName,
            groups,
            groups.Sum(x => x.TotalShiftOutputTon),
            groups.Sum(x => x.TotalReferenceCount));
    }

    private static ShiftProductOutputReportProductSeed NormalizeSeed(ShiftProductOutputReportProductSeed seed)
        => seed with
        {
            GroupName = seed.GroupName.Trim(),
            ProductCode = seed.ProductCode.Trim(),
            ProductName = string.IsNullOrWhiteSpace(seed.ProductName) ? seed.ProductCode.Trim() : seed.ProductName.Trim()
        };

    private static ShiftProductOutputReportSourceRow NormalizeSourceRow(ShiftProductOutputReportSourceRow row)
        => row with
        {
            GroupName = row.GroupName.Trim(),
            ProductCode = row.ProductCode.Trim(),
            ProductName = string.IsNullOrWhiteSpace(row.ProductName) ? row.ProductCode.Trim() : row.ProductName.Trim()
        };

    private static decimal RoundTon(decimal weightKg)
        => decimal.Round(weightKg / 1000m, 3, MidpointRounding.AwayFromZero);

    private sealed record ProductKey(string GroupName, string ProductCode, string ProductName);
}
