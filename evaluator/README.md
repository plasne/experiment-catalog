# Evaluator

This solution focuses on how we complete the following activities necessary for an evaluation:

1. [USER] Establish the ground truth.
1. [USER] User kicks off an evaluation.
1. [EVALUATOR] Enqueue the inference jobs.
1. [INFERENCE] Process the inference jobs.
1. [INFERENCE] Enqueue the evaluation jobs.
1. [EVALUATION] Process the evaluation jobs.
1. [EVALUATION] Report the results in the catalog.

The __EVALUATOR__ will require using an account with the following permissions:

- __Storage Account Contributor__: Allows the account to get storage keys which are necessary for signing SAS tokens.
- __Storage Blob Data Contributor__: Allows the account to read/write blobs to Azure Blob Storage.
- __Storage Queue Data Contributor__: Allows the account to read/write messages in Azure Storage Queues.

Any __INFERENCE__ service could be used, but a complete implementation can be found here: <https://github.com/plasne/llmbot-solution-accel/tree/main/sk>. It will require using an account with the following permissions:

- __Storage Queue Data Contributor__: Allows the account to read/write messages in Azure Storage Queues.

Any __EVALUATION__ service could be used, but a sample script can be found here: <https://github.com/plasne/experiment-catalog/tree/main/evaluation>. It will require using an account with the following permissions:

- __Storage Queue Data Reader__: Allows the account to read messages in Azure Storage Queues.

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

- __project__: This is the name of the project that will be used to identify the evaluation in the catalog.

- __experiment__: This is the name of the experiment that will be used to identify the evaluation in the catalog.

- __set__: The set is a collection of evaluations that are all done with the same parameters (code, configuration, etc.). For instance, if there are 40 questions in the ground truth and 5 iterations, then the set would contain 200 evaluation results. In the catalog, users will be able to see the aggregated results of the set as well as the breakdown by question.

- __iterations__: The number of times to run each question through inference and evaluation. This is useful for understanding the variance in the results.

> Why use queues for the evaluation pipeline?

This allows us to scale the inference and evaluation services easily and independently. For this project, we will have at least 300 evaluation questions. If we run 5 iterations of each question, that's 1500 inference jobs and 1500 evaluation jobs for a single experiment or evaluation. We can run these jobs in parallel and scale the services as needed. Using storage queues also gives us easy retry capability if something fails.

Model deployments in Azure are throttled per region, using queues also allows us to deploy inference and evalution instances in different regions to avoid throttling.

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

- __ground_truth_uri__: The URI to the ground truth file in Azure Blob Storage. This URL includes a SAS token that allows the inference service to read the file.

- __inference_uri__: The URI to the inference file in Azure Blob Storage. This URL includes a SAS token that allows the inference service to create/read/write the file. This file is not actually created until the inference service is done processing the request.

- __evaluation_uri__: The URI to the evaluation file in Azure Blob Storage. This URL includes a SAS token that allows the inference service to create/read/write the file. This file is not actually created until the evaluation service is done processing the request.

- __project__: This is the name of the project that will be used to identify the evaluation in the catalog.

- __experiment__: This is the name of the experiment that will be used to identify the evaluation in the catalog.

- __ref__: This is the identifier for the question. It should match the `ref` in the associated ground truth file.

- __set__: The set is a collection of evaluations that are all done with the same parameters as described above.

- __is_baseline__: This is a boolean that indicates whether this is evaluation should be marked as the baseline for this experiment.

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
- Add authentication for API.
- Build a UI for enqueing jobs.
- Build an API and a UI for viewing progress.
- Unit testing/integration testing.
- Documentation.
