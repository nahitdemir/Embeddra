using System.Security.Cryptography;
using System.Text;
using Embeddra.Search.Application.Embedding;

namespace Embeddra.Search.Infrastructure.Embedding;

public sealed class DeterministicEmbeddingClient : IEmbeddingClient
{
    private readonly EmbeddingOptions _options;

    public DeterministicEmbeddingClient(EmbeddingOptions options)
    {
        _options = options;
    }

    public Task<IReadOnlyList<float[]>> EmbedAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken)
    {
        if (texts.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<float[]>>(Array.Empty<float[]>());
        }

        var results = new float[texts.Count][];
        using var hasher = SHA256.Create();

        for (var i = 0; i < texts.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            results[i] = BuildVector(hasher, texts[i] ?? string.Empty, _options.Dimensions);
        }

        return Task.FromResult<IReadOnlyList<float[]>>(results);
    }

    private static float[] BuildVector(HashAlgorithm hasher, string text, int dimensions)
    {
        var bytes = hasher.ComputeHash(Encoding.UTF8.GetBytes(text));
        var vector = new float[dimensions];

        for (var i = 0; i < vector.Length; i++)
        {
            var value = bytes[i % bytes.Length];
            vector[i] = (value - 128f) / 128f;
        }

        return vector;
    }
}
