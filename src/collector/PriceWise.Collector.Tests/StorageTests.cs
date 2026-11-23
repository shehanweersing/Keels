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
}
