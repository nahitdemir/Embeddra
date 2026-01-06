namespace Embeddra.BuildingBlocks.Audit;

public static class AuditActions
{
    public const string TenantCreated = "TENANT_CREATED";
    public const string ApiKeyCreated = "API_KEY_CREATED";
    public const string ApiKeyRevoked = "API_KEY_REVOKED";
    public const string AllowedOriginsUpdated = "ALLOWED_ORIGINS_UPDATED";
    public const string BulkUploadRequested = "BULK_UPLOAD_REQUESTED";
    public const string CsvImportRequested = "CSV_IMPORT_REQUESTED";

    public const string IngestionJobStarted = "INGESTION_JOB_STARTED";
    public const string IngestionJobCompleted = "INGESTION_JOB_COMPLETED";
    public const string IngestionJobFailed = "INGESTION_JOB_FAILED";
}
