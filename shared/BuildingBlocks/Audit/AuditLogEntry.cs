namespace Embeddra.BuildingBlocks.Audit;

public sealed record AuditLogEntry(
    string Action,
    string Actor,
    object? Payload,
    string? TenantId = null,
    string? CorrelationId = null);
