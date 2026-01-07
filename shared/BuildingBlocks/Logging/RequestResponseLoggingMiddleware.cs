using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Embeddra.BuildingBlocks.Logging;

public sealed class RequestResponseLoggingMiddleware
{
    private const int MaxBodyBytes = 4096;
    private static readonly string[] BulkSummaryPaths = { "/products:bulk", "/products:importCsv" };

    private readonly RequestDelegate _next;
    private readonly ILogger<RequestResponseLoggingMiddleware> _logger;
    private readonly IRequestResponseLoggingPolicy _policy;

    public RequestResponseLoggingMiddleware(
        RequestDelegate next,
        ILogger<RequestResponseLoggingMiddleware> logger,
        IRequestResponseLoggingPolicy policy)
    {
        _next = next;
        _logger = logger;
        _policy = policy;
    }

    public async Task Invoke(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        var requestInfo = BuildRequestMetadata(context);
        var requestBody = await ReadRequestBodyAsync(context);
        var searchQuery = ExtractSearchQuery(context, requestBody?.Body);

        if (!string.IsNullOrWhiteSpace(searchQuery))
        {
            requestInfo["search_query"] = searchQuery;
        }

        var originalBody = context.Response.Body;
        await using var responseBody = new MemoryStream();
        context.Response.Body = responseBody;

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            var statusCode = context.Response.StatusCode;
            var shouldLogBody = _policy.ShouldLogBody(context, statusCode);

            if (requestBody is not null)
            {
                EnrichRequestBodyLog(context, requestInfo, requestBody, shouldLogBody);
            }

            var responseInfo = await BuildResponseLogAsync(context, responseBody, shouldLogBody);

            responseBody.Position = 0;
            await responseBody.CopyToAsync(originalBody);
            context.Response.Body = originalBody;

            _logger.LogInformation(
                "http_request {duration_ms} {@request} {@response}",
                stopwatch.ElapsedMilliseconds,
                requestInfo,
                responseInfo);
        }
    }

    private static Dictionary<string, object?> BuildRequestMetadata(HttpContext context)
    {
        return new Dictionary<string, object?>
        {
            ["method"] = context.Request.Method,
            ["path"] = context.Request.Path.Value,
            ["query"] = context.Request.QueryString.Value,
            ["origin"] = context.Request.Headers["Origin"].ToString(),
            ["user_agent"] = context.Request.Headers["User-Agent"].ToString(),
            ["remote_ip"] = context.Connection.RemoteIpAddress?.ToString(),
            ["headers"] = SensitiveDataMasker.MaskHeaders(context.Request.Headers)
        };
    }

    private string? ExtractSearchQuery(HttpContext context, string? requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
        {
            return null;
        }

        if (!IsJson(context.Request.ContentType))
        {
            return null;
        }

        if (!_policy.ShouldCaptureSearchQuery(context))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(requestBody);
            if (document.RootElement.TryGetProperty("query", out var queryElement)
                && queryElement.ValueKind == JsonValueKind.String)
            {
                return Truncate(queryElement.GetString(), _policy.SearchQueryMaxLength);
            }
        }
        catch
        {
            return null;
        }

        return null;
    }

    private void EnrichRequestBodyLog(
        HttpContext context,
        Dictionary<string, object?> requestInfo,
        BodySnapshot requestBody,
        bool shouldLogBody)
    {
        var isSummaryPath = _policy.ShouldSummarizeRequestBody(context);
        if (isSummaryPath)
        {
            requestInfo["body_summary"] = BuildBulkSummary(context, requestBody);
            return;
        }

        if (!requestBody.IsJson)
        {
            requestInfo["payload_bytes"] = requestBody.PayloadBytes;
            return;
        }

        if (!shouldLogBody)
        {
            requestInfo["payload_bytes"] = requestBody.PayloadBytes;
            requestInfo["body_logged"] = false;
            return;
        }

        var maskedBody = SensitiveDataMasker.MaskJson(requestBody.Body ?? string.Empty);
        requestInfo["body"] = maskedBody;
        requestInfo["payload_bytes"] = requestBody.PayloadBytes;
        requestInfo["body_truncated"] = requestBody.Truncated;
    }

    private async Task<Dictionary<string, object?>> BuildResponseLogAsync(
        HttpContext context,
        MemoryStream responseBody,
        bool shouldLogBody)
    {
        var responseInfo = new Dictionary<string, object?>
        {
            ["status"] = context.Response.StatusCode,
            ["content_type"] = context.Response.ContentType,
            ["headers"] = SensitiveDataMasker.MaskHeaders(context.Response.Headers)
        };

        if (!IsJson(context.Response.ContentType))
        {
            return responseInfo;
        }

        if (!shouldLogBody)
        {
            responseInfo["body_logged"] = false;
            return responseInfo;
        }

        responseBody.Position = 0;
        var body = await ReadBodyAsync(responseBody);
        responseBody.Position = 0;

        var masked = SensitiveDataMasker.MaskJson(body.Body ?? string.Empty);
        responseInfo["body"] = masked;
        responseInfo["payload_bytes"] = body.PayloadBytes;
        responseInfo["body_truncated"] = body.Truncated;

        return responseInfo;
    }

    private static async Task<BodySnapshot?> ReadRequestBodyAsync(HttpContext context)
    {
        var request = context.Request;
        var shouldReadBody = IsJson(request.ContentType) || IsBulkPath(request.Path.Value) || IsSearchPath(request.Path.Value);

        if (!shouldReadBody)
        {
            return null;
        }

        request.EnableBuffering();
        var body = await ReadBodyAsync(request.Body);
        request.Body.Position = 0;

        var contentLength = request.ContentLength;
        var payloadBytes = contentLength ?? body.PayloadBytes;
        var truncated = contentLength.HasValue ? contentLength.Value > body.PayloadBytes : body.Truncated;

        return body with
        {
            IsJson = IsJson(request.ContentType),
            PayloadBytes = payloadBytes,
            Truncated = truncated
        };
    }

    private static async Task<BodySnapshot> ReadBodyAsync(Stream bodyStream)
    {
        using var reader = new StreamReader(bodyStream, Encoding.UTF8, detectEncodingFromByteOrderMarks: false, bufferSize: MaxBodyBytes, leaveOpen: true);
        var buffer = new char[MaxBodyBytes];
        var read = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
        var body = new string(buffer, 0, read);

        var readBytes = Encoding.UTF8.GetByteCount(body);
        var payloadBytes = bodyStream.CanSeek ? bodyStream.Length : readBytes;
        var truncated = bodyStream.CanSeek && bodyStream.Length > readBytes;

        return new BodySnapshot(body, payloadBytes, truncated, false);
    }

    private static bool IsJson(string? contentType)
    {
        return !string.IsNullOrWhiteSpace(contentType)
            && contentType.Contains("application/json", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBulkPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return false;
        }

        return BulkSummaryPaths.Any(summaryPath =>
            string.Equals(summaryPath, path, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsSearchPath(string? path)
    {
        return string.Equals(path, "/search", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Truncate(string? value, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return value.Length <= maxChars
            ? value
            : value[..maxChars];
    }

    private static Dictionary<string, object?> BuildBulkSummary(HttpContext context, BodySnapshot requestBody)
    {
        var summary = new Dictionary<string, object?>
        {
            ["payload_bytes"] = requestBody.PayloadBytes,
            ["truncated"] = requestBody.Truncated
        };

        var path = context.Request.Path.Value ?? string.Empty;
        if (string.Equals(path, "/products:importCsv", StringComparison.OrdinalIgnoreCase))
        {
            var csvSummary = SummarizeCsv(requestBody.Body ?? string.Empty);
            summary["csv_row_count"] = csvSummary.RowCount;
            summary["sample_product_ids"] = csvSummary.SampleProductIds;
            return summary;
        }

        var jsonSummary = SummarizeBulkJson(requestBody.Body ?? string.Empty);
        summary["document_count"] = jsonSummary.DocumentCount;
        summary["sample_product_ids"] = jsonSummary.SampleProductIds;

        return summary;
    }

    private static (int? DocumentCount, List<string> SampleProductIds) SummarizeBulkJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Array)
            {
                return (root.GetArrayLength(), ExtractSampleProductIds(root));
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                foreach (var propertyName in new[] { "documents", "items", "products" })
                {
                    if (root.TryGetProperty(propertyName, out var array) && array.ValueKind == JsonValueKind.Array)
                    {
                        return (array.GetArrayLength(), ExtractSampleProductIds(array));
                    }
                }
            }
        }
        catch
        {
            return (null, new List<string>());
        }

        return (null, new List<string>());
    }

    private static (int? RowCount, List<string> SampleProductIds) SummarizeCsv(string csv)
    {
        if (string.IsNullOrWhiteSpace(csv))
        {
            return (0, new List<string>());
        }

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length == 0)
        {
            return (0, new List<string>());
        }

        var startIndex = 0;
        if (lines[0].Contains("id", StringComparison.OrdinalIgnoreCase))
        {
            startIndex = 1;
        }

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
        return (rowCount, sampleIds);
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

    private sealed record BodySnapshot(string? Body, long PayloadBytes, bool Truncated, bool IsJson);
}
