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
    
    ////public List<string> Lpcds { get; set; } = new List<string>(); // Menggunakan List<string> untuk menyimpan beberapa LpcdId
    ////public string LpcdId { get; set; } = string.Empty;
    
    [JsonIgnore]
    public string? Timezone { get; set; }
    public bool RequiredAuth { get; set; }
    
    public string? AuthType { get; set; } = "NoAuth";
    
    public string? Username { get; set; } 
    
    public string? Password { get; set; } = string.Empty;
    
    public string ProcessingStrategy { get; set; } = "Individual"; // Default value
    
    public string? ProcessingStrategyPathData{ get; set; } = string.Empty; // Default value
    
    public string? ProcessingStrategyPathKey { get; set; } = string.Empty;
    
    public List<GeofenceMasterLpcdDto> Lpcds { get; set; } = []; // Inisialisasi list agar tidak null

    public List<GeofenceMasterEndpointDto> GeofenceMasterEndpoints { get; set; } = [];
    public GeofenceMasterAuthDto? GeofenceMasterAuth { get; set; } 
    public List<GeofenceMasterMappingDto> GeofenceMasterMappings { get; set; } = []; // Inisialisasi list agar tidak null
    
    

    // Constructor tanpa parameter (default)
    public GeofenceMasterDto()
    {
    }

    // Constructor dengan parameter
    public GeofenceMasterDto(
        Guid? id,
        string vendorName,
        ////List<string> lpcds,
        string? timezone,
        bool requiredAuth,
        string? authType,
        string? username,
        string? password,
        string processingStrategy,
        string? processingStrategyPathData,
        string? processingStrategyPathKey,
        List<GeofenceMasterEndpointDto> geofenceMasterEndpoints,
        GeofenceMasterAuthDto? geofenceMasterAuth,
        List<GeofenceMasterMappingDto> geofenceMasterMappings,
        List<GeofenceMasterLpcdDto> lpcds
        )
    {
        Id = id;
        VendorName = vendorName;
        ////LpcdId = lpcdId;
        ///Lpcds = lpcds;
        Timezone = timezone;
        RequiredAuth = requiredAuth;
        AuthType = authType;
        Username = username;
        Password = !string.IsNullOrEmpty(password) ? password : string.Empty; // Default value
        ProcessingStrategy = processingStrategy; // Default value
        ProcessingStrategyPathData = !string.IsNullOrEmpty(processingStrategyPathData)?processingStrategyPathData: string.Empty; // Default value
        ProcessingStrategyPathKey = !string.IsNullOrEmpty(processingStrategyPathKey)?processingStrategyPathKey: string.Empty ; // Default value
        GeofenceMasterEndpoints = geofenceMasterEndpoints ?? []; // Pastikan list tidak null
        GeofenceMasterAuth = geofenceMasterAuth; // Pastikan list tidak null
        GeofenceMasterMappings = geofenceMasterMappings ?? []; // Pastikan list tidak null
        Lpcds = lpcds ?? []; // Pastikan list tidak null
    }
    
    // Override ToString untuk debugging (opsional)
    public override string ToString()
    {
        return $"Id: {Id}, VendorName: {VendorName}, Timezone: {Timezone}, RequiredAuth: {RequiredAuth}, AuthType: {AuthType}, Username: {Username}, Password: {Password}, ProcessingStrategy: {ProcessingStrategy}, ProcessingStrategyPathData: {ProcessingStrategyPathData}, ProcessingStrategyPathKey: {ProcessingStrategyPathKey}";
    }

}
