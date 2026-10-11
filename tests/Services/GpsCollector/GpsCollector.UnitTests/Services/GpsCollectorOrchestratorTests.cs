using FluentAssertions;
using GpsCollector.Application.Ports;
using GpsCollector.Application.Services;
using GpsCollector.Domain.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace GpsCollector.UnitTests.Services;

public class GpsCollectorOrchestratorTests
{
    private readonly Mock<IVendorConfigRepository> _vendorConfigRepo = new();
    private readonly Mock<IDeliverySessionRepository> _deliverySessionRepo = new();
    private readonly Mock<IVendorHttpFetcher> _httpFetcher = new();
    private readonly Mock<ITelemetryNormalizer> _normalizer = new();
    private readonly Mock<ITelemetryIngestionRepository> _ingestionRepo = new();
    private readonly Mock<ITelemetryCache> _cache = new();
    private readonly Mock<ITelemetryArchiver> _archiver = new();
    private readonly Mock<ITelemetryEventPublisher> _publisher = new();
    private readonly Mock<IGpsApiLogRepository> _apiLogRepo = new();
    private readonly Mock<IVendorHealthTracker> _healthTracker = new();

    private readonly GpsCollectorOrchestrator _orchestrator;

    public GpsCollectorOrchestratorTests()
    {
        _orchestrator = new GpsCollectorOrchestrator(
            _vendorConfigRepo.Object,
            _deliverySessionRepo.Object,
            _httpFetcher.Object,
            _normalizer.Object,
            _ingestionRepo.Object,
            _cache.Object,
            _archiver.Object,
            _publisher.Object,
            _apiLogRepo.Object,
            _healthTracker.Object,
            NullLogger<GpsCollectorOrchestrator>.Instance
        );
    }

