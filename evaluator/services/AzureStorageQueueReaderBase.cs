using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetBricks;
using Newtonsoft.Json;

namespace Evaluator;

public abstract class AzureStorageQueueReaderBase(
    IConfigFactory<IConfig> configFactory,
    IHttpClientFactory httpClientFactory,
    DefaultAzureCredential defaultAzureCredential,
    ILogger logger)
    : BackgroundService
{
    protected async Task<BlobClient> GetBlobClientAsync(string containerName, string blobName, CancellationToken cancellationToken)
    {
        var config = await configFactory.GetAsync(cancellationToken);
        var blobUrl = $"https://{config.AZURE_STORAGE_ACCOUNT_NAME}.blob.core.windows.net";
        var blobClient = string.IsNullOrEmpty(config.AZURE_STORAGE_CONNECTION_STRING)
            ? new BlobServiceClient(new Uri(blobUrl), defaultAzureCredential)
            : new BlobServiceClient(config.AZURE_STORAGE_CONNECTION_STRING);
        var containerClient = blobClient.GetBlobContainerClient(containerName);
        return containerClient.GetBlobClient(blobName);
    }

    public async Task<int> GetQueueMessageCountAsync(QueueClient queueClient)
    {
        logger.LogDebug("getting message count for queue {q}...", queueClient.Name);
        var properties = await queueClient.GetPropertiesAsync();
        var x = properties.Value.ApproximateMessagesCount;
        return x;
    }

    public async Task<Dictionary<string, int>> GetAllQueueMessageCountsAsync(List<QueueClient> queueClients)
    {
        var result = new Dictionary<string, int>();
        foreach (var queueClient in queueClients)
        {
            try
            {
                result[queueClient.Name] = await this.GetQueueMessageCountAsync(queueClient);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "could not get message count for queue {q}.", queueClient.Name);
            }
        }
        return result;
    }

    protected async Task<string> UploadBlobAsync(string containerName, string blobName, string content, CancellationToken cancellationToken)
    {
        logger.LogDebug("attempting to upload {c}/{b}...", containerName, blobName);
        var blobClient = await this.GetBlobClientAsync(containerName, blobName, cancellationToken);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);
        logger.LogInformation("successfully uploaded {c}/{b}.", containerName, blobName);
        return blobClient.Uri.ToString();
    }

    protected async Task<string> GetBlobUriAsync(string containerName, string blobName, CancellationToken cancellationToken)
    {
        var blobClient = await this.GetBlobClientAsync(containerName, blobName, cancellationToken);
        return blobClient.Uri.ToString();
    }

    protected async Task RecordMetricsAsync(
        PipelineRequest pipelineRequest,
        string? inferenceUri,
        string? evaluationUri,
        Dictionary<string, string> metrics,
        CancellationToken cancellationToken)
    {
        if (metrics.Count == 0)
        {
            return;
        }

        var config = await configFactory.GetAsync(cancellationToken);
        if (string.IsNullOrEmpty(config.EXPERIMENT_CATALOG_BASE_URL))
        {
            logger.LogWarning("there is no EXPERIMENT_CATALOG_BASE_URL provided, so no metrics will be logged.");
            return;
        }

        logger.LogDebug("attempting to record {x} metrics...", metrics.Count);
        using var httpClient = httpClientFactory.CreateClient();
        var result = new Result
        {
            Ref = pipelineRequest.Ref,
            Set = pipelineRequest.Set,
            InferenceUri = inferenceUri,
            EvaluationUri = evaluationUri,
            Metrics = metrics,
            IsBaseline = false
        };
        var resultJson = JsonConvert.SerializeObject(result);
        var response = await httpClient.PostAsync(
            $"{config.EXPERIMENT_CATALOG_BASE_URL}/api/projects/{pipelineRequest.Project}/experiments/{pipelineRequest.Experiment}/results",
            new StringContent(resultJson, Encoding.UTF8, "application/json"),
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"status code {response.StatusCode} when recording metrics: {content}");
        }
        logger.LogInformation("successfully recorded {x} metrics ({y}).",
            metrics.Count,
            string.Join(", ", metrics.Select(x => x.Key)));
    }

    protected async Task HandleResponseAsync(
        PipelineRequest pipelineRequest,
        string? responseContent,
        string? inferenceUri,
        string? evaluationUri,
        CancellationToken cancellationToken)
    {
        var metrics = new Dictionary<string, string>();

        // check if responseContent is JSON and extract metrics from $metrics nodes
        if (!string.IsNullOrEmpty(responseContent))
        {
            try
            {
                var jsonObject = JsonConvert.DeserializeObject(responseContent);
                ExtractMetricsFromJson(jsonObject, metrics);
            }
            catch (JsonException)
            {
                // responseContent is not valid JSON, skip metrics extraction
                logger.LogDebug("Response content is not valid JSON, skipping metrics extraction.");
            }
        }

        // record
        await this.RecordMetricsAsync(pipelineRequest, inferenceUri, evaluationUri, metrics, cancellationToken);
    }

    private static void ExtractMetricsFromJson(object? jsonObject, Dictionary<string, string> metrics)
    {
        if (jsonObject == null) return;

        if (jsonObject is Newtonsoft.Json.Linq.JObject jObject)
        {
            foreach (var property in jObject.Properties())
            {
                if (property.Name == "$metrics" && property.Value is Newtonsoft.Json.Linq.JObject metricsObject)
                {
                    // Found a $metrics node, extract all child properties as metrics
                    foreach (var metricProperty in metricsObject.Properties())
                    {
                        if (metricProperty.Value != null)
                        {
                            metrics[metricProperty.Name] = metricProperty.Value.ToString();
                        }
                    }
                }
                else
                {
                    // Recursively search in child objects
                    ExtractMetricsFromJson(property.Value, metrics);
                }
            }
        }
        else if (jsonObject is Newtonsoft.Json.Linq.JArray jArray)
        {
            // Recursively search in array elements
            foreach (var item in jArray)
            {
                ExtractMetricsFromJson(item, metrics);
            }
        }
    }

    protected async Task<(HttpResponseHeaders, string)> SendForProcessingAsync(
        PipelineRequest pipelineRequest,
        string? url,
        string content,
        QueueMessage queueMessage,
        string queueBody,
        Dictionary<string, string>? headers,
        CancellationToken cancellationToken)
    {
        var callId = Guid.NewGuid();
        logger.LogDebug("attempting to call '{u}' for processing with id {i}...", url, callId);

        // get the config
        var config = await configFactory.GetAsync(cancellationToken);

        // build the request
        using var httpClient = httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(config.SECONDS_BEFORE_TIMEOUT_FOR_PROCESSING);
        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-run-id", pipelineRequest.RunId.ToString());
        request.Headers.Add("x-call-id", callId.ToString());

        // add headers
        if (headers is not null)
        {
            foreach (var (key, value) in headers)
            {
                request.Headers.Add(key, value);
            }
        }

        // send the request
        var response = await httpClient.SendAsync(request, cancellationToken);

        // validate the response
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        if (config.BACKOFF_ON_STATUS_CODES.Contains((int)response.StatusCode))
        {
            var ms = response.Headers.RetryAfter?.Delta is not null
                ? (int)response.Headers.RetryAfter.Delta.Value.TotalMilliseconds
                : config.MS_TO_ADD_ON_BUSY;
            if (ms > 0)
            {
                config.MS_BETWEEN_DEQUEUE_CURRENT += ms;
                logger.LogWarning(
                    "received {code} from id {id}; delaying {ms} ms for a MS_BETWEEN_DEQUEUE of {total} ms.",
                    response.StatusCode,
                    callId,
                    ms,
                    config.MS_BETWEEN_DEQUEUE_CURRENT);
            }
        }
        if (config.DEADLETTER_ON_STATUS_CODES.Contains((int)response.StatusCode))
        {
            throw new DeadletterException($"status code {response.StatusCode} from id {callId} is considered fatal.", queueMessage, queueBody);
        }
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"status code {response.StatusCode} from id {callId} included payload: {responseBody}");
        }
        if (string.IsNullOrEmpty(responseBody))
        {
            throw new Exception($"status code {response.StatusCode} from id {callId} had an empty payload.");
        }

        // log
        logger.LogInformation("successfully called '{u}' for processing as id {i}.", url, callId);
        return (response.Headers, responseBody);
    }

    protected async Task DelayAfterDequeue(CancellationToken cancellationToken)
    {
        var config = await configFactory.GetAsync(cancellationToken);
        if (config.MS_BETWEEN_DEQUEUE_CURRENT < 1) return;
        await Task.Delay(TimeSpan.FromMilliseconds(config.MS_BETWEEN_DEQUEUE_CURRENT), cancellationToken);
    }

    protected async Task DelayAfterEmpty(CancellationToken cancellationToken)
    {
        var config = await configFactory.GetAsync(cancellationToken);
        if (config.MS_TO_PAUSE_WHEN_EMPTY < 1) return;
        await Task.Delay(TimeSpan.FromMilliseconds(config.MS_TO_PAUSE_WHEN_EMPTY), cancellationToken);
    }
}