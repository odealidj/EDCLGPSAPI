namespace GeofenceMaster.GeofenceMaster.Dtos;

public class GeofenceMasterLpcdDto
{
    public Guid Id { get; set; }
    
    [JsonIgnore]
    public Guid GpsVendorId { get; set; }
    public string Lpcd { get; set; } = string.Empty;
    
    public GeofenceMasterLpcdDto()
    {
    }
    
    public GeofenceMasterLpcdDto(Guid gpsVendorId, string lpcd)
    {
        GpsVendorId = gpsVendorId;
        Lpcd = lpcd;
    }
    
    public GeofenceMasterLpcdDto(Guid id, Guid gpsVendorId, string lpcd)
    {
        Id = id;
        GpsVendorId = gpsVendorId;
        Lpcd = lpcd;
    }
}