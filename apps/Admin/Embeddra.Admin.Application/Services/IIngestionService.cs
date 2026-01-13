using System.Text.Json;
using Embeddra.Admin.Domain;

namespace Embeddra.Admin.Application.Services;

/// <summary>
/// Ingestion işlemleri için servis sonuç modeli.
/// </summary>
public sealed record IngestionResult(
    Guid JobId,
    bool Success,
    string? Error = null,
    int? DocumentCount = null);

/// <summary>
/// Ingestion servis interface'i.
/// </summary>
public interface IIngestionService
{
    /// <summary>
    /// JSON formatında bulk ingestion başlatır.
    /// </summary>
    Task<IngestionResult> StartBulkIngestionAsync(
        string tenantId,
        JsonElement payload,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// CSV formatında ingestion başlatır.
    /// </summary>
    Task<IngestionResult> StartCsvIngestionAsync(
        string tenantId,
        string csvContent,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Job durumunu getirir.
    /// </summary>
    Task<IngestionJob?> GetJobAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);
}
