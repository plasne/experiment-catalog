using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetBricks;

namespace Evaluator;

public class Config : IConfig
{
    private readonly NetBricks.IConfig config;

    public Config(NetBricks.IConfig config)
    {
        this.config = config;
        this.PORT = this.config.Get<string>("PORT").AsInt(() => 6030);

        this.ROLES = this.config.Get("ROLES", value =>
        {
            List<Roles> roles = [];
            var list = value.AsArray(() => throw new Exception("ROLES must be an array of strings"));
            foreach (var entry in list)
            {
                var role = entry.AsEnum<Roles>(() => throw new Exception("each ROLE must be one of API, InferenceProxy, or EvaluationProxy."));
                roles.Add(role);
            }
            return roles;
        });

        this.OPEN_TELEMETRY_CONNECTION_STRING = this.config.GetSecret<string>("OPEN_TELEMETRY_CONNECTION_STRING").Result;
        this.AZURE_STORAGE_ACCOUNT_NAME = this.config.Get<string>("AZURE_STORAGE_ACCOUNT_NAME");
        this.AZURE_STORAGE_CONNECTION_STRING = this.config.GetSecret<string>("AZURE_STORAGE_CONNECTION_STRING").Result;
        this.INFERENCE_CONTAINER = this.config.Get<string>("INFERENCE_CONTAINER");
        this.EVALUATION_CONTAINER = this.config.Get<string>("EVALUATION_CONTAINER");
        this.INBOUND_INFERENCE_QUEUES = this.config.Get<string>("INBOUND_INFERENCE_QUEUES").AsArray(() => []);
        this.INBOUND_EVALUATION_QUEUES = this.config.Get<string>("INBOUND_EVALUATION_QUEUES").AsArray(() => []);
        this.OUTBOUND_INFERENCE_QUEUE = this.config.Get<string>("OUTBOUND_INFERENCE_QUEUE");
        this.INFERENCE_CONCURRENCY = this.config.Get<string>("INFERENCE_CONCURRENCY, CONCURRENCY").AsInt(() => 1);
        this.EVALUATION_CONCURRENCY = this.config.Get<string>("EVALUATION_CONCURRENCY, CONCURRENCY").AsInt(() => 1);
        this.MS_TO_PAUSE_WHEN_EMPTY = this.config.Get<string>("MS_TO_PAUSE_WHEN_EMPTY").AsInt(() => 500);
        this.DEQUEUE_FOR_X_SECONDS = this.config.Get<string>("DEQUEUE_FOR_X_SECONDS").AsInt(() => 300);
        this.MS_BETWEEN_DEQUEUE = this.config.Get<string>("MS_BETWEEN_DEQUEUE").AsInt(() => 0);
        this.MS_BETWEEN_DEQUEUE_CURRENT = this.MS_BETWEEN_DEQUEUE;
        this.MAX_ATTEMPTS_TO_DEQUEUE = this.config.Get<string>("MAX_ATTEMPTS_TO_DEQUEUE").AsInt(() => 5);
        this.MS_TO_ADD_ON_BUSY = this.config.Get<string>("MS_TO_ADD_ON_BUSY").AsInt(() => 0);
        this.MINUTES_BETWEEN_RESTORE_AFTER_BUSY = this.config.Get<string>("MINUTES_BETWEEN_RESTORE_AFTER_BUSY").AsInt(() => 0);
        this.INFERENCE_URL = this.config.Get<string>("INFERENCE_URL");
        this.EVALUATION_URL = this.config.Get<string>("EVALUATION_URL");
        this.SECONDS_BEFORE_TIMEOUT_FOR_PROCESSING = this.config.Get<string>("SECONDS_BEFORE_TIMEOUT_FOR_PROCESSING").AsInt(() => 300);
        this.BACKOFF_ON_STATUS_CODES = this.config.Get<string>("BACKOFF_ON_STATUS_CODES").AsIntArray(() => [429]);
        this.DEADLETTER_ON_STATUS_CODES = this.config.Get<string>("DEADLETTER_ON_STATUS_CODES").AsIntArray(() => [400, 401, 403, 404, 405]);
        this.EXPERIMENT_CATALOG_BASE_URL = this.config.Get<string>("EXPERIMENT_CATALOG_BASE_URL");

        this.INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_FILE = this.config.Get<string>("INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_FILE, INBOUND_GROUNDTRUTH_TRANSFORM_FILE");
        this.INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_QUERY = config.Get<string>("INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_QUERY, INBOUND_GROUNDTRUTH_TRANSFORM_QUERY").AsString(() =>
        {
            return string.IsNullOrEmpty(this.INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_FILE)
                ? string.Empty
                : File.ReadAllText(this.INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_FILE);
        });

        this.INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_FILE = this.config.Get<string>("INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_FILE, INBOUND_GROUNDTRUTH_TRANSFORM_FILE");
        this.INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_QUERY = config.Get<string>("INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_QUERY, INBOUND_GROUNDTRUTH_TRANSFORM_QUERY").AsString(() =>
        {
            return string.IsNullOrEmpty(this.INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_FILE)
                ? string.Empty
                : File.ReadAllText(this.INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_FILE);
        });

        this.INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_FILE = this.config.Get<string>("INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_FILE, INBOUND_GROUNDTRUTH_TRANSFORM_FILE");
        this.INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_QUERY = config.Get<string>("INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_QUERY, INBOUND_GROUNDTRUTH_TRANSFORM_QUERY").AsString(() =>
        {
            return string.IsNullOrEmpty(this.INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_FILE)
                ? string.Empty
                : File.ReadAllText(this.INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_FILE);
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
    }

    public int PORT { get; }

    public List<Roles> ROLES { get; }

    public string OPEN_TELEMETRY_CONNECTION_STRING { get; }

    public string AZURE_STORAGE_ACCOUNT_NAME { get; }

    public string AZURE_STORAGE_CONNECTION_STRING { get; }

    public string INFERENCE_CONTAINER { get; }

    public string EVALUATION_CONTAINER { get; }

    public string[] INBOUND_INFERENCE_QUEUES { get; }

    public string[] INBOUND_EVALUATION_QUEUES { get; }

    public string OUTBOUND_INFERENCE_QUEUE { get; }

    public int INFERENCE_CONCURRENCY { get; }

    public int EVALUATION_CONCURRENCY { get; }

    public int MS_TO_PAUSE_WHEN_EMPTY { get; }

    public int DEQUEUE_FOR_X_SECONDS { get; }

    public int MS_BETWEEN_DEQUEUE { get; }

    public int MS_BETWEEN_DEQUEUE_CURRENT { get; set; }

    public int MAX_ATTEMPTS_TO_DEQUEUE { get; }

    public int MS_TO_ADD_ON_BUSY { get; }

    public int MINUTES_BETWEEN_RESTORE_AFTER_BUSY { get; }

    public string INFERENCE_URL { get; }

    public string EVALUATION_URL { get; }

    public int SECONDS_BEFORE_TIMEOUT_FOR_PROCESSING { get; }

    public int[] BACKOFF_ON_STATUS_CODES { get; }

    public int[] DEADLETTER_ON_STATUS_CODES { get; }

    public string EXPERIMENT_CATALOG_BASE_URL { get; }

    public string INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_FILE { get; }

    public string INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_QUERY { get; }

    public string INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_FILE { get; }

    public string INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_QUERY { get; }

    public string INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_FILE { get; }

    public string INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_QUERY { get; }

    public string INBOUND_INFERENCE_TRANSFORM_FILE { get; }

    public string INBOUND_INFERENCE_TRANSFORM_QUERY { get; }

    public string INBOUND_EVALUATION_TRANSFORM_FILE { get; }

    public string INBOUND_EVALUATION_TRANSFORM_QUERY { get; }

    public void Validate()
    {
        // applies regardless of role
        this.config.Require("PORT", this.PORT.ToString());
        this.config.Require("ROLES", this.ROLES.Select(r => r.ToString()).ToArray());
        this.config.Require("OPEN_TELEMETRY_CONNECTION_STRING", OPEN_TELEMETRY_CONNECTION_STRING, hideValue: true);

        this.config.Optional("AZURE_STORAGE_ACCOUNT_NAME", this.AZURE_STORAGE_ACCOUNT_NAME);
        this.config.Optional("AZURE_STORAGE_CONNECTION_STRING", this.AZURE_STORAGE_CONNECTION_STRING, hideValue: true);
        if (string.IsNullOrEmpty(this.AZURE_STORAGE_ACCOUNT_NAME) && string.IsNullOrEmpty(this.AZURE_STORAGE_CONNECTION_STRING))
        {
            throw new Exception("Either AZURE_STORAGE_ACCOUNT_NAME or AZURE_STORAGE_CONNECTION_STRING must be specified.");
        }

        // API-specific
        if (this.ROLES.Contains(Roles.API))
        {
            this.config.Optional("INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_FILE", this.INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_FILE);
            this.config.Optional("INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_QUERY", this.INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_QUERY, hideValue: true);
        }

        // InferenceProxy-specific
        if (this.ROLES.Contains(Roles.InferenceProxy))
        {
            this.config.Require("INFERENCE_CONCURRENCY", this.INFERENCE_CONCURRENCY);
            this.config.Require("INFERENCE_CONTAINER", this.INFERENCE_CONTAINER);
            this.config.Require("INFERENCE_URL", this.INFERENCE_URL);
            this.config.Require("INBOUND_INFERENCE_QUEUES", this.INBOUND_INFERENCE_QUEUES);
            if (this.INBOUND_INFERENCE_QUEUES.Length == 0)
            {
                throw new Exception("When configured for the InferenceProxy role, INBOUND_INFERENCE_QUEUES must be specified.");
            }
            this.config.Require("OUTBOUND_INFERENCE_QUEUE", this.OUTBOUND_INFERENCE_QUEUE);
            this.config.Optional("INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_FILE", this.INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_FILE);
            this.config.Optional("INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_QUERY", this.INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_QUERY, hideValue: true);
            this.config.Optional("INBOUND_INFERENCE_TRANSFORM_FILE", this.INBOUND_INFERENCE_TRANSFORM_FILE);
            this.config.Optional("INBOUND_INFERENCE_TRANSFORM_QUERY", this.INBOUND_INFERENCE_TRANSFORM_QUERY, hideValue: true);
        }

        // EvaluationProxy-specific
        if (this.ROLES.Contains(Roles.EvaluationProxy))
        {
            this.config.Require("EVALUATION_CONCURRENCY", this.EVALUATION_CONCURRENCY);
            this.config.Require("INFERENCE_CONTAINER", this.INFERENCE_CONTAINER);
            this.config.Require("EVALUATION_CONTAINER", this.EVALUATION_CONTAINER);
            this.config.Require("EVALUATION_URL", this.EVALUATION_URL);
            this.config.Require("INBOUND_EVALUATION_QUEUES", this.INBOUND_EVALUATION_QUEUES);
            if (this.INBOUND_EVALUATION_QUEUES.Length == 0)
            {
                throw new Exception("When configured for the EvaluationProxy role, INBOUND_EVALUATION_QUEUES must be specified.");
            }
            this.config.Optional("INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_FILE", this.INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_FILE);
            this.config.Optional("INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_QUERY", this.INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_QUERY, hideValue: true);
            this.config.Optional("INBOUND_EVALUATION_TRANSFORM_FILE", this.INBOUND_EVALUATION_TRANSFORM_FILE);
            this.config.Optional("INBOUND_EVALUATION_TRANSFORM_QUERY", this.INBOUND_EVALUATION_TRANSFORM_QUERY, hideValue: true);
        }

        // any proxy
        if (this.ROLES.Contains(Roles.InferenceProxy) || this.ROLES.Contains(Roles.EvaluationProxy))
        {
            this.config.Require("SECONDS_BEFORE_TIMEOUT_FOR_PROCESSING", this.SECONDS_BEFORE_TIMEOUT_FOR_PROCESSING);
            this.config.Require("BACKOFF_ON_STATUS_CODES", this.BACKOFF_ON_STATUS_CODES.Select(c => c.ToString()).ToArray());
            this.config.Require("DEADLETTER_ON_STATUS_CODES", this.DEADLETTER_ON_STATUS_CODES.Select(c => c.ToString()).ToArray());
            this.config.Require("MAX_ATTEMPTS_TO_DEQUEUE", this.MAX_ATTEMPTS_TO_DEQUEUE.ToString());
            this.config.Require("MS_TO_PAUSE_WHEN_EMPTY", this.MS_TO_PAUSE_WHEN_EMPTY.ToString());
            this.config.Require("DEQUEUE_FOR_X_SECONDS", this.DEQUEUE_FOR_X_SECONDS.ToString());
            this.config.Require("MS_BETWEEN_DEQUEUE", this.MS_BETWEEN_DEQUEUE.ToString());
            this.config.Require("MS_TO_ADD_ON_BUSY", this.MS_TO_ADD_ON_BUSY.ToString());
            this.config.Require("MINUTES_BETWEEN_RESTORE_AFTER_BUSY", this.MINUTES_BETWEEN_RESTORE_AFTER_BUSY.ToString());
            this.config.Optional("EXPERIMENT_CATALOG_BASE_URL", this.EXPERIMENT_CATALOG_BASE_URL);
        }
    }
}