using Iso8601DurationHelper;

public interface IConfig
{
    int PORT { get; }
    string AZURE_STORAGE_ACCOUNT_ID { get; }
    string AZURE_STORAGE_GROUNDTRUTH_CONTAINER_NAME { get; }
    string AZURE_STORAGE_INFERENCE_CONTAINER_NAME { get; }
    string AZURE_STORAGE_EVALUATION_CONTAINER_NAME { get; }
    Duration MAX_DURATION_TO_RUN_EVALUATIONS { get; }
    Duration MAX_DURATION_TO_VIEW_RESULTS { get; }
    int CONCURRENCY { get; }

    void Validate();
}