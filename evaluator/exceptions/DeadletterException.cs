using System;
using Azure.Storage.Queues.Models;

namespace Evaluator;

public class DeadletterException(string message, QueueMessage queueMessage, string queueBody) : Exception(message)
{
    public QueueMessage QueueMessage { get; } = queueMessage;
    public string QueueBody { get; } = queueBody;
}
