using StationApp.Domain.Constants;
using Xunit;

namespace StationApp.Application.Tests;

public class ProductTypesTests
{
    [Theory]
    [InlineData("Clinker")]
    [InlineData("Roi clinker")]
    [InlineData("Roi_clinker")]
    public void Normalize_KeepsClinkerAsSeparateProductType(string value)
    {
        Assert.Equal(ProductTypes.Clinker, ProductTypes.Normalize(value));
    }

    [Theory]
    [InlineData("Roi")]
    [InlineData("Roi/Xa")]
    public void Normalize_MapsRoiToBulk(string value)
    {
        Assert.Equal(ProductTypes.Bulk, ProductTypes.Normalize(value));
    }

    [Fact]
    public void IsBulkLike_TreatsClinkerAsBulkOperationally()
    {
        Assert.True(ProductTypes.IsBulkLike("Clinker"));
    }
}
