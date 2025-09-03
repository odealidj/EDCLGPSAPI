
using EFCore.BulkExtensions;
using GeofenceWorker.Data.Repository.IRepository;
using GeofenceWorker.Workers.Exceptions;
using GeofenceWorker.Workers.Models;
using Microsoft.Extensions.Logging;
using Npgsql;

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
    
    public async Task<int> UpdateVarParamsPropertyRawSqlAsync(
        Guid endpointId, 
        string propertyName, 
        object newValue,
        DateTime lastModified,
        string lastModifiedBy)
    {
        try
        {


            var sql = $"UPDATE edcl.tb_m_gps_vendor_endpoint " +
                      $"SET \"VarParams\" = jsonb_set(\"VarParams\", @propertyPath, @newValue::jsonb) " +
                      $", \"LastModified\" = @lastModified " +
                      $", \"LastModifiedBy\" = @lastModifiedBy " +
                      $"WHERE \"Id\" = @endpointId";

            return await dbContext.Database.ExecuteSqlRawAsync(sql,
                new Npgsql.NpgsqlParameter("endpointId", endpointId),
                new Npgsql.NpgsqlParameter("propertyPath", new[] { propertyName }),
                new Npgsql.NpgsqlParameter("newValue", Newtonsoft.Json.JsonConvert.SerializeObject(newValue)),
                new Npgsql.NpgsqlParameter("lastModified", lastModified),
                new Npgsql.NpgsqlParameter("lastModifiedBy", lastModifiedBy)
                );            
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

    public async Task<int> InsertGpsDelivery(List<GpsDelivery> gpsDeliveries, CancellationToken cancellationToken = default)
    {
        if (gpsDeliveries == null || gpsDeliveries.Count == 0) return 0;
        
        // Konfigurasi bulk insert
        var bulkConfig = new BulkConfig
        {
            BatchSize = 500,          // Jumlah record per batch
            PreserveInsertOrder = true, // Menjaga urutan insert
            UseTempDB = false         // PostgreSQL tidak mendukung temp DB
        };

        try
        {
            // Lakukan bulk insert
            await dbContext.BulkInsertAsync(gpsDeliveries, bulkConfig, cancellationToken: cancellationToken);

            // Return jumlah record yang berhasil disimpan
            return gpsDeliveries.Count;
        }
        catch (DbUpdateException ex)
        { 
            logger.LogError(ex, "Terjadi kesalahan DbUpdateException saat menyimpan Gps Delivery.");
            // Transformasikan ke exception domain tanpa detail database sensitif
            if (ex.InnerException?.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase) == true)
            {
                throw new WorkerConflictException("Data yang Anda coba simpan menyebabkan konflik.");
            }
            throw new WorkerDataAccessException("Terjadi kesalahan saat menyimpan data.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Terjadi kesalahan umum saat menyimpan Gps Delivery.");
            throw new WorkerDataAccessException("Terjadi kesalahan saat mengakses data.");
        }
    }

    /*
    public async Task<CustomDeliveryProgressDto?> GetCustomDeliveryProgressAsync(string platNo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(platNo)) return null;
        
        var latestRecord = await dbContext.DeliveryProgresses
            .Where(dp => dp.PlatNo == platNo)
            .OrderByDescending(dp => dp.CreatedAt)
            .Select(dp => new CustomDeliveryProgressDto
            {
                DeliveryNo = dp.DeliveryNo,
                NoKtp = dp.NoKtp,
                Lpcd =  dp.Lpcd ?? "",
            })
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
        
        if (latestRecord != null) latestRecord.PlatNo = platNo;
        return latestRecord;
        
    }
    */

    public async Task<List<DeliveryProgress>> GetCustomDeliveryProgressesAsync(IEnumerable<string>? platNos, CancellationToken cancellationToken)
    {
        if (platNos == null || !platNos.Any())
        {
            return new List<DeliveryProgress>();
        }
        
        var platNosList = platNos.ToList();

        return await dbContext.DeliveryProgresses
            .FromSqlRaw(@"
            SELECT DISTINCT ON (""PlatNo"") *
            FROM edcl.tb_r_delivery_progress
            WHERE ""PlatNo"" = ANY(@platNos)
            ORDER BY ""PlatNo"", ""CreatedAt"" DESC",
                new NpgsqlParameter("platNos", platNosList))
            .ToListAsync(cancellationToken);
    }
}