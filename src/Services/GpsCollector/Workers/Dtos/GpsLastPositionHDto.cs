namespace GeofenceWorker.Workers.Dtos;

public class GpsLastPositionHDto
{
    public Guid Id { get; set; } = default!;
    public Guid GpsVendorId { get; set; }
    public string VendorName { get; set; } = string.Empty;
    public DateTime? CreatedAt { get; set; } 
    public DateTime? LastModified { get; set; } 

    public List<GpsLastPositionDDto>  Data { get; set; }= new(); 
}

