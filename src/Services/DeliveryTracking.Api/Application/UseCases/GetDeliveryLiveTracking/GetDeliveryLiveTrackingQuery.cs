using System.Text.Json;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Ports;
using DeliveryTracking.Domain.Exceptions;
using FluentValidation;
using Microsoft.Extensions.Logging;
using Shared.Contracts.CQRS;
using StackExchange.Redis;

namespace DeliveryTracking.Application.UseCases.GetDeliveryLiveTracking;

public record GetDeliveryLiveTrackingQuery(string DeliveryNo)
    : IQuery<DeliveryLiveTrackingDto>;

public class GetDeliveryLiveTrackingQueryValidator : AbstractValidator<GetDeliveryLiveTrackingQuery>
{
    public GetDeliveryLiveTrackingQueryValidator()
    {
        RuleFor(x => x.DeliveryNo)
            .NotEmpty().WithMessage("Nomor Delivery / DO tidak boleh kosong.");
    }
}

public class GetDeliveryLiveTrackingQueryHandler(
    IConnectionMultiplexer redis,
    IDeliveryTrackingRepository repository,
    ILogger<GetDeliveryLiveTrackingQueryHandler> logger)
    : IQueryHandler<GetDeliveryLiveTrackingQuery, DeliveryLiveTrackingDto>
{
    public async Task<DeliveryLiveTrackingDto> Handle(
        GetDeliveryLiveTrackingQuery request,
        CancellationToken cancellationToken)
    {
        var deliveryNo = request.DeliveryNo.Trim().ToUpperInvariant();

        var redisResult = await TryGetFromRedisLiveCacheAsync(deliveryNo);
        if (redisResult != null)
        {
            return redisResult;
        }

        return await GetFromDatabaseFallbackAsync(deliveryNo, cancellationToken);
    }

    private async Task<DeliveryLiveTrackingDto?> TryGetFromRedisLiveCacheAsync(string deliveryNo)
    {
        try
        {
            var db = redis.GetDatabase();
            var liveJson = await db.StringGetAsync($"delivery:live:{deliveryNo}");
            if (liveJson.IsNullOrEmpty) return null;

            using var doc = JsonDocument.Parse((string)liveJson!);
            return ParseLiveTrackingDto(deliveryNo, doc.RootElement);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Gagal membaca live cache Redis untuk delivery '{DeliveryNo}'. Fallback ke PostgreSQL.", deliveryNo);
            return null;
        }
    }

    private DeliveryLiveTrackingDto ParseLiveTrackingDto(string deliveryNo, JsonElement root)
    {
        var trackingStatus = GetStringProp(root, "trackingStatus") ?? "WAITING_TELEMETRY";
        var statusMessage = GetStringProp(root, "statusMessage");
        var lpcd = GetStringProp(root, "lpcd");
        var vendorCode = GetStringProp(root, "vendorCode");
        var vendorName = GetStringProp(root, "vendorName");
        var platNo = GetStringProp(root, "platNo");
        var deviceId = GetStringProp(root, "deviceId");
        var matchKey = GetStringProp(root, "matchKey") ?? GetStringProp(root, "key");
        var matchValue = GetStringProp(root, "matchValue") ?? GetStringProp(root, "val");

        decimal? lat = GetDecimalProp(root, "latitude");
        decimal? lon = GetDecimalProp(root, "longitude");
        decimal? speed = GetDecimalProp(root, "speed");
        decimal? course = GetDecimalProp(root, "course");
        var streetName = GetStringProp(root, "streetName");
        DateTime? gpsDatetime = GetDateTimeProp(root, "datetime");
        DateTime? updatedAt = GetDateTimeProp(root, "updatedAt");

        logger.LogDebug("Redis LIVE HIT untuk delivery '{DeliveryNo}', status: {TrackingStatus}", deliveryNo, trackingStatus);

        return new DeliveryLiveTrackingDto(
            DeliveryNo: deliveryNo,
            TrackingStatus: trackingStatus,
            StatusMessage: statusMessage,
            Lpcd: lpcd,
            VendorCode: vendorCode,
            VendorName: vendorName,
            PlatNo: platNo,
            DeviceId: deviceId,
            MatchKey: matchKey,
            MatchValue: matchValue,
            Latitude: lat,
            Longitude: lon,
            Speed: speed,
            Course: course,
            StreetName: streetName,
            GpsDatetime: gpsDatetime,
            UpdatedAt: updatedAt,
            DataSource: "Redis"
        );
    }

    private async Task<DeliveryLiveTrackingDto> GetFromDatabaseFallbackAsync(string deliveryNo, CancellationToken cancellationToken)
    {
        var session = await repository.GetDeliverySessionByDeliveryNoAsync(deliveryNo, cancellationToken);
        if (session == null)
        {
            throw new DeliveryProgressNotFoundException(deliveryNo);
        }

        string? resolvedVendorName = null;
        string? resolvedVendorCode = null;
        if (!string.IsNullOrWhiteSpace(session.Lpcd))
        {
            var vendorLookup = await repository.GetVendorByLpcdAsync(session.Lpcd, cancellationToken);
            resolvedVendorName = vendorLookup?.GpsVendorName;
            resolvedVendorCode = vendorLookup?.GpsVendorCode;
        }

        var latestPos = await repository.GetDeliveryLatestPositionAsync(deliveryNo, cancellationToken);
        if (latestPos != null)
        {
            return CreateDtoFromPosition(deliveryNo, session, resolvedVendorCode, resolvedVendorName, latestPos);
        }

        var missLog = await repository.GetLatestCorrelationMissLogAsync(deliveryNo, cancellationToken);
        if (missLog != null)
        {
            return CreateDtoFromMissLog(deliveryNo, session, resolvedVendorCode, resolvedVendorName, missLog);
        }

        return CreateDtoFromWaitingSession(deliveryNo, session, resolvedVendorCode, resolvedVendorName);
    }

    private static DeliveryLiveTrackingDto CreateDtoFromPosition(
        string deliveryNo,
        DeliveryTrackingSession session,
        string? vendorCode,
        string? vendorName,
        DeliveryLatestPositionDto latestPos)
    {
        return new DeliveryLiveTrackingDto(
            DeliveryNo: deliveryNo,
            TrackingStatus: session.Status == "COMPLETED" ? "COMPLETED" : "TRACKING_ACTIVE",
            StatusMessage: session.Status == "COMPLETED" ? "Pengiriman telah selesai" : "Armada terdeteksi dan aktif terlacak",
            Lpcd: session.Lpcd,
            VendorCode: vendorCode,
            VendorName: vendorName,
            PlatNo: latestPos.PlateNumber,
            DeviceId: latestPos.VehicleId,
            MatchKey: session.MatchKey,
            MatchValue: session.MatchValue,
            Latitude: latestPos.Latitude,
            Longitude: latestPos.Longitude,
            Speed: latestPos.Speed,
            Course: latestPos.Course,
            StreetName: latestPos.StreetName,
            GpsDatetime: latestPos.Datetime,
            UpdatedAt: latestPos.UpdatedAt,
            DataSource: "PostgreSQL"
        );
    }

    private static DeliveryLiveTrackingDto CreateDtoFromMissLog(
        string deliveryNo,
        DeliveryTrackingSession session,
        string? vendorCode,
        string? vendorName,
        GpsCorrelationMissLogDto missLog)
    {
        var isPlat = session.MatchKey.Equals("PlatNo", StringComparison.OrdinalIgnoreCase);
        return new DeliveryLiveTrackingDto(
            DeliveryNo: deliveryNo,
            TrackingStatus: "DATA_MISMATCH",
            StatusMessage: missLog.ErrorMessage ?? "Armada belum terdeteksi di server GPS vendor (kemungkinan GPS mati/offline, salah input saat Start Job, atau respons vendor berubah).",
            Lpcd: session.Lpcd,
            VendorCode: vendorCode,
            VendorName: vendorName,
            PlatNo: isPlat ? session.MatchValue : null,
            DeviceId: !isPlat ? session.MatchValue : null,
            MatchKey: session.MatchKey,
            MatchValue: session.MatchValue,
            Latitude: null,
            Longitude: null,
            Speed: null,
            Course: null,
            StreetName: null,
            GpsDatetime: null,
            UpdatedAt: missLog.CreatedAt,
            DataSource: "PostgreSQL"
        );
    }

    private static DeliveryLiveTrackingDto CreateDtoFromWaitingSession(
        string deliveryNo,
        DeliveryTrackingSession session,
        string? vendorCode,
        string? vendorName)
    {
        var isPlat = session.MatchKey.Equals("PlatNo", StringComparison.OrdinalIgnoreCase);
        return new DeliveryLiveTrackingDto(
            DeliveryNo: deliveryNo,
            TrackingStatus: session.Status == "COMPLETED" ? "COMPLETED" : "WAITING_TELEMETRY",
            StatusMessage: session.Status == "COMPLETED" ? "Pengiriman telah selesai" : "Menunggu data telemetri pertama dari GPS vendor",
            Lpcd: session.Lpcd,
            VendorCode: vendorCode,
            VendorName: vendorName,
            PlatNo: isPlat ? session.MatchValue : null,
            DeviceId: !isPlat ? session.MatchValue : null,
            MatchKey: session.MatchKey,
            MatchValue: session.MatchValue,
            Latitude: null,
            Longitude: null,
            Speed: null,
            Course: null,
            StreetName: null,
            GpsDatetime: null,
            UpdatedAt: session.StartedAt,
            DataSource: "PostgreSQL"
        );
    }

    private static string? GetStringProp(JsonElement el, string prop) => el.TryGetProperty(prop, out var p) ? p.GetString() : null;
    private static decimal? GetDecimalProp(JsonElement el, string prop) => el.TryGetProperty(prop, out var p) && p.TryGetDecimal(out var val) ? val : null;
    private static DateTime? GetDateTimeProp(JsonElement el, string prop) => el.TryGetProperty(prop, out var p) && p.TryGetDateTime(out var val) ? val : null;
}
