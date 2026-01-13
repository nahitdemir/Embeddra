using Embeddra.Admin.Application.Repositories;
using Embeddra.Admin.Application.Services;
using Embeddra.Admin.Domain;
using Microsoft.AspNetCore.Identity;

namespace Embeddra.Admin.Application.Services.Implementations;

public sealed class UserService : IUserService
{
    private readonly IAdminUserRepository _userRepository;
    private readonly IPasswordHasher<AdminUser> _passwordHasher;
    private readonly IJwtTokenService _tokenService;

    public UserService(
        IAdminUserRepository userRepository,
        IPasswordHasher<AdminUser> passwordHasher,
        IJwtTokenService tokenService)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
    }

    public async Task<LoginResult> LoginAsync(string? tenantId, string email, string password, CancellationToken cancellationToken = default)
    {
        // If tenantId is provided, perform traditional single-tenant login
        if (!string.IsNullOrEmpty(tenantId))
        {
            var user = await _userRepository.FindByEmailAsync(tenantId, email, cancellationToken);
            if (user is null) return new LoginResult(false, Error: "invalid_credentials");
            if (!user.IsActive) return new LoginResult(false, Error: "user_disabled");

            var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
            if (verification == PasswordVerificationResult.Failed) return new LoginResult(false, Error: "invalid_credentials");

            if (verification == PasswordVerificationResult.SuccessRehashNeeded)
                user.UpdatePasswordHash(_passwordHasher.HashPassword(user, password));

            user.RecordLogin();
            await _userRepository.UpdateAsync(user, cancellationToken);

            var (token, expiresAt) = _tokenService.CreateToken(user);
            return new LoginResult(true, Token: token, ExpiresAt: expiresAt, User: user);
        }

        // Search across all tenants if tenantId is missing
        var allUsers = await _userRepository.FindAllByEmailAsync(email, cancellationToken);
        if (allUsers.Count == 0) return new LoginResult(false, Error: "invalid_credentials");

        var validUsers = new List<AdminUser>();
        foreach (var u in allUsers)
        {
            if (!u.IsActive) continue;
            var verify = _passwordHasher.VerifyHashedPassword(u, u.PasswordHash, password);
            if (verify != PasswordVerificationResult.Failed)
            {
                validUsers.Add(u);
            }
        }

        if (validUsers.Count == 0) return new LoginResult(false, Error: "invalid_credentials");

        if (validUsers.Count == 1)
        {
            var user = validUsers[0];
            user.RecordLogin();
            await _userRepository.UpdateAsync(user, cancellationToken);
            var (token, expiresAt) = _tokenService.CreateToken(user);
            return new LoginResult(true, Token: token, ExpiresAt: expiresAt, User: user);
        }

        // Multiple tenants found: Return the list so UI can show selection
        var tenantSummaries = validUsers.Select(u => new UserTenantSummary(u.TenantId, u.Email, u.Role)).ToList();
        return new LoginResult(true, Tenants: tenantSummaries);
    }

    public async Task<IReadOnlyList<UserTenantSummary>> GetUserTenantsAsync(string email, CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.FindAllByEmailAsync(email, cancellationToken);
        return users.Select(u => new UserTenantSummary(u.TenantId, u.Email, u.Role)).ToList();
    }

    public async Task<CreateUserResult> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default)
    {
        var exists = await _userRepository.ExistsAsync(request.TenantId, request.Email, cancellationToken);
        if (exists)
        {
            return new CreateUserResult(false, Error: "user_exists");
        }

        // Dummy user for hashing (if needed by hasher implementation) or just static hash
        // Using temp user as consistent with controller logic
        var tempUser = AdminUser.Create(request.TenantId, request.Email, request.Name, "temp", request.Role);
        var hash = _passwordHasher.HashPassword(tempUser, request.Password);

        var user = AdminUser.Create(request.TenantId, request.Email, request.Name, hash, request.Role);
        
        await _userRepository.AddAsync(user, cancellationToken);

        return new CreateUserResult(true, User: user);
    }

    public async Task<AdminUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _userRepository.GetByIdAsync(userId, cancellationToken);
    }

    public async Task<IReadOnlyList<AdminUser>> GetUsersByTenantAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        return await _userRepository.GetByTenantAsync(tenantId, cancellationToken);
    }
    public async Task<AdminUser?> UpdateUserAsync(
        Guid userId, 
        string? tenantId, 
        string? name, 
        string? role, 
        string? status, 
        string? password, 
        bool isPlatformAdmin)
    {
        var user = await _userRepository.GetByIdAsync(userId);
        if (user is null)
        {
            return null;
        }

        // Security check: If not platform admin, user must belong to the requested tenant
        if (!isPlatformAdmin && user.TenantId != tenantId)
        {
            return null;
        }

        if (name != null || role != null)
        {
            user.Update(name ?? user.Name, role ?? user.Role);
        }

        if (status != null)
        {
            if (string.Equals(status, "active", StringComparison.OrdinalIgnoreCase))
            {
                user.Enable();
            }
            else
            {
                user.Disable();
            }
        }

        if (password != null)
        {
            user.UpdatePasswordHash(_passwordHasher.HashPassword(user, password));
        }

        await _userRepository.UpdateAsync(user);
        return user;
    }
}
