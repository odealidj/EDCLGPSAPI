namespace VendorMaster.Domain.Entities;

// entitas endpoint http rest api vendor (tabel m_gps_vendor_endpoint)
public class GpsVendorEndpoint : Entity<Guid>
{
    public Guid VendorId { get; set; }
    public string EndpointType { get; set; } = VendorDefaults.DefaultEndpointType;
    public string BaseUrl { get; set; } = string.Empty;
    public string Method { get; set; } = VendorDefaults.DefaultHttpMethod;
    public string ContentType { get; set; } = VendorDefaults.DefaultContentType;
    public JsonObject? Headers { get; set; }
    public JsonObject? Params { get; set; }
    public JsonObject? Bodies { get; set; }
    public JsonObject? VarParams { get; set; }
    public string? MaxPath { get; set; }

    // navigasi ke parent vendor
    public GpsVendor? GpsVendor { get; set; }
}
