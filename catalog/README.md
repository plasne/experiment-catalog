# Experiment Catalog

The catalog is a C# API that allows you to create projects with experiments. It then allows you to record results on arbitrary metrics and compare them.

## Configuration

To configure the solution, you must provide the following environment variables. You can do that by any means, but it is also supported to create a .env file at the root of the project.

- __LOG_LEVEL__ [DEFAULT: Information]: The level of logging to use. The following options are available: Trace, Debug, Information, Warning, Error, Critical, None.

- __ASPNETCORE_ENVIRONMENT__ [OPTIONAL]: This can be set to "Development" to change the behavior of __INCLUDE_CREDENTIAL_TYPES__.

- __INCLUDE_CREDENTIAL_TYPES__ [CONDITIONAL]: This setting will determine how credentials are obtained to connect to the Azure Storage Account. If the __ASPNETCORE_ENVIRONMENT__ is set to "Development", then the default will be "azcli, env" otherwise, it will be "env, mi". This is a comma-separated list of the credential types to include. The following options are available: azcli, env, mi, token, vs, vscode, browser. Please see the [DefaultAzureCredentials](https://learn.microsoft.com/en-us/dotnet/api/azure.identity.defaultazurecredential?view=azure-dotnet) documentation for how each of these work. For instance, if you use "env", you must supply an __AZURE_TENANT_ID__, __AZURE_CLIENT_ID__, and __AZURE_CLIENT_SECRET__.

- __PORT__ [DEFAULT: 6010]: The port to run the HTTP API on.

- __OPEN_TELEMETRY_CONNECTION_STRING__ [OPTIONAL]: The connection string for the Open Telemetry service. Currently this only supports Application Insights.

- __AZURE_STORAGE_ACCOUNT_NAME__ [REQUIRED]: The name of the Azure Storage account to use for storing the project containers. It is recommended to use a separate storage account for this purpose.

- __CONCURRENCY__ [DEFAULT: 4]: The number of concurrent threads that can be used for processing requests (such as loading experiments).

- __REQUIRED_BLOCK_SIZE_IN_KB_FOR_OPTIMIZE__ [DEFAULT: 1024]: In order to improve performance, the catalog will compact smaller blocks in an experiment file into larger blocks. If the average of the block size is smaller than this threshold in KB, then the catalog will optimize the file. In other words, by default if the average block size is less than 1MB, then the catalog will optimize the file.

- __REQUIRED_MIN_OF_IDLE_BEFORE_OPTIMIZE__ [DEFAULT: 10]: The number of minutes that must pass without new results coming into the catalog before it will optimize the file. This is to reduce the chance that the catalog is attempting to optimize the file while jobs are running. Any attempt to send results during optimization will fail with a 409 Conflict error.

- __OPTIMIZE_EVERY_X_MINUTES__ [DEFAULT: 5]: The number of minutes that must pass since the last optimization attempts before the catalog will attempt to optimize anything again. After startup, the catalog will attempt to optimize the files after 5 minutes (default) and after finishing will wait another 5 minutes (default) before attempting to optimize again. Each file is checked against __REQUIRED_BLOCK_SIZE_IN_KB_FOR_OPTIMIZE__ and __REQUIRED_MIN_OF_IDLE_BEFORE_OPTIMIZE__ to determine if it is eligible.

## Concepts

The catalog is organized around the following concepts:

- __Project__: A project is a collection of experiments that are all tied to the same baseline. During that project, you would expect that the grounding data and evaluation script/metrics/configuration would not change.

- __Project Baseline__: A project baseline is a special experiment that is created for each project before experimentation is done. This experiment will have an experimentation run that can be used as a comparison point for all other experiments in the project. Did they get better or worse than this baseline?

- __Experiment__: Inside a project, developers will create experiments with a hypothesis. This experiment will have a number of evaluation runs to test code, configuration, workflow, etc. - the ultimate goal of which of is to prove or disprove the hypothesis.

- __Experiment Baseline__: The first evaluation run of an experiment is generally the baseline for that experiment. Before a developer starts changing things, they need to record what the performance of the system is. If the experiment is started right after the project is started, then this baseline is probably the same as the project baseline, but as code gets merged during the project there will be drift.

- __Set__: A set is a collection of results that are all related to the same evaluation run. For instance, running 3 iterations of 12 ground truths might be considered a single set. If you later decided you needed 2 more iterations, you could add to the set.

- __Ref__: A ref is a reference to a entity that is being evaluated. Almost always this should be a reference to the ground truth. It is common that you might run multiple iterations of the same ground truth, using a ref is a way to aggregate those as well as compare the performance of ground truths across evaluation runs.

## Web UI

The UI for the catalog is written in Svelte in the [UI](./ui) project. Generally, the UI is hosted inside the catalog and that means the UI must be built and copied into the catalog project. Running this command will do this...

```bash
./refresh-ui.sh
```

## Create a project

You can call the API like this to create a project...

```bash
curl -i -X POST -H "Content-Type: application/json" -d '{ "name": "project-example" }' http://localhost:6010/api/projects
```

This will create a container in Azure Blob Storage of the specified name. The container will have a metadata property of "exp_catalog_type": "project".

## Create a baseline

You can call the API to create a baseline experiment like this...

```bash
curl -i -X POST -d '{ "name": "project-baseline", "hypothesis": "my baseline" }' -H "Content-Type: application/json" http://localhost:6010/api/projects/project-example/experiments
```

This will create an experiment blob in the project container. You should create a baseline experiment like this for each project. This baseline gives you a way to compare your experimentation results versus a stable point in time. You can mark this experiment as the baseline like this...

```bash
curl -i -X PATCH http://localhost:6010/api/projects/project-example/experiments/project-baseline/baseline
```

Finally, you can record any results you have for the baseline experiment like this...

```bash
curl -i -X POST -d '{ "ref": "q1", "set": "baseline-0", "metrics": { "gpt-coherance": 2, "gpt-relevance": 3, "gpt-correctness": 2 } }' -H "Content-Type: application/json" http://localhost:6010/api/projects/project-example/experiments/project-baseline/results
```

You do not have to pre-define any metrics, anything you want to send into the catalog will be accepted.

## Create an experiment

After you have a baseline, you will create some experiments. For example, you might create an experiment like this...

```bash
curl -i -X POST -d '{ "name": "experiment-000", "hypothesis": "I believe decreasing the temperature will give better results." }' -H "Content-Type: application/json" http://localhost:6010/api/projects/project-example/experiments
```

Then to record results for that experiment, you can do it exactly like the baseline...

```bash
curl -i -X POST -d '{ "ref": "q1", "set": "beta", "metrics": { "gpt-coherance": 3, "gpt-relevance": 2, "gpt-correctness": 3 } }' -H "Content-Type: application/json" http://localhost:6010/api/projects/project-example/experiments/experiment-000/results
```

While generally the first evaluation run of an experiment is the baseline, you can record laster evaluation runs as the baseline by including is_baseline...

```bash
curl -i -X POST -d '{ "ref": "q1", "set": "beta", "metrics": { "gpt-coherance": 3, "gpt-relevance": 2, "gpt-correctness": 3 }, "is_baseline": true }' -H "Content-Type: application/json" http://localhost:6010/api/projects/project-example/experiments/experiment-000/results
```

## Compare

Once you have some results for your experiment, you can compare them like this...

```bash
curl -i http://localhost:6010/api/projects/project-example/experiments/experiment-000/compare
```

## Annotate

If you want to annotate a set you could do it like this...

```bash
curl -i -X POST -d '{ "set": "alpha", "annotations": [ { "text": "commit 3746hf", "uri": "https://dev.azure.com/commit" } ] }' -H "Content-Type: application/json" http://localhost:6010/api/projects/project-01/experiments/pelasne-01/results
```

In that example, the commit number is being annotated so that the user could get back to the same code and configuration to repeat the experiment.

## Docker

To build the catalog service, you must be at the root and run...

```bash
docker build --rm -t exp-catalog:latest -f catalog.Dockerfile .
```

This is necessary so that the UI can be built and injected into the catalog container in a single build command.

## TODO

- Add authentication for API.
- Implement broker to be able to get to inference and evaluation output files.
  - The UI needs to be updated to allow both inference and evaluation output files to be viewed.
- Finish policy statements implementation.
- Build a UI for creating projects, evaluations, set project baseline.
- Unit testing/integration testing.
