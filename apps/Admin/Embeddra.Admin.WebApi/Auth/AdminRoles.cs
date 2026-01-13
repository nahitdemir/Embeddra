using Embeddra.Admin.Domain;

namespace Embeddra.Admin.WebApi.Auth;

public static class AdminRoles
{
    public const string PlatformOwner = UserRole.PlatformOwner;
    public const string TenantOwner = UserRole.TenantOwner;

    public static readonly IReadOnlyCollection<string> TenantWriteRoles = new[] { TenantOwner };
    public static readonly IReadOnlyCollection<string> TenantReadRoles = new[] { TenantOwner };
}
