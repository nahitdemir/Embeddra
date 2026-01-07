using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using Embeddra.BuildingBlocks.Extensions;
using Embeddra.BuildingBlocks.Logging;
using Embeddra.BuildingBlocks.Observability;
using Embeddra.BuildingBlocks.Authentication;
using Embeddra.Search.Infrastructure.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEmbeddraElasticApm(builder.Configuration, "embeddra-search");

var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
builder.Host.UseEmbeddraSerilog(builder.Configuration, "embeddra-search", "logs-embeddra-search", serviceVersion);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IRequestResponseLoggingPolicy, SearchRequestResponseLoggingPolicy>();
builder.Services.AddEmbeddraSearchInfrastructure(builder.Configuration);
builder.Services.AddEmbeddraApiKeyAuth(builder.Configuration, options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.AllowAnonymousPathPrefixes.Add("/swagger");
    }
});
builder.Services.AddHttpClient("elasticsearch", client =>
{
    var elasticsearchUrl = builder.Configuration["ELASTICSEARCH_URL"] ?? "http://localhost:9200";
    var elasticsearchUser = builder.Configuration["ELASTICSEARCH_USERNAME"];
    var elasticsearchPassword = builder.Configuration["ELASTICSEARCH_PASSWORD"];

    client.BaseAddress = new Uri(elasticsearchUrl);

    if (!string.IsNullOrWhiteSpace(elasticsearchUser) || !string.IsNullOrWhiteSpace(elasticsearchPassword))
    {
        var credentials = $"{elasticsearchUser ?? "elastic"}:{elasticsearchPassword ?? string.Empty}";
        var token = Convert.ToBase64String(Encoding.ASCII.GetBytes(credentials));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
    }
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
