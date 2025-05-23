namespace GeofenceMaster.GeofenceMaster.Models;

public class GpsVendor: Aggregate<Guid>
{
    public string VendorName { get; set; } = string.Empty;

    public string LpcdId { get; set; } = string.Empty;

    public string? Timezone { get; set; } 

    public bool? RequiredAuth { get; set; } = false;
    
    private readonly List<GpsVendorEndpoint>  _gpsVendorEndpoints = new();
    public IReadOnlyList<GpsVendorEndpoint> GpsVendorEndpoints => _gpsVendorEndpoints.AsReadOnly();
    
    private readonly List<GpsVendorAuth>  _gpsVendorAuths = new();
    public IReadOnlyList<GpsVendorAuth> GpsVendorAuths => _gpsVendorAuths.AsReadOnly();
    
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

        var existingItem = GpsVendorEndpoints.FirstOrDefault(x => x.Id == id);

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
            var newItem = new GpsVendorEndpoint(id, gpsVendorId, baseUrl, method, headers, @params, bodies);
            _gpsVendorEndpoints.Add(newItem);
        }
    }
    
    public void AddGpsVendorAuth(Guid id, Guid gpsVendorId, string baseUrl, string method, string authtype, string tokenPath,
        JsonObject? headers, JsonObject? @params, JsonObject? bodies)
    {
        ArgumentException.ThrowIfNullOrEmpty(gpsVendorId.ToString());
        ArgumentException.ThrowIfNullOrEmpty(baseUrl);
        ArgumentException.ThrowIfNullOrEmpty(method);
        ArgumentException.ThrowIfNullOrEmpty(authtype);


        var existingItem = GpsVendorAuths.FirstOrDefault(x => x.Id == id);

        if (existingItem != null)
        {
            existingItem.GpsVendorId = gpsVendorId;
            existingItem.BaseUrl = baseUrl;
            existingItem.Method = method;
            existingItem.Authtype = authtype;
            existingItem.TokenPath = tokenPath;
            existingItem.Headers = headers;
            existingItem.Params = @params;
            existingItem.Bodies = bodies;
            
        }
        else
        {
            var newItem = new GpsVendorAuth(id, gpsVendorId, baseUrl, method, authtype, tokenPath, headers, @params, bodies);
            _gpsVendorAuths.Add(newItem);
        }
    }

}