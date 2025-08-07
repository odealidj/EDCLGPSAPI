using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Shared.Data.Mongo.Documents;

public class GpsRawResponseDocument
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.GenerateNewId();

    [BsonElement("vendorId")]
    public string VendorId { get; set; } = string.Empty;

    [BsonElement("vendorName")]
    public string VendorName { get; set; } = string.Empty;

    [BsonElement("endpointUrl")]
    public string EndpointUrl { get; set; } = string.Empty;

    [BsonElement("batchId")]
    public string? BatchId { get; set; }

    [BsonElement("collectedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime CollectedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("recordCount")]
    public int RecordCount { get; set; }

    [BsonElement("rawResponse")]
    public BsonValue RawResponse { get; set; } = BsonNull.Value;

    [BsonElement("expireAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpireAt { get; set; }
}
