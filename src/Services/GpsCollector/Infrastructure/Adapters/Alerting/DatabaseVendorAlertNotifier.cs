using System.Text.Json;
using GpsCollector.Application.DTOs;
using GpsCollector.Application.Ports;
using GpsCollector.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GpsCollector.Infrastructure.Adapters.Alerting;

// Notifier alert vendor ke database PostgreSQL tb_m_gps_api_log (AOT Ready)
public sealed class DatabaseVendorAlertNotifier : IVendorAlertNotifier
{
    private readonly IGpsApiLogRepository _apiLogRepo;
    private readonly ILogger<DatabaseVendorAlertNotifier> _logger;

    public DatabaseVendorAlertNotifier(
        IGpsApiLogRepository apiLogRepo,
        ILogger<DatabaseVendorAlertNotifier> logger)
    {
        _apiLogRepo = apiLogRepo;
        _logger = logger;
    }

    public async Task NotifyAlertTriggeredAsync(VendorAlertEvent alertEvent, CancellationToken ct)
    {
        var dto = new VendorAlertMessageDto(
            VendorId: alertEvent.VendorId,
            VendorName: alertEvent.VendorName,
            EndpointUrl: alertEvent.EndpointUrl,
            ConsecutiveFailures: alertEvent.ConsecutiveFailures,
            LastStatusCode: alertEvent.LastStatusCode,
            LastErrorMessage: alertEvent.LastErrorMessage,
            AlertType: "ALERT_TRIGGERED",
            Timestamp: alertEvent.Timestamp
        );

        var paramJson = JsonSerializer.Serialize(dto, GpsCollectorJsonContext.Default.VendorAlertMessageDto);

        var logRecord = new GpsApiLogRecord
        {
            Id = Guid.NewGuid(),
            FunctionName = alertEvent.EndpointUrl,
            Status = "ALERT_TRIGGERED",
            ErrorMessage = $"[ALERT] Vendor '{alertEvent.VendorName}' gagal dihubungi sebanyak {alertEvent.ConsecutiveFailures}x berturut-turut. Error: {alertEvent.LastErrorMessage}",
            Parameter = paramJson,
            Username = alertEvent.VendorName,
            CreatedAt = alertEvent.Timestamp,
            CreatedBy = "AlertSystem"
        };

        _logger.LogError(
            "[ALERT_TRIGGERED] Vendor {VendorName} unreachable after {Failures} consecutive attempts. Details recorded in tb_m_gps_api_log.",
            alertEvent.VendorName, alertEvent.ConsecutiveFailures);

        await _apiLogRepo.LogApiCallAsync(logRecord, ct);
    }

    public async Task NotifyAlertResolvedAsync(VendorAlertEvent alertEvent, CancellationToken ct)
    {
        var downtimeSec = alertEvent.Downtime?.TotalSeconds;

        var dto = new VendorAlertMessageDto(
            VendorId: alertEvent.VendorId,
            VendorName: alertEvent.VendorName,
            EndpointUrl: alertEvent.EndpointUrl,
            ConsecutiveFailures: alertEvent.ConsecutiveFailures,
            LastStatusCode: 200,
            LastErrorMessage: null,
            AlertType: "ALERT_RESOLVED",
            Timestamp: alertEvent.Timestamp,
            DowntimeSeconds: downtimeSec
        );

        var paramJson = JsonSerializer.Serialize(dto, GpsCollectorJsonContext.Default.VendorAlertMessageDto);

        var logRecord = new GpsApiLogRecord
        {
            Id = Guid.NewGuid(),
            FunctionName = alertEvent.EndpointUrl,
            Status = "ALERT_RESOLVED",
            ErrorMessage = $"[RECOVERY] Vendor '{alertEvent.VendorName}' telah pulih normal. Total downtime: {downtimeSec:F0} detik.",
            Parameter = paramJson,
            Username = alertEvent.VendorName,
            CreatedAt = alertEvent.Timestamp,
            CreatedBy = "AlertSystem"
        };

        _logger.LogInformation(
            "[ALERT_RESOLVED] Vendor {VendorName} recovered successfully. Total downtime: {DowntimeSec:F0}s.",
            alertEvent.VendorName, downtimeSec ?? 0);

        await _apiLogRepo.LogApiCallAsync(logRecord, ct);
    }
}
