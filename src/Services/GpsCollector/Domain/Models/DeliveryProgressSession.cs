namespace GpsCollector.Domain.Models;

// Sesi delivery aktif truk dari tb_r_delivery_progress
public sealed class DeliveryProgressSession
{
    public string PlatNo { get; init; } = string.Empty;
    public string DeliveryNo { get; init; } = string.Empty;
    public string? Lpcd { get; init; }
    public string? NoKtp { get; init; }
}

// Log pemanggilan api vendor ke tb_m_gps_api_log
public sealed class GpsApiLogRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string? FunctionName { get; init; }
    public string Status { get; init; } = "1";
    public string? ErrorMessage { get; init; }
    public string? Parameter { get; init; }
    public string? Username { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string CreatedBy { get; init; } = "System";
}
