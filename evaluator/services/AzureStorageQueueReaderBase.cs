// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

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
using Azure.Storage.Queues.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Evaluator;

public abstract class AzureStorageQueueReaderBase(IConfig config,
    IHttpClientFactory httpClientFactory,
    ILogger logger,
    DefaultAzureCredential? defaultAzureCredential = null)
    : BackgroundService
{
    protected readonly IConfig config = config;
    protected readonly DefaultAzureCredential? defaultAzureCredential = defaultAzureCredential;
    protected readonly IHttpClientFactory httpClientFactory = httpClientFactory;
    protected readonly ILogger logger = logger;

    protected BlobClient GetBlobClient(string containerName, string blobName)
    {
        var blobUrl = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.blob.core.windows.net";
        var blobClient = string.IsNullOrEmpty(this.config.AZURE_STORAGE_CONNECTION_STRING)
            ? new BlobServiceClient(new Uri(blobUrl), this.defaultAzureCredential)
            : new BlobServiceClient(this.config.AZURE_STORAGE_CONNECTION_STRING);
        var containerClient = blobClient.GetBlobContainerClient(containerName);
        return containerClient.GetBlobClient(blobName);
    }

    protected async Task<string> UploadBlobAsync(string containerName, string blobName, string content, CancellationToken cancellationToken)
    {
        this.logger.LogDebug("attempting to upload {c}/{b}...", containerName, blobName);
        var blobClient = this.GetBlobClient(containerName, blobName);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        await blobClient.UploadAsync(stream, overwrite: true, cancellationToken);
        this.logger.LogInformation("successfully uploaded {c}/{b}.", containerName, blobName);
        return blobClient.Uri.ToString();
    }

    protected string GetBlobUri(string containerName, string blobName)
    {
        var blobClient = this.GetBlobClient(containerName, blobName);
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
        if (string.IsNullOrEmpty(this.config.EXPERIMENT_CATALOG_BASE_URL))
        {
            this.logger.LogWarning("there is no EXPERIMENT_CATALOG_BASE_URL provided, so no metrics will be logged.");
            return;
        }

        this.logger.LogDebug("attempting to record {x} metrics...", metrics.Count);
        using var httpClient = this.httpClientFactory.CreateClient();
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
            $"{this.config.EXPERIMENT_CATALOG_BASE_URL}/api/projects/{pipelineRequest.Project}/experiments/{pipelineRequest.Experiment}/results",
            new StringContent(resultJson, Encoding.UTF8, "application/json"),
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new Exception($"status code {response.StatusCode} when recording metrics: {content}");
        }
        this.logger.LogInformation("successfully recorded {x} metrics ({y}).",
            metrics.Count,
            string.Join(", ", metrics.Select(x => x.Key)));
    }

    protected void RecordHistograms(PipelineRequest pipelineRequest, List<string> connectionStrings)
    {
        if (connectionStrings.Count == 0)
        {
            return;
        }

        this.logger.LogDebug("attempting to record {x} histograms...", connectionStrings.Count);

        var recorded = new Dictionary<string, decimal>();
        var notRecorded = new List<string>();
        var meter = new Meter(DiagnosticService.SourceName);
        foreach (var connectionString in connectionStrings)
        {
            var definition = new HistogramDefinition(connectionString);
            if (definition.TryRecord(meter, pipelineRequest))
            {
                recorded.Add(definition.Name!, definition.Value ?? 0);
            }
            else
            {
                notRecorded.Add(definition.Name!);
            }
        }

        this.logger.LogInformation(
            "successfully recorded {r} histograms ({h}); not recorded ({n}).",
            recorded.Count,
            string.Join(", ", recorded.Select(x => $"{x.Key}={x.Value}")),
            string.Join(", ", notRecorded));
    }

    protected async Task HandleResponseHeadersAsync(
        PipelineRequest pipelineRequest,
        HttpResponseHeaders headers,
        string? inferenceUri,
        string? evaluationUri,
        CancellationToken cancellationToken)
    {
        var metrics = new Dictionary<string, string>();
        var connectionStrings = new List<string>();

        // look at all the headers
        foreach (var header in headers)
        {
            if (header.Value is null || !header.Value.Any() || string.IsNullOrEmpty(header.Value.First())) continue;
            var value = header.Value.First();

            if (header.Key.StartsWith("x-tag-", StringComparison.InvariantCultureIgnoreCase))
            {
                Activity.Current?.AddTag(header.Key[6..], value);
            }
            else if (header.Key.StartsWith("x-metric-", StringComparison.InvariantCultureIgnoreCase))
            {
                var key = header.Key[9..];
                metrics.Add(key, value);
                Activity.Current?.AddTag(key, value);
            }
            else if (header.Key.StartsWith("x-histogram-", StringComparison.InvariantCultureIgnoreCase))
            {
                connectionStrings.Add(header.Value.First());
            }
        }

        // record
        await this.RecordMetricsAsync(pipelineRequest, inferenceUri, evaluationUri, metrics, cancellationToken);
        this.RecordHistograms(pipelineRequest, connectionStrings);
    }

    protected async Task<(HttpResponseHeaders, string)> SendForProcessingAsync(
        PipelineRequest pipelineRequest,
        string url,
        string content,
        QueueMessage queueMessage,
        string queueBody,
        Dictionary<string, string>? headers,
        CancellationToken cancellationToken)
    {
        var callId = Guid.NewGuid();
        this.logger.LogDebug("attempting to call '{u}' for processing with id {i}...", url, callId);

        // build the request
        using var httpClient = this.httpClientFactory.CreateClient();
        httpClient.Timeout = TimeSpan.FromSeconds(this.config.SECONDS_BEFORE_TIMEOUT_FOR_PROCESSING);
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
        if (this.config.BACKOFF_ON_STATUS_CODES.Contains((int)response.StatusCode))
        {
            var ms = response.Headers.RetryAfter?.Delta is not null
                ? (int)response.Headers.RetryAfter.Delta.Value.TotalMilliseconds
                : this.config.MS_TO_ADD_ON_BUSY;
            if (ms > 0)
            {
                this.config.MS_BETWEEN_DEQUEUE_CURRENT += ms;
                this.logger.LogWarning(
                    "received {code} from id {id}; delaying {ms} ms for a MS_BETWEEN_DEQUEUE of {total} ms.",
                    response.StatusCode,
                    callId,
                    ms,
                    this.config.MS_BETWEEN_DEQUEUE_CURRENT);
            }
        }
        if (this.config.DEADLETTER_ON_STATUS_CODES.Contains((int)response.StatusCode))
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
        this.logger.LogInformation("successfully called '{u}' for processing as id {i}.", url, callId);
        return (response.Headers, responseBody);
    }

    protected async Task DelayAfterDequeue(CancellationToken cancellationToken)
    {
        if (this.config.MS_BETWEEN_DEQUEUE_CURRENT < 1) return;
        await Task.Delay(TimeSpan.FromMilliseconds(this.config.MS_BETWEEN_DEQUEUE_CURRENT), cancellationToken);
    }

    protected async Task DelayAfterEmpty(CancellationToken cancellationToken)
    {
        if (this.config.MS_TO_PAUSE_WHEN_EMPTY < 1) return;
        await Task.Delay(TimeSpan.FromMilliseconds(this.config.MS_TO_PAUSE_WHEN_EMPTY), cancellationToken);
    }
}