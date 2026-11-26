using System;
using System.Linq;
using System.Net.Http;
using PriceWise.Collector.Storage;
using Xunit;

namespace PriceWise.Collector.Tests;

public class StorageTests
{
    [Theory]
    [InlineData("sb_publishable_abcdef1234567890", true)]
    [InlineData("sb_secret_abcdef1234567890", false)]
    [InlineData("eyJh...legacy_jwt", false)]
    public void ServiceRoleKey_Validation_DetectsPublishableKeyMismatch(string key, bool isPublishable)
    {
        var startsWithPublishable = key.StartsWith("sb_publishable_", StringComparison.OrdinalIgnoreCase);
        Assert.Equal(isPublishable, startsWithPublishable);
    }

    [Fact]
    public void SupabaseStorageService_ModernSecretKey_DoesNotUseBearerToken()
    {
        // When using Supabase's new opaque secret key format (sb_secret_...),
        // PostgREST rejects Authorization: Bearer with HTTP 401.
        // It must be passed exclusively via the apikey header.
        var secretKey = "sb_secret_test_mock_identifier_for_unit_test";
        var storage = new SupabaseStorageService("https://testproject.supabase.co", secretKey);

        // Access internal CreatePostgRestRequest via reflection or verify request building behavior
        var req = storage.CreatePostgRestRequest(HttpMethod.Get, "products?select=id");

        Assert.True(req.Headers.Contains("apikey"));
        Assert.Equal(secretKey, req.Headers.GetValues("apikey").First());
        // Bearer header MUST NOT be present for sb_secret_ keys
        Assert.Null(req.Headers.Authorization);
    }

    [Fact]
    public void SupabaseStorageService_LegacyJwtKey_IncludesBearerToken()
    {
        // For legacy JWT keys starting with ey..., PostgREST expects Authorization: Bearer
        var legacyJwt = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.test";
        var storage = new SupabaseStorageService("https://testproject.supabase.co", legacyJwt);

        var req = storage.CreatePostgRestRequest(HttpMethod.Get, "products?select=id");

        Assert.True(req.Headers.Contains("apikey"));
        Assert.NotNull(req.Headers.Authorization);
        Assert.Equal("Bearer", req.Headers.Authorization.Scheme);
        Assert.Equal(legacyJwt, req.Headers.Authorization.Parameter);
    }

    [Theory]
    [InlineData("postgresql://postgres:mySecretPass123@db.project.supabase.co:5432/postgres", "postgresql://postgres:***@db.project.supabase.co:5432/postgres")]
    [InlineData("Host=db.project.supabase.co;Database=postgres;Username=postgres;Password=mySecretPass123;", "Host=db.project.supabase.co;Database=postgres;Username=postgres;Password=***;")]
    [InlineData("https://abc.supabase.co", "https://abc.supabase.co")]
    public void MaskConnectionString_HidesSensitiveCredentials(string input, string expectedMasked)
    {
        var masked = Program.MaskConnectionString(input);
        Assert.DoesNotContain("mySecretPass123", masked);
        Assert.Equal(expectedMasked, masked);
    }

    [Fact]
    public void Supermarket_Deserialization_FromSupabaseJson_MustPopulateProperties()
    {
        var json = "[{\"id\":\"a1000000-0000-0000-0000-000000000001\",\"name\":\"Cargills Food City\",\"slug\":\"cargills\",\"website_url\":\"https://cargillsonline.com/Web/\",\"integration_status\":\"active\",\"status_reason\":\"test\",\"created_at\":\"2026-11-22T14:01:51.252428+00:00\"}]";
        
        var list = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<PriceWise.Collector.Models.Supermarket>>(json);
        
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal(Guid.Parse("a1000000-0000-0000-0000-000000000001"), list[0].Id);
        Assert.Equal("Cargills Food City", list[0].Name);
        Assert.Equal("cargills", list[0].Slug);
        Assert.Equal("https://cargillsonline.com/Web/", list[0].WebsiteUrl);
        Assert.Equal("active", list[0].IntegrationStatus);
    }

