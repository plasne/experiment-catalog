public interface IQueueService
{
    Task<List<Queue>> ListQueues(CancellationToken cancellationToken = default);

    Task Enqueue(string queueName, string message, CancellationToken cancellationToken = default);
}