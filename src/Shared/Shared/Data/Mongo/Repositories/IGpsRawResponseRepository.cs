namespace Shared.Data.Mongo.Repositories;

public interface IGpsRawResponseRepository
{
    Task InsertAsync(
        Guid vendorId,
        string vendorName,
        string endpointUrl,
        string rawJson,
        int recordCount,
        string? batchId = null,
        CancellationToken ct = default);
}
