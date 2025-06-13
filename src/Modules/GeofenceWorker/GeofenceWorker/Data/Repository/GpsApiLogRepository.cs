using GeofenceWorker.Data.Repository.IRepository;
using GeofenceWorker.Workers.Exceptions;
using GeofenceWorker.Workers.Models;
using Microsoft.Extensions.Logging;

namespace GeofenceWorker.Data.Repository;

public class GpsApiLogRepository(GeofenceWorkerDbContext dbContext, 
    ILogger<GpsLastPositionHRepository> logger): IGpsApiLogRepository
{
    public async Task<int> InsertGpsApiLog(GpsApiLog gpsApiLog, CancellationToken cancellationToken = default)
    {
        try
        {

            dbContext.GpsApiLogs.Add(gpsApiLog);
            int affectedRows = await dbContext.SaveChangesAsync(cancellationToken);
            return affectedRows;
        }
        catch (DbUpdateException ex)
        { 
            logger.LogError(ex, "Terjadi kesalahan DbUpdateException saat menyimpan GpsApiLogs.");
            // Transformasikan ke exception domain tanpa detail database sensitif
            if (ex.InnerException?.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new WorkerConflictException("Data yang Anda coba simpan menyebabkan konflik.");
            }
            throw new WorkerDataAccessException("Terjadi kesalahan saat menyimpan data.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Terjadi kesalahan umum saat menyimpan GpsApiLogs.");
            throw new WorkerDataAccessException("Terjadi kesalahan saat mengakses data.");
        }
    }
}