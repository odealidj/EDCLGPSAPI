namespace Shared.Data.Cache;

public static class VendorCacheConstants
{
    // Kunci cache Redis persisten untuk seluruh konfigurasi vendor aktif
    public const string ActiveVendorsCacheKey = "vendor:configs:active";
}

// DTO representasi vendor aktif yang disimpan di Redis selamanya (persistent cache)
public class ActiveVendorCacheDto
{
    public Guid Id { get; set; }
    public string VendorCode { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public string? Lpcd { get; set; }
    public bool RequiredAuth { get; set; }
    public string? AuthType { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string ProcessingStrategy { get; set; } = "individual"; // individual | combined
    public string? ProcessingStrategyPathKey { get; set; }
    public string ProcessingStrategyPathData { get; set; } = "data";
    public ActiveVendorAuthCacheDto? Auth { get; set; }
    public List<ActiveVendorEndpointCacheDto> Endpoints { get; set; } = [];
    public List<ActiveVendorMappingCacheDto> Mappings { get; set; } = [];
}

public class ActiveVendorEndpointCacheDto
{
    public Guid Id { get; set; }
    public Guid GpsVendorId { get; set; }
    public string EndpointName { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string Method { get; set; } = "GET";
    public string? HeadersJson { get; set; }
    public string? ParamsJson { get; set; }
    public string? BodiesJson { get; set; }
    public string? VarParamsJson { get; set; }
    public string? MaxPath { get; set; }
}

public class ActiveVendorAuthCacheDto
{
    public Guid Id { get; set; }
    public Guid GpsVendorId { get; set; }
    public string BaseUrl { get; set; } = string.Empty;
    public string Method { get; set; } = "POST";
    public string AuthType { get; set; } = "Bearer";
    public string ContentType { get; set; } = "application/json";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? HeadersJson { get; set; }
    public string? ParamsJson { get; set; }
    public string? BodiesJson { get; set; }
    public string? TokenPath { get; set; }
}

public class ActiveVendorMappingCacheDto
{
    public Guid GpsVendorId { get; set; }
    public string ResponseField { get; set; } = string.Empty;
    public string MappedField { get; set; } = string.Empty;
}
