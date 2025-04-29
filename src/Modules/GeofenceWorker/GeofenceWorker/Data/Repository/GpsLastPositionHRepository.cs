using GeofenceWorker.Data.Repository.IRepository;
using GeofenceWorker.Workers.Exceptions;
using GeofenceWorker.Workers.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GeofenceWorker.Data.Repository;

public class GpsLastPositionHRepository(
    GeofenceWorkerDbContext dbContext, 
    ILogger<GpsLastPositionHRepository> logger)
    : IGpsLastPositionHRepository
{
    public async Task<GpsLastPositionH> InsertGpsLastPositionH(GpsLastPositionH gpsLastPositionH, bool asNoTracking = true, CancellationToken cancellationToken = default)
    {
        try
        {
            dbContext.GpsLastPositionHs.Add(gpsLastPositionH);
            await dbContext.SaveChangesAsync(cancellationToken);
            return gpsLastPositionH;
        }
        catch (DbUpdateException ex)
        { 
            logger.LogError(ex, "Terjadi kesalahan DbUpdateException saat menyimpan GpsLastPositionH.");
            // Transformasikan ke exception domain tanpa detail database sensitif
            if (ex.InnerException?.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new WorkerConflictException("Data yang Anda coba simpan menyebabkan konflik.");
            }
            throw new WorkerDataAccessException("Terjadi kesalahan saat menyimpan data.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Terjadi kesalahan umum saat menyimpan GpsLastPositionH.");
            throw new WorkerDataAccessException("Terjadi kesalahan saat mengakses data.");
        }
        
    }
}