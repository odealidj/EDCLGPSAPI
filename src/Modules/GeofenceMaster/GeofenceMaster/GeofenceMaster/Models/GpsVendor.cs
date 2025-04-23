namespace GeofenceMaster.GeofenceMaster.Models;

public class GpsVendor: Aggregate<Guid>
{
    public string VendorName { get; set; } = string.Empty;

    public string LpcdId { get; set; } = string.Empty;

    public string? Timezone { get; set; } 

    public bool? RequiredAuth { get; set; } = false;
    
    public string ProcessingStrategy { get; set; } = "Individual";
    
    public string? ProcessingStrategyPathData{ get; set; }
    
    public string ProcessingStrategyPathColumn { get; set; }
    
    private readonly List<GpsVendorEndpoint>  _gpsVendorEndpoints = new();
    public IReadOnlyList<GpsVendorEndpoint> GpsVendorEndpoints => _gpsVendorEndpoints.AsReadOnly();
    
    private readonly List<GpsVendorAuth>  _gpsVendorAuths = new();
    public IReadOnlyList<GpsVendorAuth> GpsVendorAuths => _gpsVendorAuths.AsReadOnly();
    
    public static GpsVendor Create(Guid id, string vendorName, string lpcdId , string? timezone, bool requiredAuth, 
        string processingStrategy, string? processingStrategyPathData, string processingStrategyPathColumn)
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
    
    public void AddGpsVendorAuth(Guid id, Guid gpsVendorId, string baseUrl, string method, string authtype,
        string contentType, string? username, string? password,
        string tokenPath,
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
            existingItem.ContentType = contentType;
            existingItem.Username = username;
            existingItem.Password = password;
            existingItem.TokenPath = tokenPath;
            existingItem.Headers = headers;
            existingItem.Params = @params;
            existingItem.Bodies = bodies;
            
        }
        else
        {
            var newItem = new GpsVendorAuth(
                id, 
                gpsVendorId, 
                baseUrl, 
                method, 
                authtype, 
                contentType,
                username,
                password,
                tokenPath, headers, @params, bodies);
            _gpsVendorAuths.Add(newItem);
        }
    }

}