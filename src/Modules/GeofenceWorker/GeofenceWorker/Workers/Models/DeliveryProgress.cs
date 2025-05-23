namespace GeofenceWorker.Workers.Models;

public class DeliveryProgress: Entity<Guid>
{
    public string DeliveryNo { get; set; } = string.Empty;
    public string PlatNo { get; set; } = String.Empty;
    public string NoKtp { get; set; } = String.Empty;
    public string? VendorName { get; set; } = String.Empty;
    public string? Lpcd { get; set; }


    public static DeliveryProgress Create(
        Guid id,
        string deliveryNo,
        string platNo,
        string noKtp,
        string? vendorName,
        string? lpcd
    )
    {
        ArgumentException.ThrowIfNullOrEmpty(deliveryNo);
        ArgumentException.ThrowIfNullOrEmpty(platNo);
        ArgumentException.ThrowIfNullOrEmpty(noKtp);
       
        var deliveryProgress = new DeliveryProgress
        {
            Id = id,
            DeliveryNo = deliveryNo,
            PlatNo = platNo,
            NoKtp = noKtp,
            VendorName = vendorName,
            Lpcd = !string.IsNullOrEmpty(lpcd) ? lpcd : string.Empty
        };

        return deliveryProgress;
    }
}