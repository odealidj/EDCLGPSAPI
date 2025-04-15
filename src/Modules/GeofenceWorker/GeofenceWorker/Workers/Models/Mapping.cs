namespace GeofenceWorker.Workers.Models;

public class Mapping : Entity<int>
{
    public Guid GpsVendorId { get; set; }

    public string ResponseField { get; set; } =
        string.Empty; // Nama field dari JSON respons vendor (misal: vehicleNumber)

    public string MappedField { get; set; } =
        string.Empty; // Nama field yang dipetakan dalam sistem/database (misal: PLAT_NO)

    public string? DataPath { get; set; } = string.Empty; // Nama field dari JSON respons vendor (misal: vehicleNumber)

}