using System.Data;
using Dapper;
using Delivery.Data.Repositories.IRepositories;
using Delivery.Delivery.Dtos;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Delivery.Data.Repositories;

public class DeliveryDapperRepository(string connectionString,
    ILogger<DeliveryDapperRepository> logger): IDeliveryDapperRepository
{
    public async Task<IEnumerable<TrackDeliveryEdclResponseDto>> GetTrackDeliveryAsync(TrackDeliveryEdclRequestDto param, bool asNoTracking = true,
        CancellationToken cancellationToken = default)
    {
        try
        {
            IEnumerable<TrackDeliveryEdclResponseDto> data;
            
            var sql = $"select * from  edcl.sp_edclgps1_get_track_delivery('{param.DeliveryNo}',{param.Density});";
            
            var parameters = new DynamicParameters();
            parameters.Add("p_delivery_no", param.DeliveryNo);
            parameters.Add("p_density", param.Density);

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                    data =await connection.QueryAsync<TrackDeliveryEdclResponseDto>(
                    sql,
                    parameters, 
                    commandType: CommandType.Text,
                    commandTimeout: 60);
                //// return data;
            }
            
            var deliveryEdclResponseDtos = data.ToList();
            if (deliveryEdclResponseDtos.Count != 0) return deliveryEdclResponseDtos;
            
            sql = $"select * from  edcl.sp_edclgps2_get_track_delivery('{param.DeliveryNo}',{param.Density});";

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                data =await connection.QueryAsync<TrackDeliveryEdclResponseDto>(
                    sql,
                    parameters
                    , commandType: CommandType.Text,
                    commandTimeout: 60);
                
                return data;
            }
            
        }
        catch (SqlException ex)
        {
            logger.LogError(ex.Message, "Terjadi kesalahan saat mengakses database.");
            throw;
            ////ExceptionLogger.LogException(_logger, ex);
            ////throw new InvalidOperationException(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message, "Terjadi kesalahan saat mengakses database.");
            ////ExceptionLogger.LogException(_logger, ex);
            throw;
        }
    }

    public async Task<OutGetGpsVendorByLpcd?> GetGpsVendorByLpcdAsync(InGetGpsVendorByLpcd param, CancellationToken cancellationToken = default)
    {
        try
        {
            var sql = $"SELECT * FROM edcl.get_latest_gps_vendor_by_lpcd('{param.Lpcd}');";
            
            var parameters = new DynamicParameters();
            parameters.Add("p_lpcd", param.Lpcd);

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                var data =await connection.QuerySingleOrDefaultAsync<OutGetGpsVendorByLpcd>(
                    sql,
                    parameters, 
                    commandType: CommandType.Text,
                    commandTimeout: 60);
                return data;
            }
            
        }
        catch (SqlException ex)
        {
            logger.LogError(ex.Message, "Terjadi kesalahan saat mengakses database.");
            throw;
            ////ExceptionLogger.LogException(_logger, ex);
            ////throw new InvalidOperationException(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message, "Terjadi kesalahan saat mengakses database.");
            ////ExceptionLogger.LogException(_logger, ex);
            throw;
        }
    }

    public async Task<OutGetGpsVendorByPlatNo?> GetGpsVendorByPlatNoAsync(InGetGpsVendorByPlatNo param, CancellationToken cancellationToken = default)
    {
        try
        {
            var sql = $"SELECT GpsVendorId, VendorName FROM edcl.get_latest_gps_vendor_by_plat_no('{param.PlatNo}');";
            
            var parameters = new DynamicParameters();
            parameters.Add("p_plat_no", param.PlatNo);

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                var data =await connection.QuerySingleOrDefaultAsync<OutGetGpsVendorByPlatNo>(
                    sql,
                    parameters, 
                    commandType: CommandType.Text,
                    commandTimeout: 60);
                return data;
            }
            
        }
        catch (SqlException ex)
        {
            logger.LogError(ex.Message, "Terjadi kesalahan saat mengakses database.");
            throw;
            ////ExceptionLogger.LogException(_logger, ex);
            ////throw new InvalidOperationException(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message, "Terjadi kesalahan saat mengakses database.");
            ////ExceptionLogger.LogException(_logger, ex);
            throw;
        }
    }

    public async Task<OutGetGpsLpcdByGpsVendorId?> GetGpsLpcdByGpsVendorIdAsync(InGetGpsLpcdByGpsVendorId param, CancellationToken cancellationToken = default)
    {
        try
        {
            var sql = $"SELECT * FROM edcl.get_latest_lpcd_by_gps_vendor_id('{param.GpsVendorId}');";
            
            var parameters = new DynamicParameters();
            parameters.Add("p_gps_vendor_id", param.GpsVendorId);

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                var data =await connection.QuerySingleOrDefaultAsync<OutGetGpsLpcdByGpsVendorId>(
                    sql,
                    parameters, 
                    commandType: CommandType.Text,
                    commandTimeout: 60);
                return data;
            }
            
        }
        catch (SqlException ex)
        {
            logger.LogError(ex.Message, "Terjadi kesalahan saat mengakses database.");
            throw;
            ////ExceptionLogger.LogException(_logger, ex);
            ////throw new InvalidOperationException(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message, "Terjadi kesalahan saat mengakses database.");
            ////ExceptionLogger.LogException(_logger, ex);
            throw;
        }
    }

    public async Task<OutGetGpsLpcdByGpsVendorName?> GetGpsLpcdByGpsVendorNameAsync(InGetGpsLpcdByGpsVendorName param, CancellationToken cancellationToken = default)
    {
        try
        {
            var sql = $"SELECT * FROM edcl.get_latest_lpcd_by_gps_vendor_name('{param.VendorName}');";
            
            var parameters = new DynamicParameters();
            parameters.Add("p_gps_vendor_name", param.VendorName);

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                var data =await connection.QuerySingleOrDefaultAsync<OutGetGpsLpcdByGpsVendorName>(
                    sql,
                    parameters, 
                    commandType: CommandType.Text,
                    commandTimeout: 60);
                return data;
            }
            
        }
        catch (SqlException ex)
        {
            logger.LogError(ex.Message, "Terjadi kesalahan saat mengakses database.");
            throw;
            ////ExceptionLogger.LogException(_logger, ex);
            ////throw new InvalidOperationException(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex.Message, "Terjadi kesalahan saat mengakses database.");
            ////ExceptionLogger.LogException(_logger, ex);
            throw;
        }
    }
}