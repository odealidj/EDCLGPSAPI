namespace GeofenceMaster.GeofenceMaster.Dtos;

/*
public record GeofenceMasterDto(
    Guid? Id,
    string VendorName,
    string LpcdId,
    string? Timezone,
    bool RequiredAuth,
    List<GeofenceMasterAuthDto> Items
);

*/

public class GeofenceMasterDto
{
    // Properti
    public Guid? Id { get; set; }
    public string VendorName { get; set; }
    public string LpcdId { get; set; }
    public string? Timezone { get; set; }
    public bool RequiredAuth { get; set; }
    public List<GeofenceMasterAuthDto> Items { get; set; }

    // Constructor tanpa parameter (default)
    public GeofenceMasterDto()
    {
        Items = new List<GeofenceMasterAuthDto>(); // Inisialisasi list agar tidak null
    }

    // Constructor dengan parameter
    public GeofenceMasterDto(
        Guid? id,
        string vendorName,
        string lpcdId,
        string? timezone,
        bool requiredAuth,
        List<GeofenceMasterAuthDto> items)
    {
        Id = id;
        VendorName = vendorName;
        LpcdId = lpcdId;
        Timezone = timezone;
        RequiredAuth = requiredAuth;
        Items = items ?? new List<GeofenceMasterAuthDto>(); // Pastikan list tidak null
    }

    // Override ToString untuk debugging (opsional)
    public override string ToString()
    {
        return $"Id: {Id}, VendorName: {VendorName}, LpcdId: {LpcdId}, Timezone: {Timezone}, RequiredAuth: {RequiredAuth}, Items: [{string.Join(", ", Items)}]";
    }
}
