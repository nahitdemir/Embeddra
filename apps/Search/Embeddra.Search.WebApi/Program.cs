using System.Reflection;
using Embeddra.BuildingBlocks.Extensions;
using Embeddra.BuildingBlocks.Logging;
using Embeddra.BuildingBlocks.Observability;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEmbeddraElasticApm(builder.Configuration, "embeddra-search");

var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
builder.Host.UseEmbeddraSerilog(builder.Configuration, "embeddra-search", "logs-embeddra-search", serviceVersion);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IRequestResponseLoggingPolicy, SearchRequestResponseLoggingPolicy>();
builder.Services.AddHttpClient("elasticsearch", client =>
{
    var elasticsearchUrl = builder.Configuration["ELASTICSEARCH_URL"] ?? "http://localhost:9200";
    client.BaseAddress = new Uri(elasticsearchUrl);
});
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
