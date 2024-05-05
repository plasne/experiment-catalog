# Experiment Catalog

## Create a project

```bash
curl -i -X POST -H "Content-Type: application/json" -d '{ "name": "project-03" }' http://localhost:6010/api/projects
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

## Enqueue Inference

```bash
curl -i -X POST -H "Content-Type: application/json" -d '{ "project": "project-01", "experiment": "experiment-000", "set": "both", "datasources": ["test"], "iterations": 3 }' http://localhost:6030/api/queues/pelasne
```

## Annotate

```bash
curl -i -X POST -d '{ "set": "alpha", "annotations": [ { "text": "commit 3746hf", "uri": "https://dev.azure.com/commit" } ] }' -H "Content-Type: application/json" http://localhost:6010/api/projects/project-01/experiments/pelasne-01/results
```

## DONE

- added datasources to enqueue
- added annotations
- added optimize
- added loading indicator
- allow selection of # of sets load
- support for DefaultAzureCredential

## TODO

- Add StyleCop
- Add UI to create a project
- Add UI to create an experiment
- Add UI to mark a baseline
- Test cases like no baseline experiment, no experiment to compare, etc.
- Add a Dockerfile and test deploying
- Get docs from Stewart
- Bulk up documentation
