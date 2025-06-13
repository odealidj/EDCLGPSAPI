using System.Data;
using Dapper;
using GeofenceWorker.Data.Repository.IRepository;
using GeofenceWorker.Workers.Dtos;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace GeofenceWorker.Data.Repository;

public class GpsLastPositionRepository(string connectionString,
    ILogger<GpsLastPositionRepository> logger): IGpsLastPositionRepository
{
    public async Task<IEnumerable<LastPositionResponseDto>> GetLastPositionAsync(LastPositionRequestDto param, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("p_gps_vendor_id",  param.GpsVendorId == null || param.GpsVendorId == Guid.Empty ? null : param.GpsVendorId);
            parameters.Add("p_vendor_name", string.IsNullOrEmpty(param.VendorName) ? null : param.VendorName.ToLower() );
            parameters.Add("p_plat_no", string.IsNullOrEmpty(param.PlatNo) ? null : param.PlatNo.ToLower());
            parameters.Add("p_device_id", string.IsNullOrEmpty(param.DeviceId) ? null : param.DeviceId.ToLower());
            parameters.Add("p_datetime", param.Datetime == null ? null : param.Datetime.Value);
            //parameters.Add("p_created_at", param.CreatedAt == null ? null : param.CreatedAt.Value);
            parameters.Add("p_created_at", param.CreatedAt == null ? null : param.CreatedAt.Value.Date);
            parameters.Add("p_page_index", param.PageIndex < 0 ? 0 : param.PageIndex -1);
            parameters.Add("p_page_size", param.PageSize);
            
            var sql = "SELECT * FROM edcl.sp_edclgps1_get_last_positions(@p_gps_vendor_id, @p_vendor_name, @p_plat_no, @p_device_id, @p_datetime, @p_created_at, @p_page_index, @p_page_size)";

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                var data = await connection.QueryAsync<LastPositionResponseDto>(
                    sql,
                    parameters, commandType: CommandType.Text,
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

    public async Task<long> GetLastPositionCountAsync(LastPositionRequestDto param, CancellationToken cancellationToken = default)
    {
        try
        {
            var parameters = new DynamicParameters();
            parameters.Add("p_gps_vendor_id",  param.GpsVendorId == null || param.GpsVendorId == Guid.Empty ? DBNull.Value : param.GpsVendorId);
            parameters.Add("p_vendor_name", string.IsNullOrEmpty(param.VendorName) ? null : param.VendorName.ToLower() );
            parameters.Add("p_plat_no", string.IsNullOrEmpty(param.PlatNo) ? null : param.PlatNo.ToLower());
            parameters.Add("p_device_id", string.IsNullOrEmpty(param.DeviceId) ? null : param.DeviceId.ToLower());
            parameters.Add("p_datetime", param.Datetime == null ? null : param.Datetime.Value);
            //parameters.Add("p_created_at", param.CreatedAt == null ? null : param.CreatedAt.Value);
            parameters.Add("p_created_at", param.CreatedAt == null ? null : param.CreatedAt.Value.Date);
            parameters.Add("p_page_index", param.PageIndex < 0 ? 0 : param.PageIndex -1);
            parameters.Add("p_page_size", param.PageSize);
            
            var sql = "SELECT * FROM edcl.sp_edclgps1_get_count_last_positions(@p_gps_vendor_id, @p_vendor_name, @p_plat_no, @p_device_id, @p_datetime, @p_created_at, @p_page_index, @p_page_size)";

            await using (var connection = new NpgsqlConnection(connectionString))
            {
                var data = await connection.ExecuteScalarAsync<long>(
                    sql,
                    parameters, commandType: CommandType.Text,
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