using System.Text.Json;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using Shared.Data.Mongo.Documents;
using Shared.Data.Mongo.DTOs;
using Shared.Pagination;

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

    public async Task<PaginatedResult<RawResponseItemDto>> QueryRawResponsesAsync(
        RawResponseFilterRequest filter,
        CancellationToken ct = default)
    {
        var builder = Builders<GpsRawResponseDocument>.Filter;
        var filterDef = builder.Empty;

        if (!string.IsNullOrWhiteSpace(filter.VendorId))
        {
            filterDef &= builder.Eq(x => x.VendorId, filter.VendorId.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filter.VendorName))
        {
            filterDef &= builder.Regex(x => x.VendorName, new BsonRegularExpression(filter.VendorName.Trim(), "i"));
        }

        if (filter.FromDate.HasValue)
        {
            filterDef &= builder.Gte(x => x.CollectedAt, filter.FromDate.Value.ToUniversalTime());
        }

        if (filter.ToDate.HasValue)
        {
            filterDef &= builder.Lte(x => x.CollectedAt, filter.ToDate.Value.ToUniversalTime());
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var searchRegex = new BsonRegularExpression(filter.Search.Trim(), "i");
            filterDef &= (builder.Regex(x => x.EndpointUrl, searchRegex) |
                          builder.Regex(x => x.VendorName, searchRegex) |
                          builder.Regex("batchId", searchRegex));
        }

        var pageIndex = filter.PageIndex < 1 ? 1 : filter.PageIndex;
        var pageSize = filter.PageSize <= 0 ? 20 : (filter.PageSize > 100 ? 100 : filter.PageSize);
        var skip = (pageIndex - 1) * pageSize;

        var totalCount = await _collection.CountDocumentsAsync(filterDef, cancellationToken: ct);

        var docs = await _collection.Find(filterDef)
            .SortByDescending(x => x.CollectedAt)
            .Skip(skip)
            .Limit(pageSize)
            .ToListAsync(ct);

        var items = docs.Select(MapToDto).ToList();

        return new PaginatedResult<RawResponseItemDto>(pageIndex, pageSize, totalCount, items);
    }

    public async Task<RawResponseItemDto?> GetRawResponseByIdAsync(
        string id,
        CancellationToken ct = default)
    {
        if (!ObjectId.TryParse(id, out var objectId)) return null;

        var doc = await _collection.Find(x => x.Id == objectId).FirstOrDefaultAsync(ct);
        return doc != null ? MapToDto(doc) : null;
    }

    private static RawResponseItemDto MapToDto(GpsRawResponseDocument doc)
    {
        object? parsedResponse = null;
        if (doc.RawResponse != null && !doc.RawResponse.IsBsonNull)
        {
            try
            {
                var jsonStr = doc.RawResponse.ToJson();
                parsedResponse = JsonSerializer.Deserialize<JsonElement>(jsonStr);
            }
            catch
            {
                parsedResponse = doc.RawResponse.ToString();
            }
        }

        return new RawResponseItemDto
        {
            Id = doc.Id.ToString(),
            VendorId = doc.VendorId,
            VendorName = doc.VendorName,
            EndpointUrl = doc.EndpointUrl,
            BatchId = doc.BatchId,
            CollectedAt = doc.CollectedAt,
            RecordCount = doc.RecordCount,
            RawResponse = parsedResponse,
            ExpireAt = doc.ExpireAt
        };
    }
}
