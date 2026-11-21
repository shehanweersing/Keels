using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PriceWise.Collector.Models;
using PriceWise.Collector.Validation;

namespace PriceWise.Collector.Adapters;

public class CargillsCollector : ISupermarketCollector
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<CargillsCollector> _logger;

    public string SupermarketSlug => "cargills";
    public string SupermarketName => "Cargills Food City";
    public string WebsiteUrl => "https://cargillsonline.com/Web/";
    public string IntegrationStatus => "active";
    public string? StatusReason => "Live public JSON search API with full product metadata and exact LKR pricing.";

    public CargillsCollector(HttpClient httpClient, ILogger<CargillsCollector> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<CollectorResult> CollectProductsAsync(IEnumerable<string> searchTerms, CancellationToken cancellationToken = default)
    {
        var collected = new List<RawProductItem>();
        var seenProductIds = new HashSet<string>();
        int totalChecked = 0;

        _logger.LogInformation("Starting Cargills Food City price collection...");

        try
        {
            foreach (var term in searchTerms)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                _logger.LogInformation("Querying Cargills for term '{Term}'...", term);

                using var request = new HttpRequestMessage(HttpMethod.Post, "https://cargillsonline.com/Web/GetSearchBarProductsV1");
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) PriceWiseLK/1.0 (+https://github.com/pricewise-lk)");
                request.Headers.Add("X-Requested-With", "XMLHttpRequest");
                request.Headers.Add("Cookie", "ASP.NET_Pincode=Colombo; ASP.NET_WebStoreType=1; Asp.Net_WebStoreId=1031");

                var formValues = new Dictionary<string, string> { { "Search", term } };
                request.Content = new FormUrlEncodedContent(formValues);

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Cargills search for '{Term}' returned HTTP status {StatusCode}.", term, response.StatusCode);
                    continue;
                }

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                var items = JsonSerializer.Deserialize<List<CargillsItemDto>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (items == null || items.Count == 0)
                {
                    _logger.LogInformation("No items returned from Cargills for '{Term}'.", term);
                    continue;
                }

                foreach (var dto in items)
                {
                    totalChecked++;
                    if (dto.Id <= 0 || string.IsNullOrWhiteSpace(dto.SKUCODE))
                        continue;

                    var sourceId = dto.SKUCODE.Trim();
                    if (seenProductIds.Contains(sourceId))
                        continue;

                    if (!decimal.TryParse(dto.Inventory, out var price) || price <= 0)
                    {
                        _logger.LogWarning("Skipping Cargills item '{ItemName}' due to invalid price '{PriceRaw}'.", dto.ItemName, dto.Inventory);
                        continue;
                    }

                    var cleanName = DataValidator.NormalizeName(dto.ItemName ?? string.Empty);
                    if (string.IsNullOrWhiteSpace(cleanName))
                        continue;

                    decimal quantity = 1;
                    if (decimal.TryParse(dto.UnitSize, out var parsedQty) && parsedQty > 0)
                        quantity = parsedQty;

                    string unit = "pcs";
                    if (!string.IsNullOrWhiteSpace(dto.Uom))
                    {
                        var (_, parsedUnit) = DataValidator.ParseQuantityAndUnit($"1 {dto.Uom.Trim()}");
                        unit = parsedUnit;
                    }

                    var productUrl = !string.IsNullOrWhiteSpace(dto.EnId)
                        ? $"https://cargillsonline.com/Web/ProductDetails?ID={Uri.EscapeDataString(dto.EnId)}"
                        : $"https://cargillsonline.com/Web/Product?IC={dto.CategoryId}";

                    var imageUrl = !string.IsNullOrWhiteSpace(dto.ItemImage)
                        ? (dto.ItemImage.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? dto.ItemImage : $"https://cargillsonline.com{dto.ItemImage}")
                        : null;

                    var rawItem = new RawProductItem
                    {
                        SourceSupermarketSlug = SupermarketSlug,
                        SourceProductId = sourceId,
                        SourceProductName = cleanName,
                        Price = price,
                        OriginalPrice = null,
                        Currency = "LKR",
                        ProductUrl = productUrl,
                        ImageUrl = imageUrl,
                        Brand = ExtractBrandFromName(cleanName),
                        Barcode = null,
                        Quantity = quantity,
                        Unit = unit,
                        Category = dto.MenuCategoryName ?? "Grocery",
                        AvailabilityStatus = "in_stock",
                        ObservedAt = DateTime.UtcNow
                    };

                    if (DataValidator.ValidateRawProduct(rawItem, out var reason))
                    {
                        collected.Add(rawItem);
                        seenProductIds.Add(sourceId);
                    }
                    else
                    {
                        _logger.LogWarning("Validation rejected Cargills item: {Reason}", reason);
                    }
                }

                // Friendly delay between terms
                await Task.Delay(1500, cancellationToken);
            }

            _logger.LogInformation("Cargills collection finished. Validated {Count} unique products out of {Checked} checked.", collected.Count, totalChecked);

            return new CollectorResult
            {
                SupermarketSlug = SupermarketSlug,
                Success = true,
                Status = "success",
                Products = collected,
                ProductsChecked = totalChecked,
                ProductsUpdated = collected.Count
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect products from Cargills: {Message}", ex.Message);
            return new CollectorResult
            {
                SupermarketSlug = SupermarketSlug,
                Success = false,
                Status = "failed",
                ErrorMessage = ex.Message,
                Products = collected,
                ProductsChecked = totalChecked,
                ProductsUpdated = collected.Count
            };
        }
    }

    private static string ExtractBrandFromName(string name)
    {
        var knownBrands = new[] { "Milkmaid", "Ambewela", "Maliban", "Munchee", "Dilmah", "Watawala", "Highland", "Kotmale", "Anchor", "Elephant House", "Kist", "Maggi", "Prima", "Sunlight", "Dettol", "Lifebuoy", "Signal", "Astra", "Flora", "MD", "Harischandra" };
        foreach (var brand in knownBrands)
        {
            if (name.StartsWith(brand, StringComparison.OrdinalIgnoreCase) || name.Contains($" {brand} ", StringComparison.OrdinalIgnoreCase))
                return brand;
        }
        return "Generic";
    }

    private class CargillsItemDto
    {
        [JsonPropertyName("Id")]
        public int Id { get; set; }

        [JsonPropertyName("SKUCODE")]
        public string? SKUCODE { get; set; }

        [JsonPropertyName("ItemName")]
        public string? ItemName { get; set; }

        [JsonPropertyName("ItemImage")]
        public string? ItemImage { get; set; }

        [JsonPropertyName("CategoryId")]
        public string? CategoryId { get; set; }

        [JsonPropertyName("MenuCategoryName")]
        public string? MenuCategoryName { get; set; }

        [JsonPropertyName("Uom")]
        public string? Uom { get; set; }

        [JsonPropertyName("UnitSize")]
        public string? UnitSize { get; set; }

        [JsonPropertyName("Inventory")]
        public string? Inventory { get; set; }

        [JsonPropertyName("EnId")]
        public string? EnId { get; set; }
    }
}
