using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PriceWise.Collector.Models;

namespace PriceWise.Collector.Storage;

public interface IStorageService
{
    Task<List<Product>> GetCanonicalProductsAsync(CancellationToken cancellationToken = default);
    Task<List<Supermarket>> GetSupermarketsAsync(CancellationToken cancellationToken = default);
    Task UpdateSupermarketStatusAsync(
        Guid supermarketId,
        string integrationStatus,
        string statusReason,
        CancellationToken cancellationToken = default);
    Task SaveCollectorRunAsync(CollectorRun run, CancellationToken cancellationToken = default);
    Task SaveCollectedProductsAsync(
        Supermarket supermarket,
        IReadOnlyList<RawProductItem> items,
        CancellationToken cancellationToken = default);
}
