using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Shared.Data.Mongo.Documents;

public class GpsApiLogDocument
{
    [BsonId]
    public ObjectId Id { get; set; } = ObjectId.GenerateNewId();

    [BsonElement("vendorId")]
    public string VendorId { get; set; } = string.Empty;

    [BsonElement("vendorName")]
    public string VendorName { get; set; } = string.Empty;

    [BsonElement("endpointUrl")]
    public string EndpointUrl { get; set; } = string.Empty;

    [BsonElement("functionName")]
    public string FunctionName { get; set; } = string.Empty;

    [BsonElement("requestedAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    [BsonElement("durationMs")]
    public long DurationMs { get; set; }

    [BsonElement("httpStatusCode")]
    public int? HttpStatusCode { get; set; }

    [BsonElement("success")]
    public bool Success { get; set; }

    [BsonElement("errorMessage")]
    public string? ErrorMessage { get; set; }

    [BsonElement("errorDetail")]
    public BsonValue? ErrorDetail { get; set; }

    [BsonElement("recordCount")]
    public int RecordCount { get; set; }

    [BsonElement("expireAt")]
    [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
    public DateTime ExpireAt { get; set; }
}
