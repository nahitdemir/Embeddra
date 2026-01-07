using System.Reflection;
using System.Text;
using Embeddra.BuildingBlocks.Audit;
using Embeddra.BuildingBlocks.Extensions;
using Embeddra.BuildingBlocks.Logging;
using Embeddra.BuildingBlocks.Messaging;
using Embeddra.BuildingBlocks.Observability;
using Embeddra.Worker.Host;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

builder.Configuration.AddEmbeddraElasticApm(builder.Configuration, "embeddra-worker");

var serviceVersion = Assembly.GetExecutingAssembly().GetName().Version?.ToString();
builder.Host.UseEmbeddraSerilog(builder.Configuration, "embeddra-worker", "logs-embeddra-worker", serviceVersion);

builder.Services.AddHostedService<Worker>();
builder.Services.AddHealthChecks();
builder.Services.AddEmbeddraAuditLogging(builder.Configuration);
builder.Services.AddEmbeddraRabbitMq(builder.Configuration);
builder.Services.AddSingleton<IRequestResponseLoggingPolicy, AdminRequestResponseLoggingPolicy>();
builder.Services.AddAllElasticApm();

var app = builder.Build();

app.UseEmbeddraMiddleware();

app.MapGet("/", () => Results.Content(BuildStatusPage(), "text/html; charset=utf-8"));

app.MapHealthChecks("/health");

app.Run();

static string BuildStatusPage()
{
    var rows = new (string Name, string Url, string Note)[]
    {
        ("Admin API", "http://localhost:5114", "/health, /swagger"),
        ("Search API", "http://localhost:5222", "/health, /swagger"),
        ("Worker", "http://localhost:5310", "/health"),
        ("Kibana", "http://localhost:5601", "APM UI"),
        ("Elasticsearch", "http://localhost:9200", "Cluster info"),
        ("APM Server", "http://localhost:8200", "Intake"),
        ("RabbitMQ", "http://localhost:15672", "Management UI"),
        ("Postgres", "localhost:5433", "psql"),
        ("Redis", "localhost:6379", "redis-cli")
    };

    var builder = new StringBuilder();
    builder.AppendLine("<!doctype html>");
    builder.AppendLine("<html lang=\"en\">");
    builder.AppendLine("<head>");
    builder.AppendLine("<meta charset=\"utf-8\" />");
    builder.AppendLine("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />");
    builder.AppendLine("<title>Embeddra Local Status</title>");
    builder.AppendLine("<style>");
    builder.AppendLine("body{font-family:Arial,Helvetica,sans-serif;margin:40px;background:#f7f7f7;color:#1d1d1f}");
    builder.AppendLine("h1{font-size:22px;margin-bottom:8px}");
    builder.AppendLine("table{width:100%;border-collapse:collapse;background:#fff}");
    builder.AppendLine("th,td{padding:10px 12px;border-bottom:1px solid #e5e5e5;text-align:left}");
    builder.AppendLine("th{background:#f0f0f0}");
    builder.AppendLine("a{color:#0b63ce;text-decoration:none}");
    builder.AppendLine("</style>");
    builder.AppendLine("</head>");
    builder.AppendLine("<body>");
    builder.AppendLine("<h1>Embeddra Local Services</h1>");
    builder.AppendLine("<p>Open these endpoints in your browser to verify the stack.</p>");
    builder.AppendLine("<table>");
    builder.AppendLine("<thead><tr><th>Service</th><th>Address</th><th>Notes</th></tr></thead>");
    builder.AppendLine("<tbody>");

    foreach (var row in rows)
    {
        builder.AppendLine("<tr>");
        builder.AppendLine($"<td>{row.Name}</td>");
        if (row.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            builder.AppendLine($"<td><a href=\"{row.Url}\" target=\"_blank\">{row.Url}</a></td>");
        }
        else
        {
            builder.AppendLine($"<td>{row.Url}</td>");
        }
        builder.AppendLine($"<td>{row.Note}</td>");
        builder.AppendLine("</tr>");
    }

    builder.AppendLine("</tbody>");
    builder.AppendLine("</table>");
    builder.AppendLine("</body>");
    builder.AppendLine("</html>");

    return builder.ToString();
}
