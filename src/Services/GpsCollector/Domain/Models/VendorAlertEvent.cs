namespace GpsCollector.Domain.Models;

// Model kejadian alert pemanggilan vendor API
public sealed class VendorAlertEvent
{
    public Guid VendorId { get; init; }
    public string VendorName { get; init; } = string.Empty;
    public string EndpointUrl { get; init; } = string.Empty;
    public int ConsecutiveFailures { get; init; }
    public int? LastStatusCode { get; init; }
    public string? LastErrorMessage { get; init; }
    public string AlertType { get; init; } = "ALERT_TRIGGERED"; // ALERT_TRIGGERED | ALERT_RESOLVED
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
    public TimeSpan? Downtime { get; init; }
}
