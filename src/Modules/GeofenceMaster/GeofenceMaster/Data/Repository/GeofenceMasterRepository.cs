using GeofenceMaster.Data.Repository.IRepository;
using GeofenceMaster.GeofenceMaster.Models;

namespace GeofenceMaster.Data.Repository;

// GeofenceMasterDbContext dbContext
public class GeofenceMasterRepository(GeofenceMasterDbContext dbContext)
    : IGeofenceMasterRepository
{
    public async Task<GpsVendor> CreateGeofenceMaster(GpsVendor gpsVendor, CancellationToken cancellationToken = default)
    {
        dbContext.GpsVendors.Add(gpsVendor);
        await dbContext.SaveChangesAsync(cancellationToken);
        return gpsVendor;
    }

    public async Task<IEnumerable<GpsVendor>> GetGeofenceMaster(
        string? vendorName, 
        int pageIndex, 
        int pageSize, 
        bool asNoTracking = true, 
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.GpsVendors
            .AsQueryable();

        // Filter by vendor name (optional)
        if (!string.IsNullOrWhiteSpace(vendorName))
        {
            query = query.Where(x => x.VendorName.Contains(vendorName));
        }
        
        if (asNoTracking)
        {
            query = query.AsNoTracking();
        }
        
        // Apply pagination and order by VendorName
        var pagedVendors = await query
            .OrderBy(x => x.VendorName) // Sorting by VendorName
            .ThenBy(x => x.Id) // Sorting by Id for the vendors
            .Skip(pageIndex * pageSize) // Pagination - Skip records for the current page
            .Take(pageSize) // Take only the number of records specified by pageSize
            .Include(x => x.GpsVendorEndpoints) // Include related GpsVendorEndpoints
            .Include(x => x.GpsVendorAuths) // Include related GpsVendorAuths
            .ToListAsync(cancellationToken); // Execute the query and retrieve the results

        return pagedVendors;
    }

    public async Task<int> GetGeofenceMasterCount(string? vendorName, CancellationToken cancellationToken = default)
    {
        var query = dbContext.GpsVendors
            .AsQueryable();

        // Filter by vendor name (optional)
        if (!string.IsNullOrWhiteSpace(vendorName))
        {
            query = query.Where(x => x.VendorName.Contains(vendorName));
        }

        // Return the total count
        var count = await query.CountAsync(cancellationToken);
        return count;
    }

    public async Task<int> SaveChangesAsync(string? userName = null, CancellationToken cancellationToken = default)
    {
        return await dbContext.SaveChangesAsync(cancellationToken);
    }
}