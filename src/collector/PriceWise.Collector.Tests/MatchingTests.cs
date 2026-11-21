using System;
using System.Collections.Generic;
using PriceWise.Collector.Matching;
using PriceWise.Collector.Models;
using Xunit;

namespace PriceWise.Collector.Tests;

public class MatchingTests
{
    private readonly ProductMatcher _matcher = new();

    [Fact]
    public void MatchOrCreateProduct_ExactBarcodeMatch_ReturnsConfidence1()
    {
        var existing = new Product
        {
            Id = Guid.NewGuid(),
            CanonicalName = "Highland Fresh Milk 1L",
            Brand = "Highland",
            Barcode = "4791234567890",
            Quantity = 1,
            Unit = "l",
            Category = "Dairy"
        };

        var rawItem = new RawProductItem
        {
            SourceSupermarketSlug = "arpico",
            SourceProductId = "5344",
            SourceProductName = "Highland Pasteurized Milk 1 Litre",
            Price = 450.00m,
            Barcode = "4791234567890", // Exact match
            ProductUrl = "https://myarpico.com/shop/item",
            Quantity = 1,
            Unit = "l"
        };

        var result = _matcher.MatchOrCreateProduct(rawItem, new List<Product> { existing });

        Assert.False(result.IsNewProduct);
        Assert.Equal(existing.Id, result.CanonicalProduct.Id);
        Assert.Equal("barcode_exact", result.MatchMethod);
        Assert.Equal(1.0m, result.Confidence);
        Assert.Equal("verified", result.VerificationStatus);
    }

    [Fact]
    public void MatchOrCreateProduct_DifferentQuantities_NeverMatchesAsSameProduct()
    {
        // 500g pack already in catalog
        var existing500g = new Product
        {
            Id = Guid.NewGuid(),
            CanonicalName = "Munchee Super Cream Cracker 500g",
            Brand = "Munchee",
            Quantity = 500,
            Unit = "g",
            Category = "Biscuits"
        };

        // 1kg pack being observed
        var rawItem1kg = new RawProductItem
        {
            SourceSupermarketSlug = "cargills",
            SourceProductId = "CK1001",
            SourceProductName = "Munchee Super Cream Cracker 1kg",
            Price = 1100.00m,
            ProductUrl = "https://cargillsonline.com/Web/item",
            Brand = "Munchee",
            Quantity = 1,
            Unit = "kg"
        };

        var result = _matcher.MatchOrCreateProduct(rawItem1kg, new List<Product> { existing500g });

        // Must NOT match 500g pack as identical to 1kg pack
        Assert.True(result.IsNewProduct);
        Assert.NotEqual(existing500g.Id, result.CanonicalProduct.Id);
        Assert.Equal(1, result.CanonicalProduct.Quantity);
        Assert.Equal("kg", result.CanonicalProduct.Unit);
    }

    [Fact]
    public void MatchOrCreateProduct_EquivalentUnitsNormalized_MatchesCorrectly()
    {
        // 1kg in catalog
        var existing1kg = new Product
        {
            Id = Guid.NewGuid(),
            CanonicalName = "Araliya Keeri Samba Rice 1kg",
            Brand = "Araliya",
            Quantity = 1,
            Unit = "kg",
            Category = "Rice"
        };

        // 1000g incoming item
        var rawItem1000g = new RawProductItem
        {
            SourceSupermarketSlug = "arpico",
            SourceProductId = "ARP-RICE-01",
            SourceProductName = "Araliya Keeri Samba Rice 1000g",
            Price = 340.00m,
            ProductUrl = "https://myarpico.com/shop/item",
            Brand = "Araliya",
            Quantity = 1000,
            Unit = "g"
        };

        var result = _matcher.MatchOrCreateProduct(rawItem1000g, new List<Product> { existing1kg });

        Assert.False(result.IsNewProduct);
        Assert.Equal(existing1kg.Id, result.CanonicalProduct.Id);
        Assert.True(result.Confidence >= 0.95m);
    }

    [Fact]
    public void MatchOrCreateProduct_DifferentBrands_NeverMatches()
    {
        var existing = new Product
        {
            Id = Guid.NewGuid(),
            CanonicalName = "Maliban Gold Marie 400g",
            Brand = "Maliban",
            Quantity = 400,
            Unit = "g",
            Category = "Biscuits"
        };

        var rawItem = new RawProductItem
        {
            SourceSupermarketSlug = "cargills",
            SourceProductId = "MC400",
            SourceProductName = "Munchee Marie 400g",
            Price = 320.00m,
            ProductUrl = "https://cargillsonline.com/Web/item",
            Brand = "Munchee", // Different brand
            Quantity = 400,
            Unit = "g"
        };

        var result = _matcher.MatchOrCreateProduct(rawItem, new List<Product> { existing });

        Assert.True(result.IsNewProduct);
        Assert.NotEqual(existing.Id, result.CanonicalProduct.Id);
    }
}
