using System.Reflection;
using Embeddra.BuildingBlocks.Audit;
using Embeddra.BuildingBlocks.Extensions;
using Embeddra.BuildingBlocks.Logging;
using Embeddra.BuildingBlocks.Observability;
using Embeddra.Admin.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEmbeddraElasticApm(builder.Configuration, "embeddra-admin");

var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
builder.Host.UseEmbeddraSerilog(builder.Configuration, "embeddra-admin", "logs-embeddra-admin", serviceVersion);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddEmbeddraAdminPersistence(builder.Configuration);
builder.Services.AddEmbeddraAuditLogging(builder.Configuration);
builder.Services.AddSingleton<IRequestResponseLoggingPolicy, AdminRequestResponseLoggingPolicy>();
builder.Services.AddAllElasticApm();

var app = builder.Build();

app.UseEmbeddraMiddleware();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
