using Azure.Identity;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using System.Threading;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

public class AzureBlobStorageService(
    IConfig config,
    DefaultAzureCredential defaultAzureCredential,
    AzureStorageDetails azureStorageDetails)
    : IBlobStorageService
{
    private readonly IConfig config = config;
    private readonly DefaultAzureCredential defaultAzureCredential = defaultAzureCredential;
    private readonly AzureStorageDetails azureStorageDetails = azureStorageDetails;
    private readonly SemaphoreSlim connectLock = new(1, 1);
    private BlobServiceClient? blobServiceClient;
    private BlobContainerClient? groundTruthBlobContainerClient;
    private BlobContainerClient? inferenceBlobContainerClient;
    private BlobContainerClient? evaluationBlobContainerClient;
    private string? storageAccountName;
    private string? storageAccountKey;

    private async Task Connect(CancellationToken cancellationToken = default)
    {
        try
        {
            await this.connectLock.WaitAsync(cancellationToken);
            if (this.blobServiceClient is null || this.inferenceBlobContainerClient is null || this.evaluationBlobContainerClient is null)
            {
                // get the account name and key

                // get the blob service client
                (this.storageAccountName, this.storageAccountKey) = await this.azureStorageDetails.GetNameAndKey(cancellationToken);
                string blobServiceUri = $"https://{this.storageAccountName}.blob.core.windows.net";
                this.blobServiceClient = new BlobServiceClient(new Uri(blobServiceUri), this.defaultAzureCredential);

                // get the blob container clients
                this.groundTruthBlobContainerClient = this.blobServiceClient.GetBlobContainerClient(this.config.AZURE_STORAGE_GROUNDTRUTH_CONTAINER_NAME);
                await groundTruthBlobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

                this.inferenceBlobContainerClient = this.blobServiceClient.GetBlobContainerClient(this.config.AZURE_STORAGE_INFERENCE_CONTAINER_NAME);
                await inferenceBlobContainerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

                this.evaluationBlobContainerClient = this.blobServiceClient.GetBlobContainerClient(this.config.AZURE_STORAGE_EVALUATION_CONTAINER_NAME);
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
                ExpiresOn = DateTimeOffset.UtcNow + this.config.MAX_DURATION_TO_RUN_EVALUATIONS,
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var cred = new StorageSharedKeyCredential(this.storageAccountName, this.storageAccountKey);
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
            ExpiresOn = expiry,
        };

        sasBuilder.SetPermissions(BlobSasPermissions.Read | BlobSasPermissions.Write | BlobSasPermissions.Create);

        var cred = new StorageSharedKeyCredential(this.storageAccountName, this.storageAccountKey);
        var sasToken = sasBuilder.ToSasQueryParameters(cred).ToString();

        var blobClient = client.GetBlobClient(blobName);
        var blobUrlWithSas = blobClient.Uri + "?" + sasToken;

        return blobUrlWithSas;
    }

    public async Task<string> CreateInferenceBlob(string blobName, CancellationToken cancellationToken = default)
    {
        await this.Connect(cancellationToken);
        return this.CreateBlob(this.inferenceBlobContainerClient!, blobName, DateTimeOffset.UtcNow + this.config.MAX_DURATION_TO_RUN_EVALUATIONS);
    }

    public async Task<string> CreateEvaluationBlob(string blobName, CancellationToken cancellationToken = default)
    {
        await this.Connect(cancellationToken);
        return this.CreateBlob(this.evaluationBlobContainerClient!, blobName, DateTimeOffset.UtcNow + this.config.MAX_DURATION_TO_VIEW_RESULTS);
    }
}