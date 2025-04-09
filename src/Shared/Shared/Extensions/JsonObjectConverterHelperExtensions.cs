using System.Text.Json;
using System.Text.Json.Nodes;

namespace Shared.Extensions;

public static class JsonObjectConverterHelperExtensions
{
    public static string? Serialize(JsonObject? obj)
        => obj == null ? null : obj.ToJsonString(new JsonSerializerOptions());

    public static JsonObject? Deserialize(string? json)
        => string.IsNullOrWhiteSpace(json) ? null : JsonNode.Parse(json)?.AsObject();

}