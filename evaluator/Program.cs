using System.Text.Json;
using System.Text.Json.Serialization;
using dotenv.net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using NetBricks;

// load environment variables from .env file
DotEnv.Load();

// create the web application
var builder = WebApplication.CreateBuilder(args);

// add config
var netConfig = new NetBricks.Config();
await netConfig.Apply();
var config = new Config(netConfig);
config.Validate();
builder.Services.AddSingleton<IConfig>(config);
builder.Services.AddSingleton<NetBricks.IConfig>(netConfig);
builder.Services.AddDefaultAzureCredential();

// add services to the container
builder.Services.AddHttpClient();
builder.Services.AddSingleton<AzureStorageDetails>();
builder.Services.AddSingleton<IQueueService, AzureStorageQueueService>();
builder.Services.AddSingleton<IBlobStorageService, AzureBlobStorageService>();
builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// add CORS services
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

// listen (disable TLS)
builder.WebHost.UseKestrel(options =>
{
    options.ListenAnyIP(config.PORT);
});

// build with swagger
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

// use CORS
app.UseCors("default-policy");

// add endpoints
app.UseRouting();
app.UseMiddleware<HttpExceptionMiddleware>();
app.MapControllers();

// run
app.Run();