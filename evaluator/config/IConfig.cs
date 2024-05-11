using Iso8601DurationHelper;

public interface IConfig
{
    int PORT { get; }
    string AZURE_STORAGE_ACCOUNT_ID { get; }
    string AZURE_STORAGE_INFERENCE_CONTAINER_NAME { get; }
    string AZURE_STORAGE_EVALUATION_CONTAINER_NAME { get; }
    Duration MAX_DURATION_TO_RUN_EVALUATIONS { get; }
    Duration MAX_DURATION_TO_VIEW_RESULTS { get; }
    int CONCURRENCY { get; }
    string AZURE_STORAGE_ACCOUNT_NAME { get; }
    string[] INBOUND_QUEUES { get; }
    string OUTBOUND_QUEUE { get; }
    int MS_TO_PAUSE_WHEN_EMPTY { get; }
    int DEQUEUE_FOR_X_SECONDS { get; }
    Stages INBOUND_STAGE { get; }
    Stages OUTBOUND_STAGE { get; }
    string PROCESSING_URL { get; }
    string PATH_TO_TRANSFORM_QUERY { get; }
    string TRANSFORM_QUERY { get; }
    int MAX_RETRY_ATTEMPTS { get; }
    int SECONDS_BETWEEN_RETRIES { get; }

    void Validate();
}