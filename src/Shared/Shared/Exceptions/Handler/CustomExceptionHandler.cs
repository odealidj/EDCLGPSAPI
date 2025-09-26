using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Shared.Exceptions;

namespace Shared.Exceptions.Handler;

// global exception handler buat bungkus error jadi format problem details (rfc 7807)
public class CustomExceptionHandler(
    ILogger<CustomExceptionHandler> logger,
    IHostEnvironment? env = null)
    : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        var traceId = System.Diagnostics.Activity.Current?.TraceId.ToString() ?? context.TraceIdentifier;

        const string correlationHeader = "X-Correlation-Id";
        string correlationId = traceId;
        if (context.Request.Headers.TryGetValue(correlationHeader, out var clientCorrId) &&
            !string.IsNullOrWhiteSpace(clientCorrId))
        {
            correlationId = clientCorrId.ToString();
        }
        else if (context.Request.Headers.TryGetValue("correlationId", out var clientCorrId2) &&
                 !string.IsNullOrWhiteSpace(clientCorrId2))
        {
            correlationId = clientCorrId2.ToString();
        }
        else if (context.Request.Headers.TryGetValue("X-Request-Id", out var clientReqId) &&
                 !string.IsNullOrWhiteSpace(clientReqId))
        {
            correlationId = clientReqId.ToString();
        }
        else if (context.Request.Headers.TryGetValue("requestId", out var clientReqId2) &&
                 !string.IsNullOrWhiteSpace(clientReqId2))
        {
            correlationId = clientReqId2.ToString();
        }

        context.Response.Headers["X-Trace-Id"] = traceId;
        context.Response.Headers[correlationHeader] = correlationId;

        logger.LogError(
            exception,
            "Error Message: {exceptionMessage}, TraceId: {traceId}, CorrelationId: {correlationId}, Time: {time}",
            exception.Message, traceId, correlationId, DateTime.UtcNow);

        (string Detail, string Title, int StatusCode, string Type) details = exception switch
        {
            ValidationException =>
            (
                exception.Message,
                "Validation Error",
                StatusCodes.Status400BadRequest,
                "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            ),
            BadRequestException =>
            (
                exception.Message,
                "Bad Request",
                StatusCodes.Status400BadRequest,
                "https://tools.ietf.org/html/rfc7231#section-6.5.1"
            ),
            NotFoundException =>
            (
                exception.Message,
                "Resource Not Found",
                StatusCodes.Status404NotFound,
                "https://tools.ietf.org/html/rfc7231#section-6.5.4"
            ),
            ConflictException =>
            (
                exception.Message,
                "Conflict",
                StatusCodes.Status409Conflict,
                "https://tools.ietf.org/html/rfc7231#section-6.5.8"
            ),
            DbUpdateException dbUpdateEx when dbUpdateEx.InnerException?.Message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) == true ||
                                             dbUpdateEx.InnerException?.Message.Contains("UNIQUE constraint", StringComparison.OrdinalIgnoreCase) == true =>
            (
                "Data yang dikirim bentrok dengan data yang sudah ada (duplikat unique key).",
                "Conflict",
                StatusCodes.Status409Conflict,
                "https://tools.ietf.org/html/rfc7231#section-6.5.8"
            ),
            InternalServerException =>
            (
                exception.Message,
                "Internal Server Error",
                StatusCodes.Status500InternalServerError,
                "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            ),
            _ =>
            (
                env != null && env.IsDevelopment()
                    ? exception.Message
                    : "Terjadi kesalahan internal pada server. Silakan hubungi admin dengan menyertakan TraceId.",
                "Internal Server Error",
                StatusCodes.Status500InternalServerError,
                "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            )
        };

        context.Response.StatusCode = details.StatusCode;
        context.Response.ContentType = "application/problem+json";

        var problemDetails = new ProblemDetails
        {
            Type = details.Type,
            Title = details.Title,
            Detail = details.Detail,
            Status = details.StatusCode,
            Instance = context.Request.Path
        };

        problemDetails.Extensions.Add("traceId", traceId);
        problemDetails.Extensions.Add("correlationId", correlationId);

        if (exception is ValidationException validationException)
        {
            problemDetails.Extensions.Add("ValidationErrors", validationException.Errors);
        }

        if (exception is BadRequestException badRequestEx && !string.IsNullOrWhiteSpace(badRequestEx.Details))
        {
            problemDetails.Extensions.Add("details", badRequestEx.Details);
        }
        else if (exception is ConflictException conflictEx && !string.IsNullOrWhiteSpace(conflictEx.Details))
        {
            problemDetails.Extensions.Add("details", conflictEx.Details);
        }
        else if (exception is InternalServerException internalEx && !string.IsNullOrWhiteSpace(internalEx.Details))
        {
            problemDetails.Extensions.Add("details", internalEx.Details);
        }

        await context.Response.WriteAsJsonAsync(
            problemDetails,
            options: (System.Text.Json.JsonSerializerOptions?)null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);
        return true;
    }
}
