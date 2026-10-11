using System;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Shared.Infrastructure.Idempotency;

public class RedisIdempotencyStore : IIdempotencyStore
{
    private const string KeyPrefix = "idempotency:";
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisIdempotencyStore> _logger;

    // In-memory fallback for local development or when Redis is disconnected
    private readonly ConcurrentDictionary<string, (IdempotencyRecord Record, DateTime ExpiresAtUtc)> _memoryFallback = new();

    public RedisIdempotencyStore(
        ILogger<RedisIdempotencyStore> logger,
        IConnectionMultiplexer? redis = null)
    {
        _logger = logger;
        _redis = redis;
    }

    public async Task<IdempotencyResult> TryAcquireAsync(
        string key,
        string requestHash,
        TimeSpan inFlightTtl,
        CancellationToken cancellationToken = default)
    {
        var redisKey = KeyPrefix + key;

        if (_redis != null && _redis.IsConnected)
        {
            var redisResult = await TryAcquireFromRedisAsync(redisKey, requestHash, inFlightTtl);
            if (redisResult != null)
            {
                return redisResult;
            }
        }

        return TryAcquireFromMemory(key, requestHash, inFlightTtl);
    }

    private async Task<IdempotencyResult?> TryAcquireFromRedisAsync(
        string redisKey,
        string requestHash,
        TimeSpan inFlightTtl)
    {
        try
        {
            var db = _redis!.GetDatabase();
            var initialRecord = new IdempotencyRecord
            {
                Status = "IN_FLIGHT",
                RequestHash = requestHash,
                CreatedAtUtc = DateTime.UtcNow
            };

            var json = JsonSerializer.Serialize(initialRecord);
            var acquired = await db.StringSetAsync(redisKey, json, inFlightTtl, When.NotExists);
            if (acquired)
            {
                return IdempotencyResult.Acquired();
            }

            var existingVal = await db.StringGetAsync(redisKey);
            if (existingVal.IsNullOrEmpty)
            {
                return IdempotencyResult.Acquired();
            }

            var existingRecord = JsonSerializer.Deserialize<IdempotencyRecord>(existingVal.ToString());
            return existingRecord != null ? EvaluateExistingRecord(existingRecord, requestHash) : IdempotencyResult.Acquired();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Idempotency] Redis error during TryAcquire. Falling back to memory store.");
            return null;
        }
    }

    private IdempotencyResult TryAcquireFromMemory(
        string key,
        string requestHash,
        TimeSpan inFlightTtl)
    {
        CleanupMemoryStore();
        var now = DateTime.UtcNow;

        if (_memoryFallback.TryGetValue(key, out var existingMemory))
        {
            if (existingMemory.ExpiresAtUtc > now)
            {
                return EvaluateExistingRecord(existingMemory.Record, requestHash);
            }

            _memoryFallback.TryRemove(key, out _);
        }

        var memoryRecord = new IdempotencyRecord
        {
            Status = "IN_FLIGHT",
            RequestHash = requestHash,
            CreatedAtUtc = now
        };

        _memoryFallback[key] = (memoryRecord, now.Add(inFlightTtl));
        return IdempotencyResult.Acquired();
    }

    private static IdempotencyResult EvaluateExistingRecord(IdempotencyRecord existingRecord, string requestHash)
    {
        if (string.Equals(existingRecord.Status, "IN_FLIGHT", StringComparison.OrdinalIgnoreCase))
        {
            return IdempotencyResult.InFlight();
        }

        if (!string.Equals(existingRecord.RequestHash, requestHash, StringComparison.Ordinal))
        {
            return IdempotencyResult.Mismatch();
        }

        return IdempotencyResult.Cached(
            existingRecord.StatusCode,
            existingRecord.ContentType,
            existingRecord.ResponseBody);
    }

    public async Task CompleteAsync(
        string key,
        string requestHash,
        int statusCode,
        string? contentType,
        string? responseBody,
        TimeSpan completedTtl,
        CancellationToken cancellationToken = default)
    {
        var redisKey = KeyPrefix + key;
        var record = new IdempotencyRecord
        {
            Status = "COMPLETED",
            RequestHash = requestHash,
            StatusCode = statusCode,
            ContentType = contentType,
            ResponseBody = responseBody,
            CreatedAtUtc = DateTime.UtcNow
        };

        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                var json = JsonSerializer.Serialize(record);
                await db.StringSetAsync(redisKey, json, completedTtl);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Idempotency] Redis error during CompleteAsync. Falling back to memory store.");
            }
        }

        _memoryFallback[key] = (record, DateTime.UtcNow.Add(completedTtl));
    }

    public async Task ReleaseLockAsync(string key, CancellationToken cancellationToken = default)
    {
        var redisKey = KeyPrefix + key;

        if (_redis != null && _redis.IsConnected)
        {
            try
            {
                var db = _redis.GetDatabase();
                await db.KeyDeleteAsync(redisKey);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Idempotency] Redis error during ReleaseLockAsync.");
            }
        }

        _memoryFallback.TryRemove(key, out _);
    }

    private void CleanupMemoryStore()
    {
        var now = DateTime.UtcNow;
        foreach (var kvp in _memoryFallback)
        {
            if (kvp.Value.ExpiresAtUtc <= now)
            {
                _memoryFallback.TryRemove(kvp.Key, out _);
            }
        }
    }
}
