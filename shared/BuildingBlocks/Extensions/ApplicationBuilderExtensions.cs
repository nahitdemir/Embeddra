using Embeddra.BuildingBlocks.Authentication;
using Embeddra.BuildingBlocks.Correlation;
using Embeddra.BuildingBlocks.Exceptions;
using Embeddra.BuildingBlocks.Logging;
using Embeddra.BuildingBlocks.Tenancy;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace Embeddra.BuildingBlocks.Extensions;

public static class ApplicationBuilderExtensions
{
    public static IApplicationBuilder UseEmbeddraMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<TenantIdMiddleware>();
        if (app.ApplicationServices.GetService<IApiKeyValidator>() is not null)
        {
            app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
        }
        app.UseMiddleware<RequestResponseLoggingMiddleware>();
        app.UseMiddleware<ExceptionHandlingMiddleware>();
        return app;
    }
}
