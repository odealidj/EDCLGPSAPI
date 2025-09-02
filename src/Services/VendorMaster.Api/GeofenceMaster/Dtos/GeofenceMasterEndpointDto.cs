namespace GeofenceMaster.GeofenceMaster.Dtos;

public class GeofenceMasterEndpointDto
{
    // Properti
    public Guid Id { get; set; } = Guid.Empty;
    [JsonIgnore]
    public Guid GpsVendorId { get; set; } = Guid.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string? ContentType { get; set; } = "application/json";
    public JsonObject? Headers { get; set; }
    public JsonObject? Params { get; set; }
    public JsonObject? Bodies { get; set; }
    
    public JsonObject? VarParams { get; set; }
    
    public string? MaxPath { get; set; }
    
    // Constructor tanpa parameter (default)
    public GeofenceMasterEndpointDto()
    {
    }
    
    // Constructor dengan parameter
    public GeofenceMasterEndpointDto(
        Guid id,
        Guid gpsVendorId,
        string baseUrl,
        string method,
        string? contentType,
        JsonObject? headers,
        JsonObject? @params,
        JsonObject? bodies,
        JsonObject? varParams,
        string? maxPath
        )
    {
        Id = id;
        GpsVendorId = gpsVendorId;
        BaseUrl = baseUrl;
        Method = method;
        ContentType = contentType ?? "application/json";
        Headers = headers;
        Params = @params;
        Bodies = bodies;
        VarParams = varParams;
        MaxPath = maxPath;
    }

    // Override ToString() untuk debugging (opsional)
    public override string ToString()
    {
        return $"Id: {Id}, GpsVendorId: {GpsVendorId}, BaseUrl: {BaseUrl}, Method: {Method}, Headers: {Headers}, Params: {Params}, Bodies: {Bodies}";
    }
}