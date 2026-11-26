using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using PriceWise.Collector.Models;
using PriceWise.Collector.Validation;

namespace PriceWise.Collector.Adapters;

public class ArpicoCollector : ISupermarketCollector
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ArpicoCollector> _logger;

    public string SupermarketSlug => "arpico";
    public string SupermarketName => "Arpico Supercentre";
    public string WebsiteUrl => "https://myarpico.com/shop/";
    public string IntegrationStatus => "active";
    public string? StatusReason => "Live public OpenCart search catalog permitted under robots.txt content signals.";

    public ArpicoCollector(HttpClient httpClient, ILogger<ArpicoCollector> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<CollectorResult> CollectProductsAsync(IEnumerable<string> searchTerms, CancellationToken cancellationToken = default)
    {
        var collected = new List<RawProductItem>();
        var seenProductIds = new HashSet<string>();
        int totalChecked = 0;

        _logger.LogInformation("Starting Arpico Supercentre price collection...");

        try
        {
            foreach (var term in searchTerms)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                var searchUrl = $"https://myarpico.com/index.php?route=product/search&language=en-gb&search={Uri.EscapeDataString(term)}";
                _logger.LogInformation("Fetching Arpico search: {Url}", searchUrl);

                using var request = new HttpRequestMessage(HttpMethod.Get, searchUrl);
                request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) PriceWiseLK/1.0 (+https://github.com/pricewise-lk)");

                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Arpico search for '{Term}' returned HTTP status {StatusCode}.", term, response.StatusCode);
                    continue;
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken);
                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                var productNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'product-thumb')]");
                if (productNodes == null || productNodes.Count == 0)
                {
                    _logger.LogInformation("No product cards found on Arpico for term '{Term}'.", term);
                    continue;
                }

                foreach (var node in productNodes)
                {
                    totalChecked++;

                    var titleNode = node.SelectSingleNode(".//h4[contains(@class, 'product-title')]//a")
                                 ?? node.SelectSingleNode(".//h4//a");
                    if (titleNode == null)
                        continue;

                    var rawTitle = HtmlEntity.DeEntitize(titleNode.InnerText).Trim();
                    var cleanTitle = DataValidator.NormalizeName(rawTitle);
                    if (string.IsNullOrWhiteSpace(cleanTitle))
                        continue;

                    var productUrl = titleNode.GetAttributeValue("href", string.Empty);
                    if (string.IsNullOrWhiteSpace(productUrl))
                        continue;

                    // Extract product_id from URL query string
                    var matchId = Regex.Match(productUrl, @"product_id=(\d+)", RegexOptions.IgnoreCase);
                    var sourceProductId = matchId.Success ? matchId.Groups[1].Value : cleanTitle.ToLowerInvariant().Replace(" ", "-");

                    if (seenProductIds.Contains(sourceProductId))
                        continue;

                    // Extract image
                    var imgNode = node.SelectSingleNode(".//div[contains(@class, 'image')]//img");
                    var imageUrl = imgNode?.GetAttributeValue("src", string.Empty);

                    // Extract prices
                    decimal currentPrice = 0;
                    decimal? originalPrice = null;

                    var priceNewNode = node.SelectSingleNode(".//span[contains(@class, 'price-new')]");
                    var priceOldNode = node.SelectSingleNode(".//span[contains(@class, 'price-old')]");

                    if (priceNewNode != null)
                    {
                        currentPrice = ParsePrice(priceNewNode.InnerText);
                        if (priceOldNode != null)
                        {
                            var old = ParsePrice(priceOldNode.InnerText);
                            if (old > currentPrice)
                                originalPrice = old;
                        }
                    }
                    else
                    {
                        var priceBlock = node.SelectSingleNode(".//p[contains(@class, 'price')]");
                        if (priceBlock != null)
                        {
                            currentPrice = ParsePrice(priceBlock.InnerText);
                        }
                    }

                    if (currentPrice <= 0)
                        continue;

                    var (quantity, unit) = DataValidator.ParseQuantityAndUnit(cleanTitle, 0, "pcs");

                    var rawItem = new RawProductItem
                    {
                        SourceSupermarketSlug = SupermarketSlug,
                        SourceProductId = sourceProductId,
                        SourceProductName = cleanTitle,
                        Price = currentPrice,
                        OriginalPrice = originalPrice,
                        Currency = "LKR",
                        ProductUrl = productUrl,
                        ImageUrl = imageUrl,
                        Brand = Matching.ProductMatcher.ExtractBrandFromName(cleanTitle),
                        Barcode = null,
                        Quantity = quantity,
                        Unit = unit,
                        Category = "General Grocery",
                        AvailabilityStatus = "unknown", // OpenCart search cards do not expose verified stock levels
                        ObservedAt = DateTime.UtcNow
                    };

                    if (DataValidator.ValidateRawProduct(rawItem, out var reason))
                    {
                        collected.Add(rawItem);
                        seenProductIds.Add(sourceProductId);
                    }
                    else
                    {
                        _logger.LogWarning("Validation rejected Arpico item: {Reason}", reason);
                    }
                }

                // Friendly delay
                await Task.Delay(2000, cancellationToken);
            }

            _logger.LogInformation("Arpico collection finished. Validated {Count} unique products out of {Checked} checked.", collected.Count, totalChecked);

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
            _logger.LogError(ex, "Failed to collect products from Arpico: {Message}", ex.Message);
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

    private static decimal ParsePrice(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;

        // Examples: "₨ 140.00", "₨ 1,250.00", "140.00"
        var clean = Regex.Replace(text, @"[^\d\.]", "");
        if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out var price))
            return price;

        return 0;
    }

    private static string ExtractBrandFromName(string name)
    {
        var knownBrands = new[] { "Milkmaid", "Ambev", "Ambewela", "Maliban", "Munchee", "Dilmah", "Watawala", "Highland", "Kotmale", "Anchor", "Elephant House", "Kist", "Maggi", "Prima", "Sunlight", "Dettol", "Lifebuoy", "Signal", "Astra", "Flora", "MD", "Harischandra", "Iodex", "Samahan" };
        foreach (var brand in knownBrands)
        {
            if (name.StartsWith(brand, StringComparison.OrdinalIgnoreCase) || name.Contains($" {brand} ", StringComparison.OrdinalIgnoreCase))
                return brand;
        }
        return "Generic";
    }
}
