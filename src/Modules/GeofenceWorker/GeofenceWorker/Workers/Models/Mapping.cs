using System.Text.Json.Serialization;

namespace GeofenceWorker.Workers.Models;

public class Mapping : Entity<int>
{
    public Guid GpsVendorId { get; set; }

    public string ResponseField { get; set; } =
        string.Empty; // Nama field dari JSON respons vendor (misal: vehicleNumber)

    public string MappedField { get; set; } =
        string.Empty; // Nama field yang dipetakan dalam sistem/database (misal: PLAT_NO)
    
    internal Mapping(int id, Guid gpsVendorId, string responseField, string mappedField)
    {
        Id = id;
        GpsVendorId = gpsVendorId;
        ResponseField = responseField;
        MappedField = mappedField;
    }

    [JsonConstructor]
    public Mapping(Guid gpsVendorId, string responseField, string mappedField)
    {
        GpsVendorId = gpsVendorId;
        GpsVendorId = gpsVendorId;
        ResponseField = responseField;
        MappedField = mappedField;
    }

    [JsonConstructor]
    public Mapping()
    {
    }
}