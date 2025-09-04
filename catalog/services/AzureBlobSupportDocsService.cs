using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Catalog;

public class AzureBlobSupportDocsService(
    IConfig config,
    DefaultAzureCredential defaultAzureCredential,
    ILogger<AzureBlobStorageService> logger) : ISupportDocsService
{
    private readonly IConfig config = config;
    private readonly DefaultAzureCredential defaultAzureCredential = defaultAzureCredential;
    private readonly ILogger<AzureBlobStorageService> logger = logger;
    private readonly SemaphoreSlim connectLock = new(1, 1);
    private readonly SemaphoreSlim concurrency = new(config.CONCURRENCY, config.CONCURRENCY);

    private BlobServiceClient? blobServiceClient;

    private BlobServiceClient GetBlobServiceClientAsync()
    {
        // create the blob service client using connection string if it doesn't exist
        if (this.blobServiceClient is null && !string.IsNullOrEmpty(this.config.AZURE_STORAGE_ACCOUNT_CONNSTRING_FOR_SUPPORT_DOCS))
        {
            this.blobServiceClient = new BlobServiceClient(this.config.AZURE_STORAGE_ACCOUNT_CONNSTRING_FOR_SUPPORT_DOCS);
        }

        // create the blob service client using account name if it doesn't exist
        if (this.blobServiceClient is null && !string.IsNullOrEmpty(this.config.AZURE_STORAGE_ACCOUNT_NAME_FOR_SUPPORT_DOCS))
        {
            string blobServiceUri = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME_FOR_SUPPORT_DOCS}.blob.core.windows.net";
            this.blobServiceClient = new BlobServiceClient(new Uri(blobServiceUri), this.defaultAzureCredential);
        }

        // throw if no connection string or account name was provided
        if (this.blobServiceClient is null)
        {
            throw new Exception("no connection string or account name was provided.");
        }

        return this.blobServiceClient;
    }

    private async Task<BlobServiceClient> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await this.connectLock.WaitAsync(cancellationToken);
            return GetBlobServiceClientAsync();
        }
        finally
        {
            this.connectLock.Release();
        }
    }

    public async Task<byte[]> GetSupportingDocumentAsync(string url, CancellationToken cancellationToken = default)
    {
        var client = await this.ConnectAsync(cancellationToken);

        // verify the URL is for the correct storage account
        if (!url.StartsWith($"https://{client.AccountName}.blob.core.windows.net/", StringComparison.OrdinalIgnoreCase))
        {
            throw new HttpException(400, "the URL does not point to the storage account used for the supporting documents.");
        }

        // extract container name and blob name from the URI path
        var blobUri = new Uri(url);
        var pathSegments = blobUri.AbsolutePath.TrimStart('/').Split('/', 2);
        if (pathSegments.Length < 2)
        {
            throw new HttpException(400, "the URL does not contain a valid container and blob path.");
        }

        // log the attempt
        var containerName = pathSegments[0];
        var blobName = pathSegments[1];
        this.logger.LogDebug("attempting to download blob {b} from container {c}...", blobName, containerName);

        // download the blob
        var containerClient = client.GetBlobContainerClient(containerName);
        var blobClient = containerClient.GetBlobClient(blobName);
        var response = await blobClient.DownloadAsync(cancellationToken: cancellationToken);
        using var memoryStream = new MemoryStream();
        await response.Value.Content.CopyToAsync(memoryStream);
        var result = memoryStream.ToArray();
        this.logger.LogDebug("successfully downloaded blob {b} from container {c}.", blobName, containerName);
        return result;
    }
}