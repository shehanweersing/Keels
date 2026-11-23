using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PriceWise.Collector.Matching;
using PriceWise.Collector.Models;

namespace PriceWise.Collector.Storage;

public class SupabaseStorageService : IStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly string _supabaseUrl;
    private readonly string _serviceRoleKey;
    private readonly ProductMatcher _matcher;
    private readonly ILogger<SupabaseStorageService> _logger;

    public SupabaseStorageService(
        HttpClient httpClient,
        string supabaseUrl,
        string serviceRoleKey,
        ProductMatcher matcher,
        ILogger<SupabaseStorageService> logger)
    {
        _httpClient = httpClient;
        _supabaseUrl = (supabaseUrl ?? string.Empty).Trim().Trim('"', '\'').TrimEnd('/');
        _serviceRoleKey = (serviceRoleKey ?? string.Empty).Trim().Trim('"', '\'');
        _matcher = matcher;
        _logger = logger;
    }

    internal SupabaseStorageService(string supabaseUrl, string serviceRoleKey)
        : this(new HttpClient(), supabaseUrl, serviceRoleKey, new ProductMatcher(), null!)
    {
    }

    internal HttpRequestMessage CreatePostgRestRequest(HttpMethod method, string tablePath)
    {
        var request = new HttpRequestMessage(method, $"{_supabaseUrl}/rest/v1/{tablePath}");
        
        // Supabase requires the 'apikey' header for all API requests
        request.Headers.Add("apikey", _serviceRoleKey);

        // Crucial: Supabase's new 'sb_secret_...' keys are opaque secret keys, NOT JWTs.
        // Sending 'Authorization: Bearer sb_secret_...' causes PostgREST to attempt JWT validation and return HTTP 401 Unauthorized!
        // Only attach 'Authorization: Bearer' for legacy JWT keys (which start with 'ey').
        if (_serviceRoleKey.StartsWith("ey", StringComparison.OrdinalIgnoreCase))
        {
            request.Headers.Add("Authorization", $"Bearer {_serviceRoleKey}");
        }

        // Set backend User-Agent so Supabase gateway does not mistake collector for a browser
        request.Headers.Add("User-Agent", "PriceWiseLK-Collector/1.0");

        return request;
    }

    public async Task<List<Product>> GetCanonicalProductsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreatePostgRestRequest(HttpMethod.Get, "products?select=*");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to fetch products from Supabase: HTTP {Status} - {Error}", response.StatusCode, err);
                throw new InvalidOperationException($"Failed to fetch products from Supabase: HTTP {response.StatusCode} - {err}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<List<Product>>(json, JsonOptions) ?? new List<Product>();
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Error getting products from Supabase: {Message}", ex.Message);
            throw new InvalidOperationException($"Error retrieving canonical products from Supabase: {ex.Message}", ex);
        }
    }

    public async Task<List<Supermarket>> GetSupermarketsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var request = CreatePostgRestRequest(HttpMethod.Get, "supermarkets?select=*");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to fetch supermarkets from Supabase: HTTP {Status} - {Error}", response.StatusCode, err);
                throw new InvalidOperationException($"Failed to fetch supermarkets from Supabase: HTTP {response.StatusCode} - {err}");
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var list = JsonSerializer.Deserialize<List<Supermarket>>(json, JsonOptions) ?? new List<Supermarket>();
            if (list.Count == 0)
            {
                list = await SeedDefaultSupermarketsAsync(cancellationToken);
            }
            return list;
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Error getting supermarkets from Supabase: {Message}", ex.Message);
            throw new InvalidOperationException($"Error retrieving supermarkets from Supabase: {ex.Message}", ex);
        }
    }

    private async Task<List<Supermarket>> SeedDefaultSupermarketsAsync(CancellationToken cancellationToken)
    {
        var defaults = new List<Supermarket>
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
        };

        try
        {
            using var req = CreatePostgRestRequest(HttpMethod.Post, "supermarkets?on_conflict=slug");
            req.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");
            var payload = defaults.Select(s => new
            {
                id = s.Id,
                name = s.Name,
                slug = s.Slug,
                website_url = s.WebsiteUrl,
                integration_status = s.IntegrationStatus,
                status_reason = s.StatusReason
            });
            req.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
            var resp = await _httpClient.SendAsync(req, cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully initialized default supermarkets in Supabase database.");
                return defaults;
            }
            else
            {
                var err = await resp.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Supabase returned error when auto-seeding supermarkets: HTTP {Status} - {Error}", resp.StatusCode, err);
                throw new InvalidOperationException($"Failed to auto-seed supermarkets into Supabase: HTTP {resp.StatusCode} - {err}");
            }
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            _logger.LogError(ex, "Could not auto-seed supermarkets into Supabase: {Message}", ex.Message);
            throw;
        }
    }

    public async Task SaveCollectorRunAsync(CollectorRun run, CancellationToken cancellationToken = default)
    {
        using var request = CreatePostgRestRequest(HttpMethod.Post, "collector_runs");
        var payload = new
        {
            id = run.Id,
            supermarket_id = run.SupermarketId,
            started_at = run.StartedAt,
            finished_at = run.FinishedAt,
            status = run.Status,
            products_checked = run.ProductsChecked,
            products_updated = run.ProductsUpdated,
            error_message = run.ErrorMessage
        };

        request.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOptions), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var err = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to save collector run to Supabase: HTTP {Status} - {Error}", response.StatusCode, err);
            throw new InvalidOperationException($"Failed to save collector run to Supabase: HTTP {response.StatusCode} - {err}");
        }
    }

    public async Task SaveCollectedProductsAsync(
        Supermarket supermarket,
        IReadOnlyList<RawProductItem> items,
        CancellationToken cancellationToken = default)
    {
        var existingCatalog = await GetCanonicalProductsAsync(cancellationToken);

        // Fetch store location for this supermarket (if configured)
        Guid? storeLocationId = null;
        try
        {
            using var locReq = CreatePostgRestRequest(HttpMethod.Get, $"store_locations?supermarket_id=eq.{supermarket.Id}&select=id&limit=1");
            using var locResp = await _httpClient.SendAsync(locReq, cancellationToken);
            if (locResp.IsSuccessStatusCode)
            {
                var locJson = await locResp.Content.ReadAsStringAsync(cancellationToken);
                var locList = JsonSerializer.Deserialize<List<StoreLocationDto>>(locJson, JsonOptions);
                storeLocationId = locList?.FirstOrDefault()?.Id;
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not fetch store location for supermarket {Name}: {Message}", supermarket.Name, ex.Message);
        }

        // Fetch existing store_products to prevent same-supermarket multiple offers on one canonical product
        var existingStoreProducts = new HashSet<Guid>();
        try
        {
            using var req = CreatePostgRestRequest(HttpMethod.Get, $"store_products?supermarket_id=eq.{supermarket.Id}&select=product_id");
            using var resp = await _httpClient.SendAsync(req, cancellationToken);
            if (resp.IsSuccessStatusCode)
            {
                var json = await resp.Content.ReadAsStringAsync(cancellationToken);
                var list = JsonSerializer.Deserialize<List<StoreProductRefDto>>(json, JsonOptions);
                if (list != null)
                {
                    foreach (var item in list)
                    {
                        if (item.ProductId.HasValue) existingStoreProducts.Add(item.ProductId.Value);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch existing store products for supermarket {Name}: {Message}", supermarket.Name, ex.Message);
        }

        int persistedCount = 0;
        foreach (var rawItem in items)
        {
            // Only consider canonical products that do NOT already have an offer from this supermarket
            var candidateProducts = existingCatalog
                .Where(p => !existingStoreProducts.Contains(p.Id))
                .ToList();

            var matchResult = _matcher.MatchOrCreateProduct(rawItem, candidateProducts);
            var canonical = matchResult.CanonicalProduct;
            existingStoreProducts.Add(canonical.Id);

            if (matchResult.IsNewProduct)
            {
                // Insert new canonical product
                using var pReq = CreatePostgRestRequest(HttpMethod.Post, "products");
                var pPayload = new
                {
                    id = canonical.Id,
                    canonical_name = canonical.CanonicalName,
                    brand = canonical.Brand,
                    barcode = canonical.Barcode,
                    quantity = canonical.Quantity,
                    unit = canonical.Unit,
                    category = canonical.Category
                };
                pReq.Content = new StringContent(JsonSerializer.Serialize(pPayload, JsonOptions), Encoding.UTF8, "application/json");
                using var pResp = await _httpClient.SendAsync(pReq, cancellationToken);
                if (pResp.IsSuccessStatusCode)
                {
                    existingCatalog.Add(canonical);
                }
                else
                {
                    var pErr = await pResp.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to insert canonical product '{Name}': HTTP {Status} - {Error}", canonical.CanonicalName, pResp.StatusCode, pErr);
                    throw new InvalidOperationException($"Failed to insert canonical product '{canonical.CanonicalName}': HTTP {pResp.StatusCode} - {pErr}");
                }
            }

            // Upsert store_products
            using var spReq = CreatePostgRestRequest(HttpMethod.Post, "store_products?on_conflict=supermarket_id,source_product_id");
            spReq.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");
            var spPayload = new
            {
                supermarket_id = supermarket.Id,
                store_location_id = storeLocationId,
                product_id = canonical.Id,
                source_product_id = rawItem.SourceProductId,
                source_product_name = rawItem.SourceProductName,
                product_url = rawItem.ProductUrl,
                image_url = rawItem.ImageUrl,
                availability_status = rawItem.AvailabilityStatus,
                last_observed_at = rawItem.ObservedAt
            };
            spReq.Content = new StringContent(JsonSerializer.Serialize(spPayload, JsonOptions), Encoding.UTF8, "application/json");
            using var spResp = await _httpClient.SendAsync(spReq, cancellationToken);

            Guid storeProductId = Guid.Empty;
            if (spResp.IsSuccessStatusCode)
            {
                var spJson = await spResp.Content.ReadAsStringAsync(cancellationToken);
                try
                {
                    var insertedList = JsonSerializer.Deserialize<List<StoreProductDto>>(spJson, JsonOptions);
                    storeProductId = insertedList?.FirstOrDefault()?.Id ?? Guid.Empty;
                }
                catch (JsonException jex)
                {
                    _logger.LogError(jex, "Could not parse store_products response as list: {Json}", spJson);
                    throw new InvalidOperationException($"Could not parse store_products response as list: {spJson}", jex);
                }
            }
            else
            {
                var spErr = await spResp.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to upsert store product '{Name}' (SKU {SKU}): HTTP {Status} - {Error}", 
                    rawItem.SourceProductName, rawItem.SourceProductId, spResp.StatusCode, spErr);
                throw new InvalidOperationException($"Failed to upsert store product '{rawItem.SourceProductName}' (SKU {rawItem.SourceProductId}): HTTP {spResp.StatusCode} - {spErr}");
            }

            if (storeProductId == Guid.Empty)
            {
                throw new InvalidOperationException($"Supabase upsert did not return a valid StoreProductId for SKU {rawItem.SourceProductId}");
            }

            // Insert price observation
            using var obsReq = CreatePostgRestRequest(HttpMethod.Post, "price_observations");
            var obsPayload = new
            {
                store_product_id = storeProductId,
                price = rawItem.Price,
                original_price = rawItem.OriginalPrice,
                currency = rawItem.Currency,
                observed_at = rawItem.ObservedAt,
                source_url = rawItem.ProductUrl
            };
            obsReq.Content = new StringContent(JsonSerializer.Serialize(obsPayload, JsonOptions), Encoding.UTF8, "application/json");
            using var obsResp = await _httpClient.SendAsync(obsReq, cancellationToken);
            if (!obsResp.IsSuccessStatusCode)
            {
                var obsErr = await obsResp.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogError("Failed to insert price observation for SKU {SKU}: HTTP {Status} - {Error}", 
                    rawItem.SourceProductId, obsResp.StatusCode, obsErr);
                throw new InvalidOperationException($"Failed to insert price observation for SKU {rawItem.SourceProductId}: HTTP {obsResp.StatusCode} - {obsErr}");
            }

            // Upsert product_matches audit record for cross-supermarket matches (only for matched products, not new_product)
            if (!matchResult.IsNewProduct && !string.Equals(matchResult.MatchMethod, "new_product", StringComparison.OrdinalIgnoreCase))
            {
                using var pmReq = CreatePostgRestRequest(HttpMethod.Post, "product_matches?on_conflict=product_id,store_product_id");
                pmReq.Headers.Add("Prefer", "resolution=merge-duplicates,return=representation");
                var pmPayload = new
                {
                    product_id = canonical.Id,
                    store_product_id = storeProductId,
                    match_method = matchResult.MatchMethod,
                    confidence = matchResult.Confidence,
                    verification_status = matchResult.VerificationStatus
                };
                pmReq.Content = new StringContent(JsonSerializer.Serialize(pmPayload, JsonOptions), Encoding.UTF8, "application/json");
                using var pmResp = await _httpClient.SendAsync(pmReq, cancellationToken);
                if (!pmResp.IsSuccessStatusCode)
                {
                    var pmErr = await pmResp.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogError("Failed to upsert product match for product {ProdId} and store product {StoreProdId}: HTTP {Status} - {Error}",
                        canonical.Id, storeProductId, pmResp.StatusCode, pmErr);
                    throw new InvalidOperationException($"Failed to upsert product match for product {canonical.Id}: HTTP {pmResp.StatusCode} - {pmErr}");
                }
            }

            persistedCount++;
        }

        _logger.LogInformation("Successfully persisted {Count} verified store products, observations, and matches to Supabase for {Name}.",
            persistedCount, supermarket.Name);
    }

    private class StoreLocationDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
    }

    private class StoreProductDto
    {
        [JsonPropertyName("id")]
        public Guid Id { get; set; }
    }

    private class StoreProductRefDto
    {
        [JsonPropertyName("product_id")]
        public Guid? ProductId { get; set; }
    }
}
