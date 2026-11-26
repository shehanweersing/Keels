using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PriceWise.Collector.Models;
using PriceWise.Collector.Validation;

namespace PriceWise.Collector.Adapters;

public class KeellsCollectorOptions
{
    public string OutletCode { get; set; } = "SCDR";
    public int PageSize { get; set; } = 20;
    public int MaxPagesPerTerm { get; set; } = 3;
    public bool IsPromotionOnly { get; set; } = false;
    public bool StockStatus { get; set; } = true;
    public decimal ItemPriceFrom { get; set; } = 0;
    public decimal ItemPriceTo { get; set; } = 50000;
    public string SubDepartmentCode { get; set; } = "ALL";
    public int RequestDelayMs { get; set; } = 1500;
    public int MaxRetries { get; set; } = 2;
    public int TimeoutSeconds { get; set; } = 35;
}

public class KeellsCollector : ISupermarketCollector
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<KeellsCollector> _logger;
    private readonly KeellsCollectorOptions _options;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public string SupermarketSlug => "keells";
    public string SupermarketName => "Keells Super";
    public string WebsiteUrl => "https://www.keellssuper.com/";
    public string IntegrationStatus => "active";
    public string? StatusReason => "Live public Guest Browsing JSON API (Login/GuestLogin + 2.0/Web/GetItemDetails) with outlet-scoped LKR pricing.";

    public KeellsCollector(HttpClient httpClient, ILogger<KeellsCollector> logger, KeellsCollectorOptions? options = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _options = options ?? new KeellsCollectorOptions();
        if (_httpClient.Timeout > TimeSpan.FromSeconds(_options.TimeoutSeconds))
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        }
    }

    public string BuildGetItemDetailsUrl(int fromCount, int toCount, string? outletCode = null, string? searchTerm = null)
    {
        var outlet = Uri.EscapeDataString(string.IsNullOrWhiteSpace(outletCode) ? _options.OutletCode : outletCode);
        var subDept = Uri.EscapeDataString(_options.SubDepartmentCode);
        var isPromo = _options.IsPromotionOnly ? "true" : "false";
        var stock = _options.StockStatus ? "true" : "false";
        var desc = string.IsNullOrWhiteSpace(searchTerm) ? string.Empty : Uri.EscapeDataString(searchTerm.Trim());

        return $"https://zebraliveback.keellssuper.com/2.0/Web/GetItemDetails" +
               $"?fromCount={fromCount}" +
               $"&toCount={toCount}" +
               $"&outletCode={outlet}" +
               $"&isPromotionOnly={isPromo}" +
               $"&stockStatus={stock}" +
               $"&itemPricefrom={_options.ItemPriceFrom}" +
               $"&itemPriceTo={_options.ItemPriceTo}" +
               $"&subDepartmentCode={subDept}" +
               $"&itemDescription={desc}";
    }

    public async Task<CollectorResult> CollectProductsAsync(IEnumerable<string> searchTerms, CancellationToken cancellationToken = default)
    {
        var termsList = (searchTerms ?? Array.Empty<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (termsList.Count == 0)
        {
            termsList.Add(string.Empty);
        }

        _logger.LogInformation("Starting Keells Super guest collection (Terms: {TermsCount}, MaxPagesPerTerm: {MaxPages})...",
            termsList.Count, _options.MaxPagesPerRunOrDefault(_options.MaxPagesPerTerm));

        var collected = new List<RawProductItem>();
        var seenItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int totalChecked = 0;

        try
        {
            // Step 1: Initialize a legitimate public guest session (no user login required)
            var (guestSessionId, preferredOutlet, guestStatusCode, guestErr) = await InitializeGuestSessionAsync(cancellationToken);

            if (guestStatusCode == HttpStatusCode.Unauthorized || guestStatusCode == HttpStatusCode.Forbidden)
            {
                var blockMsg = $"GuestLogin returned HTTP {(int)guestStatusCode} {guestStatusCode}. Automated guest access is blocked by server policy.";
                _logger.LogWarning("Keells Super guest session blocked: {Message}", blockMsg);
                return BlockedResult(blockMsg, totalChecked);
            }

            if (string.IsNullOrWhiteSpace(guestSessionId))
            {
                var failMsg = guestErr ?? "Failed to obtain public guest session from Keells GuestLogin.";
                _logger.LogError("Keells Super guest session failed: {Message}", failMsg);
                return FailedResult(failMsg, collected, totalChecked);
            }

            var activeOutlet = !string.IsNullOrWhiteSpace(preferredOutlet) ? preferredOutlet : _options.OutletCode;
            _logger.LogInformation("Keells guest session initialized (Outlet: {Outlet}).", activeOutlet);

            // Step 2: Query GetItemDetails per search term with safe rate-limited pagination
            foreach (var term in termsList)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                for (int page = 0; page < _options.MaxPagesPerTerm; page++)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    int fromCount = page * _options.PageSize;
                    var url = BuildGetItemDetailsUrl(fromCount, _options.PageSize, activeOutlet, term);

                    var (statusCode, body, errorMsg) = await FetchWithBoundedRetriesAsync(url, guestSessionId, cancellationToken);

                    if (statusCode == HttpStatusCode.Unauthorized || statusCode == HttpStatusCode.Forbidden)
                    {
                        var blockMsg = $"GetItemDetails returned HTTP {(int)statusCode} {statusCode} even with a public guest session. Bypassing access controls is prohibited.";
                        _logger.LogWarning("Keells Super collection blocked at term '{Term}' page {Page}: {Message}", term, page, blockMsg);
                        return BlockedResult(blockMsg, totalChecked);
                    }

                    if (statusCode == null || (int)statusCode < 200 || (int)statusCode >= 300)
                    {
                        var failMsg = errorMsg ?? $"Keells API request failed with HTTP {(statusCode.HasValue ? ((int)statusCode.Value).ToString() : "N/A")}.";
                        _logger.LogError("Keells Super collection failed at term '{Term}' page {Page}: {Message}", term, page, failMsg);
                        return FailedResult(failMsg, collected, totalChecked);
                    }

                    var pageResult = ParseResponse(body ?? string.Empty, activeOutlet, DateTime.UtcNow);
                    totalChecked += pageResult.TotalRawCount;

                    if (pageResult.TotalRawCount == 0)
                        break;

                    int addedThisPage = 0;
                    foreach (var item in pageResult.ValidatedItems)
                    {
                        if (seenItemIds.Add(item.SourceProductId))
                        {
                            collected.Add(item);
                            addedThisPage++;
                        }
                    }

                    _logger.LogInformation("Keells term '{Term}' page {Page} (fromCount={FromCount}): raw={RawCount}, newUnique={Added}.",
                        string.IsNullOrEmpty(term) ? "ALL" : term, page, fromCount, pageResult.TotalRawCount, addedThisPage);

                    // Partial page or zero newly discovered items signals end of pagination
                    if (pageResult.PrimaryListCount < _options.PageSize || addedThisPage == 0)
                        break;

                    if (_options.RequestDelayMs > 0)
                        await Task.Delay(_options.RequestDelayMs, cancellationToken);
                }

                if (_options.RequestDelayMs > 0)
                    await Task.Delay(_options.RequestDelayMs, cancellationToken);
            }

            _logger.LogInformation("Keells collection finished. Validated {Count} unique products out of {Checked} checked.",
                collected.Count, totalChecked);

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
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            const string timeoutMsg = "Keells API request timed out.";
            _logger.LogError(timeoutMsg);
            return FailedResult(timeoutMsg, collected, totalChecked);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Keells collector encountered unexpected error: {Message}", ex.Message);
            return FailedResult(ex.Message, collected, totalChecked);
        }
    }

    private async Task<(string? SessionId, string? PreferredOutlet, HttpStatusCode? StatusCode, string? Error)> InitializeGuestSessionAsync(CancellationToken cancellationToken)
    {
        const string guestLoginUrl = "https://zebraliveback.keellssuper.com/1.0/Login/GuestLogin";

        for (int attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, guestLoginUrl);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) PriceWiseLK/1.0 (+https://github.com/shehanweersing/Keels)");
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("Origin", "https://www.keellssuper.com");
            request.Headers.Add("Referer", "https://www.keellssuper.com/");
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    return (null, null, response.StatusCode, $"HTTP {(int)response.StatusCode} {response.StatusCode}");
                }

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync(cancellationToken);
                    var parsed = JsonSerializer.Deserialize<KeellsGuestLoginResponse>(json, JsonOptions);
                    var sessionId = parsed?.Result?.UserSessionID;
                    var outlet = parsed?.Result?.PreferredOutlet;

                    if (string.IsNullOrWhiteSpace(sessionId))
                    {
                        return (null, null, response.StatusCode, "GuestLogin succeeded but did not return userSessionID.");
                    }

                    return (sessionId.Trim(), outlet?.Trim(), response.StatusCode, null);
                }

                if (((int)response.StatusCode == 429 || (int)response.StatusCode >= 500) && attempt < _options.MaxRetries)
                {
                    await Task.Delay(1000 * (attempt + 1), cancellationToken);
                    continue;
                }

                return (null, null, response.StatusCode, $"GuestLogin returned HTTP {(int)response.StatusCode} {response.StatusCode}");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < _options.MaxRetries)
            {
                _logger.LogWarning("Keells GuestLogin timed out on attempt {Attempt}; retrying...", attempt + 1);
                await Task.Delay(1000 * (attempt + 1), cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return (null, null, null, "Keells GuestLogin request timed out after bounded retries.");
            }
            catch (HttpRequestException ex) when (attempt < _options.MaxRetries)
            {
                _logger.LogWarning("Transient HTTP error on Keells GuestLogin attempt {Attempt}: {Message}", attempt + 1, ex.Message);
                await Task.Delay(1000 * (attempt + 1), cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                return (null, null, null, ex.Message);
            }
        }

        return (null, null, null, "Exceeded maximum GuestLogin retry attempts.");
    }

    private async Task<(HttpStatusCode? StatusCode, string? Body, string? Error)> FetchWithBoundedRetriesAsync(
        string url,
        string guestSessionId,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt <= _options.MaxRetries; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) PriceWiseLK/1.0 (+https://github.com/shehanweersing/Keels)");
            request.Headers.Add("Accept", "application/json");
            request.Headers.Add("usersessionid", guestSessionId);
            request.Headers.Add("Origin", "https://www.keellssuper.com");
            request.Headers.Add("Referer", "https://www.keellssuper.com/");

            try
            {
                using var response = await _httpClient.SendAsync(request, cancellationToken);

                // Never retry auth failures
                if (response.StatusCode == HttpStatusCode.Unauthorized || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    return (response.StatusCode, null, $"HTTP {(int)response.StatusCode} {response.StatusCode}");
                }

                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync(cancellationToken);
                    return (response.StatusCode, content, null);
                }

                // Retry only on transient 429 or 5xx
                if (((int)response.StatusCode == 429 || (int)response.StatusCode >= 500) && attempt < _options.MaxRetries)
                {
                    await Task.Delay(1000 * (attempt + 1), cancellationToken);
                    continue;
                }

                return (response.StatusCode, null, $"HTTP {(int)response.StatusCode} {response.StatusCode}");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested && attempt < _options.MaxRetries)
            {
                _logger.LogWarning("Keells GetItemDetails timed out on attempt {Attempt}; retrying...", attempt + 1);
                await Task.Delay(1000 * (attempt + 1), cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                return (null, null, "Keells GetItemDetails request timed out after bounded retries.");
            }
            catch (HttpRequestException ex) when (attempt < _options.MaxRetries)
            {
                _logger.LogWarning("Transient HTTP error on Keells attempt {Attempt}: {Message}", attempt + 1, ex.Message);
                await Task.Delay(1000 * (attempt + 1), cancellationToken);
            }
            catch (HttpRequestException ex)
            {
                return (null, null, ex.Message);
            }
        }

        return (null, null, "Exceeded maximum retry attempts.");
    }

    public KeellsParseResult ParseResponse(string json, string outletCode, DateTime observedAt)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new KeellsParseResult(0, 0, Array.Empty<RawProductItem>());
        }

        KeellsApiEnvelope? envelope;
        try
        {
            envelope = JsonSerializer.Deserialize<KeellsApiEnvelope>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize Keells GetItemDetails JSON response.");
            return new KeellsParseResult(0, 0, Array.Empty<RawProductItem>());
        }

        if (envelope == null)
        {
            return new KeellsParseResult(0, 0, Array.Empty<RawProductItem>());
        }

        // Support both live wrapped { result: { itemDetailsList: [...] } } and unwrapped { itemDetailsList: [...] }
        var primaryList = envelope.Result?.ItemDetailsList ?? envelope.ItemDetailsList ?? new List<KeellsItemDto>();
        var promoList = envelope.Result?.PromotionItemDetailsList ?? envelope.PromotionItemDetailsList ?? new List<KeellsItemDto>();
        var combined = primaryList.Concat(promoList).ToList();

        var validated = new List<RawProductItem>();
        var localSeen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dto in combined)
        {
            var rawId = !string.IsNullOrWhiteSpace(dto.ItemID)
                ? dto.ItemID.Trim()
                : dto.ItemCode?.Trim();

            if (string.IsNullOrWhiteSpace(rawId))
                continue;

            var sourceProductId = string.IsNullOrWhiteSpace(outletCode)
                ? rawId
                : $"{outletCode}:{rawId}";

            if (!localSeen.Add(sourceProductId))
                continue;

            if (!dto.Amount.HasValue || dto.Amount.Value <= 0)
                continue;

            var cleanName = DataValidator.NormalizeName(dto.Name ?? string.Empty);
            if (string.IsNullOrWhiteSpace(cleanName))
                continue;

            var (quantity, unit) = DataValidator.ParseQuantityAndUnit(cleanName, 0, "pcs");

            var availability = (dto.IsAvailable, dto.StockInHand) switch
            {
                (false, _) => "out_of_stock",
                (_, <= 0) => "out_of_stock",
                (true, _) => "in_stock",
                (null, > 0) => "in_stock",
                _ => "unknown"
            };

            var codeForUrl = !string.IsNullOrWhiteSpace(dto.ItemCode) ? dto.ItemCode.Trim() : rawId;
            var productUrl = $"https://www.keellssuper.com/product-details?itemCode={Uri.EscapeDataString(codeForUrl)}&outletCode={Uri.EscapeDataString(outletCode)}";

            var imageUrl = !string.IsNullOrWhiteSpace(dto.ImageUrl) && Uri.TryCreate(dto.ImageUrl, UriKind.Absolute, out _)
                ? dto.ImageUrl.Trim()
                : null;

            var rawItem = new RawProductItem
            {
                SourceSupermarketSlug = SupermarketSlug,
                SourceProductId = sourceProductId,
                SourceProductName = cleanName,
                Price = dto.Amount.Value,
                OriginalPrice = null,
                Currency = "LKR",
                ProductUrl = productUrl,
                ImageUrl = imageUrl,
                Brand = Matching.ProductMatcher.ExtractBrandFromName(cleanName),
                Barcode = null,
                Quantity = quantity,
                Unit = unit,
                Category = "Grocery",
                AvailabilityStatus = availability,
                ObservedAt = observedAt
            };

            if (DataValidator.ValidateRawProduct(rawItem, out var reason))
            {
                validated.Add(rawItem);
            }
            else
            {
                _logger.LogWarning("Validation rejected Keells item '{Name}': {Reason}", cleanName, reason);
            }
        }

        return new KeellsParseResult(combined.Count, primaryList.Count, validated);
    }

    private CollectorResult BlockedResult(string message, int totalChecked) => new()
    {
        SupermarketSlug = SupermarketSlug,
        Success = false,
        Status = "blocked",
        ErrorMessage = message,
        Products = Array.Empty<RawProductItem>(),
        ProductsChecked = totalChecked,
        ProductsUpdated = 0
    };

    private CollectorResult FailedResult(string message, List<RawProductItem> collected, int totalChecked) => new()
    {
        SupermarketSlug = SupermarketSlug,
        Success = false,
        Status = "failed",
        ErrorMessage = message,
        Products = collected,
        ProductsChecked = totalChecked,
        ProductsUpdated = collected.Count
    };

    private static string ExtractBrandFromName(string name)
    {
        var knownBrands = new[]
        {
            "Milkmaid", "Ambewela", "Maliban", "Munchee", "Dilmah", "Watawala",
            "Highland", "Kotmale", "Anchor", "Elephant House", "Kist", "Maggi",
            "Prima", "Sunlight", "Dettol", "Lifebuoy", "Signal", "Astra",
            "Flora", "MD", "Harischandra", "Keells", "Krest", "Samaposha"
        };

        foreach (var brand in knownBrands)
        {
            if (name.StartsWith(brand, StringComparison.OrdinalIgnoreCase) ||
                name.Contains($" {brand} ", StringComparison.OrdinalIgnoreCase))
            {
                return brand;
            }
        }

        return "Generic";
    }
}

