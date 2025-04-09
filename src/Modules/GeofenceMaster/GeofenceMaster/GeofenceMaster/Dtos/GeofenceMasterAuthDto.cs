namespace GeofenceMaster.GeofenceMaster.Dtos;

public record GeofenceMasterAuthDto(
    Guid Id,
    Guid GpsVendorId,
    string BaseUrl,
    string Method,
    string Authtype,
    JsonObject? Headers,
    JsonObject? Params,
    JsonObject? Bodies
);