using DeliveryTracking.Application.DTOs;

namespace DeliveryTracking.Application.Ports;

// port layanan kueri posisi kendaraan (redis-first + db fallback)
public interface IPositionQueryService
{
    // ambil posisi terakhir satu armada berdasarkan nomor plat (cek redis lalu db)
    Task<LastPositionDto?> GetLastPositionByPlatNoAsync(string platNo, CancellationToken cancellationToken = default);

    // kueri multi-filter dan paginasi posisi armada
    Task<PaginatedPositionResponse> QueryPositionsAsync(LastPositionRequest request, CancellationToken cancellationToken = default);
}
