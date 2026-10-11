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
    private readonly Microsoft.Extensions.Hosting.IHostEnvironment? _env;
    private readonly ILogger<GpsCollectorOrchestrator> _logger;

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Major Code Smell", "S107:Methods should not have too many parameters", Justification = "Orchestrator constructor requires multiple domain ports injected via DI container")]
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
        ICollectorMetrics? metrics = null,
        Microsoft.Extensions.Hosting.IHostEnvironment? env = null)
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
        _env = env;
    }

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, DateTime> s_nextAllowedVendorCallTimes = new();

    public async Task<List<VendorPollingResultDto>> CollectAllVendorsAsync(CancellationToken ct)
    {
        var vendors = await _vendorConfigRepo.GetActiveVendorsAsync(ct);
        var results = new List<VendorPollingResultDto>(vendors.Count);

        foreach (var vendor in vendors)
        {
            if (ct.IsCancellationRequested) break;

            var result = await CollectVendorInternalAsync(vendor, ct, force: false);
            results.Add(result);

            // Jeda acak antar vendor (300ms - 800ms) untuk mencegah burst request bersamaan ke network/WAF
            var interVendorDelay = Random.Shared.Next(300, 801);
            await Task.Delay(interVendorDelay, ct);
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

        // Manual trigger dari REST API / test mengaktifkan force: true
        return await CollectVendorInternalAsync(vendor, ct, force: true);
    }

    private async Task<VendorPollingResultDto> CollectVendorInternalAsync(VendorConfig vendor, CancellationToken ct, bool force = false)
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

            var activeSessions = await GetActiveSessionsAsync(vendor, ct);

            if (ShouldThrottleVendor(vendor, activeSessions, force, sw, out var earlyReturn))
            {
                return earlyReturn!;
            }

            ScheduleNextVendorCallWindow(vendor);

            var (rawResponses, isFetchSuccessful, firstFailureReason) = await FetchVendorEndpointsAsync(vendor, ct);

            if (!isFetchSuccessful && activeSessions.Count > 0)
            {
                await NotifyConnectionErrorAsync(vendor, activeSessions, firstFailureReason, ct);
            }

            if (activeSessions.Count == 0)
            {
                _logger.LogInformation(
                    "Tidak ada job delivery aktif untuk vendor {VendorName}. Raw response JSON diarsipkan di MongoDB. Normalisasi posisi dilewati.",
                    vendor.VendorName);

                success = true;
                _metrics?.UpdateLastCollectionTimestamp();
                return new VendorPollingResultDto(vendor.Id, vendor.VendorName, true, 0, 0, sw.ElapsedMilliseconds);
            }

            var allPoints = NormalizeRawResponses(vendor, rawResponses, isFetchSuccessful);
            totalPointsCount = allPoints.Count;

            if (isFetchSuccessful)
            {
                totalDeliveryCount = await ProcessMatchedDeliveriesAsync(vendor, allPoints, activeSessions, ct);
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

    private async Task<List<DeliveryProgressSession>> GetActiveSessionsAsync(VendorConfig vendor, CancellationToken ct)
    {
        var activeSessions = (await _deliverySessionRepo.GetActiveSessionsForVendorAsync(vendor.Id, vendor.Lpcd, ct)) ?? [];
        if (activeSessions.Count == 0)
        {
            var legacyDict = await _deliverySessionRepo.GetActiveSessionsByPlatNosAsync([], ct);
            if (legacyDict != null && legacyDict.Count > 0)
            {
                activeSessions = legacyDict.Values.ToList();
            }
        }
        return activeSessions;
    }

    private bool ShouldThrottleVendor(
        VendorConfig vendor,
        List<DeliveryProgressSession> activeSessions,
        bool force,
        Stopwatch sw,
        out VendorPollingResultDto? earlyReturn)
    {
        earlyReturn = null;
        if (!force && activeSessions.Count == 0)
        {
            _logger.LogInformation("No active delivery tracking jobs for vendor {VendorName} (LPCD: {Lpcd}). Skipping polling to protect against rate limits and bot blacklisting.",
                vendor.VendorName, vendor.Lpcd);

            earlyReturn = new VendorPollingResultDto(vendor.Id, vendor.VendorName, true, 0, 0, sw.ElapsedMilliseconds);
            return true;
        }

        if (!force && s_nextAllowedVendorCallTimes.TryGetValue(vendor.Id, out var nextAllowedTime) && DateTime.UtcNow < nextAllowedTime)
        {
            var remaining = (nextAllowedTime - DateTime.UtcNow).TotalSeconds;
            _logger.LogInformation("Throttling protection: Vendor {VendorName} is in cooldown (remaining {Remaining:N1}s). Skipping to prevent bot detection / IP blacklisting.",
                vendor.VendorName, remaining);

            earlyReturn = new VendorPollingResultDto(vendor.Id, vendor.VendorName, true, 0, 0, sw.ElapsedMilliseconds);
            return true;
        }

        return false;
    }

    private void ScheduleNextVendorCallWindow(VendorConfig vendor)
    {
        int cooldownSec;
        if (_env != null && !_env.IsDevelopment())
        {
            cooldownSec = 60 + Random.Shared.Next(-1, 3);
        }
        else
        {
            var minSec = string.Equals(vendor.VendorCode, "INOVATRACK", StringComparison.OrdinalIgnoreCase) ? 38 : 35;
            cooldownSec = Random.Shared.Next(minSec, 76);
        }

        s_nextAllowedVendorCallTimes[vendor.Id] = DateTime.UtcNow.AddSeconds(cooldownSec);
        _logger.LogInformation("Scheduled next call window for vendor {VendorName} in {Sec}s (Anti-Bot Throttling).", vendor.VendorName, cooldownSec);
    }

    private async Task<(List<string> RawResponses, bool IsSuccess, string? FailureReason)> FetchVendorEndpointsAsync(
        VendorConfig vendor,
        CancellationToken ct)
    {
        var rawResponses = new List<string>(vendor.Endpoints.Count);
        var isFetchSuccessful = true;
        string? firstFailureReason = null;

        foreach (var ep in vendor.Endpoints)
        {
            var fetchResult = await _httpFetcher.FetchEndpointAsync(vendor, ep, ct);
            if (!fetchResult.IsSuccess || string.IsNullOrWhiteSpace(fetchResult.ResponseBody))
            {
                isFetchSuccessful = false;
                firstFailureReason = $"{fetchResult.StatusCode} {fetchResult.ReasonPhrase}";
                await HandleEndpointFailureAsync(vendor, ep, fetchResult, firstFailureReason, ct);
                continue;
            }

            await _healthTracker.RecordSuccessAsync(vendor, ep, ct);
            await _archiver.ArchiveRawResponseAsync(vendor.Id, vendor.VendorName, ep.BaseUrl, fetchResult.ResponseBody, 0, ct);

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

        return (rawResponses, isFetchSuccessful, firstFailureReason);
    }

    private async Task HandleEndpointFailureAsync(
        VendorConfig vendor,
        VendorEndpointConfig ep,
        VendorFetchResult fetchResult,
        string failureReason,
        CancellationToken ct)
    {
        await _healthTracker.RecordFailureAsync(vendor, ep, fetchResult.StatusCode, failureReason, ct);

        if (fetchResult.StatusCode == 429)
        {
            s_nextAllowedVendorCallTimes[vendor.Id] = DateTime.UtcNow.AddMinutes(3);
            _logger.LogWarning("Vendor {VendorName} returned HTTP 429 (Too Many Requests). Applied 3-minute backoff cooldown to prevent IP blacklisting.", vendor.VendorName);
        }
        else if (fetchResult.StatusCode == 403)
        {
            s_nextAllowedVendorCallTimes[vendor.Id] = DateTime.UtcNow.AddMinutes(5);
            _logger.LogWarning("Vendor {VendorName} returned HTTP 403 (Forbidden / WAF Challenge). Applied 5-minute backoff cooldown.", vendor.VendorName);
        }

        await _apiLogRepo.LogApiCallAsync(new GpsApiLogRecord
        {
            FunctionName = ep.BaseUrl,
            Status = "0",
            ErrorMessage = failureReason,
            Parameter = fetchResult.ResponseBody,
            Username = vendor.VendorName,
            CreatedBy = "GpsCollector"
        }, ct);
    }

    private List<TelemetryPoint> NormalizeRawResponses(
        VendorConfig vendor,
        List<string> rawResponses,
        bool isFetchSuccessful)
    {
        var allPoints = new List<TelemetryPoint>();
        if (!isFetchSuccessful || rawResponses.Count == 0)
        {
            return allPoints;
        }

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

        return allPoints;
    }

    private async Task NotifyConnectionErrorAsync(
        VendorConfig vendor,
        List<DeliveryProgressSession> activeSessions,
        string? failureReason,
        CancellationToken ct)
    {
        foreach (var session in activeSessions)
        {
            await _cache.UpdateDeliveryLiveStatusAsync(
                session.DeliveryNo,
                "VENDOR_CONNECTION_ERROR",
                $"Gagal terhubung ke GPS Vendor {vendor.VendorName}: {failureReason}",
                ct);
        }
    }

    private async Task<int> ProcessMatchedDeliveriesAsync(
        VendorConfig vendor,
        List<TelemetryPoint> allPoints,
        List<DeliveryProgressSession> activeSessions,
        CancellationToken ct)
    {
        var matchedDeliveries = new List<(TelemetryPoint Point, DeliveryProgressSession Session)>();

        foreach (var session in activeSessions)
        {
            var matchedPoint = FindMatchingPoint(session, allPoints);
            if (matchedPoint != null)
            {
                matchedDeliveries.Add((matchedPoint, session));
                await _cache.UpdateDeliveryLivePositionAsync(session.DeliveryNo, matchedPoint, "TRACKING_ACTIVE", ct);
            }
            else
            {
                await RecordCorrelationMissAsync(vendor, session, allPoints.Count, ct);
            }
        }

        var totalDeliveryCount = 0;
        if (matchedDeliveries.Count > 0)
        {
            totalDeliveryCount = await IngestAndPublishDeliveriesAsync(vendor, matchedDeliveries, ct);
        }

        if (allPoints.Count > 0)
        {
            await _cache.SetLastPositionsAsync(vendor.VendorName, allPoints, ct);
        }

        return totalDeliveryCount;
    }

    private async Task RecordCorrelationMissAsync(
        VendorConfig vendor,
        DeliveryProgressSession session,
        int totalPointsCount,
        CancellationToken ct)
    {
        _logger.LogWarning(
            "Kategori 2 (Data Mismatch / Correlation Miss): Armada delivery '{DeliveryNo}' tidak ditemukan pada respons vendor {VendorCode} (LPCD: {Lpcd}, MatchKey: {MatchKey}, MatchVal: {MatchValue})",
            session.DeliveryNo, vendor.VendorCode, session.Lpcd, session.MatchKey, session.MatchValue);

        await _apiLogRepo.LogApiCallAsync(new GpsApiLogRecord
        {
            FunctionName = "DELIVERY_CORRELATION_MISS",
            Status = "WARNING",
            ErrorMessage = $"Armada untuk Delivery '{session.DeliveryNo}' tidak ditemukan di respons GPS vendor {vendor.VendorCode}. Filter: LPCD='{session.Lpcd}', Key='{session.MatchKey}', Val='{session.MatchValue}'. Kemungkinan GPS unit offline/mati, salah input identitas saat Start Job, atau format respons vendor berubah.",
            ErrorResponse = $"Total data points vendor diterima: {totalPointsCount}",
            Parameter = $"{{\"deliveryNo\":\"{session.DeliveryNo}\",\"lpcd\":\"{session.Lpcd}\",\"vendorCode\":\"{vendor.VendorCode}\",\"matchKey\":\"{session.MatchKey}\",\"matchValue\":\"{session.MatchValue}\"}}",
            Username = vendor.VendorName,
            CreatedBy = "GpsCollector"
        }, ct);

        await _cache.UpdateDeliveryLiveStatusAsync(
            session.DeliveryNo,
            "DATA_MISMATCH",
            $"Armada tidak ditemukan pada respons vendor {vendor.VendorCode}. Kemungkinan GPS vendor offline atau format data tidak sesuai.",
            ct);
    }

    private async Task<int> IngestAndPublishDeliveriesAsync(
        VendorConfig vendor,
        List<(TelemetryPoint Point, DeliveryProgressSession Session)> matchedDeliveries,
        CancellationToken ct)
    {
        var totalDeliveryCount = await _ingestionRepo.IngestDeliveryPositionsAsync(
            vendor.Id,
            vendor.VendorName,
            matchedDeliveries,
            ct);

        if (totalDeliveryCount == 0)
        {
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

        return totalDeliveryCount;
    }

    private static TelemetryPoint? FindMatchingPoint(DeliveryProgressSession session, List<TelemetryPoint> points)
    {
        var cleanSessionVal = !string.IsNullOrWhiteSpace(session.CleanMatchValue)
            ? session.CleanMatchValue
            : CleanKey(session.MatchValue);

        return points.FirstOrDefault(p => IsPointMatchingSession(p, session, cleanSessionVal));
    }

    private static bool IsPointMatchingSession(TelemetryPoint p, DeliveryProgressSession session, string cleanSessionVal)
    {
        var key = session.MatchKey;
        if (string.Equals(key, "PlatNo", StringComparison.OrdinalIgnoreCase))
        {
            return CleanKey(p.PlatNo) == cleanSessionVal ||
                   string.Equals(p.PlatNo, session.MatchValue, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(key, "VehicleId", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "DeviceId", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(key, "Imei", StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(p.DeviceId, session.MatchValue, StringComparison.OrdinalIgnoreCase);
        }

        // Default / Fallback matching
        return CleanKey(p.PlatNo) == cleanSessionVal ||
               string.Equals(p.DeviceId, session.MatchValue, StringComparison.OrdinalIgnoreCase);
    }

    private static string CleanKey(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        return System.Text.RegularExpressions.Regex.Replace(input.ToUpperInvariant(), @"[\s\-_]+", "", System.Text.RegularExpressions.RegexOptions.None, TimeSpan.FromSeconds(1));
    }
}
