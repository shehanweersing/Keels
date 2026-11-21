using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PriceWise.Collector.Models;

namespace PriceWise.Collector.Adapters;

public interface ISupermarketCollector
{
    string SupermarketSlug { get; }
    string SupermarketName { get; }
    string WebsiteUrl { get; }
    string IntegrationStatus { get; }
    string? StatusReason { get; }

    Task<CollectorResult> CollectProductsAsync(IEnumerable<string> searchTerms, CancellationToken cancellationToken = default);
}

public record CollectorResult
{
    public required string SupermarketSlug { get; init; }
    public required bool Success { get; init; }
    public required string Status { get; init; }
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<RawProductItem> Products { get; init; } = Array.Empty<RawProductItem>();
    public int ProductsChecked { get; init; }
    public int ProductsUpdated { get; init; }
}
