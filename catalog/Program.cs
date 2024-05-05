using dotenv.net;
using NetBricks;
using System.Text.Json;
using System.Text.Json.Serialization;

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

// setup logger
builder.Logging.ClearProviders();
builder.Services.AddSingleLineConsoleLogger();

// add services to the container
builder.Services.AddConfig();
builder.Services.AddSingleton<IStorageService, AzureBlobStorageService>();
builder.Services.AddHostedService<AzureBlobStorageMaintenanceService>();
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
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();
app.UseMiddleware<HttpExceptionMiddleware>();
app.MapControllers();

// run
app.Run();