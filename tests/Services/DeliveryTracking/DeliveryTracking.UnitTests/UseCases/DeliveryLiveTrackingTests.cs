using System.Text.Json;
using DeliveryTracking.Application.DTOs;
using DeliveryTracking.Application.Ports;
using DeliveryTracking.Application.UseCases.GetDeliveryLiveTracking;
using DeliveryTracking.Domain.Entities;
using DeliveryTracking.Domain.Exceptions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace DeliveryTracking.UnitTests.UseCases;

public class DeliveryLiveTrackingTests
{
    private readonly Mock<IConnectionMultiplexer> _redisMock = new();
    private readonly Mock<IDatabase> _dbMock = new();
    private readonly Mock<IDeliveryTrackingRepository> _repositoryMock = new();
    private readonly Mock<ILogger<GetDeliveryLiveTrackingQueryHandler>> _loggerMock = new();

    public DeliveryLiveTrackingTests()
    {
        _redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(_dbMock.Object);
    }

    [Fact]
    public async Task Handle_KetikaDataAdaDiRedis_HarusReturnStatusDanPosisiDariRedis()
    {
        // arrange
        var deliveryNo = "DELIV-1001";
        var redisPayload = JsonSerializer.Serialize(new
        {
            deliveryNo,
            trackingStatus = "TRACKING_ACTIVE",
            platNo = "B9999XYZ",
            latitude = -6.200000m,
            longitude = 106.816666m,
            speed = 55.5m,
            streetName = "Tol Jakarta Cikampek",
            updatedAt = DateTime.UtcNow
        });

        _dbMock.Setup(d => d.StringGetAsync($"delivery:live:{deliveryNo}", It.IsAny<CommandFlags>()))
            .ReturnsAsync(redisPayload);

        var handler = new GetDeliveryLiveTrackingQueryHandler(_redisMock.Object, _repositoryMock.Object, _loggerMock.Object);

        // act
        var result = await handler.Handle(new GetDeliveryLiveTrackingQuery(deliveryNo), CancellationToken.None);

        // assert
        result.Should().NotBeNull();
        result.DeliveryNo.Should().Be(deliveryNo);
        result.TrackingStatus.Should().Be("TRACKING_ACTIVE");
        result.PlatNo.Should().Be("B9999XYZ");
        result.Latitude.Should().Be(-6.200000m);
        result.DataSource.Should().Be("Redis");
    }

    [Fact]
    public async Task Handle_KetikaRedisAdaStatusDataMismatch_HarusReturnStatusDataMismatch()
    {
        // arrange
        var deliveryNo = "DELIV-1002";
        var redisPayload = JsonSerializer.Serialize(new
        {
            deliveryNo,
            trackingStatus = "DATA_MISMATCH",
            statusMessage = "Armada tidak ditemukan pada respons vendor PNR.",
            lpcd = "AJL",
            vendorCode = "PNR",
            key = "PlatNo",
            val = "B1234ABC",
            updatedAt = DateTime.UtcNow
        });

        _dbMock.Setup(d => d.StringGetAsync($"delivery:live:{deliveryNo}", It.IsAny<CommandFlags>()))
            .ReturnsAsync(redisPayload);

        var handler = new GetDeliveryLiveTrackingQueryHandler(_redisMock.Object, _repositoryMock.Object, _loggerMock.Object);

        // act
        var result = await handler.Handle(new GetDeliveryLiveTrackingQuery(deliveryNo), CancellationToken.None);

        // assert
        result.Should().NotBeNull();
        result.DeliveryNo.Should().Be(deliveryNo);
        result.TrackingStatus.Should().Be("DATA_MISMATCH");
        result.StatusMessage.Should().Contain("tidak ditemukan");
        result.DataSource.Should().Be("Redis");
    }

