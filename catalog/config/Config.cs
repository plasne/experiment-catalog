using System;
using NetBricks;

namespace Catalog;

public class Config : IConfig
{
    private readonly NetBricks.IConfig config;

    public Config(NetBricks.IConfig config)
    {
        this.config = config;
        this.PORT = this.config.Get<string>("PORT").AsInt(() => 6010);
        this.OPEN_TELEMETRY_CONNECTION_STRING = this.config.GetSecret<string>("OPEN_TELEMETRY_CONNECTION_STRING").Result;
        this.AZURE_STORAGE_ACCOUNT_NAME = this.config.Get<string>("AZURE_STORAGE_ACCOUNT_NAME");
        this.AZURE_STORAGE_ACCOUNT_CONNSTRING = this.config.GetSecret<string>("AZURE_STORAGE_ACCOUNT_CONNSTRING").Result;
        this.CONCURRENCY = this.config.Get<string>("CONCURRENCY").AsInt(() => 4);
        this.REQUIRED_BLOCK_SIZE_IN_KB_FOR_OPTIMIZE = this.config.Get<string>("REQUIRED_BLOCK_SIZE_IN_KB_FOR_OPTIMIZE").AsInt(() => 1024);
        this.MINUTES_TO_BE_IDLE = this.config.Get<string>("MINUTES_TO_BE_IDLE").AsInt(() => 10);
        this.MINUTES_TO_BE_RECENT = this.config.Get<string>("MINUTES_TO_BE_RECENT").AsInt(() => 480); // 8 hours
        this.CALC_PVALUES_USING_X_SAMPLES = this.config.Get<string>("CALC_PVALUES_USING_X_SAMPLES").AsInt(() => 10000);
        this.CALC_PVALUES_EVERY_X_MINUTES = this.config.Get<string>("CALC_PVALUES_EVERY_X_MINUTES").AsInt(() => 0);
        this.MIN_ITERATIONS_TO_CALC_PVALUES = this.config.Get<string>("MIN_ITERATIONS_TO_CALC_PVALUES").AsInt(() => 5);
        this.CONFIDENCE_LEVEL = this.config.Get<string>("CONFIDENCE_LEVEL").AsDecimal(() => 0.95m);
        this.PRECISION_FOR_CALC_VALUES = this.config.Get<string>("PRECISION_FOR_CALC_VALUES").AsInt(() => 4);
        this.PATH_TEMPLATE = this.config.Get<string>("PATH_TEMPLATE");
        this.AZURE_STORAGE_ACCOUNT_NAME_FOR_SUPPORT_DOCS = this.config.Get<string>("AZURE_STORAGE_ACCOUNT_NAME_FOR_SUPPORT_DOCS");
        this.AZURE_STORAGE_ACCOUNT_CONNSTRING_FOR_SUPPORT_DOCS = this.config.GetSecret<string>("AZURE_STORAGE_ACCOUNT_CONNSTRING_FOR_SUPPORT_DOCS").Result;
        this.AZURE_STORAGE_CACHE_FOLDER = this.config.Get<string>("AZURE_STORAGE_CACHE_FOLDER");
        this.AZURE_STORAGE_CACHE_MAX_AGE_IN_HOURS = this.config.Get<string>("AZURE_STORAGE_CACHE_MAX_AGE_IN_HOURS").AsInt(() => 168);
        this.AZURE_STORAGE_OPTIMIZE_EVERY_X_MINUTES = this.config.Get<string>("AZURE_STORAGE_OPTIMIZE_EVERY_X_MINUTES").AsInt(() => 0);
        this.AZURE_STORAGE_CACHE_CLEANUP_EVERY_X_MINUTES = this.config.Get<string>("AZURE_STORAGE_CACHE_CLEANUP_EVERY_X_MINUTES").AsInt(() => 120);
        this.ENABLE_ANONYMOUS_DOWNLOAD = this.config.Get<string>("ENABLE_ANONYMOUS_DOWNLOAD").AsBool(() => false);
        if (this.ENABLE_ANONYMOUS_DOWNLOAD
            && string.IsNullOrEmpty(this.AZURE_STORAGE_ACCOUNT_CONNSTRING_FOR_SUPPORT_DOCS)
            && string.IsNullOrEmpty(this.AZURE_STORAGE_ACCOUNT_NAME_FOR_SUPPORT_DOCS))
        {
            this.AZURE_STORAGE_ACCOUNT_CONNSTRING_FOR_SUPPORT_DOCS = this.AZURE_STORAGE_ACCOUNT_CONNSTRING;
            this.AZURE_STORAGE_ACCOUNT_NAME_FOR_SUPPORT_DOCS = this.AZURE_STORAGE_ACCOUNT_NAME;
        }
        this.TEST_PROJECTS = this.config.Get<string>("TEST_PROJECTS").AsArray(() => new string[0]);
    }

    public int PORT { get; }

    public string OPEN_TELEMETRY_CONNECTION_STRING { get; }

    public string AZURE_STORAGE_ACCOUNT_NAME { get; }

    public string AZURE_STORAGE_ACCOUNT_CONNSTRING { get; }

    public int CONCURRENCY { get; }

