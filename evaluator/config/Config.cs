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
        this.AZURE_STORAGE_INFERENCE_CONTAINER_NAME = this.config.Get<string>("AZURE_STORAGE_INFERENCE_CONTAINER_NAME");
        this.AZURE_STORAGE_EVALUATION_CONTAINER_NAME = this.config.Get<string>("AZURE_STORAGE_EVALUATION_CONTAINER_NAME");
        this.MAX_DURATION_TO_RUN_EVALUATIONS = this.config.Get<string>("MAX_DURATION_TO_RUN_EVALUATIONS").AsDuration(() => Duration.FromHours(24));
        this.MAX_DURATION_TO_VIEW_RESULTS = this.config.Get<string>("MAX_DURATION_TO_VIEW_RESULTS").AsDuration(() => Duration.FromYears(1));
        this.CONCURRENCY = this.config.Get<string>("CONCURRENCY").AsInt(() => 4);
        this.AZURE_STORAGE_ACCOUNT_NAME = this.config.Get<string>("AZURE_STORAGE_ACCOUNT_NAME");
        this.INBOUND_QUEUES = this.config.Get<string>("INBOUND_QUEUES").AsArray(() => []);
        this.OUTBOUND_QUEUE = this.config.Get<string>("OUTBOUND_QUEUE");
        this.MS_TO_PAUSE_WHEN_EMPTY = this.config.Get<string>("MS_TO_PAUSE_WHEN_EMPTY").AsInt(() => 500);
        this.DEQUEUE_FOR_X_SECONDS = this.config.Get<string>("DEQUEUE_FOR_X_SECONDS").AsInt(() => 300);
    }

    public int PORT { get; }

    public string AZURE_STORAGE_ACCOUNT_ID { get; }

    public string AZURE_STORAGE_INFERENCE_CONTAINER_NAME { get; }

    public string AZURE_STORAGE_EVALUATION_CONTAINER_NAME { get; }

    public Duration MAX_DURATION_TO_RUN_EVALUATIONS { get; }

    public Duration MAX_DURATION_TO_VIEW_RESULTS { get; }

    public int CONCURRENCY { get; }

    public string AZURE_STORAGE_ACCOUNT_NAME { get; }

    public string[] INBOUND_QUEUES { get; }

    public string OUTBOUND_QUEUE { get; }

    public int MS_TO_PAUSE_WHEN_EMPTY { get; }

    public int DEQUEUE_FOR_X_SECONDS { get; }

    public void Validate()
    {
        this.config.Require("PORT", this.PORT.ToString());
        this.config.Require("AZURE_STORAGE_ACCOUNT_ID", this.AZURE_STORAGE_ACCOUNT_ID);
        this.config.Require("AZURE_STORAGE_INFERENCE_CONTAINER_NAME", this.AZURE_STORAGE_INFERENCE_CONTAINER_NAME);
        this.config.Require("AZURE_STORAGE_EVALUATION_CONTAINER_NAME", this.AZURE_STORAGE_EVALUATION_CONTAINER_NAME);
        this.config.Require("MAX_DURATION_TO_RUN_EVALUATIONS", this.MAX_DURATION_TO_RUN_EVALUATIONS.ToString());
        this.config.Require("MAX_DURATION_TO_VIEW_RESULTS", this.MAX_DURATION_TO_VIEW_RESULTS.ToString());
        this.config.Require("CONCURRENCY", this.CONCURRENCY.ToString());
        this.config.Require("AZURE_STORAGE_ACCOUNT_NAME", this.AZURE_STORAGE_ACCOUNT_NAME);
        this.config.Optional("INBOUND_QUEUES", this.INBOUND_QUEUES);
        this.config.Optional("OUTBOUND_QUEUE", this.OUTBOUND_QUEUE);
        this.config.Require("MS_TO_PAUSE_WHEN_EMPTY", this.MS_TO_PAUSE_WHEN_EMPTY.ToString());
        this.config.Require("DEQUEUE_FOR_X_SECONDS", this.DEQUEUE_FOR_X_SECONDS.ToString());
    }
}