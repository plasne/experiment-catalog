using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Storage;

public class AzureStorageDetails(IConfig config, DefaultAzureCredential defaultAzureCredential)
{
    private readonly IConfig config = config;
    private readonly DefaultAzureCredential defaultAzureCredential = defaultAzureCredential;
    private string? storageAccountName;
    private string? storageAccountKey;

    public async Task<(string, string)> GetNameAndKey(CancellationToken cancellationToken = default)
    {
        if (this.storageAccountName is null || this.storageAccountKey is null)
        {
            var armClient = new ArmClient(this.defaultAzureCredential);
            var storageResourceIdentifier = new ResourceIdentifier(this.config.AZURE_STORAGE_ACCOUNT_ID);
            var storageAccountResource = armClient.GetStorageAccountResource(storageResourceIdentifier);
            this.storageAccountName = storageAccountResource.Id.Name;
            await foreach (var key in storageAccountResource.GetKeysAsync(cancellationToken: cancellationToken))
            {
                this.storageAccountKey = key.Value;
                break;
            }
        }

        return (this.storageAccountName!, this.storageAccountKey!);
    }
}