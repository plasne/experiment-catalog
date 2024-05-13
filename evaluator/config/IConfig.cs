using System.Collections.Generic;
using Iso8601DurationHelper;

public interface IConfig
{
    int PORT { get; }
    List<Roles> ROLES { get; }
    string AZURE_STORAGE_ACCOUNT_NAME { get; }
    string INFERENCE_CONTAINER { get; }
    string EVALUATION_CONTAINER { get; }
    string[] INBOUND_INFERENCE_QUEUES { get; }
    string[] INBOUND_EVALUATION_QUEUES { get; }
    string OUTBOUND_INFERENCE_QUEUE { get; }
    int MS_TO_PAUSE_WHEN_EMPTY { get; }
    int DEQUEUE_FOR_X_SECONDS { get; }
    string INFERENCE_URL { get; }
    string EVALUATION_URL { get; }
    string INBOUND_GROUNDTRUTH_TRANSFORM_FILE { get; }
    string INBOUND_INFERENCE_TRANSFORM_FILE { get; }
    string INBOUND_EVALUATION_TRANSFORM_FILE { get; }
    string INBOUND_GROUNDTRUTH_TRANSFORM_QUERY { get; }
    string INBOUND_INFERENCE_TRANSFORM_QUERY { get; }
    string INBOUND_EVALUATION_TRANSFORM_QUERY { get; }
    int MAX_RETRY_ATTEMPTS { get; }
    int SECONDS_BETWEEN_RETRIES { get; }

    void Validate();
}