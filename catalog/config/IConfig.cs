namespace Catalog;

public interface IConfig
{
    int PORT { get; }
    string OPEN_TELEMETRY_CONNECTION_STRING { get; }
    string AZURE_STORAGE_ACCOUNT_NAME { get; }
    string AZURE_STORAGE_ACCOUNT_CONNSTRING { get; }
    int CONCURRENCY { get; }
    int REQUIRED_BLOCK_SIZE_IN_KB_FOR_OPTIMIZE { get; }
    int MINUTES_TO_BE_IDLE { get; }
    int MINUTES_TO_BE_RECENT { get; }
    int CALC_PVALUES_USING_X_SAMPLES { get; }
    int CALC_PVALUES_EVERY_X_MINUTES { get; }
    int MIN_ITERATIONS_TO_CALC_PVALUES { get; }
    decimal CONFIDENCE_LEVEL { get; }
    int PRECISION_FOR_CALC_VALUES { get; }
    string PATH_TEMPLATE { get; }
    string AZURE_STORAGE_ACCOUNT_NAME_FOR_SUPPORT_DOCS { get; }
    string AZURE_STORAGE_ACCOUNT_CONNSTRING_FOR_SUPPORT_DOCS { get; }
    string? AZURE_STORAGE_CACHE_FOLDER { get; }
    int AZURE_STORAGE_CACHE_MAX_AGE_IN_HOURS { get; }
    int AZURE_STORAGE_OPTIMIZE_EVERY_X_MINUTES { get; }
    int AZURE_STORAGE_CACHE_CLEANUP_EVERY_X_MINUTES { get; }
    bool ENABLE_ANONYMOUS_DOWNLOAD { get; }
    string[] TEST_PROJECTS { get; }

    void Validate();
}