    [Fact]
    public void Product_Deserialization_FromSupabaseJson_MustPopulateProperties()
    {
        var json = "[{\"id\":\"692f46ab-bd3f-4f4f-9f9b-f1742b0235df\",\"canonical_name\":\"Milkmaid Condensed Milk\",\"brand\":\"Milkmaid\",\"barcode\":\"123456\",\"quantity\":510.00,\"unit\":\"g\",\"category\":\"Dairy\",\"created_at\":\"2026-11-23T04:42:17.106383+00:00\"}]";
        
        var list = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<PriceWise.Collector.Models.Product>>(json);
        
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.Equal(Guid.Parse("692f46ab-bd3f-4f4f-9f9b-f1742b0235df"), list[0].Id);
        Assert.Equal("Milkmaid Condensed Milk", list[0].CanonicalName);
        Assert.Equal("Milkmaid", list[0].Brand);
        Assert.Equal(510.00m, list[0].Quantity);
        Assert.Equal("g", list[0].Unit);
    }

    [Fact]
    public void CollectorRun_Serialization_MustEmitSnakeCasePayload()
    {
        var run = new PriceWise.Collector.Models.CollectorRun
        {
            Id = Guid.Parse("c1000000-0000-0000-0000-000000000001"),
            SupermarketId = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
            StartedAt = new DateTime(2026, 11, 23, 4, 39, 0, DateTimeKind.Utc),
            Status = "success",
            ProductsChecked = 50,
            ProductsUpdated = 45
        };

        var json = System.Text.Json.JsonSerializer.Serialize(run);

        Assert.Contains("\"supermarket_id\":", json);
        Assert.Contains("\"products_checked\":50", json);
        Assert.Contains("\"products_updated\":45", json);
    }

    [Fact]
    public async System.Threading.Tasks.Task UpdateSupermarketStatusAsync_SendsPatchWithSnakeCaseStatusAndReason()
    {
        HttpRequestMessage? capturedReq = null;
        string? capturedBody = null;
        var handler = new TestHttpMessageHandler(async (req, ct) =>
        {
            capturedReq = req;
            capturedBody = await req.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("[]")
            };
        });

        var client = new HttpClient(handler);
        var storage = new SupabaseStorageService(
            client,
            "https://testproject.supabase.co",
            "sb_secret_test_key",
            new PriceWise.Collector.Matching.ProductMatcher(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SupabaseStorageService>.Instance);

        var id = Guid.Parse("a3000000-0000-0000-0000-000000000003");
        await storage.UpdateSupermarketStatusAsync(id, "active", "Verified 322 Keells products.");

        Assert.NotNull(capturedReq);
        Assert.Equal(HttpMethod.Patch, capturedReq!.Method);
        Assert.Contains($"supermarkets?id=eq.{id}", capturedReq.RequestUri!.ToString());
        Assert.Contains("\"integration_status\":\"active\"", capturedBody);
        Assert.Contains("\"status_reason\":\"Verified 322 Keells products.\"", capturedBody);
    }

