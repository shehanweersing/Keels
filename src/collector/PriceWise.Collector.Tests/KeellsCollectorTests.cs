using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using PriceWise.Collector.Adapters;
using Xunit;

namespace PriceWise.Collector.Tests;

public class KeellsCollectorTests
{
    private const string GuestLoginOkJson = """
    {
      "statusCode": 200,
      "result": {
        "userSessionID": "test-guest-session-1234",
        "preferredOutlet": "SCDR",
        "isGuest": true
      }
    }
    """;

    private const string SampleCapturedWrappedJson = """
    {
      "statusCode": 200,
      "result": {
        "itemDetailsList": [
          {
            "itemID": 100451,
            "itemCode": "112233",
            "name": "Anchor Full Cream Milk Powder 400g",
            "amount": 1050.00,
            "imageUrl": "https://essstr.blob.core.windows.net/ui/anchor-400g.jpg",
            "stockInHand": 24,
            "isAvailable": true,
            "promotionDiscountValue": 100.00,
            "discountPercentage": 9.52
          },
          {
            "itemID": 100452,
            "itemCode": "445566",
            "name": "Munchee Lemon Puff 200g",
            "amount": 220.00,
            "imageUrl": "https://essstr.blob.core.windows.net/ui/lemon-puff.jpg",
            "stockInHand": 0,
            "isAvailable": false,
            "promotionDiscountValue": 0,
            "discountPercentage": 0
          }
        ],
        "promotionItemDetailsList": [
          {
            "itemID": 100451,
            "itemCode": "112233",
            "name": "Anchor Full Cream Milk Powder 400g",
            "amount": 1050.00,
            "imageUrl": "https://essstr.blob.core.windows.net/ui/anchor-400g.jpg",
            "stockInHand": 24,
            "isAvailable": true,
            "promotionDiscountValue": 100.00,
            "discountPercentage": 9.52
          },
          {
            "itemID": 100999,
            "itemCode": "778899",
            "name": "Maliban Gold Marie 80g",
            "amount": 140.00,
            "imageUrl": "https://essstr.blob.core.windows.net/ui/gold-marie.jpg",
            "stockInHand": null,
            "isAvailable": true,
            "promotionDiscountValue": 20.00,
            "discountPercentage": 14.29
          }
        ]
      }
    }
    """;

    [Fact]
    public void ParseResponse_ExtractsRealFields_DeduplicatesAndPreservesOriginalPrice()
    {
        var collector = CreateCollector(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));
        var observedAt = new DateTime(2026, 11, 25, 2, 30, 0, DateTimeKind.Utc);

        var parsed = collector.ParseResponse(SampleCapturedWrappedJson, "SCDR", observedAt);

        Assert.Equal(4, parsed.TotalRawCount);
        Assert.Equal(2, parsed.PrimaryListCount);
        Assert.Equal(3, parsed.ValidatedItems.Count);

        var anchor = parsed.ValidatedItems.First(x => x.SourceProductId == "SCDR:100451");
        Assert.Equal("keells", anchor.SourceSupermarketSlug);
        Assert.Equal("Anchor Full Cream Milk Powder 400g", anchor.SourceProductName);
        Assert.Equal(1050.00m, anchor.Price);
        Assert.Null(anchor.OriginalPrice);
        Assert.Null(anchor.Barcode);
        Assert.Equal(400m, anchor.Quantity);
        Assert.Equal("g", anchor.Unit);
        Assert.Equal("in_stock", anchor.AvailabilityStatus);
        Assert.Contains("outletCode=SCDR", anchor.ProductUrl);
        Assert.Contains("itemCode=112233", anchor.ProductUrl);

        var lemonPuff = parsed.ValidatedItems.First(x => x.SourceProductId == "SCDR:100452");
        Assert.Equal("out_of_stock", lemonPuff.AvailabilityStatus);

