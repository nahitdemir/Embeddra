using System.Threading;

namespace Embeddra.BuildingBlocks.Correlation;

public static class CorrelationContext
{
    private static readonly AsyncLocal<string?> Current = new();

    public static string? CorrelationId
    {
        get => Current.Value;
        set => Current.Value = value;
    }
}
