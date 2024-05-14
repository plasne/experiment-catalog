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
builder.Services.AddDefaultAzureCredential();

// add logging
builder.Logging.ClearProviders();
builder.Services.AddSingleLineConsoleLogger();

// add http client
builder.Services.AddHttpClient();

// add API services
if (config.ROLES.Contains(Roles.API))
{
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
    builder.Services.AddHostedService<AzureStorageQueueReader>();
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
app.Run();