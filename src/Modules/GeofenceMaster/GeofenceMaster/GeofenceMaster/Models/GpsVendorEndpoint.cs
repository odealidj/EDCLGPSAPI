namespace GeofenceMaster.GeofenceMaster.Models;

public class GpsVendorEndpoint : Entity<Guid>
{
    public Guid GpsVendorId { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public string? ContentType { get; set; } = "application/json";
    public JsonObject? Headers { get; set; }
    public JsonObject? Params { get; set; }
    public JsonObject? Bodies { get; set; }
    
    public JsonObject? VarParams { get; set; }
    
    public string? MaxPath { get; set; }

    internal GpsVendorEndpoint(Guid gpsVendorId, string baseUrl, string method,
        string? contentType, 
        JsonObject? headers, JsonObject? @params, JsonObject? bodies, JsonObject? varParams,
        string? maxPath 
        )
    {
        GpsVendorId = gpsVendorId;
        BaseUrl = baseUrl;
        Method = method;
        ContentType = contentType ?? "application/json";
        ////Page = method.Equals("get", StringComparison.CurrentCultureIgnoreCase) ? page : null;
        ////PageSize = method.Equals("get", StringComparison.CurrentCultureIgnoreCase) ? pageSize : null;
        Headers = headers;
        Params = @params;
        Bodies = bodies;
        VarParams = varParams;
        MaxPath = maxPath;
    }

    [JsonConstructor]
    public GpsVendorEndpoint(Guid id, Guid gpsVendorId, string baseUrl, string method,
        string? contentType, 
        JsonObject? headers, JsonObject? @params, JsonObject? bodies, JsonObject? varParams,
        string? maxPath )
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
        VarParams = varParams;
        MaxPath = maxPath;

    }

    [JsonConstructor]
    public GpsVendorEndpoint()
    {
    }
}