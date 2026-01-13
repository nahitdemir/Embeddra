using Embeddra.Admin.Application.Repositories;
using Embeddra.Admin.Application.Services;
using Embeddra.Admin.Domain;
using Embeddra.BuildingBlocks.Authentication;
using Embeddra.Contracts;

namespace Embeddra.Admin.Application.Services.Implementations;

public sealed class ApiKeyService : IApiKeyService
{
    private readonly IApiKeyRepository _apiKeyRepository;

    public ApiKeyService(IApiKeyRepository apiKeyRepository)
    {
        _apiKeyRepository = apiKeyRepository;
    }

    public async Task<CreateApiKeyResult> CreateApiKeyAsync(CreateApiKeyRequest request, CancellationToken cancellationToken = default)
    {
        var (apiKey, keyHash, keyPrefix) = await GenerateUniqueApiKeyAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new CreateApiKeyResult(false, Error: "api_key_generation_failed");
        }

        var entity = ApiKey.Create(
            request.TenantId,
            request.Name,
            keyHash,
            keyPrefix,
            request.Description,
            request.KeyType,
            request.AllowedOrigins);

        await _apiKeyRepository.AddAsync(entity, cancellationToken);

        return new CreateApiKeyResult(true, entity.Id, apiKey, keyPrefix);
    }

    public async Task<bool> RevokeApiKeyAsync(string tenantId, Guid apiKeyId, CancellationToken cancellationToken = default)
    {
        var apiKey = await _apiKeyRepository.GetByIdAsync(apiKeyId, cancellationToken);
        if (apiKey is null || apiKey.TenantId != tenantId)
        {
            return false;
        }

        if (!apiKey.IsRevoked)
        {
            apiKey.Revoke();
            await _apiKeyRepository.UpdateAsync(apiKey, cancellationToken);
        }

        return true;
    }

    public async Task<IReadOnlyList<ApiKey>> GetApiKeysByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _apiKeyRepository.GetByTenantAsync(tenantId, cancellationToken);
    }

    private async Task<(string ApiKey, string KeyHash, string KeyPrefix)> GenerateUniqueApiKeyAsync(
        CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < 3; attempt++)
        {
            var apiKey = GenerateApiKey();
            var keyHash = ApiKeyHasher.ComputeHash(apiKey);

            var exists = await _apiKeyRepository.KeyHashExistsAsync(keyHash, cancellationToken);
            if (!exists)
            {
                var keyPrefix = ApiKeyHasher.ComputePrefix(apiKey);
                return (apiKey, keyHash, keyPrefix);
            }
        }

        return (string.Empty, string.Empty, string.Empty);
    }

    private static string GenerateApiKey()
    {
        var bytes = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes);
        return token.Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }
}
