namespace Embeddra.Contracts;

/// <summary>
/// Defines valid API key types used across the application.
/// Shared kernel: referenced by both Domain and Infrastructure layers.
/// </summary>
public static class ApiKeyTypes
{
    public const string SearchPublic = "search_public";
    public const string Admin = "admin";

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return SearchPublic;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            Admin => Admin,
            SearchPublic => SearchPublic,
            _ => SearchPublic
        };
    }
}
