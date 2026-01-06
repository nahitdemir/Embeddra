using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Embeddra.BuildingBlocks.Logging;

public static class SensitiveDataMasker
{
    private static readonly HashSet<string> SensitiveKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "apiKey",
        "token",
        "authorization",
        "password",
        "secret"
    };

    public static IDictionary<string, string?> MaskHeaders(IHeaderDictionary headers)
    {
        var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in headers)
        {
            var value = header.Value.ToString();
            result[header.Key] = IsSensitive(header.Key) ? "***" : value;
        }

        return result;
    }

    public static string MaskJson(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                WriteElement(document.RootElement, writer);
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }
        catch
        {
            return "[unparseable json]";
        }
    }

    public static string MaskObject(object? payload)
    {
        if (payload is null)
        {
            return "{}";
        }

        try
        {
            var json = JsonSerializer.Serialize(payload);
            return MaskJson(json);
        }
        catch
        {
            return "[unserializable payload]";
        }
    }

    private static bool IsSensitive(string key)
    {
        return SensitiveKeys.Contains(key);
    }

    private static void WriteElement(JsonElement element, Utf8JsonWriter writer)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    if (IsSensitive(property.Name))
                    {
                        writer.WriteStringValue("***");
                    }
                    else
                    {
                        WriteElement(property.Value, writer);
                    }
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    WriteElement(item, writer);
                }
                writer.WriteEndArray();
                break;
            case JsonValueKind.String:
                writer.WriteStringValue(element.GetString());
                break;
            case JsonValueKind.Number:
                if (element.TryGetInt64(out var longValue))
                {
                    writer.WriteNumberValue(longValue);
                }
                else if (element.TryGetDouble(out var doubleValue))
                {
                    writer.WriteNumberValue(doubleValue);
                }
                else
                {
                    writer.WriteRawValue(element.GetRawText());
                }
                break;
            case JsonValueKind.True:
                writer.WriteBooleanValue(true);
                break;
            case JsonValueKind.False:
                writer.WriteBooleanValue(false);
                break;
            case JsonValueKind.Null:
                writer.WriteNullValue();
                break;
            default:
                writer.WriteStringValue(element.GetRawText());
                break;
        }
    }
}
