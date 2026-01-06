namespace Embeddra.BuildingBlocks.Audit;

public sealed class AuditLogEntity
{
    public Guid Id { get; set; }
    public string? TenantId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Actor { get; set; }
    public string? CorrelationId { get; set; }
    public string PayloadJson { get; set; } = "{}";
    public DateTimeOffset CreatedAt { get; set; }
}
