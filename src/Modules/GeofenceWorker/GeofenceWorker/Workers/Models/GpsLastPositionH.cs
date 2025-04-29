namespace GeofenceWorker.Workers.Models;

public class GpsLastPositionH : Entity<Guid>
{
    public Guid GpsVendorId { get; set; }
    
    private readonly List<GpsLastPositionD>  _lastPositionDs = new();
    public IReadOnlyList<GpsLastPositionD> LastPositionDs => _lastPositionDs.AsReadOnly();
    
    
    public static GpsLastPositionH Create(Guid id, Guid gpsVendorId)
    {
        //ArgumentException.ThrowIfNullOrEmpty(vendorName);
        //ArgumentException.ThrowIfNullOrEmpty(lpcdId);

        var gpsLastPositionH = new GpsLastPositionH
        {
            Id = id,
            GpsVendorId = gpsVendorId
        };

        ////gpsVendor.AddDomainEvent(new GpsVendorCreatedEvent(gpsVendor));

        return gpsLastPositionH;
    }
    
    public void AddGpsLastPositionD(
        Guid id, 
        Guid gpsLastPositionHId,
        string? lpcd, 
        string platNo, 
        string deviceId, 
        DateTime datetime, 
        decimal? x,
        decimal? y,
        decimal? speed,
        decimal? course,
        string? streetName)
    {
        //ArgumentException.ThrowIfNullOrEmpty(gpsVendorId.ToString());

        var existingItem = LastPositionDs.FirstOrDefault(x => x. Id == id);

        if (existingItem != null)
        {
            existingItem.GpsLastPositionHId = gpsLastPositionHId;
            existingItem.Lpcd = lpcd;
            existingItem.PlatNo = platNo;
            existingItem.DeviceId = deviceId;
            existingItem.Datetime = datetime;
            existingItem.X = x;
            existingItem.Y = y;
            existingItem.Speed = speed;
            existingItem.Course = course;
            existingItem.StreetName = streetName;
        }
        else
        {
            var lastPositionD = new GpsLastPositionD(id, gpsLastPositionHId, lpcd, platNo, deviceId, datetime, x, y, speed,course, streetName );
            _lastPositionDs.Add(lastPositionD);
        }
    }
    
    public void AddGpsLastPositionD(
        GpsLastPositionD gpsLastPositionD)
    {
        //ArgumentException.ThrowIfNullOrEmpty(gpsVendorId.ToString());

        var existingItem = LastPositionDs.FirstOrDefault(x => x. Id == gpsLastPositionD.Id);

        if (existingItem != null)
        {
            existingItem.GpsLastPositionHId = gpsLastPositionD.Id;
            existingItem.Lpcd = gpsLastPositionD.Lpcd;
            existingItem.PlatNo = gpsLastPositionD.PlatNo;
            existingItem.DeviceId = gpsLastPositionD.DeviceId;
            existingItem.Datetime = gpsLastPositionD.Datetime;
            existingItem.X = gpsLastPositionD.X;
            existingItem.Y = gpsLastPositionD.Y;
            existingItem.Speed = gpsLastPositionD.Speed;
            existingItem.Course = gpsLastPositionD.Course;
            existingItem.StreetName = gpsLastPositionD.StreetName;
        }
        else
        {
            _lastPositionDs.Add(gpsLastPositionD);
        }
    }
}