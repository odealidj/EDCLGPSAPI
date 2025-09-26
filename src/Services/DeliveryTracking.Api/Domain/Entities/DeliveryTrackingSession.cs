using Shared.DDD;

namespace DeliveryTracking.Domain.Entities;

// entitas manifes sesi pengiriman aktif hasil klik "Start Job" dari EDCL_API (tabel edcl.t_delivery_tracking_session)
public class DeliveryTrackingSession : Entity<Guid>
{
    public string DeliveryNo { get; set; } = string.Empty;
    public string Lpcd { get; set; } = string.Empty;
    public Guid? VendorId { get; set; }
    public string MatchKey { get; set; } = "PlatNo";
    public string MatchValue { get; set; } = string.Empty;
    public string CleanMatchValue { get; set; } = string.Empty;
    public string? DriverKtp { get; set; }
    public string Status { get; set; } = "STARTED"; // STARTED, IN_TRANSIT, ARRIVED, COMPLETED
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ArrivedAt { get; set; }
    public Guid? DestinationGeofenceId { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? CompletedBy { get; set; }
    public string? CompletionSource { get; set; }
    public bool IsDeleted { get; set; } = false;

    public static DeliveryTrackingSession Create(
        string deliveryNo,
        string lpcd,
        string key,
        string value,
        string cleanValue,
        Guid? vendorId = null,
        string? driverKtp = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deliveryNo);
        ArgumentException.ThrowIfNullOrWhiteSpace(lpcd);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        return new DeliveryTrackingSession
        {
            Id = Guid.NewGuid(),
            DeliveryNo = deliveryNo.Trim().ToUpperInvariant(),
            Lpcd = lpcd.Trim().ToUpperInvariant(),
            VendorId = vendorId,
            MatchKey = string.IsNullOrWhiteSpace(key) ? "PlatNo" : key.Trim(),
            MatchValue = value.Trim(),
            CleanMatchValue = cleanValue.Trim().ToUpperInvariant(),
            DriverKtp = driverKtp?.Trim(),
            Status = "STARTED",
            StartedAt = DateTime.UtcNow,
            IsDeleted = false
        };
    }
}
