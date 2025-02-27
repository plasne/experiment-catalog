# Evaluator

This solution focuses on how we complete the following activities necessary for an evaluation:

1. [USER] Establish the ground truth.
1. [USER] User kicks off an evaluation.
1. [EVALUATOR] Enqueue the inference jobs.
1. [INFERENCE] Process the inference jobs.
1. [INFERENCE] Enqueue the evaluation jobs.
1. [EVALUATION] Process the evaluation jobs.
1. [EVALUATION] Report the results in the catalog.

The **EVALUATOR** will require using an account with the following permissions:

- **Storage Account Contributor**: Allows the account to get storage keys which are necessary for signing SAS tokens.
- **Storage Blob Data Contributor**: Allows the account to read/write blobs to Azure Blob Storage.
- **Storage Queue Data Contributor**: Allows the account to read/write messages in Azure Storage Queues.

Any **INFERENCE** service could be used, but a complete implementation can be found here: <https://github.com/plasne/llmbot-solution-accel/tree/main/sk>. This service will be used with a **EVALUATOR** sidecar which will require using an account with the following permissions:

- **Storage Queue Data Contributor**: Allows the account to read/write messages in Azure Storage Queues.

Any **EVALUATION** service could be used, but a sample script can be found here: <https://github.com/plasne/experiment-catalog/tree/main/evaluation>. This service will be used with a **EVALUATOR** sidecar which will require using an account with the following permissions:

- **Storage Queue Data Reader**: Allows the account to read messages in Azure Storage Queues.

## Configuration

The following configuration settings are used regardless of the role:

- **LOG_LEVEL** [DEFAULT: Information]: The level of logging to use. The following options are available: Trace, Debug, Information, Warning, Error, Critical, None.

- **ASPNETCORE_ENVIRONMENT** [OPTIONAL]: This can be set to "Development" to change the behavior of **INCLUDE_CREDENTIAL_TYPES**.

- **INCLUDE_CREDENTIAL_TYPES** [CONDITIONAL]: This setting will determine how credentials are obtained to connect to the Azure Storage Account. If the **ASPNETCORE_ENVIRONMENT** is set to "Development", then the default will be "azcli, env" otherwise, it will be "env, mi". This is a comma-separated list of the credential types to include. The following options are available: azcli, env, mi, token, vs, vscode, browser. Please see the [DefaultAzureCredentials](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet) documentation for how each of these work. For instance, if you use "env", you must supply an **AZURE_TENANT_ID**, **AZURE_CLIENT_ID**, and **AZURE_CLIENT_SECRET**.

- **PORT** [DEFAULT: 6030]: The port to run the HTTP API on.

- **ROLES** [1+ of: API, InferenceProxy, EvaluationProxy]: The roles of evaluation that this deployment will fill. The API role is required to enqueue jobs and monitor progress. The InferenceProxy role runs as a sidecar to inference providing the ability to read from the queue, read ground truth, write the inference results, and enqueue evaluation. The EvaluationProxy role runs as a sidecar to evaluation providing the ability to read from the queue, read inference results, write the evaluation results, and report the results in the catalog. The deployment can fulfill multiple roles - for instance, when running on a developer's machine, all roles might be used.

- **OPEN_TELEMETRY_CONNECTION_STRING** [OPTIONAL]: The connection string for the Open Telemetry service. Currently this only supports Application Insights.

- **AZURE_STORAGE_ACCOUNT_NAME** [REQUIRED:1]: The name of the Azure Storage account used for queues and result files. If this setting is used, authentication will be done using DefaultAzureCredentials as discussed above. Either AZURE_STORAGE_ACCOUNT_NAME or AZURE_STORAGE_CONNECTION_STRING must be specified.

- **AZURE_STORAGE_CONNECTION_STRING** [REQUIRED:1]: The connection string for the Azure Storage account used for queues and result files. Either AZURE_STORAGE_ACCOUNT_NAME or AZURE_STORAGE_CONNECTION_STRING must be specified.

### API Role Configuration

The following configuration settings are used when the API role is enabled:

