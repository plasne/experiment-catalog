public interface IQueueService
{
    public Task<List<Queue>> ListQueues(CancellationToken cancellationToken = default);

    public Task Enqueue(string queueName, string message, CancellationToken cancellationToken = default);
}