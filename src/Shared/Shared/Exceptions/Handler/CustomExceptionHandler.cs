using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Shared.Exceptions.Handler;
public class CustomExceptionHandler
    (ILogger<CustomExceptionHandler> logger)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        const string correlationHeader = "X-Correlation-Id";
        string requestId = traceId;
        if (context.Request.Headers.TryGetValue(correlationHeader, out var clientCorrId) &&
            !string.IsNullOrWhiteSpace(clientCorrId))
        {
            requestId = clientCorrId.ToString();
        }
        else if (context.Request.Headers.TryGetValue("X-Request-Id", out var clientReqId) &&
                 !string.IsNullOrWhiteSpace(clientReqId))
        {
            requestId = clientReqId.ToString();
        }
        else if (context.Request.Headers.TryGetValue("requestId", out var clientReqId2) &&
                 !string.IsNullOrWhiteSpace(clientReqId2))
        {
            requestId = clientReqId2.ToString();
        }

        context.Response.Headers["traceId"] = traceId;
        context.Response.Headers["requestId"] = requestId;
        context.Response.Headers["X-Trace-Id"] = traceId;
        context.Response.Headers["X-Request-Id"] = requestId;
        context.Response.Headers[correlationHeader] = requestId;

        logger.LogError(
            exception,
            "Error Message: {exceptionMessage}, TraceId: {traceId}, RequestId: {requestId}, Time: {time}",
            exception.Message, traceId, requestId, DateTime.UtcNow);

        (string Detail, string Title, int StatusCode) details = exception switch
        {
            InternalServerException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status500InternalServerError
            ),
            ValidationException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status400BadRequest
            ),
            BadRequestException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status400BadRequest
            ),
            NotFoundException =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status404NotFound
            ),
            _ =>
            (
                exception.Message,
                exception.GetType().Name,
                context.Response.StatusCode = StatusCodes.Status500InternalServerError
            )
        };

        var problemDetails = new ProblemDetails
        {
            Title = details.Title,
            Detail = details.Detail,
            Status = details.StatusCode,
            Instance = context.Request.Path
        };

        problemDetails.Extensions.Add("traceId", traceId);
        problemDetails.Extensions.Add("requestId", requestId);

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions.Add("ValidationErrors", validationException.Errors);
        }

        await context.Response.WriteAsJsonAsync(problemDetails, cancellationToken: cancellationToken);
        return true;
    }
}
