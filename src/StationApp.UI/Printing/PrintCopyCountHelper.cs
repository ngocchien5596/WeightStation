using System;
using System.Collections.Generic;
using System.Linq;
using StationApp.Domain.Constants;
using StationApp.Domain.Entities;

namespace StationApp.UI.Printing;

internal static class PrintCopyCountHelper
{
    private const int DefaultCopyCount = 1;
    private const int BulkWeighTicketCopyCount = 4;

    public static int ResolveDefaultWeighTicketCopyCount(IEnumerable<CutOrder> registrations)
    {
        if (registrations == null)
        {
            return DefaultCopyCount;
        }

        return registrations.Any(x => string.Equals(ProductTypes.Normalize(x.ProductType), ProductTypes.Bulk, StringComparison.OrdinalIgnoreCase))
            ? BulkWeighTicketCopyCount
            : DefaultCopyCount;
    }
}
