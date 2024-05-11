using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class PipelineRequestMessageHandler(
    IConfig config,
    DefaultAzureCredential defaultAzureCredential,
    ILogger<PipelineRequestMessageHandler> logger)
    : IMessageHandler<PipelineRequest>
{
    private readonly IConfig config = config;
    private readonly DefaultAzureCredential defaultAzureCredential = defaultAzureCredential;
    private readonly ILogger<PipelineRequestMessageHandler> logger = logger;

    private async Task<string> DownloadBlob(string uri, CancellationToken cancellationToken)
    {
        // split into the "container/blob" reference
        var parts = uri.Split("/", 2);
        if (parts.Length != 2)
        {
            throw new Exception($"when downloading blob '{uri}' expected a URI with a container and blob name separated by a /");
        }
        var containerName = parts[0];
        var blobName = parts[1];

        // create the blob service client
        string blobServiceUri = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.blob.core.windows.net";
        BlobServiceClient blobServiceClient = new(new Uri(blobServiceUri), this.defaultAzureCredential);

        // get the blob container client
        BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient(containerName);

        // download the blob
        BlobClient blobClient = blobContainerClient.GetBlobClient(blobName);
        var response = await blobClient.DownloadContentAsync(cancellationToken);
        return response.Value.Content.ToString();
    }

    public async Task ExecuteAsync(PipelineRequest req, CancellationToken cancellationToken)
    {
        // upload/download blobs
        var rawGroundTruth = await this.DownloadBlob(req.GroundTruthUri, cancellationToken);
        var groundTruth = rawGroundTruth.Deserialize<GroundTruthFile>(req.GroundTruthUri);

        this.logger.LogWarning("downloaded ground truth: {gt}", JsonConvert.SerializeObject(groundTruth));

        // convert from YAML to JSON when necessary
        // convert JSON to JSON when necessary
        // call processing on localhost
    }
}