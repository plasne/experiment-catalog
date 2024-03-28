using dotenv.net;
using NetBricks;
using Microsoft.AspNetCore.Rewrite;

// load environment variables from .env file
DotEnv.Load();

// create the web application
var builder = WebApplication.CreateBuilder(args);

// add services to the container
builder.Services.AddConfig();
builder.Services.AddSingleton<IStorage, AzureBlobStorage>();
builder.Services.AddControllers();
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

// build with swagger
var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

// use CORS
app.UseCors("default-policy");

// redirect to index.html
var options = new RewriteOptions().AddRedirect("^$", "index.html");
app.UseRewriter(options);

// add endpoints
app.UseStaticFiles();
app.MapControllers();

var port = Config.GetOnce("PORT") ?? "6010";
app.Run($"http://localhost:{port}");