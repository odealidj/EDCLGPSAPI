using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace VendorMaster.Infrastructure.Converters;

// converter buat mapping JsonObject .NET ke string jsonb di postgres
public class JsonObjectValueConverter : ValueConverter<JsonObject?, string>
{
    public JsonObjectValueConverter()
        : base(
            v => JsonObjectConverterHelper.Serialize(v),
            v => JsonObjectConverterHelper.Deserialize(v))
    { }
}

public static class JsonObjectConverterHelper
{
    // serialize jsonobject jadi string
    public static string Serialize(JsonObject? jsonObject)
    {
        return jsonObject != null ? jsonObject.ToJsonString() : "{}";
    }

    // deserialize string json jadi jsonobject
    public static JsonObject? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonNode.Parse(json) as JsonObject;
        }
        catch
        {
            return null;
        }
    }
}
