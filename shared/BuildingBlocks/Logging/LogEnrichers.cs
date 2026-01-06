using Embeddra.BuildingBlocks.Correlation;
using Embeddra.BuildingBlocks.Tenancy;
using Elastic.Apm;
using Serilog.Core;
using Serilog.Events;

namespace Embeddra.BuildingBlocks.Logging;

public sealed class CorrelationIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var correlationId = CorrelationContext.CorrelationId;
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("correlationId", correlationId));
        }
    }
}

public sealed class TenantIdEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var tenantId = TenantContext.TenantId;
        if (!string.IsNullOrWhiteSpace(tenantId))
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("tenant_id", tenantId));
        }
    }
}

public sealed class ElasticApmLogEnricher : ILogEventEnricher
{
    public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
    {
        var transaction = Agent.Tracer.CurrentTransaction;
        if (transaction != null)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("trace.id", transaction.TraceId));
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("transaction.id", transaction.Id));
        }

        var span = Agent.Tracer.CurrentSpan;
        if (span != null)
        {
            logEvent.AddPropertyIfAbsent(propertyFactory.CreateProperty("span.id", span.Id));
        }
    }
}
