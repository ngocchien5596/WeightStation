using StationApp.Domain.Enums;
using StationApp.Domain.Exceptions;

namespace StationApp.Domain.Services;

public static class WeightCalculator
{
    public static decimal Calculate(TransactionType transactionType, decimal weight1, decimal weight2)
    {
        return transactionType switch
        {
            TransactionType.OUTBOUND => weight2 - weight1,
            TransactionType.INBOUND => weight1 - weight2,
            _ => throw new DomainValidationException($"Unknown transaction type: {transactionType}")
        };
    }
}
