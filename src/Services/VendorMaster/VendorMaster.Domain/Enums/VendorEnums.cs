namespace VendorMaster.Domain.Enums;

// protokol komunikasi vendor gps telematika
public enum ProtocolType
{
    REST_API,
    MQTT,
    SOAP
}

// strategi penarikan & pemrosesan data armada vendor
public enum ProcessingStrategy
{
    Individual,
    Combined
}

// tipe endpoint api vendor
public enum EndpointType
{
    TELEMETRY_LAST_POS,
    TELEMETRY_HISTORY,
    AUTH
}

// jenis autentikasi api vendor
public enum AuthType
{
    BEARER_TOKEN,
    BASIC,
    API_KEY,
    NoAuth
}

// tipe bentuk area geofence pabrik
public enum GeofenceType
{
    CIRCLE,
    POLYGON
}

// tipe data nilai field pada response vendor
public enum MappingDataType
{
    STRING,
    NUMBER,
    DATETIME,
    BOOLEAN
}
