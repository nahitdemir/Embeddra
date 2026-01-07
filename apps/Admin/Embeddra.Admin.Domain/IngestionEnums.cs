namespace Embeddra.Admin.Domain;

public enum IngestionJobStatus
{
    Queued,
    Processing,
    Completed,
    Failed
}

public enum IngestionSourceType
{
    Csv,
    Json,
    Webhook,
    Pull
}
