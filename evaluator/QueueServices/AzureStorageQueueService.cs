using Azure.Storage.Queues;

public class AzureStorageQueueService : IQueueService
{
    private readonly SemaphoreSlim connectLock = new(1, 1);
    private QueueServiceClient? queueServiceClient;

    private async Task<QueueServiceClient> Connect(CancellationToken cancellationToken = default)
    {
        try
        {
            await this.connectLock.WaitAsync(cancellationToken);

            if (this.queueServiceClient is null)
            {
                var connectionString = NetBricks.Config.GetOnce("AZURE_STORAGE_CONNECTION_STRING");
                this.queueServiceClient = new QueueServiceClient(connectionString);
            }

            return queueServiceClient;
        }
        finally
        {
            this.connectLock.Release();
        }
    }

    public async Task<List<Queue>> ListQueues(CancellationToken cancellationToken = default)
    {
        var queueServiceClient = await this.Connect(cancellationToken);

        // get all the queue names
        var queueNames = new List<string>();
        await foreach (var queueItem in queueServiceClient.GetQueuesAsync(cancellationToken: cancellationToken))
        {
            queueNames.Add(queueItem.Name);
        }

        // filter the queue names
        var inferenceQueues = queueNames.Where(x => x.EndsWith("-inference"));
        var evaluationQueues = queueNames.Where(x => x.EndsWith("-evaluation"));

        // find the matches
        var queues = new List<Queue>();
        foreach (var queueName in inferenceQueues)
        {
            var prefix = queueName[..^10];
            if (evaluationQueues.Contains($"{prefix}-evaluation"))
            {
                queues.Add(new Queue { Name = prefix });
            }
        }

        return queues;
    }

    public async Task Enqueue(string queueName, string message, CancellationToken cancellationToken = default)
    {
        var queueServiceClient = await this.Connect(cancellationToken);
        var queueClient = queueServiceClient.GetQueueClient(queueName);
        await queueClient.SendMessageAsync(message, cancellationToken);
    }
}