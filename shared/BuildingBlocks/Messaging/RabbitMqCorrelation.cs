namespace Embeddra.BuildingBlocks.Messaging;

public static class RabbitMqCorrelation
{
    private const string CorrelationHeader = "X-Correlation-Id";

    public static void SetCorrelationId(IDictionary<string, object> headers, string correlationId)
    {
        headers[CorrelationHeader] = correlationId;
    }

    public static string? GetCorrelationId(IDictionary<string, object> headers)
    {
        if (headers.TryGetValue(CorrelationHeader, out var value) && value is not null)
        {
            return value.ToString();
        }

        return null;
    }
}
