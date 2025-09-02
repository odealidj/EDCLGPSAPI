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
    [JsonIgnore]
    public Guid GpsVendorId { get; set; } = Guid.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string Authtype { get; set; } = string.Empty;
    
    public string ContentType { get; set; } = "application/json";
    
    public string? Username { get; set; } = string.Empty;
    public string? Password { get; set; } = string.Empty;
    public string? TokenPath { get; set; } = string.Empty;
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
        string contentType,
        string? username,
        string? password,
        string? tokenPath,
        JsonObject? headers,
        JsonObject? @params,
        JsonObject? bodies)
    {
        Id = id;
        GpsVendorId = gpsVendorId;
        BaseUrl = baseUrl;
        Method = method;
        Authtype = authtype;
        ContentType = contentType;
        Username = !string.IsNullOrEmpty(username)?username:string.Empty;
        Password = !string.IsNullOrEmpty(password)?password:string.Empty;
        TokenPath = !string.IsNullOrEmpty(tokenPath)?tokenPath: string.Empty;
        Headers = headers;
        Params = @params;
        Bodies = bodies;
    }

    // Override ToString() untuk debugging (opsional)
    public override string ToString()
    {
        return $"Id: {Id}, GpsVendorId: {GpsVendorId}, BaseUrl: {BaseUrl}, Method: {Method}, Authtype: {Authtype}, TokenPath: {TokenPath}, Headers: {Headers}, Params: {Params}, Bodies: {Bodies}";
    }
}
