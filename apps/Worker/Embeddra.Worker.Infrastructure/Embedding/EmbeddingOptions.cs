using Microsoft.Extensions.Configuration;

namespace Embeddra.Worker.Infrastructure.Embedding;

public sealed class EmbeddingOptions
{
    public int Dimensions { get; set; } = 384;

    public static EmbeddingOptions FromConfiguration(IConfiguration configuration)
    {
        var options = new EmbeddingOptions();
        var dimensions = configuration.GetValue<int?>("Embedding:Dimensions")
            ?? configuration.GetValue<int?>("Embedding:Dims");

        if (dimensions.HasValue && dimensions.Value > 0)
        {
            options.Dimensions = dimensions.Value;
        }

        return options;
    }
}
