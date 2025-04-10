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
    public Guid Id { get; set; }
    public Guid GpsVendorId { get; set; }
    public string BaseUrl { get; set; }
    public string Method { get; set; }
    public string Authtype { get; set; }
    //public JsonObject? Headers { get; set; }
    public JsonObject? Headers { get; set; }
    //public JsonObject? Params { get; set; }
    public JsonObject Params { get; set; }
    //public JsonObject? Bodies { get; set; }
    
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
        JsonObject? headers = null,
        JsonObject? @params = null,
        JsonObject? bodies = null)
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