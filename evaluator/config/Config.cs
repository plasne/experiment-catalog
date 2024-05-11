using System;
using System.IO;
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
        this.INBOUND_STAGE = this.config.Get<string>("INBOUND_STAGE").AsEnum(() => Stages.Unknown);
        this.OUTBOUND_STAGE = this.config.Get<string>("OUTBOUND_STAGE").AsEnum(() => Stages.Unknown);
        this.PROCESSING_URL = this.config.Get<string>("PROCESSING_URL");
        this.MAX_RETRY_ATTEMPTS = config.Get<string>("MAX_RETRY_ATTEMPTS").AsInt(() => 3);
        this.SECONDS_BETWEEN_RETRIES = config.Get<string>("SECONDS_BETWEEN_RETRIES").AsInt(() => 2);
        this.PATH_TO_TRANSFORM_QUERY = config.Get<string>("PATH_TO_TRANSFORM_QUERY");
        this.TRANSFORM_QUERY = config.Get<string>("TRANSFORM_QUERY").AsString(() =>
        {
            return string.IsNullOrEmpty(this.PATH_TO_TRANSFORM_QUERY)
                ? string.Empty
                : File.ReadAllText(this.PATH_TO_TRANSFORM_QUERY);
        });
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

    public Stages INBOUND_STAGE { get; }

    public Stages OUTBOUND_STAGE { get; }

    public string PROCESSING_URL { get; }

    public int MAX_RETRY_ATTEMPTS { get; }

    public int SECONDS_BETWEEN_RETRIES { get; }

    public string PATH_TO_TRANSFORM_QUERY { get; }

    public string TRANSFORM_QUERY { get; }

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

        this.config.Require("INBOUND_STAGE", this.INBOUND_STAGE.ToString());
        this.config.Require("OUTBOUND_STAGE", this.OUTBOUND_STAGE.ToString());
        if (this.INBOUND_STAGE == Stages.Unknown || this.OUTBOUND_STAGE == Stages.Unknown)
        {
            throw new Exception("INBOUND_STAGE and OUTBOUND_STAGE must each be set to GroundTruth, Inference, or Evaluation.");
        }

        this.config.Require("PROCESSING_URL", this.PROCESSING_URL);
        this.config.Require("MAX_RETRY_ATTEMPTS", this.MAX_RETRY_ATTEMPTS.ToString());
        this.config.Require("SECONDS_BETWEEN_RETRIES", this.SECONDS_BETWEEN_RETRIES.ToString());
        this.config.Optional("PATH_TO_TRANSFORM_QUERY", this.PATH_TO_TRANSFORM_QUERY);
        this.config.Optional("TRANSFORM_QUERY", this.TRANSFORM_QUERY, hideValue: true);
    }
}