    [Fact]
    public async Task CollectVendorAsync_WhenSuccessful_ShouldIngestCacheAndPublish()
    {
        var vendorId = Guid.NewGuid();
        var vendor = new VendorConfig
        {
            Id = vendorId,
            VendorName = "EasyGo",
            ProcessingStrategy = "individual",
            ProcessingStrategyPathData = "data",
            Endpoints =
            [
                new VendorEndpointConfig
                {
                    Id = Guid.NewGuid(),
                    GpsVendorId = vendorId,
                    EndpointName = "LastPos",
                    BaseUrl = "https://api.easygo.id/pos",
                    Method = "GET"
                }
            ],
            Mappings =
            [
                new VendorFieldMapping { GpsVendorId = vendorId, ResponseField = "plat", MappedField = "PlatNo" }
            ]
        };

        var rawJson = "{\"data\":[{\"plat\":\"B 1234 CD\"}]}";
        var normalizedPoints = new List<TelemetryPoint>
        {
            new() { PlatNo = "B 1234 CD", Datetime = DateTime.UtcNow, X = 106.8m, Y = -6.2m }
        };

        _vendorConfigRepo.Setup(r => r.GetVendorByIdAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        _httpFetcher.Setup(f => f.FetchEndpointAsync(vendor, vendor.Endpoints[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VendorFetchResult(true, 200, "OK", rawJson, "https://api.easygo.id/pos"));

        _normalizer.Setup(n => n.Normalize(vendorId, rawJson, "data", vendor.Mappings, vendor.Endpoints[0].BaseUrl))
            .Returns(normalizedPoints);

        var activeSessions = new Dictionary<string, DeliveryProgressSession>(StringComparer.OrdinalIgnoreCase)
        {
            ["B 1234 CD"] = new() { PlatNo = "B 1234 CD", DeliveryNo = "DELIV-001", Lpcd = "LP01", NoKtp = "12345678" }
        };

        _deliverySessionRepo.Setup(s => s.GetActiveSessionsForVendorAsync(vendorId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeSessions.Values.ToList());
        _deliverySessionRepo.Setup(s => s.GetActiveSessionsByPlatNosAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeSessions);

        _ingestionRepo.Setup(i => i.IngestTelemetryBatchAsync(
                vendorId,
                vendor.VendorName,
                It.IsAny<Guid>(),
                normalizedPoints,
                activeSessions,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((1, 1));

        var result = await _orchestrator.CollectVendorAsync(vendorId, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PointsCount.Should().Be(1);
        result.DeliveryPointsCount.Should().Be(1);

        _archiver.Verify(a => a.ArchiveRawResponseAsync(vendorId, vendor.VendorName, vendor.Endpoints[0].BaseUrl, rawJson, 0, It.IsAny<CancellationToken>()), Times.Once);
        _ingestionRepo.Verify(i => i.IngestTelemetryBatchAsync(vendorId, vendor.VendorName, It.IsAny<Guid>(), normalizedPoints, activeSessions, It.IsAny<CancellationToken>()), Times.Once);
        _cache.Verify(c => c.SetLastPositionsAsync(vendor.VendorName, normalizedPoints, It.IsAny<CancellationToken>()), Times.Once);
        _publisher.Verify(p => p.PublishTelemetryEventAsync(vendorId, vendor.VendorName, It.IsAny<Guid>(), normalizedPoints, It.IsAny<CancellationToken>()), Times.Once);
        _healthTracker.Verify(h => h.RecordSuccessAsync(vendor, vendor.Endpoints[0], It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CollectVendorAsync_WhenHttpFails_ShouldLogToApiLogRepository()
    {
        var vendorId = Guid.NewGuid();
        var vendor = new VendorConfig
        {
            Id = vendorId,
            VendorName = "Cartrack",
            ProcessingStrategy = "individual",
            Endpoints =
            [
                new VendorEndpointConfig
                {
                    Id = Guid.NewGuid(),
                    GpsVendorId = vendorId,
                    EndpointName = "LastPos",
                    BaseUrl = "https://api.cartrack.com/pos",
                    Method = "GET"
                }
            ]
        };

        _vendorConfigRepo.Setup(r => r.GetVendorByIdAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        _httpFetcher.Setup(f => f.FetchEndpointAsync(vendor, vendor.Endpoints[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VendorFetchResult(false, 503, "Service Unavailable", "Gateway timeout", "https://api.cartrack.com/pos"));

        var result = await _orchestrator.CollectVendorAsync(vendorId, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PointsCount.Should().Be(0);

        _apiLogRepo.Verify(l => l.LogApiCallAsync(It.Is<GpsApiLogRecord>(r =>
            r.FunctionName == "https://api.cartrack.com/pos" &&
            r.Status == "0" &&
            r.ErrorMessage!.Contains("503")
        ), It.IsAny<CancellationToken>()), Times.Once);

        _ingestionRepo.Verify(i => i.IngestTelemetryBatchAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<TelemetryPoint>>(), It.IsAny<IReadOnlyDictionary<string, DeliveryProgressSession>>(), It.IsAny<CancellationToken>()), Times.Never);
        _healthTracker.Verify(h => h.RecordFailureAsync(vendor, vendor.Endpoints[0], 503, It.Is<string>(s => s.Contains("503")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CollectVendorAsync_WhenNoActiveDeliveries_ShouldArchiveToMongo_AndSkipDeliveryIngestion()
    {
        // arrange - Opsi A: raw response tetap diarsipkan ke MongoDB, tapi tidak dinormalisasi/disimpan ke PostgreSQL delivery
        var vendorId = Guid.NewGuid();
        var vendor = new VendorConfig
        {
            Id = vendorId,
            VendorCode = "HINO",
            VendorName = "HINO-CONNECT",
            Lpcd = "AJL",
            Endpoints =
            [
                new VendorEndpointConfig
                {
                    Id = Guid.NewGuid(),
                    GpsVendorId = vendorId,
                    EndpointName = "LastPos",
                    BaseUrl = "https://api.hino.co.id/pos",
                    Method = "GET"
                }
            ]
        };

        var rawJson = "{\"data\":[{\"plat\":\"B 9001 FXR\",\"lat\":-6.2,\"lng\":106.8}]}";

        _vendorConfigRepo.Setup(r => r.GetVendorByIdAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        _httpFetcher.Setup(f => f.FetchEndpointAsync(vendor, vendor.Endpoints[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VendorFetchResult(true, 200, "OK", rawJson, "https://api.hino.co.id/pos"));

        // Tidak ada sesi delivery aktif untuk vendor ini
        _deliverySessionRepo.Setup(s => s.GetActiveSessionsForVendorAsync(vendorId, "AJL", It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // act
        var result = await _orchestrator.CollectVendorAsync(vendorId, CancellationToken.None);

        // assert
        result.Success.Should().BeTrue();
        result.DeliveryPointsCount.Should().Be(0);

        // Raw response selalu tersimpan di MongoDB
        _archiver.Verify(a => a.ArchiveRawResponseAsync(vendorId, vendor.VendorName, vendor.Endpoints[0].BaseUrl, rawJson, 0, It.IsAny<CancellationToken>()), Times.Once);

        // Tidak ada penyimpanan ke tabel delivery PostgreSQL
        _ingestionRepo.Verify(i => i.IngestDeliveryPositionsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<(TelemetryPoint, DeliveryProgressSession)>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CollectVendorAsync_WhenHttpFailsWithActiveSession_Kategori1_ShouldUpdateLiveStatusToVendorConnectionError()
    {
        // arrange - Kategori 1: Gagal koneksi ke API vendor saat ada delivery aktif
        var vendorId = Guid.NewGuid();
        var vendor = new VendorConfig
        {
            Id = vendorId,
            VendorCode = "HINO",
            VendorName = "HINO-CONNECT",
            Lpcd = "AJL",
            Endpoints =
            [
                new VendorEndpointConfig
                {
                    Id = Guid.NewGuid(),
                    GpsVendorId = vendorId,
                    EndpointName = "LastPos",
                    BaseUrl = "https://api.hino.co.id/pos",
                    Method = "GET"
                }
            ]
        };

        var activeSessions = new List<DeliveryProgressSession>
        {
            new() { DeliveryNo = "DO-001", Lpcd = "AJL", VendorId = vendorId, MatchKey = "PlatNo", MatchValue = "B 9001 FXR", CleanMatchValue = "B9001FXR" }
        };

        _vendorConfigRepo.Setup(r => r.GetVendorByIdAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        _httpFetcher.Setup(f => f.FetchEndpointAsync(vendor, vendor.Endpoints[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VendorFetchResult(false, 504, "Gateway Timeout", "Timed out", "https://api.hino.co.id/pos"));

        _deliverySessionRepo.Setup(s => s.GetActiveSessionsForVendorAsync(vendorId, "AJL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeSessions);

        // act
        var result = await _orchestrator.CollectVendorAsync(vendorId, CancellationToken.None);

        // assert
        result.Success.Should().BeTrue();

        // Log dicatat ke edcl.m_gps_api_log
        _apiLogRepo.Verify(l => l.LogApiCallAsync(It.Is<GpsApiLogRecord>(r =>
            r.FunctionName == "https://api.hino.co.id/pos" &&
            r.Status == "0" &&
            r.ErrorMessage!.Contains("504")
        ), It.IsAny<CancellationToken>()), Times.Once);

        // Live cache di Redis ditandai VENDOR_CONNECTION_ERROR
        _cache.Verify(c => c.UpdateDeliveryLiveStatusAsync(
            "DO-001",
            "VENDOR_CONNECTION_ERROR",
            It.Is<string>(msg => msg.Contains("504")),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task CollectVendorAsync_WhenDataMismatch_Kategori2_ShouldCancelIngestion_LogWarning_AndUpdateLiveStatus()
    {
        // arrange - Kategori 2: HTTP 200 OK dari vendor, tapi armada tidak ditemukan
        // (bisa karena GPS mati, salah input plat/identitas saat Start Job, atau respons JSON vendor berubah)
        var vendorId = Guid.NewGuid();
        var vendor = new VendorConfig
        {
            Id = vendorId,
            VendorCode = "HINO",
            VendorName = "HINO-CONNECT",
            Lpcd = "AJL",
            Endpoints =
            [
                new VendorEndpointConfig
                {
                    Id = Guid.NewGuid(),
                    GpsVendorId = vendorId,
                    EndpointName = "LastPos",
                    BaseUrl = "https://api.hino.co.id/pos",
                    Method = "GET"
                }
            ]
        };

        // Vendor mengirim data truk lain (misal plat B 1111 AAA), bukan B 9001 FXR
        var rawJson = "{\"data\":[{\"plat\":\"B 1111 AAA\",\"lat\":-6.2,\"lng\":106.8}]}";
        var normalizedPoints = new List<TelemetryPoint>
        {
            new() { PlatNo = "B 1111 AAA", Datetime = DateTime.UtcNow, X = 106.8m, Y = -6.2m }
        };

        var activeSessions = new List<DeliveryProgressSession>
        {
            new() { DeliveryNo = "DO-001", Lpcd = "AJL", VendorId = vendorId, MatchKey = "PlatNo", MatchValue = "B 9001 FXR", CleanMatchValue = "B9001FXR" }
        };

        _vendorConfigRepo.Setup(r => r.GetVendorByIdAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        _httpFetcher.Setup(f => f.FetchEndpointAsync(vendor, vendor.Endpoints[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VendorFetchResult(true, 200, "OK", rawJson, "https://api.hino.co.id/pos"));

        _normalizer.Setup(n => n.Normalize(vendorId, rawJson, "data", vendor.Mappings, vendor.Endpoints[0].BaseUrl))
            .Returns(normalizedPoints);

        _deliverySessionRepo.Setup(s => s.GetActiveSessionsForVendorAsync(vendorId, "AJL", It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeSessions);

        // act
        var result = await _orchestrator.CollectVendorAsync(vendorId, CancellationToken.None);

        // assert
        result.Success.Should().BeTrue();
        result.DeliveryPointsCount.Should().Be(0);

        // Raw response tetap aman di MongoDB
        _archiver.Verify(a => a.ArchiveRawResponseAsync(vendorId, vendor.VendorName, vendor.Endpoints[0].BaseUrl, rawJson, 0, It.IsAny<CancellationToken>()), Times.Once);

        // Batal simpan ke tabel delivery
        _ingestionRepo.Verify(i => i.IngestDeliveryPositionsAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<(TelemetryPoint, DeliveryProgressSession)>>(), It.IsAny<CancellationToken>()), Times.Never);

        // Laporkan Kategori 2 ke edcl.m_gps_api_log sebagai WARNING
        _apiLogRepo.Verify(l => l.LogApiCallAsync(It.Is<GpsApiLogRecord>(r =>
            r.FunctionName == "DELIVERY_CORRELATION_MISS" &&
            r.Status == "WARNING" &&
            r.ErrorMessage!.Contains("DO-001") &&
            r.ErrorMessage!.Contains("B 9001 FXR")
        ), It.IsAny<CancellationToken>()), Times.Once);

        // Update live status di Redis ke DATA_MISMATCH
        _cache.Verify(c => c.UpdateDeliveryLiveStatusAsync(
            "DO-001",
            "DATA_MISMATCH",
            It.Is<string>(msg => msg.Contains("Armada tidak ditemukan")),
            It.IsAny<CancellationToken>()
        ), Times.Once);
    }

    [Fact]
    public async Task CollectVendorAsync_WhenVendorNotFound_ShouldReturnErrorMessage()
    {
        var vendorId = Guid.NewGuid();
        _vendorConfigRepo.Setup(r => r.GetVendorByIdAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((VendorConfig?)null);

        var result = await _orchestrator.CollectVendorAsync(vendorId, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task CollectVendorAsync_WhenVendorHasNoEndpoints_ShouldReturnEmptySuccess()
    {
        var vendorId = Guid.NewGuid();
        var vendor = new VendorConfig
        {
            Id = vendorId,
            VendorName = "NoEndpointVendor",
            Endpoints = []
        };

        _vendorConfigRepo.Setup(r => r.GetVendorByIdAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        var result = await _orchestrator.CollectVendorAsync(vendorId, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PointsCount.Should().Be(0);
    }

    [Fact]
    public async Task CollectVendorAsync_WhenHttpReturns429Or403_ShouldApplyBackoffCooldown()
    {
        var vendorId = Guid.NewGuid();
        var vendor = new VendorConfig
        {
            Id = vendorId,
            VendorName = "RateLimitedVendor",
            Endpoints =
            [
                new VendorEndpointConfig
                {
                    Id = Guid.NewGuid(),
                    GpsVendorId = vendorId,
                    EndpointName = "Pos",
                    BaseUrl = "https://api.test.com/pos",
                    Method = "GET"
                }
            ]
        };

        _vendorConfigRepo.Setup(r => r.GetVendorByIdAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        // Test 429
        _httpFetcher.Setup(f => f.FetchEndpointAsync(vendor, vendor.Endpoints[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VendorFetchResult(false, 429, "Too Many Requests", "Limit reached", "https://api.test.com/pos"));

        var result429 = await _orchestrator.CollectVendorAsync(vendorId, CancellationToken.None);
        result429.Success.Should().BeTrue();

        // Test 403
        _httpFetcher.Setup(f => f.FetchEndpointAsync(vendor, vendor.Endpoints[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VendorFetchResult(false, 403, "Forbidden", "WAF Block", "https://api.test.com/pos"));

        var result403 = await _orchestrator.CollectVendorAsync(vendorId, CancellationToken.None);
        result403.Success.Should().BeTrue();
    }

    [Fact]
    public async Task CollectVendorAsync_WithCombinedStrategyAndMaxPath_ShouldProcessCorrectly()
    {
        var vendorId = Guid.NewGuid();
        var epId = Guid.NewGuid();
        var vendor = new VendorConfig
        {
            Id = vendorId,
            VendorName = "CombinedVendor",
            ProcessingStrategy = "combined",
            ProcessingStrategyPathKey = "vin",
            ProcessingStrategyPathData = "items",
            Endpoints =
            [
                new VendorEndpointConfig
                {
                    Id = epId,
                    GpsVendorId = vendorId,
                    EndpointName = "CombPos",
                    BaseUrl = "https://api.combined.com/pos",
                    Method = "GET",
                    MaxPath = "max_id"
                }
            ]
        };

        var rawJson = "{\"items\":[{\"vin\":\"V1\"}],\"max_id\":999}";
        var points = new List<TelemetryPoint>
        {
            new() { DeviceId = "V1", Datetime = DateTime.UtcNow, X = 106.8m, Y = -6.2m }
        };

        var activeSessions = new List<DeliveryProgressSession>
        {
            new() { DeliveryNo = "DO-COMB", MatchKey = "DeviceId", MatchValue = "V1", CleanMatchValue = "V1" }
        };

        _vendorConfigRepo.Setup(r => r.GetVendorByIdAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        _httpFetcher.Setup(f => f.FetchEndpointAsync(vendor, vendor.Endpoints[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VendorFetchResult(true, 200, "OK", rawJson, "https://api.combined.com/pos"));

        _normalizer.Setup(n => n.ExtractMaxProperty(rawJson, "items", "max_id"))
            .Returns(999);

        _normalizer.Setup(n => n.NormalizeCombined(vendorId, It.IsAny<List<string>>(), "vin", "items", vendor.Mappings, vendor.Endpoints[0].BaseUrl))
            .Returns(points);

        _deliverySessionRepo.Setup(s => s.GetActiveSessionsForVendorAsync(vendorId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeSessions);

        _ingestionRepo.Setup(i => i.IngestDeliveryPositionsAsync(vendorId, vendor.VendorName, It.IsAny<IReadOnlyList<(TelemetryPoint, DeliveryProgressSession)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _orchestrator.CollectVendorAsync(vendorId, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.PointsCount.Should().Be(1);
        result.DeliveryPointsCount.Should().Be(1);

        _vendorConfigRepo.Verify(r => r.UpdateVarParamsMaxIdAsync(epId, "max_id", 999, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CollectAllVendorsAsync_WhenActiveVendorsPresent_ShouldProcessEachVendor()
    {
        var vendor1 = new VendorConfig
        {
            Id = Guid.NewGuid(),
            VendorName = "Vendor1",
            Endpoints = []
        };
        var vendor2 = new VendorConfig
        {
            Id = Guid.NewGuid(),
            VendorName = "Vendor2",
            Endpoints = []
        };

        _vendorConfigRepo.Setup(r => r.GetActiveVendorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([vendor1, vendor2]);

        using var cts = new CancellationTokenSource();
        var results = await _orchestrator.CollectAllVendorsAsync(cts.Token);

        results.Should().HaveCount(2);
    }

    [Fact]
    public async Task CollectAllVendorsAsync_WhenNoActiveSessions_ShouldSkipPolling()
    {
        var vendorId = Guid.NewGuid();
        var vendor = new VendorConfig
        {
            Id = vendorId,
            VendorName = "NoSessionsVendor",
            Endpoints =
            [
                new VendorEndpointConfig
                {
                    Id = Guid.NewGuid(),
                    GpsVendorId = vendorId,
                    EndpointName = "Pos",
                    BaseUrl = "https://api.test.com/pos",
                    Method = "GET"
                }
            ]
        };

        _vendorConfigRepo.Setup(r => r.GetActiveVendorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([vendor]);

        _deliverySessionRepo.Setup(s => s.GetActiveSessionsForVendorAsync(vendorId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        using var cts = new CancellationTokenSource();
        var results = await _orchestrator.CollectAllVendorsAsync(cts.Token);

        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
        results[0].PointsCount.Should().Be(0);
    }

    [Fact]
    public async Task CollectAllVendorsAsync_WhenVendorInCooldown_ShouldSkipPolling()
    {
        var vendorId = Guid.NewGuid();
        var vendor = new VendorConfig
        {
            Id = vendorId,
            VendorName = "CooldownVendor",
            Endpoints =
            [
                new VendorEndpointConfig
                {
                    Id = Guid.NewGuid(),
                    GpsVendorId = vendorId,
                    EndpointName = "Pos",
                    BaseUrl = "https://api.test.com/pos",
                    Method = "GET"
                }
            ]
        };

        _vendorConfigRepo.Setup(r => r.GetVendorByIdAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        _httpFetcher.Setup(f => f.FetchEndpointAsync(vendor, vendor.Endpoints[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VendorFetchResult(false, 429, "Too Many Requests", "Limit", "https://api.test.com/pos"));

        // Trigger 429 to put vendor in cooldown
        await _orchestrator.CollectVendorAsync(vendorId, CancellationToken.None);

        // Now run CollectAllVendorsAsync with active sessions
        _vendorConfigRepo.Setup(r => r.GetActiveVendorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([vendor]);

        var activeSessions = new List<DeliveryProgressSession>
        {
            new() { DeliveryNo = "DO-001", MatchKey = "PlatNo", MatchValue = "B123" }
        };
        _deliverySessionRepo.Setup(s => s.GetActiveSessionsForVendorAsync(vendorId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(activeSessions);

        using var cts = new CancellationTokenSource();
        var results = await _orchestrator.CollectAllVendorsAsync(cts.Token);

        results.Should().HaveCount(1);
        results[0].Success.Should().BeTrue();
    }

    [Fact]
    public async Task CollectVendorAsync_KetikaFetchThrowsException_HarusTangkapDanReturnError()
    {
        var vendorId = Guid.NewGuid();
        var vendor = new VendorConfig
        {
            Id = vendorId,
            VendorName = "CrashingVendor",
            Endpoints =
            [
                new VendorEndpointConfig
                {
                    Id = Guid.NewGuid(),
                    GpsVendorId = vendorId,
                    EndpointName = "CrashPos",
                    BaseUrl = "https://crash.test.com",
                    Method = "GET"
                }
            ]
        };

        _vendorConfigRepo.Setup(r => r.GetVendorByIdAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        _httpFetcher.Setup(f => f.FetchEndpointAsync(vendor, vendor.Endpoints[0], It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Connection forcibly closed"));

        var result = await _orchestrator.CollectVendorAsync(vendorId, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Connection forcibly closed");
    }

    [Fact]
    public async Task CollectVendorAsync_KetikaCustomMatchKeyDanLegacySessions_HarusMatchDanIngest()
    {
        var vendorId = Guid.NewGuid();
        var vendor = new VendorConfig
        {
            Id = vendorId,
            VendorName = "CustomVendor",
            ProcessingStrategy = "individual",
            ProcessingStrategyPathData = "data",
            Endpoints =
            [
                new VendorEndpointConfig
                {
                    Id = Guid.NewGuid(),
                    GpsVendorId = vendorId,
                    EndpointName = "Pos",
                    BaseUrl = "https://custom.test.com/pos",
                    Method = "GET"
                }
            ]
        };

        var rawJson = "{\"data\":[{\"plat\":\"B 1111 ZZ\"}]}";
        var points = new List<TelemetryPoint>
        {
            new() { PlatNo = "B 1111 ZZ", DeviceId = "D999", Datetime = DateTime.UtcNow, X = 106.8m, Y = -6.2m }
        };

        _vendorConfigRepo.Setup(r => r.GetVendorByIdAsync(vendorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vendor);

        _httpFetcher.Setup(f => f.FetchEndpointAsync(vendor, vendor.Endpoints[0], It.IsAny<CancellationToken>()))
            .ReturnsAsync(new VendorFetchResult(true, 200, "OK", rawJson, "https://custom.test.com/pos"));

        _normalizer.Setup(n => n.Normalize(vendorId, rawJson, "data", vendor.Mappings, vendor.Endpoints[0].BaseUrl))
            .Returns(points);

        // GetActiveSessionsForVendorAsync returns empty, legacy GetActiveSessionsByPlatNosAsync returns data
        _deliverySessionRepo.Setup(s => s.GetActiveSessionsForVendorAsync(vendorId, It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var legacySession = new DeliveryProgressSession
        {
            DeliveryNo = "DO-LEGACY",
            PlatNo = "B 1111 ZZ",
            MatchKey = "CustomOtherKey",
            MatchValue = "B 1111 ZZ",
            CleanMatchValue = "B1111ZZ"
        };
        _deliverySessionRepo.Setup(s => s.GetActiveSessionsByPlatNosAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, DeliveryProgressSession> { ["B 1111 ZZ"] = legacySession });

        _ingestionRepo.Setup(i => i.IngestDeliveryPositionsAsync(vendorId, vendor.VendorName, It.IsAny<IReadOnlyList<(TelemetryPoint, DeliveryProgressSession)>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var result = await _orchestrator.CollectVendorAsync(vendorId, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.DeliveryPointsCount.Should().Be(1);
    }
}



