using Azure.Storage.Queues;

public class AzureStorageQueueService : IQueueService
{
    private readonly SemaphoreSlim connectLock = new(1, 1);
    private QueueServiceClient? queueServiceClient;

    private async Task<QueueServiceClient> Connect()
    {
        try
        {
            await this.connectLock.WaitAsync();

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

    public async Task<List<Queue>> ListQueues()
    {
        var queueServiceClient = await this.Connect();

        // get all the queue names
        var queueNames = new List<string>();
        await foreach (var queueItem in queueServiceClient.GetQueuesAsync())
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
}