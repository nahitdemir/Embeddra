namespace Embeddra.Admin.Application.Settings;

/// <summary>
/// Tenant settings key constants.
/// </summary>
public static class SettingKeys
{
    // Search settings
    public const string SearchMaxResults = "search.max_results";
    public const string SearchTimeoutMs = "search.timeout_ms";
    public const string EnableFuzzySearch = "search.enable_fuzzy";
    public const string SearchDefaultLimit = "search.default_limit";

    // Ingestion settings
    public const string MaxProductsPerTenant = "ingestion.max_products";
    public const string AutoIndexEnabled = "ingestion.auto_index";
    public const string IngestionBatchSize = "ingestion.batch_size";

    // Feature flags
    public const string EnableAnalytics = "features.analytics";
    public const string EnableAITuning = "features.ai_tuning";
    public const string EnableSearchPreview = "features.search_preview";

    // Rate limiting
    public const string ApiRateLimit = "rate_limit.api_per_minute";
    public const string SearchRateLimit = "rate_limit.search_per_minute";

    // UI/Customization
    public const string BrandName = "ui.brand_name";
    public const string BrandColor = "ui.brand_color";

    // Maintenance
    public const string MaintenanceMode = "maintenance.mode";
    public const string MaintenanceMessage = "maintenance.message";
}
