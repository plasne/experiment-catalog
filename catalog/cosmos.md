# Cosmos DB Setup Guide

This guide explains how to configure Azure Cosmos DB as the storage backend for the experiment catalog.

## Prerequisites

- Azure subscription
- Azure CLI or Azure Portal access
- Permissions to create Cosmos DB resources

## Create Cosmos DB Account

### Using Azure CLI

```bash
# Set variables
RESOURCE_GROUP="your-resource-group"
ACCOUNT_NAME="your-cosmos-account"
LOCATION="eastus"

# Create Cosmos DB account
az cosmosdb create \
  --name $ACCOUNT_NAME \
  --resource-group $RESOURCE_GROUP \
  --locations regionName=$LOCATION \
  --default-consistency-level Session \
  --kind GlobalDocumentDB

# Create database
az cosmosdb sql database create \
  --account-name $ACCOUNT_NAME \
  --resource-group $RESOURCE_GROUP \
  --name exp-catalog

# Create containers
az cosmosdb sql container create \
  --account-name $ACCOUNT_NAME \
  --resource-group $RESOURCE_GROUP \
  --database-name exp-catalog \
  --name projects \
  --partition-key-path /name

az cosmosdb sql container create \
  --account-name $ACCOUNT_NAME \
  --resource-group $RESOURCE_GROUP \
  --database-name exp-catalog \
  --name experiments \
  --partition-key-path /projectName

az cosmosdb sql container create \
  --account-name $ACCOUNT_NAME \
  --resource-group $RESOURCE_GROUP \
  --database-name exp-catalog \
  --name results \
  --partition-key-path /experimentId
```

### Using Azure Portal

1. Navigate to Azure Portal > Create a resource > Azure Cosmos DB
2. Select **Azure Cosmos DB for NoSQL**
3. Configure:
   - Account Name: Choose a unique name
   - Location: Select your region
   - Capacity mode: Provisioned throughput or Serverless
4. Create the account
5. Navigate to Data Explorer
6. Create database `exp-catalog`
7. Create containers:
   - `projects` with partition key `/name`
   - `experiments` with partition key `/projectName`
   - `results` with partition key `/experimentId`

## Configure Indexing Policy

For optimal query performance, configure the following indexing policy on the `results` container:

```json
{
  "indexingMode": "consistent",
  "automatic": true,
  "includedPaths": [
    {
      "path": "/*"
    }
  ],
  "excludedPaths": [
    {
      "path": "/metrics/*"
    },
    {
      "path": "/annotations/*"
    },
    {
      "path": "/\"_etag\"/?"
    }
  ],
  "compositeIndexes": [
    [
      { "path": "/experimentId", "order": "ascending" },
      { "path": "/type", "order": "ascending" },
      { "path": "/set", "order": "ascending" }
    ],
    [
      { "path": "/experimentId", "order": "ascending" },
      { "path": "/type", "order": "ascending" },
      { "path": "/created", "order": "descending" }
    ]
  ]
}
```

Apply via Azure CLI:

```bash
az cosmosdb sql container update \
  --account-name $ACCOUNT_NAME \
  --resource-group $RESOURCE_GROUP \
  --database-name exp-catalog \
  --name results \
  --idx @indexing-policy.json
```

## Configuration

### Using Managed Identity (Recommended)

1. Enable managed identity on your App Service or VM
2. Assign the **Cosmos DB Built-in Data Contributor** role:

```bash
PRINCIPAL_ID="your-managed-identity-principal-id"
COSMOS_SCOPE="/subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.DocumentDB/databaseAccounts/{account}"

az cosmosdb sql role assignment create \
  --account-name $ACCOUNT_NAME \
  --resource-group $RESOURCE_GROUP \
  --principal-id $PRINCIPAL_ID \
  --role-definition-id "00000000-0000-0000-0000-000000000002" \
  --scope $COSMOS_SCOPE
```

3. Set environment variables:

```bash
COSMOS_DB_ACCOUNT_ENDPOINT=https://your-cosmos-account.documents.azure.com:443/
COSMOS_DB_DATABASE_NAME=exp-catalog
```

### Using Connection String

1. Get connection string from Azure Portal > Cosmos DB Account > Keys
2. Set environment variables:

```bash
COSMOS_DB_CONNECTION_STRING="AccountEndpoint=https://...;AccountKey=..."
COSMOS_DB_DATABASE_NAME=exp-catalog
```

## Environment Variables

| Variable                      | Required | Description                                           |
| ----------------------------- | -------- | ----------------------------------------------------- |
| `COSMOS_DB_ACCOUNT_ENDPOINT`  | Yes\*    | Cosmos DB account endpoint URL (for managed identity) |
| `COSMOS_DB_CONNECTION_STRING` | Yes\*    | Cosmos DB connection string (for key-based auth)      |
| `COSMOS_DB_DATABASE_NAME`     | Yes      | Database name (defaults to "exp-catalog")             |

\*One of `COSMOS_DB_ACCOUNT_ENDPOINT` or `COSMOS_DB_CONNECTION_STRING` is required.

## Container Schema

### projects Container

Partition key: `/name`

Document types:

- `type: "project"` - Project metadata with `baselineExperiment` field
- `type: "tag"` - Tag definitions with `refs` array
- `type: "metric"` - Metric definitions with aggregation settings

### experiments Container

Partition key: `/projectName`

Document fields:

- `id`: `{projectName}|{experimentName}`
- `projectName`: Parent project name
- `name`: Experiment name
- `hypothesis`: Experiment hypothesis
- `baseline`: Baseline reference (experiment name or set name)
- `baselineSet`: Baseline set name
- `created`: Creation timestamp
- `modified`: Last modification timestamp
- `annotations`: List of annotations

### results Container

Partition key: `/experimentId` (format: `{projectName}|{experimentName}`)

Document types:

- `type: "result"` - Individual result with `set`, `ref`, `metrics`
- `type: "statistics"` - Calculated statistics with baseline references

## Migration from Blob Storage

The application supports only one storage provider at a time. To migrate:

1. Export data from Blob Storage
2. Create Cosmos DB account and containers
3. Import data to Cosmos DB
4. Update environment variables to use Cosmos DB
5. Restart the application

## Troubleshooting

### Common Issues

1. **Authentication failed**: Ensure managed identity has the correct role assignment or connection string is valid
2. **Container not found**: Verify containers are created with the correct names and partition keys
3. **Query timeout**: Check indexing policy includes queried paths
4. **Request rate too large (429)**: Increase RU/s or enable autoscale

### Viewing Logs

The application logs Cosmos DB connection details at startup:

```
Connecting to Cosmos DB using managed identity at https://...
```

or

```
Connecting to Cosmos DB using connection string.
```

### Verifying Configuration

The application validates that exactly one storage provider is configured. If validation fails:

```
Either Azure Blob Storage (AZURE_STORAGE_ACCOUNT_NAME or AZURE_STORAGE_ACCOUNT_CONNSTRING)
or Cosmos DB (COSMOS_DB_ACCOUNT_ENDPOINT or COSMOS_DB_CONNECTION_STRING) must be configured.
```

or

```
Only one storage provider can be configured. Set either Azure Blob Storage or
Cosmos DB properties, not both.
```
