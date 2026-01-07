using System.Net;
using System.Text;
using System.Text.Json;
using Embeddra.Worker.Infrastructure.Embedding;
using Microsoft.Extensions.Logging;

namespace Embeddra.Worker.Infrastructure.Indexing;

public sealed class ElasticsearchIndexManager
{
    private const string TemplateName = "embeddra-products-template";
    private const int TemplatePriority = 200;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly EmbeddingOptions _embeddingOptions;
    private readonly ILogger<ElasticsearchIndexManager> _logger;
    private readonly SemaphoreSlim _templateLock = new(1, 1);
    private bool _templateEnsured;

    public ElasticsearchIndexManager(
        IHttpClientFactory httpClientFactory,
        EmbeddingOptions embeddingOptions,
        ILogger<ElasticsearchIndexManager> logger)
    {
        _httpClientFactory = httpClientFactory;
        _embeddingOptions = embeddingOptions;
        _logger = logger;
    }

    public async Task EnsureProductIndexAsync(string tenantId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
        {
            throw new InvalidOperationException("tenant_id is required for index creation.");
        }

        await EnsureTemplateAsync(cancellationToken);
        await EnsureAliasAsync(tenantId, cancellationToken);
    }

    private async Task EnsureTemplateAsync(CancellationToken cancellationToken)
    {
        if (_templateEnsured)
        {
            return;
        }

        await _templateLock.WaitAsync(cancellationToken);
        try
        {
            if (_templateEnsured)
            {
                return;
            }

            var client = _httpClientFactory.CreateClient("elasticsearch");
            var body = BuildTemplatePayload(_embeddingOptions.Dimensions);
            var content = new StringContent(body, Encoding.UTF8, "application/json");

            using var response = await client.PutAsync($"/_index_template/{TemplateName}", content, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new InvalidOperationException(
                    $"Failed to create index template: {(int)response.StatusCode} {responseBody}");
            }

            _templateEnsured = true;
        }
        finally
        {
            _templateLock.Release();
        }
    }

    private async Task EnsureAliasAsync(string tenantId, CancellationToken cancellationToken)
    {
        var aliasName = ElasticIndexNameResolver.ResolveProductIndexName(tenantId);
        if (string.IsNullOrWhiteSpace(aliasName))
        {
            throw new InvalidOperationException("Failed to resolve alias name.");
        }

        var client = _httpClientFactory.CreateClient("elasticsearch");

        if (await ResourceExistsAsync(client, $"/_alias/{aliasName}", cancellationToken))
        {
            return;
        }

        if (await ResourceExistsAsync(client, $"/{aliasName}", cancellationToken))
        {
            _logger.LogInformation(
                "elasticsearch_index_exists_without_alias {index_name}",
                aliasName);
            return;
        }

        var backingIndexName = $"{aliasName}-000001";
        if (await ResourceExistsAsync(client, $"/{backingIndexName}", cancellationToken))
        {
            await CreateAliasAsync(client, aliasName, backingIndexName, cancellationToken);
            return;
        }

        await CreateIndexWithAliasAsync(client, aliasName, backingIndexName, cancellationToken);
    }

    private static async Task<bool> ResourceExistsAsync(
        HttpClient client,
        string path,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, path);
        using var response = await client.SendAsync(request, cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }

        if (response.IsSuccessStatusCode)
        {
            return true;
        }

        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
        throw new InvalidOperationException(
            $"Unexpected Elasticsearch response {(int)response.StatusCode}: {responseBody}");
    }

    private static async Task CreateAliasAsync(
        HttpClient client,
        string aliasName,
        string backingIndexName,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["actions"] = new[]
            {
                new Dictionary<string, object?>
                {
                    ["add"] = new Dictionary<string, object?>
                    {
                        ["index"] = backingIndexName,
                        ["alias"] = aliasName,
                        ["is_write_index"] = true
                    }
                }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await client.PostAsync("/_aliases", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Failed to create alias {aliasName}: {(int)response.StatusCode} {responseBody}");
        }
    }

    private static async Task CreateIndexWithAliasAsync(
        HttpClient client,
        string aliasName,
        string backingIndexName,
        CancellationToken cancellationToken)
    {
        var payload = new Dictionary<string, object?>
        {
            ["aliases"] = new Dictionary<string, object?>
            {
                [aliasName] = new Dictionary<string, object?>
                {
                    ["is_write_index"] = true
                }
            }
        };

        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await client.PutAsync($"/{backingIndexName}", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException(
                $"Failed to create index {backingIndexName}: {(int)response.StatusCode} {responseBody}");
        }
    }

    private static string BuildTemplatePayload(int embeddingDimensions)
    {
        var template = new Dictionary<string, object?>
        {
            ["index_patterns"] = new[] { "products-*" },
            ["priority"] = TemplatePriority,
            ["template"] = new Dictionary<string, object?>
            {
                ["settings"] = new Dictionary<string, object?>
                {
                    ["number_of_shards"] = 1,
                    ["number_of_replicas"] = 0
                },
                ["mappings"] = new Dictionary<string, object?>
                {
                    ["dynamic"] = true,
                    ["properties"] = new Dictionary<string, object?>
                    {
                        ["tenant_id"] = new Dictionary<string, object?> { ["type"] = "keyword" },
                        ["product_id"] = new Dictionary<string, object?> { ["type"] = "keyword" },
                        ["name"] = new Dictionary<string, object?>
                        {
                            ["type"] = "text",
                            ["fields"] = new Dictionary<string, object?>
                            {
                                ["keyword"] = new Dictionary<string, object?>
                                {
                                    ["type"] = "keyword",
                                    ["ignore_above"] = 256
                                }
                            }
                        },
                        ["description"] = new Dictionary<string, object?> { ["type"] = "text" },
                        ["brand"] = new Dictionary<string, object?> { ["type"] = "keyword" },
                        ["category"] = new Dictionary<string, object?> { ["type"] = "keyword" },
                        ["price"] = new Dictionary<string, object?> { ["type"] = "float" },
                        ["in_stock"] = new Dictionary<string, object?> { ["type"] = "boolean" },
                        ["attributes"] = new Dictionary<string, object?> { ["type"] = "flattened" },
                        ["embedding"] = new Dictionary<string, object?>
                        {
                            ["type"] = "dense_vector",
                            ["dims"] = embeddingDimensions,
                            ["index"] = true,
                            ["similarity"] = "cosine"
                        }
                    }
                }
            },
            ["_meta"] = new Dictionary<string, object?>
            {
                ["managed_by"] = "embeddra-worker",
                ["version"] = 1
            }
        };

        return JsonSerializer.Serialize(template);
    }
}
