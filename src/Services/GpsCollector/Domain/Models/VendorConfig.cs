namespace GpsCollector.Domain.Models;

// Konfigurasi vendor gps
public sealed class VendorConfig
{
    public Guid Id { get; init; }
    public string VendorName { get; init; } = string.Empty;
    public bool RequiredAuth { get; init; }
    public string? AuthType { get; init; }
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string ProcessingStrategy { get; init; } = "individual"; // individual | combined
    public string? ProcessingStrategyPathKey { get; init; }
    public string ProcessingStrategyPathData { get; init; } = "data";
    public VendorAuthConfig? Auth { get; init; }
    public List<VendorEndpointConfig> Endpoints { get; init; } = [];
    public List<VendorFieldMapping> Mappings { get; init; } = [];
}

// Konfigurasi endpoint HTTP rest vendor
public sealed class VendorEndpointConfig
{
    public Guid Id { get; init; }
    public Guid GpsVendorId { get; init; }
    public string EndpointName { get; init; } = string.Empty;
    public string BaseUrl { get; init; } = string.Empty;
    public string Method { get; init; } = "GET";
    public string? HeadersJson { get; init; }
    public string? ParamsJson { get; init; }
    public string? BodiesJson { get; init; }
    public string? VarParamsJson { get; init; }
    public string? MaxPath { get; init; }
}

// Konfigurasi auth vendor (Bearer / Basic)
public sealed class VendorAuthConfig
{
    public Guid Id { get; init; }
    public Guid GpsVendorId { get; init; }
    public string BaseUrl { get; init; } = string.Empty;
    public string Method { get; init; } = "POST";
    public string AuthType { get; init; } = "Bearer";
    public string ContentType { get; init; } = "application/json";
    public string? Username { get; init; }
    public string? Password { get; init; }
    public string? HeadersJson { get; init; }
    public string? ParamsJson { get; init; }
    public string? BodiesJson { get; init; }
    public string? TokenPath { get; init; }
}

// Mapping field respons vendor ke field standar EDCL
public sealed class VendorFieldMapping
{
    public Guid GpsVendorId { get; init; }
    public string ResponseField { get; init; } = string.Empty;
    public string MappedField { get; init; } = string.Empty;
}
