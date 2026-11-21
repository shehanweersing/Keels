using System;

namespace PriceWise.Collector.Models;

public class Supermarket
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public required string WebsiteUrl { get; set; }
    public string IntegrationStatus { get; set; } = "active";
    public string? StatusReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class StoreLocation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SupermarketId { get; set; }
    public required string LocationName { get; set; }
    public string LocationType { get; set; } = "delivery_hub";
    public required string DeliveryArea { get; set; }
    public string? ExternalStoreId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string CanonicalName { get; set; }
    public required string Brand { get; set; }
    public string? Barcode { get; set; }
    public decimal Quantity { get; set; }
    public required string Unit { get; set; }
    public required string Category { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class StoreProduct
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SupermarketId { get; set; }
    public Guid? StoreLocationId { get; set; }
    public Guid? ProductId { get; set; }
    public required string SourceProductId { get; set; }
    public required string SourceProductName { get; set; }
    public required string ProductUrl { get; set; }
    public string? ImageUrl { get; set; }
    public string AvailabilityStatus { get; set; } = "in_stock";
    public DateTime? LastObservedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PriceObservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StoreProductId { get; set; }
    public decimal Price { get; set; }
    public decimal? OriginalPrice { get; set; }
    public string Currency { get; set; } = "LKR";
    public DateTime ObservedAt { get; set; } = DateTime.UtcNow;
    public required string SourceUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CollectorRun
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SupermarketId { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? FinishedAt { get; set; }
    public string Status { get; set; } = "running";
    public int ProductsChecked { get; set; }
    public int ProductsUpdated { get; set; }
    public string? ErrorMessage { get; set; }
}

public class ProductMatch
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProductId { get; set; }
    public Guid StoreProductId { get; set; }
    public required string MatchMethod { get; set; }
    public decimal Confidence { get; set; }
    public string VerificationStatus { get; set; } = "verified";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
