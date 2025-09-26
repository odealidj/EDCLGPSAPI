namespace VendorMaster.Domain.Constants;

// nilai default & konstanta baku vendor master
public static class VendorDefaults
{
    // default string values dari enum
    public const string DefaultProtocolType = "REST_API";
    public const string DefaultProcessingStrategy = "Individual";
    public const string DefaultEndpointType = "TELEMETRY_LAST_POS";
    public const string DefaultAuthType = "BEARER_TOKEN";
    public const string DefaultGeofenceType = "CIRCLE";
    public const string DefaultMappingDataType = "STRING";

    // default konvensi sistem & network
    public const string DefaultTimezone = "Asia/Jakarta";
    public const string DefaultContentType = "application/json";
    public const string DefaultHttpMethod = "GET";
    public const string DefaultAuthHttpMethod = "POST";
    public const int DefaultTokenTtlSeconds = 86400; // 24 jam
    public const int DefaultGeofenceRadiusMeters = 150;
    public const int DefaultVersion = 1;
}
