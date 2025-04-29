namespace GeofenceMaster.GeofenceMaster.Dtos;

public class GeofenceMasterMappingDto
{
    public int Id { get; set; }
    
    [JsonIgnore]
    public Guid GpsVendorId { get; set; }

    public string ResponseField { get; set; } =
        string.Empty; // Nama field dari JSON respons vendor (misal: vehicleNumber)

    public string MappedField { get; set; } =
        string.Empty; // Nama field yang dipetakan dalam sistem/database (misal: PLAT_NO)
    
    public GeofenceMasterMappingDto()
    {
    }
    
    public GeofenceMasterMappingDto(Guid gpsVendorId, string responseField, string mappedField)
    {
        GpsVendorId = gpsVendorId;
        ResponseField = responseField;
        MappedField = mappedField;
    }
    
    public GeofenceMasterMappingDto(int id, Guid gpsVendorId, string responseField, string mappedField)
    {
        Id = id;
        GpsVendorId = gpsVendorId;
        ResponseField = responseField;
        MappedField = mappedField;
    }
}