- **INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_FILE** [OPTIONAL]: The path to a local file that will be read and the contents used for INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_QUERY if that setting is not provided.

- **INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_QUERY** [OPTIONAL]: The Jsonata query to convert the ground truth files format from how they appear in the storage account to how they must be consumed by the API in order to create the request. The expected format is `{ "ref": "<ground-truth-ref-id>" }`.

### InferenceProxy Role Configuration

The following configuration settings are used when the InferenceProxy role is enabled:

- **INFERENCE_CONCURRENCY** [DEFAULT: 4]: The number of concurrent requests that can be sent by the sidecar to the inference solution.

- **INFERENCE_CONTAINER** [REQUIRED]: The name of the Azure Storage container where the inference results will be written.

- **INFERENCE_URL** [REQUIRED]: The URL of the inference service.

- **INBOUND_INFERENCE_QUEUES** [REQUIRED]: A comma-separated list of the names of the Azure Storage queues that the sidecar will listen to for inference requests. Requests will be pulled from the queues equitably, ensuring that if multiple developers have evaluation jobs running, no one developer can starve out the others.

- **OUTBOUND_INFERENCE_QUEUE** [REQUIRED]: The name of the Azure Storage queue where the sidecar will enqueue the evaluation requests.

- **INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_FILE** [OPTIONAL]: The path to a local file that will be read and the contents used for INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_QUERY if that setting is not provided.

- **INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_QUERY** [OPTIONAL]: The Jsonata query to convert the ground truth files format from how they appear in the storage account to how they must be consumed by the inference solution. This is optional, obviously it is perferable for the ground truth files to already be in the format required by the inference solution, but when that is not the case, this setting is provided.

## Samples

