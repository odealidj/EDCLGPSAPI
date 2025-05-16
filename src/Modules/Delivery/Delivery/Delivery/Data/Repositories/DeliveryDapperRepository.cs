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


            var sql = $"select * from  edcl.sp_edclgps2_get_track_delivery('{param.DeliveryNo}',{param.Density});";
            
            Console.WriteLine(sql);

            var parameters = new DynamicParameters();
            parameters.Add("p_delivery_no", param.DeliveryNo);
            parameters.Add("p_density", param.Density);

            using (var connection = new NpgsqlConnection(connectionString))
            {
                var data =await connection.QueryAsync<TrackDeliveryEdclResponseDto>(
                    sql,
                    parameters
                    , commandType: CommandType.Text,
                    commandTimeout: 60);

                return data;

            }
        }
        catch (SqlException ex)
        {
            ////ExceptionLogger.LogException(_logger, ex);
            throw new InvalidOperationException(ex.Message);
        }
        catch (Exception ex)
        {
            ////ExceptionLogger.LogException(_logger, ex);
            throw;
        }
    }
}