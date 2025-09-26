using GpsCollector.Application.DTOs;
using GpsCollector.Domain.Models;

namespace GpsCollector.Application.Ports;

// Port Inbound: Orkestrator proses pengumpulan telemetri GPS
public interface IGpsCollectorOrchestrator
{
    Task<List<VendorPollingResultDto>> CollectAllVendorsAsync(CancellationToken ct);
    Task<VendorPollingResultDto> CollectVendorAsync(Guid vendorId, CancellationToken ct);
}

// Port Outbound: Akses konfigurasi vendor dan delay sistem dari PostgreSQL
public interface IVendorConfigRepository
{
    Task<List<VendorConfig>> GetActiveVendorsAsync(CancellationToken ct);
    Task<VendorConfig?> GetVendorByIdAsync(Guid vendorId, CancellationToken ct);
    Task<int> GetPollingDelayMinutesAsync(CancellationToken ct);
    Task UpdateVarParamsMaxIdAsync(Guid endpointId, string propertyName, int maxId, CancellationToken ct);
}

// Port Outbound: Akses sesi delivery progress aktif armada
public interface IDeliverySessionRepository
{
    Task<List<DeliveryProgressSession>> GetActiveSessionsForVendorAsync(Guid vendorId, string? lpcd, CancellationToken ct);
    Task<Dictionary<string, DeliveryProgressSession>> GetActiveSessionsByPlatNosAsync(IEnumerable<string> platNos, CancellationToken ct);
}

// Hasil pemanggilan HTTP endpoint vendor
public sealed record VendorFetchResult(
    bool IsSuccess,
    int StatusCode,
    string? ReasonPhrase,
    string? ResponseBody,
    string? RequestUrl
);

// Port Outbound: Client HTTP pemanggil API vendor dengan retry Polly v8
public interface IVendorHttpFetcher
{
    Task<VendorFetchResult> FetchEndpointAsync(VendorConfig vendor, VendorEndpointConfig endpoint, CancellationToken ct);
}

// Port Outbound: Parser dan normalisasi JSON vendor tanpa refleksi
public interface ITelemetryNormalizer
{
    List<TelemetryPoint> Normalize(Guid vendorId, string rawJson, string dataPath, IReadOnlyList<VendorFieldMapping> mappings, string endpointUrl);
    List<TelemetryPoint> NormalizeCombined(Guid vendorId, IReadOnlyList<string> rawJsons, string keyPath, string dataPath, IReadOnlyList<VendorFieldMapping> mappings, string endpointUrl);
    int? ExtractMaxProperty(string rawJson, string dataPath, string maxPropertyPath);
}

// Port Outbound: Bulk ingestion PostgreSQL via Binary COPY stream
public interface ITelemetryIngestionRepository
{
    Task<int> IngestDeliveryPositionsAsync(
        Guid vendorId,
        string vendorName,
        IReadOnlyList<(TelemetryPoint Point, DeliveryProgressSession Session)> matchedDeliveries,
        CancellationToken ct);

    Task<(int TotalInserted, int DeliveryInserted)> IngestTelemetryBatchAsync(
        Guid vendorId,
        string vendorName,
        Guid headerId,
        IReadOnlyList<TelemetryPoint> points,
        IReadOnlyDictionary<string, DeliveryProgressSession> activeSessions,
        CancellationToken ct);
}

// Port Outbound: Cache snapshot posisi terkini di Redis
public interface ITelemetryCache
{
    Task UpdateDeliveryLivePositionAsync(string deliveryNo, TelemetryPoint point, string trackingStatus, CancellationToken ct);
    Task UpdateDeliveryLiveStatusAsync(string deliveryNo, string trackingStatus, string? errorMessage, CancellationToken ct);
    Task SetLastPositionsAsync(string vendorName, IReadOnlyList<TelemetryPoint> points, CancellationToken ct);
}

// Port Outbound: Arsip payload raw JSON ke MongoDB
public interface ITelemetryArchiver
{
    Task ArchiveRawResponseAsync(Guid vendorId, string vendorName, string endpointUrl, string rawJson, int recordCount, CancellationToken ct);
}

// Port Outbound: Publisher event telemetri ke RabbitMQ
public interface ITelemetryEventPublisher
{
    Task PublishTelemetryEventAsync(Guid vendorId, string vendorName, Guid headerId, IReadOnlyList<TelemetryPoint> points, CancellationToken ct);
}

// Port Outbound: Pencatatan log pemanggilan API ke tb_m_gps_api_log
public interface IGpsApiLogRepository
{
    Task LogApiCallAsync(GpsApiLogRecord logRecord, CancellationToken ct);
}

// Port Outbound / Core: Pelacak kesehatan koneksi endpoint vendor
public interface IVendorHealthTracker
{
    Task RecordFailureAsync(VendorConfig vendor, VendorEndpointConfig endpoint, int statusCode, string? errorMessage, CancellationToken ct);
    Task RecordSuccessAsync(VendorConfig vendor, VendorEndpointConfig endpoint, CancellationToken ct);
    int GetConsecutiveFailures(Guid vendorId, Guid endpointId);
    bool IsAlertActive(Guid vendorId, Guid endpointId);
}

// Port Outbound: Pengirim notifikasi alert kegagalan dan pemulihan koneksi vendor
public interface IVendorAlertNotifier
{
    Task NotifyAlertTriggeredAsync(VendorAlertEvent alertEvent, CancellationToken ct);
    Task NotifyAlertResolvedAsync(VendorAlertEvent alertEvent, CancellationToken ct);
}

