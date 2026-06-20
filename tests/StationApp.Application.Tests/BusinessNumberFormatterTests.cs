using StationApp.Application.Formatting;
using Xunit;

namespace StationApp.Application.Tests;

public class BusinessNumberFormatterTests
{
    [Fact]
    public void PrefixWithStation_Adds_Station_Prefix_When_Missing()
    {
        var result = BusinessNumberFormatter.PrefixWithStation("qn02", "PC26060001");

        Assert.Equal("QN02-PC26060001", result);
    }

    [Fact]
    public void PrefixWithStation_Does_Not_Duplicate_Existing_Prefix()
    {
        var result = BusinessNumberFormatter.PrefixWithStation("QN02", "QN01-PC26060001");

        Assert.Equal("QN01-PC26060001", result);
    }

    [Fact]
    public void BuildCounterKey_Uses_Normalized_Station_Code()
    {
        var result = BusinessNumberFormatter.BuildCounterKey("WeighTicket", "qn03", "PC2606");

        Assert.Equal("WeighTicket_QN03_PC2606", result);
    }

    [Theory]
    [InlineData("QN01-LC26060066", "LC26060066")]
    [InlineData("QN02-PC26060001", "PC26060001")]
    [InlineData("PGN26060001", "PGN26060001")]
    public void ToDisplay_Returns_Short_Business_Number(string businessNumber, string expected)
    {
        var result = BusinessNumberFormatter.ToDisplay(businessNumber);

        Assert.Equal(expected, result);
    }
}
