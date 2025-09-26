using GpsCollector.Application.Ports;
using GpsCollector.Domain.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace GpsCollector.Infrastructure.Adapters.Persistence;

// Repositori pencatatan log pemanggilan vendor API ke tb_m_gps_api_log
public sealed class PostgresGpsApiLogRepository : IGpsApiLogRepository
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresGpsApiLogRepository> _logger;

    public PostgresGpsApiLogRepository(NpgsqlDataSource dataSource, ILogger<PostgresGpsApiLogRepository> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task LogApiCallAsync(GpsApiLogRecord logRecord, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO edcl.tb_m_gps_api_log (
                ""Id"", ""FunctionName"", ""Status"", ""ErrorMessage"", 
                ""Parameter"", ""Username"", ""CreatedAt"", ""CreatedBy""
            ) VALUES (
                @id, @functionName, @status, @errorMessage, 
                @parameter, @username, @createdAt, @createdBy
            );";

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = logRecord.Id });
            cmd.Parameters.Add(new NpgsqlParameter("functionName", NpgsqlDbType.Text) { Value = (object?)logRecord.FunctionName ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter("status", NpgsqlDbType.Text) { Value = logRecord.Status });
            cmd.Parameters.Add(new NpgsqlParameter("errorMessage", NpgsqlDbType.Text) { Value = (object?)logRecord.ErrorMessage ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter("parameter", NpgsqlDbType.Text) { Value = (object?)logRecord.Parameter ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter("username", NpgsqlDbType.Text) { Value = (object?)logRecord.Username ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter("createdAt", NpgsqlDbType.TimestampTz) { Value = logRecord.CreatedAt });
            cmd.Parameters.Add(new NpgsqlParameter("createdBy", NpgsqlDbType.Text) { Value = logRecord.CreatedBy });

            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write API log for function {FunctionName}", logRecord.FunctionName);
        }
    }
}
