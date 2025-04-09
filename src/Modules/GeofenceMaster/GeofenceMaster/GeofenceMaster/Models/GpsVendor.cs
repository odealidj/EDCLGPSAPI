namespace GeofenceMaster.GeofenceMaster.Models;

public class GpsVendor: Aggregate<Guid>
{
    public string VendorName { get; set; }

    public string LpcdId { get; set; }

    public string? Timezone { get; set; }

    public bool? RequiredAuth { get; set; } = false;
    
    private readonly List<GpsVendorAuth> _items = new();
    public IReadOnlyList<GpsVendorAuth> Items => _items.AsReadOnly();
    
    public static GpsVendor Create(Guid id, string vendorName, string lpcdId , string timezone, bool requiredAuth)
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
    
    public void AddGpsVendorAuth(Guid id, Guid gpsVendorId, string baseUrl, string method, string authtype,
        JsonObject? headers, JsonObject? @params, JsonObject? bodies)
    {
        ArgumentException.ThrowIfNullOrEmpty(gpsVendorId.ToString());
        ArgumentException.ThrowIfNullOrEmpty(baseUrl);
        ArgumentException.ThrowIfNullOrEmpty(method);
        ArgumentException.ThrowIfNullOrEmpty(authtype);


        var existingItem = Items.FirstOrDefault(x => x.GpsVendorId == gpsVendorId);

        if (existingItem != null)
        {
            existingItem.GpsVendorId = gpsVendorId;
            existingItem.BaseUrl = baseUrl;
            existingItem.Method = method;
            existingItem.Authtype = authtype;
            existingItem.Headers = headers;
            existingItem.Params = @params;
            existingItem.Bodies = bodies;
            
        }
        else
        {
            var newItem = new GpsVendorAuth(id, gpsVendorId, baseUrl, method, authtype, headers, @params, bodies);
            //var newItem = new GpsVendorAuth(Id, baseUrl, method, authtype);
            _items.Add(newItem);
        }
    }

}