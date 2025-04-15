namespace GeofenceWorker.Workers.Models;

public class ResponseFormat : Entity<int>
{
    public Guid GpsVendorId { get; set; }

    public string ResponsePath { get; set; } = string.Empty; // message.data.token
    
    public string FieldType { get; set; } = string.Empty; // string, int, bool, etc

    public string MappedField { get; set; } = string.Empty; //AUTH_TOKEN

}