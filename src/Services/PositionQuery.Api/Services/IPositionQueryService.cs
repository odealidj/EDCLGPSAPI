using PositionQuery.Api.Models;

namespace PositionQuery.Api.Services;

public interface IPositionQueryService
{
    Task<LastPositionDto?> GetLastPositionByPlatNoAsync(string platNo, CancellationToken cancellationToken = default);
    Task<PaginatedPositionResponse> QueryPositionsAsync(LastPositionRequest request, CancellationToken cancellationToken = default);
}
