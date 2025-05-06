using System.Text.Json.Nodes;
using GeofenceWorker.Workers.Exceptions;
using Shared.DDD;

namespace GeofenceWorker.Workers.Models;

public class GpsVendor: Aggregate<Guid>
{
    public string VendorName { get; set; } = string.Empty;

    public string? Timezone { get; set; } 

    public bool RequiredAuth { get; set; }
    
    public string? AuthType { get; set; } = "NoAuth";
    
    public string? Username { get; set; } 
    
    public string? Password { get; set; } = string.Empty;
    
    public string? ProcessingStrategy { get; set; } = "Individual";
    
    public string? ProcessingStrategyPathData{ get; set; } = "data";
    
    public string? ProcessingStrategyPathKey { get; set; } = string.Empty;
    
    private readonly List<GpsVendorEndpoint>  _gpsVendorEndpoints = new();
    public IReadOnlyList<GpsVendorEndpoint> GpsVendorEndpoints => _gpsVendorEndpoints.AsReadOnly();
    
    private readonly List<Mapping>  _mappings = new();
    public IReadOnlyList<Mapping> Mappings => _mappings.AsReadOnly();
    
    private readonly List<GpsVendorLpcd>  _lpcds = new();
    public IReadOnlyList<GpsVendorLpcd> Lpcds => _lpcds.AsReadOnly();
    
    ////private readonly List<GpsVendorAuth>  _gpsVendorAuths = new();
    ////public IReadOnlyList<GpsVendorAuth> GpsVendorAuths => _gpsVendorAuths.AsReadOnly();

    ////public GpsVendorEndpoint Endpoint { get; set; } = null!; 
    public GpsVendorAuth? Auth { get; set; } 
    
       
    public static GpsVendor Create(Guid id, string vendorName, string lpcdId , string? timezone, bool requiredAuth, 
        string? authType, string? username, string? password,
        string? processingStrategy, string? processingStrategyPathData, string? processingStrategyPathKey )
    {
        ArgumentException.ThrowIfNullOrEmpty(vendorName);
        ArgumentException.ThrowIfNullOrEmpty(lpcdId);

        var gpsVendor = new GpsVendor
        {
            Id = id,
            VendorName = vendorName,
            Timezone = timezone,
            RequiredAuth = requiredAuth,
            AuthType = authType,
            Username = username,
            Password = !string.IsNullOrEmpty(password)? password: string.Empty,
            ProcessingStrategy = processingStrategy??"Individual" ,
            ProcessingStrategyPathData = processingStrategyPathData?? string.Empty,
            ProcessingStrategyPathKey = processingStrategyPathKey ?? string.Empty
        };

        ////gpsVendor.AddDomainEvent(new GpsVendorCreatedEvent(gpsVendor));

        return gpsVendor;
    }
    
    
    public void AddGpsVendorEndpoint(Guid id, Guid gpsVendorId, string baseUrl, string method, 
        string? contentType, ////int? page, int? pageSize,
        JsonObject? headers, JsonObject? @params, JsonObject? bodies,
        string? maxPath)
    {
        ArgumentException.ThrowIfNullOrEmpty(gpsVendorId.ToString());
        ArgumentException.ThrowIfNullOrEmpty(baseUrl);
        ArgumentException.ThrowIfNullOrEmpty(method);

        var existingItem = GpsVendorEndpoints.FirstOrDefault(x => x.Id == id);

        if (existingItem != null)
        {
            existingItem.GpsVendorId = gpsVendorId;
            existingItem.BaseUrl = baseUrl;
            existingItem.Method = method;
            existingItem.ContentType = contentType?? "application/json";
            ////existingItem.Page = method.Equals("get", StringComparison.CurrentCultureIgnoreCase) ? page: null;
            ////existingItem.PageSize = method.Equals("get", StringComparison.CurrentCultureIgnoreCase) ? pageSize : null;
            existingItem.Headers = headers;
            existingItem.Params = @params;
            existingItem.Bodies = bodies;
            existingItem.MaxPath = maxPath;
            
        }
        else
        {
            var newItem = new GpsVendorEndpoint(id, gpsVendorId, baseUrl, method, contentType, headers, @params, bodies, maxPath);
            _gpsVendorEndpoints.Add(newItem);
        }
    }
    
    public void AddMapping(int? id, Guid gpsVendorId, string responseField, string mappedField)
    {
        ArgumentException.ThrowIfNullOrEmpty(gpsVendorId.ToString());
        ArgumentException.ThrowIfNullOrEmpty(responseField);
        ArgumentException.ThrowIfNullOrEmpty(mappedField);

        if (id != null)
        {
            var existingItem = Mappings.FirstOrDefault(x => x.Id == id);
            if (existingItem == null) throw new GeofenceMasterMappingNotFoundException(id.Value);
            
            existingItem.GpsVendorId = gpsVendorId;
            existingItem.ResponseField = responseField;
            existingItem.MappedField = mappedField;
        }
        else
        {
            var newItem = new Mapping(gpsVendorId,responseField, mappedField);
            _mappings.Add(newItem);
        }
    }
    
    public void AddLpcd(Guid id, Guid gpsVendorId, string lpcd)
    {
        ArgumentException.ThrowIfNullOrEmpty(gpsVendorId.ToString());
        ArgumentException.ThrowIfNullOrEmpty(lpcd);
        
        var existingItem = Lpcds.FirstOrDefault(x => x.Id == id);
        if (existingItem != null) 
        {   
            existingItem.GpsVendorId = gpsVendorId;
            existingItem.Lpcd = lpcd;
        }
        else
        {
            var newItem = new GpsVendorLpcd(id, gpsVendorId, lpcd);
            _lpcds.Add(newItem);
        }
    }

}