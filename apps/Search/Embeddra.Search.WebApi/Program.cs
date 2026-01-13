using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using Embeddra.BuildingBlocks.Extensions;
using Embeddra.BuildingBlocks.Logging;
using Embeddra.BuildingBlocks.Observability;
using Embeddra.BuildingBlocks.Authentication;
using Embeddra.Contracts;
using Embeddra.Search.Infrastructure.DependencyInjection;
using Embeddra.Search.WebApi.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEmbeddraElasticApm(builder.Configuration, "embeddra-search");

var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
builder.Host.UseEmbeddraSerilog(builder.Configuration, "embeddra-search", "logs-embeddra-search", serviceVersion);

builder.Services.AddControllers();
builder.Services.AddMemoryCache();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddCors(options =>
{
    options.AddPolicy("EmbeddraWidget", policy =>
    {
        policy.AllowAnyHeader()
            .AllowAnyMethod()
            .WithExposedHeaders("X-Correlation-Id");

        var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>();
        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins);
        }
        else if (builder.Environment.IsDevelopment())
        {
            policy.AllowAnyOrigin();
        }
    });
});
builder.Services.AddSingleton<IRequestResponseLoggingPolicy, SearchRequestResponseLoggingPolicy>();
builder.Services.AddEmbeddraSearchInfrastructure(builder.Configuration);
builder.Services.AddSingleton(SearchRateLimitOptions.FromConfiguration(builder.Configuration));
builder.Services.AddSingleton<SearchRateLimiter>();
builder.Services.AddEmbeddraApiKeyAuth(builder.Configuration, options =>
{
    if (builder.Environment.IsDevelopment())
    {
        options.AllowAnonymousPathPrefixes.Add("/swagger");
    }

    options.AllowedKeyTypes.Add(ApiKeyTypes.SearchPublic);
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

app.UseCors("EmbeddraWidget");
app.UseEmbeddraMiddleware();
app.UseMiddleware<SearchAccessMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.Run();
