using Iso8601DurationHelper;
using NetBricks;

public class Config : IConfig
{
    private readonly NetBricks.IConfig config;

    public Config(NetBricks.IConfig config)
    {
        this.config = config;
        this.PORT = this.config.Get<string>("PORT").AsInt(() => 6030);
        this.AZURE_STORAGE_ACCOUNT_ID = this.config.Get<string>("AZURE_STORAGE_ACCOUNT_ID");
        this.AZURE_STORAGE_GROUNDTRUTH_CONTAINER_NAME = this.config.Get<string>("AZURE_STORAGE_GROUNDTRUTH_CONTAINER_NAME");
        this.AZURE_STORAGE_INFERENCE_CONTAINER_NAME = this.config.Get<string>("AZURE_STORAGE_INFERENCE_CONTAINER_NAME");
        this.AZURE_STORAGE_EVALUATION_CONTAINER_NAME = this.config.Get<string>("AZURE_STORAGE_EVALUATION_CONTAINER_NAME");
        this.MAX_DURATION_TO_RUN_EVALUATIONS = this.config.Get<string>("MAX_DURATION_TO_RUN_EVALUATIONS").AsDuration(() => Duration.FromHours(24));
        this.MAX_DURATION_TO_VIEW_RESULTS = this.config.Get<string>("MAX_DURATION_TO_VIEW_RESULTS").AsDuration(() => Duration.FromYears(1));
        this.CONCURRENCY = this.config.Get<string>("CONCURRENCY").AsInt(() => 4);
    }

    public int PORT { get; }

    public string AZURE_STORAGE_ACCOUNT_ID { get; }

    public string AZURE_STORAGE_GROUNDTRUTH_CONTAINER_NAME { get; }

    public string AZURE_STORAGE_INFERENCE_CONTAINER_NAME { get; }

    public string AZURE_STORAGE_EVALUATION_CONTAINER_NAME { get; }

    public Duration MAX_DURATION_TO_RUN_EVALUATIONS { get; }

    public Duration MAX_DURATION_TO_VIEW_RESULTS { get; }

    public int CONCURRENCY { get; }

    public void Validate()
    {
        this.config.Require("PORT", this.PORT.ToString());
        this.config.Require("AZURE_STORAGE_ACCOUNT_ID", this.AZURE_STORAGE_ACCOUNT_ID);
        this.config.Require("AZURE_STORAGE_GROUNDTRUTH_CONTAINER_NAME", this.AZURE_STORAGE_GROUNDTRUTH_CONTAINER_NAME);
        this.config.Require("AZURE_STORAGE_INFERENCE_CONTAINER_NAME", this.AZURE_STORAGE_INFERENCE_CONTAINER_NAME);
        this.config.Require("AZURE_STORAGE_EVALUATION_CONTAINER_NAME", this.AZURE_STORAGE_EVALUATION_CONTAINER_NAME);
        this.config.Require("MAX_DURATION_TO_RUN_EVALUATIONS", this.MAX_DURATION_TO_RUN_EVALUATIONS.ToString());
        this.config.Require("MAX_DURATION_TO_VIEW_RESULTS", this.MAX_DURATION_TO_VIEW_RESULTS.ToString());
        this.config.Require("CONCURRENCY", this.CONCURRENCY.ToString());
    }
}