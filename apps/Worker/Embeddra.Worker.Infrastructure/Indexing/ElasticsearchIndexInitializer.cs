using Embeddra.Worker.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Embeddra.Worker.Infrastructure.Indexing;

public sealed class ElasticsearchIndexInitializer : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ElasticsearchIndexManager _indexManager;
    private readonly ILogger<ElasticsearchIndexInitializer> _logger;

    public ElasticsearchIndexInitializer(
        IServiceScopeFactory scopeFactory,
        ElasticsearchIndexManager indexManager,
        ILogger<ElasticsearchIndexInitializer> logger)
    {
        _scopeFactory = scopeFactory;
        _indexManager = indexManager;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<IngestionDbContext>();
            var tenantIds = await dbContext.Tenants
                .AsNoTracking()
                .Where(x => x.Status == "active")
                .Select(x => x.Id)
                .ToListAsync(cancellationToken);

            if (tenantIds.Count == 0)
            {
                return;
            }

            foreach (var tenantId in tenantIds)
            {
                await _indexManager.EnsureProductIndexAsync(tenantId, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "elasticsearch_index_initializer_failed");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
