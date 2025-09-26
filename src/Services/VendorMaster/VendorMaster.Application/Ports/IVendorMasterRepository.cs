namespace VendorMaster.Application.Ports;

// port outbound untuk akses database vendor master & geofence destination
public interface IVendorMasterRepository
{
    // ambil daftar vendor dengan filter dan paging
    Task<IEnumerable<GpsVendor>> GetVendorsAsync(
        Guid? id,
        string? vendorName,
        int pageIndex,
        int pageSize,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    // hitung total data vendor
    Task<int> GetVendorsCountAsync(
        Guid? id,
        string? vendorName,
        CancellationToken cancellationToken = default);

    // ambil vendor spesifik by id lengkap dengan child relations
    Task<GpsVendor?> GetVendorByIdAsync(
        Guid id,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    // ambil vendor spesifik by vendor_code
    Task<GpsVendor?> GetVendorByCodeAsync(
        string vendorCode,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default);

    // simpan vendor baru
    Task<GpsVendor> CreateVendorAsync(
        GpsVendor vendor,
        CancellationToken cancellationToken = default);

    // update vendor dan child entities
    Task<bool> UpdateVendorAsync(
        GpsVendor vendor,
        CancellationToken cancellationToken = default);

    // hapus vendor by id (hard delete child, soft delete vendor)
    Task<bool> DeleteVendorAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    // ambil daftar geofence plant toyota
    Task<IEnumerable<GeofenceDestination>> GetGeofenceDestinationsAsync(
        bool activeOnly = true,
        CancellationToken cancellationToken = default);

    // ambil geofence plant by id
    Task<GeofenceDestination?> GetGeofenceDestinationByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    // commit transaksi
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
