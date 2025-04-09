using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GPSInterfacing.Data.JsonConverters;

public class JsonObjectValueConverter : ValueConverter<JsonObject?, string?>
{
    public JsonObjectValueConverter()
        : base(
            convertToProviderExpression: v => JsonObjectConverterHelperExtensions.Serialize(v),
            convertFromProviderExpression: v => JsonObjectConverterHelperExtensions.Deserialize(v),
            mappingHints: null)
    { }
}