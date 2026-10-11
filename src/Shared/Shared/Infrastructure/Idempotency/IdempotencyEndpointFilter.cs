using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Shared.Infrastructure.Idempotency;

public class IdempotencyEndpointFilter(
    IIdempotencyStore store,
    ILogger logger,
    TimeSpan? customCompletedTtl = null) : IEndpointFilter
{
    public const string HeaderName = "X-Idempotency-Key";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var cancellationToken = httpContext.RequestAborted;

        // 1. Mandatory X-Idempotency-Key check
        var hasKey = (httpContext.Request.Headers.TryGetValue(HeaderName, out var keyVal) && !string.IsNullOrWhiteSpace(keyVal))
            || (httpContext.Request.Headers.TryGetValue("Idempotency-Key", out keyVal) && !string.IsNullOrWhiteSpace(keyVal));

        if (!hasKey)
        {
            logger.LogWarning("[Idempotency] Request rejected. Missing required header: {HeaderName} on {Path}",
                HeaderName, httpContext.Request.Path);

            return Results.Problem(
                title: "Missing Idempotency Key",
                detail: $"Header '{HeaderName}' is required for this operation.",
                statusCode: StatusCodes.Status400BadRequest,
                instance: httpContext.Request.Path);
        }

        var idempotencyKey = keyVal.ToString().Trim();
        var endpointPath = httpContext.Request.Path.ToString().ToLowerInvariant();
        var storeKey = $"{endpointPath}:{idempotencyKey}";

        // 2. Hash request payload (SHA-256)
        httpContext.Request.EnableBuffering();
        string requestBody = string.Empty;
        if (httpContext.Request.Body.CanSeek)
        {
            httpContext.Request.Body.Position = 0;
            using var reader = new StreamReader(httpContext.Request.Body, Encoding.UTF8, leaveOpen: true);
            requestBody = await reader.ReadToEndAsync(cancellationToken);
            httpContext.Request.Body.Position = 0;
        }

        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(requestBody)));
        var inFlightTtl = TimeSpan.FromSeconds(60);
        var completedTtl = customCompletedTtl ?? TimeSpan.FromHours(24);

        // 3. Try acquire atomic lock or retrieve cached response
        var acquireResult = await store.TryAcquireAsync(storeKey, requestHash, inFlightTtl, cancellationToken);

        if (acquireResult.Status == IdempotencyStatus.InFlightConflict)
        {
            logger.LogWarning("[Idempotency] Concurrent request detected for key: {Key} on {Path}", idempotencyKey, endpointPath);

            return Results.Problem(
                title: "Concurrent Request In Progress",
                detail: $"A request with this {HeaderName} is currently being processed. Please retry shortly.",
                statusCode: StatusCodes.Status409Conflict,
                instance: httpContext.Request.Path);
        }

        if (acquireResult.Status == IdempotencyStatus.PayloadMismatch)
        {
            logger.LogWarning("[Idempotency] Payload mismatch for reused key: {Key} on {Path}", idempotencyKey, endpointPath);

            return Results.Problem(
                title: "Idempotency Key Payload Mismatch",
                detail: $"{HeaderName} was already used with a different request payload.",
                statusCode: StatusCodes.Status422UnprocessableEntity,
                instance: httpContext.Request.Path);
        }

        if (acquireResult.Status == IdempotencyStatus.Completed)
        {
            logger.LogInformation("[Idempotency] Replaying cached response for key: {Key} on {Path}", idempotencyKey, endpointPath);

            return new CachedIdempotentResult(
                acquireResult.StatusCode ?? 200,
                acquireResult.ContentType ?? "application/json; charset=utf-8",
                acquireResult.ResponseBody ?? string.Empty);
        }

        // 4. Lock acquired: Execute endpoint pipeline
        httpContext.Response.Headers["X-Cache"] = "MISS";
        object? result;
        try
        {
            result = await next(context);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Idempotency] Exception during execution. Releasing lock for key: {Key}", idempotencyKey);
            await store.ReleaseLockAsync(storeKey, CancellationToken.None);
            throw;
        }

        return new IdempotencyResultExecutor(result, store, storeKey, requestHash, completedTtl, logger);
    }
}

public class CachedIdempotentResult(int statusCode, string contentType, string responseBody) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        httpContext.Response.StatusCode = statusCode;
        if (!string.IsNullOrEmpty(contentType))
        {
            httpContext.Response.ContentType = contentType;
        }

        httpContext.Response.Headers["X-Cache"] = "HIT";
        httpContext.Response.Headers["X-Idempotent-Replayed"] = "true";

        await httpContext.Response.WriteAsync(responseBody, Encoding.UTF8);
    }
}

public class IdempotencyResultExecutor(
    object? innerResult,
    IIdempotencyStore store,
    string storeKey,
    string requestHash,
    TimeSpan completedTtl,
    ILogger logger) : IResult
{
    public async Task ExecuteAsync(HttpContext httpContext)
    {
        var originalBodyStream = httpContext.Response.Body;
        await using var memoryStream = new MemoryStream();
        httpContext.Response.Body = memoryStream;

        try
        {
            if (innerResult is IResult result)
            {
                await result.ExecuteAsync(httpContext);
            }
            else if (innerResult != null)
            {
                await httpContext.Response.WriteAsJsonAsync(innerResult, httpContext.RequestAborted);
            }

            memoryStream.Position = 0;
            var responseBody = await new StreamReader(memoryStream, Encoding.UTF8).ReadToEndAsync();
            var statusCode = httpContext.Response.StatusCode;
            var contentType = httpContext.Response.ContentType;

            if (statusCode < 500)
            {
                await store.CompleteAsync(
                    storeKey,
                    requestHash,
                    statusCode,
                    contentType,
                    responseBody,
                    completedTtl,
                    httpContext.RequestAborted);
            }
            else
            {
                logger.LogWarning("[Idempotency] Status code {StatusCode} >= 500. Releasing lock for key: {StoreKey}",
                    statusCode, storeKey);
                await store.ReleaseLockAsync(storeKey, CancellationToken.None);
            }

            memoryStream.Position = 0;
            await memoryStream.CopyToAsync(originalBodyStream);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Idempotency] Error writing response. Releasing lock for key: {StoreKey}", storeKey);
            await store.ReleaseLockAsync(storeKey, CancellationToken.None);
            throw;
        }
        finally
        {
            httpContext.Response.Body = originalBodyStream;
        }
    }
}
