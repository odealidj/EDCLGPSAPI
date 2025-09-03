using GeofenceWorker.Data.Repository.IRepository;
using GeofenceWorker.Workers.Exceptions;
using GeofenceWorker.Workers.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GeofenceWorker.Data.Repository;

public class GpsApiLogRepository(
    GeofenceWorkerDbContext dbContext, 
    ILogger<GpsLastPositionHRepository> logger,
    Shared.Data.Mongo.Repositories.IGpsApiLogRepository? mongoApiLogRepo = null) : IGpsApiLogRepository
{
    public async Task<int> InsertGpsApiLog(GpsApiLog gpsApiLog, CancellationToken cancellationToken = default)
    {
        // 1. Dual-write ke MongoDB (Non-blocking / safe error isolation)
        if (mongoApiLogRepo != null)
        {
            try
            {
                await mongoApiLogRepo.InsertAsync(
                    vendorId: Guid.Empty,
                    vendorName: gpsApiLog.Username ?? "Unknown",
                    endpointUrl: gpsApiLog.FunctionName ?? string.Empty,
                    functionName: gpsApiLog.FunctionName ?? "ProcessEndpoint",
                    durationMs: 0,
                    httpStatusCode: gpsApiLog.Status == "1" ? 200 : null,
                    success: gpsApiLog.Status == "1",
                    errorMessage: gpsApiLog.ErrorMessage,
                    errorDetailJson: gpsApiLog.ErrorResponse ?? gpsApiLog.Parameter,
                    recordCount: 0,
                    ct: cancellationToken
                );
            }
            catch (Exception exMongo)
            {
                logger.LogWarning(exMongo, "Failed dual-write GpsApiLog to MongoDB.");
            }
        }

        // 2. Tulis ke PostgreSQL (Legacy primary store)
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