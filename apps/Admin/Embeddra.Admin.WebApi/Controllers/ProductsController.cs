using System.Text;
using System.Text.Json;
using Embeddra.Admin.Application.Services;
using Embeddra.Admin.WebApi.Auth;
using Embeddra.BuildingBlocks.Audit;
using Embeddra.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace Embeddra.Admin.WebApi.Controllers;

[ApiController]
public sealed class ProductsController : ControllerBase
{
    private static readonly JsonSerializerOptions PayloadSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly string[] DefaultCsvHeaders =
    {
        "id",
        "name",
        "description",
        "brand",
        "category",
        "price",
        "in_stock"
    };

    private static readonly HashSet<string> KnownHeaders = new(StringComparer.OrdinalIgnoreCase)
    {
        "id",
        "product_id",
        "productid",
        "name",
        "title",
        "description",
        "summary",
        "brand",
        "brand_name",
        "category",
        "category_name",
        "price",
        "unit_price",
        "amount",
        "in_stock",
        "instock",
        "available"
    };

    private readonly IAuditLogWriter _auditLogWriter;
    private readonly IIngestionService _ingestionService;
    private readonly ITenantService _tenantService;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(
        IAuditLogWriter auditLogWriter,
        IIngestionService ingestionService,
        ITenantService tenantService,
        ILogger<ProductsController> logger)
    {
        _auditLogWriter = auditLogWriter;
        _ingestionService = ingestionService;
        _tenantService = tenantService;
        _logger = logger;
    }

    [HttpPost("products:bulk")]
    public async Task<IActionResult> BulkUpload([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        if (!AdminAuthContext.CanTenantWrite(HttpContext))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        var tenantId = TenantContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { error = "tenant_required" });
        }

        if (!await _tenantService.ExistsAsync(tenantId, cancellationToken))
        {
            return NotFound(new { error = "tenant_not_found" });
        }

        var summary = SummarizeBulkPayload(payload);
        if (!summary.DocumentCount.HasValue || summary.DocumentCount.Value <= 0)
        {
            return BadRequest(new { error = "invalid_payload" });
        }

        var result = await _ingestionService.StartBulkIngestionAsync(
            tenantId,
            payload,
            cancellationToken);

