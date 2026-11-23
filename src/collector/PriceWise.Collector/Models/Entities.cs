using System;
using System.Text.Json.Serialization;

namespace PriceWise.Collector.Models;

public class Supermarket
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("name")]
    public required string Name { get; set; }

    [JsonPropertyName("slug")]
    public required string Slug { get; set; }

    [JsonPropertyName("website_url")]
    public required string WebsiteUrl { get; set; }

    [JsonPropertyName("integration_status")]
    public string IntegrationStatus { get; set; } = "active";

    [JsonPropertyName("status_reason")]
    public string? StatusReason { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class StoreLocation
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("supermarket_id")]
    public Guid SupermarketId { get; set; }

    [JsonPropertyName("location_name")]
    public required string LocationName { get; set; }

    [JsonPropertyName("location_type")]
    public string LocationType { get; set; } = "delivery_hub";

    [JsonPropertyName("delivery_area")]
    public required string DeliveryArea { get; set; }

    [JsonPropertyName("external_store_id")]
    public string? ExternalStoreId { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Product
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("canonical_name")]
    public required string CanonicalName { get; set; }

    [JsonPropertyName("brand")]
    public required string Brand { get; set; }

    [JsonPropertyName("barcode")]
    public string? Barcode { get; set; }

    [JsonPropertyName("quantity")]
    public decimal Quantity { get; set; }

    [JsonPropertyName("unit")]
    public required string Unit { get; set; }

    [JsonPropertyName("category")]
    public required string Category { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class StoreProduct
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("supermarket_id")]
    public Guid SupermarketId { get; set; }

    [JsonPropertyName("store_location_id")]
    public Guid? StoreLocationId { get; set; }

    [JsonPropertyName("product_id")]
    public Guid? ProductId { get; set; }

    [JsonPropertyName("source_product_id")]
    public required string SourceProductId { get; set; }

    [JsonPropertyName("source_product_name")]
    public required string SourceProductName { get; set; }

    [JsonPropertyName("product_url")]
    public required string ProductUrl { get; set; }

    [JsonPropertyName("image_url")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("availability_status")]
    public string AvailabilityStatus { get; set; } = "in_stock";

    [JsonPropertyName("last_observed_at")]
    public DateTime? LastObservedAt { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class PriceObservation
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("store_product_id")]
    public Guid StoreProductId { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("original_price")]
    public decimal? OriginalPrice { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "LKR";

    [JsonPropertyName("observed_at")]
    public DateTime ObservedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("source_url")]
    public required string SourceUrl { get; set; }

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class CollectorRun
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("supermarket_id")]
    public Guid SupermarketId { get; set; }

    [JsonPropertyName("started_at")]
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("finished_at")]
    public DateTime? FinishedAt { get; set; }

    [JsonPropertyName("status")]
    public string Status { get; set; } = "running";

    [JsonPropertyName("products_checked")]
    public int ProductsChecked { get; set; }

    [JsonPropertyName("products_updated")]
    public int ProductsUpdated { get; set; }

    [JsonPropertyName("error_message")]
    public string? ErrorMessage { get; set; }
}

public class ProductMatch
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [JsonPropertyName("product_id")]
    public Guid ProductId { get; set; }

    [JsonPropertyName("store_product_id")]
    public Guid StoreProductId { get; set; }

    [JsonPropertyName("match_method")]
    public required string MatchMethod { get; set; }

    [JsonPropertyName("confidence")]
    public decimal Confidence { get; set; }

    [JsonPropertyName("verification_status")]
    public string VerificationStatus { get; set; } = "verified";

    [JsonPropertyName("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
