using NetBricks;

namespace Catalog;

public class Config : IConfig
{
    private readonly NetBricks.IConfig config;

    public Config(NetBricks.IConfig config)
    {
        this.config = config;
        this.PORT = this.config.Get<string>("PORT").AsInt(() => 6010);
        this.AZURE_STORAGE_ACCOUNT_NAME = this.config.Get<string>("AZURE_STORAGE_ACCOUNT_NAME");
        this.CONCURRENCY = this.config.Get<string>("CONCURRENCY").AsInt(() => 4);
        this.REQUIRED_BLOCK_SIZE_IN_MB_FOR_OPTIMIZE = this.config.Get<string>("REQUIRED_BLOCK_SIZE_IN_MB_FOR_OPTIMIZE").AsInt(() => 1);
        this.REQUIRED_MIN_OF_IDLE_BEFORE_OPTIMIZE = this.config.Get<string>("REQUIRED_MIN_OF_IDLE_BEFORE_OPTIMIZE").AsInt(() => 10);
        this.OPTIMIZE_EVERY_X_MINUTES = this.config.Get<string>("OPTIMIZE_EVERY_X_MINUTES").AsInt(() => 5);
    }

    public int PORT { get; }

    public string AZURE_STORAGE_ACCOUNT_NAME { get; }

    public int CONCURRENCY { get; }

    public int REQUIRED_BLOCK_SIZE_IN_MB_FOR_OPTIMIZE { get; }

    public int REQUIRED_MIN_OF_IDLE_BEFORE_OPTIMIZE { get; }

    public int OPTIMIZE_EVERY_X_MINUTES { get; }

    public void Validate()
    {
        this.config.Require("PORT", this.PORT);
        this.config.Require("AZURE_STORAGE_ACCOUNT_NAME", AZURE_STORAGE_ACCOUNT_NAME);
        this.config.Require("CONCURRENCY", this.CONCURRENCY);
        this.config.Require("REQUIRED_BLOCK_SIZE_IN_MB_FOR_OPTIMIZE", this.REQUIRED_BLOCK_SIZE_IN_MB_FOR_OPTIMIZE);
        this.config.Require("REQUIRED_MIN_OF_IDLE_BEFORE_OPTIMIZE", this.REQUIRED_MIN_OF_IDLE_BEFORE_OPTIMIZE);
        this.config.Require("OPTIMIZE_EVERY_X_MINUTES", this.OPTIMIZE_EVERY_X_MINUTES);
    }
}