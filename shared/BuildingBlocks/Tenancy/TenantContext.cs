using System.Threading;

namespace Embeddra.BuildingBlocks.Tenancy;

public static class TenantContext
{
    private static readonly AsyncLocal<string?> Current = new();

    public static string? TenantId
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}
