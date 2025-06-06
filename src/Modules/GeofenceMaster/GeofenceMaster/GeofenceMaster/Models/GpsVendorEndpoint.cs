namespace GeofenceMaster.GeofenceMaster.Models;

public class GpsVendorEndpoint : Entity<Guid>
{
    public Guid GpsVendorId { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public JsonObject? Headers { get; set; }
    public JsonObject? Params { get; set; }
    public JsonObject? Bodies { get; set; }

    internal GpsVendorEndpoint(Guid gpsVendorId, string baseUrl, string method,
        JsonObject? headers, JsonObject? @params, JsonObject? bodies)
    {
        GpsVendorId = gpsVendorId;
        BaseUrl = baseUrl;
        Method = method;
        Headers = headers;
        Params = @params;
        Bodies = bodies;

    }

    [JsonConstructor]
    public GpsVendorEndpoint(Guid id, Guid gpsVendorId, string baseUrl, string method,
        JsonObject? headers, JsonObject? @params, JsonObject? bodies)
    {
        Id = id;
        GpsVendorId = gpsVendorId;
        BaseUrl = baseUrl;
        Method = method;
        Headers = headers;
        Params = @params;
        Bodies = bodies;

    }

    [JsonConstructor]
    public GpsVendorEndpoint()
    {
    }
}