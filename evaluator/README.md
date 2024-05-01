# Evaluator

Storage Account Contributor - get the keys
Storage Blob Data Contributor - read/write to blob
Storage Queue Data Contributor - read/write to queue

To get a list of queues...

```bash
curl -i http://localhost:6030/api/queues
```

To enqueue an evaluation...

```bash
curl -i -X POST -H "Content-Type: application/json" -d '{ "project": "project-01", "experiment": "experiment-000", "set": "may-01-a" }' http://localhost:6030/api/queues/pelasne
```
