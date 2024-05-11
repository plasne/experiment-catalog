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
                (this.storageAccountName, this.storageAccountKey) = await this.azureStorageDetails.GetNameAndKey(cancellationToken);

                // get the blob service client
                string blobServiceUri = $"https://{this.storageAccountName}.blob.core.windows.net";
                this.blobServiceClient = new BlobServiceClient(new Uri(blobServiceUri), this.defaultAzureCredential);

                // get the blob container clients
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

    public async Task<List<string>> ListGroundTruthUris(List<string> Datasources, CancellationToken cancellationToken = default)
    {
        await this.Connect(cancellationToken);
        var blobUrls = new List<string>();

        foreach (var datasourceName in Datasources)
        {
            var groundTruthBlobContainerClient = this.blobServiceClient!.GetBlobContainerClient($"{datasourceName}-groundtruth");
            var existsResponse = await groundTruthBlobContainerClient.ExistsAsync(cancellationToken);
            if (!existsResponse.Value)
            {
                continue;
            }

            await foreach (var blob in groundTruthBlobContainerClient!.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                var sasBuilder = new BlobSasBuilder
                {
                    BlobContainerName = groundTruthBlobContainerClient.Name,
                    BlobName = blob.Name,
                    Resource = "b",
                    StartsOn = DateTimeOffset.UtcNow,
                    ExpiresOn = DateTimeOffset.UtcNow + this.config.MAX_DURATION_TO_RUN_EVALUATIONS,
                };

                sasBuilder.SetPermissions(BlobSasPermissions.Read);

                var cred = new StorageSharedKeyCredential(this.storageAccountName, this.storageAccountKey);
                var sasToken = sasBuilder.ToSasQueryParameters(cred).ToString();

                var blobClient = groundTruthBlobContainerClient.GetBlobClient(blob.Name);
                var blobUrlWithSas = blobClient.Uri + "?" + sasToken;

                blobUrls.Add(blobUrlWithSas);
            }
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

    public async Task<List<Datasource>> ListDatasources(CancellationToken cancellationToken = default)
    {
        await this.Connect(cancellationToken);
        List<Datasource> containersWithGroundTruth = [];
        await foreach (var container in this.blobServiceClient!.GetBlobContainersAsync(cancellationToken: cancellationToken))
        {
            if (container.Name.EndsWith("-groundtruth"))
            {
                var prefix = container.Name[..^12];
                containersWithGroundTruth.Add(new Datasource { Name = prefix });
            }
        }
        return containersWithGroundTruth;
    }
}