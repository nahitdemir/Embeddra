namespace Embeddra.Admin.Application.Services;

/// <summary>
/// Tenant Settings servis interface'i.
/// </summary>
public interface ITenantSettingsService
{
    /// <summary>
    /// Setting değerini getirir (cached). Setting yoksa default değer döner.
    /// </summary>
    Task<T?> GetSettingAsync<T>(string tenantId, string key, T? defaultValue = default, CancellationToken cancellationToken = default) where T : notnull;

    /// <summary>
    /// Setting değerini string olarak getirir (cached).
    /// </summary>
    Task<string?> GetSettingStringAsync(string tenantId, string key, string? defaultValue = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Setting değerini number olarak getirir (cached).
    /// </summary>
    Task<int?> GetSettingIntAsync(string tenantId, string key, int? defaultValue = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Setting değerini boolean olarak getirir (cached).
    /// </summary>
    Task<bool> GetSettingBoolAsync(string tenantId, string key, bool defaultValue = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tüm tenant settings'leri getirir.
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> GetAllSettingsAsync(string tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Setting değerini set eder (cache'i invalidate eder).
    /// </summary>
    Task SetSettingAsync(string tenantId, string key, string value, string? description = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Setting'i siler (cache'i invalidate eder).
    /// </summary>
    Task DeleteSettingAsync(string tenantId, string key, CancellationToken cancellationToken = default);

    /// <summary>
    /// Cache'i invalidate eder (tüm tenant settings için).
    /// </summary>
    void InvalidateCache(string tenantId);
}
