namespace Shared.Data.Mongo.Repositories;

public interface IGpsApiLogRepository
{
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
