using GeofenceMaster.Data.Repository.IRepository;
using GeofenceMaster.GeofenceMaster.Exceptions;
using GeofenceMaster.GeofenceMaster.Models;
using Microsoft.Extensions.Logging;

namespace GeofenceMaster.Data.Repository;

// GeofenceMasterDbContext dbContext
public class GeofenceMasterRepository(
    GeofenceMasterDbContext dbContext, 
    ILogger<GeofenceMasterRepository> logger)
    : IGeofenceMasterRepository
{
    public async Task<GpsVendor> CreateGeofenceMaster(GpsVendor gpsVendor, CancellationToken cancellationToken = default)
    {
        try
        {
            dbContext.GpsVendors.Add(gpsVendor);
            await dbContext.SaveChangesAsync(cancellationToken);
            return gpsVendor;
            
        }
        catch (DbUpdateException ex)
        { 
            logger.LogError(ex, "Terjadi kesalahan DbUpdateException saat menyimpan GpsLastPositionH.");
            // Transformasikan ke exception domain tanpa detail database sensitif
            if (ex.InnerException?.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new GeofenceMasterConflictException("Vendor name is already exists.", ex.Message);
            }
            throw new GeofenceMasterDatabaseAccessException("\nAn error occurred while saving data.", ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Terjadi kesalahan umum saat menyimpan GpsLastPositionH.");
            throw new GeofenceMasterInternalServerException("An error occurred while saving data.", ex.Message);
        }
    }

    public async Task<IEnumerable<GpsVendor>> GetGeofenceMaster(
        Guid? id, 
        string? vendorName, 
        int pageIndex, 
        int pageSize, 
        bool asNoTracking = true, 
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.GpsVendors
            .AsQueryable();

        if (id != null && id != Guid.Empty)
        {
            query = query.Where(x => x.Id == id);
        }

        if (!string.IsNullOrWhiteSpace(vendorName))
        {
            query = query.Where(x => EF.Functions.ILike(x.VendorName, $"%{vendorName}%"));
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
            .Include(x => x.GpsVendorAuth) // Include related GpsVendorAuths
            .Include(x => x.Mappings)
            .Include(x => x.Lpcds)
            .ToListAsync(cancellationToken); // Execute the query and retrieve the results

        return pagedVendors;
    }

    public async Task<int> GetGeofenceMasterCount(Guid? id, string? vendorName, CancellationToken cancellationToken = default)
    {
        var query = dbContext.GpsVendors
            .AsQueryable();

        if (id != null && id != Guid.Empty)
        {
            query = query.Where(x => x.Id == id);
        }
        
        if (!string.IsNullOrWhiteSpace(vendorName))
        {
            ////query = query.Where(x => x.VendorName.Contains(vendorName));
            query = query.Where(x => EF.Functions.ILike(x.VendorName, $"%{vendorName}%"));
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