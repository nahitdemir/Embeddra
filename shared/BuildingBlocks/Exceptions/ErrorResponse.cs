namespace Embeddra.BuildingBlocks.Exceptions;

public sealed record ErrorResponse(string Code, string Message, string? CorrelationId);
