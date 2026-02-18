using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using NetBricks;

namespace Evaluator;

[LogConfig("Configuration:")]
public class Config : IConfig, IValidatableObject
{
    private readonly List<string> invalidRoles = [];

    [SetValue("PORT")]
    public int PORT { get; set; } = 6030;

    [SetValue("ROLES")]
    [LogConfig(mode: LogConfigMode.Never)]
    public string[]? ROLES_RAW { get; set; }

    public List<Roles> ROLES { get; set; } = [];

    [SetValue("OPEN_TELEMETRY_CONNECTION_STRING")]
    [ResolveSecret]
    [LogConfig(mode: LogConfigMode.Masked)]
    public string? OPEN_TELEMETRY_CONNECTION_STRING { get; set; }

    [SetValue("AZURE_STORAGE_ACCOUNT_NAME")]
    public string? AZURE_STORAGE_ACCOUNT_NAME { get; set; }

    [SetValue("AZURE_STORAGE_CONNECTION_STRING")]
    [ResolveSecret]
    [LogConfig(mode: LogConfigMode.Masked)]
    public string? AZURE_STORAGE_CONNECTION_STRING { get; set; }

    [SetValue("INFERENCE_CONTAINER")]
    public string? INFERENCE_CONTAINER { get; set; }

    [SetValue("EVALUATION_CONTAINER")]
    public string? EVALUATION_CONTAINER { get; set; }

    [SetValue("INBOUND_INFERENCE_QUEUES")]
    public string[] INBOUND_INFERENCE_QUEUES { get; set; } = [];

    [SetValue("INBOUND_EVALUATION_QUEUES")]
    public string[] INBOUND_EVALUATION_QUEUES { get; set; } = [];

    [SetValue("OUTBOUND_INFERENCE_QUEUE")]
    public string? OUTBOUND_INFERENCE_QUEUE { get; set; }

    [SetValue("INFERENCE_CONCURRENCY", "CONCURRENCY")]
    [Range(1, 100)]
    public int INFERENCE_CONCURRENCY { get; set; } = 1;

    [SetValue("EVALUATION_CONCURRENCY", "CONCURRENCY")]
    [Range(1, 100)]
    public int EVALUATION_CONCURRENCY { get; set; } = 1;

    [SetValue("MS_TO_PAUSE_WHEN_EMPTY")]
    public int MS_TO_PAUSE_WHEN_EMPTY { get; set; } = 500;

    [SetValue("DEQUEUE_FOR_X_SECONDS")]
    public int DEQUEUE_FOR_X_SECONDS { get; set; } = 300;

    [SetValue("MS_BETWEEN_DEQUEUE")]
    public int MS_BETWEEN_DEQUEUE { get; set; } = 0;

    public int MS_BETWEEN_DEQUEUE_CURRENT { get; set; }

    [SetValue("MAX_ATTEMPTS_TO_DEQUEUE")]
    public int MAX_ATTEMPTS_TO_DEQUEUE { get; set; } = 5;

    [SetValue("MS_TO_ADD_ON_BUSY")]
    public int MS_TO_ADD_ON_BUSY { get; set; } = 0;

    [SetValue("MINUTES_BETWEEN_RESTORE_AFTER_BUSY")]
    public int MINUTES_BETWEEN_RESTORE_AFTER_BUSY { get; set; } = 0;

    [SetValue("INFERENCE_URL")]
    public string? INFERENCE_URL { get; set; }

    [SetValue("EVALUATION_URL")]
    public string? EVALUATION_URL { get; set; }

    [SetValue("SECONDS_BEFORE_TIMEOUT_FOR_PROCESSING")]
    public int SECONDS_BEFORE_TIMEOUT_FOR_PROCESSING { get; set; } = 300;

    [SetValue("BACKOFF_ON_STATUS_CODES")]
    [LogConfig(mode: LogConfigMode.Never)]
    public string[]? BACKOFF_ON_STATUS_CODES_RAW { get; set; }

    public int[] BACKOFF_ON_STATUS_CODES { get; set; } = [429];

    [SetValue("DEADLETTER_ON_STATUS_CODES")]
    [LogConfig(mode: LogConfigMode.Never)]
    public string[]? DEADLETTER_ON_STATUS_CODES_RAW { get; set; }

    public int[] DEADLETTER_ON_STATUS_CODES { get; set; } = [400, 401, 403, 404, 405];

    [SetValue("EXPERIMENT_CATALOG_BASE_URL")]
    public string? EXPERIMENT_CATALOG_BASE_URL { get; set; }

    [SetValue("INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_FILE", "INBOUND_GROUNDTRUTH_TRANSFORM_FILE")]
    public string? INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_FILE { get; set; }

