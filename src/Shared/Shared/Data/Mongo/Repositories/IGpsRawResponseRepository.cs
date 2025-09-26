using Shared.Data.Mongo.DTOs;
using Shared.Pagination;

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

    Task<PaginatedResult<RawResponseItemDto>> QueryRawResponsesAsync(
        RawResponseFilterRequest filter,
        CancellationToken ct = default);

    Task<RawResponseItemDto?> GetRawResponseByIdAsync(
        string id,
        CancellationToken ct = default);
}
