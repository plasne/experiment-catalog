using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

public class AzureStorageQueueReader(
    IConfig config,
    DefaultAzureCredential defaultAzureCredential,
    IServiceProvider serviceProvider,
    ILogger<AzureStorageQueueReader> logger)
    : BackgroundService
{
    private readonly IConfig config = config;
    private readonly DefaultAzureCredential defaultAzureCredential = defaultAzureCredential;
    private readonly IServiceProvider serviceProvider = serviceProvider;
    private readonly ILogger<AzureStorageQueueReader> logger = logger;
    private readonly List<QueueClient> inboundQueues = [];
    private QueueClient? outboundQueue;

    private async Task VerifyConnectivity(QueueClient queueClient, CancellationToken cancellationToken)
    {
        this.logger.LogDebug("attempting to connect to queue {q}...", queueClient.Name);
        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(30));
        var properties = await queueClient.GetPropertiesAsync(cts.Token);
        this.logger.LogInformation(
            "successfully authenticated to queue {q} and found ~{c} messages.",
            queueClient.Name,
            properties.Value.ApproximateMessagesCount);
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        // make sure there are inbound queues
        if (this.inboundQueues.Count == 0)
        {
            return;
        }

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

    private async Task<bool> GetMessageFromInboundQueue(QueueClient queue, CancellationToken cancellationToken)
    {
        try
        {
            // check for a message
            this.logger.LogDebug("checking for a message in queue {q}...", queue.Name);
            var message = queue.ReceiveMessage(TimeSpan.FromSeconds(this.config.DEQUEUE_FOR_X_SECONDS), cancellationToken);
            var body = message?.Value?.Body?.ToString();
            if (string.IsNullOrEmpty(body))
            {
                return false;
            }

            // deserialize to the appropriate type
            var request = JsonConvert.DeserializeObject<PipelineRequest>(body)
                ?? throw new Exception("could not deserialize inference request.");

            // process the message
            using var scope = this.serviceProvider.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<IMessageHandler<PipelineRequest>>();
            await handler.ExecuteAsync(request, cancellationToken);

            // delete the message
            await queue.DeleteMessageAsync(message!.Value.MessageId, message.Value.PopReceipt, cancellationToken);
            return true;
        }
        catch (Exception e)
        {
            this.logger.LogError(e, "error getting message from queue {q}...", queue.Name);
            return false;
        }
    }

    private async Task<int> GetMessagesFromInboundQueues(CancellationToken cancellationToken)
    {
        var count = 0;
        try
        {
            foreach (var queue in this.inboundQueues)
            {
                if (await this.GetMessageFromInboundQueue(queue, cancellationToken))
                {
                    count++;
                }
            }

        }
        catch (Exception e)
        {
            this.logger.LogError(e, "error getting messages from queues...");
        }
        return count;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // make sure there are inbound queues
        if (this.inboundQueues.Count == 0)
        {
            return;
        }

        // look for messages in the inbound queues
        while (!stoppingToken.IsCancellationRequested)
        {
            var messagesFound = await this.GetMessagesFromInboundQueues(stoppingToken);
            if (messagesFound == 0)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(this.config.MS_TO_PAUSE_WHEN_EMPTY), stoppingToken);
            }
        }
    }
}