namespace Embeddra.Worker.Infrastructure.Persistence;

public sealed class TenantRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "active";
    public DateTimeOffset CreatedAt { get; set; }
}
