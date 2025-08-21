namespace Shared.Infrastructure.LeaderElection;

public interface ILeaderElection
{
    bool IsLeader { get; }
    Task<bool> TryAcquireAsync(TimeSpan ttl, CancellationToken ct = default);
    Task<bool> RenewAsync(TimeSpan ttl, CancellationToken ct = default);
    Task ReleaseAsync(CancellationToken ct = default);
}
