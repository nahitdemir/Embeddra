using System.Text;

namespace Embeddra.BuildingBlocks.Messaging;

public static class RabbitMqHeaders
{
    public const string RetryCountHeader = "X-Retry-Count";

    public static int GetRetryCount(IDictionary<string, object>? headers)
    {
        if (headers is null)
        {
            return 0;
        }

        if (!headers.TryGetValue(RetryCountHeader, out var value) || value is null)
        {
            return 0;
        }

        return value switch
        {
            int count => count,
            long count => (int)count,
            short count => count,
            sbyte count => count,
            byte count => count,
            string text => int.TryParse(text, out var parsed) ? parsed : 0,
            byte[] bytes => ParseBytes(bytes),
            ReadOnlyMemory<byte> memory => ParseBytes(memory.Span),
            _ => int.TryParse(value.ToString(), out var parsed) ? parsed : 0
        };
    }

    public static void SetRetryCount(IDictionary<string, object> headers, int retryCount)
    {
        headers[RetryCountHeader] = retryCount;
    }

    private static int ParseBytes(ReadOnlySpan<byte> bytes)
    {
        var text = Encoding.UTF8.GetString(bytes);
        return int.TryParse(text, out var parsed) ? parsed : 0;
    }
}
