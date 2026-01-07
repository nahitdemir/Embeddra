namespace Embeddra.BuildingBlocks.Authentication;

public sealed record ApiKeyValidationResult(Guid ApiKeyId, string TenantId);
