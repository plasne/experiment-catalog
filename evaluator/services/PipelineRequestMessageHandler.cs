using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Blobs;
using Jsonata.Net.Native;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

public class PipelineRequestMessageHandler(
    IConfig config,
    DefaultAzureCredential defaultAzureCredential,
    IHttpClientFactory httpClientFactory,
    ILogger<PipelineRequestMessageHandler> logger)
    : IMessageHandler<PipelineRequest>
{
    private readonly IConfig config = config;
    private readonly DefaultAzureCredential defaultAzureCredential = defaultAzureCredential;
    private readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    private readonly ILogger<PipelineRequestMessageHandler> logger = logger;

    private async Task<string> DownloadBlob((string container, string blob) blobRef, CancellationToken cancellationToken)
    {
        this.logger.LogDebug("attempting to download {c}/{b}...", blobRef.container, blobRef.blob);

        // create the clients
        string blobServiceUri = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.blob.core.windows.net";
        BlobServiceClient blobServiceClient = new(new Uri(blobServiceUri), this.defaultAzureCredential);
        BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient(blobRef.container);

        // download the blob
        BlobClient blobClient = blobContainerClient.GetBlobClient(blobRef.blob);
        var response = await blobClient.DownloadContentAsync(cancellationToken);
        this.logger.LogInformation("successfully downloaded {c}/{b}.", blobRef.container, blobRef.blob);
        return response.Value.Content.ToString();
    }

    private async Task UploadBlob((string container, string blob) blobRef, string content, CancellationToken cancellationToken)
    {
        this.logger.LogDebug("attempting to upload {c}/{b}...", blobRef.container, blobRef.blob);

        // create the blob service client
        string blobServiceUri = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.blob.core.windows.net";
        BlobServiceClient blobServiceClient = new(new Uri(blobServiceUri), this.defaultAzureCredential);
        BlobContainerClient blobContainerClient = blobServiceClient.GetBlobContainerClient(blobRef.container);

        // upload the blob
        BlobClient blobClient = blobContainerClient.GetBlobClient(blobRef.blob);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var response = await blobClient.UploadAsync(stream, cancellationToken);

        this.logger.LogInformation("successfully uploaded {c}/{b}.", blobRef.container, blobRef.blob);
    }

    public async Task ExecuteAsync(PipelineRequest req, CancellationToken cancellationToken)
    {
        // download blob
        var inboundBlobRef = req.GetBlobRefForStage(this.config.INBOUND_STAGE);
        var inboundContent = await this.DownloadBlob(inboundBlobRef, cancellationToken);

        // convert to JSON if YAML
        if (inboundBlobRef.blob.EndsWith(".yaml", StringComparison.InvariantCultureIgnoreCase))
        {
            this.logger.LogDebug("atttempting to convert YAML to JSON...");
            var yamlDeserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
            var obj = yamlDeserializer.Deserialize(inboundContent)
                ?? throw new Exception($"failed to deserialize YAML.");
            inboundContent = JsonConvert.SerializeObject(obj)
                ?? throw new Exception($"failed to serialize JSON.");
            this.logger.LogDebug("successfully converted YAML to JSON.");
        }

        // transform if necessary
        if (!string.IsNullOrEmpty(this.config.TRANSFORM_QUERY))
        {
            this.logger.LogDebug("attempting to transform content...");
            JsonataQuery query = new(this.config.TRANSFORM_QUERY);
            inboundContent = query.Eval(inboundContent);
            this.logger.LogDebug("successfully transformed content.");
        }

        // call processing URL
        this.logger.LogDebug("attempting to call '{u}' for processing...", this.config.PROCESSING_URL);
        using var httpClient = this.httpClientFactory.CreateClient("retry");
        var requestBody = new StringContent(inboundContent, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(this.config.PROCESSING_URL, requestBody, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"calling {this.config.PROCESSING_URL} resulted in {response.StatusCode}: {responseBody}.");
        }
        this.logger.LogInformation("successfully called '{u}' for processing.", this.config.PROCESSING_URL);

        // upload the result
        var outboundBlobRef = req.GetBlobRefForStage(this.config.OUTBOUND_STAGE);
        await this.UploadBlob(outboundBlobRef, responseBody, cancellationToken);
    }
}