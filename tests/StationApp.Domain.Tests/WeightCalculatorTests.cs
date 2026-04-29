using Xunit;
using StationApp.Domain.Enums;
using StationApp.Domain.Services;

namespace StationApp.Domain.Tests;

public class WeightCalculatorTests
{
    [Fact]
    public void Outbound_NetWeight_Is_Weight2_Minus_Weight1()
    {
        var result = WeightCalculator.Calculate(TransactionType.OUTBOUND, 10000m, 35000m);
        Assert.Equal(25000m, result);
    }

    [Fact]
    public void Inbound_NetWeight_Is_Weight1_Minus_Weight2()
    {
        var result = WeightCalculator.Calculate(TransactionType.INBOUND, 35000m, 10000m);
        Assert.Equal(25000m, result);
    }

    [Fact]
    public void Outbound_Negative_NetWeight()
    {
        var result = WeightCalculator.Calculate(TransactionType.OUTBOUND, 35000m, 10000m);
        Assert.True(result < 0);
    }

    [Fact]
    public void Inbound_Negative_NetWeight()
    {
        var result = WeightCalculator.Calculate(TransactionType.INBOUND, 10000m, 35000m);
        Assert.True(result < 0);
    }
}
