# Coding Standards

This document describes the coding standards for the experiment catalog project.

## Comments

Comments should start with a **lowercase letter**, not uppercase:

```csharp
// correct: lowercase start
var client = new CosmosClient(...);

// Incorrect: uppercase start
var client = new CosmosClient(...);
```

## C# Naming Conventions

### Private Fields

Private fields should **not** use underscore prefix. Use camelCase without any prefix.

```csharp
// Correct
private CosmosClient? cosmosClient;
private IStorageService? storageService;

// Incorrect
private CosmosClient? _cosmosClient;
private IStorageService? _storageService;
```

When accessing private fields within the same class, use the `this.` qualifier for clarity:

```csharp
if (this.cosmosClient is null)
{
    this.cosmosClient = new CosmosClient(...);
}
```

### JSON Serialization

Use `snake_case` for JSON property names when serializing to external systems (like Cosmos DB):

```csharp
[JsonProperty("experiment_id")]
public string? ExperimentId { get; set; }

[JsonProperty("baseline_experiment")]
public string? BaselineExperiment { get; set; }

[JsonProperty("policy_results")]
public Dictionary<string, PolicyResult>? PolicyResults { get; set; }
```

### SQL Queries

When writing SQL queries for Cosmos DB, property names should match the JSON serialization format (snake_case):

```csharp
var query = new QueryDefinition("SELECT * FROM c WHERE c.experiment_id = @experimentId")
    .WithParameter("@experimentId", experimentId);
```

## Interface Design

### Storage-Specific Methods

Methods that are specific to a particular storage implementation should **not** be included in the shared interface. For example, `OptimizeExperimentAsync` is specific to Azure Blob Storage and should be kept as a public method on `AzureBlobStorageService` only, not in `IStorageService`.

## Configuration Properties

### Computed Enable Properties

Use computed properties for checking if features are enabled, following the pattern of `IsAuthenticationEnabled`:

```csharp
public bool IsAuthenticationEnabled => string.IsNullOrEmpty(OIDC_AUTHORITY) == false;

public bool IsCosmosEnabled => !string.IsNullOrEmpty(COSMOS_DB_ACCOUNT_ENDPOINT) || !string.IsNullOrEmpty(COSMOS_DB_CONNECTION_STRING);

public bool IsBlobStorageEnabled => !string.IsNullOrEmpty(AZURE_STORAGE_ACCOUNT_NAME) || !string.IsNullOrEmpty(AZURE_STORAGE_ACCOUNT_CONNSTRING);
```

Use these properties in validation logic and factory pattern selection instead of duplicating the check logic.
