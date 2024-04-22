using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Blobs.Specialized;

public class AzureBlobStorageService : IStorageService
{
    private readonly SemaphoreSlim connectLock = new(1, 1);
    private BlobServiceClient? blobServiceClient;
    private JsonSerializerOptions jsonOptionsForSerialization = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters = { new MetricConverter() },
    };
    private JsonSerializerOptions jsonOptionsForDeserialization = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    private class MetricConverter : JsonConverter<Metric>
    {
        public override Metric Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            throw new NotImplementedException();
        }

        public override void Write(Utf8JsonWriter writer, Metric value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteNumber("value", value.Value);
            writer.WriteEndObject();
        }
    }

    private async Task<BlobContainerClient> Connect(string projectName)
    {
        try
        {
            await this.connectLock.WaitAsync();

            // create the blob service client if it doesn't exist
            if (this.blobServiceClient is null)
            {
                var connectionString = NetBricks.Config.GetOnce("AZURE_STORAGE_CONNECTION_STRING");
                this.blobServiceClient = new BlobServiceClient(connectionString);
            }

            // create the container client
            var containerClient = this.blobServiceClient.GetBlobContainerClient(projectName);
            await containerClient.CreateIfNotExistsAsync();

            return containerClient;
        }
        finally
        {
            this.connectLock.Release();
        }
    }

    public async Task<IEnumerable<Experiment>> GetExperiments(string projectName)
    {
        var containerClient = await this.Connect(projectName);

        // get experiment names
        var experimentNames = new List<string>();
        await foreach (var blobItem in containerClient.GetBlobsAsync())
        {
            var experimentName = blobItem.Name;
            if (!experimentName.EndsWith(".jsonl")) continue;
            experimentNames.Add(experimentName[..^6]);
        }

        // load the experiments
        var tasks = new List<Task<Experiment>>();
        var semaphore = new SemaphoreSlim(4);
        foreach (var experimentName in experimentNames)
        {
            await semaphore.WaitAsync();
            tasks.Add(Task.Run(() =>
            {
                try
                {
                    return this.LoadExperiment(containerClient, experimentName, includeResults: false);
                }
                finally
                {
                    semaphore.Release();
                }
            }));
        }

        return await Task.WhenAll(tasks);
    }

    public async Task AddExperiment(string projectName, Experiment experiment)
    {
        // TODO: validate projectName and experiment.Name
        var containerClient = await this.Connect(projectName);
        var appendBlobClient = containerClient.GetAppendBlobClient($"{experiment.Name}.jsonl");
        var response = await appendBlobClient.ExistsAsync();
        if (response.Value) throw new HttpException(409, "experiment already exists.");
        await appendBlobClient.CreateAsync(new AppendBlobCreateOptions
        {
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/x-ndjson" }
        });
        var serializedJson = JsonSerializer.Serialize(experiment, jsonOptionsForSerialization);
        using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedJson + "\n"));
        await appendBlobClient.AppendBlockAsync(memoryStream);
    }

    public async Task SetExperimentAsBaseline(string projectName, string experimentName)
    {
        var containerClient = await this.Connect(projectName);
        var metadata = new Dictionary<string, string>
        {
            { "baseline", experimentName }
        };
        await containerClient.SetMetadataAsync(metadata);
    }

    public async Task AddResult(string projectName, string experimentName, Result result)
    {
        // TODO: validate projectName and experimentName
        var containerClient = await this.Connect(projectName);
        var appendBlobClient = containerClient.GetAppendBlobClient($"{experimentName}.jsonl");
        var response = await appendBlobClient.ExistsAsync();
        if (!response.Value) throw new HttpException(404, "experiment not found.");
        var serializedJson = JsonSerializer.Serialize(result, jsonOptionsForSerialization);
        using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(serializedJson + "\n"));
        await appendBlobClient.AppendBlockAsync(memoryStream);
    }

    private async Task<Experiment> LoadExperiment(
        BlobContainerClient containerClient,
        string experimentName,
        bool includeResults = true)
    {
        var appendBlobClient = containerClient.GetAppendBlobClient($"{experimentName}.jsonl");
        var response = await appendBlobClient.DownloadAsync();
        using var memoryStream = new MemoryStream();
        await response.Value.Content.CopyToAsync(memoryStream);
        memoryStream.Position = 0;
        using var streamReader = new StreamReader(memoryStream);

        // the first line is of type Experiment
        var experimentLine = await streamReader.ReadLineAsync()
            ?? throw new Exception("no experiment info was found in the file.");
        var experiment = JsonSerializer.Deserialize<Experiment>(experimentLine, jsonOptionsForDeserialization)
            ?? throw new Exception("the experiment info was corrupt.");

        // if we don't need to load the results, we're done
        if (!includeResults) return experiment;

        // all other lines are of type Result
        var results = new List<Result>();
        while (!streamReader.EndOfStream)
        {
            var resultLine = await streamReader.ReadLineAsync();
            if (resultLine is null) break;
            var result = JsonSerializer.Deserialize<Result>(resultLine, jsonOptionsForDeserialization);
            if (result is null) continue;
            results.Add(result);
        }
        experiment.Results = results;

        return experiment;
    }

    public async Task<Experiment> GetProjectBaseline(string projectName)
    {
        var containerClient = await this.Connect(projectName);

        // identify the baseline experiment
        var properties = await containerClient.GetPropertiesAsync();
        if (!properties.Value.Metadata.TryGetValue("baseline", out var baselineExperimentName))
        {
            throw new HttpException(404, "no baseline experiment has been identified.");
        }

        // load the baseline experiment
        var experiment = await this.LoadExperiment(containerClient, baselineExperimentName);
        return experiment;
    }

    public async Task<Experiment> GetExperiment(string projectName, string experimentName)
    {
        var containerClient = await this.Connect(projectName);
        var experiment = await this.LoadExperiment(containerClient, experimentName);
        return experiment;
    }
}