        var marie = parsed.ValidatedItems.First(x => x.SourceProductId == "SCDR:100999");
        Assert.Equal("in_stock", marie.AvailabilityStatus);
    }

    [Fact]
    public void ParseResponse_HandlesMissingPricesAndInvalidItemsSafely()
    {
        const string json = """
        {
          "statusCode": 200,
          "result": {
            "itemDetailsList": [
              { "itemID": 1, "itemCode": "A1", "name": "Valid Tea 100g", "amount": 350.00, "isAvailable": true, "stockInHand": 5 },
              { "itemID": 2, "itemCode": "A2", "name": "Missing Price Tea", "amount": null, "isAvailable": true },
              { "itemID": 3, "itemCode": "A3", "name": "Zero Price Tea", "amount": 0, "isAvailable": true },
              { "itemID": null, "itemCode": null, "name": "No ID Item", "amount": 100.00 }
            ],
            "promotionItemDetailsList": null
          }
        }
        """;

        var collector = CreateCollector(new StubHttpMessageHandler((_, _) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK))));

        var parsed = collector.ParseResponse(json, "SCDR", DateTime.UtcNow);

        Assert.Single(parsed.ValidatedItems);
        Assert.Equal("SCDR:1", parsed.ValidatedItems[0].SourceProductId);
        Assert.Equal(350.00m, parsed.ValidatedItems[0].Price);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task CollectProductsAsync_StopsImmediatelyOnGuestLoginBlock(HttpStatusCode blockCode)
    {
        int requestCount = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            requestCount++;
            return Task.FromResult(new HttpResponseMessage(blockCode));
        });

        var collector = CreateCollector(handler, new KeellsCollectorOptions { MaxRetries = 2, RequestDelayMs = 0 });
        var result = await collector.CollectProductsAsync(new[] { "milk" });

        Assert.False(result.Success);
        Assert.Equal("blocked", result.Status);
        Assert.Empty(result.Products);
        Assert.Equal(1, requestCount);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task CollectProductsAsync_StopsImmediatelyOnGetItemDetailsAuthBlock_WithoutRetries(HttpStatusCode blockCode)
    {
        int getItemCalls = 0;
        var handler = new StubHttpMessageHandler((req, _) =>
        {
            if (req.RequestUri!.ToString().Contains("GuestLogin"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(GuestLoginOkJson, Encoding.UTF8, "application/json")
                });
            }

            getItemCalls++;
            return Task.FromResult(new HttpResponseMessage(blockCode));
        });

        var collector = CreateCollector(handler, new KeellsCollectorOptions { MaxRetries = 2, RequestDelayMs = 0 });
        var result = await collector.CollectProductsAsync(new[] { "milk" });

        Assert.False(result.Success);
        Assert.Equal("blocked", result.Status);
        Assert.Empty(result.Products);
        Assert.Equal(1, getItemCalls);
    }

    [Fact]
    public async Task CollectProductsAsync_InitializesGuestSession_AndStopsAtPartialTailPage()
    {
        int getItemCalls = 0;
        string? observedHeaderSession = null;

        var handler = new StubHttpMessageHandler((req, _) =>
        {
            var uri = req.RequestUri!.ToString();
            if (uri.Contains("GuestLogin"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(GuestLoginOkJson, Encoding.UTF8, "application/json")
                });
            }

            getItemCalls++;
            observedHeaderSession = req.Headers.TryGetValues("usersessionid", out var vals) ? vals.FirstOrDefault() : null;

            if (uri.Contains("fromCount=0"))
            {
                var page0 = """
                {
                  "statusCode": 200,
                  "result": {
                    "itemDetailsList": [
                      { "itemID": 10, "itemCode": "C10", "name": "Item Ten 500g", "amount": 500, "isAvailable": true, "stockInHand": 10 },
                      { "itemID": 11, "itemCode": "C11", "name": "Item Eleven 1kg", "amount": 900, "isAvailable": true, "stockInHand": 3 }
                    ]
                  }
                }
                """;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(page0, Encoding.UTF8, "application/json")
                });
            }

            var page1 = """
            {
              "statusCode": 200,
              "result": {
                "itemDetailsList": [
                  { "itemID": 12, "itemCode": "C12", "name": "Item Twelve 250ml", "amount": 250, "isAvailable": true, "stockInHand": 1 }
                ],
                "promotionItemDetailsList": [
                  { "itemID": 10, "itemCode": "C10", "name": "Item Ten 500g", "amount": 500, "isAvailable": true, "stockInHand": 10 }
                ]
              }
            }
            """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(page1, Encoding.UTF8, "application/json")
            });
        });

        var collector = CreateCollector(handler, new KeellsCollectorOptions
        {
            PageSize = 2,
            MaxPagesPerTerm = 10,
            RequestDelayMs = 0
        });

        var result = await collector.CollectProductsAsync(new[] { "milk" });

        Assert.True(result.Success);
        Assert.Equal("success", result.Status);
        Assert.Equal("test-guest-session-1234", observedHeaderSession);
        Assert.Equal(2, getItemCalls);
        Assert.Equal(3, result.Products.Count);
    }

    [Fact]
    public async Task CollectProductsAsync_ReturnsFailedOnHttp500AfterBoundedRetries()
    {
        int getItemCalls = 0;
        var handler = new StubHttpMessageHandler((req, _) =>
        {
            if (req.RequestUri!.ToString().Contains("GuestLogin"))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(GuestLoginOkJson, Encoding.UTF8, "application/json")
                });
            }

            getItemCalls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
        });

        var collector = CreateCollector(handler, new KeellsCollectorOptions { MaxRetries = 1, RequestDelayMs = 0 });
        var result = await collector.CollectProductsAsync(new[] { "milk" });

        Assert.False(result.Success);
        Assert.Equal("failed", result.Status);
        Assert.Equal(2, getItemCalls);
        Assert.Empty(result.Products);
    }

    [Fact]
    public async Task CollectProductsAsync_RetriesGuestLoginTimeout_AndSucceeds()
    {
        int loginCalls = 0;
        var handler = new StubHttpMessageHandler((req, _) =>
        {
            if (req.RequestUri!.ToString().Contains("GuestLogin"))
            {
                loginCalls++;
                if (loginCalls == 1)
                {
                    throw new TaskCanceledException("Simulated HttpClient 35s timeout");
                }

                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(GuestLoginOkJson, Encoding.UTF8, "application/json")
                });
            }

            var page = """
            {
              "statusCode": 200,
              "result": {
                "itemDetailsList": [
                  { "itemID": 55, "itemCode": "C55", "name": "Retry Milk 500ml", "amount": 420, "isAvailable": true, "stockInHand": 5 }
                ]
              }
            }
            """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(page, Encoding.UTF8, "application/json")
            });
        });

        var collector = CreateCollector(handler, new KeellsCollectorOptions { MaxRetries = 1, PageSize = 20, RequestDelayMs = 0 });
        var result = await collector.CollectProductsAsync(new[] { "milk" });

        Assert.True(result.Success);
        Assert.Equal("success", result.Status);
        Assert.Equal(2, loginCalls);
        Assert.Single(result.Products);
    }

    private static KeellsCollector CreateCollector(HttpMessageHandler handler, KeellsCollectorOptions? options = null)
    {
        var client = new HttpClient(handler);
        return new KeellsCollector(client, NullLogger<KeellsCollector>.Instance, options);
    }

    private class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => _handler(request, cancellationToken);
    }
}
