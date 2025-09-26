using System.Text.Json;
using System.Text.Json.Serialization;

namespace GpsCollector.Application.DTOs;

// Context Source Generator System.Text.Json (100% Native AOT & Trimming Compatible)
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(GpsTelemetryPointDto))]
[JsonSerializable(typeof(List<GpsTelemetryPointDto>))]
[JsonSerializable(typeof(GpsDeliveryMessageDto))]
[JsonSerializable(typeof(GpsCachePayloadDto))]
[JsonSerializable(typeof(CollectorStatusDto))]
[JsonSerializable(typeof(VendorPollingResultDto))]
[JsonSerializable(typeof(List<VendorPollingResultDto>))]
[JsonSerializable(typeof(VendorAlertMessageDto))]
[JsonSerializable(typeof(DeliveryLivePositionCacheDto))]
[JsonSerializable(typeof(DeliveryLiveStatusCacheDto))]
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, string?>))]
[JsonSerializable(typeof(JsonElement))]
[JsonSerializable(typeof(Shared.Data.Mongo.DTOs.RawResponseItemDto))]
[JsonSerializable(typeof(List<Shared.Data.Mongo.DTOs.RawResponseItemDto>))]
[JsonSerializable(typeof(Shared.Pagination.PaginatedResult<Shared.Data.Mongo.DTOs.RawResponseItemDto>))]
[JsonSerializable(typeof(Shared.Data.Cache.ActiveVendorCacheDto))]
[JsonSerializable(typeof(List<Shared.Data.Cache.ActiveVendorCacheDto>))]
[JsonSerializable(typeof(Shared.Data.Cache.ActiveVendorEndpointCacheDto))]
[JsonSerializable(typeof(Shared.Data.Cache.ActiveVendorAuthCacheDto))]
[JsonSerializable(typeof(Shared.Data.Cache.ActiveVendorMappingCacheDto))]
public partial class GpsCollectorJsonContext : JsonSerializerContext
{
}
