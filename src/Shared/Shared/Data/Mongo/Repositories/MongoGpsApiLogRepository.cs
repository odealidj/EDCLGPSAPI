using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Shared.Data.Mongo.Documents;

namespace Shared.Data.Mongo.Repositories;

public class MongoGpsApiLogRepository : IGpsApiLogRepository
{
    private readonly IMongoCollection<GpsApiLogDocument> _collection;
    private readonly MongoDbSettings _settings;

    public MongoGpsApiLogRepository(IMongoDatabase database, IOptions<MongoDbSettings> options)
    {
        _settings = options.Value;
        _collection = database.GetCollection<GpsApiLogDocument>(_settings.ApiLogsCollection);
    }

    public async Task InsertAsync(
        Guid vendorId,
        string vendorName,
        string endpointUrl,
        string functionName,
        long durationMs,
        int? httpStatusCode,
        bool success,
        string? errorMessage,
        string? errorDetailJson,
        int recordCount,
        CancellationToken ct = default)
    {
        BsonValue? errorDetail = null;
        if (!string.IsNullOrWhiteSpace(errorDetailJson))
        {
            try
            {
                errorDetail = BsonDocument.Parse(errorDetailJson);
            }
            catch
            {
                errorDetail = new BsonString(errorDetailJson);
            }
        }

        var doc = new GpsApiLogDocument
        {
            VendorId = vendorId.ToString(),
            VendorName = vendorName,
            EndpointUrl = endpointUrl,
            FunctionName = functionName,
            RequestedAt = DateTime.UtcNow,
            DurationMs = durationMs,
            HttpStatusCode = httpStatusCode,
            Success = success,
            ErrorMessage = errorMessage,
            ErrorDetail = errorDetail,
            RecordCount = recordCount,
            ExpireAt = DateTime.UtcNow.AddDays(_settings.ApiLogTtlDays)
        };

        await _collection.InsertOneAsync(doc, cancellationToken: ct);
    }
}
