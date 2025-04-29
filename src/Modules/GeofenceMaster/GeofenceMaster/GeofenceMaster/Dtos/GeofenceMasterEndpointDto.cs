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
    ////public int? Page { get; set; } = 1;
    ////public int? PageSize { get; set; } = 250;
    
    public JsonObject? Headers { get; set; }
    public JsonObject? Params { get; set; }
    public JsonObject? Bodies { get; set; }
    
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
        ////int? page,
        ////int? pageSize,
        JsonObject? headers,
        JsonObject? @params,
        JsonObject? bodies )
    {
        Id = id;
        GpsVendorId = gpsVendorId;
        BaseUrl = baseUrl;
        Method = method;
        ContentType = contentType ?? "application/json";
        ////Page = method.Equals("get", StringComparison.CurrentCultureIgnoreCase) ? page : null;
        ////PageSize = method.Equals("get", StringComparison.CurrentCultureIgnoreCase) ? pageSize : null;
        Headers = headers;
        Params = @params;
        Bodies = bodies;
    }

    // Override ToString() untuk debugging (opsional)
    public override string ToString()
    {
        return $"Id: {Id}, GpsVendorId: {GpsVendorId}, BaseUrl: {BaseUrl}, Method: {Method}, Headers: {Headers}, Params: {Params}, Bodies: {Bodies}";
    }
}