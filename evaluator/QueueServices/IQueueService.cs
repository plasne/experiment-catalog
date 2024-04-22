public interface IQueueService
{
    public Task<List<Queue>> ListQueues();
}