    [SetValue("INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_QUERY", "INBOUND_GROUNDTRUTH_TRANSFORM_QUERY")]
    public string? INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_QUERY { get; set; }

    [SetValue("INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_FILE", "INBOUND_GROUNDTRUTH_TRANSFORM_FILE")]
    public string? INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_FILE { get; set; }

    [SetValue("INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_QUERY", "INBOUND_GROUNDTRUTH_TRANSFORM_QUERY")]
    public string? INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_QUERY { get; set; }

    [SetValue("INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_FILE", "INBOUND_GROUNDTRUTH_TRANSFORM_FILE")]
    public string? INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_FILE { get; set; }

    [SetValue("INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_QUERY", "INBOUND_GROUNDTRUTH_TRANSFORM_QUERY")]
    public string? INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_QUERY { get; set; }

    [SetValue("INBOUND_INFERENCE_TRANSFORM_FILE")]
    public string? INBOUND_INFERENCE_TRANSFORM_FILE { get; set; }

    [SetValue("INBOUND_INFERENCE_TRANSFORM_QUERY")]
    public string? INBOUND_INFERENCE_TRANSFORM_QUERY { get; set; }

    [SetValue("INBOUND_EVALUATION_TRANSFORM_FILE")]
    public string? INBOUND_EVALUATION_TRANSFORM_FILE { get; set; }

    [SetValue("INBOUND_EVALUATION_TRANSFORM_QUERY")]
    public string? INBOUND_EVALUATION_TRANSFORM_QUERY { get; set; }

    [SetValue("PROCESS_METRICS_IN_INFERENCE_RESPONSE")]
    public bool PROCESS_METRICS_IN_INFERENCE_RESPONSE { get; set; } = false;

    [SetValue("PROCESS_METRICS_IN_EVALUATION_RESPONSE")]
    public bool PROCESS_METRICS_IN_EVALUATION_RESPONSE { get; set; } = true;

    [SetValue("JOB_STATUS_CONTAINER")]
    public string? JOB_STATUS_CONTAINER { get; set; }

    [SetValue("JOB_DONE_TIMEOUT_MINUTES")]
    public int JOB_DONE_TIMEOUT_MINUTES { get; set; } = 15;

