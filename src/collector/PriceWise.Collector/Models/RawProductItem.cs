using System;

namespace PriceWise.Collector.Models;

public record RawProductItem
{
    public required string SourceSupermarketSlug { get; init; }
    public required string SourceProductId { get; init; }
    public required string SourceProductName { get; init; }
    public required decimal Price { get; init; }
    public decimal? OriginalPrice { get; init; }
    public string Currency { get; init; } = "LKR";
    public required string ProductUrl { get; init; }
    public string? ImageUrl { get; init; }
    public string? Brand { get; init; }
    public string? Barcode { get; init; }
    public decimal Quantity { get; init; }
    public string Unit { get; init; } = "pcs";
    public string Category { get; init; } = "General";
    public string AvailabilityStatus { get; init; } = "in_stock";
    public DateTime ObservedAt { get; init; } = DateTime.UtcNow;
}
