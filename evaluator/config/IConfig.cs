using System.Collections.Generic;

namespace Evaluator;

public interface IConfig
{
    int PORT { get; set; }
    List<Roles> ROLES { get; set; }
    string? OPEN_TELEMETRY_CONNECTION_STRING { get; set; }
    string? AZURE_STORAGE_ACCOUNT_NAME { get; set; }
    string? AZURE_STORAGE_CONNECTION_STRING { get; set; }
    string? INFERENCE_CONTAINER { get; set; }
    string? EVALUATION_CONTAINER { get; set; }
    string[] INBOUND_INFERENCE_QUEUES { get; set; }
    string[] INBOUND_EVALUATION_QUEUES { get; set; }
    string? OUTBOUND_INFERENCE_QUEUE { get; set; }
    int INFERENCE_CONCURRENCY { get; set; }
    int EVALUATION_CONCURRENCY { get; set; }
    int MS_TO_PAUSE_WHEN_EMPTY { get; set; }
    int DEQUEUE_FOR_X_SECONDS { get; set; }
    int MS_BETWEEN_DEQUEUE { get; set; }
    int MS_BETWEEN_DEQUEUE_CURRENT { get; set; }
    int MAX_ATTEMPTS_TO_DEQUEUE { get; set; }
    int MS_TO_ADD_ON_BUSY { get; set; }
    int MINUTES_BETWEEN_RESTORE_AFTER_BUSY { get; set; }
    string? INFERENCE_URL { get; set; }
    string? EVALUATION_URL { get; set; }
    int SECONDS_BEFORE_TIMEOUT_FOR_PROCESSING { get; set; }
    int[] BACKOFF_ON_STATUS_CODES { get; set; }
    int[] DEADLETTER_ON_STATUS_CODES { get; set; }
    string? EXPERIMENT_CATALOG_BASE_URL { get; set; }
    string? INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_FILE { get; set; }
    string? INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_QUERY { get; set; }
    string? INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_FILE { get; set; }
    string? INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_QUERY { get; set; }
    string? INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_FILE { get; set; }
    string? INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_QUERY { get; set; }
    string? INBOUND_INFERENCE_TRANSFORM_FILE { get; set; }
    string? INBOUND_INFERENCE_TRANSFORM_QUERY { get; set; }
    string? INBOUND_EVALUATION_TRANSFORM_FILE { get; set; }
    string? INBOUND_EVALUATION_TRANSFORM_QUERY { get; set; }
    bool PROCESS_METRICS_IN_INFERENCE_RESPONSE { get; set; }
    bool PROCESS_METRICS_IN_EVALUATION_RESPONSE { get; set; }
    string? JOB_STATUS_CONTAINER { get; set; }
    int JOB_DONE_TIMEOUT_MINUTES { get; set; }
}