using System.Text;
using FluentAssertions;
using GpsCollector.Domain.Models;
using GpsCollector.Infrastructure.Adapters.Cache;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using StackExchange.Redis;

namespace GpsCollector.UnitTests.Adapters;

public class RedisTelemetryCacheTests
{
    [Fact]
    public async Task UpdateDeliveryLivePositionAsync_HarusSimpanKeyDanPublishKeRedis()
    {
        // arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
        redisMock.SetupGet(r => r.IsConnected).Returns(true);

        var cache = new RedisTelemetryCache(NullLogger<RedisTelemetryCache>.Instance, redisMock.Object, null);

        var point = new TelemetryPoint
        {
            Id = Guid.NewGuid(),
            HeaderId = Guid.NewGuid(),
            Lpcd = "LPCD-JKT-01",
            PlatNo = "B 1234 ABC",
            DeviceId = "DEV-01",
            Datetime = DateTime.UtcNow,
            X = 106.82m,
            Y = -6.21m,
            Speed = 55m,
            Course = 90m,
            StreetName = "Jl. Sudirman"
        };

        // act
        await cache.UpdateDeliveryLivePositionAsync("DO-TEST-001", point, "IN_PROGRESS", CancellationToken.None);

        // assert
        dbMock.Verify(d => d.StringSetAsync(
            It.Is<RedisKey>(k => k == "delivery:live:DO-TEST-001"),
            It.Is<RedisValue>(v => v.ToString().Contains("B 1234 ABC")),
            It.IsAny<TimeSpan?>(),
            It.IsAny<bool>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.Once);

        dbMock.Verify(d => d.PublishAsync(
            It.Is<RedisChannel>(c => c == RedisChannel.Literal("delivery:live:DO-TEST-001")),
            It.IsAny<RedisValue>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task UpdateDeliveryLiveStatusAsync_HarusSimpanStatusDanPublish()
    {
        // arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);
        redisMock.SetupGet(r => r.IsConnected).Returns(true);

        var cache = new RedisTelemetryCache(NullLogger<RedisTelemetryCache>.Instance, redisMock.Object, null);

        // act
        await cache.UpdateDeliveryLiveStatusAsync("DO-COMPLETED-999", "COMPLETED", null, CancellationToken.None);

        // assert
        dbMock.Verify(d => d.StringSetAsync(
            It.Is<RedisKey>(k => k == "delivery:live:DO-COMPLETED-999"),
            It.Is<RedisValue>(v => v.ToString().Contains("COMPLETED")),
            It.IsAny<TimeSpan?>(),
            It.IsAny<bool>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task SetLastPositionsAsync_HarusSimpanSnapshotPlatNomor()
    {
        // arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);

        var cache = new RedisTelemetryCache(NullLogger<RedisTelemetryCache>.Instance, redisMock.Object, null);

        var points = new List<TelemetryPoint>
        {
            new()
            {
                Id = Guid.NewGuid(),
                HeaderId = Guid.NewGuid(),
                Lpcd = "LPCD-1",
                PlatNo = "B 9999 XYZ",
                DeviceId = "DEV-99",
                Datetime = DateTime.UtcNow,
                X = 106.8m,
                Y = -6.2m,
                Speed = 40m,
                Course = 180m,
                StreetName = "Jl. Gatot Subroto"
            }
        };

        // act
        await cache.SetLastPositionsAsync("Vendor Tracker", points, CancellationToken.None);

        // assert
        dbMock.Verify(d => d.StringSetAsync(
            It.Is<RedisKey>(k => k == "gps:last:B 9999 XYZ"),
            It.IsAny<RedisValue>(),
            It.IsAny<TimeSpan?>(),
            It.IsAny<bool>(),
            It.IsAny<When>(),
            It.IsAny<CommandFlags>()), Times.Once);
    }

    [Fact]
    public async Task SetRedisKeyAsync_KetikaRedisNull_HarusFallbackKeDistributedCache()
    {
        // arrange
        var distCacheMock = new Mock<IDistributedCache>();
        var cache = new RedisTelemetryCache(NullLogger<RedisTelemetryCache>.Instance, null, distCacheMock.Object);

        var point = new TelemetryPoint
        {
            Id = Guid.NewGuid(),
            HeaderId = Guid.NewGuid(),
            Lpcd = "LPCD-1",
            PlatNo = "D 1234 ABC",
            DeviceId = "DEV-1",
            Datetime = DateTime.UtcNow,
            X = 107.6m,
            Y = -6.9m,
            Speed = 30m,
            Course = 0m,
            StreetName = "Jl. Asia Afrika"
        };

        // act
        await cache.UpdateDeliveryLivePositionAsync("DO-DIST-CACHE-1", point, "IN_PROGRESS", CancellationToken.None);

        // assert
        distCacheMock.Verify(c => c.SetAsync(
            It.Is<string>(k => k == "delivery:live:DO-DIST-CACHE-1"),
            It.IsAny<byte[]>(),
            It.IsAny<DistributedCacheEntryOptions>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateDeliveryLivePositionAsync_KetikaExceptionRedis_HarusTanganiGracefully()
    {
        // arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Throws(new RedisConnectionException(ConnectionFailureType.UnableToConnect, "Server down"));

        var cache = new RedisTelemetryCache(NullLogger<RedisTelemetryCache>.Instance, redisMock.Object, null);

        var point = new TelemetryPoint
        {
            Id = Guid.NewGuid(),
            HeaderId = Guid.NewGuid(),
            Lpcd = "LPCD-1",
            PlatNo = "B 1234 ABC",
            DeviceId = "DEV-1",
            Datetime = DateTime.UtcNow,
            X = 106.8m,
            Y = -6.2m,
            Speed = 0m,
            Course = 0m,
            StreetName = "Jl. Thamrin"
        };

        // act - tidak boleh throw exception / crash
        var act = async () => await cache.UpdateDeliveryLivePositionAsync("DO-ERR-001", point, "IN_PROGRESS", CancellationToken.None);

        // assert
        await act.Should().NotThrowAsync();
    }
}