        if (!result.Success)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = result.Error ?? "ingestion_publish_failed", job_id = result.JobId });
        }

        await _auditLogWriter.WriteAsync(
            new AuditLogEntry(
                AuditActions.BulkUploadRequested,
                ResolveActor(),
                new
                {
                    tenant_id = tenantId,
                    job_id = result.JobId,
                    document_count = summary.DocumentCount,
                    sample_product_ids = summary.SampleProductIds
                }),
            cancellationToken);

        return Accepted(new
        {
            job_id = result.JobId,
            status = "queued",
            count = summary.DocumentCount
        });
    }

    [HttpPost("products:importCsv")]
    public async Task<IActionResult> ImportCsv([FromBody] string csvPayload, CancellationToken cancellationToken)
    {
        if (!AdminAuthContext.CanTenantWrite(HttpContext))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        var tenantId = TenantContext.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            return BadRequest(new { error = "tenant_required" });
        }

        if (!await _tenantService.ExistsAsync(tenantId, cancellationToken))
        {
            return NotFound(new { error = "tenant_not_found" });
        }

        var parseResult = ParseCsvPayload(csvPayload);
        if (parseResult.Documents.Count == 0)
        {
            return BadRequest(new { error = "invalid_payload" });
        }

        // Convert parsed documents to JsonElement is not straightforward without re-serializing.
        // But IngestionService accepts JsonElement.
        // We can serialize to JsonDocument and use RootElement.
        var payloadJson = JsonSerializer.Serialize(parseResult.Documents, PayloadSerializerOptions);
        using var jsonDoc = JsonDocument.Parse(payloadJson);

        var result = await _ingestionService.StartBulkIngestionAsync(
            tenantId,
            jsonDoc.RootElement.Clone(), // Clone to detach from disposable doc
            cancellationToken);

        if (!result.Success)
        {
            return StatusCode(
                StatusCodes.Status500InternalServerError,
                new { error = result.Error ?? "ingestion_publish_failed", job_id = result.JobId });
        }

        await _auditLogWriter.WriteAsync(
            new AuditLogEntry(
                AuditActions.CsvImportRequested,
                ResolveActor(),
                new
                {
                    tenant_id = tenantId,
                    job_id = result.JobId,
                    csv_row_count = parseResult.RowCount,
                    sample_product_ids = parseResult.SampleProductIds
                }),
            cancellationToken);

        return Accepted(new
        {
            job_id = result.JobId,
            status = "queued",
            count = parseResult.RowCount
        });
    }



    private string ResolveActor()
    {
        if (User.Identity?.IsAuthenticated == true && !string.IsNullOrWhiteSpace(User.Identity.Name))
        {
            return User.Identity.Name;
        }

        var headerActor = Request.Headers["X-Actor"].ToString();
        return string.IsNullOrWhiteSpace(headerActor) ? "system" : headerActor;
    }

    private static BulkPayloadSummary SummarizeBulkPayload(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            return new BulkPayloadSummary(payload.GetArrayLength(), ExtractSampleProductIds(payload));
        }

        if (payload.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in new[] { "documents", "items", "products" })
            {
                if (payload.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array)
                {
                    return new BulkPayloadSummary(array.GetArrayLength(), ExtractSampleProductIds(array));
                }
            }

            if (payload.TryGetProperty("document", out var document) && document.ValueKind == JsonValueKind.Object)
            {
                return new BulkPayloadSummary(1, ExtractSampleProductIds(document));
            }

            return new BulkPayloadSummary(1, ExtractSampleProductIds(payload));
        }

        return new BulkPayloadSummary(null, new List<string>());
    }

    private static CsvParseResult ParseCsvPayload(string csvPayload)
    {
        if (string.IsNullOrWhiteSpace(csvPayload))
        {
            return CsvParseResult.Empty;
        }

        var lines = csvPayload
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimEnd('\r'))
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToList();

        if (lines.Count == 0)
        {
            return CsvParseResult.Empty;
        }

        var firstRow = ParseCsvLine(lines[0]);
        var hasHeader = firstRow.Any(HeaderLooksLikeName);
        var headers = hasHeader
            ? firstRow.Select(NormalizeHeader).ToList()
            : BuildDefaultHeaders(firstRow.Count);

        var startIndex = hasHeader ? 1 : 0;
        var documents = new List<Dictionary<string, object?>>();
        var sampleIds = new List<string>();

        for (var i = startIndex; i < lines.Count; i++)
        {
            var columns = ParseCsvLine(lines[i]);
            if (columns.Count == 0 || columns.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            var document = BuildCsvDocument(headers, columns, out var idValue);
            if (document.Count == 0)
            {
                continue;
            }

            documents.Add(document);

            if (!string.IsNullOrWhiteSpace(idValue) && sampleIds.Count < 5)
            {
                sampleIds.Add(idValue);
            }
        }

        return documents.Count == 0
            ? CsvParseResult.Empty
            : new CsvParseResult(documents, documents.Count, sampleIds);
    }

    private static bool HeaderLooksLikeName(string header)
    {
        if (string.IsNullOrWhiteSpace(header))
        {
            return false;
        }

        var normalized = NormalizeHeader(header);
        return KnownHeaders.Contains(normalized);
    }

    private static List<string> BuildDefaultHeaders(int columnCount)
    {
        var headers = new List<string>(columnCount);
        for (var i = 0; i < columnCount; i++)
        {
            if (i < DefaultCsvHeaders.Length)
            {
                headers.Add(DefaultCsvHeaders[i]);
            }
            else
            {
                headers.Add($"attr_{i - DefaultCsvHeaders.Length + 1}");
            }
        }

        return headers;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var results = new List<string>();
        if (string.IsNullOrEmpty(line))
        {
            results.Add(string.Empty);
            return results;
        }

        var buffer = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    buffer.Append('"');
                    i++;
                    continue;
                }

                inQuotes = !inQuotes;
                continue;
            }

            if (ch == ',' && !inQuotes)
            {
                results.Add(buffer.ToString());
                buffer.Clear();
                continue;
            }

            buffer.Append(ch);
        }

        results.Add(buffer.ToString());
        return results;
    }

    private static Dictionary<string, object?> BuildCsvDocument(
        IReadOnlyList<string> headers,
        IReadOnlyList<string> columns,
        out string? idValue)
    {
        var document = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        idValue = null;

        var count = Math.Min(headers.Count, columns.Count);
        for (var i = 0; i < count; i++)
        {
            var header = headers[i];
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            var value = columns[i].Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            switch (header)
            {
                case "id":
                    document["id"] = value;
                    idValue ??= value;
                    break;
                case "product_id":
                    document["product_id"] = value;
                    idValue ??= value;
                    break;
                case "name":
                    document["name"] = value;
                    break;
                case "description":
                    document["description"] = value;
                    break;
                case "brand":
                    document["brand"] = value;
                    break;
                case "category":
                    document["category"] = value;
                    break;
                case "price":
                    document["price"] = value;
                    break;
                case "in_stock":
                    document["in_stock"] = value;
                    break;
                default:
                    attributes[header] = value;
                    break;
            }
        }

        if (attributes.Count > 0)
        {
            document["attributes"] = attributes;
        }

        return document;
    }

    private static string NormalizeHeader(string header)
    {
        var normalized = header.Trim().Trim('"');
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return string.Empty;
        }

        normalized = normalized.ToLowerInvariant()
            .Replace(" ", "_", StringComparison.Ordinal)
            .Replace("-", "_", StringComparison.Ordinal);

        normalized = normalized.Replace("__", "_", StringComparison.Ordinal);

        return normalized switch
        {
            "productid" => "product_id",
            "product_id" => "product_id",
            "product-id" => "product_id",
            "title" => "name",
            "summary" => "description",
            "brand_name" => "brand",
            "category_name" => "category",
            "unit_price" => "price",
            "amount" => "price",
            "instock" => "in_stock",
            "available" => "in_stock",
            _ => normalized
        };
    }

    private static List<string> ExtractSampleProductIds(JsonElement element)
    {
        var results = new List<string>();

        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                if (results.Count >= 5)
                {
                    break;
                }

                if (TryExtractId(item, out var id))
                {
                    results.Add(id);
                }
            }

            return results;
        }

        if (element.ValueKind == JsonValueKind.Object && TryExtractId(element, out var objectId))
        {
            results.Add(objectId);
        }

        return results;
    }

    private static bool TryExtractId(JsonElement element, out string id)
    {
        id = string.Empty;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        return TryGetString(element, "id", out id)
            || TryGetString(element, "productId", out id)
            || TryGetString(element, "product_id", out id);
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = property.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        value = text;
        return true;
    }

    private static string TruncateError(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "unknown";
        }

        return message.Length > 512 ? message[..512] : message;
    }

    private sealed record BulkPayloadSummary(int? DocumentCount, List<string> SampleProductIds);

    private sealed record CsvParseResult(
        List<Dictionary<string, object?>> Documents,
        int RowCount,
        List<string> SampleProductIds)
    {
        public static CsvParseResult Empty { get; } = new(
            new List<Dictionary<string, object?>>(),
            0,
            new List<string>());
    }


}
