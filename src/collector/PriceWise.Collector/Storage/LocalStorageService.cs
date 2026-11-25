using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PriceWise.Collector.Matching;
using PriceWise.Collector.Models;

namespace PriceWise.Collector.Storage;

public class LocalStorageService : IStorageService
{
    private readonly string _outputDirectory;
    private readonly ProductMatcher _matcher;
    private readonly ILogger<LocalStorageService> _logger;

    private readonly List<Product> _products = new();
    private readonly List<Supermarket> _supermarkets = new();
    private readonly List<StoreProduct> _storeProducts = new();
    private readonly List<PriceObservation> _observations = new();
    private readonly List<CollectorRun> _collectorRuns = new();
    private readonly List<ProductMatch> _productMatches = new();

    public LocalStorageService(string outputDirectory, ProductMatcher matcher, ILogger<LocalStorageService> logger)
    {
        _outputDirectory = outputDirectory;
        _matcher = matcher;
        _logger = logger;
        Directory.CreateDirectory(_outputDirectory);
        LoadExistingData();
    }

    private void LoadExistingData()
    {
        try
        {
            var superFile = Path.Combine(_outputDirectory, "supermarkets.json");
            if (File.Exists(superFile))
            {
                var json = File.ReadAllText(superFile);
                var list = JsonSerializer.Deserialize<List<Supermarket>>(json);
                if (list != null) _supermarkets.AddRange(list);
            }

            var prodFile = Path.Combine(_outputDirectory, "products.json");
            if (File.Exists(prodFile))
            {
                var json = File.ReadAllText(prodFile);
                var list = JsonSerializer.Deserialize<List<Product>>(json);
                if (list != null) _products.AddRange(list);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Could not load prior local data: {Message}", ex.Message);
        }
    }

    public Task<List<Product>> GetCanonicalProductsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_products.ToList());
    }

    public Task<List<Supermarket>> GetSupermarketsAsync(CancellationToken cancellationToken = default)
    {
        if (_supermarkets.Count == 0)
        {
            _supermarkets.AddRange(new[]
            {
                new Supermarket
                {
                    Id = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
                    Name = "Cargills Food City",
                    Slug = "cargills",
                    WebsiteUrl = "https://cargillsonline.com/Web/",
                    IntegrationStatus = "active",
                    StatusReason = "Verified live public JSON search API with full product metadata and exact LKR pricing."
                },
                new Supermarket
                {
                    Id = Guid.Parse("a2000000-0000-0000-0000-000000000002"),
                    Name = "Arpico Supercentre",
                    Slug = "arpico",
                    WebsiteUrl = "https://myarpico.com/shop/",
                    IntegrationStatus = "active",
                    StatusReason = "Verified live public OpenCart search catalog permitted under robots.txt content signals."
                },
                new Supermarket
                {
                    Id = Guid.Parse("a3000000-0000-0000-0000-000000000003"),
                    Name = "Keells Super",
                    Slug = "keells",
                    WebsiteUrl = "https://www.keellssuper.com/",
                    IntegrationStatus = "blocked",
                    StatusReason = "Direct API calls to zebraliveback require encrypted client session tokens and anti-bot challenge cookies. Automated collection blocked pending authorized enterprise data feed."
                }
            });
        }
        return Task.FromResult(_supermarkets.ToList());
    }

    public Task UpdateSupermarketStatusAsync(
        Guid supermarketId,
        string integrationStatus,
        string statusReason,
        CancellationToken cancellationToken = default)
    {
        var existing = _supermarkets.FirstOrDefault(s => s.Id == supermarketId);
        if (existing != null)
        {
            existing.IntegrationStatus = integrationStatus;
            existing.StatusReason = statusReason;
            PersistJson("supermarkets.json", _supermarkets);
        }
        return Task.CompletedTask;
    }

    public Task SaveCollectorRunAsync(CollectorRun run, CancellationToken cancellationToken = default)
    {
        _collectorRuns.Add(run);
        PersistJson("collector_runs.json", _collectorRuns);
        return Task.CompletedTask;
    }

    public Task SaveCollectedProductsAsync(
        Supermarket supermarket,
        IReadOnlyList<RawProductItem> items,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Processing {Count} raw items for supermarket {Name}...", items.Count, supermarket.Name);

        foreach (var rawItem in items)
        {
            // Evaluate cross-store canonical match only against products that do NOT already have a listing from THIS supermarket.
            // A supermarket does not sell the exact same physical product under two distinct SKUs at different prices.
            var candidateCatalog = _products
                .Where(p => !_storeProducts.Any(sp => sp.ProductId == p.Id && sp.SupermarketId == supermarket.Id))
                .ToList();

            var matchResult = _matcher.MatchOrCreateProduct(rawItem, candidateCatalog);
            var canonical = matchResult.CanonicalProduct;

            if (matchResult.IsNewProduct)
            {
                _products.Add(canonical);
            }

            // Find or create StoreProduct
            var existingStoreProduct = _storeProducts.FirstOrDefault(sp =>
                sp.SupermarketId == supermarket.Id && sp.SourceProductId == rawItem.SourceProductId);

            if (existingStoreProduct == null)
            {
                existingStoreProduct = new StoreProduct
                {
                    Id = Guid.NewGuid(),
                    SupermarketId = supermarket.Id,
                    ProductId = canonical.Id,
                    SourceProductId = rawItem.SourceProductId,
                    SourceProductName = rawItem.SourceProductName,
                    ProductUrl = rawItem.ProductUrl,
                    ImageUrl = rawItem.ImageUrl,
                    AvailabilityStatus = rawItem.AvailabilityStatus,
                    LastObservedAt = rawItem.ObservedAt,
                    CreatedAt = DateTime.UtcNow
                };
                _storeProducts.Add(existingStoreProduct);
            }
            else
            {
                existingStoreProduct.ProductId = canonical.Id;
                existingStoreProduct.LastObservedAt = rawItem.ObservedAt;
                existingStoreProduct.AvailabilityStatus = rawItem.AvailabilityStatus;
                if (!string.IsNullOrWhiteSpace(rawItem.ImageUrl))
                    existingStoreProduct.ImageUrl = rawItem.ImageUrl;
            }

            // Record price observation
            var observation = new PriceObservation
            {
                Id = Guid.NewGuid(),
                StoreProductId = existingStoreProduct.Id,
                Price = rawItem.Price,
                OriginalPrice = rawItem.OriginalPrice,
                Currency = rawItem.Currency,
                ObservedAt = rawItem.ObservedAt,
                SourceUrl = rawItem.ProductUrl,
                CreatedAt = DateTime.UtcNow
            };
            _observations.Add(observation);

            // Record match audit record
            var existingMatch = _productMatches.FirstOrDefault(pm =>
                pm.ProductId == canonical.Id && pm.StoreProductId == existingStoreProduct.Id);

            if (existingMatch == null)
            {
                _productMatches.Add(new ProductMatch
                {
                    Id = Guid.NewGuid(),
                    ProductId = canonical.Id,
                    StoreProductId = existingStoreProduct.Id,
                    MatchMethod = matchResult.MatchMethod,
                    Confidence = matchResult.Confidence,
                    VerificationStatus = matchResult.VerificationStatus,
                    CreatedAt = DateTime.UtcNow
                });
            }
        }

        // Export normalized datasets and verified comparisons to JSON
        PersistJson("supermarkets.json", _supermarkets);
        PersistJson("products.json", _products);
        PersistJson("store_products.json", _storeProducts);
        PersistJson("price_observations.json", _observations);
        PersistJson("product_matches.json", _productMatches);

        // Generate combined verified comparison view for frontend
        GenerateComparisonCatalog();

        return Task.CompletedTask;
    }

    private void GenerateComparisonCatalog()
    {
        var comparisonCatalog = _products.Select(p =>
        {
            var storeListings = _storeProducts
                .Where(sp => sp.ProductId == p.Id)
                .Select(sp =>
                {
                    var super = _supermarkets.FirstOrDefault(s => s.Id == sp.SupermarketId);
                    var latestObservation = _observations
                        .Where(o => o.StoreProductId == sp.Id)
                        .OrderByDescending(o => o.ObservedAt)
                        .FirstOrDefault();

                    var priceHistory = _observations
                        .Where(o => o.StoreProductId == sp.Id)
                        .OrderBy(o => o.ObservedAt)
                        .Select(o => new
                        {
                            o.Price,
                            o.OriginalPrice,
                            o.ObservedAt
                        })
                        .ToList();

                    return new
                    {
                        StoreProductId = sp.Id,
                        SupermarketId = sp.SupermarketId,
                        SupermarketName = super?.Name ?? "Unknown",
                        SupermarketSlug = super?.Slug ?? "unknown",
                        sp.SourceProductId,
                        sp.SourceProductName,
                        sp.ProductUrl,
                        sp.ImageUrl,
                        sp.AvailabilityStatus,
                        sp.LastObservedAt,
                        CurrentPrice = latestObservation?.Price,
                        OriginalPrice = latestObservation?.OriginalPrice,
                        Currency = latestObservation?.Currency ?? "LKR",
                        PriceHistory = priceHistory
                    };
                })
                .Where(l => l.CurrentPrice.HasValue)
                .ToList();

            var verifiedOffers = storeListings.Where(l => l.CurrentPrice > 0).ToList();
            var distinctSupermarkets = verifiedOffers.Select(l => l.SupermarketId).Distinct().Count();
            var minPrice = verifiedOffers.Count > 0 ? verifiedOffers.Min(l => l.CurrentPrice!.Value) : (decimal?)null;
            var maxPrice = verifiedOffers.Count > 0 ? verifiedOffers.Max(l => l.CurrentPrice!.Value) : (decimal?)null;
            var cheapestSupermarket = verifiedOffers.FirstOrDefault(l => l.CurrentPrice == minPrice)?.SupermarketName;
            var priceDiff = (distinctSupermarkets >= 2 && minPrice.HasValue && maxPrice.HasValue && maxPrice > minPrice)
                ? (maxPrice.Value - minPrice.Value)
                : 0;

            return new
            {
                p.Id,
                p.CanonicalName,
                p.Brand,
                p.Barcode,
                p.Quantity,
                p.Unit,
                p.Category,
                OffersCount = storeListings.Count,
                SupermarketCount = distinctSupermarkets,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                PriceDifference = priceDiff,
                CheapestSupermarket = cheapestSupermarket,
                Offers = storeListings
            };
        }).ToList();

        PersistJson("verified_catalog.json", comparisonCatalog);
    }

    private void PersistJson<T>(string fileName, T data)
    {
        var filePath = Path.Combine(_outputDirectory, fileName);
        var options = new JsonSerializerOptions { WriteIndented = true };
        var json = JsonSerializer.Serialize(data, options);
        File.WriteAllText(filePath, json);
    }
}
