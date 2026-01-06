using System.Text.Json;
using Embeddra.BuildingBlocks.Audit;
using Microsoft.AspNetCore.Mvc;

namespace Embeddra.Admin.WebApi.Controllers;

[ApiController]
public sealed class ProductsController : ControllerBase
{
    private readonly IAuditLogWriter _auditLogWriter;

    public ProductsController(IAuditLogWriter auditLogWriter)
    {
        _auditLogWriter = auditLogWriter;
    }

    [HttpPost("products:bulk")]
    public async Task<IActionResult> BulkUpload([FromBody] JsonElement payload, CancellationToken cancellationToken)
    {
        var summary = SummarizeBulkPayload(payload);
        await _auditLogWriter.WriteAsync(
            new AuditLogEntry(AuditActions.BulkUploadRequested, ResolveActor(), summary),
            cancellationToken);

        return Accepted(new { status = "queued" });
    }

    [HttpPost("products:importCsv")]
    public async Task<IActionResult> ImportCsv([FromBody] string csvPayload, CancellationToken cancellationToken)
    {
        var summary = SummarizeCsv(csvPayload);
        await _auditLogWriter.WriteAsync(
            new AuditLogEntry(AuditActions.CsvImportRequested, ResolveActor(), summary),
            cancellationToken);

        return Accepted(new { status = "queued" });
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

    private static object SummarizeBulkPayload(JsonElement payload)
    {
        if (payload.ValueKind == JsonValueKind.Array)
        {
            return new
            {
                document_count = payload.GetArrayLength(),
                sample_product_ids = ExtractSampleProductIds(payload)
            };
        }

        if (payload.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in new[] { "documents", "items", "products" })
            {
                if (payload.TryGetProperty(property, out var array) && array.ValueKind == JsonValueKind.Array)
                {
                    return new
                    {
                        document_count = array.GetArrayLength(),
                        sample_product_ids = ExtractSampleProductIds(array)
                    };
                }
            }
        }

        return new { document_count = (int?)null, sample_product_ids = Array.Empty<string>() };
    }

    private static object SummarizeCsv(string csvPayload)
    {
        if (string.IsNullOrWhiteSpace(csvPayload))
        {
            return new { csv_row_count = 0, sample_product_ids = Array.Empty<string>() };
        }

        var lines = csvPayload.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var startIndex = lines.Length > 0 && lines[0].Contains("id", StringComparison.OrdinalIgnoreCase) ? 1 : 0;
        var sampleIds = new List<string>();

        for (var i = startIndex; i < lines.Length && sampleIds.Count < 5; i++)
        {
            var columns = lines[i].Split(',', StringSplitOptions.RemoveEmptyEntries);
            if (columns.Length > 0)
            {
                sampleIds.Add(columns[0].Trim());
            }
        }

        var rowCount = Math.Max(lines.Length - startIndex, 0);
        return new { csv_row_count = rowCount, sample_product_ids = sampleIds };
    }

    private static List<string> ExtractSampleProductIds(JsonElement array)
    {
        var results = new List<string>();
        foreach (var item in array.EnumerateArray())
        {
            if (results.Count >= 5)
            {
                break;
            }

            if (item.ValueKind == JsonValueKind.Object)
            {
                if (TryGetString(item, "id", out var id)
                    || TryGetString(item, "productId", out id)
                    || TryGetString(item, "product_id", out id))
                {
                    results.Add(id);
                }
            }
            else if (item.ValueKind == JsonValueKind.String)
            {
                var idValue = item.GetString();
                if (!string.IsNullOrWhiteSpace(idValue))
                {
                    results.Add(idValue);
                }
            }
        }

        return results;
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
}
