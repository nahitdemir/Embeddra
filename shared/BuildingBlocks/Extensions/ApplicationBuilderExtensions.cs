using Embeddra.BuildingBlocks.Correlation;
using Embeddra.BuildingBlocks.Exceptions;
using Embeddra.BuildingBlocks.Logging;
using Embeddra.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Builder;

namespace Embeddra.BuildingBlocks.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseEmbeddraMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<TenantIdMiddleware>();
        app.UseMiddleware<RequestResponseLoggingMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        return app;
    }
}
