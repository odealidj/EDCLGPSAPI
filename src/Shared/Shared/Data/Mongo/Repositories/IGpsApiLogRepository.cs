namespace Shared.Data.Mongo.Repositories;

public interface IGpsApiLogRepository
{
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Log repository records exhaustive diagnostic telemetry fields directly")]
    Task InsertAsync(
        Guid vendorId,
        string vendorName,
        string endpointUrl,
        string functionName,
        long durationMs,
        int? httpStatusCode,
        bool success,
        string? errorMessage,
        string? errorDetailJson,
        int recordCount,
        CancellationToken ct = default);
}
