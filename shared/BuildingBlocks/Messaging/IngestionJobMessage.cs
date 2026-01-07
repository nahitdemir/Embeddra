using System.Text.Json.Serialization;

namespace Embeddra.BuildingBlocks.Messaging;

public sealed record IngestionJobMessage
{
    [JsonPropertyName("job_id")]
    public string? JobId { get; init; }

    [JsonPropertyName("tenant_id")]
    public string? TenantId { get; init; }

    [JsonPropertyName("source_type")]
    public string? SourceType { get; init; }

    [JsonPropertyName("count")]
    public int Count { get; init; }
}
