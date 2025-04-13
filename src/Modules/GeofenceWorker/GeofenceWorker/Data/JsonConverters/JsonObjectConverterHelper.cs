using System.Text.Json;
using System.Text.Json.Nodes;

namespace GeofenceWorker.Data.JsonConverters;

public static class JsonObjectConverterHelper
{
    public static string? Serialize(JsonObject? obj)
        => obj == null ? null : obj.ToJsonString(new JsonSerializerOptions());

    public static JsonObject? Deserialize(string? json)
        => string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json)?.AsObject();

}