namespace DeliveryTracking.Domain.Entities;

// entitas utama progres pengiriman unit armada toyota (tabel tb_r_delivery_progress)
public class DeliveryProgress : Entity<Guid>
{
    public string DeliveryNo { get; set; } = string.Empty;
    public string PlatNo { get; set; } = string.Empty;
    public string NoKtp { get; set; } = string.Empty;
    public string? VendorName { get; set; }
    public string? Lpcd { get; set; }

    public static DeliveryProgress Create(
        Guid id,
        string deliveryNo,
        string platNo,
        string noKtp,
        string? vendorName = null,
        string? lpcd = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(deliveryNo);

        return new DeliveryProgress
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            DeliveryNo = deliveryNo.Trim().ToUpperInvariant(),
            PlatNo = platNo?.Trim().ToUpperInvariant() ?? string.Empty,
            NoKtp = noKtp?.Trim() ?? string.Empty,
            VendorName = vendorName?.Trim(),
            Lpcd = lpcd?.Trim().ToUpperInvariant() ?? string.Empty
        };
    }

    public void UpdateDetails(string platNo, string noKtp, string? vendorName, string? lpcd)
    {
        if (!string.IsNullOrWhiteSpace(platNo))
            PlatNo = platNo.Trim().ToUpperInvariant();

        if (!string.IsNullOrWhiteSpace(noKtp))
            NoKtp = noKtp.Trim();

        if (!string.IsNullOrWhiteSpace(vendorName))
            VendorName = vendorName.Trim();

        if (!string.IsNullOrWhiteSpace(lpcd))
            Lpcd = lpcd.Trim().ToUpperInvariant();
    }
}
