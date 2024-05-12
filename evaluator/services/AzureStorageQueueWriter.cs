using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

public class AzureStorageQueueWriter : BackgroundService
{

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // try and connect to all the inbound queues
        foreach (var queue in this.config.INBOUND_QUEUES)
        {
            var url = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{queue}";
            var queueClient = new QueueClient(new Uri(url), this.defaultAzureCredential);
            await this.VerifyConnectivity(queueClient, cancellationToken);
            this.inboundQueues.Add(queueClient);
        }

        // try and connect to the outbound queue
        if (!string.IsNullOrEmpty(this.config.OUTBOUND_QUEUE))
        {
            var url = $"https://{this.config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{this.config.OUTBOUND_QUEUE}";
            var queueClient = new QueueClient(new Uri(url), this.defaultAzureCredential);
            await this.VerifyConnectivity(queueClient, cancellationToken);
            this.outboundQueue = queueClient;
        }

        await base.StartAsync(cancellationToken);
    }


    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        throw new System.NotImplementedException();
    }
}