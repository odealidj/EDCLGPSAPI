namespace GpsCollector.Domain.Models;

// Sesi delivery aktif truk dari edcl.t_delivery_tracking_session
public sealed class DeliveryProgressSession
{
    public Guid SessionId { get; init; }
    public string DeliveryNo { get; init; } = string.Empty;
    public string Lpcd { get; init; } = string.Empty;
    public Guid? VendorId { get; init; }
    public string? VendorCode { get; init; }
    public string MatchKey { get; init; } = "PlatNo";
    public string MatchValue { get; init; } = string.Empty;
    public string CleanMatchValue { get; init; } = string.Empty;
    public string? DriverKtp { get; init; }

    // Helper kompatibilitas ke PlatNo dan NoKtp
    public string PlatNo
    {
        get => string.Equals(MatchKey, "PlatNo", StringComparison.OrdinalIgnoreCase) ? MatchValue : (CleanMatchValue ?? string.Empty);
        init => MatchValue = value;
    }

    public string? NoKtp
    {
        get => DriverKtp;
        init => DriverKtp = value;
    }
}

// Log pemanggilan api vendor ke edcl.m_gps_api_log
public sealed class GpsApiLogRecord
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string? FunctionName { get; init; }
    public string Status { get; init; } = "1";
    public string? ErrorMessage { get; init; }
    public string? ErrorResponse { get; init; }
    public string? Parameter { get; init; }
    public string? Username { get; init; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
    public string CreatedBy { get; init; } = "System";
}
