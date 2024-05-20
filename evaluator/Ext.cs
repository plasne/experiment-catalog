using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Azure.Monitor.OpenTelemetry.Exporter;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Jsonata.Net.Native;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using YamlDotNet.Serialization;

namespace Evaluator;

public static class Ext
{
    public static void AddOpenTelemetry(
        this ILoggingBuilder builder,
        string openTelemetryConnectionString)
    {
        builder.AddOpenTelemetry(logging =>
        {
            logging.AddAzureMonitorLogExporter(o => o.ConnectionString = openTelemetryConnectionString);
        });
    }

    public static void AddOpenTelemetry(
        this IServiceCollection serviceCollection,
        string sourceName,
        string applicationName,
        string openTelemetryConnectionString)
    {
        serviceCollection.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName: applicationName))
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddHttpClientInstrumentation();
                metrics.AddMeter(sourceName);
                metrics.AddAzureMonitorMetricExporter(o => o.ConnectionString = openTelemetryConnectionString);
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation();
                tracing.AddHttpClientInstrumentation(o =>
                {
                    o.FilterHttpRequestMessage = (request) =>
                    {
                        return request.RequestUri is not null && !request.RequestUri.ToString().Contains(".queue.core.windows.net");
                    };
                });
                tracing.AddSource(sourceName);
                tracing.AddAzureMonitorTraceExporter(o => o.ConnectionString = openTelemetryConnectionString);
            });
    }

    public static async Task ConnectAsync(this QueueClient queueClient, ILogger logger, CancellationToken cancellationToken)
    {
        logger.LogInformation("attempting to connect to queue {q}...", queueClient.Name);
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

    public static void AddTagsFromPipelineRequest(this Activity activity, PipelineRequest request)
    {
        activity.AddTag("run_id", request.RunId.ToString());
        activity.AddTag("id", request.Id);
        activity.AddTag("project", request.Project);
        activity.AddTag("experiment", request.Experiment);
        activity.AddTag("ref", request.Ref);
        activity.AddTag("set", request.Set);
        activity.AddTag("is_baseline", request.IsBaseline.ToString());
    }
}