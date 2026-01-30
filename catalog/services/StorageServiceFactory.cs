using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using NetBricks;

namespace Catalog;

/// <summary>
/// Factory that creates storage service instances based on configuration.
/// </summary>
public class StorageServiceFactory(
    IConfigFactory<IConfig> configFactory,
    DefaultAzureCredential defaultAzureCredential,
    ConcurrencyService concurrencyService,
    ILoggerFactory loggerFactory) : IStorageServiceFactory
{
    private readonly SemaphoreSlim initLock = new(1, 1);
    private IStorageService? storageService;

    /// <inheritdoc/>
    public async Task<IStorageService> GetStorageServiceAsync(CancellationToken cancellationToken = default)
    {
        if (this.storageService is not null)
        {
            return this.storageService;
        }

        await this.initLock.WaitAsync(cancellationToken);
        try
        {
            if (this.storageService is not null)
            {
                return this.storageService;
            }

            var config = await configFactory.GetAsync(cancellationToken);

            if (config.IsCosmosEnabled)
            {
                this.storageService = new AzureCosmosStorageService(
                    configFactory,
                    defaultAzureCredential,
                    concurrencyService,
                    loggerFactory.CreateLogger<AzureCosmosStorageService>());
            }
            else
            {
                this.storageService = new AzureBlobStorageService(
                    configFactory,
                    defaultAzureCredential,
                    concurrencyService,
                    loggerFactory.CreateLogger<AzureBlobStorageService>());
            }

            return this.storageService;
        }
        finally
        {
            this.initLock.Release();
        }
    }
}
