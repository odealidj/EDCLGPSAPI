namespace GeofenceMaster.GeofenceMaster.Dtos;

/*
public record GeofenceMasterAuthDto(
    Guid Id,
    Guid GpsVendorId,
    string BaseUrl,
    string Method,
    string Authtype,
    JsonObject? Headers,
    JsonObject? Params,
    JsonObject? Bodies
);
*/


public class GeofenceMasterAuthDto
{
    // Properti
    public Guid Id { get; set; } = Guid.Empty;
    public Guid GpsVendorId { get; set; } = Guid.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Authtype { get; set; } = string.Empty;
    public JsonObject? Headers { get; set; }
    public JsonObject? Params { get; set; }
    public JsonObject? Bodies { get; set; }

    // Constructor tanpa parameter (default)
    public GeofenceMasterAuthDto()
    {
    }

    // Constructor dengan parameter
    public GeofenceMasterAuthDto(
        Guid id,
        Guid gpsVendorId,
        string baseUrl,
        string method,
        string authtype,
        JsonObject? headers,
        JsonObject? @params,
        JsonObject? bodies)
    {
        Id = id;
        GpsVendorId = gpsVendorId;
        BaseUrl = baseUrl;
        Method = method;
        Authtype = authtype;
        Headers = headers;
        Params = @params;
        Bodies = bodies;
    }

    // Override ToString() untuk debugging (opsional)
    public override string ToString()
    {
        return $"Id: {Id}, GpsVendorId: {GpsVendorId}, BaseUrl: {BaseUrl}, Method: {Method}, Authtype: {Authtype}, Headers: {Headers}, Params: {Params}, Bodies: {Bodies}";
    }
}
