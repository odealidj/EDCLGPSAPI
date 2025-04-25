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
    public Guid? Id { get; set; } = Guid.Empty; // Menggunakan Guid.Empty sebagai nilai default
    public string VendorName { get; set; } = string.Empty; 
    public string LpcdId { get; set; } = string.Empty;
    public string? Timezone { get; set; }
    public bool RequiredAuth { get; set; }
    
    public string? ProcessingStrategy { get; set; } = "Individual"; // Default value
    
    public string? ProcessingStrategyPathData{ get; set; } = "data"; // Default value
    
    public string? ProcessingStrategyPathKey { get; set; } = string.Empty;

    public List<GeofenceMasterEndpointDto> GeofenceMasterEndpoints { get; set; }
    public List<GeofenceMasterAuthDto> GeofenceMasterAuths { get; set; }

    // Constructor tanpa parameter (default)
    public GeofenceMasterDto()
    {
        GeofenceMasterEndpoints = new List<GeofenceMasterEndpointDto>();
        GeofenceMasterAuths = new List<GeofenceMasterAuthDto>(); // Inisialisasi list agar tidak null
    }

    // Constructor dengan parameter
    public GeofenceMasterDto(
        Guid? id,
        string vendorName,
        string lpcdId,
        string? timezone,
        bool requiredAuth,
        string? processingStrategy,
        string? processingStrategyPathData,
        string? processingStrategyPathKey,
        List<GeofenceMasterEndpointDto> geofenceMasterEndpoints,
        List<GeofenceMasterAuthDto> geofenceMasterAuths)
    {
        Id = id;
        VendorName = vendorName;
        LpcdId = lpcdId;
        Timezone = timezone;
        RequiredAuth = requiredAuth;
        ProcessingStrategy = processingStrategy ?? "Individual"; // Default value
        ProcessingStrategyPathData = processingStrategyPathData ?? "data"; // Default value
        ProcessingStrategyPathKey = processingStrategyPathKey ?? string.Empty; // Default value
        GeofenceMasterEndpoints = geofenceMasterEndpoints ?? []; // Pastikan list tidak null
        GeofenceMasterAuths = geofenceMasterAuths ?? []; // Pastikan list tidak null
    }
    
    // Override ToString untuk debugging (opsional)
    public override string ToString()
    {
        return $"Id: {Id}, VendorName: {VendorName}, LpcdId: {LpcdId}, Timezone: {Timezone}, RequiredAuth: {RequiredAuth}, GeofenceMasterEndpoints: [{string.Join(", ", GeofenceMasterEndpoints)}], GeofenceMasterAuths: [{string.Join(", ", GeofenceMasterAuths)}]";
    }
}
