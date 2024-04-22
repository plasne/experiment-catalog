# Experiment Catalog

## Create a project

```bash
TBD
```

## Create a baseline

Create the baseline experiment...

```bash
curl -i -X POST -d '{ "name": "project-baseline", "description": "my baseline" }' -H "Content-Type: application/json" http://localhost:6010/api/projects/project-01/experiments
```

Record one or more evaluations...

```bash
curl -i -X POST -d '{ "ref": "q1", "set": "baseline-0", "metrics": { "gpt-coherance": { "value": 2 }, "gpt-relevance": { "value": 3 }, "gpt-correctness": { "value": 2 } } }' -H "Content-Type: application/json" http://localhost:6010/api/projects/project-01/experiments/pelasne-baseline/results
```

Mark this experiment as the baseline for the project...

```bash
curl -i -X PATCH http://localhost:6010/api/projects/project-01/experiments/project-baseline/baseline
```

## Create an experiment

```bash
curl -i -X POST -d '{ "name": "experiment-000", "description": "decrease temp", "hypothesis": "I believe decreasing the temperature will give better results." }' -H "Content-Type: application/json" http://localhost:6010/api/projects/project-01/experiments
```

Record one or more evaluations...

```bash
curl -i -X POST -d '{ "ref": "q1", "set": "alpha", "metrics": { "gpt-coherance": { "value": 3 }, "gpt-relevance": { "value": 2 }, "gpt-correctness": { "value": 3 } } }' -H "Content-Type: application/json" http://localhost:6010/api/projects/project-01/experiments/experiment-000/results
```

## Compare

```bash
curl -i http://localhost:6010/api/projects/project-01/experiments/experiment-000/compare
```

## TODO

- Add proper config implementation
- Add StyleCop
- Add validation to the API
- Add an endpoint for creating a project
- Add UI to create a project
- Add UI to create an experiment
- Add UI to mark a baseline
- Share styles like link-button across the ui project
- Test cases like no baseline experiment, no experiment to compare, etc.
- Switch to DefaultAzureCredential
- Add a Dockerfile and test deploying
- Get docs from Stewart
- Bulk up documentation
