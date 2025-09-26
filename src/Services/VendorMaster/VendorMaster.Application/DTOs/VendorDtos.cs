using System.Text.Json.Serialization;

namespace VendorMaster.Application.DTOs;

// dto utama master vendor gps (kompatibel format baru dan legacy geofencemaster)
public class VendorDto
{
    public Guid Id { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public string ProtocolType { get; set; } = VendorDefaults.DefaultProtocolType;
    public string Timezone { get; set; } = VendorDefaults.DefaultTimezone;
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = VendorDefaults.DefaultVersion;
    public string ProcessingStrategy { get; set; } = VendorDefaults.DefaultProcessingStrategy;
    public string? ProcessingStrategyPathData { get; set; }
    public string? ProcessingStrategyPathKey { get; set; }

    // child collections
    public List<VendorEndpointDto> Endpoints { get; set; } = [];
    public VendorAuthDto? Auth { get; set; }
    public List<VendorMappingDto> Mappings { get; set; } = [];
    public List<VendorLpcdDto> Lpcds { get; set; } = [];

    // convenience shortcut single lpcd dan array string
    private string? _lpcd;
    public string? Lpcd
    {
        get => _lpcd ?? Lpcds.FirstOrDefault()?.Lpcd;
        set => _lpcd = value;
    }

    private List<string>? _lpcdCodes;
    public List<string> LpcdCodes
    {
        get => _lpcdCodes ?? Lpcds.Select(x => x.Lpcd).ToList();
        set => _lpcdCodes = value;
    }

    // kompatibilitas buat request legacy geofencemaster (diabaikan saat serialisasi response)
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<VendorEndpointDto>? GeofenceMasterEndpoints
    {
        get => null;
        set => Endpoints = (Endpoints.Count == 0 && value != null) ? value : Endpoints;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public VendorAuthDto? GeofenceMasterAuth
    {
        get => null;
        set => Auth ??= value;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<VendorMappingDto>? GeofenceMasterMappings
    {
        get => null;
        set => Mappings = (Mappings.Count == 0 && value != null) ? value : Mappings;
    }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<VendorLpcdDto>? GeofenceMasterLpcds
    {
        get => null;
        set => Lpcds = (Lpcds.Count == 0 && value != null) ? value : Lpcds;
    }

    public bool? RequiredAuth { get; set; }
    public string? AuthType { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }

    public VendorDto() { }

    public VendorDto(
        Guid id,
        string vendorName,
        string? timezone,
        bool? requiredAuth,
        string? authType,
        string? username,
        string? password,
        string processingStrategy,
        string? processingStrategyPathData,
        string? processingStrategyPathKey,
        List<VendorEndpointDto>? endpoints,
        VendorAuthDto? auth,
        List<VendorMappingDto>? mappings,
        List<VendorLpcdDto>? lpcds,
        string? vendorCode = null,
        string? protocolType = VendorDefaults.DefaultProtocolType,
        bool isActive = true)
    {
        Id = id;
        VendorName = vendorName;
        VendorCode = string.IsNullOrWhiteSpace(vendorCode)
            ? vendorName.Replace(" ", "_").ToUpperInvariant()
            : vendorCode;
        ProtocolType = protocolType ?? VendorDefaults.DefaultProtocolType;
        Timezone = timezone ?? VendorDefaults.DefaultTimezone;
        IsActive = isActive;
        RequiredAuth = requiredAuth;
        AuthType = authType;
        Username = username;
        Password = password;
        ProcessingStrategy = processingStrategy ?? VendorDefaults.DefaultProcessingStrategy;
        ProcessingStrategyPathData = processingStrategyPathData;
        ProcessingStrategyPathKey = processingStrategyPathKey;
        Endpoints = endpoints ?? [];
        Auth = auth;
        Mappings = mappings ?? [];
        Lpcds = lpcds ?? [];
    }
}

// dto endpoint http rest vendor
public class VendorEndpointDto
{
    public Guid Id { get; set; }
    public Guid GpsVendorId { get; set; }
    public string EndpointType { get; set; } = VendorDefaults.DefaultEndpointType;
    public string BaseUrl { get; set; } = string.Empty;
    public string Method { get; set; } = VendorDefaults.DefaultHttpMethod;
    public string ContentType { get; set; } = VendorDefaults.DefaultContentType;
    public JsonObject? Headers { get; set; }
    public JsonObject? Params { get; set; }
    public JsonObject? Bodies { get; set; }
    public JsonObject? VarParams { get; set; }
    public string? MaxPath { get; set; }

    public VendorEndpointDto() { }

    public VendorEndpointDto(
        Guid id,
        Guid gpsVendorId,
        string baseUrl,
        string method,
        string? contentType,
        JsonObject? headers,
        JsonObject? @params,
        JsonObject? bodies,
        JsonObject? varParams,
        string? maxPath,
        string? endpointType = VendorDefaults.DefaultEndpointType)
    {
        Id = id;
        GpsVendorId = gpsVendorId;
        EndpointType = endpointType ?? VendorDefaults.DefaultEndpointType;
        BaseUrl = baseUrl;
        Method = string.IsNullOrWhiteSpace(method) ? VendorDefaults.DefaultHttpMethod : method;
        ContentType = contentType ?? VendorDefaults.DefaultContentType;
        Headers = headers;
        Params = @params;
        Bodies = bodies;
        VarParams = varParams;
        MaxPath = maxPath;
    }
}

// dto autentikasi vendor
public class VendorAuthDto
{
    public Guid Id { get; set; }
    public Guid GpsVendorId { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string Method { get; set; } = VendorDefaults.DefaultAuthHttpMethod;
    public string Authtype { get; set; } = VendorDefaults.DefaultAuthType;
    public string ContentType { get; set; } = VendorDefaults.DefaultContentType;
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? TokenPath { get; set; }
    public string? TokenExpiresInPath { get; set; }
    public int TokenTtlSeconds { get; set; } = VendorDefaults.DefaultTokenTtlSeconds;
    public JsonObject? Headers { get; set; }
    public JsonObject? Params { get; set; }
    public JsonObject? Bodies { get; set; }

    public VendorAuthDto() { }

    public VendorAuthDto(
        Guid id,
        Guid gpsVendorId,
        string baseUrl,
        string method,
        string authtype,
        string? contentType,
        string? username,
        string? password,
        string? tokenPath,
        JsonObject? headers,
        JsonObject? @params,
        JsonObject? bodies,
        string? tokenExpiresInPath = null,
        int tokenTtlSeconds = VendorDefaults.DefaultTokenTtlSeconds)
    {
        Id = id;
        GpsVendorId = gpsVendorId;
        BaseUrl = baseUrl;
        Method = string.IsNullOrWhiteSpace(method) ? VendorDefaults.DefaultAuthHttpMethod : method;
        Authtype = string.IsNullOrWhiteSpace(authtype) ? VendorDefaults.DefaultAuthType : authtype;
        ContentType = contentType ?? VendorDefaults.DefaultContentType;
        Username = username;
        Password = password;
        TokenPath = tokenPath;
        TokenExpiresInPath = tokenExpiresInPath;
        TokenTtlSeconds = tokenTtlSeconds <= 0 ? VendorDefaults.DefaultTokenTtlSeconds : tokenTtlSeconds;
        Headers = headers;
        Params = @params;
        Bodies = bodies;
    }
}

// dto dynamic mapping field vendor -> field standar edcl
public class VendorMappingDto
{
    public int Id { get; set; }
    public Guid GpsVendorId { get; set; }
    public string ResponseField { get; set; } = string.Empty;
    public string MappedField { get; set; } = string.Empty;
    public string DataType { get; set; } = VendorDefaults.DefaultMappingDataType;

    public VendorMappingDto() { }

    public VendorMappingDto(Guid gpsVendorId, string responseField, string mappedField, string dataType = VendorDefaults.DefaultMappingDataType)
    {
        GpsVendorId = gpsVendorId;
        ResponseField = responseField;
        MappedField = mappedField;
        DataType = string.IsNullOrWhiteSpace(dataType) ? VendorDefaults.DefaultMappingDataType : dataType;
    }

    public VendorMappingDto(int id, Guid gpsVendorId, string responseField, string mappedField, string dataType = VendorDefaults.DefaultMappingDataType)
    {
        Id = id;
        GpsVendorId = gpsVendorId;
        ResponseField = responseField;
        MappedField = mappedField;
        DataType = string.IsNullOrWhiteSpace(dataType) ? VendorDefaults.DefaultMappingDataType : dataType;
    }
}

// dto mapping unit armada toyota (lpcd)
public class VendorLpcdDto
{
    public Guid Id { get; set; }
    public Guid GpsVendorId { get; set; }
    public string Lpcd { get; set; } = string.Empty;

    public VendorLpcdDto() { }

    public VendorLpcdDto(Guid gpsVendorId, string lpcd)
    {
        GpsVendorId = gpsVendorId;
        Lpcd = lpcd;
    }

    public VendorLpcdDto(Guid id, Guid gpsVendorId, string lpcd)
    {
        Id = id;
        GpsVendorId = gpsVendorId;
        Lpcd = lpcd;
    }
}

// dto parameter kueri get vendors
public record GetVendorsDto(
    Guid? Id = null,
    string? VendorName = null,
    string? Lpcd = null,
    int PageIndex = 1,
    int PageSize = 10
);

// dto geofence destination plant toyota tmmin
public class GeofenceDestinationDto
{
    public Guid Id { get; set; }
    public string DestinationCode { get; set; } = string.Empty;
    public string DestinationName { get; set; } = string.Empty;
    public string GeofenceType { get; set; } = VendorDefaults.DefaultGeofenceType;
    public decimal CenterLatitude { get; set; }
    public decimal CenterLongitude { get; set; }
    public int RadiusMeters { get; set; } = VendorDefaults.DefaultGeofenceRadiusMeters;
    public JsonObject? PolygonCoordinates { get; set; }
    public bool IsActive { get; set; } = true;
}
