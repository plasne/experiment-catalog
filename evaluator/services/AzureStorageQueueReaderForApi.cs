using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure.Identity;
using Azure.Storage.Queues;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NetBricks;
using Newtonsoft.Json;

namespace Evaluator;

public class AzureStorageQueueReaderForApi(
    IConfigFactory<IConfig> configFactory,
    DefaultAzureCredential defaultAzureCredential,
    IServiceProvider serviceProvider,
    ILogger<AzureStorageQueueReaderForApi> logger)
    : BackgroundService
{
    private readonly IConfigFactory<IConfig> configFactory = configFactory;
    private readonly DefaultAzureCredential defaultAzureCredential = defaultAzureCredential;
    private readonly IServiceProvider serviceProvider = serviceProvider;
    private readonly ILogger<AzureStorageQueueReaderForApi> logger = logger;
    private QueueClient? inboundQueue;
    private QueueClient? deadletterQueue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        this.logger.LogInformation("starting to listen for job requests in AzureStorageQueueReaderForApi...");
        while (!stoppingToken.IsCancellationRequested)
        {
            var processed = await this.ProcessNextMessageAsync(stoppingToken);
            if (!processed)
            {
                var config = await this.configFactory.GetAsync(stoppingToken);
                if (config.MS_TO_PAUSE_WHEN_EMPTY > 0)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(config.MS_TO_PAUSE_WHEN_EMPTY), stoppingToken);
                }
            }
        }
    }

    private async Task<bool> ProcessNextMessageAsync(CancellationToken cancellationToken)
    {
        try
        {
            var config = await this.configFactory.GetAsync(cancellationToken);

            this.logger.LogDebug("checking for a message in queue {q}...", this.inboundQueue!.Name);
            var message = await this.inboundQueue.ReceiveMessageAsync(
                TimeSpan.FromSeconds(config.DEQUEUE_FOR_X_SECONDS),
                cancellationToken);
            var body = message?.Value?.Body?.ToString();
            if (string.IsNullOrEmpty(body))
            {
                return false;
            }

            // handle deadletter
            if (message!.Value.DequeueCount > config.MAX_ATTEMPTS_TO_DEQUEUE)
            {
                if (this.deadletterQueue is not null)
                {
                    this.logger.LogWarning(
                        "message {m} has been dequeued {c} times; moving to dead-letter queue {q}...",
                        message.Value.MessageId,
                        message.Value.DequeueCount,
                        this.deadletterQueue.Name);
                    await this.deadletterQueue.SendMessageAsync(body, cancellationToken);
                }
                else
                {
                    this.logger.LogWarning(
                        "message {m} has been dequeued {c} times; no dead-letter queue configured, discarding message.",
                        message.Value.MessageId,
                        message.Value.DequeueCount);
                }
                await this.inboundQueue.DeleteMessageAsync(message.Value.MessageId, message.Value.PopReceipt, cancellationToken);
                return true;
            }

            // deserialize the enqueue request
            var request = JsonConvert.DeserializeObject<EnqueueRequest>(body)
                ?? throw new Exception("could not deserialize EnqueueRequest from job queue message.");

            // forward to the existing AzureStorageQueueWriter via its channel
            var writer = this.serviceProvider
                .GetServices<IHostedService>()
                .OfType<AzureStorageQueueWriter>()
                .First();
            await writer.StartEnqueueRequestAsync(request);

            // delete the message
            await this.inboundQueue.DeleteMessageAsync(message.Value.MessageId, message.Value.PopReceipt, cancellationToken);
            this.logger.LogInformation("successfully processed job queue message {m} for run {r}.", message.Value.MessageId, request.RunId);
            return true;
        }
        catch (OperationCanceledException)
        {
            // ignore; this is expected when stopping
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "error processing message from job queue...");
        }
        return false;
    }

    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        var config = await this.configFactory.GetAsync(cancellationToken);
        if (!config.ROLES.Contains(Roles.API))
        {
            this.logger.LogInformation("API role not configured; skipping AzureStorageQueueReaderForApi.");
            return;
        }

        if (string.IsNullOrEmpty(config.INBOUND_JOB_QUEUE))
        {
            this.logger.LogInformation("INBOUND_JOB_QUEUE not configured; skipping AzureStorageQueueReaderForApi.");
            return;
        }

        // connect to the inbound queue
        var queueUrl = $"https://{config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{config.INBOUND_JOB_QUEUE}";
        this.inboundQueue = string.IsNullOrEmpty(config.AZURE_STORAGE_CONNECTION_STRING)
            ? new QueueClient(new Uri(queueUrl), this.defaultAzureCredential)
            : new QueueClient(config.AZURE_STORAGE_CONNECTION_STRING, config.INBOUND_JOB_QUEUE);
        await this.inboundQueue.ConnectAsync(this.logger, cancellationToken);

        // connect to the deadletter queue (optional)
        try
        {
            var deadletterUrl = $"https://{config.AZURE_STORAGE_ACCOUNT_NAME}.queue.core.windows.net/{config.INBOUND_JOB_QUEUE}-deadletter";
            this.deadletterQueue = string.IsNullOrEmpty(config.AZURE_STORAGE_CONNECTION_STRING)
                ? new QueueClient(new Uri(deadletterUrl), this.defaultAzureCredential)
                : new QueueClient(config.AZURE_STORAGE_CONNECTION_STRING, config.INBOUND_JOB_QUEUE + "-deadletter");
            await this.deadletterQueue.ConnectAsync(this.logger, cancellationToken);
        }
        catch (Exception ex)
        {
            this.logger.LogWarning(ex, "dead-letter queue {q}-deadletter not available; dead-lettering will be disabled.", config.INBOUND_JOB_QUEUE);
            this.deadletterQueue = null;
        }

        await base.StartAsync(cancellationToken);
    }
}
