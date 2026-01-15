using System;
using System.Linq;
using System.Threading.Tasks;
using Catalog;
using dotenv.net;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NetBricks;

// load environment variables from .env file
DotEnv.Load();

// create the web application
var builder = WebApplication.CreateBuilder(args);

// add config using new NetBricks pattern
builder.Services.AddHttpClient();
builder.Services.AddDefaultAzureCredential();
builder.Services.AddConfig<IConfig, Config>();

// configure Kestrel using IConfigFactory
builder.Services.AddSingleton<IConfigureOptions<KestrelServerOptions>, KestrelConfigurator>();

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
    builder.Services.AddOpenTelemetry("catalog", builder.Environment.ApplicationName, openTelemetryConnectionString);
}

// add services to the container
builder.Services.AddSingleton<IStorageService, AzureBlobStorageService>();
builder.Services.AddSingleton<ISupportDocsService, AzureBlobSupportDocsService>();
builder.Services.AddSingleton<CalculateStatisticsService>();
builder.Services.AddSingleton<ConcurrencyService>();
builder.Services.AddHostedService<AzureBlobStorageMaintenanceService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<CalculateStatisticsService>());

// add controllers with swagger
builder.Services.AddControllers().AddNewtonsoftJson();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen().AddSwaggerGenNewtonsoftSupport();

// add authentication with deferred configuration
builder.Services.AddSingleton<IConfigureOptions<JwtBearerOptions>, JwtBearerConfigurator>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();
builder.Services.AddAuthorization();
builder.Services.AddSingleton<IConfigureOptions<AuthorizationOptions>, AuthorizationConfigurator>();

// add CORS services
builder.Services.AddCors(options =>
{
    options.AddPolicy("default-policy",
    corsBuilder =>
    {
        corsBuilder.WithOrigins("http://localhost:6020")
               .AllowAnyHeader()
               .AllowAnyMethod()
               .AllowCredentials();
    });
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

// add authentication and authorization middleware
app.UseAuthentication();
app.UseAuthorization();

// map controllers
app.MapControllers();

// run
app.Run();