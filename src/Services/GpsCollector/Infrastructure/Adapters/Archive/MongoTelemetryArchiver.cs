using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using GpsCollector.Application.Ports;
using Microsoft.Extensions.Logging;
using Shared.Data.Mongo.Repositories;

namespace GpsCollector.Infrastructure.Adapters.Archive;

// Adapter arsip payload JSON mentah ke MongoDB (TTL 30 hari)
// Dilengkapi In-Memory Payload Hash Filter di RAM untuk mencegah penulisan data duplikat jika isi response tidak berubah
public sealed class MongoTelemetryArchiver : ITelemetryArchiver
{
    private readonly IGpsRawResponseRepository? _rawRepo;
    private readonly ILogger<MongoTelemetryArchiver> _logger;
    private static readonly ConcurrentDictionary<string, string> s_lastPayloadHashes = new();

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
            // 1. Hitung Hash MD5 dari payload JSON mentah (< 0.05 ms, 0 query DB)
            var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(rawJson));
            var currentHash = Convert.ToHexString(hashBytes);
            var cacheKey = $"{vendorId:N}:{endpointUrl}";

            // 2. Evaluasi apakah payload identik dengan siklus polling sebelumnya
            if (s_lastPayloadHashes.TryGetValue(cacheKey, out var previousHash) &&
                string.Equals(previousHash, currentHash, StringComparison.Ordinal))
            {
                _logger.LogDebug(
                    "Payload vendor {VendorName} identik dengan polling sebelumnya (Hash: {Hash}). Penulisan ke MongoDB dilewati.",
                    vendorName, currentHash);
                return;
            }

            // 3. Payload baru/berubah: simpan ke MongoDB dan perbarui hash di RAM
            s_lastPayloadHashes[cacheKey] = currentHash;

            await _rawRepo.InsertAsync(
                vendorId: vendorId,
                vendorName: vendorName,
                endpointUrl: endpointUrl,
                rawJson: rawJson,
                recordCount: recordCount,
                batchId: Guid.NewGuid().ToString("N"),
                ct: ct);

            _logger.LogInformation(
                "Payload baru untuk vendor {VendorName} berhasil diarsipkan ke MongoDB (Hash: {Hash})",
                vendorName, currentHash);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to archive raw JSON to MongoDB for vendor {VendorName}", vendorName);
        }
    }
}
