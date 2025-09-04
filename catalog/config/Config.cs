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
        this.REQUIRED_MIN_OF_IDLE_BEFORE_OPTIMIZE = this.config.Get<string>("REQUIRED_MIN_OF_IDLE_BEFORE_OPTIMIZE").AsInt(() => 10);
        this.OPTIMIZE_EVERY_X_MINUTES = this.config.Get<string>("OPTIMIZE_EVERY_X_MINUTES").AsInt(() => 0);
        this.PATH_TEMPLATE = this.config.Get<string>("PATH_TEMPLATE");
        this.AZURE_STORAGE_ACCOUNT_NAME_FOR_SUPPORT_DOCS = this.config.Get<string>("AZURE_STORAGE_ACCOUNT_NAME_FOR_SUPPORT_DOCS");
        this.AZURE_STORAGE_ACCOUNT_CONNSTRING_FOR_SUPPORT_DOCS = this.config.GetSecret<string>("AZURE_STORAGE_ACCOUNT_CONNSTRING_FOR_SUPPORT_DOCS").Result;
        this.ENABLE_ANONYMOUS_DOWNLOAD = this.config.Get<string>("ENABLE_ANONYMOUS_DOWNLOAD").AsBool(() => false);
        if (this.ENABLE_ANONYMOUS_DOWNLOAD
            && string.IsNullOrEmpty(this.AZURE_STORAGE_ACCOUNT_CONNSTRING_FOR_SUPPORT_DOCS)
            && string.IsNullOrEmpty(this.AZURE_STORAGE_ACCOUNT_NAME_FOR_SUPPORT_DOCS))
        {
            this.AZURE_STORAGE_ACCOUNT_CONNSTRING_FOR_SUPPORT_DOCS = this.AZURE_STORAGE_ACCOUNT_CONNSTRING;
            this.AZURE_STORAGE_ACCOUNT_NAME_FOR_SUPPORT_DOCS = this.AZURE_STORAGE_ACCOUNT_NAME;
        }
    }

    public int PORT { get; }

    public string OPEN_TELEMETRY_CONNECTION_STRING { get; }

    public string AZURE_STORAGE_ACCOUNT_NAME { get; }

    public string AZURE_STORAGE_ACCOUNT_CONNSTRING { get; }

    public int CONCURRENCY { get; }

    public int REQUIRED_BLOCK_SIZE_IN_KB_FOR_OPTIMIZE { get; }

    public int REQUIRED_MIN_OF_IDLE_BEFORE_OPTIMIZE { get; }

    public int OPTIMIZE_EVERY_X_MINUTES { get; }

    public string PATH_TEMPLATE { get; }

    public string AZURE_STORAGE_ACCOUNT_NAME_FOR_SUPPORT_DOCS { get; }

    public string AZURE_STORAGE_ACCOUNT_CONNSTRING_FOR_SUPPORT_DOCS { get; }

    public bool ENABLE_ANONYMOUS_DOWNLOAD { get; }

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
        this.config.Require("REQUIRED_MIN_OF_IDLE_BEFORE_OPTIMIZE", this.REQUIRED_MIN_OF_IDLE_BEFORE_OPTIMIZE);
        this.config.Require("OPTIMIZE_EVERY_X_MINUTES", this.OPTIMIZE_EVERY_X_MINUTES);
        this.config.Optional("PATH_TEMPLATE", this.PATH_TEMPLATE);
        this.config.Optional("AZURE_STORAGE_ACCOUNT_NAME_FOR_SUPPORT_DOCS", this.AZURE_STORAGE_ACCOUNT_NAME_FOR_SUPPORT_DOCS);
        this.config.Optional("AZURE_STORAGE_ACCOUNT_CONNSTRING_FOR_SUPPORT_DOCS", this.AZURE_STORAGE_ACCOUNT_CONNSTRING_FOR_SUPPORT_DOCS, hideValue: true);
        this.config.Optional("ENABLE_ANONYMOUS_DOWNLOAD", this.ENABLE_ANONYMOUS_DOWNLOAD.ToString());
    }
}