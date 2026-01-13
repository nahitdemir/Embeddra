using Embeddra.Admin.Domain;

namespace Embeddra.Admin.Application.Services;

/// <summary>
/// API Key oluşturma isteği.
/// </summary>
public sealed record CreateApiKeyRequest(
    string TenantId,
    string Name,
    string? Description,
    string? KeyType,
    string[]? AllowedOrigins);

/// <summary>
/// API Key oluşturma sonucu.
/// </summary>
public sealed record CreateApiKeyResult(
    bool Success,
    Guid? ApiKeyId = null,
    string? PlainTextKey = null,
    string? KeyPrefix = null,
    string? Error = null);

/// <summary>
/// API Key servis interface'i.
/// </summary>
public interface IApiKeyService
{
    /// <summary>
    /// Yeni API anahtarı oluşturur.
    /// </summary>
    Task<CreateApiKeyResult> CreateApiKeyAsync(
        CreateApiKeyRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// API anahtarını iptal eder.
    /// </summary>
    Task<bool> RevokeApiKeyAsync(
        string tenantId,
        Guid apiKeyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Tenant'ın API anahtarlarını getirir.
    /// </summary>
    Task<IReadOnlyList<ApiKey>> GetApiKeysByTenantAsync(
        string tenantId,
        CancellationToken cancellationToken = default);
}
