using System.Text.Json;
using Embeddra.Admin.Application.Repositories;
using Embeddra.Admin.Domain;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Embeddra.Admin.Application.Services.Implementations;

/// <summary>
/// Tenant Settings servis implementasyonu (caching + validation).
/// </summary>
public sealed class TenantSettingsService : ITenantSettingsService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private readonly ITenantSettingsRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TenantSettingsService> _logger;

    public TenantSettingsService(
        ITenantSettingsRepository repository,
        IMemoryCache cache,
        ILogger<TenantSettingsService> logger)
    {
        _repository = repository;
        _cache = cache;
        _logger = logger;
    }

    public async Task<T?> GetSettingAsync<T>(string tenantId, string key, T? defaultValue = default, CancellationToken cancellationToken = default) where T : notnull
    {
        var cacheKey = GetCacheKey(tenantId, key);
        if (_cache.TryGetValue(cacheKey, out T? cached))
        {
            return cached;
        }

        var setting = await _repository.GetAsync(tenantId, key, cancellationToken);
        if (setting == null)
        {
            _cache.Set(cacheKey, defaultValue, CacheDuration);
            return defaultValue;
        }

        try
        {
            var value = DeserializeValue<T>(setting.Value, setting.Type);
            _cache.Set(cacheKey, value, CacheDuration);
            return value;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Setting deserialize failed: {TenantId} {Key}", tenantId, key);
            _cache.Set(cacheKey, defaultValue, CacheDuration);
            return defaultValue;
        }
    }

    public async Task<string?> GetSettingStringAsync(string tenantId, string key, string? defaultValue = null, CancellationToken cancellationToken = default)
    {
        return await GetSettingAsync<string>(tenantId, key, defaultValue ?? string.Empty, cancellationToken);
    }

    public async Task<int?> GetSettingIntAsync(string tenantId, string key, int? defaultValue = null, CancellationToken cancellationToken = default)
    {
        var value = await GetSettingStringAsync(tenantId, key, null, cancellationToken);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        // Integer parse dene
        if (int.TryParse(value, out var parsedInt))
        {
            return parsedInt;
        }

        // Double parse dene (ondalıklı sayılar için)
        if (double.TryParse(value, out var parsedDouble))
        {
            return (int)parsedDouble;
        }

        _logger.LogWarning("Setting parse failed (int): {TenantId} {Key} {Value}", tenantId, key, value);
        return defaultValue;
    }

    public async Task<bool> GetSettingBoolAsync(string tenantId, string key, bool defaultValue = false, CancellationToken cancellationToken = default)
    {
        var value = await GetSettingStringAsync(tenantId, key, null, cancellationToken);
        if (string.IsNullOrWhiteSpace(value))
        {
            return defaultValue;
        }

        if (bool.TryParse(value, out var parsed))
        {
            return parsed;
        }

        // "true", "1", "yes", "on" gibi değerleri de kabul et
        var normalized = value.Trim().ToLowerInvariant();
        if (normalized == "true" || normalized == "1" || normalized == "yes" || normalized == "on")
        {
            return true;
        }

        if (normalized == "false" || normalized == "0" || normalized == "no" || normalized == "off")
        {
            return false;
        }

        _logger.LogWarning("Setting parse failed (bool): {TenantId} {Key} {Value}", tenantId, key, value);
        return defaultValue;
    }

    public async Task<IReadOnlyDictionary<string, string>> GetAllSettingsAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetAllCacheKey(tenantId);
        if (_cache.TryGetValue(cacheKey, out IReadOnlyDictionary<string, string>? cached))
        {
            return cached ?? new Dictionary<string, string>();
        }

        var settings = await _repository.GetAllByTenantAsync(tenantId, cancellationToken);
        var dictionary = settings.ToDictionary(s => s.Key, s => s.Value, StringComparer.OrdinalIgnoreCase);
        _cache.Set(cacheKey, dictionary, CacheDuration);
        return dictionary;
    }

    public async Task SetSettingAsync(string tenantId, string key, string value, string? description = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("TenantId gerekli.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Key gerekli.", nameof(key));
        if (value == null)
            throw new ArgumentNullException(nameof(value));

        var exists = await _repository.ExistsAsync(tenantId, key, cancellationToken);
        if (exists)
        {
            var setting = await _repository.GetAsync(tenantId, key, cancellationToken);
            if (setting != null)
            {
                setting.UpdateValue(value, description);
                await _repository.UpdateAsync(setting, cancellationToken);
            }
        }
        else
        {
            // Type'ı otomatik tespit et
            var type = InferSettingType(value);
            var newSetting = TenantSetting.Create(tenantId, key, value, type, description);
            await _repository.AddAsync(newSetting, cancellationToken);
        }

        InvalidateCache(tenantId);
    }

    public async Task DeleteSettingAsync(string tenantId, string key, CancellationToken cancellationToken = default)
    {
        await _repository.DeleteAsync(tenantId, key, cancellationToken);
        InvalidateCache(tenantId);
    }

    public void InvalidateCache(string tenantId)
    {
        // Tüm tenant settings cache'ini temizle
        var allCacheKey = GetAllCacheKey(tenantId);
        _cache.Remove(allCacheKey);

        // Individual cache key'leri temizlemek için pattern matching kullanılamaz
        // Bu yüzden sadece GetAllCacheKey'i temizliyoruz
        // Individual key'ler TTL ile otomatik expire olacak
    }

    private static string GetCacheKey(string tenantId, string key)
    {
        return $"tenant_settings:{tenantId}:{key}";
    }

    private static string GetAllCacheKey(string tenantId)
    {
        return $"tenant_settings_all:{tenantId}";
    }

    private static T DeserializeValue<T>(string value, SettingType type) where T : notnull
    {
        return type switch
        {
            SettingType.String => (T)(object)value,
            SettingType.Number => (T)(object)int.Parse(value),
            SettingType.Boolean => (T)(object)bool.Parse(value),
            SettingType.Json => JsonSerializer.Deserialize<T>(value) ?? throw new InvalidOperationException($"JSON deserialize failed: {value}"),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unknown setting type")
        };
    }

    private static SettingType InferSettingType(string value)
    {
        if (bool.TryParse(value, out _))
        {
            return SettingType.Boolean;
        }

        if (int.TryParse(value, out _) || double.TryParse(value, out _))
        {
            return SettingType.Number;
        }

        if (value.TrimStart().StartsWith("{") || value.TrimStart().StartsWith("["))
        {
            try
            {
                JsonSerializer.Deserialize<object>(value);
                return SettingType.Json;
            }
            catch
            {
                // JSON değil, string olarak kabul et
            }
        }

        return SettingType.String;
    }
}
