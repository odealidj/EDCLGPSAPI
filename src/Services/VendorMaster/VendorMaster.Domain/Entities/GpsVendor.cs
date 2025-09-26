namespace VendorMaster.Domain.Entities;

// aggregate root vendor gps telematika (tabel m_gps_vendor)
public class GpsVendor : Aggregate<Guid>
{
    public string VendorCode { get; set; } = string.Empty;
    public string VendorName { get; set; } = string.Empty;
    public string ProtocolType { get; set; } = VendorDefaults.DefaultProtocolType;
    public string Timezone { get; set; } = VendorDefaults.DefaultTimezone;
    public bool IsActive { get; set; } = true;
    public int Version { get; set; } = VendorDefaults.DefaultVersion;
    public string ProcessingStrategy { get; set; } = VendorDefaults.DefaultProcessingStrategy;
    public string? ProcessingStrategyPathData { get; set; }
    public string? ProcessingStrategyPathKey { get; set; }
    public bool IsDeleted { get; set; }

    // relasi ke child endpoints
    private readonly List<GpsVendorEndpoint> _endpoints = [];
    public IReadOnlyList<GpsVendorEndpoint> GpsVendorEndpoints => _endpoints;

    // relasi ke auth config (one-to-one)
    public GpsVendorAuth? GpsVendorAuth { get; set; }

    // relasi ke mapping json vendor -> model edcl
    private readonly List<GpsVendorMapping> _mappings = [];
    public IReadOnlyList<GpsVendorMapping> Mappings => _mappings;

    // relasi ke unit armada toyota (lpcd)
    private readonly List<GpsVendorLpcd> _lpcds = [];
    public IReadOnlyList<GpsVendorLpcd> Lpcds => _lpcds;

    // factory method buat bikin instance baru vendor
    public static GpsVendor Create(
        Guid id,
        string vendorCode,
        string vendorName,
        string? protocolType = VendorDefaults.DefaultProtocolType,
        string? timezone = VendorDefaults.DefaultTimezone,
        bool isActive = true,
        string processingStrategy = VendorDefaults.DefaultProcessingStrategy,
        string? processingStrategyPathData = null,
        string? processingStrategyPathKey = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vendorName);

        // kalo vendor code kosong, generate dari nama vendor
        var code = string.IsNullOrWhiteSpace(vendorCode)
            ? vendorName.Trim().Replace(" ", "_").ToUpperInvariant()
            : vendorCode.Trim().ToUpperInvariant();

        return new GpsVendor
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            VendorCode = code,
            VendorName = vendorName.Trim(),
            ProtocolType = string.IsNullOrWhiteSpace(protocolType) ? VendorDefaults.DefaultProtocolType : protocolType,
            Timezone = string.IsNullOrWhiteSpace(timezone) ? VendorDefaults.DefaultTimezone : timezone,
            IsActive = isActive,
            Version = VendorDefaults.DefaultVersion,
            ProcessingStrategy = string.IsNullOrWhiteSpace(processingStrategy) ? VendorDefaults.DefaultProcessingStrategy : processingStrategy,
            ProcessingStrategyPathData = processingStrategyPathData,
            ProcessingStrategyPathKey = processingStrategyPathKey,
            IsDeleted = false
        };
    }

    // update data pokok vendor
    public void UpdateDetails(
        string vendorCode,
        string vendorName,
        string? protocolType,
        string? timezone,
        bool isActive,
        string processingStrategy,
        string? processingStrategyPathData,
        string? processingStrategyPathKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vendorName);

        if (!string.IsNullOrWhiteSpace(vendorCode))
            VendorCode = vendorCode.Trim().ToUpperInvariant();

        VendorName = vendorName.Trim();
        ProtocolType = string.IsNullOrWhiteSpace(protocolType) ? ProtocolType : protocolType;
        Timezone = string.IsNullOrWhiteSpace(timezone) ? Timezone : timezone;
        IsActive = isActive;
        ProcessingStrategy = string.IsNullOrWhiteSpace(processingStrategy) ? ProcessingStrategy : processingStrategy;
        ProcessingStrategyPathData = processingStrategyPathData;
        ProcessingStrategyPathKey = processingStrategyPathKey;
        Version++;
    }

    // tambah endpoint baru
    public void AddEndpoint(
        Guid id,
        string endpointType,
        string baseUrl,
        string method,
        string? contentType,
        JsonObject? headers,
        JsonObject? @params,
        JsonObject? bodies,
        JsonObject? varParams,
        string? maxPath)
    {
        _endpoints.Add(new GpsVendorEndpoint
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            VendorId = Id,
            EndpointType = string.IsNullOrWhiteSpace(endpointType) ? VendorDefaults.DefaultEndpointType : endpointType,
            BaseUrl = baseUrl,
            Method = string.IsNullOrWhiteSpace(method) ? VendorDefaults.DefaultHttpMethod : method,
            ContentType = contentType ?? VendorDefaults.DefaultContentType,
            Headers = headers,
            Params = @params,
            Bodies = bodies,
            VarParams = varParams,
            MaxPath = maxPath
        });
    }

    // pasang konfigurasi auth vendor
    public void SetAuth(
        Guid id,
        string authType,
        string baseUrl,
        string method,
        string? contentType,
        string? username,
        string? password,
        string? tokenPath,
        string? tokenExpiresInPath,
        int tokenTtlSeconds,
        JsonObject? headers,
        JsonObject? @params,
        JsonObject? bodies)
    {
        GpsVendorAuth = new GpsVendorAuth
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            VendorId = Id,
            AuthType = string.IsNullOrWhiteSpace(authType) ? VendorDefaults.DefaultAuthType : authType,
            BaseUrl = baseUrl,
            Method = string.IsNullOrWhiteSpace(method) ? VendorDefaults.DefaultAuthHttpMethod : method,
            ContentType = contentType ?? VendorDefaults.DefaultContentType,
            Username = username,
            Password = password,
            TokenPath = tokenPath,
            TokenExpiresInPath = tokenExpiresInPath,
            TokenTtlSeconds = tokenTtlSeconds <= 0 ? VendorDefaults.DefaultTokenTtlSeconds : tokenTtlSeconds,
            Headers = headers,
            Params = @params,
            Bodies = bodies
        };
    }

    // tambah pemetaan field json vendor
    public void AddMapping(int id, string responseField, string mappedField, string? dataType = VendorDefaults.DefaultMappingDataType)
    {
        _mappings.Add(new GpsVendorMapping
        {
            Id = id,
            VendorId = Id,
            ResponseField = responseField,
            MappedField = mappedField,
            DataType = string.IsNullOrWhiteSpace(dataType) ? VendorDefaults.DefaultMappingDataType : dataType.ToUpperInvariant()
        });
    }

    // tambah lpcd toyota
    public void AddLpcd(Guid id, string lpcd)
    {
        _lpcds.Add(new GpsVendorLpcd
        {
            Id = id == Guid.Empty ? Guid.NewGuid() : id,
            VendorId = Id,
            Lpcd = lpcd.Trim().ToUpperInvariant()
        });
    }

    // bersihkan child collections saat replace update
    public void ClearEndpoints() => _endpoints.Clear();
    public void ClearMappings() => _mappings.Clear();
    public void ClearLpcds() => _lpcds.Clear();
}
