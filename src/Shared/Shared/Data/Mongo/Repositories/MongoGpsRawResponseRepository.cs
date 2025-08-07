using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Shared.Data.Mongo.Documents;

namespace Shared.Data.Mongo.Repositories;

public class MongoGpsRawResponseRepository : IGpsRawResponseRepository
{
    private readonly IMongoCollection<GpsRawResponseDocument> _collection;
    private readonly MongoDbSettings _settings;

    public MongoGpsRawResponseRepository(IMongoDatabase database, IOptions<MongoDbSettings> options)
    {
        _settings = options.Value;
        _collection = database.GetCollection<GpsRawResponseDocument>(_settings.RawResponsesCollection);
    }

    public async Task InsertAsync(
        Guid vendorId,
        string vendorName,
        string endpointUrl,
        string rawJson,
        int recordCount,
        string? batchId = null,
        CancellationToken ct = default)
    {
        BsonValue rawDoc;
        try
        {
            rawDoc = BsonDocument.Parse(rawJson);
        }
        catch
        {
            rawDoc = new BsonDocument("raw_text", rawJson ?? string.Empty);
        }

        var doc = new GpsRawResponseDocument
        {
            VendorId = vendorId.ToString(),
            VendorName = vendorName,
            EndpointUrl = endpointUrl,
            BatchId = batchId,
            CollectedAt = DateTime.UtcNow,
            RecordCount = recordCount,
            RawResponse = rawDoc,
            ExpireAt = DateTime.UtcNow.AddDays(_settings.RawResponseTtlDays)
        };

        await _collection.InsertOneAsync(doc, cancellationToken: ct);
    }
}
