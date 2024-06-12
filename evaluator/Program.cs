using System;
using dotenv.net;
using Evaluator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetBricks;

// load environment variables from .env file
DotEnv.Load();

// create the web application
var builder = WebApplication.CreateBuilder(args);

// add config
var netConfig = new NetBricks.Config();
await netConfig.Apply();
var config = new Evaluator.Config(netConfig);
config.Validate();
builder.Services.AddSingleton<Evaluator.IConfig>(config);
builder.Services.AddSingleton<NetBricks.IConfig>(netConfig);

// add credentials if connection string is not provided
if (string.IsNullOrEmpty(config.AZURE_STORAGE_CONNECTION_STRING))
{
    builder.Services.AddDefaultAzureCredential();
}

// add logging
builder.Logging.ClearProviders();
builder.Services.AddSingleLineConsoleLogger();
builder.Logging.AddOpenTelemetry(config.OPEN_TELEMETRY_CONNECTION_STRING);
builder.Services.AddOpenTelemetry(DiagnosticService.Source.Name, builder.Environment.ApplicationName, config.OPEN_TELEMETRY_CONNECTION_STRING);

// add http client
builder.Services.AddHttpClient();

// add API services
if (config.ROLES.Contains(Roles.API))
{
    Console.WriteLine("ADDING SERVICE: AzureStorageQueueWriter");
    builder.Services.AddHostedService<AzureStorageQueueWriter>();
    builder.Services.AddControllers().AddNewtonsoftJson();
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddSwaggerGen().AddSwaggerGenNewtonsoftSupport();
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("default-policy",
        builder =>
        {
            builder.WithOrigins("http://localhost:6020")
                   .AllowAnyHeader()
                   .AllowAnyMethod();
        });
    });
    builder.WebHost.UseKestrel(options =>
    {
        options.ListenAnyIP(config.PORT);
    });
}

// add InferenceProxy and EvaluationProxy services
if (config.ROLES.Contains(Roles.InferenceProxy) || config.ROLES.Contains(Roles.EvaluationProxy))
{
    Console.WriteLine("ADDING SERVICE: AzureStorageQueueReader");
    builder.Services.AddHostedService<AzureStorageQueueReader>();
}

// add maintenance service
if (config.MINUTES_BETWEEN_RESTORE_AFTER_BUSY > 0)
{
    Console.WriteLine("ADDING SERVICE: Maintenance");
    builder.Services.AddHostedService<Maintenance>();
}

// build
var app = builder.Build();

// add API endpoints and routing
if (config.ROLES.Contains(Roles.API))
{
    // use swagger
    app.UseSwagger();
    app.UseSwaggerUI();

    // use CORS
    app.UseCors("default-policy");

    // add endpoints
    app.UseRouting();
    app.UseMiddleware<HttpExceptionMiddleware>();
    app.MapControllers();
}

// run
await app.RunAsync();