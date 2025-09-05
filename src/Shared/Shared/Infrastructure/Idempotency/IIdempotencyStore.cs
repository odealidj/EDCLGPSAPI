using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Infrastructure.Idempotency;

public interface IIdempotencyStore
{
    Task<IdempotencyResult> TryAcquireAsync(
        string key,
        string requestHash,
        TimeSpan inFlightTtl,
        CancellationToken cancellationToken = default);

    Task CompleteAsync(
        string key,
        string requestHash,
        int statusCode,
        string? contentType,
        string? responseBody,
        TimeSpan completedTtl,
        CancellationToken cancellationToken = default);

    Task ReleaseLockAsync(string key, CancellationToken cancellationToken = default);
}
