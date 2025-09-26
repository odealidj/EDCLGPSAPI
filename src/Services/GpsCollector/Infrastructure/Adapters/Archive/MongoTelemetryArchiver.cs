using GpsCollector.Application.Ports;
using Microsoft.Extensions.Logging;
using Shared.Data.Mongo.Repositories;

namespace GpsCollector.Infrastructure.Adapters.Archive;

// Adapter arsip payload JSON mentah ke MongoDB (TTL 30 hari)
public sealed class MongoTelemetryArchiver : ITelemetryArchiver
{
    private readonly IGpsRawResponseRepository? _rawRepo;
    private readonly ILogger<MongoTelemetryArchiver> _logger;

    public MongoTelemetryArchiver(ILogger<MongoTelemetryArchiver> logger, IGpsRawResponseRepository? rawRepo = null)
    {
        _logger = logger;
        _rawRepo = rawRepo;
    }

    public async Task ArchiveRawResponseAsync(
        Guid vendorId,
        string vendorName,
        string endpointUrl,
        string rawJson,
        int recordCount,
        CancellationToken ct)
    {
        if (_rawRepo == null || string.IsNullOrWhiteSpace(rawJson)) return;

        try
        {
            await _rawRepo.InsertAsync(
                vendorId: vendorId,
                vendorName: vendorName,
                endpointUrl: endpointUrl,
                rawJson: rawJson,
                recordCount: recordCount,
                batchId: Guid.NewGuid().ToString("N"),
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to archive raw JSON to MongoDB for vendor {VendorName}", vendorName);
        }
    }
}
