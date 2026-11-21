using System;
using PriceWise.Collector.Models;
using PriceWise.Collector.Validation;
using Xunit;

namespace PriceWise.Collector.Tests;

public class ValidationTests
{
    [Fact]
    public void ValidateRawProduct_ValidItem_PassesValidation()
    {
        var item = new RawProductItem
        {
            SourceSupermarketSlug = "cargills",
            SourceProductId = "BV20102",
            SourceProductName = "Milkmaid Condensed Milk 510g",
            Price = 990.00m,
            OriginalPrice = null,
            ProductUrl = "https://cargillsonline.com/Web/ProductDetails?ID=Em1GPI1wiW+z9w89sKr0+g==",
            Quantity = 510,
            Unit = "g"
        };

        var isValid = DataValidator.ValidateRawProduct(item, out var reason);

        Assert.True(isValid);
        Assert.Null(reason);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-100)]
    [InlineData(-0.5)]
    public void ValidateRawProduct_ZeroOrNegativePrice_FailsValidation(decimal invalidPrice)
    {
        var item = new RawProductItem
        {
            SourceSupermarketSlug = "cargills",
            SourceProductId = "BV20102",
            SourceProductName = "Milkmaid",
            Price = invalidPrice,
            ProductUrl = "https://cargillsonline.com/Web/item"
        };

        var isValid = DataValidator.ValidateRawProduct(item, out var reason);

        Assert.False(isValid);
        Assert.Contains("must be strictly greater than 0", reason);
    }

    [Fact]
    public void ValidateRawProduct_PriceAboveUpperLimit_FailsValidation()
    {
        var item = new RawProductItem
        {
            SourceSupermarketSlug = "arpico",
            SourceProductId = "9999",
            SourceProductName = "Luxury Caviar",
            Price = 1_500_000m,
            ProductUrl = "https://myarpico.com/shop/item"
        };

        var isValid = DataValidator.ValidateRawProduct(item, out var reason);

        Assert.False(isValid);
        Assert.Contains("exceeds 1,000,000 LKR", reason);
    }

    [Fact]
    public void ValidateRawProduct_OriginalPriceLowerThanPrice_FailsValidation()
    {
        var item = new RawProductItem
        {
            SourceSupermarketSlug = "arpico",
            SourceProductId = "308",
            SourceProductName = "Iodex",
            Price = 350.00m,
            OriginalPrice = 300.00m, // Invalid: original price cannot be cheaper than discounted price
            ProductUrl = "https://myarpico.com/shop/item"
        };

        var isValid = DataValidator.ValidateRawProduct(item, out var reason);

        Assert.False(isValid);
        Assert.Contains("cannot be lower than current discounted price", reason);
    }

    [Theory]
    [InlineData("Ambewela Fresh Milk 1L", 1, "l")]
    [InlineData("Ambewela Fresh Milk 200Ml", 200, "ml")]
    [InlineData("Munchee Super Cream Cracker 500g", 500, "g")]
    [InlineData("Basmati Rice 1.00kg", 1.00, "kg")]
    [InlineData("Dilmah Leaf Tea 400 g", 400, "g")]
    public void ParseQuantityAndUnit_ValidFormats_ExtractsCorrectly(string input, decimal expectedQty, string expectedUnit)
    {
        var (qty, unit) = DataValidator.ParseQuantityAndUnit(input);

        Assert.Equal(expectedQty, qty);
        Assert.Equal(expectedUnit, unit);
    }
}
