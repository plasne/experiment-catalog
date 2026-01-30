using System.Threading;
using System.Threading.Tasks;

namespace Catalog;

/// <summary>
/// Factory for creating storage service instances based on configuration.
/// </summary>
public interface IStorageServiceFactory
{
    /// <summary>
    /// Gets or creates the appropriate storage service based on configuration.
    /// </summary>
    Task<IStorageService> GetStorageServiceAsync(CancellationToken cancellationToken = default);
}
