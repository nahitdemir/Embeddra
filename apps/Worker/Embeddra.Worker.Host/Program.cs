using System.Reflection;
using Embeddra.BuildingBlocks.Audit;
using Embeddra.BuildingBlocks.Extensions;
using Embeddra.BuildingBlocks.Logging;
using Embeddra.BuildingBlocks.Observability;
using Embeddra.Worker.Host;
using Microsoft.AspNetCore.Builder;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEmbeddraElasticApm(builder.Configuration, "embeddra-worker");

var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
builder.Host.UseEmbeddraSerilog(builder.Configuration, "embeddra-worker", "logs-embeddra-worker", serviceVersion);

builder.Services.AddHostedService<Worker>();
builder.Services.AddHealthChecks();
builder.Services.AddEmbeddraAuditLogging(builder.Configuration);
builder.Services.AddSingleton<IRequestResponseLoggingPolicy, AdminRequestResponseLoggingPolicy>();
builder.Services.AddAllElasticApm();

var app = builder.Build();

app.UseEmbeddraMiddleware();

app.MapHealthChecks("/health");

app.Run();