    public int REQUIRED_BLOCK_SIZE_IN_KB_FOR_OPTIMIZE { get; }

    public int MINUTES_TO_BE_IDLE { get; }

    public int MINUTES_TO_BE_RECENT { get; }

    public int CALC_PVALUES_USING_X_SAMPLES { get; }

    public int CALC_PVALUES_EVERY_X_MINUTES { get; }

    public int MIN_ITERATIONS_TO_CALC_PVALUES { get; }

    public decimal CONFIDENCE_LEVEL { get; }

    public int PRECISION_FOR_CALC_VALUES { get; }

    public string PATH_TEMPLATE { get; }

    public string AZURE_STORAGE_ACCOUNT_NAME_FOR_SUPPORT_DOCS { get; }

    public string AZURE_STORAGE_ACCOUNT_CONNSTRING_FOR_SUPPORT_DOCS { get; }

    public string? AZURE_STORAGE_CACHE_FOLDER { get; }

    public int AZURE_STORAGE_CACHE_MAX_AGE_IN_HOURS { get; }

    public int AZURE_STORAGE_OPTIMIZE_EVERY_X_MINUTES { get; }

    public int AZURE_STORAGE_CACHE_CLEANUP_EVERY_X_MINUTES { get; }

    public bool ENABLE_ANONYMOUS_DOWNLOAD { get; }

    public string[] TEST_PROJECTS { get; }

    public void Validate()
    {
        this.config.Require("PORT", this.PORT);
        this.config.Optional("OPEN_TELEMETRY_CONNECTION_STRING", this.OPEN_TELEMETRY_CONNECTION_STRING, hideValue: true);
        this.config.Optional("AZURE_STORAGE_ACCOUNT_NAME", AZURE_STORAGE_ACCOUNT_NAME);
        this.config.Optional("AZURE_STORAGE_ACCOUNT_CONNECTION_STRING", this.AZURE_STORAGE_ACCOUNT_CONNSTRING, hideValue: true);
        if (string.IsNullOrEmpty(this.AZURE_STORAGE_ACCOUNT_NAME) && string.IsNullOrEmpty(this.AZURE_STORAGE_ACCOUNT_CONNSTRING))
        {
            throw new Exception("either AZURE_STORAGE_ACCOUNT_NAME or AZURE_STORAGE_ACCOUNT_CONNECTION_STRING must be set.");
        }
        this.config.Require("CONCURRENCY", this.CONCURRENCY);
        this.config.Require("REQUIRED_BLOCK_SIZE_IN_KB_FOR_OPTIMIZE", this.REQUIRED_BLOCK_SIZE_IN_KB_FOR_OPTIMIZE);
        this.config.Require("MINUTES_TO_BE_IDLE", this.MINUTES_TO_BE_IDLE);
        this.config.Require("MINUTES_TO_BE_RECENT", this.MINUTES_TO_BE_RECENT);
        this.config.Require("CALC_PVALUES_USING_X_SAMPLES", this.CALC_PVALUES_USING_X_SAMPLES);
        this.config.Require("CALC_PVALUES_EVERY_X_MINUTES", this.CALC_PVALUES_EVERY_X_MINUTES);
        this.config.Require("MIN_ITERATIONS_TO_CALC_PVALUES", this.MIN_ITERATIONS_TO_CALC_PVALUES);
        this.config.Require("CONFIDENCE_LEVEL", this.CONFIDENCE_LEVEL.ToString());
        this.config.Require("PRECISION_FOR_CALC_VALUES", this.PRECISION_FOR_CALC_VALUES.ToString());
        this.config.Optional("PATH_TEMPLATE", this.PATH_TEMPLATE);
        this.config.Optional("AZURE_STORAGE_ACCOUNT_NAME_FOR_SUPPORT_DOCS", this.AZURE_STORAGE_ACCOUNT_NAME_FOR_SUPPORT_DOCS);
        this.config.Optional("AZURE_STORAGE_ACCOUNT_CONNSTRING_FOR_SUPPORT_DOCS", this.AZURE_STORAGE_ACCOUNT_CONNSTRING_FOR_SUPPORT_DOCS, hideValue: true);
        this.config.Optional("AZURE_STORAGE_CACHE_FOLDER", this.AZURE_STORAGE_CACHE_FOLDER);
        this.config.Optional("AZURE_STORAGE_CACHE_MAX_AGE_IN_HOURS", this.AZURE_STORAGE_CACHE_MAX_AGE_IN_HOURS.ToString());
        this.config.Optional("AZURE_STORAGE_OPTIMIZE_EVERY_X_MINUTES", this.AZURE_STORAGE_OPTIMIZE_EVERY_X_MINUTES.ToString());
        this.config.Optional("AZURE_STORAGE_CACHE_CLEANUP_EVERY_X_MINUTES", this.AZURE_STORAGE_CACHE_CLEANUP_EVERY_X_MINUTES.ToString());
        this.config.Optional("ENABLE_ANONYMOUS_DOWNLOAD", this.ENABLE_ANONYMOUS_DOWNLOAD.ToString());
        this.config.Optional("TEST_PROJECTS", string.Join(",", this.TEST_PROJECTS));
    }
}