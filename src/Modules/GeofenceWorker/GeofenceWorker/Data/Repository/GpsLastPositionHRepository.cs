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
    public async Task<GpsVendorEndpoint?> GetEndPointByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await dbContext.GpsVendorEndpoints.FirstOrDefaultAsync(v => v.Id == id, cancellationToken: cancellationToken);
    }
    
    public async Task<int> UpdateVarParamsPropertyRawSqlAsync(Guid endpointId, string propertyName, object newValue)
    {
        try
        {


            var sql = $"UPDATE edcl.tb_m_gps_vendor_endpoint " +
                      $"SET \"VarParams\" = jsonb_set(\"VarParams\", @propertyPath, @newValue::jsonb) " +
                      $"WHERE \"Id\" = @endpointId";

            return await dbContext.Database.ExecuteSqlRawAsync(sql,
                new Npgsql.NpgsqlParameter("endpointId", endpointId),
                new Npgsql.NpgsqlParameter("propertyPath", new string[] { "lastPositionId" }),
                new Npgsql.NpgsqlParameter("newValue", Newtonsoft.Json.JsonConvert.SerializeObject(newValue)));            
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

    public async Task<bool> UpdateVarParamsAsync(GpsVendorEndpoint endpoint, CancellationToken cancellationToken = default)
    {
        // Atau _context.Entry(vendor).State = EntityState.Modified;
        endpoint.LastModified = DateTime.UtcNow;
        dbContext.Update(endpoint); 
        return await dbContext.SaveChangesAsync(cancellationToken) > 0;
    }

    public async Task<GpsLastPositionH> InsertGpsLastPositionH(GpsLastPositionH gpsLastPositionH, CancellationToken cancellationToken = default)
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