internal static class KeellsOptionsExtensions
{
    public static int MaxPagesPerRunOrDefault(this KeellsCollectorOptions opts, int value) => value > 0 ? value : 3;
}

public record KeellsParseResult(
    int TotalRawCount,
    int PrimaryListCount,
    IReadOnlyList<RawProductItem> ValidatedItems);

public class KeellsGuestLoginResponse
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("result")]
    public KeellsGuestLoginResult? Result { get; set; }
}

public class KeellsGuestLoginResult
{
    [JsonPropertyName("userSessionID")]
    public string? UserSessionID { get; set; }

    [JsonPropertyName("preferredOutlet")]
    public string? PreferredOutlet { get; set; }

    [JsonPropertyName("isGuest")]
    public bool IsGuest { get; set; }
}

public class KeellsApiEnvelope
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("result")]
    public KeellsGetItemDetailsResponse? Result { get; set; }

    [JsonPropertyName("itemDetailsList")]
    public List<KeellsItemDto>? ItemDetailsList { get; set; }

    [JsonPropertyName("promotionItemDetailsList")]
    public List<KeellsItemDto>? PromotionItemDetailsList { get; set; }
}

public class KeellsGetItemDetailsResponse
{
    [JsonPropertyName("itemDetailsList")]
    public List<KeellsItemDto>? ItemDetailsList { get; set; }

    [JsonPropertyName("promotionItemDetailsList")]
    public List<KeellsItemDto>? PromotionItemDetailsList { get; set; }
}

public class KeellsItemDto
{
    [JsonPropertyName("itemID")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? ItemID { get; set; }

    [JsonPropertyName("itemCode")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? ItemCode { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("amount")]
    public decimal? Amount { get; set; }

    [JsonPropertyName("imageUrl")]
    public string? ImageUrl { get; set; }

    [JsonPropertyName("stockInHand")]
    public decimal? StockInHand { get; set; }

    [JsonPropertyName("isAvailable")]
    public bool? IsAvailable { get; set; }

    [JsonPropertyName("promotionDiscountValue")]
    public decimal? PromotionDiscountValue { get; set; }

    [JsonPropertyName("discountPercentage")]
    public decimal? DiscountPercentage { get; set; }
}

public class FlexibleStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number => reader.TryGetInt64(out var l) ? l.ToString() : reader.GetDouble().ToString(),
            JsonTokenType.Null => null,
            _ => null
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        if (value == null) writer.WriteNullValue();
        else writer.WriteStringValue(value);
    }
}
