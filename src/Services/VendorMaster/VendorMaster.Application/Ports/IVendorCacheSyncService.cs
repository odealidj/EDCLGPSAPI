namespace VendorMaster.Application.Ports;

// Port outbound untuk sinkronisasi konfigurasi vendor aktif ke Redis cache
public interface IVendorCacheSyncService
{
    // Hapus cache lama dan isi ulang data vendor-vendor aktif terbaru ke Redis selamanya (infinite TTL)
    Task RefreshActiveVendorsCacheAsync(CancellationToken cancellationToken = default);

    // Hapus kunci cache di Redis jika vendor dihapus/di-update
    Task InvalidateActiveVendorsCacheAsync(CancellationToken cancellationToken = default);
}
