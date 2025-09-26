namespace VendorMaster.Domain.Entities;

// entitas kredensial & handshake auth vendor (tabel m_gps_vendor_auth)
public class GpsVendorAuth : Entity<Guid>
{
    public Guid VendorId { get; set; }
    public string AuthType { get; set; } = VendorDefaults.DefaultAuthType;
    public string BaseUrl { get; set; } = string.Empty;
    public string Method { get; set; } = VendorDefaults.DefaultAuthHttpMethod;
    public string ContentType { get; set; } = VendorDefaults.DefaultContentType;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? TokenPath { get; set; }
    public string? TokenExpiresInPath { get; set; }
    public int TokenTtlSeconds { get; set; } = VendorDefaults.DefaultTokenTtlSeconds;
    public JsonObject? Headers { get; set; }
    public JsonObject? Params { get; set; }
    public JsonObject? Bodies { get; set; }

    // navigasi ke parent vendor
    public GpsVendor? GpsVendor { get; set; }
}