    [Fact]
    public async Task Handle_KetikaRedisKosongDanAdaPosisiDiDb_HarusReturnDariDb()
    {
        // arrange
        var deliveryNo = "DELIV-1003";
        _dbMock.Setup(d => d.StringGetAsync($"delivery:live:{deliveryNo}", It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var session = DeliveryTrackingSession.Create(deliveryNo, "AJL", "PlatNo", "B1234ABC", "B1234ABC");
        _repositoryMock.Setup(r => r.GetDeliverySessionByDeliveryNoAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var latestPos = new DeliveryLatestPositionDto(
            deliveryNo,
            session.Id,
            Guid.NewGuid(),
            "B1234ABC",
            null,
            DateTime.UtcNow,
            106.816666m,
            -6.200000m,
            60m,
            180m,
            "Jl. Tol Karawang",
            DateTime.UtcNow
        );
        _repositoryMock.Setup(r => r.GetDeliveryLatestPositionAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestPos);

        var handler = new GetDeliveryLiveTrackingQueryHandler(_redisMock.Object, _repositoryMock.Object, _loggerMock.Object);

        // act
        var result = await handler.Handle(new GetDeliveryLiveTrackingQuery(deliveryNo), CancellationToken.None);

        // assert
        result.Should().NotBeNull();
        result.DeliveryNo.Should().Be(deliveryNo);
        result.TrackingStatus.Should().Be("TRACKING_ACTIVE");
        result.DataSource.Should().Be("PostgreSQL");
        result.Latitude.Should().Be(-6.200000m);
    }

    [Fact]
    public async Task Handle_KetikaRedisKosongDanAdaLogCorrelationMiss_HarusReturnDataMismatchDariDbLog()
    {
        // arrange
        var deliveryNo = "DELIV-1004";
        _dbMock.Setup(d => d.StringGetAsync($"delivery:live:{deliveryNo}", It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var session = DeliveryTrackingSession.Create(deliveryNo, "AJL", "PlatNo", "B9999XYZ", "B9999XYZ");
        _repositoryMock.Setup(r => r.GetDeliverySessionByDeliveryNoAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _repositoryMock.Setup(r => r.GetDeliveryLatestPositionAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryLatestPositionDto?)null);

        var missLog = new GpsCorrelationMissLogDto(
            "WARNING",
            "Armada tidak ditemukan di respons vendor. Kemungkinan GPS offline.",
            "0 points",
            DateTime.UtcNow
        );
        _repositoryMock.Setup(r => r.GetLatestCorrelationMissLogAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(missLog);

        var handler = new GetDeliveryLiveTrackingQueryHandler(_redisMock.Object, _repositoryMock.Object, _loggerMock.Object);

        // act
        var result = await handler.Handle(new GetDeliveryLiveTrackingQuery(deliveryNo), CancellationToken.None);

        // assert
        result.Should().NotBeNull();
        result.DeliveryNo.Should().Be(deliveryNo);
        result.TrackingStatus.Should().Be("DATA_MISMATCH");
        result.StatusMessage.Should().Contain("tidak ditemukan di respons vendor");
        result.DataSource.Should().Be("PostgreSQL");
    }

    [Fact]
    public async Task Handle_KetikaSesiTidakDitemukan_HarusLemparDeliveryProgressNotFoundException()
    {
        // arrange
        var deliveryNo = "DELIV-UNKNOWN";
        _dbMock.Setup(d => d.StringGetAsync($"delivery:live:{deliveryNo}", It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        _repositoryMock.Setup(r => r.GetDeliverySessionByDeliveryNoAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryTrackingSession?)null);

        var handler = new GetDeliveryLiveTrackingQueryHandler(_redisMock.Object, _repositoryMock.Object, _loggerMock.Object);

        // act
        var act = () => handler.Handle(new GetDeliveryLiveTrackingQuery(deliveryNo), CancellationToken.None);

        // assert
        await act.Should().ThrowAsync<DeliveryProgressNotFoundException>();
    }

    [Fact]
    public async Task Handle_KetikaRedisKosongDanTidakAdaPosisiMaupunMissLog_HarusReturnWaitingTelemetry()
    {
        // arrange
        var deliveryNo = "DELIV-WAITING";
        _dbMock.Setup(d => d.StringGetAsync($"delivery:live:{deliveryNo}", It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var session = DeliveryTrackingSession.Create(deliveryNo, "LPCD-01", "PlatNo", "B1234XYZ", "B1234XYZ");
        _repositoryMock.Setup(r => r.GetDeliverySessionByDeliveryNoAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _repositoryMock.Setup(r => r.GetDeliveryLatestPositionAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryLatestPositionDto?)null);

        _repositoryMock.Setup(r => r.GetLatestCorrelationMissLogAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GpsCorrelationMissLogDto?)null);

        var handler = new GetDeliveryLiveTrackingQueryHandler(_redisMock.Object, _repositoryMock.Object, _loggerMock.Object);

        // act
        var result = await handler.Handle(new GetDeliveryLiveTrackingQuery(deliveryNo), CancellationToken.None);

        // assert
        result.Should().NotBeNull();
        result.TrackingStatus.Should().Be("WAITING_TELEMETRY");
        result.StatusMessage.Should().Contain("Menunggu data telemetri");
    }

    [Fact]
    public async Task Handle_KetikaSesiSudahCompleted_HarusReturnStatusCompleted()
    {
        // arrange
        var deliveryNo = "DELIV-COMPLETED";
        _dbMock.Setup(d => d.StringGetAsync($"delivery:live:{deliveryNo}", It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var session = DeliveryTrackingSession.Create(deliveryNo, "LPCD-01", "PlatNo", "B1234XYZ", "B1234XYZ");
        session.Status = "COMPLETED";
        session.CompletedAt = DateTime.UtcNow;
        session.CompletedBy = "Driver";
        session.CompletionSource = "MOBILE";

        _repositoryMock.Setup(r => r.GetDeliverySessionByDeliveryNoAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        var latestPos = new DeliveryLatestPositionDto(
            deliveryNo,
            session.Id,
            Guid.NewGuid(),
            "B 1234 XYZ",
            "DEV-01",
            DateTime.UtcNow,
            106.8m,
            -6.2m,
            40.0m,
            180.0m,
            "Jl Sudirman",
            DateTime.UtcNow
        );
        _repositoryMock.Setup(r => r.GetDeliveryLatestPositionAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(latestPos);

        var handler = new GetDeliveryLiveTrackingQueryHandler(_redisMock.Object, _repositoryMock.Object, _loggerMock.Object);

        // act
        var result = await handler.Handle(new GetDeliveryLiveTrackingQuery(deliveryNo), CancellationToken.None);

        // assert
        result.Should().NotBeNull();
        result.TrackingStatus.Should().Be("COMPLETED");
        result.StatusMessage.Should().Be("Pengiriman telah selesai");
    }

    [Fact]
    public async Task Handle_KetikaDeviceIdMatchKeyDanWaiting_HarusSetDeviceIdDanStatus()
    {
        // arrange
        var deliveryNo = "DELIV-DEV-MATCH";
        _dbMock.Setup(d => d.StringGetAsync($"delivery:live:{deliveryNo}", It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var session = DeliveryTrackingSession.Create(deliveryNo, "LPCD-02", "DeviceId", "DEV-100", "DEV-100");
        session.Status = "COMPLETED";

        _repositoryMock.Setup(r => r.GetDeliverySessionByDeliveryNoAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _repositoryMock.Setup(r => r.GetDeliveryLatestPositionAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryLatestPositionDto?)null);

        var missLog = new GpsCorrelationMissLogDto("WARNING", null, null, DateTime.UtcNow);
        _repositoryMock.Setup(r => r.GetLatestCorrelationMissLogAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(missLog);

        var handler = new GetDeliveryLiveTrackingQueryHandler(_redisMock.Object, _repositoryMock.Object, _loggerMock.Object);

        // act
        var result = await handler.Handle(new GetDeliveryLiveTrackingQuery(deliveryNo), CancellationToken.None);

        // assert
        result.Should().NotBeNull();
        result.DeviceId.Should().Be("DEV-100");
        result.PlatNo.Should().BeNull();
        result.TrackingStatus.Should().Be("DATA_MISMATCH");
    }

    [Fact]
    public async Task Handle_KetikaWaitingSessionStatusCompleted_HarusReturnCompleted()
    {
        // arrange
        var deliveryNo = "DELIV-WAITING-COMPLETED";
        _dbMock.Setup(d => d.StringGetAsync($"delivery:live:{deliveryNo}", It.IsAny<CommandFlags>()))
            .ReturnsAsync(RedisValue.Null);

        var session = DeliveryTrackingSession.Create(deliveryNo, "LPCD-02", "DeviceId", "DEV-200", "DEV-200");
        session.Status = "COMPLETED";

        _repositoryMock.Setup(r => r.GetDeliverySessionByDeliveryNoAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync(session);

        _repositoryMock.Setup(r => r.GetDeliveryLatestPositionAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync((DeliveryLatestPositionDto?)null);

        _repositoryMock.Setup(r => r.GetLatestCorrelationMissLogAsync(deliveryNo, It.IsAny<CancellationToken>()))
            .ReturnsAsync((GpsCorrelationMissLogDto?)null);

        var handler = new GetDeliveryLiveTrackingQueryHandler(_redisMock.Object, _repositoryMock.Object, _loggerMock.Object);

        // act
        var result = await handler.Handle(new GetDeliveryLiveTrackingQuery(deliveryNo), CancellationToken.None);

        // assert
        result.Should().NotBeNull();
        result.DeviceId.Should().Be("DEV-200");
        result.TrackingStatus.Should().Be("COMPLETED");
        result.StatusMessage.Should().Be("Pengiriman telah selesai");
    }
}

