using GpsCollector.Application.Ports;
using GpsCollector.Domain.Models;
using Microsoft.Extensions.Logging;
using Npgsql;
using NpgsqlTypes;

namespace GpsCollector.Infrastructure.Adapters.Persistence;

// Repositori pencatatan log pemanggilan vendor API ke edcl.m_gps_api_log
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
            INSERT INTO edcl.m_gps_api_log (
                id, function_name, status, error_message, error_response,
                parameter, username, created_at, created_by
            ) VALUES (
                @id, @functionName, @status, @errorMessage, @errorResponse,
                @parameter, @username, @createdAt, @createdBy
            );";

        try
        {
            await using var conn = await _dataSource.OpenConnectionAsync(ct);
            await using var cmd = new NpgsqlCommand(sql, conn);

            cmd.Parameters.Add(new NpgsqlParameter("id", NpgsqlDbType.Uuid) { Value = logRecord.Id });
            cmd.Parameters.Add(new NpgsqlParameter("functionName", NpgsqlDbType.Varchar) { Value = (object?)logRecord.FunctionName ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter("status", NpgsqlDbType.Varchar) { Value = logRecord.Status });
            cmd.Parameters.Add(new NpgsqlParameter("errorMessage", NpgsqlDbType.Text) { Value = (object?)logRecord.ErrorMessage ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter("errorResponse", NpgsqlDbType.Text) { Value = (object?)logRecord.ErrorResponse ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter("parameter", NpgsqlDbType.Text) { Value = (object?)logRecord.Parameter ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter("username", NpgsqlDbType.Varchar) { Value = (object?)logRecord.Username ?? DBNull.Value });
            cmd.Parameters.Add(new NpgsqlParameter("createdAt", NpgsqlDbType.TimestampTz) { Value = logRecord.CreatedAt });
            cmd.Parameters.Add(new NpgsqlParameter("createdBy", NpgsqlDbType.Varchar) { Value = logRecord.CreatedBy });

            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write API log to edcl.m_gps_api_log for function {FunctionName}", logRecord.FunctionName);
        }
    }
}
