using GeofenceWorker.Workers.Dtos;

namespace GeofenceWorker.Data.Repository.IRepository;

public interface IGpsLastPositionRepository
{
    Task<IEnumerable<LastPositionResponseDto>>GetLastPositionAsync(LastPositionRequestDto param, CancellationToken cancellationToken = default);
    Task<long> GetLastPositionCountAsync(LastPositionRequestDto param, CancellationToken cancellationToken = default);
}