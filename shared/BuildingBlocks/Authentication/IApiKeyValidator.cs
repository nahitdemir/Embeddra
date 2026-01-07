namespace Embeddra.BuildingBlocks.Authentication;

public interface IApiKeyValidator
{
    Task<ApiKeyValidationResult?> ValidateAsync(string apiKey, CancellationToken cancellationToken);
}
