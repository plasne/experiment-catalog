using System;
using System.IO;
using NetBricks;

public class Config : IConfig
{
    private readonly NetBricks.IConfig config;

    public Config(NetBricks.IConfig config)
    {
        this.config = config;
        this.PORT = this.config.Get<string>("PORT").AsInt(() => 6030);
        this.CONCURRENCY = this.config.Get<string>("CONCURRENCY").AsInt(() => 4);
        this.AZURE_STORAGE_ACCOUNT_NAME = this.config.Get<string>("AZURE_STORAGE_ACCOUNT_NAME");
        this.INFERENCE_CONTAINER = this.config.Get<string>("INFERENCE_CONTAINER");
        this.EVALUATION_CONTAINER = this.config.Get<string>("EVALUATION_CONTAINER");
        this.INBOUND_INFERENCE_QUEUES = this.config.Get<string>("INBOUND_INFERENCE_QUEUES").AsArray(() => []);
        this.INBOUND_EVALUATION_QUEUES = this.config.Get<string>("INBOUND_EVALUATION_QUEUES").AsArray(() => []);
        this.OUTBOUND_GROUNDTRUTH_QUEUE = this.config.Get<string>("OUTBOUND_GROUNDTRUTH_QUEUE");
        this.OUTBOUND_INFERENCE_QUEUE = this.config.Get<string>("OUTBOUND_INFERENCE_QUEUE");
        this.MS_TO_PAUSE_WHEN_EMPTY = this.config.Get<string>("MS_TO_PAUSE_WHEN_EMPTY").AsInt(() => 500);
        this.DEQUEUE_FOR_X_SECONDS = this.config.Get<string>("DEQUEUE_FOR_X_SECONDS").AsInt(() => 300);
        this.INFERENCE_URL = this.config.Get<string>("INFERENCE_URL");
        this.EVALUATION_URL = this.config.Get<string>("EVALUATION_URL");
        this.MAX_RETRY_ATTEMPTS = config.Get<string>("MAX_RETRY_ATTEMPTS").AsInt(() => 3);
        this.SECONDS_BETWEEN_RETRIES = config.Get<string>("SECONDS_BETWEEN_RETRIES").AsInt(() => 2);

        this.INBOUND_GROUNDTRUTH_TRANSFORM_FILE = this.config.Get<string>("INBOUND_GROUNDTRUTH_TRANSFORM_FILE");
        this.INBOUND_GROUNDTRUTH_TRANSFORM_QUERY = config.Get<string>("INBOUND_GROUNDTRUTH_TRANSFORM_QUERY").AsString(() =>
        {
            return string.IsNullOrEmpty(this.INBOUND_GROUNDTRUTH_TRANSFORM_FILE)
                ? string.Empty
                : File.ReadAllText(this.INBOUND_GROUNDTRUTH_TRANSFORM_FILE);
        });

        this.INBOUND_INFERENCE_TRANSFORM_FILE = this.config.Get<string>("INBOUND_INFERENCE_TRANSFORM_FILE");
        this.INBOUND_INFERENCE_TRANSFORM_QUERY = config.Get<string>("INBOUND_INFERENCE_TRANSFORM_QUERY").AsString(() =>
        {
            return string.IsNullOrEmpty(this.INBOUND_INFERENCE_TRANSFORM_FILE)
                ? string.Empty
                : File.ReadAllText(this.INBOUND_INFERENCE_TRANSFORM_FILE);
        });

        this.INBOUND_EVALUATION_TRANSFORM_FILE = this.config.Get<string>("INBOUND_EVALUATION_TRANSFORM_FILE");
        this.INBOUND_EVALUATION_TRANSFORM_QUERY = config.Get<string>("INBOUND_EVALUATION_TRANSFORM_QUERY").AsString(() =>
        {
            return string.IsNullOrEmpty(this.INBOUND_EVALUATION_TRANSFORM_FILE)
                ? string.Empty
                : File.ReadAllText(this.INBOUND_EVALUATION_TRANSFORM_FILE);
        });

        this.IS_API =
            !string.IsNullOrEmpty(this.OUTBOUND_GROUNDTRUTH_QUEUE);
        this.IS_INFERENCE_PROXY =
            !string.IsNullOrEmpty(this.INFERENCE_CONTAINER)
            && !string.IsNullOrEmpty(this.INFERENCE_URL)
            && this.INBOUND_INFERENCE_QUEUES.Length > 0
            && !string.IsNullOrEmpty(this.OUTBOUND_INFERENCE_QUEUE);
        this.IS_EVALUATION_PROXY =
            !string.IsNullOrEmpty(this.EVALUATION_CONTAINER)
            && !string.IsNullOrEmpty(this.EVALUATION_URL)
            && this.INBOUND_EVALUATION_QUEUES.Length > 0;
    }

    public int PORT { get; }

    public int CONCURRENCY { get; }

