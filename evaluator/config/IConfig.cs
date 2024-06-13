using System.Collections.Generic;

namespace Evaluator;

public interface IConfig
{
    int PORT { get; }
    List<Roles> ROLES { get; }
    string OPEN_TELEMETRY_CONNECTION_STRING { get; }
    string AZURE_STORAGE_ACCOUNT_NAME { get; }
    string AZURE_STORAGE_CONNECTION_STRING { get; }
    string INFERENCE_CONTAINER { get; }
    string EVALUATION_CONTAINER { get; }
    string[] INBOUND_INFERENCE_QUEUES { get; }
    string[] INBOUND_EVALUATION_QUEUES { get; }
    string OUTBOUND_INFERENCE_QUEUE { get; }
    int CONCURRENCY { get; }
    int MS_TO_PAUSE_WHEN_EMPTY { get; }
    int DEQUEUE_FOR_X_SECONDS { get; }
    int MS_BETWEEN_DEQUEUE { get; }
    int MS_BETWEEN_DEQUEUE_CURRENT { get; set; }
    int MAX_ATTEMPTS_TO_DEQUEUE { get; }
    int MS_TO_ADD_ON_BUSY { get; }
    int MINUTES_BETWEEN_RESTORE_AFTER_BUSY { get; }
    string INFERENCE_URL { get; }
    string EVALUATION_URL { get; }
    int[] BACKOFF_ON_STATUS_CODES { get; }
    int[] DEADLETTER_ON_STATUS_CODES { get; }
    string EXPERIMENT_CATALOG_BASE_URL { get; }
    string INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_FILE { get; }
    string INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_QUERY { get; }
    string INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_FILE { get; }
    string INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_QUERY { get; }
    string INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_FILE { get; }
    string INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_QUERY { get; }
    string INBOUND_INFERENCE_TRANSFORM_FILE { get; }
    string INBOUND_INFERENCE_TRANSFORM_QUERY { get; }
    string INBOUND_EVALUATION_TRANSFORM_FILE { get; }
    string INBOUND_EVALUATION_TRANSFORM_QUERY { get; }

    void Validate();
}