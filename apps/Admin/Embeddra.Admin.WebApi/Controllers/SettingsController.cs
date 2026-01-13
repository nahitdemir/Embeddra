using Embeddra.Admin.Application.Services;
using Embeddra.Admin.WebApi.Auth;
using Embeddra.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Mvc;

namespace Embeddra.Admin.WebApi.Controllers;

[ApiController]
public sealed class SettingsController : ControllerBase
{
    private readonly ITenantSettingsService _settingsService;

    public SettingsController(ITenantSettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    /// <summary>
    /// Tüm tenant settings'leri getirir.
    /// </summary>
    [HttpGet("settings")]
    public async Task<IActionResult> GetAllSettings(
        [FromQuery] string? tenantId,
        CancellationToken cancellationToken)
    {
        if (!AdminAuthContext.CanTenantRead(HttpContext))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        var resolvedTenant = ResolveTenant(AdminAuthContext.Get(HttpContext).IsPlatform, tenantId);
        if (string.IsNullOrWhiteSpace(resolvedTenant))
        {
            return BadRequest(new { error = "tenant_required" });
        }

        var settings = await _settingsService.GetAllSettingsAsync(resolvedTenant, cancellationToken);
        return Ok(new { settings });
    }

    /// <summary>
    /// Belirli bir setting değerini getirir.
    /// </summary>
    [HttpGet("settings/{key}")]
    public async Task<IActionResult> GetSetting(
        string key,
        [FromQuery] string? tenantId,
        CancellationToken cancellationToken)
    {
        if (!AdminAuthContext.CanTenantRead(HttpContext))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        var resolvedTenant = ResolveTenant(AdminAuthContext.Get(HttpContext).IsPlatform, tenantId);
        if (string.IsNullOrWhiteSpace(resolvedTenant))
        {
            return BadRequest(new { error = "tenant_required" });
        }

        var value = await _settingsService.GetSettingStringAsync(resolvedTenant, key, null, cancellationToken);
        if (value == null)
        {
            return NotFound(new { error = "setting_not_found", key });
        }

        return Ok(new { key, value });
    }

    /// <summary>
    /// Setting değerini set eder veya günceller.
    /// </summary>
    [HttpPut("settings/{key}")]
    public async Task<IActionResult> SetSetting(
        string key,
        [FromBody] SetSettingRequest request,
        [FromQuery] string? tenantId,
        CancellationToken cancellationToken)
    {
        if (!AdminAuthContext.CanTenantWrite(HttpContext))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        var resolvedTenant = ResolveTenant(AdminAuthContext.Get(HttpContext).IsPlatform, tenantId);
        if (string.IsNullOrWhiteSpace(resolvedTenant))
        {
            return BadRequest(new { error = "tenant_required" });
        }

        if (string.IsNullOrWhiteSpace(request.Value))
        {
            return BadRequest(new { error = "value_required" });
        }

        await _settingsService.SetSettingAsync(resolvedTenant, key, request.Value, request.Description, cancellationToken);
        return Ok(new { key, value = request.Value, message = "Setting updated successfully" });
    }

    /// <summary>
    /// Setting'i siler.
    /// </summary>
    [HttpDelete("settings/{key}")]
    public async Task<IActionResult> DeleteSetting(
        string key,
        [FromQuery] string? tenantId,
        CancellationToken cancellationToken)
    {
        if (!AdminAuthContext.CanTenantWrite(HttpContext))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "forbidden" });
        }

        var resolvedTenant = ResolveTenant(AdminAuthContext.Get(HttpContext).IsPlatform, tenantId);
        if (string.IsNullOrWhiteSpace(resolvedTenant))
        {
            return BadRequest(new { error = "tenant_required" });
        }

        await _settingsService.DeleteSettingAsync(resolvedTenant, key, cancellationToken);
        return Ok(new { key, message = "Setting deleted successfully" });
    }

    private string? ResolveTenant(bool isPlatform, string? requestedTenant)
    {
        if (isPlatform)
        {
            return string.IsNullOrWhiteSpace(requestedTenant) ? null : requestedTenant.Trim();
        }

        var tenantId = TenantContext.TenantId;
        return string.IsNullOrWhiteSpace(tenantId) ? null : tenantId;
    }
}

/// <summary>
/// Setting set request model.
/// </summary>
public sealed record SetSettingRequest(string Value, string? Description = null);
