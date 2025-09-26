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
[JsonSerializable(typeof(Dictionary<string, string>))]
[JsonSerializable(typeof(Dictionary<string, string?>))]
[JsonSerializable(typeof(JsonElement))]
public partial class GpsCollectorJsonContext : JsonSerializerContext
{
}
