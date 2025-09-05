using System;

namespace Shared.Infrastructure.Idempotency;

public enum IdempotencyStatus
{
    Acquired,
    InFlightConflict,
    Completed,
    PayloadMismatch
}

public class IdempotencyRecord
{
    public string Status { get; set; } = "IN_FLIGHT"; // "IN_FLIGHT" | "COMPLETED"
    public string RequestHash { get; set; } = string.Empty;
    public int StatusCode { get; set; } = 200;
    public string? ContentType { get; set; }
    public string? ResponseBody { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public record IdempotencyResult
{
    public IdempotencyStatus Status { get; init; }
    public int? StatusCode { get; init; }
    public string? ContentType { get; init; }
    public string? ResponseBody { get; init; }

    public static IdempotencyResult Acquired() => new() { Status = IdempotencyStatus.Acquired };
    public static IdempotencyResult InFlight() => new() { Status = IdempotencyStatus.InFlightConflict };
    public static IdempotencyResult Mismatch() => new() { Status = IdempotencyStatus.PayloadMismatch };
    public static IdempotencyResult Cached(int statusCode, string? contentType, string? responseBody) =>
        new()
        {
            Status = IdempotencyStatus.Completed,
            StatusCode = statusCode,
            ContentType = contentType,
            ResponseBody = responseBody
        };
}
