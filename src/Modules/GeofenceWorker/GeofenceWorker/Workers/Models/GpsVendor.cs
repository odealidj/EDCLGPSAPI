using System.Text.Json.Nodes;
using Shared.DDD;

namespace GeofenceWorker.Workers.Models;

public class GpsVendor: Aggregate<Guid>
{
    public string VendorName { get; set; } = string.Empty;

    public string LpcdId { get; set; } = string.Empty;

    public string? Timezone { get; set; } 

    public bool RequiredAuth { get; set; }

    public GpsVendorEndpoint Endpoint { get; set; } = null!; 
    public GpsVendorAuth? Auth { get; set; } 
    
       
    public static GpsVendor Create(Guid id, string vendorName, string lpcdId , string? timezone, bool requiredAuth)
    {
        ArgumentException.ThrowIfNullOrEmpty(vendorName);
        ArgumentException.ThrowIfNullOrEmpty(lpcdId);

        var gpsVendor = new GpsVendor
        {
            Id = id,
            VendorName = vendorName,
            LpcdId = lpcdId,
            Timezone = timezone,
            RequiredAuth = requiredAuth
           
        };

        ////gpsVendor.AddDomainEvent(new GpsVendorCreatedEvent(gpsVendor));

        return gpsVendor;
    }
    
    public void AddGpsVendorEndpoint(Guid id, Guid gpsVendorId, string baseUrl, string method, 
        JsonObject? headers, JsonObject? @params, JsonObject? bodies)
    {
        ArgumentException.ThrowIfNullOrEmpty(gpsVendorId.ToString());
        ArgumentException.ThrowIfNullOrEmpty(baseUrl);
        ArgumentException.ThrowIfNullOrEmpty(method);
        
        if (Endpoint != null && Endpoint.GpsVendorId == gpsVendorId)
        {
            // Update existing GpsVendorEndpoint
            Endpoint.BaseUrl = baseUrl;
            Endpoint.Method = method;
            Endpoint.Headers = headers;
            Endpoint.Params = @params;
            Endpoint.Bodies = bodies;
        }
        else
        {
            // Create new GpsVendorEndpoint
            Endpoint = new GpsVendorEndpoint(id, gpsVendorId, baseUrl, method, headers, @params, bodies);
        }
        
    }
    
    public void AddGpsVendorAuth(Guid id, Guid gpsVendorId, string baseUrl, string method, string authtype,
        JsonObject? headers, JsonObject? @params, JsonObject? bodies)
    {
        ArgumentException.ThrowIfNullOrEmpty(gpsVendorId.ToString());
        ArgumentException.ThrowIfNullOrEmpty(baseUrl);
        ArgumentException.ThrowIfNullOrEmpty(method);
        ArgumentException.ThrowIfNullOrEmpty(authtype);
        
        if (Auth != null && Auth.GpsVendorId == gpsVendorId)
        {
            // Update existing GpsVendorEndpoint
            Auth.BaseUrl = baseUrl;
            Auth.Method = method;
            Auth.Authtype = authtype;
            Auth.Headers = headers;
            Auth.Params = @params;
            Auth.Bodies = bodies;
        }
        else
        {
            // Create new GpsVendorEndpoint
            Auth = new GpsVendorAuth(id, gpsVendorId, baseUrl, method, authtype, headers, @params, bodies);
        }

    }

}