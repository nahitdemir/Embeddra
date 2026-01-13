using Embeddra.BuildingBlocks.Messaging;

namespace Embeddra.Admin.Application.Services;

public interface IIngestionJobPublisher
{
    Task PublishAsync(IngestionJobMessage message, string correlationId, CancellationToken cancellationToken);
}
