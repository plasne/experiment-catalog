using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Logging;
using NetBricks;
using Newtonsoft.Json;

namespace Catalog;

/// <summary>
/// Azure Cosmos DB implementation of the storage service.
/// Uses three containers: projects, experiments, results.
/// </summary>
public class AzureCosmosStorageService(
    IConfigFactory<IConfig> configFactory,
    DefaultAzureCredential defaultAzureCredential,
    ConcurrencyService concurrencyService,
    ILogger<AzureCosmosStorageService> logger) : IStorageService
{
    private CosmosClient? cosmosClient;
    private Database? database;

    private async Task<Database> GetDatabaseAsync(CancellationToken cancellationToken = default)
    {
        if (this.database is not null)
        {
            return this.database;
        }

        var connectLock = await concurrencyService.GetConnectLock(cancellationToken);
        try
        {
            await connectLock.WaitAsync(cancellationToken);

            if (this.database is not null)
            {
                return this.database;
            }

            var config = await configFactory.GetAsync(cancellationToken);

            // Try connection string first
            if (this.cosmosClient is null && !string.IsNullOrEmpty(config.COSMOS_DB_CONNECTION_STRING))
            {
                logger.LogInformation("Connecting to Cosmos DB using connection string.");
                this.cosmosClient = new CosmosClient(
                    config.COSMOS_DB_CONNECTION_STRING,
                    new CosmosClientOptions
                    {
                        SerializerOptions = new CosmosSerializationOptions
                        {
                            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                        }
                    });
            }

            // Fall back to managed identity
            if (this.cosmosClient is null && !string.IsNullOrEmpty(config.COSMOS_DB_ACCOUNT_ENDPOINT))
            {
                logger.LogInformation("Connecting to Cosmos DB using managed identity at {Endpoint}.", config.COSMOS_DB_ACCOUNT_ENDPOINT);
                this.cosmosClient = new CosmosClient(
                    config.COSMOS_DB_ACCOUNT_ENDPOINT,
                    defaultAzureCredential,
                    new CosmosClientOptions
                    {
                        SerializerOptions = new CosmosSerializationOptions
                        {
                            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
                        }
                    });
            }

            if (this.cosmosClient is null)
            {
                throw new InvalidOperationException("No Cosmos DB connection string or account endpoint was provided.");
            }

            this.database = this.cosmosClient.GetDatabase(config.COSMOS_DB_DATABASE_NAME);
            return this.database;
        }
        finally
        {
            connectLock.Release();
        }
    }

    private async Task<Container> GetContainerAsync(string containerName, CancellationToken cancellationToken = default)
    {
        var database = await GetDatabaseAsync(cancellationToken);
        return database.GetContainer(containerName);
    }

    /// <inheritdoc/>
    public bool TryValidProjectName(string? projectName, out string? errorMessage)
    {
        if (string.IsNullOrEmpty(projectName))
        {
            errorMessage = "project name cannot be null or empty.";
            return false;
        }

        if (projectName.Length < 1 || projectName.Length > 255)
        {
            errorMessage = "project name must be between 1 and 255 characters.";
            return false;
        }

        // Cosmos document IDs cannot contain /, \, ?, #
        char[] invalidChars = ['/', '\\', '?', '#'];
        if (projectName.IndexOfAny(invalidChars) >= 0)
        {
            errorMessage = "project name cannot contain /, \\, ?, or #.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <inheritdoc/>
    public bool TryValidExperimentName(string? experimentName, out string? errorMessage)
    {
        if (string.IsNullOrEmpty(experimentName))
        {
            errorMessage = "experiment name cannot be null or empty.";
            return false;
        }

        if (experimentName.Length > 255)
        {
            errorMessage = "experiment name must be 255 characters or fewer.";
            return false;
        }

        // Cosmos document IDs cannot contain /, \, ?, #
        char[] invalidChars = ['/', '\\', '?', '#'];
        if (experimentName.IndexOfAny(invalidChars) >= 0)
        {
            errorMessage = "experiment name cannot contain /, \\, ?, or #.";
            return false;
        }

        errorMessage = null;
        return true;
    }

    /// <inheritdoc/>
    public async Task<IList<Project>> GetProjectsAsync(CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync("projects", cancellationToken);
        var query = new QueryDefinition("SELECT * FROM c WHERE c.type = 'project'");

        var projects = new List<Project>();
        using var feed = container.GetItemQueryIterator<CosmosProjectDocument>(query);
        while (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync(cancellationToken);
            projects.AddRange(response.Select(d => new Project { Name = d.Name! }));
        }

        return projects;
    }

    /// <inheritdoc/>
    public async Task AddProjectAsync(Project project, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync("projects", cancellationToken);
        var document = new CosmosProjectDocument
        {
            Id = project.Name,
            Name = project.Name,
            Type = "project"
        };

        try
        {
            await container.CreateItemAsync(document, new PartitionKey(project.Name), cancellationToken: cancellationToken);
            logger.LogInformation("Created project {ProjectName}.", project.Name);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            logger.LogWarning("Project {ProjectName} already exists.", project.Name);
            throw new InvalidOperationException($"Project '{project.Name}' already exists.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<IList<Experiment>> GetExperimentsAsync(string projectName, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync("experiments", cancellationToken);
        var query = new QueryDefinition("SELECT * FROM c WHERE c.project_name = @projectName")
            .WithParameter("@projectName", projectName);

        var experiments = new List<Experiment>();
        using var feed = container.GetItemQueryIterator<CosmosExperimentDocument>(query);
        while (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync(cancellationToken);
            experiments.AddRange(response.Select(MapToExperiment));
        }

        return experiments;
    }

    /// <inheritdoc/>
    public async Task AddExperimentAsync(string projectName, Experiment experiment, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync("experiments", cancellationToken);
        var document = new CosmosExperimentDocument
        {
            Id = $"{projectName}|{experiment.Name}",
            ProjectName = projectName,
            Name = experiment.Name,
            Hypothesis = experiment.Hypothesis,
            Baseline = experiment.Baseline,
            BaselineSet = experiment.BaselineSet,
            Created = DateTimeOffset.UtcNow,
            Annotations = experiment.Annotations
        };

        try
        {
            await container.CreateItemAsync(document, new PartitionKey(projectName), cancellationToken: cancellationToken);
            logger.LogInformation("Created experiment {ExperimentName} in project {ProjectName}.", experiment.Name, projectName);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            logger.LogWarning("Experiment {ExperimentName} already exists in project {ProjectName}.", experiment.Name, projectName);
            throw new InvalidOperationException($"Experiment '{experiment.Name}' already exists in project '{projectName}'.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<Experiment> GetExperimentAsync(
        string projectName,
        string experimentName,
        bool includeResults = true,
        CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync("experiments", cancellationToken);
        var documentId = $"{projectName}|{experimentName}";

        try
        {
            var response = await container.ReadItemAsync<CosmosExperimentDocument>(
                documentId,
                new PartitionKey(projectName),
                cancellationToken: cancellationToken);

            var experiment = MapToExperiment(response.Resource);

            if (includeResults)
            {
                var (results, statistics) = await LoadResultsAndStatisticsAsync(projectName, experimentName, cancellationToken);
                experiment.Results = results;
                experiment.Statistics = statistics;
            }

            return experiment;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"Experiment '{experimentName}' not found in project '{projectName}'.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task<Experiment> GetProjectBaselineAsync(string projectName, CancellationToken cancellationToken = default)
    {
        // Get the project to find baseline experiment name
        var projectsContainer = await GetContainerAsync("projects", cancellationToken);
        try
        {
            var projectResponse = await projectsContainer.ReadItemAsync<CosmosProjectDocument>(
                projectName,
                new PartitionKey(projectName),
                cancellationToken: cancellationToken);

            var baselineExperimentName = projectResponse.Resource.BaselineExperiment;
            if (string.IsNullOrEmpty(baselineExperimentName))
            {
                throw new InvalidOperationException($"No baseline experiment set for project '{projectName}'.");
            }

            return await GetExperimentAsync(projectName, baselineExperimentName, true, cancellationToken);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"Project '{projectName}' not found.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task SetExperimentAsBaselineAsync(string projectName, string experimentName, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync("projects", cancellationToken);

        try
        {
            var response = await container.ReadItemAsync<CosmosProjectDocument>(
                projectName,
                new PartitionKey(projectName),
                cancellationToken: cancellationToken);

            var document = response.Resource;
            document.BaselineExperiment = experimentName;

            await container.ReplaceItemAsync(document, projectName, new PartitionKey(projectName), cancellationToken: cancellationToken);
            logger.LogInformation("Set experiment {ExperimentName} as baseline for project {ProjectName}.", experimentName, projectName);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"Project '{projectName}' not found.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task SetBaselineForExperiment(string projectName, string experimentName, string setName, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync("experiments", cancellationToken);
        var documentId = $"{projectName}|{experimentName}";

        try
        {
            var response = await container.ReadItemAsync<CosmosExperimentDocument>(
                documentId,
                new PartitionKey(projectName),
                cancellationToken: cancellationToken);

            var document = response.Resource;
            document.BaselineSet = setName;
            document.Modified = DateTimeOffset.UtcNow;

            await container.ReplaceItemAsync(document, documentId, new PartitionKey(projectName), cancellationToken: cancellationToken);
            logger.LogInformation("Set baseline set {SetName} for experiment {ExperimentName}.", setName, experimentName);
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"Experiment '{experimentName}' not found in project '{projectName}'.", ex);
        }
    }

    /// <inheritdoc/>
    public async Task AddResultAsync(string projectName, string experimentName, Result result, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync("results", cancellationToken);
        var experimentId = $"{projectName}|{experimentName}";

        var document = new CosmosResultDocument
        {
            Id = Guid.NewGuid().ToString(),
            ExperimentId = experimentId,
            Type = "result",
            Ref = result.Ref,
            Set = result.Set,
            InferenceUri = result.InferenceUri,
            EvaluationUri = result.EvaluationUri,
            Metrics = result.Metrics,
            Annotations = result.Annotations,
            PolicyResults = result.PolicyResults,
            Created = result.Created == default ? DateTime.UtcNow : result.Created,
            Runtime = result.Runtime
        };

        await container.CreateItemAsync(document, new PartitionKey(experimentId), cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task AddStatisticsAsync(string projectName, string experimentName, Statistics statistics, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync("results", cancellationToken);
        var experimentId = $"{projectName}|{experimentName}";

        var document = new CosmosStatisticsDocument
        {
            Id = Guid.NewGuid().ToString(),
            ExperimentId = experimentId,
            Type = "statistics",
            Set = statistics.Set,
            BaselineExperiment = statistics.BaselineExperiment,
            BaselineSet = statistics.BaselineSet,
            BaselineResultCount = statistics.BaselineResultCount,
            SetResultCount = statistics.SetResultCount,
            NumSamples = statistics.NumSamples,
            ConfidenceLevel = statistics.ConfidenceLevel,
            Metrics = statistics.Metrics,
            Created = statistics.Created == default ? DateTime.UtcNow : statistics.Created
        };

        await container.CreateItemAsync(document, new PartitionKey(experimentId), cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<IList<string>> ListTagsAsync(string projectName, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync("projects", cancellationToken);
        var query = new QueryDefinition("SELECT * FROM c WHERE c.project_name = @projectName AND c.type = 'tag'")
            .WithParameter("@projectName", projectName);

        var tags = new List<string>();
        using var feed = container.GetItemQueryIterator<CosmosTagDocument>(query);
        while (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync(cancellationToken);
            tags.AddRange(response.Where(t => t.Name is not null).Select(t => t.Name!));
        }

        return tags;
    }

    /// <inheritdoc/>
    public async Task AddTagAsync(string projectName, Tag tag, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync("projects", cancellationToken);
        var document = new CosmosTagDocument
        {
            Id = $"{projectName}|tag|{tag.Name}",
            ProjectName = projectName,
            Type = "tag",
            Name = tag.Name,
            Refs = tag.Refs
        };

        await container.UpsertItemAsync(document, new PartitionKey(projectName), cancellationToken: cancellationToken);
        logger.LogInformation("Added/updated tag {TagName} for project {ProjectName}.", tag.Name, projectName);
    }

    /// <inheritdoc/>
    public async Task<IList<Tag>> GetTagsAsync(string projectName, IEnumerable<string> tags, CancellationToken cancellationToken = default)
    {
        var tagsList = tags.ToList();
        if (tagsList.Count == 0)
        {
            return [];
        }

        var container = await GetContainerAsync("projects", cancellationToken);
        var result = new List<Tag>();

        // Build query with IN clause
        var tagParams = string.Join(", ", tagsList.Select((_, i) => $"@tag{i}"));
        var queryText = $"SELECT * FROM c WHERE c.project_name = @projectName AND c.type = 'tag' AND c.name IN ({tagParams})";
        var queryDef = new QueryDefinition(queryText)
            .WithParameter("@projectName", projectName);

        for (int i = 0; i < tagsList.Count; i++)
        {
            queryDef = queryDef.WithParameter($"@tag{i}", tagsList[i]);
        }

        using var feed = container.GetItemQueryIterator<CosmosTagDocument>(queryDef);
        while (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync(cancellationToken);
            result.AddRange(response.Select(d => new Tag { Name = d.Name!, Refs = d.Refs }));
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task AddMetricsAsync(string projectName, IList<MetricDefinition> metrics, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync("projects", cancellationToken);

        foreach (var metric in metrics)
        {
            var document = new CosmosMetricDefinitionDocument
            {
                Id = $"{projectName}|metric|{metric.Name}",
                ProjectName = projectName,
                Type = "metric",
                Name = metric.Name,
                Min = metric.Min,
                Max = metric.Max,
                AggregateFunction = metric.AggregateFunction,
                Order = metric.Order,
                Tags = metric.Tags
            };

            await container.UpsertItemAsync(document, new PartitionKey(projectName), cancellationToken: cancellationToken);
        }

        logger.LogInformation("Added/updated {Count} metrics for project {ProjectName}.", metrics.Count, projectName);
    }

    /// <inheritdoc/>
    public async Task<IList<MetricDefinition>> GetMetricsAsync(string projectName, CancellationToken cancellationToken = default)
    {
        var container = await GetContainerAsync("projects", cancellationToken);
        var query = new QueryDefinition("SELECT * FROM c WHERE c.project_name = @projectName AND c.type = 'metric'")
            .WithParameter("@projectName", projectName);

        var metrics = new List<MetricDefinition>();
        using var feed = container.GetItemQueryIterator<CosmosMetricDefinitionDocument>(query);
        while (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync(cancellationToken);
            metrics.AddRange(response.Select(d => new MetricDefinition
            {
                Name = d.Name!,
                Min = d.Min,
                Max = d.Max,
                AggregateFunction = d.AggregateFunction,
                Order = d.Order,
                Tags = d.Tags
            }));
        }

        return metrics;
    }

    /// <inheritdoc/>
    public async Task<Experiment> GetProjectBaselineWithBaselineSetAsync(
        string projectName,
        CancellationToken cancellationToken = default)
    {
        // Get project to find baseline experiment
        var projectsContainer = await GetContainerAsync("projects", cancellationToken);
        CosmosProjectDocument projectDoc;
        try
        {
            var projectResponse = await projectsContainer.ReadItemAsync<CosmosProjectDocument>(
                projectName,
                new PartitionKey(projectName),
                cancellationToken: cancellationToken);
            projectDoc = projectResponse.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new InvalidOperationException($"Project '{projectName}' not found.", ex);
        }

        if (string.IsNullOrEmpty(projectDoc.BaselineExperiment))
        {
            throw new InvalidOperationException($"No baseline experiment set for project '{projectName}'.");
        }

        // Get experiment metadata (without results)
        var experiment = await GetExperimentAsync(projectName, projectDoc.BaselineExperiment, false, cancellationToken);
        var baselineSet = experiment.BaselineSet ?? experiment.LastSet;

        // Load the baseline set's results and all statistics in a single query
        var resultsContainer = await GetContainerAsync("results", cancellationToken);
        var experimentId = $"{projectName}|{projectDoc.BaselineExperiment}";

        // Query for both results (filtered by set) and statistics
        var query = new QueryDefinition(
            "SELECT * FROM c WHERE c.experiment_id = @experimentId AND ((c.type = 'result' AND c.set = @set) OR c.type = 'statistics')")
            .WithParameter("@experimentId", experimentId)
            .WithParameter("@set", baselineSet ?? string.Empty);

        var results = new List<Result>();
        var statistics = new List<Statistics>();
        using var feed = resultsContainer.GetItemQueryIterator<dynamic>(query);
        while (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync(cancellationToken);
            foreach (var item in response)
            {
                string type = item.type;
                if (type == "result")
                {
                    var doc = JsonConvert.DeserializeObject<CosmosResultDocument>(item.ToString());
                    if (doc is not null) results.Add(MapToResult(doc));
                }
                else if (type == "statistics")
                {
                    var doc = JsonConvert.DeserializeObject<CosmosStatisticsDocument>(item.ToString());
                    if (doc is not null) statistics.Add(MapToStatistics(doc));
                }
            }
        }

        experiment.Results = results;
        experiment.Statistics = statistics;

        return experiment;
    }

    /// <inheritdoc/>
    public async Task<Experiment> GetExperimentWithSetsAsync(
        string projectName,
        string experimentName,
        IEnumerable<string> sets,
        CancellationToken cancellationToken = default)
    {
        // Get experiment metadata (without results)
        var experiment = await GetExperimentAsync(projectName, experimentName, false, cancellationToken);

        // Load only the specified sets' results and all statistics
        var setsList = sets.ToList();
        var resultsContainer = await GetContainerAsync("results", cancellationToken);
        var experimentId = $"{projectName}|{experimentName}";

        // Build query for results (filtered by set) OR statistics
        string queryText;
        QueryDefinition queryDef;

        if (setsList.Count > 0)
        {
            var setParams = string.Join(", ", setsList.Select((_, i) => $"@set{i}"));
            queryText = $"SELECT * FROM c WHERE c.experiment_id = @experimentId AND ((c.type = 'result' AND c.set IN ({setParams})) OR c.type = 'statistics')";
            queryDef = new QueryDefinition(queryText)
                .WithParameter("@experimentId", experimentId);

            for (int i = 0; i < setsList.Count; i++)
            {
                queryDef = queryDef.WithParameter($"@set{i}", setsList[i]);
            }
        }
        else
        {
            // Just load statistics if no sets specified
            queryText = "SELECT * FROM c WHERE c.experiment_id = @experimentId AND c.type = 'statistics'";
            queryDef = new QueryDefinition(queryText)
                .WithParameter("@experimentId", experimentId);
        }

        var results = new List<Result>();
        var statistics = new List<Statistics>();
        using var feed = resultsContainer.GetItemQueryIterator<dynamic>(queryDef);
        while (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync(cancellationToken);
            foreach (var item in response)
            {
                string type = item.type;
                if (type == "result")
                {
                    var doc = JsonConvert.DeserializeObject<CosmosResultDocument>(item.ToString());
                    if (doc is not null) results.Add(MapToResult(doc));
                }
                else if (type == "statistics")
                {
                    var doc = JsonConvert.DeserializeObject<CosmosStatisticsDocument>(item.ToString());
                    if (doc is not null) statistics.Add(MapToStatistics(doc));
                }
            }
        }

        experiment.Results = results;
        experiment.Statistics = statistics;

        return experiment;
    }

    private async Task<(List<Result> Results, List<Statistics> Statistics)> LoadResultsAndStatisticsAsync(
        string projectName,
        string experimentName,
        CancellationToken cancellationToken)
    {
        var container = await GetContainerAsync("results", cancellationToken);
        var experimentId = $"{projectName}|{experimentName}";
        var query = new QueryDefinition("SELECT * FROM c WHERE c.experiment_id = @experimentId")
            .WithParameter("@experimentId", experimentId);

        var results = new List<Result>();
        var statistics = new List<Statistics>();
        using var feed = container.GetItemQueryIterator<dynamic>(query);
        while (feed.HasMoreResults)
        {
            var response = await feed.ReadNextAsync(cancellationToken);
            foreach (var item in response)
            {
                string type = item.type;
                if (type == "result")
                {
                    var doc = JsonConvert.DeserializeObject<CosmosResultDocument>(item.ToString());
                    if (doc is not null) results.Add(MapToResult(doc));
                }
                else if (type == "statistics")
                {
                    var doc = JsonConvert.DeserializeObject<CosmosStatisticsDocument>(item.ToString());
                    if (doc is not null) statistics.Add(MapToStatistics(doc));
                }
            }
        }

        return (results, statistics);
    }

    private static Experiment MapToExperiment(CosmosExperimentDocument doc) => new()
    {
        Name = doc.Name!,
        Hypothesis = doc.Hypothesis!,
        Baseline = doc.BaselineSet ?? doc.Baseline,
        Created = doc.Created,
        Modified = doc.Modified,
        Annotations = doc.Annotations
    };

    private static Result MapToResult(CosmosResultDocument doc) => new()
    {
        Ref = doc.Ref,
        Set = doc.Set,
        InferenceUri = doc.InferenceUri,
        EvaluationUri = doc.EvaluationUri,
        Metrics = doc.Metrics,
        Annotations = doc.Annotations,
        PolicyResults = doc.PolicyResults,
        Created = doc.Created,
        Runtime = doc.Runtime
    };

    private static Statistics MapToStatistics(CosmosStatisticsDocument doc) => new()
    {
        Set = doc.Set,
        BaselineExperiment = doc.BaselineExperiment,
        BaselineSet = doc.BaselineSet,
        BaselineResultCount = doc.BaselineResultCount,
        SetResultCount = doc.SetResultCount,
        NumSamples = doc.NumSamples,
        ConfidenceLevel = doc.ConfidenceLevel,
        Metrics = doc.Metrics,
        Created = doc.Created
    };

    // Internal document classes for Cosmos serialization
    private class CosmosProjectDocument
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("baseline_experiment")]
        public string? BaselineExperiment { get; set; }
    }

    private class CosmosExperimentDocument
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("project_name")]
        public string? ProjectName { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("hypothesis")]
        public string? Hypothesis { get; set; }

        [JsonProperty("baseline")]
        public string? Baseline { get; set; }

        [JsonProperty("baseline_set")]
        public string? BaselineSet { get; set; }

        [JsonProperty("created")]
        public DateTimeOffset Created { get; set; }

        [JsonProperty("modified")]
        public DateTimeOffset? Modified { get; set; }

        [JsonProperty("annotations")]
        public List<Annotation>? Annotations { get; set; }
    }

    private class CosmosResultDocument
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("experiment_id")]
        public string? ExperimentId { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("ref")]
        public string? Ref { get; set; }

        [JsonProperty("set")]
        public string? Set { get; set; }

        [JsonProperty("inference_uri")]
        public string? InferenceUri { get; set; }

        [JsonProperty("evaluation_uri")]
        public string? EvaluationUri { get; set; }

        [JsonProperty("metrics")]
        public Dictionary<string, Metric>? Metrics { get; set; }

        [JsonProperty("annotations")]
        public List<Annotation>? Annotations { get; set; }

        [JsonProperty("policy_results")]
        public Dictionary<string, PolicyResult>? PolicyResults { get; set; }

        [JsonProperty("created")]
        public DateTime Created { get; set; }

        [JsonProperty("runtime")]
        public int? Runtime { get; set; }
    }

    private class CosmosStatisticsDocument
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("experiment_id")]
        public string? ExperimentId { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("set")]
        public string? Set { get; set; }

        [JsonProperty("baseline_experiment")]
        public string? BaselineExperiment { get; set; }

        [JsonProperty("baseline_set")]
        public string? BaselineSet { get; set; }

        [JsonProperty("baseline_result_count")]
        public int BaselineResultCount { get; set; }

        [JsonProperty("set_result_count")]
        public int SetResultCount { get; set; }

        [JsonProperty("num_samples")]
        public int NumSamples { get; set; }

        [JsonProperty("confidence_level")]
        public decimal ConfidenceLevel { get; set; }

        [JsonProperty("metrics")]
        public Dictionary<string, Metric>? Metrics { get; set; }

        [JsonProperty("created")]
        public DateTime Created { get; set; }
    }

    private class CosmosTagDocument
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("project_name")]
        public string? ProjectName { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("refs")]
        public List<string>? Refs { get; set; }
    }

    private class CosmosMetricDefinitionDocument
    {
        [JsonProperty("id")]
        public string? Id { get; set; }

        [JsonProperty("project_name")]
        public string? ProjectName { get; set; }

        [JsonProperty("type")]
        public string? Type { get; set; }

        [JsonProperty("name")]
        public string? Name { get; set; }

        [JsonProperty("min")]
        public decimal? Min { get; set; }

        [JsonProperty("max")]
        public decimal? Max { get; set; }

        [JsonProperty("aggregate_function")]
        public AggregateFunctions AggregateFunction { get; set; }

        [JsonProperty("order")]
        public int? Order { get; set; }

        [JsonProperty("tags")]
        public IList<string>? Tags { get; set; }
    }
}
