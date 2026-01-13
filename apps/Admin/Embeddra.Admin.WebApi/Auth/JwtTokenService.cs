using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Embeddra.Admin.Domain;
using Microsoft.IdentityModel.Tokens;

using Embeddra.Admin.Application.Services;

namespace Embeddra.Admin.WebApi.Auth;

public sealed class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(JwtOptions options)
    {
        _options = options;
    }

    public (string Token, DateTimeOffset ExpiresAt) CreateToken(AdminUser user)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(_options.ExpiryMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(ClaimTypes.Name, user.Name),
            new(ClaimTypes.Role, user.Role)
        };

        if (!string.IsNullOrEmpty(user.TenantId))
        {
            claims.Add(new(AdminClaims.TenantId, user.TenantId));
        }

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expires.UtcDateTime,
            signingCredentials: credentials);

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.WriteToken(token);

        return (jwt, expires);
    }
}
