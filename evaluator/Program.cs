using System;
using System.Threading.Tasks;
using dotenv.net;
using Evaluator;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NetBricks;

// Load environment variables from .env file
var ENV_FILES = System.Environment.GetEnvironmentVariable("ENV_FILES").AsArray(() => [".env"]);
Console.WriteLine($"ENV_FILES = {string.Join(", ", ENV_FILES!)}");
DotEnv.Load(new DotEnvOptions(envFilePaths: ENV_FILES, overwriteExistingVars: false));

// create the web application
var builder = WebApplication.CreateBuilder(args);

// add config using NetBricks
builder.Services.AddHttpClient();
builder.Services.AddDefaultAzureCredential();
builder.Services.AddConfig<IConfig, Config>();

// add logging
builder.Logging.ClearProviders();
builder.Services.AddSingleLineConsoleLogger();
builder.Logging.AddFilter("Microsoft.AspNetCore.Mvc.ModelBinding", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Server.Kestrel.Connections", LogLevel.Warning);
builder.Logging.AddFilter("Microsoft.AspNetCore.Server.Kestrel.Transport.Sockets", LogLevel.Warning);

// configure OpenTelemetry logging early using IConfiguration (before full config is available)
// NOTE: It is unfortunate, but there appears to be no way to add OpenTelemetry in an async
// manner such that config could pull from App Config or Key Vault at startup.
var openTelemetryConnectionString = builder.Configuration["OPEN_TELEMETRY_CONNECTION_STRING"];
if (!string.IsNullOrEmpty(openTelemetryConnectionString))
{
    builder.Logging.AddOpenTelemetry(openTelemetryConnectionString);
    builder.Services.AddOpenTelemetry("evaluator", builder.Environment.ApplicationName, openTelemetryConnectionString);
}

// add API services
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

// configure Kestrel using IConfigFactory
builder.Services.AddSingleton<IConfigureOptions<KestrelServerOptions>, KestrelConfigurator>();

// add InferenceProxy, EvaluationProxy, and Maintenance services
builder.Services.AddHostedService<AzureStorageQueueReaderForInference>();
builder.Services.AddHostedService<AzureStorageQueueReaderForEvaluation>();
builder.Services.AddHostedService<Maintenance>();

// build
var app = builder.Build();

// use swagger (API only)
app.UseSwagger();
app.UseSwaggerUI();

// use CORS (API only)
app.UseCors("default-policy");

// add endpoints (API only)
app.UseRouting();
app.UseMiddleware<HttpExceptionMiddleware>();
app.MapControllers();

// run
await app.RunAsync();
