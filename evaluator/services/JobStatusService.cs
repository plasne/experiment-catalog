using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;
using Microsoft.Extensions.Logging;
using NetBricks;
using Newtonsoft.Json;

namespace Evaluator;

public class JobStatusService(
    IConfigFactory<IConfig> configFactory,
    DefaultAzureCredential defaultAzureCredential,
    ILogger<JobStatusService> logger)
{
    private readonly IConfigFactory<IConfig> configFactory = configFactory;
    private readonly DefaultAzureCredential defaultAzureCredential = defaultAzureCredential;
    private readonly ILogger<JobStatusService> logger = logger;

    private async Task<bool> IsEnabledAsync(CancellationToken ct)
    {
        var config = await this.configFactory.GetAsync(ct);
        return !string.IsNullOrEmpty(config.JOB_STATUS_CONTAINER);
    }

    private async Task<AppendBlobClient> GetAppendBlobClientAsync(string runId, CancellationToken ct)
    {
        var config = await this.configFactory.GetAsync(ct);
        if (!string.IsNullOrEmpty(config.AZURE_STORAGE_CONNECTION_STRING))
        {
            var serviceClient = new BlobServiceClient(config.AZURE_STORAGE_CONNECTION_STRING);
            var containerClient = serviceClient.GetBlobContainerClient(config.JOB_STATUS_CONTAINER);
            return containerClient.GetAppendBlobClient(runId);
        }
        else
        {
            var blobUrl = $"https://{config.AZURE_STORAGE_ACCOUNT_NAME}.blob.core.windows.net";
            var serviceClient = new BlobServiceClient(new Uri(blobUrl), this.defaultAzureCredential);
            var containerClient = serviceClient.GetBlobContainerClient(config.JOB_STATUS_CONTAINER);
            return containerClient.GetAppendBlobClient(runId);
        }
    }

    public async Task CreateJobAsync(Guid runId, string project, string experiment, string set, int totalItems, CancellationToken ct)
    {
        if (!await this.IsEnabledAsync(ct))
        {
            return;
        }

        var appendBlob = await this.GetAppendBlobClientAsync(runId.ToString(), ct);
        await appendBlob.CreateIfNotExistsAsync(cancellationToken: ct);
        var metadata = new Dictionary<string, string>
        {
            ["total_items"] = totalItems.ToString(),
            ["project"] = project,
            ["experiment"] = experiment,
            ["set"] = set,
            ["started_at"] = DateTimeOffset.UtcNow.ToString("o"),
        };
        await appendBlob.SetMetadataAsync(metadata, cancellationToken: ct);
        this.logger.LogInformation("created job status blob for run {runId} with {count} total items.", runId, totalItems);
    }

    public async Task RecordOutcomeAsync(Guid runId, string id, JobStage stage, JobOutcome status, string? error, CancellationToken ct)
    {
        try
        {
            if (!await this.IsEnabledAsync(ct))
            {
                return;
            }

            var appendBlob = await this.GetAppendBlobClientAsync(runId.ToString(), ct);
            await appendBlob.CreateIfNotExistsAsync(cancellationToken: ct);

            var record = new JobStatusRecord
            {
                Id = id,
                Stage = stage,
                Status = status,
                Error = error,
                Timestamp = DateTimeOffset.UtcNow,
            };

            var line = JsonConvert.SerializeObject(record, Formatting.None) + "\n";
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(line));
            await appendBlob.AppendBlockAsync(stream, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "failed to record outcome for {id} in run {runId}; status tracking may be incomplete.", id, runId);
        }
    }

    public async Task<List<JobSummary>> ListJobsAsync(DateTimeOffset? since, CancellationToken ct)
    {
        var config = await this.configFactory.GetAsync(ct);
        var jobs = new List<JobSummary>();

        if (string.IsNullOrEmpty(config.JOB_STATUS_CONTAINER))
        {
            return jobs;
        }

        BlobContainerClient containerClient;
        if (!string.IsNullOrEmpty(config.AZURE_STORAGE_CONNECTION_STRING))
        {
            var serviceClient = new BlobServiceClient(config.AZURE_STORAGE_CONNECTION_STRING);
            containerClient = serviceClient.GetBlobContainerClient(config.JOB_STATUS_CONTAINER);
        }
        else
        {
            var blobUrl = $"https://{config.AZURE_STORAGE_ACCOUNT_NAME}.blob.core.windows.net";
            var serviceClient = new BlobServiceClient(new Uri(blobUrl), this.defaultAzureCredential);
            containerClient = serviceClient.GetBlobContainerClient(config.JOB_STATUS_CONTAINER);
        }

        await foreach (var blobItem in containerClient.GetBlobsAsync(new GetBlobsOptions { Traits = BlobTraits.Metadata }, cancellationToken: ct))
        {
            var metadata = blobItem.Metadata;
            if (metadata == null || !metadata.ContainsKey("started_at"))
            {
                continue;
            }

            var startedAt = DateTimeOffset.Parse(metadata["started_at"]);
            if (since.HasValue && startedAt < since.Value)
            {
                continue;
            }

            var summary = new JobSummary
            {
                RunId = blobItem.Name,
                Project = metadata.TryGetValue("project", out var project) ? project : string.Empty,
                Experiment = metadata.TryGetValue("experiment", out var experiment) ? experiment : string.Empty,
                Set = metadata.TryGetValue("set", out var setName) ? setName : string.Empty,
                TotalItems = metadata.ContainsKey("total_items") ? int.Parse(metadata["total_items"]) : 0,
                StartedAt = startedAt,
                CompletedAt = metadata.TryGetValue("completed_at", out var completedAt) ? DateTimeOffset.Parse(completedAt) : null,
                InferenceSucceeded = metadata.TryGetValue("inference_succeeded", out var infSucc) ? int.Parse(infSucc) : null,
                InferenceFailed = metadata.TryGetValue("inference_failed", out var infFail) ? int.Parse(infFail) : null,
                EvaluationSucceeded = metadata.TryGetValue("evaluation_succeeded", out var evalSucc) ? int.Parse(evalSucc) : null,
                EvaluationFailed = metadata.TryGetValue("evaluation_failed", out var evalFail) ? int.Parse(evalFail) : null,
            };
            jobs.Add(summary);
        }

        return jobs;
    }

    public async Task<JobStatus?> GetJobStatusAsync(string runId, CancellationToken ct)
    {
        if (!await this.IsEnabledAsync(ct))
        {
            return null;
        }

        var config = await this.configFactory.GetAsync(ct);
        var appendBlob = await this.GetAppendBlobClientAsync(runId, ct);

        // check metadata first for cached results
        var properties = await appendBlob.GetPropertiesAsync(cancellationToken: ct);
        var metadata = properties.Value.Metadata;

        if (metadata.ContainsKey("completed_at"))
        {
            return BuildStatusFromMetadata(runId, metadata);
        }

        // download and parse the blob
        var response = await appendBlob.DownloadContentAsync(cancellationToken: ct);
        var content = response.Value.Content.ToString();
        var records = this.ParseRecords(content);

        // dedup: group by (id, stage), keep latest by timestamp
        var latestByKey = records
            .GroupBy(r => (r.Id, r.Stage))
            .Select(g => g.OrderByDescending(r => r.Timestamp).First())
            .ToList();

        // tally
        var inferenceSucceeded = latestByKey.Count(r => r.Stage == JobStage.Inference && r.Status == JobOutcome.Success);
        var inferenceFailed = latestByKey.Count(r => r.Stage == JobStage.Inference && r.Status == JobOutcome.Failed);
        var evaluationSucceeded = latestByKey.Count(r => r.Stage == JobStage.Evaluation && r.Status == JobOutcome.Success);
        var evaluationFailed = latestByKey.Count(r => r.Stage == JobStage.Evaluation && r.Status == JobOutcome.Failed);

        var totalItems = metadata.ContainsKey("total_items") ? int.Parse(metadata["total_items"]) : 0;

        // determine completion: count distinct successful ids per stage;
        // failures may be retried so only successes reliably indicate completion
        var distinctInferenceSuccesses = latestByKey
            .Where(r => r.Stage == JobStage.Inference && r.Status == JobOutcome.Success)
            .Select(r => r.Id)
            .Distinct()
            .Count();
        var distinctEvaluationSuccesses = latestByKey
            .Where(r => r.Stage == JobStage.Evaluation && r.Status == JobOutcome.Success)
            .Select(r => r.Id)
            .Distinct()
            .Count();
        var isCountComplete = totalItems > 0
            && distinctInferenceSuccesses >= totalItems
            && distinctEvaluationSuccesses >= totalItems;

        var lastRecordTime = records.Count > 0 ? records.Max(r => r.Timestamp) : DateTimeOffset.MinValue;
        var isTimedOut = records.Count > 0
            && (DateTimeOffset.UtcNow - lastRecordTime).TotalMinutes >= config.JOB_DONE_TIMEOUT_MINUTES;

        if (isCountComplete || isTimedOut)
        {
            // cache in metadata
            var updatedMetadata = new Dictionary<string, string>(metadata)
            {
                ["inference_succeeded"] = inferenceSucceeded.ToString(),
                ["inference_failed"] = inferenceFailed.ToString(),
                ["evaluation_succeeded"] = evaluationSucceeded.ToString(),
                ["evaluation_failed"] = evaluationFailed.ToString(),
                ["completed_at"] = DateTimeOffset.UtcNow.ToString("o"),
            };

            try
            {
                await appendBlob.SetMetadataAsync(updatedMetadata, cancellationToken: ct);
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "failed to cache completion metadata for run {runId}.", runId);
            }
        }

        return new JobStatus
        {
            RunId = runId,
            TotalItems = totalItems,
            Stages =
            [
                new JobStageStatus { Stage = JobStage.Inference, Succeeded = inferenceSucceeded, Failed = inferenceFailed },
                new JobStageStatus { Stage = JobStage.Evaluation, Succeeded = evaluationSucceeded, Failed = evaluationFailed },
            ],
        };
    }

    private List<JobStatusRecord> ParseRecords(string content)
    {
        var records = new List<JobStatusRecord>();
        if (string.IsNullOrWhiteSpace(content))
        {
            return records;
        }

        foreach (var line in content.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var record = JsonConvert.DeserializeObject<JobStatusRecord>(line);
                if (record != null)
                {
                    records.Add(record);
                }
            }
            catch (Exception ex)
            {
                this.logger.LogWarning(ex, "skipping malformed JSONL line in job status blob.");
            }
        }

        return records;
    }

    private static JobStatus BuildStatusFromMetadata(string runId, IDictionary<string, string> metadata)
    {
        return new JobStatus
        {
            RunId = runId,
            TotalItems = metadata.ContainsKey("total_items") ? int.Parse(metadata["total_items"]) : 0,
            Stages =
            [
                new JobStageStatus
                {
                    Stage = JobStage.Inference,
                    Succeeded = metadata.ContainsKey("inference_succeeded") ? int.Parse(metadata["inference_succeeded"]) : 0,
                    Failed = metadata.ContainsKey("inference_failed") ? int.Parse(metadata["inference_failed"]) : 0,
                },
                new JobStageStatus
                {
                    Stage = JobStage.Evaluation,
                    Succeeded = metadata.ContainsKey("evaluation_succeeded") ? int.Parse(metadata["evaluation_succeeded"]) : 0,
                    Failed = metadata.ContainsKey("evaluation_failed") ? int.Parse(metadata["evaluation_failed"]) : 0,
                },
            ],
        };
    }
}
