using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

public class AzureBlobStorageService : IStorageService
{
    private readonly SemaphoreSlim connectLock = new(1, 1);
    private BlobServiceClient? blobServiceClient;
    private BlobContainerClient? blobContainerClient;

    private async Task<BlobContainerClient> Connect()
    {
        try
        {
            await this.connectLock.WaitAsync();

            // create if it doesn't exist
            if (this.blobServiceClient is null || this.blobContainerClient is null)
            {
                var connectionString = NetBricks.Config.GetOnce("AZURE_STORAGE_CONNECTION_STRING");
                var containerName = NetBricks.Config.GetOnce("AZURE_STORAGE_CONTAINER_NAME");
                this.blobServiceClient = new BlobServiceClient(connectionString);

                this.blobContainerClient = this.blobServiceClient.GetBlobContainerClient(containerName);
                await blobContainerClient.CreateIfNotExistsAsync();
            }

            return blobContainerClient;
        }
        finally
        {
            this.connectLock.Release();
        }
    }

    public async Task<List<string>> ListGroundTruthUris()
    {
        var container = await this.Connect();
        var blobUrls = new List<string>();

        await foreach (var blob in container.GetBlobsAsync())
        {
            var blobClient = container.GetBlobClient(blob.Name);

            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = container.Name,
                BlobName = blob.Name,
                Resource = "b",
                StartsOn = DateTimeOffset.UtcNow,
                ExpiresOn = DateTimeOffset.UtcNow.AddHours(4)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            // TODO: replace this with DefaultAzureCredential
            var accountName = NetBricks.Config.GetOnce("AZURE_STORAGE_ACCOUNT_NAME");
            var accountKey = NetBricks.Config.GetOnce("AZURE_STORAGE_ACCOUNT_KEY");
            var cred = new StorageSharedKeyCredential(accountName, accountKey);
            var sasToken = sasBuilder.ToSasQueryParameters(cred).ToString();

            var blobUrlWithSas = blobClient.Uri + "?" + sasToken;

            blobUrls.Add(blobUrlWithSas);
        }

        return blobUrls;
    }
}