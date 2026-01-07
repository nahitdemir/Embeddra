using System.Text;

namespace Embeddra.Worker.Infrastructure.Indexing;

public static class ElasticIndexNameResolver
{
    private const string IndexPrefix = "products";

    public static string ResolveProductIndexName(string tenantId)
    {
        var normalized = NormalizeIndexSegment(tenantId);
        return string.IsNullOrWhiteSpace(normalized)
            ? $"{IndexPrefix}-default"
            : $"{IndexPrefix}-{normalized}";
    }

    private static string NormalizeIndexSegment(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var trimmed = input.Trim().ToLowerInvariant();
        var builder = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (ch is >= 'a' and <= 'z' || ch is >= '0' and <= '9' || ch == '-' || ch == '_')
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('-');
            }
        }

        var sanitized = builder.ToString().Trim('-', '_');
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return string.Empty;
        }

        return sanitized.Length > 60 ? sanitized[..60] : sanitized;
    }
}
