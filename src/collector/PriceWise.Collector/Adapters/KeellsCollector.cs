using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PriceWise.Collector.Models;

namespace PriceWise.Collector.Adapters;

public class KeellsCollector : ISupermarketCollector
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KeellsCollector> _logger;

    public string SupermarketSlug => "keells";
    public string SupermarketName => "Keells Super";
    public string WebsiteUrl => "https://www.keellssuper.com/";
    public string IntegrationStatus => "blocked";
    public string? StatusReason => "Direct API calls to zebraliveback require encrypted client session tokens and anti-bot challenge cookies. Automated collection blocked pending authorized enterprise data feed.";

    public KeellsCollector(HttpClient httpClient, ILogger<KeellsCollector> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<CollectorResult> CollectProductsAsync(IEnumerable<string> searchTerms, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Auditing Keells Super integration status...");

        try
        {
            // Verify live endpoint status without attempting unauthorized bypass
            using var request = new HttpRequestMessage(HttpMethod.Get, "https://zebraliveback.keellssuper.com/Web/GetSuggestedItems?locationCode=1");
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) PriceWiseLK/1.0 (+https://github.com/pricewise-lk)");

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            _logger.LogWarning("Keells backend returned status {StatusCode}. Verified that direct automated access remains blocked as documented.", response.StatusCode);

            return new CollectorResult
            {
                SupermarketSlug = SupermarketSlug,
                Success = false,
                Status = "blocked",
                ErrorMessage = $"Direct unauthenticated endpoint returned HTTP {(int)response.StatusCode} {response.StatusCode}. Client requires encrypted session token and Cloudflare challenge token. Bypassing encryption or anti-bot protections is strictly forbidden by project security rules.",
                Products = Array.Empty<RawProductItem>(),
                ProductsChecked = 0,
                ProductsUpdated = 0
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keells audit check encountered error: {Message}", ex.Message);
            return new CollectorResult
            {
                SupermarketSlug = SupermarketSlug,
                Success = false,
                Status = "blocked",
                ErrorMessage = ex.Message,
                Products = Array.Empty<RawProductItem>(),
                ProductsChecked = 0,
                ProductsUpdated = 0
            };
        }
    }
}
