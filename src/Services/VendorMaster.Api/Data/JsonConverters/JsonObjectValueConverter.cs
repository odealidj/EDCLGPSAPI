using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GeofenceMaster.Data.JsonConverters;

public class JsonObjectValueConverter : ValueConverter<JsonObject?, string?>
{
    public JsonObjectValueConverter()
        : base(
            convertToProviderExpression: v => JsonObjectConverterHelper.Serialize(v),
            convertFromProviderExpression: v => JsonObjectConverterHelper.Deserialize(v),
            mappingHints: null)
    { }
}