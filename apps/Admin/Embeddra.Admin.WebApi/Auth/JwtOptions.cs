using Microsoft.Extensions.Configuration;

namespace Embeddra.Admin.WebApi.Auth;

public sealed class JwtOptions
{
    public string Issuer { get; init; } = "embeddra-admin";
    public string Audience { get; init; } = "embeddra-admin";
    public string SigningKey { get; init; } = "dev-signing-key-change-me";
    public int ExpiryMinutes { get; init; } = 720;

    public static JwtOptions FromConfiguration(IConfiguration configuration)
    {
        var section = configuration.GetSection("Admin:Jwt");
        return new JwtOptions
        {
            Issuer = section["Issuer"] ?? "embeddra-admin",
            Audience = section["Audience"] ?? "embeddra-admin",
            SigningKey = section["SigningKey"] ?? "dev-signing-key-change-me",
            ExpiryMinutes = int.TryParse(section["ExpiryMinutes"], out var minutes) ? minutes : 720
        };
    }
}