Samples of all API features (API role) are available in the evaluator.http file. You can use the [REST Client](https://marketplace.visualstudio.com/items?itemName=humao.rest-client) extension in Visual Studio Code to run these samples.

## Establish the ground truth

All ground truth files should be loaded into an Azure Storage Blob Container. Each file should contain 1 question and either be a JSON or YAML file adhering to the following specifications:

```json
{
  "ref": "an-identifier-for-the-question",
  "history": [
    {
      "role": "user",
      "msg": "the-question"
    }
  ],
  "ground_truth": "the-ground-truth-answer"
}
```

```yaml
ref: an-identifier-for-the-question
history:
  - role: user
    msg: the-question
ground_truth: >
  the-ground-truth-answer
  which-can-span-multiple-lines
```

If desired, you can reflect multiple turns of the conversation, for example...

```yaml
ref: an-identifier-for-the-question
history:
  - role: user
    msg: hello
  - role: system
    msg: Hello, what can I help you with today?
  - role: user
    msg: the-question
ground_truth: >
  the-ground-truth-answer
  which-can-span-multiple-lines
```

## User kicks off an evaluation

This portion of the pipeline uses the [evaluator service](https://github.com/plasne/experiment-catalog/tree/main/evaluator). The evaluator service requires at least one pair of matching queues: one for inference and one for evaluation. The queues should be in an Azure Storage Account and be named "QueueName-inference" and "QueueName-evaluation", respectively.

To find out what queue pairs are being identified by the evaluator service, you can query the evaluator service's API:

```bash
curl -i http://localhost:6030/api/queues
```

To kick off an evaluation, you can post a JSON payload to the evaluator service's API like so:

```bash
curl -i -X POST -H "Content-Type: application/json" -d '{ "project": "project-01", "experiment": "experiment-000", "set": "may-01-a", "iterations": 2 }' http://localhost:6030/api/queues/pelasne
```

To further understand this example:

- **project**: This is the name of the project that will be used to identify the evaluation in the catalog.

- **experiment**: This is the name of the experiment that will be used to identify the evaluation in the catalog.

- **set**: The set is a collection of evaluations that are all done with the same parameters (code, configuration, etc.). For instance, if there are 40 questions in the ground truth and 5 iterations, then the set would contain 200 evaluation results. In the catalog, users will be able to see the aggregated results of the set as well as the breakdown by question.

- **iterations**: The number of times to run each question through inference and evaluation. This is useful for understanding the variance in the results.

> Why use queues for the evaluation pipeline?

This allows us to scale the inference and evaluation services easily and independently. During the project where this tool was developed, we had 800 evaluation questions. If we run 5 iterations of each question, that's 4000 inference jobs and 4000 evaluation jobs for a single experiment or evaluation. We can run these jobs in parallel and scale the services as needed. Using storage queues also gives us easy retry capability if something fails.

Traditional model deployments in Azure are throttled per region, using queues also allows us to deploy inference and evalution instances in different regions to avoid throttling. You should also consider models that have global deployment to eliminate having to deploy in multiple regions.

## Enqueue the inference jobs

When the user kicks off the evaluation, the evaluator service will enqueue the inference requests in the "QueueName-inference" queue. The request will look like this:

```json
{
  "ground_truth_uri": "https://stpelasneai8330507663733.blob.core.windows.net/ground-truth/q1.json?sv=2...D",
  "inference_uri": "https://stpelasneai8330507663733.blob.core.windows.net/inference/74e4c7c3-bd72-4def-b21c-477098e87d83-q1.json?sv=2...D",
  "evaluation_uri": "https://stpelasneai8330507663733.blob.core.windows.net/evaluation/74e4c7c3-bd72-4def-b21c-477098e87d83-q1.json?sv=2...D",
  "project": "project-01",
  "experiment": "experiment-000",
  "ref": "q1",
  "set": "gamma",
  "is_baseline": false
}
```

- **ground_truth_uri**: The URI to the ground truth file in Azure Blob Storage.

- **inference_uri**: The URI to the inference file in Azure Blob Storage. This file is not actually created until the inference service is done processing the request.

- **evaluation_uri**: The URI to the evaluation file in Azure Blob Storage. This file is not actually created until the evaluation service is done processing the request.

- **project**: This is the name of the project that will be used to identify the evaluation in the catalog.

- **experiment**: This is the name of the experiment that will be used to identify the evaluation in the catalog.

- **ref**: This is the identifier for the question. It should match the `ref` in the associated ground truth file.

- **set**: The set is a collection of evaluations that are all done with the same parameters as described above.

- **is_baseline**: This is a boolean that indicates whether this is evaluation should be marked as the baseline for this experiment.

## Process the inference jobs

The inference service will listen to the "QueueName-inference" queue and process the inference requests.

The inference service will write the results to the "inference_uri" provided in the request. The format of this file could be anything as long as the evaluation service can read it.

## Enqueue the evaluation jobs

After writing the inference output, the inference service will enqueue an evaluation request in the "QueueName-evaluation" queue. The request is exactly the same as the inference request, in fact, it can be exactly the same payload.

## Process the evaluation jobs

The evaluation service will listen to the "QueueName-evaluation" queue and process the evaluation requests.

The evaluation service will write the results to the "evaluation_uri" provided in the request. The format of this file could be anything - it is not used in any other steps, but users may want to download it for further analysis.

## Report the results in the catalog

The evaluation service will also write the results to the catalog. An example of writing the metrics to catalog is as follows:

```bash
curl -i -X POST -d '{ "ref": "q1", "set": "may-01-a", "metrics": { "gpt-coherance": { "value": 3 }, "gpt-relevance": { "value": 2 }, "gpt-correctness": { "value": 3 } } }' -H "Content-Type: application/json" http://localhost:6010/api/projects/project-01/experiments/experiment-000/results
```

This will write the metrics to the catalog for the given `project` and `experiment`. The metrics will be associated with the question identified by `ref` and the set identified by `set`.

To see what that might look like, see these examples:

![Compare Experiment](./images/compare-experiment.png)

![Compare Set](./images/compare-set.png)

## TODO

- Rather than work off response headers, use Jsonata against the response body.
- Build a UI for enqueing jobs.
- Build an API and a UI for viewing progress.
- Unit testing/integration testing.
- Documentation.
