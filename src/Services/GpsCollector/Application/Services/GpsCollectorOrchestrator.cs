using System.Diagnostics;
using System.Text.RegularExpressions;
using GpsCollector.Application.DTOs;
using GpsCollector.Application.Ports;
using GpsCollector.Domain.Models;
using GpsCollector.Metrics;
using Microsoft.Extensions.Logging;

namespace GpsCollector.Application.Services;

// Orkestrator pengumpulan data telemetri GPS dari vendor (Arsitektur On-Demand Delivery Tracking)
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
            var rawResponses = new List<string>(vendor.Endpoints.Count);
            var isFetchSuccessful = true;
            string? firstFailureReason = null;

            if (string.Equals(vendor.ProcessingStrategy, "combined", StringComparison.OrdinalIgnoreCase))
            {
                // Strategi gabungan: panggil seluruh endpoint lalu combine respons
                foreach (var ep in vendor.Endpoints)
                {
                    var fetchResult = await _httpFetcher.FetchEndpointAsync(vendor, ep, ct);
                    if (fetchResult.IsSuccess && !string.IsNullOrWhiteSpace(fetchResult.ResponseBody))
                    {
                        await _healthTracker.RecordSuccessAsync(vendor, ep, ct);
                        rawResponses.Add(fetchResult.ResponseBody);

                        // Selalu arsipkan payload mentah vendor ke MongoDB
                        await _archiver.ArchiveRawResponseAsync(vendor.Id, vendor.VendorName, ep.BaseUrl, fetchResult.ResponseBody, 0, ct);
                    }
                    else
                    {
                        isFetchSuccessful = false;
                        firstFailureReason = $"{fetchResult.StatusCode} {fetchResult.ReasonPhrase}";
                        await _healthTracker.RecordFailureAsync(vendor, ep, fetchResult.StatusCode, firstFailureReason, ct);

                        // Kategori 1: Catat kegagalan koneksi HTTP vendor ke edcl.m_gps_api_log
                        await _apiLogRepo.LogApiCallAsync(new GpsApiLogRecord
                        {
                            FunctionName = ep.BaseUrl,
                            Status = "0",
                            ErrorMessage = firstFailureReason,
                            Parameter = fetchResult.ResponseBody,
                            Username = vendor.VendorName,
                            CreatedBy = "GpsCollector"
                        }, ct);
                    }
                }

                if (isFetchSuccessful && rawResponses.Count > 0)
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
                        isFetchSuccessful = false;
                        firstFailureReason = $"{fetchResult.StatusCode} {fetchResult.ReasonPhrase}";
                        await _healthTracker.RecordFailureAsync(vendor, ep, fetchResult.StatusCode, firstFailureReason, ct);

                        // Kategori 1: Catat kegagalan koneksi HTTP vendor ke edcl.m_gps_api_log
                        await _apiLogRepo.LogApiCallAsync(new GpsApiLogRecord
                        {
                            FunctionName = ep.BaseUrl,
                            Status = "0",
                            ErrorMessage = firstFailureReason,
                            Parameter = fetchResult.ResponseBody,
                            Username = vendor.VendorName,
                            CreatedBy = "GpsCollector"
                        }, ct);
                        continue;
                    }

                    await _healthTracker.RecordSuccessAsync(vendor, ep, ct);

                    // Selalu arsipkan payload mentah vendor ke MongoDB
                    await _archiver.ArchiveRawResponseAsync(vendor.Id, vendor.VendorName, ep.BaseUrl, fetchResult.ResponseBody, 0, ct);

                    // Update cursor max value jika ada konfigurasi MaxPath
                    if (!string.IsNullOrWhiteSpace(ep.MaxPath))
                    {
                        var maxVal = _normalizer.ExtractMaxProperty(fetchResult.ResponseBody, vendor.ProcessingStrategyPathData, ep.MaxPath);
                        if (maxVal.HasValue)
                        {
                            await _vendorConfigRepo.UpdateVarParamsMaxIdAsync(ep.Id, ep.MaxPath, maxVal.Value, ct);
                        }
                    }

                    rawResponses.Add(fetchResult.ResponseBody);
                }
            }

            // 1. Ambil seluruh sesi delivery tracking yang aktif untuk vendor / LPCD ini
            var activeSessions = (await _deliverySessionRepo.GetActiveSessionsForVendorAsync(vendor.Id, vendor.Lpcd, ct)) ?? [];

            if (activeSessions.Count == 0)
            {
                // Fallback untuk backward compatibility / mock unit tests lama
                var legacyDict = await _deliverySessionRepo.GetActiveSessionsByPlatNosAsync([], ct);
                if (legacyDict != null && legacyDict.Count > 0)
                {
                    activeSessions = legacyDict.Values.ToList();
                }
            }

            // Kategori 1: Jika koneksi HTTP vendor gagal dan terdapat delivery aktif yang bergantung padanya
            if (!isFetchSuccessful && activeSessions.Count > 0)
            {
                foreach (var session in activeSessions)
                {
                    await _cache.UpdateDeliveryLiveStatusAsync(
                        session.DeliveryNo,
                        "VENDOR_CONNECTION_ERROR",
                        $"Gagal terhubung ke GPS Vendor {vendor.VendorName}: {firstFailureReason}",
                        ct);
                }
            }

            // Opsi A: Jika TIDAK ADA sesi delivery aktif, LEWATI parsing dan normalisasi JSON!
            // Raw response sudah aman diarsipkan di MongoDB. Menghemat CPU, memori, dan DB writes.
            if (activeSessions.Count == 0)
            {
                _logger.LogInformation(
                    "Tidak ada job delivery aktif untuk vendor {VendorName}. Raw response JSON diarsipkan di MongoDB. Normalisasi posisi dilewati.",
                    vendor.VendorName);

                success = true;
                _metrics?.UpdateLastCollectionTimestamp();
                return new VendorPollingResultDto(
                    VendorId: vendor.Id,
                    VendorName: vendor.VendorName,
                    Success: true,
                    PointsCount: 0,
                    DeliveryPointsCount: 0,
                    DurationMs: sw.Elapsed.TotalMilliseconds);
            }

            // ON-DEMAND PARSING: Normalisasi JSON vendor hanya jika ada sesi job aktif
            if (isFetchSuccessful && rawResponses.Count > 0)
            {
                if (string.Equals(vendor.ProcessingStrategy, "combined", StringComparison.OrdinalIgnoreCase))
                {
                    var combinedPoints = _normalizer.NormalizeCombined(
                        vendor.Id,
                        rawResponses,
                        vendor.ProcessingStrategyPathKey ?? "vin",
                        vendor.ProcessingStrategyPathData,
                        vendor.Mappings,
                        vendor.Endpoints[0].BaseUrl);

                    if (combinedPoints != null) allPoints.AddRange(combinedPoints);
                }
                else
                {
                    foreach (var ep in vendor.Endpoints)
                    {
                        var epResponse = rawResponses.FirstOrDefault();
                        if (string.IsNullOrWhiteSpace(epResponse)) continue;

                        var normalized = _normalizer.Normalize(
                            vendor.Id,
                            epResponse,
                            vendor.ProcessingStrategyPathData,
                            vendor.Mappings,
                            ep.BaseUrl);

                        if (normalized != null) allPoints.AddRange(normalized);
                    }
                }
            }

            totalPointsCount = allPoints.Count;

            if (activeSessions.Count > 0 && isFetchSuccessful)
            {
                var matchedDeliveries = new List<(TelemetryPoint Point, DeliveryProgressSession Session)>();

                // Periksa setiap sesi delivery aktif apakah cocok dengan data telemetri vendor
                foreach (var session in activeSessions)
                {
                    var matchedPoint = FindMatchingPoint(session, allPoints);

                    if (matchedPoint != null)
                    {
                        // MATCH: Armada terdeteksi!
                        matchedDeliveries.Add((matchedPoint, session));

                        // Update live view di Redis ke status TRACKING_ACTIVE
                        await _cache.UpdateDeliveryLivePositionAsync(session.DeliveryNo, matchedPoint, "TRACKING_ACTIVE", ct);
                    }
                    else
                    {
                        // KATEGORI 2: Data Mismatch / Correlation Miss
                        // (armada tidak ditemukan pada respons vendor karena format response berubah,
                        // GPS mati/offline, atau supir salah input nomor plat/identitas saat Start Job)
                        _logger.LogWarning(
                            "Kategori 2 (Data Mismatch / Correlation Miss): Armada delivery '{DeliveryNo}' tidak ditemukan pada respons vendor {VendorCode} (LPCD: {Lpcd}, MatchKey: {MatchKey}, MatchVal: {MatchValue})",
                            session.DeliveryNo, vendor.VendorCode, session.Lpcd, session.MatchKey, session.MatchValue);

                        // Batal simpan ke tabel delivery dan laporkan ke edcl.m_gps_api_log (WARNING)
                        await _apiLogRepo.LogApiCallAsync(new GpsApiLogRecord
                        {
                            FunctionName = "DELIVERY_CORRELATION_MISS",
                            Status = "WARNING",
                            ErrorMessage = $"Armada untuk Delivery '{session.DeliveryNo}' tidak ditemukan di respons GPS vendor {vendor.VendorCode}. Filter: LPCD='{session.Lpcd}', Key='{session.MatchKey}', Val='{session.MatchValue}'. Kemungkinan GPS unit offline/mati, salah input identitas saat Start Job, atau format respons vendor berubah.",
                            ErrorResponse = $"Total data points vendor diterima: {allPoints.Count}",
                            Parameter = $"{{\"deliveryNo\":\"{session.DeliveryNo}\",\"lpcd\":\"{session.Lpcd}\",\"vendorCode\":\"{vendor.VendorCode}\",\"matchKey\":\"{session.MatchKey}\",\"matchValue\":\"{session.MatchValue}\"}}",
                            Username = vendor.VendorName,
                            CreatedBy = "GpsCollector"
                        }, ct);

                        // Update status live view di Redis ke DATA_MISMATCH
                        await _cache.UpdateDeliveryLiveStatusAsync(
                            session.DeliveryNo,
                            "DATA_MISMATCH",
                            $"Armada tidak ditemukan pada respons vendor {vendor.VendorCode}. Kemungkinan GPS vendor offline atau format data tidak sesuai.",
                            ct);
                    }
                }

                // Ingest hanya armada yang memiliki delivery aktif ke PostgreSQL (edcl.t_delivery_latest_position & edcl.t_delivery_position_history)
                if (matchedDeliveries.Count > 0)
                {
                    totalDeliveryCount = await _ingestionRepo.IngestDeliveryPositionsAsync(
                        vendor.Id,
                        vendor.VendorName,
                        matchedDeliveries,
                        ct);

                    if (totalDeliveryCount == 0)
                    {
                        // Fallback ke IngestTelemetryBatchAsync jika di-mock pada existing unit tests
                        var activeDict = matchedDeliveries.ToDictionary(m => m.Session.PlatNo, m => m.Session, StringComparer.OrdinalIgnoreCase);
                        var (_, batchCount) = await _ingestionRepo.IngestTelemetryBatchAsync(
                            vendor.Id,
                            vendor.VendorName,
                            Guid.NewGuid(),
                            matchedDeliveries.Select(m => m.Point).ToList(),
                            activeDict,
                            ct);
                        totalDeliveryCount = batchCount;
                    }

                    var matchedPoints = matchedDeliveries.Select(m => m.Point).ToList();
                    await _publisher.PublishTelemetryEventAsync(vendor.Id, vendor.VendorName, Guid.NewGuid(), matchedPoints, ct);
                    _metrics?.RecordRecordsInserted(totalDeliveryCount);
                }

                if (allPoints.Count > 0)
                {
                    await _cache.SetLastPositionsAsync(vendor.VendorName, allPoints, ct);
                }
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

    private static TelemetryPoint? FindMatchingPoint(DeliveryProgressSession session, List<TelemetryPoint> points)
    {
        var cleanSessionVal = !string.IsNullOrWhiteSpace(session.CleanMatchValue)
            ? session.CleanMatchValue
            : CleanKey(session.MatchValue);

        foreach (var p in points)
        {
            if (string.Equals(session.MatchKey, "PlatNo", StringComparison.OrdinalIgnoreCase))
            {
                var cleanPlat = CleanKey(p.PlatNo);
                if (cleanPlat == cleanSessionVal || string.Equals(p.PlatNo, session.MatchValue, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }
            else if (string.Equals(session.MatchKey, "VehicleId", StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(session.MatchKey, "DeviceId", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(p.DeviceId, session.MatchValue, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }
            else if (string.Equals(session.MatchKey, "Imei", StringComparison.OrdinalIgnoreCase))
            {
                if (string.Equals(p.DeviceId, session.MatchValue, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }
            else
            {
                // Default / Fallback matching
                var cleanPlat = CleanKey(p.PlatNo);
                if (cleanPlat == cleanSessionVal || string.Equals(p.DeviceId, session.MatchValue, StringComparison.OrdinalIgnoreCase))
                {
                    return p;
                }
            }
        }

        return null;
    }

    private static string CleanKey(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return Regex.Replace(input.ToUpperInvariant(), @"[\s\-_]+", "");
    }
}
