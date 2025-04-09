using GeofenceMaster.Data.Repository.IRepository;
using GeofenceMaster.GeofenceMaster.Models;

namespace GeofenceMaster.Data.Repository;

public class GeofenceMasterRepository(GeofenceMasterDbContext dbContext)
    : IGeofenceMasterRepository
{
    public async Task<GpsVendor> CreateGeofenceMaster(GpsVendor gpsVendor, CancellationToken cancellationToken = default)
    {
        dbContext.GpsVendors.Add(gpsVendor);
        await dbContext.SaveChangesAsync(cancellationToken);
        return gpsVendor;
    }

    public async Task<IEnumerable<GpsVendor>> GetGeofenceMaster(string? vendorName, int pageIndex, int pageSize, bool asNoTracking = true, CancellationToken cancellationToken = default)
    {
        var baseQuery = dbContext.GpsVendors.AsQueryable();

        if (!string.IsNullOrWhiteSpace(vendorName))
        {
            baseQuery = baseQuery.Where(x => x.VendorName.Contains(vendorName));
        }

        if (asNoTracking)
        {
            baseQuery = baseQuery.AsNoTracking();
        }

        // Step 1: Ambil hasil paged dari baseQuery
        var pagedList = await baseQuery
            .OrderBy(x => x.VendorName)
            .Skip(pageIndex * pageSize)
            .Take(pageSize)
            .Select(x => new { x.Id, x.VendorName }) // pilih Id dan jaga urutan
            .ToListAsync(cancellationToken);

        var orderedIds = pagedList.Select(x => x.Id).ToList();

        // Step 2: Ambil detail + Include berdasarkan ID hasil paginasi
        var pagedVendors = await dbContext.GpsVendors
            .Where(x => orderedIds.Contains(x.Id))
            .Include(x => x.Items)
            .ToListAsync(cancellationToken);

        // Step 3: Jaga urutan sesuai hasil paging sebelumnya
        var result = orderedIds
            .Join(pagedVendors, id => id, vendor => vendor.Id, (id, vendor) => vendor)
            .ToList();

        return result;
        

    }

    public async Task<int> SaveChangesAsync(string? userName = null, CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }
}