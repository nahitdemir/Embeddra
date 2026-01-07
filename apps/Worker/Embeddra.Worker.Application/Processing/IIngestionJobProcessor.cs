using Embeddra.BuildingBlocks.Messaging;

namespace Embeddra.Worker.Application.Processing;

public interface IIngestionJobProcessor
{
    Task<IngestionJobProcessingResult> ProcessAsync(
        IngestionJobMessage message,
        int retryCount,
        CancellationToken cancellationToken);
}