    [SetValues]
    public void ApplyDerivedValues()
    {
        ROLES = ParseRoles(ROLES_RAW);
        BACKOFF_ON_STATUS_CODES = ParseIntArray(BACKOFF_ON_STATUS_CODES_RAW, [429]);
        DEADLETTER_ON_STATUS_CODES = ParseIntArray(DEADLETTER_ON_STATUS_CODES_RAW, [400, 401, 403, 404, 405]);

        INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_QUERY = ResolveTransformQuery(
            INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_QUERY,
            INBOUND_GROUNDTRUTH_FOR_API_TRANSFORM_FILE);
        INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_QUERY = ResolveTransformQuery(
            INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_QUERY,
            INBOUND_GROUNDTRUTH_FOR_INFERENCE_TRANSFORM_FILE);
        INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_QUERY = ResolveTransformQuery(
            INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_QUERY,
            INBOUND_GROUNDTRUTH_FOR_EVALUATION_TRANSFORM_FILE);
        INBOUND_INFERENCE_TRANSFORM_QUERY = ResolveTransformQuery(
            INBOUND_INFERENCE_TRANSFORM_QUERY,
            INBOUND_INFERENCE_TRANSFORM_FILE);
        INBOUND_EVALUATION_TRANSFORM_QUERY = ResolveTransformQuery(
            INBOUND_EVALUATION_TRANSFORM_QUERY,
            INBOUND_EVALUATION_TRANSFORM_FILE);

        MS_BETWEEN_DEQUEUE_CURRENT = MS_BETWEEN_DEQUEUE;
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (ROLES.Count == 0)
        {
            yield return new ValidationResult("ROLES must include at least one role.", new[] { nameof(ROLES) });
        }

        if (invalidRoles.Count > 0)
        {
            yield return new ValidationResult(
                $"ROLES contains invalid values: {string.Join(", ", invalidRoles)}.",
                new[] { nameof(ROLES) });
        }

        if (string.IsNullOrEmpty(AZURE_STORAGE_ACCOUNT_NAME) && string.IsNullOrEmpty(AZURE_STORAGE_CONNECTION_STRING))
        {
            yield return new ValidationResult(
                "Either AZURE_STORAGE_ACCOUNT_NAME or AZURE_STORAGE_CONNECTION_STRING must be set.",
                new[] { nameof(AZURE_STORAGE_ACCOUNT_NAME), nameof(AZURE_STORAGE_CONNECTION_STRING) });
        }

        var hasInference = ROLES.Contains(Roles.InferenceProxy);
        var hasEvaluation = ROLES.Contains(Roles.EvaluationProxy);

        if (hasInference)
        {
            if (string.IsNullOrEmpty(INFERENCE_CONTAINER))
            {
                yield return new ValidationResult(
                    "INFERENCE_CONTAINER must be set when using the InferenceProxy role.",
                    new[] { nameof(INFERENCE_CONTAINER) });
            }

            if (string.IsNullOrEmpty(INFERENCE_URL))
            {
                yield return new ValidationResult(
                    "INFERENCE_URL must be set when using the InferenceProxy role.",
                    new[] { nameof(INFERENCE_URL) });
            }

            if (INBOUND_INFERENCE_QUEUES.Length == 0)
            {
                yield return new ValidationResult(
                    "INBOUND_INFERENCE_QUEUES must be set when using the InferenceProxy role.",
                    new[] { nameof(INBOUND_INFERENCE_QUEUES) });
            }

            if (string.IsNullOrEmpty(OUTBOUND_INFERENCE_QUEUE))
            {
                yield return new ValidationResult(
                    "OUTBOUND_INFERENCE_QUEUE must be set when using the InferenceProxy role.",
                    new[] { nameof(OUTBOUND_INFERENCE_QUEUE) });
            }
        }

        if (hasEvaluation)
        {
            if (string.IsNullOrEmpty(INFERENCE_CONTAINER))
            {
                yield return new ValidationResult(
                    "INFERENCE_CONTAINER must be set when using the EvaluationProxy role.",
                    new[] { nameof(INFERENCE_CONTAINER) });
            }

            if (string.IsNullOrEmpty(EVALUATION_CONTAINER))
            {
                yield return new ValidationResult(
                    "EVALUATION_CONTAINER must be set when using the EvaluationProxy role.",
                    new[] { nameof(EVALUATION_CONTAINER) });
            }

            if (string.IsNullOrEmpty(EVALUATION_URL))
            {
                yield return new ValidationResult(
                    "EVALUATION_URL must be set when using the EvaluationProxy role.",
                    new[] { nameof(EVALUATION_URL) });
            }

            if (INBOUND_EVALUATION_QUEUES.Length == 0)
            {
                yield return new ValidationResult(
                    "INBOUND_EVALUATION_QUEUES must be set when using the EvaluationProxy role.",
                    new[] { nameof(INBOUND_EVALUATION_QUEUES) });
            }
        }

        if (hasInference || hasEvaluation)
        {
            if (SECONDS_BEFORE_TIMEOUT_FOR_PROCESSING <= 0)
            {
                yield return new ValidationResult(
                    "SECONDS_BEFORE_TIMEOUT_FOR_PROCESSING must be greater than 0 for proxy roles.",
                    new[] { nameof(SECONDS_BEFORE_TIMEOUT_FOR_PROCESSING) });
            }

            if (BACKOFF_ON_STATUS_CODES.Length == 0)
            {
                yield return new ValidationResult(
                    "BACKOFF_ON_STATUS_CODES must include at least one status code for proxy roles.",
                    new[] { nameof(BACKOFF_ON_STATUS_CODES) });
            }

            if (DEADLETTER_ON_STATUS_CODES.Length == 0)
            {
                yield return new ValidationResult(
                    "DEADLETTER_ON_STATUS_CODES must include at least one status code for proxy roles.",
                    new[] { nameof(DEADLETTER_ON_STATUS_CODES) });
            }
        }
    }

    private List<Roles> ParseRoles(string[]? raw)
    {
        invalidRoles.Clear();
        List<Roles> roles = [];
        if (raw is null || raw.Length == 0)
        {
            return roles;
        }

        foreach (var entry in raw)
        {
            if (Enum.TryParse(entry, true, out Roles role))
            {
                roles.Add(role);
            }
            else
            {
                invalidRoles.Add(entry);
            }
        }

        return roles;
    }

    private static int[] ParseIntArray(string[]? raw, int[] defaults)
    {
        if (raw is null || raw.Length == 0)
        {
            return defaults;
        }

        List<int> values = [];
        foreach (var entry in raw)
        {
            if (int.TryParse(entry, out var parsed))
            {
                values.Add(parsed);
            }
        }

        return values.Count == 0 ? defaults : [.. values];
    }

    private static string? ResolveTransformQuery(string? query, string? filePath)
    {
        if (!string.IsNullOrEmpty(query))
        {
            return query;
        }

        if (string.IsNullOrEmpty(filePath))
        {
            return query;
        }

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"transform file not found: {filePath}", filePath);
        }

        return File.ReadAllText(filePath);
    }
}