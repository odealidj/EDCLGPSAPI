using System.Text.Json.Nodes;

namespace Shared.Extensions;

public static class JsonObjectExtensions
{
    public static JsonObject? ToJsonObject(this Dictionary<string, string>? dict)
    {
        if (dict is null) return null;

        var obj = new JsonObject();
        foreach (var (key, value) in dict)
        {
            obj[key] = value;
        }

        return obj;
    }
}