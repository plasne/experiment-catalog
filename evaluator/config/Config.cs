using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetBricks;

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

        this.AZURE_STORAGE_ACCOUNT_NAME = this.config.Get<string>("AZURE_STORAGE_ACCOUNT_NAME");
        this.INFERENCE_CONTAINER = this.config.Get<string>("INFERENCE_CONTAINER");
        this.EVALUATION_CONTAINER = this.config.Get<string>("EVALUATION_CONTAINER");
        this.INBOUND_INFERENCE_QUEUES = this.config.Get<string>("INBOUND_INFERENCE_QUEUES").AsArray(() => []);
        this.INBOUND_EVALUATION_QUEUES = this.config.Get<string>("INBOUND_EVALUATION_QUEUES").AsArray(() => []);
        this.OUTBOUND_INFERENCE_QUEUE = this.config.Get<string>("OUTBOUND_INFERENCE_QUEUE");
        this.MS_TO_PAUSE_WHEN_EMPTY = this.config.Get<string>("MS_TO_PAUSE_WHEN_EMPTY").AsInt(() => 500);
        this.DEQUEUE_FOR_X_SECONDS = this.config.Get<string>("DEQUEUE_FOR_X_SECONDS").AsInt(() => 300);
        this.INFERENCE_URL = this.config.Get<string>("INFERENCE_URL");
        this.EVALUATION_URL = this.config.Get<string>("EVALUATION_URL");
        this.EXPERIMENT_CATALOG_BASE_URL = this.config.Get<string>("EXPERIMENT_CATALOG_BASE_URL");

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
    }

    public int PORT { get; }

    public List<Roles> ROLES { get; }

    public string AZURE_STORAGE_ACCOUNT_NAME { get; }

    public string INFERENCE_CONTAINER { get; }

    public string EVALUATION_CONTAINER { get; }

    public string[] INBOUND_INFERENCE_QUEUES { get; }

    public string[] INBOUND_EVALUATION_QUEUES { get; }

    public string OUTBOUND_INFERENCE_QUEUE { get; }

    public int MS_TO_PAUSE_WHEN_EMPTY { get; }

    public int DEQUEUE_FOR_X_SECONDS { get; }

    public string INFERENCE_URL { get; }

    public string EVALUATION_URL { get; }

    public string EXPERIMENT_CATALOG_BASE_URL { get; }

    public string INBOUND_GROUNDTRUTH_TRANSFORM_FILE { get; }

    public string INBOUND_INFERENCE_TRANSFORM_FILE { get; }

    public string INBOUND_EVALUATION_TRANSFORM_FILE { get; }

    public string INBOUND_GROUNDTRUTH_TRANSFORM_QUERY { get; }

    public string INBOUND_INFERENCE_TRANSFORM_QUERY { get; }

    public string INBOUND_EVALUATION_TRANSFORM_QUERY { get; }

    public void Validate()
    {
        // required
        this.config.Require("PORT", this.PORT.ToString());
        this.config.Require("ROLES", this.ROLES.Select(r => r.ToString()).ToArray());
        this.config.Require("AZURE_STORAGE_ACCOUNT_NAME", this.AZURE_STORAGE_ACCOUNT_NAME);
        this.config.Require("MS_TO_PAUSE_WHEN_EMPTY", this.MS_TO_PAUSE_WHEN_EMPTY.ToString());
        this.config.Require("DEQUEUE_FOR_X_SECONDS", this.DEQUEUE_FOR_X_SECONDS.ToString());

        // API-specific
        if (this.ROLES.Contains(Roles.API))
        {
            this.config.Optional("INBOUND_GROUNDTRUTH_TRANSFORM_FILE", this.INBOUND_GROUNDTRUTH_TRANSFORM_FILE);
            this.config.Optional("INBOUND_GROUNDTRUTH_TRANSFORM_QUERY", this.INBOUND_GROUNDTRUTH_TRANSFORM_QUERY, hideValue: true);
        }

        // InferenceProxy-specific
        if (this.ROLES.Contains(Roles.InferenceProxy))
        {
            this.config.Require("INFERENCE_CONTAINER", this.INFERENCE_CONTAINER);
            this.config.Require("INFERENCE_URL", this.INFERENCE_URL);
            this.config.Require("INBOUND_INFERENCE_QUEUES", this.INBOUND_INFERENCE_QUEUES);
            if (this.INBOUND_INFERENCE_QUEUES.Length == 0)
            {
                throw new Exception("When configured for the InferenceProxy role, INBOUND_INFERENCE_QUEUES must be specified.");
            }
            this.config.Require("OUTBOUND_INFERENCE_QUEUE", this.OUTBOUND_INFERENCE_QUEUE);
        }

        // EvaluationProxy-specific
        if (this.ROLES.Contains(Roles.EvaluationProxy))
        {
            this.config.Optional("INFERENCE_CONTAINER", this.INFERENCE_CONTAINER);
            this.config.Require("EVALUATION_CONTAINER", this.EVALUATION_CONTAINER);
            this.config.Require("EVALUATION_URL", this.EVALUATION_URL);
            this.config.Require("INBOUND_EVALUATION_QUEUES", this.INBOUND_EVALUATION_QUEUES);
            if (this.INBOUND_EVALUATION_QUEUES.Length == 0)
            {
                throw new Exception("When configured for the EvaluationProxy role, INBOUND_EVALUATION_QUEUES must be specified.");
            }
        }

        // any proxy
        if (this.ROLES.Contains(Roles.InferenceProxy) || this.ROLES.Contains(Roles.EvaluationProxy))
        {
            this.config.Optional("EXPERIMENT_CATALOG_BASE_URL", this.EXPERIMENT_CATALOG_BASE_URL);
            this.config.Optional("INBOUND_INFERENCE_TRANSFORM_FILE", this.INBOUND_INFERENCE_TRANSFORM_FILE);
            this.config.Optional("INBOUND_INFERENCE_TRANSFORM_QUERY", this.INBOUND_INFERENCE_TRANSFORM_QUERY, hideValue: true);
            this.config.Optional("INBOUND_EVALUATION_TRANSFORM_FILE", this.INBOUND_EVALUATION_TRANSFORM_FILE);
            this.config.Optional("INBOUND_EVALUATION_TRANSFORM_QUERY", this.INBOUND_EVALUATION_TRANSFORM_QUERY, hideValue: true);
        }
    }
}