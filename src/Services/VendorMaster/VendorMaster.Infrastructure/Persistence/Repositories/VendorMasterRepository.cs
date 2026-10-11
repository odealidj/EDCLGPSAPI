namespace VendorMaster.Infrastructure.Persistence.Repositories;

// implementasi port repository vendor master via ef core
public class VendorMasterRepository(
    VendorMasterDbContext dbContext,
    ILogger<VendorMasterRepository> logger) : IVendorMasterRepository
{
    // ambil list vendor terpaginasi
    public async Task<IEnumerable<GpsVendor>> GetVendorsAsync(
        Guid? id,
        string? vendorName,
        string? lpcd,
        int pageIndex,
        int pageSize,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.GpsVendors.AsQueryable();

        if (id.HasValue && id.Value != Guid.Empty)
            query = query.Where(x => x.Id == id.Value);

        if (!string.IsNullOrWhiteSpace(vendorName))
            query = query.Where(x => EF.Functions.ILike(x.VendorName, $"%{vendorName.Trim()}%"));

        if (!string.IsNullOrWhiteSpace(lpcd))
            query = query.Where(x => x.Lpcds.Any(l => EF.Functions.ILike(l.Lpcd, $"%{lpcd.Trim()}%")));

        if (asNoTracking)
            query = query.AsNoTracking();

        var skipCount = (pageIndex <= 1 ? 0 : pageIndex - 1) * pageSize;

        return await query
            .OrderBy(x => x.VendorName)
            .ThenBy(x => x.Id)
            .Skip(skipCount)
            .Take(pageSize)
            .Include(x => x.GpsVendorEndpoints)
            .Include(x => x.GpsVendorAuth)
            .Include(x => x.Mappings)
            .Include(x => x.Lpcds)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
    }

    // hitung total data vendor
    public async Task<int> GetVendorsCountAsync(
        Guid? id,
        string? vendorName,
        string? lpcd,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.GpsVendors.AsQueryable();

        if (id.HasValue && id.Value != Guid.Empty)
            query = query.Where(x => x.Id == id.Value);

        if (!string.IsNullOrWhiteSpace(vendorName))
            query = query.Where(x => EF.Functions.ILike(x.VendorName, $"%{vendorName.Trim()}%"));

        if (!string.IsNullOrWhiteSpace(lpcd))
            query = query.Where(x => x.Lpcds.Any(l => EF.Functions.ILike(l.Lpcd, $"%{lpcd.Trim()}%")));

        return await query.CountAsync(cancellationToken);
    }

    // cek apakah lpcd sudah terdaftar pada vendor lain
    public async Task<bool> IsLpcdExistsAsync(
        string lpcd,
        Guid? excludeVendorId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lpcd))
            return false;

        var normalizedLpcd = lpcd.Trim().ToUpperInvariant();
        var query = dbContext.Lpcds.AsQueryable();

        if (excludeVendorId.HasValue && excludeVendorId.Value != Guid.Empty)
            query = query.Where(x => x.VendorId != excludeVendorId.Value);

        return await query.AnyAsync(x => x.Lpcd == normalizedLpcd, cancellationToken);
    }

    // ambil vendor spesifik by lpcd
    public async Task<GpsVendor?> GetVendorByLpcdAsync(
        string lpcd,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(lpcd))
            return null;

        var normalizedLpcd = lpcd.Trim().ToUpperInvariant();
        var query = dbContext.GpsVendors.AsQueryable();

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query
            .Include(x => x.GpsVendorEndpoints)
            .Include(x => x.GpsVendorAuth)
            .Include(x => x.Mappings)
            .Include(x => x.Lpcds)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Lpcds.Any(l => l.Lpcd == normalizedLpcd), cancellationToken);
    }

    // ambil vendor spesifik by id
    public async Task<GpsVendor?> GetVendorByIdAsync(
        Guid id,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.GpsVendors.AsQueryable();

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query
            .Include(x => x.GpsVendorEndpoints)
            .Include(x => x.GpsVendorAuth)
            .Include(x => x.Mappings)
            .Include(x => x.Lpcds)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    // ambil vendor by code
    public async Task<GpsVendor?> GetVendorByCodeAsync(
        string vendorCode,
        bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.GpsVendors.AsQueryable();

        if (asNoTracking)
            query = query.AsNoTracking();

        return await query
            .Include(x => x.GpsVendorEndpoints)
            .Include(x => x.GpsVendorAuth)
            .Include(x => x.Mappings)
            .Include(x => x.Lpcds)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.VendorCode == vendorCode.ToUpperInvariant(), cancellationToken);
    }

    // create vendor baru
    public async Task<GpsVendor> CreateVendorAsync(
        GpsVendor vendor,
        CancellationToken cancellationToken = default)
    {
        try
        {
            dbContext.GpsVendors.Add(vendor);
            await dbContext.SaveChangesAsync(cancellationToken);
            return vendor;
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Gagal simpan vendor baru ke database.");
            if (ex.InnerException?.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase) == true ||
                ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new VendorConflictException("Vendor Code atau nama vendor sudah terdaftar.", ex.Message);
            }
            throw new VendorBadRequestException("Terjadi kesalahan saat menyimpan data vendor.", ex.Message);
        }
    }

    // update vendor dan refresh child relasi
    public async Task<bool> UpdateVendorAsync(
        GpsVendor vendor,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException ex)
        {
            logger.LogError(ex, "Gagal update vendor di database.");
            if (ex.InnerException?.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase) == true ||
                ex.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new VendorConflictException("Vendor Code atau nama vendor sudah terdaftar.", ex.Message);
            }
            throw new VendorBadRequestException("Terjadi kesalahan saat mengupdate vendor.", ex.Message);
        }
    }

    // hapus vendor (soft delete vendor, bersihkan child relations)
    public async Task<bool> DeleteVendorAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var vendor = await dbContext.GpsVendors
            .Include(x => x.GpsVendorEndpoints)
            .Include(x => x.GpsVendorAuth)
            .Include(x => x.Mappings)
            .Include(x => x.Lpcds)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

        if (vendor == null) return false;

        // soft delete vendor
        vendor.IsDeleted = true;
        vendor.IsActive = false;

        // bersihkan child data agar tidak orphaned
        dbContext.GpsVendorEndpoints.RemoveRange(vendor.GpsVendorEndpoints);
        if (vendor.GpsVendorAuth != null)
            dbContext.GpsVendorAuths.Remove(vendor.GpsVendorAuth);
        dbContext.Mappings.RemoveRange(vendor.Mappings);
        dbContext.Lpcds.RemoveRange(vendor.Lpcds);

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    // ambil daftar geofence pabrik toyota
    public async Task<IEnumerable<GeofenceDestination>> GetGeofenceDestinationsAsync(
        bool activeOnly = true,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.GeofenceDestinations.AsNoTracking();

        if (activeOnly)
            query = query.Where(x => x.IsActive);

        return await query
            .OrderBy(x => x.DestinationCode)
            .ToListAsync(cancellationToken);
    }

    // ambil geofence pabrik by id
    public async Task<GeofenceDestination?> GetGeofenceDestinationByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.GeofenceDestinations
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
    }

    // commit transaksi db
    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }
}
