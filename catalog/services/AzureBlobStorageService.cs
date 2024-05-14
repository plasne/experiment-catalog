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

public class AzureBlobStorageService(
    IConfig config,
    DefaultAzureCredential defaultAzureCredential,
    ILogger<AzureBlobStorageService> logger) : IStorageService
{
    private readonly IConfig config = config;
    private readonly DefaultAzureCredential defaultAzureCredential = defaultAzureCredential;
    private readonly ILogger<AzureBlobStorageService> logger = logger;
    private readonly SemaphoreSlim connectLock = new(1, 1);
    private readonly SemaphoreSlim concurrency = new(config.CONCURRENCY, config.CONCURRENCY);

    private BlobServiceClient? blobServiceClient;

    private async Task<BlobServiceClient> ConnectAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await this.connectLock.WaitAsync(cancellationToken);

            // create the blob service client if it doesn't exist
            if (this.blobServiceClient is null)
            {
                string blobServiceUri = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.blob.core.windows.net";
                this.blobServiceClient = new BlobServiceClient(new Uri(blobServiceUri), this.defaultAzureCredential);
            }

            return this.blobServiceClient;
        }
        finally
        {
            this.connectLock.Release();
        }
    }

    private async Task<BlobContainerClient> ConnectAsync(string projectName, CancellationToken cancellationToken = default)
    {
        try
        {
            await this.connectLock.WaitAsync(cancellationToken);

            // create the blob service client if it doesn't exist
            if (this.blobServiceClient is null)
            {
                string blobServiceUri = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.blob.core.windows.net";
                this.blobServiceClient = new BlobServiceClient(new Uri(blobServiceUri), this.defaultAzureCredential);
            }

            // create the container client
            var containerClient = this.blobServiceClient.GetBlobContainerClient(projectName);
            await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);

            return containerClient;
        }
        finally
        {
            this.connectLock.Release();
        }
    }

    public async Task<IList<Project>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var client = await this.ConnectAsync(cancellationToken);
        var projects = new List<Project>();
        await foreach (var blobContainerItem in client.GetBlobContainersAsync(
            BlobContainerTraits.Metadata,
            cancellationToken: cancellationToken))
        {
            if (blobContainerItem.Properties.Metadata.TryGetValue("exp_catalog_type", out var type) && type == "project")
            {
                projects.Add(new Project { Name = blobContainerItem.Name });
            }
        }
        return projects;
    }

    public async Task AddProjectAsync(Project project, CancellationToken cancellationToken = default)
    {
        var client = await this.ConnectAsync(cancellationToken);
        var containerClient = client.GetBlobContainerClient(project.Name);
        await containerClient.CreateIfNotExistsAsync(cancellationToken: cancellationToken);
        var metadata = new Dictionary<string, string>
        {
            { "exp_catalog_type", "project" }
        };
        await containerClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
    }

    public async Task<IList<Experiment>> GetExperimentsAsync(string projectName, CancellationToken cancellationToken = default)
    {
        var containerClient = await this.ConnectAsync(projectName, cancellationToken);

        // get experiment names
        var experimentNames = new List<string>();
        await foreach (var blobItem in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
        {
            var experimentName = blobItem.Name;
            if (!experimentName.EndsWith(".jsonl")) continue;
            if (experimentName.Contains("-optimizing")) continue;
            experimentNames.Add(experimentName[..^6]);
        }

        // load the experiments
        var tasks = new List<Task<Experiment>>();
        foreach (var experimentName in experimentNames)
        {
            await this.concurrency.WaitAsync(cancellationToken);
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    return this.LoadExperimentAsync(containerClient, experimentName, includeResults: false, cancellationToken: cancellationToken);
                }
                finally
                {
                    this.concurrency.Release();
                }
            }));
        }

        return await Task.WhenAll(tasks);
    }

    public async Task AddExperimentAsync(string projectName, Experiment experiment, CancellationToken cancellationToken = default)
    {
        var containerClient = await this.ConnectAsync(projectName, cancellationToken);
        var appendBlobClient = containerClient.GetAppendBlobClient($"{experiment.Name}.jsonl");
        var response = await appendBlobClient.ExistsAsync(cancellationToken);
        if (response.Value) throw new HttpException(409, "experiment already exists.");
        await appendBlobClient.CreateAsync(new AppendBlobCreateOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/x-ndjson" }
        }, cancellationToken);
        var serializedJson = JsonConvert.SerializeObject(experiment);
        using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedJson + "\n"));
        await appendBlobClient.AppendBlockAsync(memoryStream, cancellationToken: cancellationToken);
    }

    public async Task SetExperimentAsBaselineAsync(string projectName, string experimentName, CancellationToken cancellationToken = default)
    {
        var containerClient = await this.ConnectAsync(projectName, cancellationToken);
        var metadata = new Dictionary<string, string>
        {
            { "exp_catalog_type", "project" },
            { "baseline", experimentName }
        };
        await containerClient.SetMetadataAsync(metadata, cancellationToken: cancellationToken);
    }

    public async Task AddResultAsync(string projectName, string experimentName, Result result, CancellationToken cancellationToken = default)
    {
        var containerClient = await this.ConnectAsync(projectName, cancellationToken);

        // ensure experiment is not being optimized
        var optimizing = containerClient.GetAppendBlobClient($"{experimentName}-optimizing.jsonl");
        if (optimizing.Exists(cancellationToken))
        {
            throw new HttpException(409, "experiment is currently being optimized.");
        }

        // add the result to the experiment
        var appendBlobClient = containerClient.GetAppendBlobClient($"{experimentName}.jsonl");
        var response = await appendBlobClient.ExistsAsync(cancellationToken);
        if (!response.Value) throw new HttpException(404, "experiment not found.");
        var serializedJson = JsonConvert.SerializeObject(result);
        using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedJson + "\n"));
        await appendBlobClient.AppendBlockAsync(memoryStream, cancellationToken: cancellationToken);
    }

    private async Task<Experiment> LoadExperimentAsync(
        BlobContainerClient containerClient,
        string experimentName,
        bool includeResults = true,
        CancellationToken cancellationToken = default)
    {
        var appendBlobClient = containerClient.GetAppendBlobClient($"{experimentName}.jsonl");
        HttpRange range = includeResults ? default : new HttpRange(0, 4096);
        var response = await appendBlobClient.DownloadAsync(range, cancellationToken: cancellationToken);
        using var memoryStream = new MemoryStream();
        await response.Value.Content.CopyToAsync(memoryStream, cancellationToken);
        memoryStream.Position = 0;
        using var streamReader = new StreamReader(memoryStream);

        // the first line is of type Experiment
        var experimentLine = await streamReader.ReadLineAsync(cancellationToken)
            ?? throw new Exception("no experiment info was found in the file.");
        var experiment = JsonConvert.DeserializeObject<Experiment>(experimentLine)
            ?? throw new Exception("the experiment info was corrupt.");

        // if we don't need to load the results, we're done
        if (!includeResults) return experiment;

        // all other lines are of type Result
        var results = new List<Result>();
        while (!streamReader.EndOfStream)
        {
            var resultLine = await streamReader.ReadLineAsync(cancellationToken);
            if (resultLine is null) break;
            var result = JsonConvert.DeserializeObject<Result>(resultLine);
            if (result is null) continue;
            results.Add(result);
        }
        experiment.Results = results;

        return experiment;
    }

    public async Task<Experiment> GetProjectBaselineAsync(string projectName, CancellationToken cancellationToken = default)
    {
        var containerClient = await this.ConnectAsync(projectName, cancellationToken);

        // identify the baseline experiment
        var properties = await containerClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        if (!properties.Value.Metadata.TryGetValue("baseline", out var baselineExperimentName))
        {
            throw new HttpException(404, "no baseline experiment has been identified.");
        }

        // load the baseline experiment
        var experiment = await this.LoadExperimentAsync(containerClient, baselineExperimentName, cancellationToken: cancellationToken);
        return experiment;
    }

    public async Task<Experiment> GetExperimentAsync(string projectName, string experimentName, CancellationToken cancellationToken = default)
    {
        var containerClient = await this.ConnectAsync(projectName, cancellationToken);
        var experiment = await this.LoadExperimentAsync(containerClient, experimentName, cancellationToken: cancellationToken);
        return experiment;
    }

    private async Task CopyAsync(AppendBlobClient sourceBlobClient, AppendBlobClient targetBlobClient, CancellationToken cancellationToken = default)
    {
        // log beginning of copy operation
        this.logger.LogInformation("attempting to copy {s} to {t}...", sourceBlobClient.Uri, targetBlobClient.Uri);
        var count = 0;

        // open the source blob
        using var readStream = await sourceBlobClient.OpenReadAsync(cancellationToken: cancellationToken);
        using var reader = new StreamReader(readStream);
        var content = new StringBuilder();

        // read from the source blob
        while (!reader.EndOfStream)
        {
            // read and append line
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null) break;
            if (content.Length + line.Length < 4 * 1024 * 1024)
            {
                content.Append(line + "\n");
                continue;
            }

            // write to the target blob
            using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(content.ToString()));
            await targetBlobClient.AppendBlockAsync(memoryStream, cancellationToken: cancellationToken);
            content.Clear();
            content.Append(line + "\n");
            count++;
        }

        // flush anything left over
        if (content.Length > 0)
        {
            using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(content.ToString()));
            await targetBlobClient.AppendBlockAsync(memoryStream, cancellationToken: cancellationToken);
            count++;
        }

        // log end of copy operation
        this.logger.LogInformation(
            "successfully copied {s} to {t} (now with {x} blocks).",
            sourceBlobClient.Uri,
            targetBlobClient.Uri,
            count);
    }

    public async Task OptimizeExperimentAsync(string projectName, string experimentName, CancellationToken cancellationToken = default)
    {
        try
        {
            this.logger.LogDebug("attempting to optimize project {p}, experiment {e}...", projectName, experimentName);

            // open the source blob
            var containerClient = await this.ConnectAsync(projectName, cancellationToken);
            var sourceBlobClient = containerClient.GetAppendBlobClient($"{experimentName}.jsonl");

            // create the target blob
            var targetBlobClient = containerClient.GetAppendBlobClient($"{experimentName}-optimizing.jsonl");
            await targetBlobClient.CreateAsync(cancellationToken: cancellationToken);

            // copy from source to target
            await this.CopyAsync(sourceBlobClient, targetBlobClient, cancellationToken);

            // delete the source blob and then recreate it
            await sourceBlobClient.DeleteAsync(cancellationToken: cancellationToken);
            await sourceBlobClient.CreateAsync(cancellationToken: cancellationToken);

            // copy from the target to source
#pragma warning disable S2234 // Parameters should be passed in the correct order
            await this.CopyAsync(targetBlobClient, sourceBlobClient, cancellationToken);
#pragma warning restore S2234 // intended to support copying back

            // delete the target blob
            await targetBlobClient.DeleteAsync(cancellationToken: cancellationToken);

            this.logger.LogDebug("successfully optimized project {p}, experiment {e}.", projectName, experimentName);
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "error optimizing project {p}, experiment {e}...", projectName, experimentName);
            throw;
        }
    }

    private async Task<bool> ShouldBlobBeOptimizedAsync(AppendBlobClient appendBlobClient, string experimentName, CancellationToken cancellationToken)
    {
        var properties = await appendBlobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
        var blockCount = properties.Value.BlobCommittedBlockCount;
        var blobSize = properties.Value.ContentLength;
        var minSinceLastModified = (DateTimeOffset.UtcNow - properties.Value.LastModified).TotalMinutes;
        this.logger.LogDebug(
            "experiment {e} had a block count of {x}, size of {y}, and was last modified {z} min ago.",
            experimentName,
            blockCount,
            blobSize,
            minSinceLastModified);
        if (blockCount < 2) return false;
        if (minSinceLastModified < this.config.REQUIRED_MIN_OF_IDLE_BEFORE_OPTIMIZE) return false;
        if ((blobSize / blockCount) >= this.config.REQUIRED_BLOCK_SIZE_IN_MB_FOR_OPTIMIZE * 1024 * 1024) return false;
        return true;
    }

    public async Task OptimizeAsync(CancellationToken cancellationToken = default)
    {
        this.logger.LogDebug("starting optimize operation...");

        // look at each project
        this.logger.LogDebug("attempting to get a list of projects...");
        var projects = await this.GetProjectsAsync(cancellationToken);
        this.logger.LogDebug("successfully obtained a list of {x} projects.", projects.Count);
        foreach (var project in projects)
        {
            // look at each experiment
            var containerClient = await this.ConnectAsync(project.Name!, cancellationToken);
            this.logger.LogDebug("getting a list of experiments in project {p}...", project.Name);
            await foreach (var blobItem in containerClient.GetBlobsAsync(cancellationToken: cancellationToken))
            {
                // skip things that aren't active experiments
                if (!blobItem.Name.EndsWith(".jsonl")) continue;
                if (blobItem.Name.Contains("-optimizing")) continue;
                var experimentName = blobItem.Name[..^6];

                // we want to optimize blobs that have more blocks than they should
                var appendBlobClient = containerClient.GetAppendBlobClient(blobItem.Name);
                var shouldOptimize = await this.ShouldBlobBeOptimizedAsync(appendBlobClient, experimentName, cancellationToken);
                if (!shouldOptimize) continue;

                // optimize the experiment
                try
                {
                    await this.OptimizeExperimentAsync(project.Name!, experimentName, cancellationToken);
                }
                catch (Exception)
                {
                    // still try and optimize other experiments
                }
            }
        }

        this.logger.LogDebug("completed optimize operation.");
    }
}