    [Fact]
    public async System.Threading.Tasks.Task SaveCollectedProductsAsync_RepeatedCollection_IsIdempotent_AndReusesCanonicalProductId()
    {
        var supermarket = new PriceWise.Collector.Models.Supermarket
        {
            Id = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
            Name = "Cargills Food City",
            Slug = "cargills",
            WebsiteUrl = "https://cargillsonline.com/Web/"
        };

        var storedProducts = new System.Collections.Generic.List<object>();
        var storedStoreProducts = new System.Collections.Generic.List<object>();
        int productInserts = 0;
        int observationInserts = 0;

        var handler = new TestHttpMessageHandler(async (req, ct) =>
        {
            var uri = req.RequestUri!.ToString();
            if (req.Method == HttpMethod.Get && uri.Contains("products?"))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(storedProducts))
                };
            }
            if (req.Method == HttpMethod.Get && uri.Contains("store_locations?"))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("[]") };
            }
            if (req.Method == HttpMethod.Get && uri.Contains("store_products?"))
            {
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK)
                {
                    Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(storedStoreProducts))
                };
            }
            if (req.Method == HttpMethod.Post && uri.EndsWith("/rest/v1/products"))
            {
                productInserts++;
                var body = await req.Content!.ReadAsStringAsync(ct);
                var doc = System.Text.Json.JsonDocument.Parse(body).RootElement;
                storedProducts.Add(new
                {
                    id = doc.GetProperty("id").GetGuid(),
                    canonical_name = doc.GetProperty("canonical_name").GetString(),
                    brand = doc.GetProperty("brand").GetString(),
                    barcode = (string?)null,
                    quantity = doc.GetProperty("quantity").GetDecimal(),
                    unit = doc.GetProperty("unit").GetString(),
                    category = doc.GetProperty("category").GetString(),
                    created_at = DateTime.UtcNow
                });
                return new HttpResponseMessage(System.Net.HttpStatusCode.Created) { Content = new StringContent("[]") };
            }
            if (req.Method == HttpMethod.Post && uri.Contains("store_products?on_conflict"))
            {
                var body = await req.Content!.ReadAsStringAsync(ct);
                var doc = System.Text.Json.JsonDocument.Parse(body).RootElement;
                var spId = Guid.NewGuid();
                if (storedStoreProducts.Count == 0)
                {
                    storedStoreProducts.Add(new
                    {
                        source_product_id = doc.GetProperty("source_product_id").GetString(),
                        product_id = doc.GetProperty("product_id").GetGuid()
                    });
                }
                return new HttpResponseMessage(System.Net.HttpStatusCode.Created)
                {
                    Content = new StringContent($"[{{\"id\":\"{spId}\"}}]")
                };
            }
            if (req.Method == HttpMethod.Post && uri.EndsWith("/rest/v1/price_observations"))
            {
                observationInserts++;
                return new HttpResponseMessage(System.Net.HttpStatusCode.Created) { Content = new StringContent("[]") };
            }

            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent("[]") };
        });

        var storage = new SupabaseStorageService(
            new HttpClient(handler),
            "https://testproject.supabase.co",
            "sb_secret_test_key",
            new PriceWise.Collector.Matching.ProductMatcher(),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<SupabaseStorageService>.Instance);

        var items = new[]
        {
            new PriceWise.Collector.Models.RawProductItem
            {
                SourceSupermarketSlug = "cargills",
                SourceProductId = "BV20102",
                SourceProductName = "Milkmaid Condensed Milk",
                Price = 990m,
                ProductUrl = "https://cargillsonline.com/Web/ProductDetails?ID=1",
                Brand = "Milkmaid",
                Quantity = 510m,
                Unit = "g",
                Category = "Grocery",
                AvailabilityStatus = "unknown",
                ObservedAt = DateTime.UtcNow
            }
        };

        // Run 1: creates 1 canonical product + 1 observation
        await storage.SaveCollectedProductsAsync(supermarket, items);
        Assert.Equal(1, productInserts);
        Assert.Equal(1, observationInserts);

        // Run 2: same SKU collected again -> MUST NOT insert a new canonical product!
        await storage.SaveCollectedProductsAsync(supermarket, items);
        Assert.Equal(1, productInserts);
        Assert.Equal(2, observationInserts);
    }

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, System.Threading.CancellationToken, System.Threading.Tasks.Task<HttpResponseMessage>> _fn;
        public TestHttpMessageHandler(Func<HttpRequestMessage, System.Threading.CancellationToken, System.Threading.Tasks.Task<HttpResponseMessage>> fn) => _fn = fn;
        protected override System.Threading.Tasks.Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
            => _fn(request, cancellationToken);
    }
}
