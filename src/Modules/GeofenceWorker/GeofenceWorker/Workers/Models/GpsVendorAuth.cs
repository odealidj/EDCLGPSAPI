using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Shared.DDD;

namespace GeofenceWorker.Workers.Models;

public class GpsVendorAuth: Entity<Guid>
{
    public Guid GpsVendorId { get; set; }

    public string BaseUrl { get; set; } = string.Empty;

    public string Method { get; set; } = string.Empty;

    public string Authtype { get; set; } = string.Empty;

    // Gunakan System.Text.Json.Nodes.JsonObject atau JsonDocument untuk JSONB
    
    //public JsonObject? Headers { get; set; }
    public JsonObject? Headers { get; set; }

    public JsonObject? Params { get; set; }

    public JsonObject? Bodies { get; set; }
    
    public GpsVendor GpsVendor { get; set; }
    
    internal GpsVendorAuth(Guid gpsVendorId, string baseUrl, string method, string authtype,
        JsonObject? headers, JsonObject? @params, JsonObject? bodies)
        
    {
        GpsVendorId = gpsVendorId;
        BaseUrl = baseUrl;
        Method = method;
        Authtype = authtype;
        Headers = headers;
        Params = @params;
        Bodies = bodies;
       
    }
    
    [JsonConstructor]
    public GpsVendorAuth(Guid id, Guid gpsVendorId, string baseUrl, string method, string authtype, 
        JsonObject? headers, JsonObject? @params, JsonObject? bodies)
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

    [JsonConstructor]
    public GpsVendorAuth()
    {
    }
}