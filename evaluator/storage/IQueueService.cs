using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

public interface IQueueService
{
    Task<List<Queue>> ListQueues(CancellationToken cancellationToken = default);

    Task Enqueue(string queueName, string message, CancellationToken cancellationToken = default);
}