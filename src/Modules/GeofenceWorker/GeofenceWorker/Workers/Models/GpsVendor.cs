using System.Text.Json.Nodes;
using Shared.DDD;

namespace GeofenceWorker.Workers.Models;

public class GpsVendor: Aggregate<Guid>
{
    public string VendorName { get; set; } = string.Empty;

    public string LpcdId { get; set; } = string.Empty;

    public string? Timezone { get; set; } 

    public bool RequiredAuth { get; set; }

    public string ProcessingStrategy { get; set; } = "Individual";
    
    public string? ProcessingStrategyPathData{ get; set; }
    
    public string? ProcessingStrategyPathColumn { get; set; }
    
    private readonly List<GpsVendorEndpoint>  _endpoints = new();
    public IReadOnlyList<GpsVendorEndpoint> Endpoints => _endpoints.AsReadOnly();
    
    ////private readonly List<GpsVendorAuth>  _gpsVendorAuths = new();
    ////public IReadOnlyList<GpsVendorAuth> GpsVendorAuths => _gpsVendorAuths.AsReadOnly();

    ////public GpsVendorEndpoint Endpoint { get; set; } = null!; 
    public GpsVendorAuth? Auth { get; set; } 
    
       
    public static GpsVendor Create(Guid id, string vendorName, string lpcdId , string? timezone, bool requiredAuth, 
        string processingStrategy, string? processingStrategyPathData, string? processingStrategyPathColumn )
    {
        ArgumentException.ThrowIfNullOrEmpty(vendorName);
        ArgumentException.ThrowIfNullOrEmpty(lpcdId);

        var gpsVendor = new GpsVendor
        {
            Id = id,
            VendorName = vendorName,
            LpcdId = lpcdId,
            Timezone = timezone,
            RequiredAuth = requiredAuth,
            ProcessingStrategy = processingStrategy,
            ProcessingStrategyPathData = processingStrategyPathData,
            ProcessingStrategyPathColumn = processingStrategyPathColumn
        };

        ////gpsVendor.AddDomainEvent(new GpsVendorCreatedEvent(gpsVendor));

        return gpsVendor;
    }
    
    /*
    public void AddGpsVendorEndpoint(Guid id, Guid gpsVendorId, string baseUrl, string method, 
        string contentType,
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
            Endpoint.ContentType = contentType;
            Endpoint.Headers = headers;
            Endpoint.Params = @params;
            Endpoint.Bodies = bodies;
        }
        else
        {
            // Create new GpsVendorEndpoint
            Endpoint = new GpsVendorEndpoint(id, gpsVendorId, baseUrl,  method, contentType, headers, @params, bodies);
        }
        
    }
    */
    
    public void AddGpsVendorEndpoint(Guid id, Guid gpsVendorId, string baseUrl, string method, 
        string contentType,
        JsonObject? headers, JsonObject? @params, JsonObject? bodies)
    {
        ArgumentException.ThrowIfNullOrEmpty(gpsVendorId.ToString());
        ArgumentException.ThrowIfNullOrEmpty(baseUrl);
        ArgumentException.ThrowIfNullOrEmpty(method);

        var existingItem = Endpoints.FirstOrDefault(x => x.Id == id);

        if (existingItem != null)
        {
            existingItem.GpsVendorId = gpsVendorId;
            existingItem.BaseUrl = baseUrl;
            existingItem.Method = method;
            existingItem.Headers = headers;
            existingItem.Params = @params;
            existingItem.Bodies = bodies;
            
        }
        else
        {
            var newItem = new GpsVendorEndpoint(id, gpsVendorId, baseUrl, method, contentType, headers, @params, bodies);
            _endpoints.Add(newItem);
        }
    }
    
    public void AddGpsVendorAuth(Guid id, Guid gpsVendorId, string baseUrl, string method, string authtype,
        string contentType, string? username, string? password,
        string tokenPath,
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
            Auth.ContentType = contentType;
            Auth.Username = username;
            Auth.Password = password;
            Auth.TokenPath = tokenPath;
            Auth.Headers = headers;
            Auth.Params = @params;
            Auth.Bodies = bodies;
        }
        else
        {
            // Create new GpsVendorEndpoint
            Auth = new GpsVendorAuth(id, gpsVendorId, baseUrl, method, authtype, contentType, username, password, tokenPath, headers, @params, bodies);
        }

    }

}