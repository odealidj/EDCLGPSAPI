using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Shared.Infrastructure.LeaderElection;

public class RedisLeaderElection : ILeaderElection
{
    private const string DefaultLockKey = "edcl:collector:leader";
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<RedisLeaderElection> _logger;
    private readonly string _lockKey;
    private readonly string _nodeId;
    private bool _isLeader;

    public bool IsLeader => _isLeader;

    public RedisLeaderElection(
        ILogger<RedisLeaderElection> logger,
        IConnectionMultiplexer? redis = null,
        string lockKey = DefaultLockKey)
    {
        _logger = logger;
        _redis = redis;
        _lockKey = lockKey;
        _nodeId = Environment.GetEnvironmentVariable("HOSTNAME") 
            ?? Environment.MachineName 
            ?? Guid.NewGuid().ToString("N");
    }

    public async Task<bool> TryAcquireAsync(TimeSpan ttl, CancellationToken ct = default)
    {
        if (_redis == null || !_redis.IsConnected)
        {
            // Standalone fallback: if Redis is not configured or offline, act as single-instance leader
            _isLeader = true;
            return true;
        }

        try
        {
            var db = _redis.GetDatabase();
            var acquired = await db.StringSetAsync(_lockKey, _nodeId, ttl, When.NotExists);
            if (acquired)
            {
                _isLeader = true;
                _logger.LogInformation("[LeaderElection] Node {NodeId} successfully acquired leader lease on {LockKey} for {TtlSeconds}s", 
                    _nodeId, _lockKey, ttl.TotalSeconds);
                return true;
            }

            var currentLeader = await db.StringGetAsync(_lockKey);
            if (currentLeader == _nodeId)
            {
                _isLeader = true;
                await db.KeyExpireAsync(_lockKey, ttl);
                return true;
            }

            _isLeader = false;
            _logger.LogDebug("[LeaderElection] Node {NodeId} is STANDBY (Active leader: {CurrentLeader})", 
                _nodeId, (string?)currentLeader);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LeaderElection] Redis communication error. Running in fallback leader mode.");
            _isLeader = true;
            return true;
        }
    }

    public async Task<bool> RenewAsync(TimeSpan ttl, CancellationToken ct = default)
    {
        if (_redis == null || !_redis.IsConnected) return true;

        try
        {
            var db = _redis.GetDatabase();
            var currentLeader = await db.StringGetAsync(_lockKey);
            if (currentLeader == _nodeId)
            {
                await db.KeyExpireAsync(_lockKey, ttl);
                return true;
            }

            _isLeader = false;
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LeaderElection] Failed to renew leader lease for {NodeId}", _nodeId);
            return false;
        }
    }

    public async Task ReleaseAsync(CancellationToken ct = default)
    {
        if (_redis == null || !_redis.IsConnected) return;

        try
        {
            var db = _redis.GetDatabase();
            var currentLeader = await db.StringGetAsync(_lockKey);
            if (currentLeader == _nodeId)
            {
                await db.KeyDeleteAsync(_lockKey);
                _isLeader = false;
                _logger.LogInformation("[LeaderElection] Node {NodeId} gracefully released leader lock on {LockKey}", 
                    _nodeId, _lockKey);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[LeaderElection] Error releasing leader lock for {NodeId}", _nodeId);
        }
    }
}
