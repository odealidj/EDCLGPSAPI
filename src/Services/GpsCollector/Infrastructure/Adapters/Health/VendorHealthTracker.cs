using System.Collections.Concurrent;
using GpsCollector.Application.Ports;
using GpsCollector.Domain.Models;
using Microsoft.Extensions.Logging;

namespace GpsCollector.Infrastructure.Adapters.Health;

// State pelacak kesehatan per vendor endpoint
internal sealed class VendorEndpointHealthState
{
    public int ConsecutiveFailures { get; set; }
    public DateTime? FirstFailureAt { get; set; }
    public DateTime? LastFailureAt { get; set; }
    public int? LastStatusCode { get; set; }
    public string? LastErrorMessage { get; set; }
    public bool IsAlertActive { get; set; }
}

// Pelacak kesehatan koneksi endpoint vendor & pemicu alert (AOT Ready)
public sealed class VendorHealthTracker : IVendorHealthTracker
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VendorHealthTracker> _logger;
    private readonly int _failureThreshold;
    private readonly ConcurrentDictionary<string, VendorEndpointHealthState> _states = new(StringComparer.OrdinalIgnoreCase);

    public VendorHealthTracker(
        IServiceScopeFactory scopeFactory,
        ILogger<VendorHealthTracker> logger,
        int failureThreshold = 3)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _failureThreshold = failureThreshold > 0 ? failureThreshold : 3;
    }

    public async Task RecordFailureAsync(
        VendorConfig vendor,
        VendorEndpointConfig endpoint,
        int statusCode,
        string? errorMessage,
        CancellationToken ct)
    {
        var key = BuildKey(vendor.Id, endpoint.Id);
        var state = _states.GetOrAdd(key, _ => new VendorEndpointHealthState());

        lock (state)
        {
            state.ConsecutiveFailures++;
            state.LastFailureAt = DateTime.UtcNow;
            state.FirstFailureAt ??= DateTime.UtcNow;
            state.LastStatusCode = statusCode;
            state.LastErrorMessage = errorMessage;
        }

        _logger.LogWarning(
            "Vendor {VendorName} endpoint {Url} failed ({Failures}/{Threshold} consecutive failures). Status: {StatusCode}",
            vendor.VendorName, endpoint.BaseUrl, state.ConsecutiveFailures, _failureThreshold, statusCode);

        // Picu alert jika mencapai ambang batas dan alert belum aktif
        VendorAlertEvent? alertEvent = null;

        lock (state)
        {
            if (state.ConsecutiveFailures >= _failureThreshold && !state.IsAlertActive)
            {
                state.IsAlertActive = true;

                alertEvent = new VendorAlertEvent
                {
                    VendorId = vendor.Id,
                    VendorName = vendor.VendorName,
                    EndpointUrl = endpoint.BaseUrl,
                    ConsecutiveFailures = state.ConsecutiveFailures,
                    LastStatusCode = state.LastStatusCode,
                    LastErrorMessage = state.LastErrorMessage,
                    AlertType = "ALERT_TRIGGERED",
                    Timestamp = DateTime.UtcNow
                };
            }
        }

        if (alertEvent != null)
        {
            _logger.LogError(
                "Triggering OUTAGE ALERT for vendor {VendorName} after {Failures} consecutive failures.",
                vendor.VendorName, alertEvent.ConsecutiveFailures);

            using var scope = _scopeFactory.CreateScope();
            var alertNotifier = scope.ServiceProvider.GetRequiredService<IVendorAlertNotifier>();
            await alertNotifier.NotifyAlertTriggeredAsync(alertEvent, ct);
        }
    }

    public async Task RecordSuccessAsync(
        VendorConfig vendor,
        VendorEndpointConfig endpoint,
        CancellationToken ct)
    {
        var key = BuildKey(vendor.Id, endpoint.Id);
        if (!_states.TryGetValue(key, out var state))
        {
            return;
        }

        VendorAlertEvent? recoveryEvent = null;

        lock (state)
        {
            if (state.IsAlertActive)
            {
                var downtime = state.FirstFailureAt.HasValue
                    ? DateTime.UtcNow - state.FirstFailureAt.Value
                    : TimeSpan.Zero;

                recoveryEvent = new VendorAlertEvent
                {
                    VendorId = vendor.Id,
                    VendorName = vendor.VendorName,
                    EndpointUrl = endpoint.BaseUrl,
                    ConsecutiveFailures = state.ConsecutiveFailures,
                    LastStatusCode = 200,
                    LastErrorMessage = null,
                    AlertType = "ALERT_RESOLVED",
                    Timestamp = DateTime.UtcNow,
                    Downtime = downtime
                };

                state.IsAlertActive = false;
            }

            // Reset counter setelah sukses
            state.ConsecutiveFailures = 0;
            state.FirstFailureAt = null;
            state.LastFailureAt = null;
            state.LastErrorMessage = null;
            state.LastStatusCode = null;
        }

        if (recoveryEvent != null)
        {
            _logger.LogInformation(
                "Triggering RECOVERY ALERT for vendor {VendorName}. Total downtime: {DowntimeSeconds:F0}s",
                vendor.VendorName, recoveryEvent.Downtime?.TotalSeconds ?? 0);

            using var scope = _scopeFactory.CreateScope();
            var alertNotifier = scope.ServiceProvider.GetRequiredService<IVendorAlertNotifier>();
            await alertNotifier.NotifyAlertResolvedAsync(recoveryEvent, ct);
        }
    }

    public int GetConsecutiveFailures(Guid vendorId, Guid endpointId)
    {
        var key = BuildKey(vendorId, endpointId);
        return _states.TryGetValue(key, out var state) ? state.ConsecutiveFailures : 0;
    }

    public bool IsAlertActive(Guid vendorId, Guid endpointId)
    {
        var key = BuildKey(vendorId, endpointId);
        return _states.TryGetValue(key, out var state) && state.IsAlertActive;
    }

    private static string BuildKey(Guid vendorId, Guid endpointId) => $"{vendorId:N}:{endpointId:N}";
}
