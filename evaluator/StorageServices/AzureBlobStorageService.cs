using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;

public class AzureBlobStorageService : IStorageService
{
    private readonly SemaphoreSlim connectLock = new(1, 1);
    private BlobServiceClient? blobServiceClient;
    private BlobContainerClient? groundTruthBlobContainerClient;
    private BlobContainerClient? inferenceBlobContainerClient;
    private BlobContainerClient? evaluationBlobContainerClient;

    private async Task Connect(CancellationToken cancellationToken = default)
    {
        try
        {
            await this.connectLock.WaitAsync(cancellationToken);
            if (this.blobServiceClient is null || this.inferenceBlobContainerClient is null || this.evaluationBlobContainerClient is null)
            {
                var connectionString = NetBricks.Config.GetOnce("AZURE_STORAGE_CONNECTION_STRING");
                var groundTruthContainerName = NetBricks.Config.GetOnce("AZURE_STORAGE_GROUNDTRUTH_CONTAINER_NAME");
                var inferenceContainerName = NetBricks.Config.GetOnce("AZURE_STORAGE_INFERENCE_CONTAINER_NAME");
                var evaluationContainerName = NetBricks.Config.GetOnce("AZURE_STORAGE_EVALUATION_CONTAINER_NAME");
                this.blobServiceClient = new BlobServiceClient(connectionString);

                this.groundTruthBlobContainerClient = this.blobServiceClient.GetBlobContainerClient(groundTruthContainerName);
                await groundTruthBlobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

                this.inferenceBlobContainerClient = this.blobServiceClient.GetBlobContainerClient(inferenceContainerName);
                await inferenceBlobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

                this.evaluationBlobContainerClient = this.blobServiceClient.GetBlobContainerClient(evaluationContainerName);
                await evaluationBlobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
            }
        }
        finally
        {
            this.connectLock.Release();
        }
    }

    public async Task<List<string>> ListGroundTruthUris(CancellationToken cancellationToken = default)
    {
        await this.Connect(cancellationToken);
        var blobUrls = new List<string>();

        await foreach (var blob in this.groundTruthBlobContainerClient!.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = this.groundTruthBlobContainerClient.Name,
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

            var blobClient = this.groundTruthBlobContainerClient.GetBlobClient(blob.Name);
            var blobUrlWithSas = blobClient.Uri + "?" + sasToken;

            blobUrls.Add(blobUrlWithSas);
        }

        return blobUrls;
    }

    private string CreateBlob(BlobContainerClient client, string blobName, DateTimeOffset expiry)
    {
        var sasBuilder = new BlobSasBuilder
        {
            BlobContainerName = client.Name,
            BlobName = blobName,
            Resource = "b",
            StartsOn = DateTimeOffset.UtcNow,
            ExpiresOn = DateTimeOffset.UtcNow.AddHours(4)
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Write | BlobSasPermissions.Create);

        // TODO: replace this with DefaultAzureCredential
        var accountName = NetBricks.Config.GetOnce("AZURE_STORAGE_ACCOUNT_NAME");
        var accountKey = NetBricks.Config.GetOnce("AZURE_STORAGE_ACCOUNT_KEY");
        var cred = new StorageSharedKeyCredential(accountName, accountKey);
        var sasToken = sasBuilder.ToSasQueryParameters(cred).ToString();

        var blobClient = client.GetBlobClient(blobName);
        var blobUrlWithSas = blobClient.Uri + "?" + sasToken;

        return blobUrlWithSas;
    }

    public async Task<string> CreateInferenceBlob(string blobName, CancellationToken cancellationToken = default)
    {
        await this.Connect(cancellationToken);
        return this.CreateBlob(this.inferenceBlobContainerClient!, blobName, DateTimeOffset.UtcNow.AddHours(4));
    }

    public async Task<string> CreateEvaluationBlob(string blobName, CancellationToken cancellationToken = default)
    {
        await this.Connect(cancellationToken);
        return this.CreateBlob(this.evaluationBlobContainerClient!, blobName, DateTimeOffset.UtcNow.AddYears(1));
    }
}