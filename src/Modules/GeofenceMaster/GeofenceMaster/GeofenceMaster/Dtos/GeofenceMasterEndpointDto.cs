namespace GeofenceMaster.GeofenceMaster.Dtos;

public class GeofenceMasterEndpointDto
{
    // Properti
    public Guid Id { get; set; } = Guid.Empty;
    public Guid GpsVendorId { get; set; } = Guid.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
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
        JsonObject? headers,
        JsonObject? @params,
        JsonObject? bodies )
    {
        Id = id;
        GpsVendorId = gpsVendorId;
        BaseUrl = baseUrl;
        Method = method;
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