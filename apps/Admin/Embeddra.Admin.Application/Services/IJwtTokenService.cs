using Embeddra.Admin.Domain;

namespace Embeddra.Admin.Application.Services;

public interface IJwtTokenService
{
    (string Token, DateTimeOffset ExpiresAt) CreateToken(AdminUser user);
}
