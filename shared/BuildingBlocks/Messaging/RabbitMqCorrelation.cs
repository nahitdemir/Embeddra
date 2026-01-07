using System.Text;

namespace Embeddra.BuildingBlocks.Messaging;

public static class RabbitMqCorrelation
{
    private const string CorrelationHeader = "X-Correlation-Id";

    public static void SetCorrelationId(IDictionary<string, object> headers, string correlationId)
    {
        headers[CorrelationHeader] = correlationId;
    }

    public static string? GetCorrelationId(IDictionary<string, object>? headers)
    {
        if (headers is null)
        {
            return null;
        }

        if (headers.TryGetValue(CorrelationHeader, out var value) && value is not null)
        {
            return value switch
            {
                byte[] bytes => Encoding.UTF8.GetString(bytes),
                ReadOnlyMemory<byte> memory => Encoding.UTF8.GetString(memory.Span),
                string text => text,
                _ => value.ToString()
            };
        }

        return null;
    }
}
