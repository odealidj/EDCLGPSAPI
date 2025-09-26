using System.Diagnostics;
using GpsCollector.Application.DTOs;
using GpsCollector.Application.Ports;
using GpsCollector.Domain.Models;
using GpsCollector.Metrics;
using Microsoft.Extensions.Logging;

namespace GpsCollector.Application.Services;

// Orkestrator pengumpulan data telemetri dari vendor pihak ketiga
public sealed class GpsCollectorOrchestrator : IGpsCollectorOrchestrator
{
    private readonly IVendorConfigRepository _vendorConfigRepo;
    private readonly IDeliverySessionRepository _deliverySessionRepo;
    private readonly IVendorHttpFetcher _httpFetcher;
    private readonly ITelemetryNormalizer _normalizer;
    private readonly ITelemetryIngestionRepository _ingestionRepo;
    private readonly ITelemetryCache _cache;
    private readonly ITelemetryArchiver _archiver;
    private readonly ITelemetryEventPublisher _publisher;
    private readonly IGpsApiLogRepository _apiLogRepo;
    private readonly IVendorHealthTracker _healthTracker;
    private readonly ICollectorMetrics? _metrics;
    private readonly ILogger<GpsCollectorOrchestrator> _logger;

    public GpsCollectorOrchestrator(
        IVendorConfigRepository vendorConfigRepo,
        IDeliverySessionRepository deliverySessionRepo,
        IVendorHttpFetcher httpFetcher,
        ITelemetryNormalizer normalizer,
        ITelemetryIngestionRepository ingestionRepo,
        ITelemetryCache cache,
        ITelemetryArchiver archiver,
        ITelemetryEventPublisher publisher,
        IGpsApiLogRepository apiLogRepo,
        IVendorHealthTracker healthTracker,
        ILogger<GpsCollectorOrchestrator> logger,
        ICollectorMetrics? metrics = null)
    {
        _vendorConfigRepo = vendorConfigRepo;
        _deliverySessionRepo = deliverySessionRepo;
        _httpFetcher = httpFetcher;
        _normalizer = normalizer;
        _ingestionRepo = ingestionRepo;
        _cache = cache;
        _archiver = archiver;
        _publisher = publisher;
        _apiLogRepo = apiLogRepo;
        _healthTracker = healthTracker;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task<List<VendorPollingResultDto>> CollectAllVendorsAsync(CancellationToken ct)
    {
        var vendors = await _vendorConfigRepo.GetActiveVendorsAsync(ct);
        var results = new List<VendorPollingResultDto>(vendors.Count);

        foreach (var vendor in vendors)
        {
            if (ct.IsCancellationRequested) break;

            var result = await CollectVendorInternalAsync(vendor, ct);
            results.Add(result);
        }

        return results;
    }

    public async Task<VendorPollingResultDto> CollectVendorAsync(Guid vendorId, CancellationToken ct)
    {
        var vendor = await _vendorConfigRepo.GetVendorByIdAsync(vendorId, ct);
        if (vendor == null)
        {
            return new VendorPollingResultDto(
                VendorId: vendorId,
                VendorName: "Unknown",
                Success: false,
                PointsCount: 0,
                DeliveryPointsCount: 0,
                DurationMs: 0,
                ErrorMessage: $"Vendor with ID {vendorId} not found");
        }

        return await CollectVendorInternalAsync(vendor, ct);
    }

    private async Task<VendorPollingResultDto> CollectVendorInternalAsync(VendorConfig vendor, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        var success = false;
        var totalPointsCount = 0;
        var totalDeliveryCount = 0;
        string? errorMessage = null;

        try
        {
            if (vendor.Endpoints.Count == 0)
            {
                _logger.LogWarning("Vendor {VendorName} has no endpoints configured.", vendor.VendorName);
                return new VendorPollingResultDto(vendor.Id, vendor.VendorName, true, 0, 0, 0);
            }

            var allPoints = new List<TelemetryPoint>();

            if (string.Equals(vendor.ProcessingStrategy, "combined", StringComparison.OrdinalIgnoreCase))
            {
                // Strategi gabungan: panggil seluruh endpoint lalu combine respons
                var rawResponses = new List<string>(vendor.Endpoints.Count);
                foreach (var ep in vendor.Endpoints)
                {
                    var fetchResult = await _httpFetcher.FetchEndpointAsync(vendor, ep, ct);
                    if (fetchResult.IsSuccess && !string.IsNullOrWhiteSpace(fetchResult.ResponseBody))
                    {
                        await _healthTracker.RecordSuccessAsync(vendor, ep, ct);
                        rawResponses.Add(fetchResult.ResponseBody);
                        await _archiver.ArchiveRawResponseAsync(vendor.Id, vendor.VendorName, ep.BaseUrl, fetchResult.ResponseBody, 0, ct);
                    }
                    else
                    {
                        await _healthTracker.RecordFailureAsync(vendor, ep, fetchResult.StatusCode, $"{fetchResult.StatusCode} {fetchResult.ReasonPhrase}", ct);
                        await _apiLogRepo.LogApiCallAsync(new GpsApiLogRecord
                        {
                            FunctionName = ep.BaseUrl,
                            Status = "0",
                            ErrorMessage = $"{fetchResult.StatusCode} {fetchResult.ReasonPhrase}",
                            Parameter = fetchResult.ResponseBody,
                            Username = vendor.VendorName
                        }, ct);
                    }
                }

                if (rawResponses.Count > 0)
                {
                    var combinedPoints = _normalizer.NormalizeCombined(
                        vendor.Id,
                        rawResponses,
                        vendor.ProcessingStrategyPathKey ?? "vin",
                        vendor.ProcessingStrategyPathData,
                        vendor.Mappings,
                        vendor.Endpoints[0].BaseUrl);

                    allPoints.AddRange(combinedPoints);
                }
            }
            else
            {
                // Strategi individual: proses tiap endpoint satu persatu
                foreach (var ep in vendor.Endpoints)
                {
                    var fetchResult = await _httpFetcher.FetchEndpointAsync(vendor, ep, ct);
                    if (!fetchResult.IsSuccess || string.IsNullOrWhiteSpace(fetchResult.ResponseBody))
                    {
                        await _healthTracker.RecordFailureAsync(vendor, ep, fetchResult.StatusCode, $"{fetchResult.StatusCode} {fetchResult.ReasonPhrase}", ct);
                        await _apiLogRepo.LogApiCallAsync(new GpsApiLogRecord
                        {
                            FunctionName = ep.BaseUrl,
                            Status = "0",
                            ErrorMessage = $"{fetchResult.StatusCode} {fetchResult.ReasonPhrase}",
                            Parameter = fetchResult.ResponseBody,
                            Username = vendor.VendorName
                        }, ct);
                        continue;
                    }

                    await _healthTracker.RecordSuccessAsync(vendor, ep, ct);
                    await _archiver.ArchiveRawResponseAsync(vendor.Id, vendor.VendorName, ep.BaseUrl, fetchResult.ResponseBody, 0, ct);

                    var normalized = _normalizer.Normalize(
                        vendor.Id,
                        fetchResult.ResponseBody,
                        vendor.ProcessingStrategyPathData,
                        vendor.Mappings,
                        ep.BaseUrl);

                    // Update cursor max value jika ada konfigurasi MaxPath
                    if (!string.IsNullOrWhiteSpace(ep.MaxPath))
                    {
                        var maxVal = _normalizer.ExtractMaxProperty(fetchResult.ResponseBody, vendor.ProcessingStrategyPathData, ep.MaxPath);
                        if (maxVal.HasValue)
                        {
                            await _vendorConfigRepo.UpdateVarParamsMaxIdAsync(ep.Id, ep.MaxPath, maxVal.Value, ct);
                        }
                    }

                    allPoints.AddRange(normalized);
                }
            }

            totalPointsCount = allPoints.Count;

            if (allPoints.Count > 0)
            {
                var headerId = Guid.NewGuid();
                foreach (var p in allPoints)
                {
                    p.HeaderId = headerId;
                }

                // Ambil daftar plat nomor unik untuk cek sesi delivery aktif
                var distinctPlats = allPoints
                    .Where(p => !string.IsNullOrWhiteSpace(p.PlatNo))
                    .Select(p => p.PlatNo!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                var activeSessions = await _deliverySessionRepo.GetActiveSessionsByPlatNosAsync(distinctPlats, ct);

                // Bulk insert ke PostgreSQL (Binary COPY stream)
                var (rawInserted, deliveryInserted) = await _ingestionRepo.IngestTelemetryBatchAsync(
                    vendor.Id,
                    vendor.VendorName,
                    headerId,
                    allPoints,
                    activeSessions,
                    ct);

                totalDeliveryCount = deliveryInserted;

                // Update snapshot cache di Redis (gps:last:{platNo})
                await _cache.SetLastPositionsAsync(vendor.VendorName, allPoints, ct);

                // Publish event ke RabbitMQ topic
                await _publisher.PublishTelemetryEventAsync(vendor.Id, vendor.VendorName, headerId, allPoints, ct);

                _metrics?.RecordRecordsInserted(rawInserted);
            }

            success = true;
            _metrics?.UpdateLastCollectionTimestamp();
        }
        catch (Exception ex)
        {
            errorMessage = ex.Message;
            _logger.LogError(ex, "Failed to collect telemetry from vendor {VendorName}: {ErrorMessage}", vendor.VendorName, ex.Message);
        }
        finally
        {
            sw.Stop();
            _metrics?.RecordVendorPoll(vendor.VendorName, success, sw.Elapsed.TotalMilliseconds);
        }

        return new VendorPollingResultDto(
            VendorId: vendor.Id,
            VendorName: vendor.VendorName,
            Success: success,
            PointsCount: totalPointsCount,
            DeliveryPointsCount: totalDeliveryCount,
            DurationMs: sw.Elapsed.TotalMilliseconds,
            ErrorMessage: errorMessage);
    }
}
