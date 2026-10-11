using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Shared.Infrastructure.Idempotency;
using StackExchange.Redis;

namespace DeliveryTracking.UnitTests.Adapters;

public class RedisIdempotencyStoreTests
{
    [Fact]
    public async Task TryAcquireAsync_DenganMemoryFallback_PertamaKali_HarusAcquired()
    {
        // arrange
        var store = new RedisIdempotencyStore(NullLogger<RedisIdempotencyStore>.Instance, redis: null);

        // act
        var result = await store.TryAcquireAsync("key-1", "hash-abc", TimeSpan.FromMinutes(1));

        // assert
        result.Status.Should().Be(IdempotencyStatus.Acquired);
    }

    [Fact]
    public async Task TryAcquireAsync_DenganMemoryFallback_SedangInFlight_HarusInFlight()
    {
        // arrange
        var store = new RedisIdempotencyStore(NullLogger<RedisIdempotencyStore>.Instance, redis: null);
        await store.TryAcquireAsync("key-2", "hash-abc", TimeSpan.FromMinutes(1));

        // act
        var secondResult = await store.TryAcquireAsync("key-2", "hash-abc", TimeSpan.FromMinutes(1));

        // assert
        secondResult.Status.Should().Be(IdempotencyStatus.InFlightConflict);
    }

    [Fact]
    public async Task CompleteAsync_KemudianTryAcquireDenganHashSama_HarusReturnCached()
    {
        // arrange
        var store = new RedisIdempotencyStore(NullLogger<RedisIdempotencyStore>.Instance, redis: null);
        await store.TryAcquireAsync("key-3", "hash-abc", TimeSpan.FromMinutes(1));
        await store.CompleteAsync("key-3", "hash-abc", 200, "application/json", "{\"status\":\"ok\"}", TimeSpan.FromMinutes(5));

        // act
        var cachedResult = await store.TryAcquireAsync("key-3", "hash-abc", TimeSpan.FromMinutes(1));

        // assert
        cachedResult.Status.Should().Be(IdempotencyStatus.Completed);
        cachedResult.StatusCode.Should().Be(200);
        cachedResult.ResponseBody.Should().Be("{\"status\":\"ok\"}");
    }

    [Fact]
    public async Task TryAcquireAsync_KetikaRequestHashBerbeda_HarusReturnMismatch()
    {
        // arrange
        var store = new RedisIdempotencyStore(NullLogger<RedisIdempotencyStore>.Instance, redis: null);
        await store.TryAcquireAsync("key-4", "hash-original", TimeSpan.FromMinutes(1));
        await store.CompleteAsync("key-4", "hash-original", 200, "application/json", "{\"status\":\"ok\"}", TimeSpan.FromMinutes(5));

        // act
        var mismatchResult = await store.TryAcquireAsync("key-4", "hash-different", TimeSpan.FromMinutes(1));

        // assert
        mismatchResult.Status.Should().Be(IdempotencyStatus.PayloadMismatch);
    }

    [Fact]
    public async Task TryAcquireAsync_DenganRedisMock_KetikaLockAcquired_HarusAcquired()
    {
        // arrange
        var redisMock = new Mock<IConnectionMultiplexer>();
        var dbMock = new Mock<IDatabase>();
        redisMock.SetupGet(r => r.IsConnected).Returns(true);
        redisMock.Setup(r => r.GetDatabase(It.IsAny<int>(), It.IsAny<object>())).Returns(dbMock.Object);

        dbMock.SetReturnsDefault<Task<bool>>(Task.FromResult(true));

        var store = new RedisIdempotencyStore(NullLogger<RedisIdempotencyStore>.Instance, redisMock.Object);

        // act
        var result = await store.TryAcquireAsync("key-redis", "hash-redis", TimeSpan.FromMinutes(1));

        // assert
        result.Status.Should().Be(IdempotencyStatus.Acquired);
    }
}
