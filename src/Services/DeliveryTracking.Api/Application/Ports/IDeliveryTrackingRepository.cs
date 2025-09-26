using DeliveryTracking.Domain.Entities;
using DeliveryTracking.Application.DTOs;

namespace DeliveryTracking.Application.Ports;

// port repository abstraksi akses data delivery tracking (murni hexagonal)
public interface IDeliveryTrackingRepository
{
    // ambil data jejak koordinat gps via stored procedure
    Task<IReadOnlyList<TrackDeliveryPointDto>> GetTrackDeliveryAsync(
        string deliveryNo,
        int density = 1,
        CancellationToken cancellationToken = default);

    // ambil data detail progres delivery
    Task<DeliveryProgress?> GetDeliveryProgressByDeliveryNoAsync(
        string deliveryNo,
        CancellationToken cancellationToken = default);

    // simpan atau perbarui status progres pengiriman
    Task<SaveDeliveryProgressResult> UpsertDeliveryProgressAsync(
        DeliveryProgress progress,
        CancellationToken cancellationToken = default);

    // bulk insert data progres pengiriman dengan postgresql binary copy
    Task<int> BulkInsertDeliveryProgressAsync(
        IReadOnlyList<DeliveryProgress> items,
        CancellationToken cancellationToken = default);

    // bulk insert data telemetri titik gps dengan postgresql binary copy
    Task<int> BulkInsertGpsDeliveriesAsync(
        IReadOnlyList<GpsDelivery> items,
        CancellationToken cancellationToken = default);

    // hapus status progres delivery berdasarkan nomor do
    Task<bool> DeleteDeliveryProgressAsync(
        string deliveryNo,
        CancellationToken cancellationToken = default);

    // lookup cepat data vendor dari nomor lpcd
    Task<VendorByLpcdLookupResult?> GetVendorByLpcdAsync(
        string lpcd,
        CancellationToken cancellationToken = default);

    // lookup cepat data vendor dari plat nomor
    Task<VendorByPlatNoLookupResult?> GetVendorByPlatNoAsync(
        string platNo,
        CancellationToken cancellationToken = default);

    // lookup cepat data lpcd dari nama vendor
    Task<LpcdByVendorNameLookupResult?> GetLpcdByVendorNameAsync(
        string vendorName,
        CancellationToken cancellationToken = default);

    // kueri daftar posisi armada langsung dari postgresql (sp_edclgps1_get_last_positions)
    Task<PaginatedPositionResponse> QueryPositionsFromDbAsync(
        LastPositionRequest param,
        CancellationToken cancellationToken = default);

    // ambil posisi terakhir armada dari database berdasarkan nomor plat
    Task<LastPositionDto?> GetLastPositionByPlatNoFromDbAsync(
        string platNo,
        CancellationToken cancellationToken = default);

    // simpan / daftarkan sesi pelacakan pengiriman baru (Start Job)
    Task<DeliveryTrackingSession> StartDeliverySessionAsync(
        DeliveryTrackingSession session,
        CancellationToken cancellationToken = default);

    // ambil sesi pelacakan pengiriman berdasarkan nomor do
    Task<DeliveryTrackingSession?> GetDeliverySessionByDeliveryNoAsync(
        string deliveryNo,
        CancellationToken cancellationToken = default);

    // selesaikan sesi pelacakan pengiriman (Complete Job)
    Task<DeliveryTrackingSession?> CompleteDeliverySessionAsync(
        string deliveryNo,
        string? completedBy = null,
        string? source = null,
        decimal? latitude = null,
        decimal? longitude = null,
        CancellationToken cancellationToken = default);

    // ambil daftar sesi pelacakan pengiriman aktif
    Task<IReadOnlyList<DeliveryTrackingSession>> GetActiveDeliverySessionsAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);
}