    public string AZURE_STORAGE_ACCOUNT_NAME { get; }

    public string INFERENCE_CONTAINER { get; }

    public string EVALUATION_CONTAINER { get; }

    public string[] INBOUND_INFERENCE_QUEUES { get; }

    public string[] INBOUND_EVALUATION_QUEUES { get; }

    public string OUTBOUND_GROUNDTRUTH_QUEUE { get; }

    public string OUTBOUND_INFERENCE_QUEUE { get; }

    public int MS_TO_PAUSE_WHEN_EMPTY { get; }

    public int DEQUEUE_FOR_X_SECONDS { get; }

    public string INFERENCE_URL { get; }

    public string EVALUATION_URL { get; }

    public int MAX_RETRY_ATTEMPTS { get; }

    public int SECONDS_BETWEEN_RETRIES { get; }

    public string INBOUND_GROUNDTRUTH_TRANSFORM_FILE { get; }

    public string INBOUND_INFERENCE_TRANSFORM_FILE { get; }

    public string INBOUND_EVALUATION_TRANSFORM_FILE { get; }

    public string INBOUND_GROUNDTRUTH_TRANSFORM_QUERY { get; }

    public string INBOUND_INFERENCE_TRANSFORM_QUERY { get; }

    public string INBOUND_EVALUATION_TRANSFORM_QUERY { get; }

    public bool IS_API { get; }

    public bool IS_INFERENCE_PROXY { get; }

    public bool IS_EVALUATION_PROXY { get; }

    public void Validate()
    {
        this.config.Require("PORT", this.PORT.ToString());
        this.config.Require("CONCURRENCY", this.CONCURRENCY.ToString());
        this.config.Require("AZURE_STORAGE_ACCOUNT_NAME", this.AZURE_STORAGE_ACCOUNT_NAME);
        this.config.Optional("INFERENCE_CONTAINER", this.INFERENCE_CONTAINER);
        this.config.Optional("EVALUATION_CONTAINER", this.EVALUATION_CONTAINER);
        this.config.Optional("INBOUND_INFERENCE_QUEUES", this.INBOUND_INFERENCE_QUEUES);
        this.config.Optional("INBOUND_EVALUATION_QUEUES", this.INBOUND_EVALUATION_QUEUES);
        this.config.Optional("OUTBOUND_GROUNDTRUTH_QUEUE", this.OUTBOUND_GROUNDTRUTH_QUEUE);
        this.config.Optional("OUTBOUND_INFERENCE_QUEUE", this.OUTBOUND_INFERENCE_QUEUE);
        this.config.Require("MS_TO_PAUSE_WHEN_EMPTY", this.MS_TO_PAUSE_WHEN_EMPTY.ToString());
        this.config.Require("DEQUEUE_FOR_X_SECONDS", this.DEQUEUE_FOR_X_SECONDS.ToString());
        this.config.Optional("INFERENCE_URL", this.INFERENCE_URL);
        this.config.Optional("EVALUATION_URL", this.EVALUATION_URL);
        this.config.Require("MAX_RETRY_ATTEMPTS", this.MAX_RETRY_ATTEMPTS.ToString());
        this.config.Require("SECONDS_BETWEEN_RETRIES", this.SECONDS_BETWEEN_RETRIES.ToString());
        this.config.Optional("INBOUND_GROUNDTRUTH_TRANSFORM_FILE", this.INBOUND_GROUNDTRUTH_TRANSFORM_FILE);
        this.config.Optional("INBOUND_GROUNDTRUTH_TRANSFORM_QUERY", this.INBOUND_GROUNDTRUTH_TRANSFORM_QUERY, hideValue: true);
        this.config.Optional("INBOUND_INFERENCE_TRANSFORM_FILE", this.INBOUND_INFERENCE_TRANSFORM_FILE);
        this.config.Optional("INBOUND_INFERENCE_TRANSFORM_QUERY", this.INBOUND_INFERENCE_TRANSFORM_QUERY, hideValue: true);
        this.config.Optional("INBOUND_EVALUATION_TRANSFORM_FILE", this.INBOUND_EVALUATION_TRANSFORM_FILE);
        this.config.Optional("INBOUND_EVALUATION_TRANSFORM_QUERY", this.INBOUND_EVALUATION_TRANSFORM_QUERY, hideValue: true);
        this.config.Optional("IS_API", this.IS_API.ToString());
        this.config.Optional("IS_INFERENCE_PROXY", this.IS_INFERENCE_PROXY.ToString());
        this.config.Optional("IS_EVALUATION_PROXY", this.IS_EVALUATION_PROXY.ToString());
        if (!this.IS_API && !this.IS_INFERENCE_PROXY && !this.IS_EVALUATION_PROXY)
        {
            throw new Exception("The application must be configured for at least one role of API, Inference Proxy, or Evaluation Proxy.");
        }
    }
}