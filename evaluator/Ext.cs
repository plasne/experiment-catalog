using System;
using System.Threading;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Iso8601DurationHelper;
using Jsonata.Net.Native;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using YamlDotNet.Serialization;

namespace Evaluator;

public static class Ext
{
    public static Duration AsDuration(this string value, Func<Duration> dflt)
    {
        if (Duration.TryParse(value, out var duration))
        {
            return duration;
        }
        return dflt();
    }

    public static async Task ConnectAsync(this QueueClient queueClient, ILogger logger, CancellationToken cancellationToken)
    {
        logger.LogDebug("attempting to connect to queue {q}...", queueClient.Name);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        var properties = await queueClient.GetPropertiesAsync(cts.Token);
        logger.LogInformation(
            "successfully authenticated to queue {q} and found ~{c} messages.",
            queueClient.Name,
            properties.Value.ApproximateMessagesCount);
    }

    public static async Task<string> DownloadAndTransformAsync(
        this BlobClient blobClient,
        string? transformQuery,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            logger.LogDebug("attempting to download and transform {c}/{b}...", blobClient.BlobContainerName, blobClient.Name);

            // download the blob
            logger.LogDebug("{c}/{b}: attempting to download...", blobClient.BlobContainerName, blobClient.Name);
            var response = await blobClient.DownloadContentAsync(cancellationToken);
            var content = response.Value.Content.ToString();
            logger.LogInformation("{c}/{b}: successfully downloaded.", blobClient.BlobContainerName, blobClient.Name);

            // convert to JSON if YAML
            if (blobClient.Name.EndsWith(".yaml", StringComparison.InvariantCultureIgnoreCase))
            {
                logger.LogDebug("{c}/{b}: attempting to convert YAML to JSON...", blobClient.BlobContainerName, blobClient.Name);
                var yamlDeserializer = new DeserializerBuilder().IgnoreUnmatchedProperties().Build();
                var obj = yamlDeserializer.Deserialize(content)
                    ?? throw new Exception($"failed to deserialize YAML.");
                content = JsonConvert.SerializeObject(obj)
                    ?? throw new Exception($"failed to serialize JSON.");
                logger.LogDebug("{c}/{b}: successfully converted YAML to JSON.", blobClient.BlobContainerName, blobClient.Name);
            }

            // transform if necessary
            if (!string.IsNullOrEmpty(transformQuery))
            {
                logger.LogDebug("{c}/{b}: attempting to transform content using a Jsonata query...", blobClient.BlobContainerName, blobClient.Name);
                var query = new JsonataQuery(transformQuery);
                content = query.Eval(content);
                logger.LogInformation("{c}/{b}: successfully transformed content using a Jsonata query.", blobClient.BlobContainerName, blobClient.Name);
            }

            // make sure there is content
            if (string.IsNullOrEmpty(content)) throw new Exception("content is empty.");

            logger.LogInformation("successfully downloaded and transformed {c}/{b}.", blobClient.BlobContainerName, blobClient.Name);
            return content;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "failed to download and transform {c}/{b}...", blobClient.BlobContainerName, blobClient.Name);
            throw;
        }
    }
}